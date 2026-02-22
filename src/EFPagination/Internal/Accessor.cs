using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EFPagination.Internal;

/// <summary>
/// Provides cached, fast property lookup by name for a given type.
/// Used during loose-typing adaptation to map property names from one type to another.
/// Instances are cached globally per type.
/// </summary>
internal sealed class Accessor
{
    private static readonly ConcurrentDictionary<Type, Accessor> s_typeToAccessorMap = new();

    private readonly FrozenDictionary<string, PropertyInfo> _propertyInfoMap;

    private Accessor(
        FrozenDictionary<string, PropertyInfo> propertyInfoLookup)
    {
        _propertyInfoMap = propertyInfoLookup;
    }

    /// <summary>
    /// Attempts to find a public instance property by name.
    /// </summary>
    /// <param name="key">The property name to look up.</param>
    /// <param name="value">The <see cref="PropertyInfo"/> if found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the property exists; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetProperty(string key, [MaybeNullWhen(false)] out PropertyInfo value) => _propertyInfoMap.TryGetValue(key, out value);

    /// <summary>
    /// Returns the cached <see cref="Accessor"/> for the specified type, creating one if necessary.
    /// </summary>
    /// <param name="type">The type to obtain the accessor for.</param>
    /// <returns>The cached accessor.</returns>
    public static Accessor Obtain(Type type) => s_typeToAccessorMap.GetOrAdd(type, CreateNew);

    private static Accessor CreateNew(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var propertyInfoLookup = new Dictionary<string, PropertyInfo>(
            capacity: properties.Length, comparer: StringComparer.Ordinal);
        foreach (var p in properties)
        {
            propertyInfoLookup[p.Name] = p;
        }

        return new(propertyInfoLookup.ToFrozenDictionary(StringComparer.Ordinal));
    }
}
