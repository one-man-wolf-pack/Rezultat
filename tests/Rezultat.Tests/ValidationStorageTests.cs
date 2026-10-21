using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ValidationStorageTests
{
    [Fact]
    public void InvalidMany_and_extraction_reuse_the_immutable_input()
    {
        ImmutableArray<string> input = ["a", "b"];
        var validation = Validation<int, string>.InvalidMany(input);

        Assert.False(validation.TryGetValue(out _, out var first));
        Assert.False(validation.TryGetValue(out _, out var second));
        var matched = validation.Match(static _ => default, static issues => issues);

        Assert.Same(Backing(input), Backing(first));
        Assert.Same(Backing(input), Backing(second));
        Assert.Same(Backing(input), Backing(matched));
    }

    [Fact]
    public void Single_invalid_operand_of_Zip_reuses_its_buffer_for_every_arity()
    {
        ImmutableArray<string> input = ["a", "b"];
        var invalid = Validation<int, string>.InvalidMany(input);
        var valid = Validation<int, string>.Valid(1);

        Assert.Same(Backing(input), Backing(Issues(invalid.Zip(valid, static (a, b) => a + b))));
        Assert.Same(Backing(input), Backing(Issues(valid.Zip(invalid, static (a, b) => a + b))));
        Assert.Same(Backing(input), Backing(Issues(valid.Zip(valid, invalid, static (a, b, c) => a + b + c))));
        Assert.Same(Backing(input), Backing(Issues(valid.Zip(invalid, valid, valid, static (a, b, c, d) => a + b + c + d))));
    }

    [Fact]
    public void Several_invalid_operands_produce_one_new_exact_buffer()
    {
        ImmutableArray<string> a = ["a"];
        ImmutableArray<string> b = ["b1", "b2"];
        ImmutableArray<string> d = ["d"];
        var valid = Validation<int, string>.Valid(1);

        var combined = Issues(Validation<int, string>.InvalidMany(a).Zip(
            Validation<int, string>.InvalidMany(b), valid, Validation<int, string>.InvalidMany(d), static (_, _, _, _) => 0));

        Assert.Equal(["a", "b1", "b2", "d"], combined);
        Assert.Equal(4, Backing(combined).Length);
        Assert.DoesNotContain(Backing(combined), new[] { Backing(a), Backing(b), Backing(d) });
    }

    [Fact]
    public void AndThen_and_OrElse_pass_buffers_without_copying()
    {
        ImmutableArray<string> input = ["a"];
        ImmutableArray<string> replacement = ["r"];
        var invalid = Validation<int, string>.InvalidMany(input);
        var received = default(ImmutableArray<string>);

        var chained = invalid.AndThen(static value => Validation<string, string>.Valid($"{value}"));
        var replaced = invalid.OrElse(issues => { received = issues; return Validation<int, string>.InvalidMany(replacement); });

        Assert.Same(Backing(input), Backing(Issues(chained)));
        Assert.Same(Backing(input), Backing(received));
        Assert.Same(Backing(replacement), Backing(Issues(replaced)));
    }

    [Fact]
    public void Traverse_with_one_invalid_reuses_its_slice()
    {
        ImmutableArray<string> input = ["a", "b"];

        var result = new[] { 1, 2, 3 }.Traverse(value =>
            value == 2 ? Validation<int, string>.InvalidMany(input) : Validation<int, string>.Valid(value));

        Assert.Same(Backing(input), Backing(Issues(result)));
    }

    [Fact]
    public void Traverse_with_several_invalids_freezes_one_new_buffer()
    {
        ImmutableArray<string> first = ["a"];
        ImmutableArray<string> second = ["b"];

        var result = new[] { first, second }.Traverse(static issues => Validation<int, string>.InvalidMany(issues));

        Assert.Equal(["a", "b"], Issues(result));
        Assert.NotSame(Backing(first), Backing(Issues(result)));
    }

    [Fact]
    public void MapIssues_fills_one_new_buffer_of_the_input_length()
    {
        ImmutableArray<int> input = [1, 2, 3];

        var mapped = Issues(Validation<int, int>.InvalidMany(input).MapIssues(static issue => issue * 10));

        Assert.Equal([10, 20, 30], mapped);
        Assert.Equal(3, Backing(mapped).Length);
    }

    private static ImmutableArray<TIssue> Issues<T, TIssue>(Validation<T, TIssue> validation)
    {
        Assert.False(validation.TryGetValue(out _, out var issues));
        return issues;
    }

    private static TIssue[] Backing<TIssue>(ImmutableArray<TIssue> issues) => ImmutableCollectionsMarshal.AsArray(issues)!;
}
