using Microsoft.AspNetCore.Http;

namespace EFPagination.AspNetCore;

/// <summary>
/// Extensions for resolving <see cref="PaginationRequest"/> sort parameters against a <see cref="PaginationSortRegistry{T}"/>.
/// </summary>
public static class PaginationSortRegistryAspNetExtensions
{
    /// <summary>
    /// Resolves the definition for the request's sort parameters.
    /// </summary>
    /// <typeparam name="T">The entity type handled by the registry.</typeparam>
    /// <param name="registry">The registry of sortable fields.</param>
    /// <param name="request">The bound pagination request parameters.</param>
    /// <returns>The matched pagination definition, or the default definition when no sort was requested.</returns>
    /// <exception cref="BadHttpRequestException"><paramref name="request"/> names an unknown sort field.
    /// -or-
    /// <paramref name="request"/> names an unsupported sort direction.</exception>
    public static PaginationQueryDefinition<T> Resolve<T>(this PaginationSortRegistry<T> registry, PaginationRequest request)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (registry.TryResolve(request.SortBy.AsSpan(), request.SortDir.AsSpan(), out var definition))
            return definition;

        var dir = request.SortDir.AsSpan();
        if (!dir.IsEmpty &&
            !dir.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !dir.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException($"sortDir '{request.SortDir}' is not supported. sortDir must be 'asc' or 'desc'.");
        }

        throw new BadHttpRequestException($"'{request.SortBy}' is not a sortable field.");
    }
}
