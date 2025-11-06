using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Represents a single textual edit using zero-based, end-exclusive coordinates
    /// over the specified <see cref="TextRange"/>.
    /// </summary>
    /// <param name="Range">
    /// The range of text to replace (zero-based, end-exclusive).
    /// </param>
    /// <param name="NewText">
    /// The text to insert at <paramref name="Range"/>. May be empty to indicate deletion.
    /// </param>
    public sealed record TextEdit(TextRange Range, string NewText);

    /// <summary>
    /// Enumerates the kinds of nodes addressable by a JSON cursor.
    /// </summary>
    public enum JsonCursorKind
    {
        /// <summary>
        /// A JSON object node (<c>{ ... }</c>).
        /// </summary>
        Object,

        /// <summary>
        /// A JSON array node (<c>[ ... ]</c>).
        /// </summary>
        Array,

        /// <summary>
        /// A JSON property node (<c>"name": value</c>).
        /// </summary>
        Property,

        /// <summary>
        /// A JSON value node (string, number, boolean, null).
        /// </summary>
        Value
    }

    /// <summary>
    /// Provides a strongly-typed cursor over a JSON path, exposing name/value ranges
    /// and helper methods to produce precise text edits for common operations.
    /// </summary>
    public sealed class JsonCursor
    {
        private JsonCursor(string path, JsonCursorKind kind, TextRange? name, TextRange? value, JToken token,
            string? parentPath)
        {
            Path = path;
            Kind = kind;
            NameRange = name;
            ValueRange = value;
            Token = token;
            ParentPath = parentPath;
        }

        /// <summary>
        /// Gets the JSON path represented by this cursor.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the kind of JSON node at <see cref="Path"/>.
        /// </summary>
        public JsonCursorKind Kind { get; }

        /// <summary>
        /// Gets the source range of the property name, when applicable.
        /// </summary>
        public TextRange? NameRange { get; }

        /// <summary>
        /// Gets the source range of the value for this node, when applicable.
        /// </summary>
        public TextRange? ValueRange { get; }

        /// <summary>
        /// Gets the underlying <see cref="JToken"/> at <see cref="Path"/>.
        /// </summary>
        public JToken Token { get; }

        /// <summary>
        /// Gets the parent JSON path of this cursor, if any.
        /// </summary>
        public string? ParentPath { get; }

        /// <summary>
        /// Creates a <see cref="JsonCursor"/> from a parsed result and a JSON path.
        /// </summary>
        /// <param name="result">
        /// The parse result that contains the root token and path ranges.
        /// </param>
        /// <param name="path">
        /// The JSON path to locate within <paramref name="result"/>.
        /// </param>
        /// <returns>
        /// A <see cref="JsonCursor"/> if the path resolves; otherwise, <c>null</c>.
        /// </returns>
        public static JsonCursor? FromPath(JsonParseResult result, string path)
        {
            if (result.Root is null) return null;

            var tok = SafeSelect(result.Root, path);
            if (tok is null) return null;

            result.PathRanges.TryGetValue(path, out var pr);
            var parentPath = ComputeParentPath(path);

            var kind = tok switch
            {
                JProperty => JsonCursorKind.Property,
                JObject => JsonCursorKind.Object,
                JArray => JsonCursorKind.Array,
                _ => JsonCursorKind.Value
            };

            return new JsonCursor(path, kind, pr?.Name, pr?.Value, tok, parentPath);
        }

        /// <summary>
        /// Converts the current token value to <typeparamref name="T"/> using Newtonsoft.Json.
        /// </summary>
        /// <typeparam name="T">
        /// The target CLR type.
        /// </typeparam>
        /// <returns>
        /// The converted value, or <c>null</c> if conversion is not possible.
        /// </returns>
        public T? As<T>()
        {
            return Token.ToObject<T>();
        }

        /// <summary>
        /// Replaces the value at the cursor using its <see cref="ValueRange"/>.
        /// </summary>
        /// <param name="documentText">
        /// The full document text (not modified by this method).
        /// </param>
        /// <param name="newValue">
        /// The JSON value to write at the cursor. It is serialized using <see cref="Formatting.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="TextEdit"/> to apply to the document text, or <c>null</c> if the cursor has no value range.
        /// </returns>
        public TextEdit? Set(string documentText, JToken newValue)
        {
            if (ValueRange is null) return null;
            var json = newValue.ToString(Formatting.None);
            return new TextEdit(ValueRange, json);
        }

        /// <summary>
        /// Inserts a property into an object cursor as <c>"name": value</c> before the closing <c>}</c>.
        /// </summary>
        /// <param name="documentText">
        /// The full document text (not modified by this method).
        /// </param>
        /// <param name="name">
        /// The property name to insert.
        /// </param>
        /// <param name="value">
        /// The property value as a <see cref="JToken"/>; serialized with <see cref="Formatting.None"/>.
        /// </param>
        /// <param name="addCommaIfNeeded">
        /// Whether to add a leading comma when the object already contains properties.
        /// </param>
        /// <param name="indent">
        /// The indentation string to use for the inserted line (e.g., two spaces).
        /// </param>
        /// <returns>
        /// A <see cref="TextEdit"/> that inserts the property, or <c>null</c> if the cursor is not at an object.
        /// </returns>
        public TextEdit? InsertProperty(string documentText, string name, JToken value, bool addCommaIfNeeded = true,
            string indent = "  ")
        {
            if (Kind == JsonCursorKind.Property && Token is JProperty { Value: JObject } prop)
                return InsertPropertyIntoObject(documentText, (prop.Value as JObject)!, name, value, addCommaIfNeeded, indent);

            if (Kind != JsonCursorKind.Object || Token is not JObject obj) return null;
            return InsertPropertyIntoObject(documentText, obj, name, value, addCommaIfNeeded, indent);
        }

        /// <summary>
        /// Inserts an array item at the end (or at <paramref name="index"/> when provided).
        /// </summary>
        /// <param name="documentText">
        /// The full document text (not modified by this method).
        /// </param>
        /// <param name="value">
        /// The array item as a <see cref="JToken"/>; serialized with <see cref="Formatting.None"/>.
        /// </param>
        /// <param name="index">
        /// Optional zero-based index at which to insert; if <c>null</c>, appends to the end.
        /// </param>
        /// <param name="addCommaIfNeeded">
        /// Whether to add a leading comma when the array already contains elements.
        /// </param>
        /// <returns>
        /// A <see cref="TextEdit"/> that inserts the array item, or <c>null</c> if the cursor is not at an array.
        /// </returns>
        public TextEdit? InsertArrayItem(string documentText, JToken value, int? index = null, bool addCommaIfNeeded = true)
        {
            if (Kind == JsonCursorKind.Property && Token is JProperty { Value: JArray } prop)
                return InsertArrayItemCore(documentText, (prop.Value as JArray)!, value, index, addCommaIfNeeded);

            if (Kind != JsonCursorKind.Array || Token is not JArray arr) return null;
            return InsertArrayItemCore(documentText, arr, value, index, addCommaIfNeeded);
        }

        private static TextEdit? InsertPropertyIntoObject(string text, JObject obj, string name, JToken value,
            bool addComma, string indent)
        {
            var start = FindTokenStartBrace(text, obj);
            if (start < 0) return null;

            var end = FindMatchingBrace(text, start);
            if (end <= start) return null;

            var needsComma = addComma && HasAnyPropertyBetween(text, start + 1, end);
            var baseIndent = ComputeIndent(text, start);
            var insertIndent = baseIndent + indent;

            var property = $"\"{name}\": {value.ToString(Formatting.None)}";
            var insert = (needsComma ? "," : "") + "\n" + insertIndent + property + "\n" + baseIndent;

            var insertPoint = end;

            var range = new TextRange(
                new TextPosition(LineOf(text, insertPoint), ColOf(text, insertPoint), insertPoint),
                new TextPosition(LineOf(text, insertPoint), ColOf(text, insertPoint), insertPoint));

            return new TextEdit(range, insert);
        }

        private static TextEdit? InsertArrayItemCore(string text, JArray array, JToken value, int? index, bool addComma)
        {
            var start = FindTokenStartBracket(text, array);
            if (start < 0) return null;

            var end = FindMatchingBracket(text, start);
            if (end <= start) return null;

            var insertPoint = end;
            var hasAny = HasAnyElement(text, start, end);

            var insert = (hasAny && addComma ? "," : "") + " " + value.ToString(Formatting.None);
            if (!hasAny) insert = " " + value.ToString(Formatting.None) + " ";

            if (index.HasValue && hasAny)
                if (TryGetArrayElementSpan(text, start, end, index.Value, out var _, out var elemEnd))
                {
                    insertPoint = elemEnd;
                    insert = "," + " " + value.ToString(Formatting.None);
                }

            var range = new TextRange(
                new TextPosition(LineOf(text, insertPoint), ColOf(text, insertPoint), insertPoint),
                new TextPosition(LineOf(text, insertPoint), ColOf(text, insertPoint), insertPoint));

            return new TextEdit(range, insert);
        }

        private static string? ComputeParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var i = path.LastIndexOf('.');
            var j = path.LastIndexOf('[');
            var cut = Math.Max(i, j);

            if (cut <= 0) return null;

            if (path[cut] == '[')
            {
                var k = path.LastIndexOf(']', path.Length - 1);
                if (k > cut) return path[..cut];
            }

            return path[..cut];
        }

        private static JToken? SafeSelect(JToken root, string path)
        {
            try
            {
                return root.SelectToken(path);
            }
            catch
            {
                return null;
            }
        }

        private static int FindTokenStartBrace(string text, JObject obj)
        {
            var (l, c) = GetStart(obj);
            var off = ToOffset(text, l, c);
            while (off < text.Length && text[off] != '{') off++;
            return off < text.Length ? off : -1;
        }

        private static int FindTokenStartBracket(string text, JArray arr)
        {
            var (l, c) = GetStart(arr);
            var off = ToOffset(text, l, c);
            while (off < text.Length && text[off] != '[') off++;
            return off < text.Length ? off : -1;
        }

        private static int FindMatchingBrace(string text, int startOffset)
        {
            var i = startOffset;
            while (i < text.Length && text[i] != '{') i++;
            if (i >= text.Length) return text.Length;

            var depth = 0;
            for (; i < text.Length; i++)
            {
                var ch = text[i];

                if (ch == '"')
                {
                    i = SkipString(text, i);
                    continue;
                }

                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return text.Length;
        }

        private static int FindMatchingBracket(string text, int startOffset)
        {
            var i = startOffset;
            while (i < text.Length && text[i] != '[') i++;
            if (i >= text.Length) return text.Length;

            var depth = 0;
            for (; i < text.Length; i++)
            {
                var ch = text[i];

                if (ch == '"')
                {
                    i = SkipString(text, i);
                    continue;
                }

                if (ch == '[')
                {
                    depth++;
                }
                else if (ch == ']')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return text.Length;
        }

        private static int SkipString(string text, int startQuote)
        {
            var i = startQuote + 1;
            var escape = false;

            for (; i < text.Length; i++)
            {
                var ch = text[i];

                if (ch == '\n') continue;

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escape = true;
                    continue;
                }

                if (ch == '"') return i;
            }

            return i;
        }

        private static bool HasAnyPropertyBetween(string text, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var ch = text[i];
                if (ch == '"') return true;
                if (!char.IsWhiteSpace(ch) && ch != '{' && ch != '}') return true;
            }
            return false;
        }

        private static bool HasAnyElement(string text, int arrayStart, int arrayEnd)
        {
            for (var i = arrayStart + 1; i < arrayEnd; i++)
            {
                var ch = text[i];
                if (!char.IsWhiteSpace(ch) && ch != '[' && ch != ']') return true;
            }
            return false;
        }

        private static bool TryGetArrayElementSpan(string text, int arrayStart, int arrayEnd, int index, out int elemStart,
            out int elemEnd)
        {
            elemStart = -1;
            elemEnd = -1;

            var i = arrayStart + 1;
            var depth = 0;
            var inString = false;
            var currentIndex = 0;
            var candidateStart = -1;

            while (i < arrayEnd)
            {
                var ch = text[i];

                if (!inString)
                {
                    if (ch == '"')
                    {
                        inString = true;
                        if (currentIndex == index && candidateStart < 0) candidateStart = i;
                        i++;
                        continue;
                    }

                    if (ch is '[' or '{')
                    {
                        depth++;
                        if (currentIndex == index && candidateStart < 0) candidateStart = i;
                        i++;
                        continue;
                    }

                    if (ch == ']' || ch == '}')
                    {
                        depth--;
                        if (depth < 0) depth = 0;
                        i++;
                        continue;
                    }

                    if (ch == ',' && depth == 0)
                    {
                        if (currentIndex == index)
                        {
                            elemStart = candidateStart >= 0 ? candidateStart : i;
                            elemEnd = i;
                            return true;
                        }

                        currentIndex++;
                        candidateStart = -1;
                        i++;
                        continue;
                    }

                    if (!char.IsWhiteSpace(ch) && currentIndex == index && candidateStart < 0) candidateStart = i;
                    i++;
                }
                else
                {
                    if (ch == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inString = false;
                        i++;
                        continue;
                    }

                    i++;
                }
            }

            if (currentIndex == index)
            {
                elemStart = candidateStart >= 0 ? candidateStart : arrayEnd;
                elemEnd = arrayEnd;
                return elemStart <= elemEnd;
            }

            return false;
        }

        private static (int line, int col) GetStart(JToken token)
        {
            var li = (IJsonLineInfo)token;
            var l = Math.Max(0, li.LineNumber - 1);
            var c = Math.Max(0, li.LinePosition - 1);
            return (l, c);
        }

        private static int ToOffset(string text, int line, int col)
        {
            var off = 0;
            var curLine = 0;
            var i = 0;

            while (i < text.Length && curLine < line)
            {
                if (text[i] == '\n') curLine++;
                off++;
                i++;
            }

            return Math.Clamp(off + col, 0, text.Length);
        }

        private static int LineOf(string text, int offset)
        {
            var l = 0;
            for (var i = 0; i < offset; i++)
                if (text[i] == '\n')
                    l++;
            return l;
        }

        private static int ColOf(string text, int offset)
        {
            var i = offset - 1;
            while (i >= 0 && text[i] != '\n') i--;
            return offset - (i + 1);
        }

        /// <summary>
        /// Returns the indentation (spaces/tabs) of the line that contains the given offset.
        /// </summary>
        [RequiresUnreferencedCode(
            "This method may use reflection or serialization that is not compatible with trimming. See https://learn.microsoft.com/dotnet/core/deploying/trimming/trim-warnings/il2026")]
        private static string ComputeIndent(string text, int anyOffsetOnLine)
        {
            var lineStart = LineStartOffset(text, anyOffsetOnLine);
            var i = lineStart;

            while (i < text.Length)
            {
                var ch = text[i];
                if (ch != ' ' && ch != '\t') break;
                i++;
            }

            return text.Substring(lineStart, i - lineStart);
        }

        /// <summary>
        /// Returns the offset of the first character on the line containing the given offset.
        /// </summary>
        private static int LineStartOffset(string text, int offset)
        {
            offset = Math.Clamp(offset, 0, text.Length);
            var i = offset;
            while (i > 0 && text[i - 1] != '\n') i--;
            return i;
        }
    }
}