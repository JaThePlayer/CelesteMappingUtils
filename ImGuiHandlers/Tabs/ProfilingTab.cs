using Celeste.Mod.MappingUtils.ModIntegration;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Celeste.Mod.MappingUtils.Helpers;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal class ProfilingTab : Tab
{
    public override string Name => "Profiling";

    public override bool CanBeVisible() => true;

    public static int ClearFrameCount = 5;
    public static int FramesUntilClear = ClearFrameCount;

    private static bool HooksLoaded;

    private static FrameInfo? DisplayedInfo;
    private static FrameInfo CurrentInfo = new();


    #region Hooks
    private static readonly ILContext.Manipulator EntityListRenderManipulator = (il) => InjectProfiling<Entity, Entity>(il, "Render", "Render", EndRender, (c) => c.Emit(OpCodes.Dup));
    private static readonly ILContext.Manipulator EntityListUpdateManipulator = (il) => InjectProfiling<Entity, Entity>(il, "_PreUpdate", "_PostUpdate", EndUpdate, (c) => c.Emit(OpCodes.Dup));

    private static readonly ILContext.Manipulator RendererListBeforeRenderManipulator =
        (il) => InjectProfiling<Renderer, Renderer>(il, "BeforeRender", "BeforeRender", EndBeforeRender, (c) =>
    {
        c.Emit(OpCodes.Call, typeof(Draw).GetProperty("Renderer")!.GetGetMethod()!);
    });

    private static readonly ILContext.Manipulator RendererListRenderManipulator =
    (il) => InjectProfiling<Renderer, Renderer>(il, "Render", "Render", EndRender, (c) =>
    {
        c.Emit(OpCodes.Call, typeof(Draw).GetProperty("Renderer")!.GetGetMethod()!);
    });

    private static readonly ILContext.Manipulator RendererListUpdateManipulator =
        //(il) => InjectProfiling<List<Renderer>.Enumerator, Renderer>(il, "get_Current", "Update", EndUpdate, (c) => c.Emit(OpCodes.Dup));
        (il) => InjectProfilingAfterEnumeratorGet<Renderer>(il, "Update", EndUpdate, (c) => c.Emit(OpCodes.Dup));

    private static readonly ILContext.Manipulator BackdropRendererRenderManipulator = (il) => InjectProfiling<Backdrop, Backdrop>(il, "Render", "Render", EndRender, GetRendererInBackdropRendererFunc);
    private static readonly ILContext.Manipulator BackdropRendererBeforeRenderManipulator = (il) => InjectProfiling<Backdrop, Backdrop>(il, "BeforeRender", "BeforeRender", EndBeforeRender, GetRendererInBackdropRendererFunc);
    private static readonly ILContext.Manipulator BackdropRendererUpdateManipulator
        //= (il) => InjectProfiling<List<Backdrop>.Enumerator, Backdrop>(il, "get_Current", "Update", EndUpdate, (c) => c.Emit(OpCodes.Dup));
        = (il) => InjectProfilingAfterEnumeratorGet<Backdrop>(il, "Update", EndUpdate, (c) => c.Emit(OpCodes.Dup));

    private static void GetRendererInBackdropRendererFunc(ILCursor c)
    {
        var backdropLoc = c.Body.Variables.First(v => v.VariableType.Is(typeof(Backdrop))) ?? throw new Exception("Didn't find backdrop local");

        c.Emit(OpCodes.Ldloc, backdropLoc);
    }

    private static readonly List<ILHook> ArbitraryHooks = new();

    public static void LoadHooksIfNeeded()
    {
        if (HooksLoaded)
            return;
        HooksLoaded = true;

        IL.Monocle.EntityList.RenderExcept += EntityListRenderManipulator;
        IL.Monocle.EntityList.RenderOnly += EntityListRenderManipulator;
        IL.Monocle.EntityList.Update += EntityListUpdateManipulator;

        IL.Monocle.RendererList.BeforeRender += RendererListBeforeRenderManipulator;
        IL.Monocle.RendererList.Render += RendererListRenderManipulator;
        IL.Monocle.RendererList.Update += RendererListUpdateManipulator;

        IL.Celeste.BackdropRenderer.Render += BackdropRendererRenderManipulator;
        IL.Celeste.BackdropRenderer.BeforeRender += BackdropRendererBeforeRenderManipulator;
        IL.Celeste.BackdropRenderer.Update += BackdropRendererUpdateManipulator;
        IL.Celeste.Level.BeforeRender += Level_BeforeRender1;
        
        ArbitraryHooks.Add(new ILHook(
            typeof(Player).GetMethod("orig_Update", BindingFlags.Instance | BindingFlags.Public)!,
            PlayerOrigUpdate_SplitTriggerOnStay));

        MappingUtilsModule.OnUnload += UnloadHooksIfNeeded;

        if (MappingUtilsModule.Settings is { ProfilerHookedMethods: { Count: > 0 } hookedMethods }
        && FrostHelperAPI.LoadIfNeeded() && FrostHelperAPI.EntityNameToTypeOrNull is { } nameToType)
        {
            foreach (var hookedMethod in hookedMethods)
            {
                var method = hookedMethod.FindMethod();
                if (method is { })
                {
                    var hook = new ILHook(method, (il) => InjectProfiling(il, hookedMethod.Name));
                    ArbitraryHooks.Add(hook);
                }
            }
        }
    }

    /// <summary>
    /// Profile Trigger.OnStay and make sure that time spent in OnStay is not added to player.Update.
    /// </summary>
    private static void PlayerOrigUpdate_SplitTriggerOnStay(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<Trigger>(nameof(Trigger.OnStay))))
        {
            Logger.Log(LogLevel.Warn, "MappingUtils.Profiling", $"Failed to apply {il.Method.GetID()} hook!");
            return;
        }

        var infoLoc = new VariableDefinition(il.Import(typeof(Info)));
        cursor.Body.Variables.Add(infoLoc);

        // First, this is taking place inside of Player.Update, and we want to count OnStay seperately.
        // We'll stop profiling Player.Update:
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(EndUpdateNoCountIncrease);

        // Next, profile Trigger.OnStay
        cursor.Emit(OpCodes.Ldloc_S, (byte)10); // TODO: this seems bad but it should be fine since there won't be any updates anymore???
        cursor.EmitDelegate(BeginFor);
        cursor.Emit(OpCodes.Stloc, infoLoc);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<Trigger>(nameof(Trigger.OnStay)));

        // stop profiling the trigger
        cursor.Emit(OpCodes.Ldloc, infoLoc);
        cursor.EmitDelegate(EndOnStay);

        // begin profiling the player again - the hook on EntityList.Update will handle ending the profiling
        // (we can safely pop the Info object, because its the same object that the other hook will use)
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(BeginFor);
        cursor.EmitPop();
    }

    private static void InjectProfiling(ILContext il, string name)
    {
        var cursor = new ILCursor(il);

        var infoLoc = new VariableDefinition(il.Import(typeof(Info)));
        cursor.Body.Variables.Add(infoLoc);

        cursor.Emit(OpCodes.Ldstr, name);
        cursor.EmitDelegate(BeginForArbitrary);
        cursor.Emit(OpCodes.Stloc, infoLoc);

        while (cursor.TryGotoNext(MoveType.Before, instr => instr.MatchRet()))
        {
            cursor.Emit(OpCodes.Ldloc, infoLoc);
            cursor.EmitDelegate(EndArbitrary);
            cursor.GotoNext(MoveType.After, instr => instr.MatchRet());
        }
    }

    private static void Level_BeforeRender1(ILContext il)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchLdfld<BeforeRenderHook>(nameof(BeforeRenderHook.Callback))))
        {
            Logger.Log(LogLevel.Warn, "MappingUtils.Profiling", $"Failed to apply {il.Method.GetID()} hook!");
            return;
        }

        var infoLoc = new VariableDefinition(il.Import(typeof(Info)));
        cursor.Body.Variables.Add(infoLoc);

        cursor.Emit(OpCodes.Dup);
        cursor.EmitDelegate(GetEntityFromBeforeRenderHook);
        cursor.EmitDelegate(BeginFor);
        cursor.Emit(OpCodes.Stloc, infoLoc);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<Action>(nameof(Action.Invoke)));

        cursor.Emit(OpCodes.Ldloc, infoLoc);
        cursor.EmitDelegate(EndBeforeRender);
    }

    private static Entity GetEntityFromBeforeRenderHook(BeforeRenderHook hook)
        => hook.Entity;

    private static void InjectProfiling<StartT, EndT>(ILContext il, string startingFuncName, string endingFuncName, Action<Info> onEnd, Action<ILCursor> selfRetriever)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.Before, instr => instr.MatchCallOrCallvirt<StartT>(startingFuncName)))
        {
            Logger.Log(LogLevel.Warn, "MappingUtils.Profiling", $"Failed to apply {il.Method.GetID()} hook!");
            return;
        }

        var infoLoc = new VariableDefinition(il.Import(typeof(Info)));
        cursor.Body.Variables.Add(infoLoc);

        selfRetriever(cursor);
        cursor.EmitDelegate(BeginFor);
        cursor.Emit(OpCodes.Stloc, infoLoc);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<EndT>(endingFuncName));

        cursor.Emit(OpCodes.Ldloc, infoLoc);
        cursor.EmitDelegate(onEnd);
    }

    private static void InjectProfilingAfterEnumeratorGet<EndT>(ILContext il, string endingFuncName, Action<Info> onEnd, Action<ILCursor> selfRetriever)
    {
        var cursor = new ILCursor(il);

        if (!cursor.TryGotoNext(MoveType.After, instr => instr.Match(OpCodes.Call) && ((MethodReference)instr.Operand).Name == "get_Current"))
        {
            Logger.Log(LogLevel.Warn, "MappingUtils.Profiling", $"Failed to apply {il.Method.GetID()} hook!");
            return;
        }

        var infoLoc = new VariableDefinition(il.Import(typeof(Info)));
        cursor.Body.Variables.Add(infoLoc);

        selfRetriever(cursor);
        cursor.EmitDelegate(BeginFor);
        cursor.Emit(OpCodes.Stloc, infoLoc);

        cursor.GotoNext(MoveType.After, instr => instr.MatchCallOrCallvirt<EndT>(endingFuncName));

        cursor.Emit(OpCodes.Ldloc, infoLoc);
        cursor.EmitDelegate(onEnd);
    }

    public static void UnloadHooksIfNeeded()
    {
        if (!HooksLoaded)
            return;
        HooksLoaded = false;

        IL.Monocle.EntityList.RenderExcept -= EntityListRenderManipulator;
        IL.Monocle.EntityList.RenderOnly -= EntityListRenderManipulator;
        IL.Monocle.EntityList.Update -= EntityListUpdateManipulator;

        IL.Monocle.RendererList.BeforeRender -= RendererListBeforeRenderManipulator;
        IL.Monocle.RendererList.Render -= RendererListRenderManipulator;
        IL.Monocle.RendererList.Update -= RendererListUpdateManipulator;

        IL.Celeste.BackdropRenderer.Render -= BackdropRendererRenderManipulator;
        IL.Celeste.BackdropRenderer.BeforeRender -= BackdropRendererBeforeRenderManipulator;
        IL.Celeste.BackdropRenderer.Update -= BackdropRendererUpdateManipulator;
        IL.Celeste.Level.BeforeRender -= Level_BeforeRender1;

        foreach (var h in ArbitraryHooks)
        {
            h.Undo();
            h.Dispose();
        }
        ArbitraryHooks.Clear();
    }

    private static Info BeginFor(object entity)
    {
        var info = CurrentInfo.GetFor(entity.GetType());
        info.Begin();

        return info;
    }

    private static Info BeginForArbitrary(string method)
    {
        var info = CurrentInfo.GetFor(method);
        info.Begin();

        return info;
    }

    private static void EndRender(Info info) => info.Stop(info.Render);

    private static void EndUpdate(Info info) => info.Stop(info.Update);
    private static void EndUpdateNoCountIncrease(Entity entity)
    {
        var info = CurrentInfo.GetFor(entity);
        info.StopNoCountIncrease(info.Update);
    }

    private static void EndBeforeRender(Info info) => info.Stop(info.BeforeRender);
    
    private static void EndOnStay(Info info) => info.Stop(info.OnStay);

    private static void EndArbitrary(Info info) => info.Stop(info.Update);
    #endregion

    public override void OnOpen()
    {
        base.OnOpen();
        LoadHooksIfNeeded();
    }

    public override void OnClose()
    {
        base.OnClose();
        UnloadHooksIfNeeded();
    }

    private static string GetName(OneOf<Type, string> typeOrString) => typeOrString.Match(
        (Type t) => t.Name,
        (string name) => name
    );
    
    private static string GetFullName(OneOf<Type, string> typeOrString) => typeOrString.Match(
        (Type t) => t.FullName ?? t.Name,
        (string name) => name
    );

    private static readonly Dictionary<string, string> ColumnTooltips = new()
    {
        ["Type"] = "The short name of the entity types.\nHover over specific names to get more information.",
        ["#"] = "The amount of entities of this type that got profiled.\nThis might not necessarily be equal to the real amount of entities of this type in the room.",
        ["Total%"] = "The percent of the total profiled time spent by this entity type.",
        ["Frame%"] = "The percent of the target frame time (1/60th of a second) spent by this entity type.",
        ["Total"] = "The total time spent by this entity type.",
        ["Update"] = "The time spent by this entity type in the Update method.",
        ["Render"] = "The time spent by this entity type in the Render method.",
        ["BeforeRender"] = "The time spent by this entity type in the BeforeRender method.",
    };

    public override void Render(Level? level)
    {
        LoadHooksIfNeeded();

        var flags = ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings;
        var textBaseWidth = ImGui.CalcTextSize("A").X;

        if (ImGui.InputInt("Refresh Rate (frames)", ref ClearFrameCount).WithTooltip("How often the table below should get refreshed.\nIf this is above 1, the shown results will be an average."))
        {
            ClearFrameCount = Math.Max(ClearFrameCount, 1);
            FramesUntilClear = ClearFrameCount;
            CurrentInfo = new();
            DisplayedInfo = CurrentInfo;
        }

        DisplayedInfo ??= CurrentInfo;

        var allInfos = DisplayedInfo.GetInfos().MergeBy(GetFullName, (a, b) => a.CreateMerged(b)).ToList();

        var orderedInfo = allInfos.OrderByDescending(i => i.Item2.TotalTime);
        var totalTime = allInfos.Aggregate(TimeSpan.Zero, (t, kv) => t + kv.Item2.TotalTime);
        var frameTime = TimeSpan.FromSeconds(1 / 60.0d);

        ImGuiUtf8.TextUnformatted($"FPS Score: {(int)(60 / (totalTime.TotalSeconds / ClearFrameCount))}");
        ImGuiExt.AddTooltip("""
            The amount of FPS you'd get if the framerate was uncapped and the time tracked by the profiler was all the time the game spent each frame.
            In reality, you'd never be able to get even close to this FPS, but this number can be used to compare the performance of different rooms/maps.
            Never compare the FPS Score between different computers, as its hardware-dependent.
            """);

        const int columnCount = 9;
        if (!ImGui.BeginTable("Perf", columnCount, flags))
        {
            return;
        }

        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("#", ImGuiTableColumnFlags.WidthFixed, textBaseWidth * 2f);
        ImGui.TableSetupColumn("Total%");
        ImGui.TableSetupColumn("Frame%");
        ImGui.TableSetupColumn("Total");
        ImGui.TableSetupColumn("Update");
        ImGui.TableSetupColumn("Render");
        ImGui.TableSetupColumn("BeforeRender");
        ImGui.TableSetupColumn("OnStay");

        //ImGui.TableHeadersRow();
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        int columns = ImGui.TableGetColumnCount();
        for (int columnN = 0; columnN < columns; columnN++)
        {
            if (!ImGui.TableSetColumnIndex(columnN))
                continue;

            var name = ImGui.TableGetColumnName(columnN);

            ImGui.TableHeader(name);

            if (ColumnTooltips.TryGetValue(name, out var tooltip))
            {
                ImGuiExt.AddTooltip(tooltip);
            }
        }
        
        ImGui.TableSetupScrollFreeze(columnCount, 1);

        foreach (var (name, info) in orderedInfo)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var declaringType = name.Match(
                (Type t) => t,
                (string str) => MappingUtilsModule.Settings.ProfilerHookedMethods.FirstOrDefault(m => m.Name == str)?.FindMethod()?.DeclaringType
            );
            
            ImGui.Text(GetName(name));
            if (declaringType is { })
            {
                ImGuiExt.AddDecompilationTooltip(declaringType, null);
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();

                var fullName = name.Match(
                    t => t.FullName,
                    str => MappingUtilsModule.Settings.ProfilerHookedMethods.FirstOrDefault(m => m.Name == str)?.FindMethod()?.GetID() ?? "unknown"
                );

                ImGuiUtf8.TextUnformatted($"Full Name: {fullName}");

                if (name.AsT1() is { } type && FrostHelperAPI.LoadIfNeeded() && FrostHelperAPI.EntityNameFromType is { } nameFromType
                    && nameFromType(type) is { } sid)
                {
                    ImGuiUtf8.TextUnformatted($"SID: {sid}");
                }

                ImGui.EndTooltip();
            }

            ImGui.TableNextColumn();
            ImGuiUtf8.TextUnformatted($"{info.Count / ClearFrameCount}");
            RenderPercent(info.TotalTime, totalTime, 2f);
            RenderPercent(info.TotalTime, frameTime * ClearFrameCount, 20f);

            RenderTime(info.TotalTime, frameTime * ClearFrameCount);
            RenderTime(info.Update.Time, frameTime * ClearFrameCount);
            RenderTime(info.Render.Time, frameTime * ClearFrameCount);
            RenderTime(info.BeforeRender.Time, frameTime * ClearFrameCount);
            RenderTime(info.OnStay.Time, frameTime * ClearFrameCount);
            
            static void RenderPercent(TimeSpan time, TimeSpan totalTime, float colorMult)
            {
                var p = time / totalTime;

                var color = Color.Lerp(Color.White, Color.Red, (float)p * colorMult);

                ImGui.TableNextColumn();
                ImGui.TextColored(color.ToNumVec4(), Interpolator.Temp($"{(p*100):F1}%%"));
            }

            static void RenderTime(TimeSpan time, TimeSpan totalTime)
            {
                ImGui.TableNextColumn();
                
                double timeMs = (time.TotalMilliseconds / ClearFrameCount);
                if (timeMs <= 0)
                    return;
                
                var color = Color.Lerp(Color.White, Color.Red, (float)(time / totalTime) * 20f);
                ImGui.TextColored(color.ToNumVec4(), Interpolator.Temp($"{timeMs:F3}"));
            }
        }


        ImGui.EndTable();

        FramesUntilClear--;
        if (FramesUntilClear == 0)
        {
            FramesUntilClear = ClearFrameCount;

            DisplayedInfo = CurrentInfo;
            CurrentInfo = new();
        }
    }

    public class FrameInfo
    {
        private Dictionary<Type, Info> EntityInfo = new();
        private Dictionary<string, Info> ArbitraryMethodInfo = new();

        public IEnumerable<(OneOf<Type, string>, Info)> GetInfos()
        {
            foreach (var (t, i) in EntityInfo)
            {
                yield return (new(t), i);
            }
            foreach (var (t, i) in ArbitraryMethodInfo)
            {
                yield return (new(t), i);
            }
        }

        public void Clear()
        {
            EntityInfo.Clear();
        }

        public Info GetFor(string arbitraryMethod)
        {
            if (!ArbitraryMethodInfo.TryGetValue(arbitraryMethod, out var info))
            {
                info = new Info();
                ArbitraryMethodInfo.Add(arbitraryMethod, info);
            }

            return info;
        }

        public Info GetFor(Entity entity)
        {
            var t = entity.GetType();

            return GetFor(t);
        }

        public Info GetFor(Backdrop entity)
        {
            var t = entity.GetType();

            return GetFor(t);
        }

        public Info GetFor(Type t)
        {
            if (!EntityInfo.TryGetValue(t, out var info))
            {
                info = new Info();
                EntityInfo.Add(t, info);
            }

            return info;
        }
    }

    public class Info
    {
        public Section Update = new();
        public Section Render = new();
        public Section BeforeRender = new();
        public Section OnStay = new();

        public TimeSpan TotalTime => Update.Time + Render.Time + BeforeRender.Time + OnStay.Time;
        public int Count => Math.Max(Update.Count, Math.Max(Render.Count, Math.Max(BeforeRender.Count, OnStay.Count)));

        private long StartTimestamp;

        public Info CreateMerged(Info other)
        {
            var merged = new Info();
            merged.Update.Time = Update.Time + other.Update.Time;
            merged.Render.Time = Render.Time + other.Render.Time;
            merged.BeforeRender.Time = BeforeRender.Time + other.BeforeRender.Time;
            merged.OnStay.Time = OnStay.Time + other.OnStay.Time;
            
            merged.Update.Count = Update.Count + other.Update.Count;
            merged.Render.Count = Render.Count + other.Render.Count;
            merged.BeforeRender.Count = BeforeRender.Count + other.BeforeRender.Count;
            merged.OnStay.Count = OnStay.Count + other.OnStay.Count;

            return merged;
        }

        public void Begin()
        {
            StartTimestamp = Stopwatch.GetTimestamp();
        }

        public void Stop(Section into)
        {
            var elapsed = Stopwatch.GetElapsedTime(StartTimestamp);
            into.Time += elapsed;
            into.Count++;
        }
        
        /// <summary>
        /// Same as Stop, but doesn't decreate 'Count', used when the profiling has to be stopped midway through a frame.
        /// </summary>
        /// <param name="into"></param>
        public void StopNoCountIncrease(Section into)
        {
            var elapsed = Stopwatch.GetElapsedTime(StartTimestamp);
            into.Time += elapsed;
        }

        public class Section
        {
            public int Count;
            public TimeSpan Time;
        }
    }
}
