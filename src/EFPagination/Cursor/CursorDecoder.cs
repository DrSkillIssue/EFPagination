using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using EFPagination.Internal;

namespace EFPagination.Cursor;

/// <summary>
/// Parses Base64Url-encoded cursor tokens produced by <see cref="CursorEncoder"/>. Verifies the
/// optional HMAC trailer when a signing key is supplied, validates the schema fingerprint, and
/// decodes the typed body into the caller-supplied <see cref="ColumnBinding"/> array.
/// </summary>
internal static class CursorDecoder
{
    /// <summary>
    /// Decodes <paramref name="encoded"/> against <paramref name="definition"/>, writing the
    /// values into <paramref name="bindings"/> and returning header metadata.
    /// </summary>
    /// <typeparam name="T">The entity type associated with <paramref name="definition"/>.</typeparam>
    /// <param name="encoded">The Base64Url cursor token.</param>
    /// <param name="definition">The pagination definition the cursor is bound to.</param>
    /// <param name="bindings">The destination bindings (one per column in <paramref name="definition"/>).</param>
    /// <param name="signingKey">The HMAC-SHA256 signing key, or <see langword="null"/> to require an unsigned cursor.</param>
    /// <param name="metadata">When this method returns <see langword="true"/>, the decoded header metadata.</param>
    /// <returns><see langword="true"/> if the cursor was decoded and (when applicable) verified.</returns>
    [SkipLocalsInit]
    public static bool TryDecodeWithDefinition<T>(
        ReadOnlySpan<char> encoded,
        PaginationQueryDefinition<T> definition,
        ColumnBinding[] bindings,
        byte[]? signingKey,
        out CursorMetadata metadata)
    {
        metadata = default;
        if (encoded.IsEmpty) return false;

        var maxLen = Base64Url.GetMaxDecodedLength(encoded.Length);
        byte[]? rented = null;
        Span<byte> scratch = maxLen <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

        try
        {
            if (!Base64Url.TryDecodeFromChars(encoded, scratch, out var bytesWritten))
                return false;

            var payload = scratch[..bytesWritten];
            if (!ParseHeader(payload, signingKey, out var header))
                return false;
            if (header.Fingerprint != definition.SchemaFingerprint)
                return false;

            var reader = new CursorReader(payload.Slice(header.BodyStart, header.BodyEnd - header.BodyStart));

            if (!DecodeBody(ref reader, definition, bindings, out var valueCount))
                return false;
            if (reader.Failed || reader.Remaining != 0)
                return false;

            metadata = new CursorMetadata(header.SortBy, header.TotalCount, header.Fingerprint, valueCount);
            return true;
        }
        catch (FormatException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (InvalidCastException) { return false; }
        finally
        {
            if (rented is not null) ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private readonly ref struct ParsedHeader
    {
        public uint Fingerprint { get; init; }
        public string? SortBy { get; init; }
        public int? TotalCount { get; init; }
        public int BodyStart { get; init; }
        public int BodyEnd { get; init; }
    }

    [SkipLocalsInit]
    private static bool ParseHeader(ReadOnlySpan<byte> payload, byte[]? signingKey, out ParsedHeader header)
    {
        header = default;
        if (payload.Length < 2 + 4 || payload[0] != CursorFormat.Version)
            return false;

        var flags = payload[1];
        var signed = (flags & CursorFormat.FlagSigned) != 0;

        int bodyEnd;
        if (signed)
        {
            if (signingKey is null) return false;
            if (payload.Length < 2 + 4 + CursorFormat.HmacTruncatedLength) return false;
            bodyEnd = payload.Length - CursorFormat.HmacTruncatedLength;

            Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
            HMACSHA256.HashData(signingKey, payload[..bodyEnd], expected);
            if (!CryptographicOperations.FixedTimeEquals(expected[..CursorFormat.HmacTruncatedLength], payload.Slice(bodyEnd, CursorFormat.HmacTruncatedLength)))
                return false;
        }
        else
        {
            if (signingKey is not null) return false;
            bodyEnd = payload.Length;
        }

        var reader = new CursorReader(payload[2..bodyEnd]);

        var fingerprint = reader.ReadUInt32();
        if (reader.Failed) return false;

        string? sortBy = null;
        if ((flags & CursorFormat.FlagSortBy) != 0)
        {
            sortBy = reader.ReadString();
            if (reader.Failed) return false;
        }

        int? totalCount = null;
        if ((flags & CursorFormat.FlagTotalCount) != 0)
        {
            totalCount = reader.ReadVarInt32();
            if (reader.Failed) return false;
        }

        header = new ParsedHeader
        {
            Fingerprint = fingerprint,
            SortBy = sortBy,
            TotalCount = totalCount,
            BodyStart = 2 + reader.Position,
            BodyEnd = bodyEnd,
        };
        return true;
    }

    private static bool DecodeBody<T>(ref CursorReader reader, PaginationQueryDefinition<T> definition, ColumnBinding[] bindings, out int valueCount)
    {
        valueCount = 0;
        var count = (int)reader.ReadVarUInt32();
        if (reader.Failed) return false;

        var columns = definition.Columns;
        if (count != columns.Length || bindings.Length < count) return false;

        var nullMaskLength = (count + 7) >>> 3;
        var nullMask = reader.ReadRawBytes(nullMaskLength);
        if (reader.Failed) return false;

        for (var i = 0; i < count; i++)
        {
            var col = columns[i];
            if (((nullMask[i >>> 3] >>> (i & 7)) & 1) != 0)
            {
                if (!col.IsNullable) return false;
                col.WriteBindingFromBoxed(null, bindings[i]);
            }
            else
            {
                col.DecodeCursorValueInto(ref reader, bindings[i]);
                if (reader.Failed) return false;
            }
        }

        valueCount = count;
        return true;
    }
}
