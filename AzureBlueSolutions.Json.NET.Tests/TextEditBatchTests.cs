namespace AzureBlueSolutions.Json.NET.Tests;

/// <summary>
///     Tests for <see cref="TextEditBatch" /> factory helpers.
/// </summary>
public sealed class TextEditBatchTests
{
    /// <summary>
    ///     Helper to create a <see cref="TextEdit" /> with simple ranges.
    ///     Start and end are absolute offsets; line/column are derived trivially.
    /// </summary>
    private static TextEdit MakeEdit(int start, int end, string newText)
    {
        var startPos = new TextPosition(0, start, start);
        var endPos = new TextPosition(0, end, end);
        var range = new TextRange(startPos, endPos);
        return new TextEdit(range, newText);
    }

    [Fact]
    public void FromNullable_Filters_Nulls_And_Preserves_Order()
    {
        var e1 = MakeEdit(0, 3, "A");
        TextEdit? e2 = null;
        var e3 = MakeEdit(5, 8, "B");
        TextEdit? e4 = null;
        var e5 = MakeEdit(10, 11, "");

        var input = new[] { e1, e2, e3, e4, e5 };

        var batch = TextEditBatch.FromNullable(input);

        Assert.NotNull(batch);
        Assert.NotNull(batch.Edits);

        // Only the non-null edits should be present, in original order.
        Assert.Collection(
            batch.Edits,
            x => Assert.Same(e1, x),
            x => Assert.Same(e3, x),
            x => Assert.Same(e5, x)
        );
    }

    [Fact]
    public void FromNullable_Empty_Sequence_Yields_Empty_Batch()
    {
        var input = Array.Empty<TextEdit?>();

        var batch = TextEditBatch.FromNullable(input);

        Assert.NotNull(batch);
        Assert.NotNull(batch.Edits);
        Assert.Empty(batch.Edits);
    }

    [Fact]
    public void FromNullable_Works_With_LINQ_Deferred_Sources()
    {
        var all = new[]
        {
            (TextEdit?)MakeEdit(0, 1, "X"),
            null,
            (TextEdit?)MakeEdit(2, 3, "Y"),
            null
        };

        // Simulate a deferred sequence (e.g., LINQ)
        var deferred = all.Where(_ => true);

        var batch = TextEditBatch.FromNullable(deferred);

        Assert.NotNull(batch);
        Assert.Equal(2, batch.Edits.Count);
        Assert.Equal("X", batch.Edits[0].NewText);
        Assert.Equal("Y", batch.Edits[1].NewText);
    }

    [Fact]
    public void FromNullable_All_Nulls_Yields_Empty_Batch()
    {
        var input = new TextEdit?[] { null, null, null };

        var batch = TextEditBatch.FromNullable(input);

        Assert.NotNull(batch);
        Assert.Empty(batch.Edits);
    }

    [Fact]
    public void Of_Creates_Batch_With_All_Edits_In_Order()
    {
        var a = MakeEdit(0, 1, "A");
        var b = MakeEdit(1, 2, "B");
        var c = MakeEdit(2, 3, "C");

        var batch = TextEditBatch.Of(a, b, c);

        Assert.NotNull(batch);
        Assert.NotNull(batch.Edits);
        Assert.Equal(3, batch.Edits.Count);
        Assert.Same(a, batch.Edits[0]);
        Assert.Same(b, batch.Edits[1]);
        Assert.Same(c, batch.Edits[2]);
    }

    [Fact]
    public void FromNullable_Does_Not_Clone_Or_Copy_TextEdits()
    {
        // Ensure the same instances are kept (useful if callers rely on reference equality).
        var original = MakeEdit(5, 9, "Z");
        var batch = TextEditBatch.FromNullable([original]);

        Assert.Single(batch.Edits);
        Assert.Same(original, batch.Edits[0]);
    }
}