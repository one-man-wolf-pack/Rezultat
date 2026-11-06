using Rezultat.Qualification;

var checks = new Checks();

checks.That("empty input has no word", !Words.TryFirstWord([], out var none) && none.IsEmpty);
checks.That("blank input has no word", !Words.TryFirstWord("   ", out var blank) && blank.IsEmpty);
checks.That("single word", Words.TryFirstWord("order", out var single) && single.SequenceEqual("order"));

const string Text = "  pay order 7";
checks.That("first of several words borrows the source",
    Words.TryFirstWord(Text, out var first) && first.SequenceEqual("pay") && first.Overlaps(Text.AsSpan()));

Span<char> buffer = stackalloc char[16];
"ship it".CopyTo(buffer);
checks.That("stack-backed input borrows the stack buffer",
    Words.TryFirstWord(buffer[.."ship it".Length], out var stacked) && stacked.SequenceEqual("ship") && stacked.Overlaps(buffer));

return checks.Complete();
