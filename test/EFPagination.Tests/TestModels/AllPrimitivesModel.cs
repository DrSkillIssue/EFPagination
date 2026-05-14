namespace EFPagination.TestModels;

/// <summary>
/// Fixture model holding one property per cursor-supported primitive type. Used by the
/// all-types schema-bound round-trip tests.
/// </summary>
public sealed class AllPrimitivesModel
{
    public string String { get; set; } = "";
    public bool Boolean { get; set; }
    public char Char { get; set; }
    public byte Byte { get; set; }
    public sbyte SByte { get; set; }
    public short Int16 { get; set; }
    public ushort UInt16 { get; set; }
    public int Int32 { get; set; }
    public uint UInt32 { get; set; }
    public long Int64 { get; set; }
    public ulong UInt64 { get; set; }
    public float Single { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public System.Guid Guid { get; set; }
    public System.DateTime DateTime { get; set; }
    public System.DateTimeOffset DateTimeOffset { get; set; }
    public System.DateOnly DateOnly { get; set; }
    public System.TimeOnly TimeOnly { get; set; }
    public System.TimeSpan TimeSpan { get; set; }
    public TestEnum Enum { get; set; }
}
