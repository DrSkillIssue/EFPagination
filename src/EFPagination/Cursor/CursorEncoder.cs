using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EFPagination.Internal;

namespace EFPagination.Cursor;

/// <summary>
/// Builds Base64Url-encoded cursor tokens by streaming the binary payload through a
/// <see cref="CursorWriter"/> and optionally appending an HMAC trailer. The two public entry
/// points cover the two value sources used by <see cref="PaginationCursor"/>: a reference object
/// or a pre-extracted bindings array.
/// </summary>
internal static class CursorEncoder
{
    /// <summary>
    /// Encodes a cursor by reading column values directly from <paramref name="reference"/>.
    /// </summary>
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="definition">The pagination definition.</param>
    /// <param name="reference">The entity (or DTO with matching property names) supplying the values.</param>
    /// <param name="options">Optional cursor metadata.</param>
    /// <returns>A Base64Url-encoded cursor token.</returns>
    public static string EncodeSchemaBound<T>(
        PaginationQueryDefinition<T> definition,
        T reference,
        PaginationCursorOptions options) where T : notnull
        => EncodeCore(options, new FromReferenceBody<T>(definition, reference, options));

    /// <summary>
    /// Encodes a cursor from a pre-extracted bindings array.
    /// </summary>
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="definition">The pagination definition.</param>
    /// <param name="bindings">The typed boundary bindings, one per column.</param>
    /// <param name="options">Optional cursor metadata.</param>
    /// <returns>A Base64Url-encoded cursor token.</returns>
    public static string EncodeSchemaBoundFromBindings<T>(
        PaginationQueryDefinition<T> definition,
        ColumnBinding[] bindings,
        PaginationCursorOptions options)
        => EncodeCore(options, new FromBindingsBody<T>(definition, bindings, options));

    [SkipLocalsInit]
    private static string EncodeCore<TBody>(PaginationCursorOptions options, scoped in TBody body)
        where TBody : struct, IEncodeBody, allows ref struct
    {
        var buffer = CursorBufferPool.Rent();
        try
        {
            var writer = new CursorWriter(buffer);
            body.Write(ref writer);

            if (options.SigningKey is byte[] signingKey)
            {
                Span<byte> hmacFull = stackalloc byte[HMACSHA256.HashSizeInBytes];
                HMACSHA256.HashData(signingKey, buffer.WrittenSpan, hmacFull);
                var dest = buffer.GetSpan(CursorFormat.HmacTruncatedLength);
                hmacFull[..CursorFormat.HmacTruncatedLength].CopyTo(dest);
                buffer.Advance(CursorFormat.HmacTruncatedLength);
            }

            var len = Base64Url.GetEncodedLength(buffer.Written);
            return string.Create(len, buffer, static (span, state) =>
            {
                Base64Url.TryEncodeToChars(state.WrittenSpan, span, out _);
            });
        }
        finally
        {
            CursorBufferPool.Return(buffer);
        }
    }

    private interface IEncodeBody
    {
        void Write(scoped ref CursorWriter writer);
    }

    private readonly ref struct FromReferenceBody<T> : IEncodeBody where T : notnull
    {
        private readonly PaginationQueryDefinition<T> _definition;
        private readonly T _reference;
        private readonly PaginationCursorOptions _options;

        public FromReferenceBody(PaginationQueryDefinition<T> definition, T reference, PaginationCursorOptions options)
        { _definition = definition; _reference = reference; _options = options; }

        public void Write(ref CursorWriter writer)
        {
            var columns = _definition.Columns;
            WriteHeader(ref writer, _options, _definition.SchemaFingerprint, columns.Length);
            var nullMaskStart = writer.ReserveZeros((columns.Length + 7) >>> 3);
            for (var i = 0; i < columns.Length; i++)
            {
                if (!columns[i].TryWriteCursorValueFromReference(_reference, ref writer))
                    writer.SetBit(nullMaskStart, i);
            }
        }
    }

    private readonly ref struct FromBindingsBody<T> : IEncodeBody
    {
        private readonly PaginationQueryDefinition<T> _definition;
        private readonly ColumnBinding[] _bindings;
        private readonly PaginationCursorOptions _options;

        public FromBindingsBody(PaginationQueryDefinition<T> definition, ColumnBinding[] bindings, PaginationCursorOptions options)
        { _definition = definition; _bindings = bindings; _options = options; }

        public void Write(ref CursorWriter writer)
        {
            var columns = _definition.Columns;
            var count = Math.Min(columns.Length, _bindings.Length);
            WriteHeader(ref writer, _options, _definition.SchemaFingerprint, count);
            var nullMaskStart = writer.ReserveZeros((count + 7) >>> 3);
            for (var i = 0; i < count; i++)
            {
                if (!columns[i].TryWriteCursorValueFromBinding(_bindings[i], ref writer))
                    writer.SetBit(nullMaskStart, i);
            }
        }
    }

    private static void WriteHeader(ref CursorWriter writer, PaginationCursorOptions options, uint fingerprint, int valueCount)
    {
        byte flags = 0;
        if (options.SortBy is not null) flags |= CursorFormat.FlagSortBy;
        if (options.TotalCount.HasValue) flags |= CursorFormat.FlagTotalCount;
        if (options.SigningKey is not null) flags |= CursorFormat.FlagSigned;

        writer.WriteByte(CursorFormat.Version);
        writer.WriteByte(flags);
        writer.WriteUInt32(fingerprint);

        if (options.SortBy is not null)
            writer.WriteString(options.SortBy);
        if (options.TotalCount.HasValue)
            writer.WriteVarInt32(options.TotalCount.GetValueOrDefault());

        writer.WriteVarUInt32((uint)valueCount);
    }

}
