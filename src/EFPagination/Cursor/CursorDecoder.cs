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

            var headerReader = new CursorReader(payload[2..bodyEnd]);

            var fingerprint = headerReader.ReadUInt32();
            if (headerReader.Failed) return false;

            string? sortBy = null;
            if ((flags & CursorFormat.FlagSortBy) != 0)
            {
                sortBy = headerReader.ReadString();
                if (headerReader.Failed) return false;
            }

            int? totalCount = null;
            if ((flags & CursorFormat.FlagTotalCount) != 0)
            {
                totalCount = headerReader.ReadVarInt32();
                if (headerReader.Failed) return false;
            }

            if (fingerprint != definition.SchemaFingerprint)
                return false;

            var bodyStart = 2 + headerReader.Position;
            var reader = new CursorReader(payload.Slice(bodyStart, bodyEnd - bodyStart));

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

            if (reader.Failed || reader.Remaining != 0)
                return false;

            metadata = new CursorMetadata(sortBy, totalCount, fingerprint, count);
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
}
