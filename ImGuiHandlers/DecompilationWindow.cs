using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Celeste.Mod.MappingUtils.Helpers;
using Celeste.Mod.MappingUtils.ModIntegration;
using ImGuiColorTextEditNet;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public class DecompilationWindow(Type type, MethodBase? method = null) : ImGuiHandler
{
    public Type Type => type;
    
    private readonly object _sourceLock = new();
    
    private bool _decompilationStarted;
    private bool _decompilationSucceded;
    private bool _decompilationFinished;

    private string? _source;

    private TextEditor? _editor;
    private string? _lastEditorText;
    
    public override void Render()
    {
        base.Render();

        string text;

        lock (_sourceLock)
        {
            text = (_decompilationSucceded, _decompilationFinished, _source) switch
            {
                (true, true, var msg) => msg ?? "???",
                (false, true, var msg) => $"""
                                           Decompilation failed.

                                           Error message: {msg}
                                           """,
                (_, false, _) => "Decompiling...",
            };
            
            _editor ??= new TextEditor
            {
                AllText = text,
                SyntaxHighlighter = new CSharpStyleHighlighter(),
                Renderer =
                {
                    IsShowingWhitespace = false,
                },
                Options =
                {
                    IsReadOnly = true,
                }
            };
            
            _lastEditorText ??= text;
            if (_lastEditorText != text)
            {
                _editor.AllText = text;
                
                var maxX = 0f;
                var maxY = 0f;

                foreach (var l in _editor.TextLines)
                {
                    var lineSize = ImGui.CalcTextSize(l);
                    maxX = float.Max(lineSize.X, maxX);
                    maxY += lineSize.Y;
                }
                
                var size = new NumVector2(maxX, maxY);
                size.Y += ImGui.GetFrameHeightWithSpacing() * 3;

                size.X = float.Min(size.X, 700f);
                size.Y = float.Min(size.Y, 500f);

                ImGui.SetNextWindowSize(size, ImGuiCond.Always);
            } else
            {
                ImGui.SetNextWindowSize(new(
                    ImGui.CalcTextSize("  Decompiling...  ").X, 
                    ImGui.GetFrameHeightWithSpacing() * 2f + ImGui.GetTextLineHeightWithSpacing()), ImGuiCond.Once);
            }
        }
        
        bool open = true;
        if (!ImGui.Begin($"Decompilation - {type}##{_decompilationFinished}{GetHashCode()}", ref open, ImGuiWindowFlags.NoSavedSettings))
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
                var (success, msg) = await IlSpyCmd.DecompileAsync(type, method);

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
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x00000000);
            
            _editor.Render("");
            
            ImGui.PopStyleColor(1);
        }
        
        ImGui.End();
        
        if (!open)
        {
            Engine.Scene.OnEndOfFrame += () => ImGuiManager.Handlers.Remove(this);
        }
    }
}