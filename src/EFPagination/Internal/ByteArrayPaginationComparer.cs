namespace EFPagination.Internal;

internal static class ByteArrayPaginationComparer
{
    public static int Compare(byte[] a, byte[] b) => a.AsSpan().SequenceCompareTo(b.AsSpan());
}
