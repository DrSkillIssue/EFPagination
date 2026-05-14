using EFPagination.Cursor;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Encodes and decodes opaque cursor tokens containing typed pagination boundary values.
/// The wire format is a versioned binary payload (v3) base64url-encoded for URL safety.
/// </summary>
public static class PaginationCursor
{
    /// <summary>
    /// Encodes definition-bound boundary values into an opaque cursor token using the most
    /// compact (schema-bound) wire layout. Type information is omitted from the payload — the
    /// decoder reconstructs types from the matching <see cref="PaginationQueryDefinition{T}"/>
    /// and validates payload compatibility via the schema fingerprint.
    /// </summary>
    public static string Encode<T>(
        PaginationQueryDefinition<T> definition,
        T reference,
        PaginationCursorOptions options = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reference);
        return CursorEncoder.EncodeSchemaBound(definition, reference, options);
    }

    /// <summary>
    /// Encodes pre-extracted definition-bound boundary values into an opaque cursor token using
    /// the schema-bound wire layout.
    /// </summary>
    public static string Encode<T>(
        PaginationQueryDefinition<T> definition,
        PaginationValues<T> values,
        PaginationCursorOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return CursorEncoder.EncodeSchemaBoundFromBindings(definition, values.Bindings ?? [], options);
    }

    /// <summary>
    /// Encodes name/value pairs into an opaque self-describing cursor token using the tagged
    /// wire layout. Each value is preceded by a type tag, allowing decoding without a
    /// <see cref="PaginationQueryDefinition{T}"/>.
    /// </summary>
    public static string Encode(ReadOnlySpan<ColumnValue> values, PaginationCursorOptions options = default)
        => CursorEncoder.EncodeTagged(values, options);

    /// <summary>
    /// Decodes a cursor token into definition-bound ordered pagination values, returning
    /// decoded metadata in <paramref name="metadata"/>.
    /// </summary>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="definition">The pagination definition that determines the expected value order.</param>
    /// <param name="values">When this method returns <see langword="true"/>, contains the decoded ordered values.</param>
    /// <param name="metadata">When this method returns <see langword="true"/>, contains decoded sort key, total count, fingerprint and value count.</param>
    /// <param name="signingKey">The HMAC-SHA256 signing key for verification, or <see langword="null"/> to skip verification.</param>
    public static bool TryDecode<T>(
        ReadOnlySpan<char> encoded,
        PaginationQueryDefinition<T> definition,
        out PaginationValues<T> values,
        out CursorMetadata metadata,
        byte[]? signingKey = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var columns = definition.Columns;
        var bindings = new ColumnBinding[columns.Length];
        for (var i = 0; i < columns.Length; i++)
            bindings[i] = columns[i].CreateBinding();

        if (CursorDecoder.TryDecodeWithDefinition(encoded, definition, bindings, signingKey, out metadata))
        {
            values = new PaginationValues<T>(bindings);
            return true;
        }

        values = PaginationValues<T>.Empty;
        return false;
    }

    /// <summary>
    /// Decodes a tagged cursor token into a caller-supplied <see cref="ColumnValue"/> buffer.
    /// </summary>
    public static bool TryDecode(
        ReadOnlySpan<char> encoded,
        Span<ColumnValue> values,
        out CursorMetadata metadata,
        byte[]? signingKey = null)
        => CursorDecoder.TryDecodeTagged(encoded, values, signingKey, out metadata);

    /// <summary>
    /// Registers an enum type as allowed for tagged-mode cursor decoding. Called automatically by
    /// <see cref="PaginationBuilder{T}"/> when a column uses an enum type.
    /// </summary>
    internal static void RegisterEnumType(Type enumType) => EnumTypeRegistry.Register(enumType);
}
