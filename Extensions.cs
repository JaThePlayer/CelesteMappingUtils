using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod.MappingUtils;

internal static partial class Extensions
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

    // Same as ToDictionary, but doesn't crash if keys repeat.
    public static Dictionary<TKey, TValue> ToDictionarySafe<TFrom, TKey, TValue>(this IEnumerable<TFrom> self, Func<TFrom, TKey> keySelector, Func<TFrom, TValue> valueSelector,
        IEqualityComparer<TKey>? comparer = null, bool ignoreExceptions = false)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>(comparer);
        foreach (var item in self)
        {
            if (ignoreExceptions)
            {
                try
                {
                    dict[keySelector(item)] = valueSelector(item);
                }
                catch { }
            }
            else
            {
                dict[keySelector(item)] = valueSelector(item);
            }

        }

        return dict;
    }

    public static IEnumerable<(TKey, TValue)> MergeBy<TKey, TInner, TValue>(this IEnumerable<(TKey, TValue)> self, Func<TKey, TInner> keySelector, Func<TValue, TValue, TValue> merger)
        where TInner : notnull
        where TKey : notnull
    {
        var dict = new Dictionary<TInner, (TKey, TValue)>();
        foreach (var (k, val) in self)
        {
            var key = keySelector(k);
            if (dict.TryGetValue(key, out var existing))
            {
                dict[key] = (existing.Item1, merger(existing.Item2, val));
            }
            else
            {
                dict[key] = (k, val);
            }
        }

        foreach (var (_, item) in dict)
        {
            yield return (item.Item1, item.Item2);
        }
    }

    public static string FixedToString(this Instruction self, bool printOffsets = true)
    {
        var operand = self.Operand;
        var opcode = self.OpCode;

        var instruction = new StringBuilder();

        if (printOffsets)
        {
            AppendLabel(instruction, self);
            instruction.Append(':');
            instruction.Append(' ');
        }
        instruction.Append(opcode.Name);

        if (operand == null)
            return instruction.ToString();

        instruction.Append(' ');

        switch (opcode.OperandType)
        {
        case OperandType.ShortInlineBrTarget:
        case OperandType.InlineBrTarget:
            //AppendLabel(instruction, (Instruction)operand);
            AppendLabel(instruction, operand switch
            {
                Instruction instr => instr,
                ILLabel label => label.Target!
            });
            break;
        case OperandType.InlineSwitch:
            var labels = (Instruction[])operand;
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0)
                    instruction.Append(',');

                AppendLabel(instruction, labels[i]);
            }
            break;
        case OperandType.InlineString:
            instruction.Append('\"');
            instruction.Append(operand);
            instruction.Append('\"');
            break;
        case OperandType.InlineMethod:
            // fix issues with instructions not being compared correctly due to things like this:
            //(newobj System.Void System.Nullable`1<Microsoft.Xna.Framework.Rectangle>::.ctor(T), newobj System.Void System.Nullable`1<Microsoft.Xna.Framework.Rectangle>::.ctor(!0))
            var s = operand.ToString()!;
            var sFixed = s.Contains('!') ? FixGenericNamesRegex().Replace(s, (m) =>
            {
                var number = int.Parse(m.ValueSpan[1..]);
                if (number == 0)
                    return "T";
                return $"T{number}";
            }) : s;

            //if (s != sFixed)
            //    Console.WriteLine($"Fixed generic name: {s} -> {sFixed}");

            instruction.Append(sFixed);
            break;
        default:
            instruction.Append(operand);
            break;
        }

        return instruction.ToString();
    }

    static void AppendLabel(StringBuilder builder, Instruction instruction)
    {
        builder.Append("IL_");
        builder.Append(instruction.Offset.ToString("x4"));
    }

    [GeneratedRegex(@"!\d+")]
    private static partial Regex FixGenericNamesRegex();
}
