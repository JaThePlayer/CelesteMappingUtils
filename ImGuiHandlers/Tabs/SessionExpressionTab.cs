using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.MappingUtils.ModIntegration;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;

internal record WatchedExpression(string Text, SessionExpressionDisplayParts? DisplayParts);

internal sealed class SessionExpressionTab : Tab
{
    private object? _inspector;

    private readonly List<string> _sessionExpressionEvaluationErrors = [];

    public List<WatchedExpression> WatchedExpressions { get; private set; } = [];
    
    public override string Name => "Session Expressions";

    public override bool CanBeVisible()
    {
        return Engine.Scene is Level && FrostHelperAPI.CreateSessionExpressionInspector is not null;
    }

    public override void OnOpen()
    {
        WatchedExpressions =
            MappingUtilsModule.Settings.WatchedSessionExpressions
                .Select(x =>
                {
                    var inspector = FrostHelperAPI.CreateSessionExpressionInspector?.Invoke(null);
                    if (inspector is null)
                        return null;

                    FrostHelperAPI.SetInspectorText!(inspector, x);
                    
                    if (FrostHelperAPI.GetInspectorErrors!(inspector).Count > 0)
                        return new WatchedExpression(x, null);
                    
                    return new WatchedExpression(x, FrostHelperAPI.GetInspectorRenderParts!(inspector));
                })
                .Where(x => x is not null)
                .ToList()!;
        base.OnOpen();
    }

    private int _selectedCompletionId = 0;
    
    public override void Render(Level? level)
    {
        if (level is null)
            return;
        
        _inspector ??= FrostHelperAPI.CreateSessionExpressionInspector?.Invoke(null);
        if (_inspector is null)
            return;
        
        ImGui.TextLinkOpenURL("Session Expression basics", "https://github.com/JaThePlayer/FrostHelper/wiki/Session-Expressions");
        ImGui.TextLinkOpenURL("Session Expression reference", "https://github.com/JaThePlayer/FrostHelper/wiki/Session-Expressions-Reference");
        
        var text = FrostHelperAPI.GetInspectorText!(_inspector);
        
        var pos = ImGui.GetCursorPos();
        //ImGui.PushStyleColor(ImGuiCol.Text, 0);
        var ret = ImGui.InputText("Expression", ref text, 512);
        //ImGui.PopStyleColor(1);
        //ImGui.SameLine();
        //ImGui.TextUnformatted("Expression");
        
        SetText(text);

        var cursorIdx = text.Length;
        var completions = FrostHelperAPI.GetAutoCompletions?.Invoke(_inspector, cursorIdx);
        
        if (completions?.Count > 0)
        {
            ImGui.SetNextWindowPos(ImGui.GetCursorScreenPos());
            ImGuiWindowFlags flags =
                ImGuiWindowFlags.Tooltip |
                //ImGuiWindowFlags_NoNav |
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize;
            if (ImGui.Begin("Tooltip##InteractiveTooltip", flags))
            {
                if (ImGui.GetIO().KeysData[(int)ImGuiKey.DownArrow].Down > 0)
                {
                    //Input.MenuDown.ConsumePress();
                    _selectedCompletionId++;
                    _selectedCompletionId %= completions.Count;
                }
                if (Input.MenuUp.Pressed)
                {
                    Input.MenuUp.ConsumePress();
                    _selectedCompletionId--;
                    if (_selectedCompletionId < 0)
                        _selectedCompletionId = completions.Count - 1;
                }

                
                var completionI = -1;
                foreach (var completion in completions)
                {
                    ImGui.PushID(completionI++);
                    var size = completion.Name.Sum(x => ImGui.CalcTextSize(x.Contents).X);
                    var tooltipPos = ImGui.GetCursorPos();
                    RenderDisplayParts(completion.Name, selectable: false);
                    ImGui.SetCursorPos(tooltipPos);
                    var clicked = ImGui.Selectable("", completionI == _selectedCompletionId, ImGuiSelectableFlags.None/*, new NumVector2(size, ImGui.GetTextLineHeightWithSpacing())*/);
                    //ImGui.NewLine();

                    if (clicked || (completionI == _selectedCompletionId && Input.MenuConfirm.Pressed) )
                    {
                        SetText(completion.Contents);
                    }
                
                    ImGui.PopID();
                }
            
                ImGui.End();
            }
        }
        
        var errors = FrostHelperAPI.GetInspectorErrors!(_inspector);
        if (errors.Count > 0)
        {
            RenderErrorList("Errors while parsing expression:", errors);
        }
        else
        {
            ImGui.SetCursorPos(pos + ImGui.GetStyle().FramePadding);
            var parts = FrostHelperAPI.GetInspectorRenderParts!(_inspector);
            RenderDisplayParts(parts);
            
            ImGui.NewLine();
            
            var evaluated = EvaluateExpression(text, out _);
            if (evaluated is null)
            {
                RenderErrorList("Errors while running expression:", _sessionExpressionEvaluationErrors);
            }
            else
            {
                ImGui.TextUnformatted("Expression result:");
                ImGui.SameLine();
                RenderResult(evaluated);
            }
            
            if (ImGui.Button("Watch expression").WithTooltip("Adds this expression to the table at the bottom to live-update."))
            {
                MappingUtilsModule.Settings.WatchedSessionExpressions.Add(text);
                MappingUtilsModule.Instance.SaveSettings();
                WatchedExpressions.Add(new WatchedExpression(text, parts));
            }
        }
        
        ImGui.SeparatorText("Watched Expressions");
        
        if (!ImGui.BeginTable("Watched Expressions", 3, ImGuiExt.TableFlags | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        ImGui.TableSetupColumn("Expression", ImGuiTableColumnFlags.NoHide | ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Value").X * 2f);
        ImGui.TableSetupColumn(" ", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(" Delete ").X);
        ImGui.TableHeadersRow();
        var i = 0;
        foreach (var expr in WatchedExpressions)
        {
            ImGui.PushID(i++);
            ImGui.TableNextColumn();
            if (expr.DisplayParts is { } parts)
            {
                RenderDisplayParts(parts);
            }
            else
            {
                ImGui.TextUnformatted(expr.Text);
            }
            
            ImGui.TableNextColumn();
            
            var evaluated = EvaluateExpression(expr.Text, out _);
            if (evaluated is null)
            {
                RenderErrorList("<ERROR>", []);
            }
            else
            {
                RenderResult(evaluated);
            }
            
            ImGui.TableNextColumn();
            if (ImGui.Button("Delete##"))
            {
                level.OnEndOfFrame += () =>
                {
                    MappingUtilsModule.Settings.WatchedSessionExpressions.Remove(expr.Text);
                    MappingUtilsModule.Instance.SaveSettings();
                    WatchedExpressions.Remove(expr);
                };
            }
            
            ImGui.TableNextRow();
            ImGui.PopID();
        }
        
        ImGui.EndTable();
    }

    private void SetText(string newText)
    {
        if (_inspector is null)
            return;

        if (FrostHelperAPI.GetInspectorText!(_inspector) == newText)
            return;
        
        FrostHelperAPI.SetInspectorText!(_inspector, newText);
        _selectedCompletionId = 0;
    }

    private static void RenderDisplayParts(IReadOnlyList<ApiSimpleRenderPart> parts, bool selectable = true)
    {
        RenderDisplayParts(parts.Select<ApiSimpleRenderPart, ApiRenderPart>(p => (p.Contents, p.ColorId, null)), selectable);
    }
    
    private static void RenderDisplayParts(IEnumerable<ApiRenderPart> parts, bool selectable = true)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, 0);
        // Make sure the selectables don't render their selection
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x00000000);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x00000000);
        foreach (var (content, colorId, tooltipParts) in parts)
        {
            var color = ResolveColor(colorId);
            ImGui.PushStyleColor(ImGuiCol.Text, color.ToNumVec4());
            if (selectable)
            {
                ImGui.TextUnformatted(content);
            }
            else
            {
                ImGui.Selectable(content, false, ImGuiSelectableFlags.AllowOverlap, ImGui.CalcTextSize(content));
            }
            
            if (!content.EndsWith('\n'))
                ImGui.SameLine(0, 0);
            ImGui.PopStyleColor(1);

            if (selectable && tooltipParts?.Count > 0 && ImGui.IsItemHovered() && ImGui.BeginTooltip())
            {
                foreach (var (tooltipContent, tooltipColor) in tooltipParts)
                {
                    color = ResolveColor(tooltipColor);
                    ImGui.PushStyleColor(ImGuiCol.Text, color.ToNumVec4());
                    ImGui.TextUnformatted(tooltipContent);
                    if (!tooltipContent.EndsWith('\n'))
                        ImGui.SameLine(0, 0);
                    ImGui.PopStyleColor(1);
                }
                
                ImGui.EndTooltip();
            }
        }
        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(1);
    }

    private static void RenderErrorList(string header, IEnumerable<string> errors)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, 0xff0000ff);
        ImGui.TextUnformatted(header);
        foreach (var error in errors)
        {
            ImGui.TextUnformatted($"- {error}");
        }
        ImGui.PopStyleColor(1);
    }
    
    private static void RenderResult(object result)
    {
        if (result is IEnumerable enumerable and not string and not Entity)
        {
            ImGui.TextUnformatted("IEnumerable:");
            var i = 0;
            foreach (var innerResult in enumerable)
            {
                ImGui.TextUnformatted($"- {i++}:");
                ImGui.SameLine();
                RenderResult(innerResult);
            }

            return;
        }
        
        ImGui.TextUnformatted($"{result}");
    }
    
    private object? EvaluateExpression(string expr, out object? expression)
    {
        expression = null;
        if (Engine.Scene is not Level level)
            return null;

        _sessionExpressionEvaluationErrors.Clear();
        using var sink = FrostHelperAPI.RegisterNotificationSink!((logLevel, s) =>
        {
            _sessionExpressionEvaluationErrors.Add(s);
            return false;
        });
        
        if (!FrostHelperAPI.TryCreateSessionExpression!(expr, out expression))
        {
            return null;
        }

        return FrostHelperAPI.GetSessionExpressionValue!(expression, level.Session);
    }

    private static Color ResolveColor(string colorId) => colorId switch
    {
        "string" => Color.Orange,
        "flag" => Color.Gold,
        "counter" => Color.Gold,
        "slider" => Color.Gold,
        "command" => Color.LightSkyBlue,
        "field" => Color.LightSkyBlue,
        "type" => Color.MediumPurple,
        _ => Color.White,
    };
}