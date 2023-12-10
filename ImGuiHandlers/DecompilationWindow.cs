using System.Threading.Tasks;
using Celeste.Mod.MappingUtils.ModIntegration;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public class DecompilationWindow(Type type) : ImGuiHandler
{
    public Type Type => type;
    
    private readonly object _sourceLock = new();
    
    private bool _decompilationStarted;
    private bool _decompilationSucceded;
    private bool _decompilationFinished;

    private string? _source;
    
    public override void Render()
    {
        base.Render();
        
        bool open = true;
        ImGui.SetNextWindowSize(new(500f, 800f), ImGuiCond.Once);
        if (!ImGui.Begin($"Decompilation - {type}", ref open))
        {
            if (!open)
            {
                Engine.Scene.OnEndOfFrame += () => ImGuiManager.Handlers.Remove(this);
            }
            return;
        }
            
        
        if (!_decompilationStarted)
        {
            _decompilationStarted = true;
            Task.Run(async () =>
            {
                var (success, msg) = await IlSpyCmd.DecompileAsync(type);

                lock (_sourceLock)
                {
                    _source = msg;
                    _decompilationSucceded = success;
                    _decompilationFinished = true;
                }
            });
        }

        lock (_sourceLock)
        {
            var text = (_decompilationSucceded, _decompilationFinished, _source) switch
            {
                (true, true, var msg) => msg ?? "???",
                (false, true, var msg) => $"""
                                    Decompilation failed.
                                    Do you have ilspycmd installed globally?
                                    If not, run 'dotnet tool install ilspycmd -g' in the command line to install it.
                                    
                                    Error message: {msg}
                                    """,
                (_, false, _) => "Decompiling...",
            };

            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x00000000);
            
            var size = ImGui.GetWindowSize();
            size.Y -= ImGui.GetFrameHeightWithSpacing() * 2;
            ImGui.InputTextMultiline("", ref text, (uint)text.Length, size, ImGuiInputTextFlags.ReadOnly);
            
            ImGui.PopStyleColor(1);
        }
        
        ImGui.End();
        
        if (!open)
        {
            Engine.Scene.OnEndOfFrame += () => ImGuiManager.Handlers.Remove(this);
        }
    }
}