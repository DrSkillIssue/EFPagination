namespace EFPagination;

/// <summary>
/// Thrown when a reference object used for keyset pagination is missing a property required by
/// the pagination column definition. Indicates a type mismatch between the entity and the
/// reference object passed to <c>Paginate</c> under the loose-typing path.
/// </summary>
/// <param name="message">A human-readable description of the mismatch.</param>
/// <param name="propertyName">The name of the property that was not found on the reference object.</param>
/// <param name="referenceType">The CLR type of the reference object that was searched.</param>
/// <param name="entityType">The entity type that declares the missing pagination column.</param>
#pragma warning disable CA1032 // Implement standard exception constructors — we expose only the one production callers use.
public sealed class IncompatibleReferenceException(
    string message,
    string propertyName,
    Type referenceType,
    Type entityType) : Exception(message)
{
    /// <summary>
    /// Gets the name of the property that was not found on the reference object.
    /// </summary>
    /// <value>The missing property name.</value>
    public string PropertyName { get; } = propertyName;

    /// <summary>
    /// Gets the CLR type of the reference object that was searched.
    /// </summary>
    /// <value>The reference object type.</value>
    public Type ReferenceType { get; } = referenceType;

    /// <summary>
    /// Gets the entity type that declares the missing pagination column.
    /// </summary>
    /// <value>The entity type.</value>
    public Type EntityType { get; } = entityType;
}
