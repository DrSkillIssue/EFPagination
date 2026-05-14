using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

internal static class PaginationDiagnostics
{
    private static readonly ActivitySource s_source = new("EFPagination");
    private static readonly string[] s_directionNames = [nameof(PaginationDirection.Forward), nameof(PaginationDirection.Backward)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Activity? StartPaginate<T>(
        PaginationColumn<T>[] columns,
        PaginationDirection direction,
        bool isCached)
        => s_source.HasListeners() ? StartCore(typeof(T).Name, columns.Length, direction, isCached) : null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Activity? StartCore(string entityName, int columnCount, PaginationDirection direction, bool isCached)
    {
        var activity = s_source.StartActivity("Paginate");
        if (activity is not null)
        {
            activity.SetTag("pagination.entity", entityName);
            activity.SetTag("pagination.direction", s_directionNames[(int)direction]);
            activity.SetTag("pagination.columns", columnCount.ToString(CultureInfo.InvariantCulture));
            activity.SetTag("pagination.cached", isCached ? "True" : "False");
        }
        return activity;
    }
}
