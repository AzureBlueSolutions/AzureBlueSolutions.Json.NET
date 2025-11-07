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
    /// Groups multiple <see cref="TextEdit"/> items for batch application.
    /// </summary>
    /// <param name="Edits">
    /// The ordered set of edits to apply.
    /// </param>
    public sealed record TextEditBatch(IReadOnlyList<TextEdit> Edits)
    {
        /// <summary>
        /// Creates a batch from the specified edits.
        /// </summary>
        /// <param name="edits">One or more edits.</param>
        /// <returns>A <see cref="TextEditBatch"/> containing the provided edits.</returns>
        public static TextEditBatch Of(params TextEdit[] edits) => new(edits);

        /// <summary>
        /// Creates a batch from the specified edits, skipping any <c>null</c> entries.
        /// </summary>
        /// <param name="edits">A sequence of edits, possibly containing <c>null</c> items.</param>
        /// <returns>A <see cref="TextEditBatch"/> containing only non‑null edits.</returns>
        public static TextEditBatch FromNullable(IEnumerable<TextEdit?> edits)
        {
            var list = new List<TextEdit>();
            foreach (var e in edits)
                if (e is not null) list.Add(e);
            return new TextEditBatch(list);
        }
    }

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
        /// Attempts to create a <see cref="JsonCursor"/> from a parsed result and a JSON path.
        /// </summary>
        /// <param name="result">The parse result that contains the root token and path ranges.</param>
        /// <param name="path">The JSON path to locate.</param>
        /// <param name="cursor">
        /// When successful, receives the created cursor; otherwise <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if the path resolves; otherwise <c>false</c>.</returns>
        public static bool TryFromPath(JsonParseResult result, string path, [NotNullWhen(true)] out JsonCursor? cursor)
        {
            cursor = FromPath(result, path);
            return cursor is not null;
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
        /// Attempts to replace the value at the cursor using its <see cref="ValueRange"/>.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="newValue">The JSON value to serialize with <see cref="Formatting.None"/>.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TrySet(string documentText, JToken newValue, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = Set(documentText, newValue);
            return edit is not null;
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
        /// Attempts to insert a property into an object cursor as <c>"name": value</c>.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="name">Property name to insert.</param>
        /// <param name="value">Property value as a <see cref="JToken"/>.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertProperty(string documentText, string name, JToken value, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertProperty(documentText, name, value, true, "  ");
            return edit is not null;
        }

        /// <summary>
        /// Attempts to insert a property into an object cursor as <c>"name": value</c>.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="name">Property name to insert.</param>
        /// <param name="value">Property value as a <see cref="JToken"/>.</param>
        /// <param name="addCommaIfNeeded">Whether to add a comma when non-empty.</param>
        /// <param name="indent">Indentation to use for the inserted line.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertProperty(
            string documentText, 
            string name, 
            JToken value, 
            bool addCommaIfNeeded, 
            string indent,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertProperty(documentText, name, value, addCommaIfNeeded, indent);
            return edit is not null;
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

        /// <summary>
        /// Attempts to insert an array item at the end.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="value">The array item as a <see cref="JToken"/>.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertArrayItem(
            string documentText,
            JToken value,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertArrayItem(documentText, value, null, true);
            return edit is not null;
        }

        /// <summary>
        /// Attempts to insert an array item at the specified index (or append when <paramref name="index"/> is <c>null</c>).
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="value">The array item as a <see cref="JToken"/>.</param>
        /// <param name="index">Target index, or <c>null</c> to append.</param>
        /// <param name="addCommaIfNeeded">Whether to add a comma when non-empty.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertArrayItem(
            string documentText,
            JToken value,
            int? index,
            bool addCommaIfNeeded,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertArrayItem(documentText, value, index, addCommaIfNeeded);
            return edit is not null;
        }

        /// <summary>
        /// Attempts to remove the property represented by this cursor.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="edit">When successful, receives the deletion edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the cursor is a property and the edit could be produced; otherwise <c>false</c>.</returns>
        public bool TryRemoveProperty(string documentText, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = null;
            if (Kind != JsonCursorKind.Property || Token is not JProperty prop || NameRange is null) return false;
            if (prop.Parent is not JObject parent) return false;

            var objectStart = FindTokenStartBrace(documentText, parent);
            if (objectStart < 0) return false;
            var objectEnd = FindMatchingBrace(documentText, objectStart);
            if (objectEnd <= objectStart) return false;

            var start = NameRange.Start.Offset;
            var end = ValueRange?.End.Offset ?? start;

            var forward = end;
            ScanWhitespaceAndCommentsForward(documentText, ref forward);
            var consumedTrailingComma = false;
            if (forward < objectEnd && documentText[forward] == ',')
            {
                forward++;
                while (forward < objectEnd && (documentText[forward] == ' ' || documentText[forward] == '\t')) forward++;
                end = forward;
                consumedTrailingComma = true;
            }

            if (!consumedTrailingComma)
            {
                var back = start;
                while (back > objectStart && char.IsWhiteSpace(documentText[back - 1])) back--;
                if (back > objectStart && documentText[back - 1] == ',')
                {
                    back--;
                    var lineStart = LineStartOffset(documentText, back);
                    var trim = back;
                    while (trim > lineStart && documentText[trim - 1] == ' ') trim--;
                    start = trim;
                }
            }

            var range = new TextRange(
                new TextPosition(LineOf(documentText, start), ColOf(documentText, start), start),
                new TextPosition(LineOf(documentText, end), ColOf(documentText, end), end));

            edit = new TextEdit(range, string.Empty);
            return true;
        }

        /// <summary>
        /// Attempts to remove the child property with the specified <paramref name="propertyName"/>
        /// from this object cursor (or from a property whose value is an object).
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="propertyName">The property name to remove.</param>
        /// <param name="edit">When successful, receives the deletion edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the property exists and the edit could be produced; otherwise <c>false</c>.</returns>
        public bool TryRemoveProperty(string documentText, string propertyName, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = null;

            JObject? targetObject = Kind switch
            {
                JsonCursorKind.Object when Token is JObject o => o,
                JsonCursorKind.Property when Token is JProperty { Value: JObject vo } => vo as JObject,
                _ => null
            };

            var prop = targetObject?.Property(propertyName, StringComparison.Ordinal);
            if (prop is null) return false;

            if (!TryComputePropertyDeletion(documentText, targetObject, prop, out var deletion))
                return false;

            edit = deletion;
            return true;
        }

        /// <summary>
        /// Attempts to remove an array item from an array cursor (or array-valued property) at <paramref name="index"/>.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="index">Zero-based index of the item to remove.</param>
        /// <param name="edit">When successful, receives the deletion edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be produced; otherwise <c>false</c>.</returns>
        public bool TryRemoveArrayItem(string documentText, int index, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = null;

            JArray? array = Kind switch
            {
                JsonCursorKind.Array when Token is JArray a => a,
                JsonCursorKind.Property when Token is JProperty { Value: JArray va } => va as JArray,
                _ => null
            };

            if (array is null) return false;

            var startBracket = FindTokenStartBracket(documentText, array);
            if (startBracket < 0) return false;
            var endBracket = FindMatchingBracket(documentText, startBracket);
            if (endBracket <= startBracket) return false;

            if (!TryGetArrayElementSpan(documentText, startBracket, endBracket, index, out var elemStart, out var elemEnd))
                return false;

            var deleteStart = elemStart;
            var deleteEnd = elemEnd;

            var forward = elemEnd;
            ScanWhitespaceAndCommentsForward(documentText, ref forward);
            var consumedTrailingComma = false;
            if (forward < endBracket && documentText[forward] == ',')
            {
                forward++;
                while (forward < endBracket && (documentText[forward] == ' ' || documentText[forward] == '\t')) forward++;
                deleteEnd = forward;
                consumedTrailingComma = true;
            }

            if (!consumedTrailingComma)
            {
                var back = elemStart;
                while (back > startBracket && char.IsWhiteSpace(documentText[back - 1])) back--;
                if (back > startBracket && documentText[back - 1] == ',')
                {
                    back--;
                    var lineStart = LineStartOffset(documentText, back);
                    var trim = back;
                    while (trim > lineStart && documentText[trim - 1] == ' ') trim--;
                    deleteStart = trim;
                }
            }

            var range = new TextRange(
                new TextPosition(LineOf(documentText, deleteStart), ColOf(documentText, deleteStart), deleteStart),
                new TextPosition(LineOf(documentText, deleteEnd), ColOf(documentText, deleteEnd), deleteEnd));

            edit = new TextEdit(range, string.Empty);
            return true;
        }

        /// <summary>
        /// Attempts to remove the node represented by this cursor when it is a property or an array item.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="edit">When successful, receives the deletion edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a deletion edit was produced; otherwise <c>false</c>.</returns>
        public bool TryRemoveSelf(string documentText, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = null;

            if (Kind == JsonCursorKind.Property)
                return TryRemoveProperty(documentText, out edit);

            if (Token.Parent is JArray parentArray)
            {
                var itemIndex = IndexOfChild(parentArray, Token);
                if (itemIndex >= 0)
                    return TryRemoveArrayItem(documentText, itemIndex, out edit);
            }

            return false;
        }

        /// <summary>
        /// Attempts to replace the value at the cursor with an empty object (<c>{}</c>).
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TrySetObject(string documentText, [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = Set(documentText, new JObject());
            return edit is not null;
        }

        /// <summary>
        /// Attempts to insert a property with an empty object value (<c>"name": {}</c>) into an object cursor.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="name">Property name to insert.</param>
        /// <param name="addCommaIfNeeded">Whether to add a comma when non-empty.</param>
        /// <param name="indent">Indentation to use for the inserted line.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertObjectProperty(
            string documentText,
            string name,
            bool addCommaIfNeeded,
            string indent,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertProperty(documentText, name, new JObject(), addCommaIfNeeded, indent);
            return edit is not null;
        }

        /// <summary>
        /// Attempts to insert an empty object (<c>{}</c>) as an array item.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="index">Target index, or <c>null</c> to append.</param>
        /// <param name="addCommaIfNeeded">Whether to add a comma when non-empty.</param>
        /// <param name="edit">When successful, receives the edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the edit could be created; otherwise <c>false</c>.</returns>
        public bool TryInsertObjectArrayItem(
            string documentText, 
            int? index,
            bool addCommaIfNeeded,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = InsertArrayItem(documentText, new JObject(), index, addCommaIfNeeded);
            return edit is not null;
        }

        /// <summary>
        /// Attempts to remove the node at <paramref name="path"/> by creating a cursor
        /// for that path and invoking <see cref="TryRemoveSelf(string, out TextEdit?)"/>.
        /// </summary>
        /// <param name="documentText">The full document text.</param>
        /// <param name="result">The parse result that provides the root and path ranges.</param>
        /// <param name="path">The JSON path to remove.</param>
        /// <param name="edit">When successful, receives the deletion edit; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a deletion edit was produced; otherwise <c>false</c>.</returns>
        public static bool TryRemoveAt(
            string documentText,
            JsonParseResult result,
            string path,
            [NotNullWhen(true)] out TextEdit? edit)
        {
            edit = null;
            var target = FromPath(result, path);
            return target is not null &&
                   target.TryRemoveSelf(documentText, out edit);
        }

        /// <summary>
        /// Creates a <see cref="TextEditBatch"/> from a set of optional edits, skipping any <c>null</c> entries.
        /// </summary>
        /// <param name="edits">One or more edits that may include <c>null</c>.</param>
        /// <returns>A batch containing only the non‑null edits.</returns>
        public static TextEditBatch CreateBatch(params TextEdit?[] edits)
        {
            var list = new List<TextEdit>(edits.Length);
            list.AddRange(edits.OfType<TextEdit>());
            return new TextEditBatch(list);
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

            var range = new TextRange(
                new TextPosition(LineOf(text, end), ColOf(text, end), end),
                new TextPosition(LineOf(text, end), ColOf(text, end), end));
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

        private static bool TryComputePropertyDeletion(string text, JObject parentObject, JProperty property, out TextEdit? deletion)
        {
            deletion = null;

            var objectStart = FindTokenStartBrace(text, parentObject);
            if (objectStart < 0) return false;
            var objectEnd = FindMatchingBrace(text, objectStart);
            if (objectEnd <= objectStart) return false;

            var (pl, pc) = GetStart(property);
            var propOffsetSeed = ToOffset(text, pl, pc);

            var nameQuoteStart = propOffsetSeed;
            while (nameQuoteStart < text.Length && text[nameQuoteStart] != '"') nameQuoteStart++;
            if (nameQuoteStart >= text.Length || nameQuoteStart >= objectEnd) return false;

            var nameQuoteEnd = SkipString(text, nameQuoteStart);
            if (nameQuoteEnd >= text.Length) return false;

            var afterName = nameQuoteEnd + 1;
            ScanWhitespaceAndCommentsForward(text, ref afterName);

            if (afterName < text.Length && text[afterName] != ':')
            {
                while (afterName < text.Length && text[afterName] != ':' && afterName < objectEnd) afterName++;
            }
            if (afterName >= text.Length || afterName >= objectEnd) return false;
            afterName++;

            ScanWhitespaceAndCommentsForward(text, ref afterName);
            if (afterName >= text.Length) return false;

            var valueStart = afterName;
            var valueEnd = FindJsonValueEnd(text, valueStart);
            if (valueEnd < valueStart) return false;

            var start = nameQuoteStart;
            var end = valueEnd;

            var forward = end;
            ScanWhitespaceAndCommentsForward(text, ref forward);
            var consumedTrailingComma = false;
            if (forward < objectEnd && text[forward] == ',')
            {
                forward++;
                while (forward < objectEnd && (text[forward] == ' ' || text[forward] == '\t')) forward++;
                end = forward;
                consumedTrailingComma = true;
            }

            if (!consumedTrailingComma)
            {
                var back = start;
                while (back > objectStart && char.IsWhiteSpace(text[back - 1])) back--;
                if (back > objectStart && text[back - 1] == ',')
                {
                    back--;
                    var lineStart = LineStartOffset(text, back);
                    var trim = back;
                    while (trim > lineStart && text[trim - 1] == ' ') trim--;
                    start = trim;
                }
            }

            var range = new TextRange(
                new TextPosition(LineOf(text, start), ColOf(text, start), start),
                new TextPosition(LineOf(text, end), ColOf(text, end), end));
            deletion = new TextEdit(range, string.Empty);
            return true;
        }

        private static int FindJsonValueEnd(string text, int start)
        {
            var i = start;
            if (i >= text.Length) return i;

            ScanWhitespaceAndCommentsForward(text, ref i);
            if (i >= text.Length) return i;

            var c = text[i];

            switch (c)
            {
                case '"':
                {
                    var endQuote = SkipString(text, i);
                    return endQuote + 1 <= text.Length ? endQuote + 1 : text.Length;
                }
                case '{':
                {
                    var match = FindMatchingBrace(text, i);
                    return match + 1 <= text.Length ? match + 1 : text.Length;
                }
                case '[':
                {
                    var match = FindMatchingBracket(text, i);
                    return match + 1 <= text.Length ? match + 1 : text.Length;
                }
            }

            if (StartsAt(text, i, "true")) return i + 4;
            if (StartsAt(text, i, "false")) return i + 5;
            if (StartsAt(text, i, "null")) return i + 4;

            if (c == '-' || c == '+' || char.IsDigit(c))
            {
                var j = i;
                if (text[j] == '-' || text[j] == '+') j++;
                while (j < text.Length && char.IsDigit(text[j])) j++;
                if (j < text.Length && text[j] == '.')
                {
                    j++;
                    while (j < text.Length && char.IsDigit(text[j])) j++;
                }
                if (j < text.Length && (text[j] == 'e' || text[j] == 'E'))
                {
                    j++;
                    if (j < text.Length && (text[j] == '-' || text[j] == '+')) j++;
                    while (j < text.Length && char.IsDigit(text[j])) j++;
                }
                return j;
            }

            var k = i;
            while (k < text.Length && text[k] != ',' && text[k] != '}' && text[k] != ']' && text[k] != '\n' && text[k] != '\r') k++;
            return k;
        }

        private static string? ComputeParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var dot = path.LastIndexOf('.');
            var bracket = path.LastIndexOf('[');
            var cut = Math.Max(dot, bracket);
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
                switch (ch)
                {
                    case '"':
                        i = SkipString(text, i);
                        continue;
                    case '{':
                        depth++;
                        break;
                    case '}':
                    {
                        depth--;
                        if (depth == 0) return i;
                        break;
                    }
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
                switch (ch)
                {
                    case '"':
                        i = SkipString(text, i);
                        continue;
                    case '[':
                        depth++;
                        break;
                    case ']':
                    {
                        depth--;
                        if (depth == 0) return i;
                        break;
                    }
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
                switch (ch)
                {
                    case '\\':
                        escape = true;
                        continue;
                    case '"':
                        return i;
                }
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
                    switch (ch)
                    {
                        case '"':
                        {
                            inString = true;
                            if (currentIndex == index && candidateStart < 0) candidateStart = i;
                            i++;
                            continue;
                        }
                        case '[' or '{':
                        {
                            depth++;
                            if (currentIndex == index && candidateStart < 0) candidateStart = i;
                            i++;
                            continue;
                        }
                        case ']' or '}':
                        {
                            depth--;
                            if (depth < 0) depth = 0;
                            i++;
                            continue;
                        }
                        case ',' when depth == 0:
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
                    }

                    if (!char.IsWhiteSpace(ch) && currentIndex == index && candidateStart < 0) candidateStart = i;
                }
                else
                {
                    switch (ch)
                    {
                        case '\\':
                            i += 2;
                            continue;
                        case '"':
                            inString = false;
                            i++;
                            continue;
                    }
                }

                i++;
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
            IJsonLineInfo li = token;
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

        private static void ScanWhitespaceAndCommentsForward(string text, ref int index)
        {
            while (index < text.Length)
            {
                var ch = text[index];
                if (char.IsWhiteSpace(ch)) { index++; continue; }
                if (ch == '/' && index + 1 < text.Length)
                {
                    var next = text[index + 1];
                    if (next == '/')
                    {
                        index += 2;
                        while (index < text.Length && text[index] != '\n') index++;
                        continue;
                    }
                    if (next == '*')
                    {
                        index += 2;
                        while (index + 1 < text.Length && !(text[index] == '*' && text[index + 1] == '/')) index++;
                        if (index + 1 < text.Length) index += 2;
                        continue;
                    }
                }
                break;
            }
        }

        private static int IndexOfChild(JArray array, JToken child)
        {
            var i = 0;
            foreach (var t in array)
            {
                if (ReferenceEquals(t, child)) return i;
                i++;
            }
            return -1;
        }

        private static bool StartsAt(string text, int start, string s)
        {
            if (start + s.Length > text.Length) return false;
            return !s.Where((t, i) =>
                text[start + i] != t).Any();
        }
    }
}
