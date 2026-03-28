using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

internal static class PaginationDiagnostics
{
    private static readonly ActivitySource s_source = new("EFPagination");
    private static readonly string[] s_directionNames = [nameof(PaginationDirection.Forward), nameof(PaginationDirection.Backward)];
    private static readonly string[] s_smallInts = ["0", "1", "2", "3", "4", "5", "6", "7", "8"];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Activity? StartPaginate<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        bool isCached)
    {
        if (!s_source.HasListeners())
            return null;

        return StartCore(typeof(T).Name, columns.Length, direction, isCached);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Activity? StartCore(
        string entityName,
        int columnCount,
        PaginationDirection direction,
        bool isCached)
    {
        var activity = s_source.StartActivity("Paginate");
        if (activity is not null)
        {
            activity.SetTag("pagination.entity", entityName);
            activity.SetTag("pagination.direction", s_directionNames[(int)direction]);
            activity.SetTag("pagination.columns", (uint)columnCount < (uint)s_smallInts.Length
                ? s_smallInts[columnCount]
                : columnCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            activity.SetTag("pagination.cached", isCached ? "True" : "False");
        }
        return activity;
    }
}
