using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Rezultat;
using Rezultat.Testing.Xunit;

var checks = new Checks();
var assembly = typeof(ResultAssertions).Assembly;

checks.That("exported types are exactly ResultAssertions and ValidationAssertions",
    assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal)
        .SequenceEqual(["Rezultat.Testing.Xunit.ResultAssertions", "Rezultat.Testing.Xunit.ValidationAssertions"]));
CheckOwner(typeof(ResultAssertions), typeof(Result<,>), ("ShouldBeOk", 0), ("ShouldBeError", 1));
CheckOwner(typeof(ValidationAssertions), typeof(Validation<,>), ("ShouldBeValid", 0), ("ShouldBeInvalid", -1));

var reference = new object();
checks.That("ShouldBeOk returns the exact reference", ReferenceEquals(reference, Result<object, string>.Ok(reference).ShouldBeOk()));
checks.That("ShouldBeError returns the exact reference", ReferenceEquals(reference, Result<int, object>.Error(reference).ShouldBeError()));
checks.That("ShouldBeValid returns the exact reference", ReferenceEquals(reference, Validation<object, string>.Valid(reference).ShouldBeValid()));
var issues = ImmutableArray.Create<string?>("b", null, "b");
checks.That("ShouldBeInvalid returns the stored issues",
    Validation<int, string?>.InvalidMany(issues).ShouldBeInvalid() == issues);
checks.That("static owner call form", ResultAssertions.ShouldBeOk(Result<int, string>.Ok(5)) == 5
    && ValidationAssertions.ShouldBeValid(Validation<int, string>.Valid(6)) == 6);
checks.That("nullable payload extracts null", Result<string?, string>.Ok(null).ShouldBeOk() is null);
checks.That("unconstrained generic caller", Extract(Result<int?, string>.Ok(null)) is null && Extract(Result<string, string>.Ok("x")).Length == 1);

CheckFailure("wrong branch", () => Result<int, string>.Error("evidence").ShouldBeOk(), "Result.Error(evidence)");
CheckFailure("uninitialized", () => default(Validation<int, string>).ShouldBeInvalid(), "Validation.Uninitialized");
CheckFailure("formatter", () => Result<Throwing, int>.Ok(new Throwing()).ShouldBeError(), "<ToString threw System.InvalidOperationException>");

return checks.Complete();

static T Extract<T>(Result<T, string> result) => result.ShouldBeOk();

void CheckOwner(Type owner, Type carrier, params (string Name, int Returned)[] expected)
{
    checks.That($"{owner.Name} is a public static extension owner",
        owner is { IsPublic: true, IsAbstract: true, IsSealed: true } && owner.IsDefined(typeof(ExtensionAttribute)));
    var methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    checks.That($"{owner.Name} declares exactly {string.Join(", ", expected.Select(item => item.Name))}",
        methods.Select(method => method.Name).Order(StringComparer.Ordinal).SequenceEqual(expected.Select(item => item.Name).Order(StringComparer.Ordinal)));
    foreach (var (name, returned) in expected)
    {
        var method = methods.SingleOrDefault(candidate => candidate.Name == name);
        if (method is null)
        {
            checks.Fail($"{owner.Name}.{name} missing");
            continue;
        }

        var arguments = method.GetGenericArguments();
        var parameter = method.GetParameters().Single().ParameterType;
        var returnType = returned < 0 ? typeof(ImmutableArray<>).MakeGenericType(arguments[1]) : arguments[returned];
        checks.That($"{owner.Name}.{name} is an extension over {carrier.Name} with two unconstrained type parameters",
            method.IsDefined(typeof(ExtensionAttribute)) && arguments.Length == 2
            && arguments.All(argument => argument.GenericParameterAttributes == GenericParameterAttributes.None && argument.GetGenericParameterConstraints().Length == 0)
            && parameter == carrier.MakeGenericType(arguments) && method.ReturnType == returnType);
        checks.That($"{owner.Name}.{name} signature uses only Rezultat and BCL types",
            new[] { method.ReturnType, parameter }.SelectMany(Flatten).All(type => type.Assembly == typeof(Unit).Assembly || type.Assembly == typeof(object).Assembly || type.Assembly == typeof(ImmutableArray).Assembly));
        checks.That($"{owner.Name}.{name} declares carrier and payload without nullable annotations or flow attributes",
            new[] { method.ReturnParameter, method.GetParameters()[0] }.All(item =>
                NullableFlags(item, method).All(flag => flag != 2)
                && !item.GetCustomAttributesData().Any(attribute => attribute.AttributeType.Namespace == "System.Diagnostics.CodeAnalysis")));
    }
}

static byte[] NullableFlags(ParameterInfo parameter, MethodInfo method)
{
    static CustomAttributeData? Find(IEnumerable<CustomAttributeData> attributes, string name) =>
        attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == $"System.Runtime.CompilerServices.{name}");

    var nullable = Find(parameter.GetCustomAttributesData(), "NullableAttribute");
    if (nullable is not null)
    {
        var value = nullable.ConstructorArguments[0];
        return value.Value is byte flag ? [flag] : ((IEnumerable<CustomAttributeTypedArgument>)value.Value!).Select(item => (byte)item.Value!).ToArray();
    }

    var context = Find(method.GetCustomAttributesData(), "NullableContextAttribute")
        ?? Find(method.DeclaringType!.GetCustomAttributesData(), "NullableContextAttribute");
    return context is null ? [] : [(byte)context.ConstructorArguments[0].Value!];
}

static IEnumerable<Type> Flatten(Type type) =>
    type.IsGenericParameter ? [] : type.IsGenericType ? [type.GetGenericTypeDefinition(), .. type.GetGenericArguments().SelectMany(Flatten)] : [type];

void CheckFailure(string name, Action action, string evidence)
{
    try
    {
        action();
        checks.Fail($"{name}: no failure");
    }
    catch (Exception exception)
    {
        var type = exception.GetType();
        checks.Text($"{name}: failure type", "Xunit.Sdk.FailException", type.FullName ?? string.Empty);
        checks.Text($"{name}: failure assembly", "xunit.v3.assert", type.Assembly.GetName().Name ?? string.Empty);
        checks.That($"{name}: message carries '{evidence}': {exception.Message}", exception.Message.Contains(evidence, StringComparison.Ordinal));
        Console.WriteLine($"INFO {name}: {type.Assembly.GetName().Name} {type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
    }
}

internal sealed class Throwing
{
    public override string ToString() => throw new InvalidOperationException("formatter");
}
