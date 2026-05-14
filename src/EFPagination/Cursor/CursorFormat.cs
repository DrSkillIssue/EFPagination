namespace EFPagination.Cursor;

/// <summary>
/// Constants describing the on-the-wire cursor binary format. Producers and consumers must agree
/// on these values to round-trip cursors across library versions.
/// </summary>
internal static class CursorFormat
{
    /// <summary>
    /// The current cursor wire-format version byte. Incremented on incompatible payload changes.
    /// </summary>
    public const byte Version = 0x04;

    /// <summary>
    /// Flag bit indicating the header carries a UTF-8 <c>SortBy</c> string.
    /// </summary>
    public const byte FlagSortBy = 1 << 0;

    /// <summary>
    /// Flag bit indicating the header carries a varint-encoded <c>TotalCount</c>.
    /// </summary>
    public const byte FlagTotalCount = 1 << 1;

    /// <summary>
    /// Flag bit indicating the payload ends with an HMAC-SHA256 signature trailer.
    /// </summary>
    public const byte FlagSigned = 1 << 2;

    /// <summary>
    /// Truncated HMAC-SHA256 trailer length, in bytes. 128 bits is the sweet spot between
    /// cursor authenticity and token bloat; RFC 2104 §5 permits truncation to at least L/2 bits.
    /// </summary>
    public const int HmacTruncatedLength = 16;
}
