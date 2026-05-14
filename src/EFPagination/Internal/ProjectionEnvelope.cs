namespace EFPagination.Internal;

/// <summary>
/// A 1-key projection envelope. Carries the projected DTO alongside one keyset key column so the
/// SQL <c>SELECT</c> covers both in a single round trip.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0>(TOut item, TK0 k0)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;
}

/// <summary>
/// A 2-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1>(TOut item, TK0 k0, TK1 k1)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;
}

/// <summary>
/// A 3-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2>(TOut item, TK0 k0, TK1 k1, TK2 k2)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;
}

/// <summary>
/// A 4-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <typeparam name="TK3">The CLR type of key column 3.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
/// <param name="k3">The value of key column 3.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3>(TOut item, TK0 k0, TK1 k1, TK2 k2, TK3 k3)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;

    /// <summary>Gets the value of key column 3.</summary>
    public TK3 K3 { get; } = k3;
}

/// <summary>
/// A 5-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <typeparam name="TK3">The CLR type of key column 3.</typeparam>
/// <typeparam name="TK4">The CLR type of key column 4.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
/// <param name="k3">The value of key column 3.</param>
/// <param name="k4">The value of key column 4.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4>(TOut item, TK0 k0, TK1 k1, TK2 k2, TK3 k3, TK4 k4)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;

    /// <summary>Gets the value of key column 3.</summary>
    public TK3 K3 { get; } = k3;

    /// <summary>Gets the value of key column 4.</summary>
    public TK4 K4 { get; } = k4;
}

/// <summary>
/// A 6-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <typeparam name="TK3">The CLR type of key column 3.</typeparam>
/// <typeparam name="TK4">The CLR type of key column 4.</typeparam>
/// <typeparam name="TK5">The CLR type of key column 5.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
/// <param name="k3">The value of key column 3.</param>
/// <param name="k4">The value of key column 4.</param>
/// <param name="k5">The value of key column 5.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5>(TOut item, TK0 k0, TK1 k1, TK2 k2, TK3 k3, TK4 k4, TK5 k5)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;

    /// <summary>Gets the value of key column 3.</summary>
    public TK3 K3 { get; } = k3;

    /// <summary>Gets the value of key column 4.</summary>
    public TK4 K4 { get; } = k4;

    /// <summary>Gets the value of key column 5.</summary>
    public TK5 K5 { get; } = k5;
}

/// <summary>
/// A 7-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <typeparam name="TK3">The CLR type of key column 3.</typeparam>
/// <typeparam name="TK4">The CLR type of key column 4.</typeparam>
/// <typeparam name="TK5">The CLR type of key column 5.</typeparam>
/// <typeparam name="TK6">The CLR type of key column 6.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
/// <param name="k3">The value of key column 3.</param>
/// <param name="k4">The value of key column 4.</param>
/// <param name="k5">The value of key column 5.</param>
/// <param name="k6">The value of key column 6.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6>(TOut item, TK0 k0, TK1 k1, TK2 k2, TK3 k3, TK4 k4, TK5 k5, TK6 k6)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;

    /// <summary>Gets the value of key column 3.</summary>
    public TK3 K3 { get; } = k3;

    /// <summary>Gets the value of key column 4.</summary>
    public TK4 K4 { get; } = k4;

    /// <summary>Gets the value of key column 5.</summary>
    public TK5 K5 { get; } = k5;

    /// <summary>Gets the value of key column 6.</summary>
    public TK6 K6 { get; } = k6;
}

/// <summary>
/// An 8-key projection envelope. See <see cref="ProjectionEnvelope{TOut, TK0}"/> for usage.
/// </summary>
/// <typeparam name="TOut">The projected DTO type.</typeparam>
/// <typeparam name="TK0">The CLR type of key column 0.</typeparam>
/// <typeparam name="TK1">The CLR type of key column 1.</typeparam>
/// <typeparam name="TK2">The CLR type of key column 2.</typeparam>
/// <typeparam name="TK3">The CLR type of key column 3.</typeparam>
/// <typeparam name="TK4">The CLR type of key column 4.</typeparam>
/// <typeparam name="TK5">The CLR type of key column 5.</typeparam>
/// <typeparam name="TK6">The CLR type of key column 6.</typeparam>
/// <typeparam name="TK7">The CLR type of key column 7.</typeparam>
/// <param name="item">The projected DTO.</param>
/// <param name="k0">The value of key column 0.</param>
/// <param name="k1">The value of key column 1.</param>
/// <param name="k2">The value of key column 2.</param>
/// <param name="k3">The value of key column 3.</param>
/// <param name="k4">The value of key column 4.</param>
/// <param name="k5">The value of key column 5.</param>
/// <param name="k6">The value of key column 6.</param>
/// <param name="k7">The value of key column 7.</param>
internal readonly struct ProjectionEnvelope<TOut, TK0, TK1, TK2, TK3, TK4, TK5, TK6, TK7>(TOut item, TK0 k0, TK1 k1, TK2 k2, TK3 k3, TK4 k4, TK5 k5, TK6 k6, TK7 k7)
{
    /// <summary>Gets the projected DTO.</summary>
    public TOut Item { get; } = item;

    /// <summary>Gets the value of key column 0.</summary>
    public TK0 K0 { get; } = k0;

    /// <summary>Gets the value of key column 1.</summary>
    public TK1 K1 { get; } = k1;

    /// <summary>Gets the value of key column 2.</summary>
    public TK2 K2 { get; } = k2;

    /// <summary>Gets the value of key column 3.</summary>
    public TK3 K3 { get; } = k3;

    /// <summary>Gets the value of key column 4.</summary>
    public TK4 K4 { get; } = k4;

    /// <summary>Gets the value of key column 5.</summary>
    public TK5 K5 { get; } = k5;

    /// <summary>Gets the value of key column 6.</summary>
    public TK6 K6 { get; } = k6;

    /// <summary>Gets the value of key column 7.</summary>
    public TK7 K7 { get; } = k7;
}
