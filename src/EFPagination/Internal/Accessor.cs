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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetProperty(Type type, string name, [MaybeNullWhen(false)] out PropertyInfo property)
        => s_cache.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .ToFrozenDictionary(p => p.Name, StringComparer.Ordinal))
          .TryGetValue(name, out property);
}
