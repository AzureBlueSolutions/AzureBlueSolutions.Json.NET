using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Convenience extensions that accept (line, column) or LSP <see cref="Position" />
///     and delegate to <see cref="TokenQuery" /> and <see cref="CommaPolicy" /> using
///     <see cref="TextLineIndex" /> for offset math.
/// </summary>
public static class TokenQueryExtensions
{
    /// <summary>
    ///     Returns the last non-comment token whose end offset is &lt;= the offset of (line, column).
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line.</param>
    /// <param name="column">Zero-based column.</param>
    public static (JsonTokenSpan token, int index)? PreviousSignificantAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return TokenQuery.PreviousSignificant(tokens, offset);
    }

    /// <summary>
    ///     Returns the first non-comment token whose start offset is &gt;= the offset of (line, column).
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line.</param>
    /// <param name="column">Zero-based column.</param>
    public static (JsonTokenSpan token, int index)? NextSignificantAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return TokenQuery.NextSignificant(tokens, offset);
    }

    /// <summary>
    ///     Returns the token that ends exactly at the offset of (line, column), if any.
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line.</param>
    /// <param name="column">Zero-based column.</param>
    public static (JsonTokenSpan token, int index)? TokenEndingAtPosition(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return TokenQuery.TokenEndingAt(tokens, offset);
    }

    /// <summary>
    ///     Returns the token that covers the offset of (line, column) (start ≤ offset &lt; end), if any.
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line.</param>
    /// <param name="column">Zero-based column.</param>
    public static (JsonTokenSpan token, int index)? TokenCoveringPosition(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return TokenQuery.TokenCovering(tokens, offset);
    }

    /// <summary>
    ///     On newline: if the previous token terminates a value and the next token looks like a property name,
    ///     insert a comma after the previous token (unless one already exists), using (line, column) to compute the cursor
    ///     offset.
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line of the caret after the typed newline.</param>
    /// <param name="column">Zero-based column of the caret after the typed newline.</param>
    /// <returns>A <see cref="TextEdit" /> to insert a comma, or <c>null</c> when no edit is required.</returns>
    public static TextEdit? TryInsertCommaBeforeNewlineAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return CommaPolicy.TryInsertCommaBeforeNewline(text, tokens, offset);
    }

    /// <summary>
    ///     On '}' or ']': if there is a trailing comma immediately before the closer, remove it.
    ///     Uses (line, column) to compute the cursor offset (caret after the closer).
    /// </summary>
    /// <param name="text">Full document text.</param>
    /// <param name="tokens">Token stream for <paramref name="text" />.</param>
    /// <param name="line">Zero-based line of the caret after the typed closer.</param>
    /// <param name="column">Zero-based column of the caret after the typed closer.</param>
    /// <returns>A <see cref="TextEdit" /> to remove the dangling comma, or <c>null</c> when not applicable.</returns>
    public static TextEdit? TryRemoveCommaBeforeCloserAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int column)
    {
        var offset = ToOffset(text, line, column);
        return CommaPolicy.TryRemoveCommaBeforeCloser(text, tokens, offset);
    }

    /// <summary>
    ///     Returns the last non-comment token whose end offset is &lt;= the offset of the LSP <paramref name="position" />.
    /// </summary>
    public static (JsonTokenSpan token, int index)? PreviousSignificantAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return PreviousSignificantAt(text, tokens, position.Line, position.Character);
    }

    /// <summary>
    ///     Returns the first non-comment token whose start offset is &gt;= the offset of the LSP <paramref name="position" />.
    /// </summary>
    public static (JsonTokenSpan token, int index)? NextSignificantAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return NextSignificantAt(text, tokens, position.Line, position.Character);
    }

    /// <summary>
    ///     Returns the token that ends exactly at the offset of the LSP <paramref name="position" />, if any.
    /// </summary>
    public static (JsonTokenSpan token, int index)? TokenEndingAtPosition(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return TokenEndingAtPosition(text, tokens, position.Line, position.Character);
    }

    /// <summary>
    ///     Returns the token that covers the offset of the LSP <paramref name="position" /> (start ≤ offset &lt; end), if any.
    /// </summary>
    public static (JsonTokenSpan token, int index)? TokenCoveringPosition(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return TokenCoveringPosition(text, tokens, position.Line, position.Character);
    }

    /// <summary>
    ///     On newline: (LSP position overload) insert a comma after the previous value when the next token is a property name.
    /// </summary>
    public static TextEdit? TryInsertCommaBeforeNewlineAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return TryInsertCommaBeforeNewlineAt(text, tokens, position.Line, position.Character);
    }

    /// <summary>
    ///     On '}' or ']': (LSP position overload) remove trailing comma before the closer.
    /// </summary>
    public static TextEdit? TryRemoveCommaBeforeCloserAt(
        string text,
        IReadOnlyList<JsonTokenSpan> tokens,
        Position position)
    {
        return TryRemoveCommaBeforeCloserAt(text, tokens, position.Line, position.Character);
    }

    private static int ToOffset(string text, int line, int column)
    {
        var index = new TextLineIndex(text);
        return index.GetOffset(line, column);
    }
}