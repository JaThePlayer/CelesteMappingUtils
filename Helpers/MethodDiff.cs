using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.MappingUtils.Helpers;

internal class MethodDiff
{
    public record class Instr(ElementType Type, Instruction Instruction, MethodInfo? Source, List<string>? AdditionalInfo);

    public enum ElementType
    {
        Unchanged,
        Added,
        Removed
    }


    public readonly MethodBase Method;

    public IReadOnlyList<Instr> Instructions => _Instructions;

    private List<Instr> _Instructions;

    internal List<string> AppliedHookNames;

    public MethodDiff(MethodBase orig)
    {
        Method = orig;
        var hooked = DetourManager.GetDetourInfo(orig).ILHooks;
        AppliedHookNames = hooked.Select(h => h.ManipulatorMethod.GetID(simple: true)).ToList();

        using var cloneDef = new DynamicMethodDefinition(orig);
        using var cloneDefContext = new ILContext(cloneDef.Definition);

        _Instructions = cloneDef.Definition.Body.Instructions.Select(instr => new Instr(ElementType.Unchanged, instr, null, new(0))).ToList();

        if (hooked is not { })
            return;

        foreach (var hook in hooked)
        {
            // ILHookInfo only gives us public access to the method the manipulator delegate calls.
            // We need to retrieve the actual delegate passed to the original IL hook, as the manipulator method it calls may be non-static...
            // Time to use monomod to access monomod internals :)
            var hookState = new DynamicData(hook).Get("hook")!;
            var manipulator = new DynamicData(hookState).Get<ILContext.Manipulator>("Manip")!;

            try
            {
                manipulator(cloneDefContext);
                var instrs = cloneDefContext.Instrs;

                _Instructions = CreateDiff(_Instructions, instrs, hook.ManipulatorMethod);
            }
            catch (Exception ex)
            {
                Logger.Log("MappingUtils.ILHookDiffer", $"Failed to apply IL hook {hook.ManipulatorMethod.GetID()}: {ex}");
            }
        }
    }

    private static List<Instr> CreateDiff(List<Instr> origInstrs, Mono.Collections.Generic.Collection<Instruction> finInstrs, MethodInfo? def)
    {
        var diff = new List<Instr>(origInstrs.Count);
        var oi = 0;
        var fi = 0;

        while (fi < finInstrs.Count)
        {
            var instr = finInstrs[fi++];
            var origInstr = origInstrs.ElementAtOrDefault(oi);
            if (origInstr is null)
            {
                diff.Add(new(ElementType.Added, instr, def, new()));
                continue;
            }

            //Console.WriteLine((origInstr.Instruction.FixedToString(false), instr.FixedToString(false)));

            if (InstrsEqual(instr, origInstr.Instruction))
            {
                oi++;
                diff.Add(new(origInstr.Type, instr, origInstr.Source, origInstr.AdditionalInfo));
            }
            else
            {
                // search if the instruction we're looking for even exists
                // because there could be simillar code later in the function, we'll limit the search to the next few instructions (random number)
                bool isDeleted = true;
                for (int i = fi; i < finInstrs.Count && i < fi + 15; i++)
                {
                    if (InstrsEqual(finInstrs[i], origInstr.Instruction))
                    {
                        isDeleted = false;
                        break;
                    }
                }
                if (isDeleted)
                {
                    diff.Add(new(ElementType.Removed, origInstr.Instruction, def, origInstr.AdditionalInfo));
                    oi++;
                    fi--;
                    continue;
                }

                var info = new List<string>(0);
                /*
                if (instr.MatchCall(out var v) && v.DeclaringType.Is("MonoMod.Cil.FastDelegateInvokers") && v.Name.Contains("Invoke", StringComparison.Ordinal) &&
                    instr.Previous.Previous.Previous.MatchLdcI4(out var id) &&
                    instr.Previous.Previous.MatchLdcI4(out var hash))
                {
                    //Console.Write($"\n  |-> calls {DynamicReferenceManager.GetValue<Delegate>(new(id, hash))!.Method.GetID()}");
                    info.Add($"calls {DynamicReferenceManager.GetValue<Delegate>(new(id, hash))!.Method.GetID()}");
                }*/
                if (instr.MatchCall(out var v) && v.DeclaringType.Is("MonoMod.Utils.DynamicReferenceManager") && v.Name.Contains("GetValue", StringComparison.Ordinal) &&
                    instr.Previous.Previous.MatchLdcI4(out var id) &&
                    instr.Previous.MatchLdcI4(out var hash))
                {
                    var value = DynamicReferenceManager.GetValue(new(id, hash))!;
                    info.Add(value switch
                    {
                        Delegate d => $"retrieves {d.Method.GetID()}",
                        _ => $"retrieves {value} [{value.GetType().FullName}]"
                    });
                }

                diff.Add(new(ElementType.Added, instr, def, info));
            }
        }

        return diff;
    }

    private static bool InstrsEqual(Instruction a, Instruction b)
    {
        if (a.OpCode != b.OpCode)
        {
            if (ShorthandEqualToLongForm(a, b))
                return true;
            if (ShorthandEqualToLongForm(b, a))
                return true;

            //Console.WriteLine((a.FixedToString(false), b.FixedToString(false)));
            return false;
        }

        if (a.FixedToString(false) == b.FixedToString(false))
            return true;

        var opc = a.OpCode;


        //Console.WriteLine(opc.FlowControl);
        if (opc.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch)
            return true;

        // monomod's MonoMod.Utils.DynamicReferenceManager::GetValueTUnsafe<System.Delegate>(System.Int32,System.Int32)) requires two ints which might as well be random
        // let's ignore int loads relating to this
        if (b.MatchLdcI4(out _))
        {
            if (b.Next?.Next?.FixedToString(false).Contains("MonoMod.Utils.DynamicReferenceManager", StringComparison.Ordinal) ?? false)
                return true;
            if (b.Next?.FixedToString(false).Contains("MonoMod.Utils.DynamicReferenceManager", StringComparison.Ordinal) ?? false)
                return true;
        }

        if (a.OpCode == OpCodes.Callvirt || a.OpCode == OpCodes.Call)
        {
            // generic methods mess stuff up
            //(callvirt T System.Collections.Generic.List`1<Monocle.Entity>::get_Item(System.Int32), callvirt !0 System.Collections.Generic.List`1<Monocle.Entity>::get_Item(System.Int32))
            var am = (MethodReference)a.Operand;
            var bm = (MethodReference)b.Operand;
            //Console.WriteLine((am.GetID(simple: true), bm.GetID(simple: true)));

            if (am.GetID(simple: true) == bm.GetID(simple: true))
                return true;
        }

        //Console.WriteLine((a.FixedToString(false), b.FixedToString(false)));
        return false;

        bool ShorthandEqualToLongForm(Instruction a, Instruction b)
        {
            if (a.MatchLdcI4(out var av) && b.MatchLdcI4(av))
                return true;

            if (a.OpCode.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch
                && a.OpCode.ToLongOp() == b.OpCode.ToLongOp())
            {
                return true;
            }

            if (a.OpCode == OpCodes.Call && b.OpCode == OpCodes.Callvirt)
            {
                return a.Operand == b.Operand;
            }

            return false;
        }
    }

    private void PrintImpl(Action<string?> print, Action<ConsoleColor> setColor, Action resetColor)
    {
        try
        {
            print($"IL Diff: {Method.GetID()}\n");
            foreach (var i in _Instructions)
            {
                switch (i.Type)
                {
                case ElementType.Added:
                    setColor(ConsoleColor.Green);
                    print("+ ");
                    break;
                case ElementType.Removed:
                    setColor(ConsoleColor.Red);
                    print("- ");
                    break;
                default:
                    resetColor();
                    print("  ");
                    break;
                }

                print(i.Instruction.FixedToString());

                if (i.Type != ElementType.Unchanged && i.Source is { } source)
                {
                    setColor(ConsoleColor.Yellow);
                    print($" @ {source.GetID(simple: true)}");
                }

                print("\n");

                if (i.AdditionalInfo is { Count: > 0 } info)
                {
                    setColor(ConsoleColor.DarkYellow);
                    foreach (var item in info)
                    {
                        print($"  |-> {item}\n");
                    }
                }

                resetColor();
            }
        }
        finally
        {
            resetColor();
        }
    }

    public void PrintToStream(Stream stream)
    {
        using var writer = new StreamWriter(stream);

        PrintImpl(writer.Write, setColor: (c) => { }, resetColor: () => { });
    }

    public void PrintToConsole()
    {
        try
        {
            Console.WriteLine();
            PrintImpl(Console.Write, (c) => Console.ForegroundColor = c, Console.ResetColor);
        }
        finally
        {
            Console.ResetColor();
        }
    }
}


