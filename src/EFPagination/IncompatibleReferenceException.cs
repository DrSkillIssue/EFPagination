namespace EFPagination;

/// <summary>
/// Thrown when a reference object used for keyset pagination is missing a property
/// required by the pagination column definition.
/// </summary>
#pragma warning disable CA1032 // Implement standard exception constructors — we expose only the one production callers use.
public sealed class IncompatibleReferenceException(
    string message,
    string propertyName,
    Type referenceType,
    Type entityType) : Exception(message)
{
    /// <summary>The name of the property that was not found on the reference object.</summary>
    public string PropertyName { get; } = propertyName;

    /// <summary>The type of the reference object that was searched.</summary>
    public Type ReferenceType { get; } = referenceType;

    /// <summary>The entity type that defines the pagination column.</summary>
    public Type EntityType { get; } = entityType;
}
