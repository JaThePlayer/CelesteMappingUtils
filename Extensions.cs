﻿using System.Collections.Generic;
using System;
using System.Linq;

namespace Celeste.Mod.MappingUtils;

internal static class Extensions
{
    public static System.Numerics.Vector4 ToNumVec4(this Color color)
    {
        var c = color.ToVector4();
        return new System.Numerics.Vector4(c.X, c.Y, c.Z, c.W);
    }

    public static System.Numerics.Vector3 ToNumVec3(this Color color)
    {
        var c = color.ToVector3();
        return new System.Numerics.Vector3(c.X, c.Y, c.Z);
    }

    public static Color ToColor(this System.Numerics.Vector4 vec)
    {
        return new(vec.X, vec.Y, vec.Z, vec.W);
    }

    public static Color ToColor(this System.Numerics.Vector3 vec)
    {
        return new(vec.X, vec.Y, vec.Z);
    }

    /// <summary>
    /// Filters and orders the <paramref name="source"/> using the provided search string and list of favorites, for use with search bars in UI's.
    /// </summary>
    public static IEnumerable<T> SearchFilter<T>(this IEnumerable<T> source, Func<T, string> textSelector, string search, HashSet<string>? favorites = null)
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);

        var filter = source.Select(e => (e, Name: textSelector(e)));

        if (hasSearch)
        {
            var searchSplit = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            filter = filter
                .Where(e => searchSplit.All(search => e.Name.Contains(search, StringComparison.InvariantCultureIgnoreCase))) // filter out materials that don't contain the search
                .OrderOrThenByDescending(e => e.Name.StartsWith(search, StringComparison.InvariantCultureIgnoreCase)); // put materials that start with the search first.
        }

        if (favorites is { })
        {
            filter = filter.OrderOrThenByDescending(e => favorites.Contains(e.Name)); // put favorites in front of other options
        }

        filter = filter.OrderOrThenBy(e => e.Name); // order alphabetically.

        return filter.Select(e => e.e);
    }

    private static IEnumerable<T> OrderOrThenBy<T, T2>(this IEnumerable<T> self, Func<T, T2> selector)
    {
        if (self is IOrderedEnumerable<T> ordered)
            return ordered.ThenBy(selector);
        return self.OrderBy(selector);
    }

    private static IEnumerable<T> OrderOrThenByDescending<T, T2>(this IEnumerable<T> self, Func<T, T2> selector)
    {
        if (self is IOrderedEnumerable<T> ordered)
            return ordered.ThenByDescending(selector);
        return self.OrderByDescending(selector);
    }
}