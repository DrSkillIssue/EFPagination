using EFPagination.Cursor;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Encodes and decodes opaque cursor tokens containing typed pagination boundary values.
/// The wire format is a versioned binary payload base64url-encoded for URL safety.
/// </summary>
public static class PaginationCursor
{
    /// <summary>
    /// Encodes definition-bound boundary values into an opaque cursor token. Type information is
    /// omitted from the payload — the decoder reconstructs types from the matching
    /// <see cref="PaginationQueryDefinition{T}"/> and validates payload compatibility via the
    /// schema fingerprint.
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
    /// Encodes pre-extracted definition-bound boundary values into an opaque cursor token.
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
    /// Decodes a cursor token into definition-bound ordered pagination values.
    /// </summary>
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
}
