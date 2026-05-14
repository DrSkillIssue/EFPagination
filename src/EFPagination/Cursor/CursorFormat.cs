namespace EFPagination.Cursor;

internal static class CursorFormat
{
    public const byte Version = 0x04;

    // Header flag bits. Bits 3-7 reserved for future format extensions.
    public const byte FlagSortBy = 1 << 0;
    public const byte FlagTotalCount = 1 << 1;
    public const byte FlagSigned = 1 << 2;

    // 128-bit truncated HMAC-SHA256 trailer. RFC 2104 §5 permits truncation to at least L/2 bits;
    // 16 bytes is the sweet spot for cursor authenticity vs token bloat.
    public const int HmacTruncatedLength = 16;
}
