namespace EFPagination;

/// <summary>
/// Thrown when a reference object used for keyset pagination is missing a property
/// required by the pagination column definition, indicating a type mismatch between
/// the entity and the reference object (loose typing).
/// </summary>
public sealed class IncompatibleReferenceException : Exception
{
    /// <summary>
    /// The name of the property that was not found on the reference object.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// The type of the reference object that was searched.
    /// </summary>
    public Type ReferenceType { get; }

    /// <summary>
    /// The entity type that defines the pagination column.
    /// </summary>
    public Type EntityType { get; }

    /// <inheritdoc />
    public IncompatibleReferenceException()
        : this(string.Empty, typeof(object), typeof(object))
    {
    }

    /// <inheritdoc />
    public IncompatibleReferenceException(string message)
        : base(message)
    {
        PropertyName = string.Empty;
        ReferenceType = typeof(object);
        EntityType = typeof(object);
    }

    /// <inheritdoc />
    public IncompatibleReferenceException(string message, Exception innerException)
        : base(message, innerException)
    {
        PropertyName = string.Empty;
        ReferenceType = typeof(object);
        EntityType = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance with details about the missing property.
    /// </summary>
    /// <param name="propertyName">The name of the property that was not found.</param>
    /// <param name="referenceType">The type of the reference object that was searched.</param>
    /// <param name="entityType">The entity type that defines the pagination column.</param>
    public IncompatibleReferenceException(
        string propertyName,
        Type referenceType,
        Type entityType)
        : base($"Property '{propertyName}' required by entity '{entityType.Name}' was not found on reference object of type '{referenceType.Name}'. " +
               $"When using loose typing, the reference object must have all properties used in the pagination definition.")
    {
        PropertyName = propertyName;
        ReferenceType = referenceType;
        EntityType = entityType;
    }
}
