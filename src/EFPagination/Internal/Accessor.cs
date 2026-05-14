using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Globally cached <see cref="FrozenDictionary{TKey, TValue}"/> property-name lookup per type.
/// Used during loose-typing adaptation to map property names from one type to another.
/// </summary>
internal static class AccessorCache
{
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<string, PropertyInfo>> s_cache = new();

    /// <summary>
    /// Looks up a public instance property by name on <paramref name="type"/>, caching the
    /// per-type lookup table across calls.
    /// </summary>
    /// <param name="type">The type to search.</param>
    /// <param name="name">The case-sensitive property name to look up.</param>
    /// <param name="property">When this method returns <see langword="true"/>, the matched <see cref="PropertyInfo"/>.</param>
    /// <returns><see langword="true"/> if the property exists; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetProperty(Type type, string name, [MaybeNullWhen(false)] out PropertyInfo property)
        => s_cache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .ToFrozenDictionary(p => p.Name, StringComparer.Ordinal))
          .TryGetValue(name, out property);
}
