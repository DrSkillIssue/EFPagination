using EFPagination.Cursor;
using EFPagination.Internal;

namespace EFPagination;

/// <summary>
/// Encodes and decodes opaque cursor tokens containing typed pagination boundary values.
/// </summary>
/// <remarks>
/// The wire format is a versioned binary payload Base64Url-encoded for URL safety. Type
/// information is omitted from the payload; the decoder reconstructs CLR types from the
/// matching <see cref="PaginationQueryDefinition{T}"/> and validates payload compatibility
/// via a schema fingerprint. Cursors may optionally carry a 128-bit truncated HMAC-SHA256
/// signature for tamper detection.
/// </remarks>
public static class PaginationCursor
{
    /// <summary>
    /// Encodes definition-bound boundary values into an opaque cursor token by reading the
    /// configured column values from <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="definition">The pagination query definition that determines the column order.</param>
    /// <param name="reference">The entity (or DTO with matching property names) supplying the boundary values.</param>
    /// <param name="options">Optional cursor metadata (logical sort key, total count, signing key).</param>
    /// <returns>A Base64Url-encoded cursor token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="reference"/> is <see langword="null"/>.</exception>
    /// <exception cref="IncompatibleReferenceException"><paramref name="reference"/> is missing a property required by the pagination definition.</exception>
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
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="definition">The pagination query definition that determines the column order.</param>
    /// <param name="values">The typed boundary values, typically obtained from <see cref="TryDecode{T}"/>.</param>
    /// <param name="options">Optional cursor metadata (logical sort key, total count, signing key).</param>
    /// <returns>A Base64Url-encoded cursor token.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
    public static string Encode<T>(
        PaginationQueryDefinition<T> definition,
        PaginationValues<T> values,
        PaginationCursorOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return CursorEncoder.EncodeSchemaBoundFromBindings(definition, values.Bindings ?? [], options);
    }

    /// <summary>
    /// Decodes a cursor token into definition-bound ordered pagination values and metadata.
    /// </summary>
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="encoded">The encoded cursor token.</param>
    /// <param name="definition">The pagination query definition that determines the expected value order and CLR types.</param>
    /// <param name="values">
    /// When this method returns <see langword="true"/>, contains the decoded values bound to <paramref name="definition"/>.
    /// When <see langword="false"/>, contains <see cref="PaginationValues{T}.Empty"/>.
    /// </param>
    /// <param name="metadata">
    /// When this method returns <see langword="true"/>, contains the decoded sort key and total count (when present in the payload).
    /// </param>
    /// <param name="signingKey">
    /// The HMAC-SHA256 signing key for verification, or <see langword="null"/> to skip verification.
    /// When non-<see langword="null"/>, signed cursors must verify successfully or this method returns <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the cursor was decoded successfully; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is <see langword="null"/>.</exception>
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
