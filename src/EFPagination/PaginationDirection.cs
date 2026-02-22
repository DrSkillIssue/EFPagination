namespace EFPagination;

/// <summary>
/// Specifies the direction of keyset pagination.
/// </summary>
public enum PaginationDirection
{
    /// <summary>
    /// Paginate forward (fetch rows after the reference).
    /// </summary>
    Forward,

    /// <summary>
    /// Paginate backward (fetch rows before the reference).
    /// </summary>
    Backward,
}
