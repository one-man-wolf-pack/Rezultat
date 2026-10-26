using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Rezultat;
using Rezultat.Qualification;
using Rezultat.Testing.Xunit;

var checks = new Checks();
var assembly = typeof(ResultAssertions).Assembly;

checks.That("exported types are exactly ResultAssertions and ValidationAssertions",
    assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal)
        .SequenceEqual(["Rezultat.Testing.Xunit.ResultAssertions", "Rezultat.Testing.Xunit.ValidationAssertions"]));
CheckOwner(typeof(ResultAssertions), typeof(Result<,>), ("ShouldBeOk", 0), ("ShouldBeError", 1));
CheckOwner(typeof(ValidationAssertions), typeof(Validation<,>), ("ShouldBeValid", 0), ("ShouldBeInvalid", -1));

object reference = new object();
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
CheckFailure("included payload", () => Result<int, string>.Error("evidence").ShouldBeOk(includePayload: true), "Result.Error(evidence)");

Throwing.Calls = 0;
checks.That("excluded payload: success extracts exact values",
    ReferenceEquals(reference, Result<object, Throwing>.Ok(reference).ShouldBeOk(includePayload: false))
    && ReferenceEquals(reference, Result<Throwing, object>.Error(reference).ShouldBeError(includePayload: false))
    && ReferenceEquals(reference, Validation<object, Throwing>.Valid(reference).ShouldBeValid(includePayload: false))
    && Validation<Throwing, string?>.InvalidMany(issues).ShouldBeInvalid(includePayload: false) == issues
    && Result<string?, string>.Ok(null).ShouldBeOk(includePayload: false) is null);
CheckExcluded("excluded wrong Result branch", () => Result<int, Throwing>.Error(new Throwing()).ShouldBeOk(includePayload: false), "Result.Error");
CheckExcluded("excluded wrong Ok branch", () => Result<Throwing, int>.Ok(new Throwing()).ShouldBeError(includePayload: false), "Result.Ok");
CheckExcluded("excluded Invalid issues", () => Validation<int, Throwing>.InvalidMany([new Throwing(), new Throwing()]).ShouldBeValid(includePayload: false), "Validation.Invalid");
CheckExcluded("excluded wrong Valid branch", () => Validation<Throwing, int>.Valid(new Throwing()).ShouldBeInvalid(includePayload: false), "Validation.Valid");
CheckExcluded("excluded uninitialized", () => default(Result<Throwing, Throwing>).ShouldBeOk(includePayload: false), "Result.Uninitialized");
checks.That($"excluded payload: no formatter call ({Throwing.Calls})", Throwing.Calls == 0);

return checks.Complete();

static T Extract<T>(Result<T, string> result) => result.ShouldBeOk();

void CheckOwner(Type owner, Type carrier, params (string Name, int Returned)[] expected) {
    checks.That($"{owner.Name} is a public static extension owner",
        owner is { IsPublic: true, IsAbstract: true, IsSealed: true } && owner.IsDefined(typeof(ExtensionAttribute)));
    var methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    checks.That($"{owner.Name} declares exactly {string.Join(", ", expected.Select(item => item.Name))}, each without and with includePayload",
        methods.Select(method => method.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.SelectMany(item => new[] { item.Name, item.Name }).Order(StringComparer.Ordinal)));
    foreach (var (name, returned) in expected) {
        var withoutFlag = methods.SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == 1);
        var withFlag = methods.SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == 2);
        if (withoutFlag is null || withFlag is null) {
            checks.Fail($"{owner.Name}.{name} overloads missing");
            continue;
        }

        var flag = withFlag.GetParameters()[1];
        checks.That($"{owner.Name}.{name} has a required bool includePayload",
            flag is { Name: "includePayload", IsOptional: false, HasDefaultValue: false } && flag.ParameterType == typeof(bool)
            && flag.GetCustomAttributesData().Count == 0);
        CheckSignature(owner, carrier, name, returned, withoutFlag);
        CheckSignature(owner, carrier, name, returned, withFlag);
    }
}

void CheckSignature(Type owner, Type carrier, string shortName, int returned, MethodInfo method) {
    string name = $"{shortName}({string.Join(", ", method.GetParameters().Select(item => item.ParameterType.Name))})";
    var arguments = method.GetGenericArguments();
    var parameter = method.GetParameters()[0].ParameterType;
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

static byte[] NullableFlags(ParameterInfo parameter, MethodInfo method) {
    static CustomAttributeData? Find(IEnumerable<CustomAttributeData> attributes, string name) =>
        attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == $"System.Runtime.CompilerServices.{name}");

    var nullable = Find(parameter.GetCustomAttributesData(), "NullableAttribute");
    if (nullable is not null) {
        var value = nullable.ConstructorArguments[0];
        return value.Value is byte flag ? [flag] : [.. ((IEnumerable<CustomAttributeTypedArgument>)value.Value!).Select(item => (byte)item.Value!)];
    }

    var context = Find(method.GetCustomAttributesData(), "NullableContextAttribute")
        ?? Find(method.DeclaringType!.GetCustomAttributesData(), "NullableContextAttribute");
    return context is null ? [] : [(byte)context.ConstructorArguments[0].Value!];
}

static IEnumerable<Type> Flatten(Type type) =>
    type.IsGenericParameter ? [] : type.IsGenericType ? [type.GetGenericTypeDefinition(), .. type.GetGenericArguments().SelectMany(Flatten)] : [type];

void CheckFailure(string name, Action action, string evidence) {
    try {
        action();
        checks.Fail($"{name}: no failure");
    } catch (Exception exception) {
        var type = exception.GetType();
        checks.Text($"{name}: failure type", "Xunit.Sdk.FailException", type.FullName ?? string.Empty);
        checks.Text($"{name}: failure assembly", "xunit.v3.assert", type.Assembly.GetName().Name ?? string.Empty);
        checks.That($"{name}: message carries '{evidence}': {exception.Message}", exception.Message.Contains(evidence, StringComparison.Ordinal));
        Console.WriteLine($"INFO {name}: {type.Assembly.GetName().Name} {type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}");
    }
}

void CheckExcluded(string name, Action action, string actual) {
    try {
        action();
        checks.Fail($"{name}: no failure");
    } catch (Exception exception) {
        checks.Text($"{name}: failure type", "Xunit.Sdk.FailException", exception.GetType().FullName ?? string.Empty);
        checks.That($"{name}: message names only the branches: {exception.Message}",
            exception.Message.Contains(actual, StringComparison.Ordinal)
            && !exception.Message.Contains("secret", StringComparison.Ordinal)
            && !exception.Message.Contains("ToString", StringComparison.Ordinal)
            && !exception.Message.Any(char.IsDigit));
    }
}

internal sealed class Throwing {
    public static int Calls { get; set; }

    public override string ToString() {
        Calls++;
        throw new InvalidOperationException("secret formatter");
    }

    public override bool Equals(object? obj) {
        Calls++;
        return ReferenceEquals(this, obj);
    }

    public override int GetHashCode() {
        Calls++;
        return 0;
    }
}
