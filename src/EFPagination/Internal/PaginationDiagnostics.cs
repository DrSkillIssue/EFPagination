using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Emits <see cref="ActivitySource"/> tracing for pagination operations on the
/// <c>EFPagination</c> source. No work is performed unless a listener is registered.
/// </summary>
internal static class PaginationDiagnostics
{
    private static readonly ActivitySource s_source = new("EFPagination");
    private static readonly string[] s_directionNames = [nameof(PaginationDirection.Forward), nameof(PaginationDirection.Backward)];

    /// <summary>
    /// Starts a <c>Paginate</c> activity when there is at least one registered listener;
    /// otherwise returns <see langword="null"/> with no allocation.
    /// </summary>
    /// <typeparam name="T">The entity type being paginated.</typeparam>
    /// <param name="columns">The pagination columns; the length is reported as a tag.</param>
    /// <param name="direction">The pagination direction.</param>
    /// <param name="isCached">Whether the cached predicate template is in use.</param>
    /// <returns>The started <see cref="Activity"/>, or <see langword="null"/> when no listener is attached.</returns>
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
