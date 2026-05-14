namespace EFPagination.Cursor;

internal static class CursorFormat
{
    public const byte Version = 0x03;

    public const byte FlagFingerprint = 1 << 0;
    public const byte FlagSortBy = 1 << 1;
    public const byte FlagTotalCount = 1 << 2;
    public const byte FlagSchemaBound = 1 << 3;
    public const byte FlagSigned = 1 << 4;

    // 128-bit truncated HMAC-SHA256 trailer. RFC 2104 §5 permits truncation to at least L/2 bits;
    // 16 bytes is the sweet spot for cursor authenticity vs token bloat.
    public const int HmacTruncatedLength = 16;

    public const byte KindNull = 0;
    public const byte KindString = 1;
    public const byte KindBoolean = 2;
    public const byte KindChar = 3;
    public const byte KindByte = 4;
    public const byte KindSByte = 5;
    public const byte KindInt16 = 6;
    public const byte KindUInt16 = 7;
    public const byte KindInt32 = 8;
    public const byte KindUInt32 = 9;
    public const byte KindInt64 = 10;
    public const byte KindUInt64 = 11;
    public const byte KindSingle = 12;
    public const byte KindDouble = 13;
    public const byte KindDecimal = 14;
    public const byte KindGuid = 15;
    public const byte KindDateTime = 16;
    public const byte KindDateTimeOffset = 17;
    public const byte KindDateOnly = 18;
    public const byte KindTimeOnly = 19;
    public const byte KindTimeSpan = 20;
    public const byte KindEnum = 21;
}
