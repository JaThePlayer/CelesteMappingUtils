using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Runtime;
using System.Threading;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Analysis.GC;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal sealed class GcTab : Tab
{
    private object? _diagnosticsClient;
    private LinkedList<float> _pauses = [];
    private float[] _pausesForPlot = [];
    private float[] _pausesForPlotSorted = [];
    
    private long _startTime;
    private double _allocRateMbSec;
    private long _startG0, _startG1, _startG2;
    private GCReason _gcReason = (GCReason)(-1);
    private double _heapSizeBefore, _heapSizeAfter;
    

    private int _maxPauseCount = 10_000;
    
    public override string Name => "GC";

    public override void RenderTooltip()
    {
        ImGuiExt.AddTooltip("Displays Garbage Collection statistics.");
    }

    public override bool CanBeVisible() => true;

    public override void OnOpen()
    {
        base.OnOpen();

        _pauses = [];
        _pausesForPlot = new float[_maxPauseCount];
        _pausesForPlotSorted = new float[_maxPauseCount];

        if (_diagnosticsClient is null)
        {
            var client = new DiagnosticsClient(Environment.ProcessId);
            _diagnosticsClient = client;
            var eventPipeSession = client.StartEventPipeSession([
                new("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, (long)ClrTraceEventParser.Keywords.GC)
            ], false);
            var source = new EventPipeEventSource(eventPipeSession.EventStream);

            source.NeedLoadedDotNetRuntimes();
            source.AddCallbackOnProcessStart(proc =>
            {
                proc.AddCallbackOnDotNetRuntimeLoad(runtime =>
                {
                    runtime.GCEnd += (p, gc) =>
                    {
                        if (p.ProcessID == Environment.ProcessId)
                        {
                            _allocRateMbSec = gc.AllocRateMBSec;
                            _gcReason = gc.Reason;
                            _heapSizeBefore = gc.HeapSizeBeforeMB;
                            _heapSizeAfter = gc.HeapSizeAfterMB;
                            lock (_pauses)
                            {
                                _pauses.AddLast((float)gc.PauseDurationMSec);
                                if (_pauses.Count > _maxPauseCount)
                                    _pauses.RemoveFirst();
                            }
                        }
                    };
                });
            });

            new Thread(() => source.Process()).Start();
        }

        ResetData();
    }

    private void ResetData()
    {
        lock (_pauses)
        {
            _pauses ??= [];
            _pauses.Clear();
            _pausesForPlot.AsSpan().Clear();
            _pausesForPlot.AsSpan().Clear();
            _startTime = Stopwatch.GetTimestamp();
            _startG0 = GC.CollectionCount(0);
            _startG1 = GC.CollectionCount(1);
            _startG2 = GC.CollectionCount(2);
        }
    }

    private readonly ComboCache<GCLatencyMode> _modeCache = new();
    
    public override void Render(Level? level)
    {
        int pauseAmt;
        lock (_pauses)
        {
            if (ImGui.Button("Reset"))
            {
                ResetData();
            }
            pauseAmt = _pauses.Count;
            _pauses.CopyTo(_pausesForPlot, 0);
        }

        var elapsed = Stopwatch.GetElapsedTime(_startTime);
        var elapsedMs = elapsed.TotalMilliseconds;
        var totalPause = _pausesForPlot.Sum();
        
        ImGuiUtf8.TextUnformatted($"Elapsed Time: {elapsed.TotalSeconds:F1}s");
        ImGuiUtf8.TextUnformatted($"Max GC Pause: {_pausesForPlot.MaxSafe(0)}ms");
        ImGuiUtf8.TextUnformatted($"Average GC Pause: {totalPause.SafeDivide(pauseAmt, 0)}ms");
        ImGuiUtf8.TextUnformatted($"Total GC Pause: {totalPause}ms ({(totalPause / elapsedMs) * 100.0:F2}%)");
        ImGuiUtf8.TextUnformatted($"GC Count: G0 {GC.CollectionCount(0) - _startG0}, G1 {GC.CollectionCount(1) - _startG1}, G2 {GC.CollectionCount(2) - _startG2}");
        
        ImGuiUtf8.TextUnformatted($"Alloc Rate: {_allocRateMbSec:F2}MB/s, {_allocRateMbSec * 1024:F2}KB/s");
        ImGui.SeparatorText("Last GC");
        ImGuiUtf8.TextUnformatted($"Reason: {_gcReason}");
        ImGuiUtf8.TextUnformatted($"Heap Size: {_heapSizeBefore:F2}mb -> {_heapSizeAfter:F2}mb (diff: {_heapSizeAfter - _heapSizeBefore:F2}mb))");

        var mode = GCSettings.LatencyMode;
        if (ImGuiExt.EnumCombo("GC Mode", ref mode, _modeCache))
        {
            GCSettings.LatencyMode = mode;
        }
        
        /*
        _pausesForPlot.CopyTo(_pausesForPlotSorted, 0);
        _pausesForPlotSorted.AsSpan().Sort();
        ImGui.Text($"P99.9 GC Pause: {_pausesForPlotSorted.Take((int)(pauseAmt * 0.999)).SafeMax(0)}ms");
        ImGui.Text($"P99 GC Pause: {_pausesForPlotSorted.Take((int)(pauseAmt * 0.99)).SafeMax(0)}ms");
        ImGui.Text($"P95 GC Pause: {_pausesForPlotSorted.Take((int)(pauseAmt * 0.95)).SafeMax(0)}ms");
        ImGui.Text($"P90 GC Pause: {_pausesForPlotSorted.Take((int)(pauseAmt * 0.9)).SafeMax(0)}ms");
        ImGui.Text($"P80 GC Pause: {_pausesForPlotSorted.Take((int)(pauseAmt * 0.8)).SafeMax(0)}ms");
        */
        ImGui.PlotHistogram("GC Times:", ref _pausesForPlot[0], _pausesForPlot.Length, 0, "GC Pause Times", 0f, 32f, ImGui.GetContentRegionAvail());
    }
}