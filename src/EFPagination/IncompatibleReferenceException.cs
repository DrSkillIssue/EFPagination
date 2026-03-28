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
    /// <value>The missing property name, or an empty string when unavailable.</value>
    public string PropertyName { get; }

    /// <summary>
    /// The type of the reference object that was searched.
    /// </summary>
    /// <value>The reference object type, or <see cref="object"/> when unavailable.</value>
    public Type ReferenceType { get; }

    /// <summary>
    /// The entity type that defines the pagination column.
    /// </summary>
    /// <value>The entity type that defined the missing property, or <see cref="object"/> when unavailable.</value>
    public Type EntityType { get; }

    /// <summary>
    /// Initializes a new exception with default placeholder metadata.
    /// </summary>
    public IncompatibleReferenceException()
        : this(string.Empty, typeof(object), typeof(object))
    {
    }

    /// <summary>
    /// Initializes a new exception with the specified message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public IncompatibleReferenceException(string message)
        : base(message)
    {
        PropertyName = string.Empty;
        ReferenceType = typeof(object);
        EntityType = typeof(object);
    }

    /// <summary>
    /// Initializes a new exception with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The underlying exception that caused the current exception.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// Initializes a new instance with a custom message and property details.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="propertyName">The name of the property that was not found.</param>
    /// <param name="referenceType">The type of the reference object that was searched.</param>
    /// <param name="entityType">The entity type that defines the pagination column.</param>
    public IncompatibleReferenceException(
        string message,
        string propertyName,
        Type referenceType,
        Type entityType)
        : base(message)
    {
        PropertyName = propertyName;
        ReferenceType = referenceType;
        EntityType = entityType;
    }
}
