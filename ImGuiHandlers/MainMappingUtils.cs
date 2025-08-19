using Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;
using Celeste.Mod.MappingUtils.ModIntegration;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public class MainMappingUtils : ImGuiHandler
{
    public static List<Tab> Tabs { get; private set; } =
    [
        new MetadataTab(),
        new FlagTab(),
        new CountersTab(),
        new CheatTab(),
        new StylegroundViewTab(),
        new ProfilingTab(),
        new HooksTab(),
        new ParticleTab(),
        new GcTab(),
        // new LogTab(), //- not quite there yet, doesn't live reload :/
        //new EntitiesTab(), // not sure how useful this is yet
    ];

    public static bool Enabled { get; set; }

    public const int ItemWidth = 150;

    private static bool _firstUseSinceEnabled = false;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (MappingUtilsModule.Settings?.OpenMenu?.Pressed ?? false)
        {
            Enabled = !Enabled;
            _firstUseSinceEnabled = true;
            MappingUtilsModule.Settings?.OpenMenu.ConsumePress();
            MappingUtilsModule.Settings?.OpenMenu.ConsumeBuffer();

            if (!Enabled)
            {
                foreach (var tab in Tabs)
                {
                    OnTabToggled(tab, false);
                }
            }
        }
    }

    private static void OnTabToggled(Tab tab, bool isOpen)
    {
        if (isOpen && !tab.OpenLastFrame)
        {
            tab.OnOpen();
            tab.OpenLastFrame = true;
        }

        if (!isOpen && tab.OpenLastFrame)
        {
            tab.OpenLastFrame = false;
            tab.OnClose();
        }
    }

    public override unsafe void Render()
    {
        base.Render();
        
        if (!Enabled || Engine.Scene is AssetReloadHelper or LevelLoader or null)
        {
            return;
        }
        
        FrostHelperAPI.LoadIfNeeded();
        
        uint? dockspace = null;
        foreach (var tab in Tabs)
        {
            if (!tab.CanBeVisible())
                continue;
            
            if (dockspace is null)
            {
                ImGui.SetNextWindowPos(new(0, 0), ImGuiCond.FirstUseEver, NumVector2.Zero);
                ImGui.SetNextWindowSize(new(ItemWidth * 2.5f, ImGui.GetMainViewport().Size.Y), ImGuiCond.FirstUseEver);
            }
            else
            {
                ImGui.SetNextWindowDockID(dockspace.Value, ImGuiCond.FirstUseEver);
            }
            
            var tabOpen = ImGui.Begin(tab.CachedNamespacedImguiName ??= $"{tab.Name}##MappingUtils", ImGuiWindowFlags.NoFocusOnAppearing);
            dockspace ??= ImGui.GetWindowDockID();
            
            OnTabToggled(tab, tabOpen);
            if (!tabOpen)
            {
                continue;
            }

            try
            {
                tab.RenderTooltip();
                tab.Render(Engine.Scene as Level);
            }
            finally
            {
                ImGui.End();
            }
        }
        
        /*
        uint id = ImGui.GetID("MappingUtils");
        var node = igDockBuilderGetNode(id);
        var nodeExistsAlready = node != null && node->ChildNodes_A != null;
        var dockspaceId = DockSpaceOverViewport_NoScrollbar(id, ImGui.GetMainViewport(),ImGuiDockNodeFlags.NoDockingOverCentralNode | ImGuiDockNodeFlags.PassthruCentralNode);
        
        
        if (Engine.Scene is not Level level || !Enabled)
        {
            return;
        }

        if (!nodeExistsAlready)
        {
            // Clear out existing layout
            igDockBuilderRemoveNode(dockspaceId);
            igDockBuilderAddNode(dockspaceId, (ImGuiDockNodeFlags)(1 << 10));

            // Main node should cover entire window
            igDockBuilderSetNodeSize(dockspaceId, ImGui.GetMainViewport().Size);

            uint dockSpace2;
            igDockBuilderSplitNode(dockspaceId, ImGuiDir.Left, 0.3f, &dockSpace2, null);
            igDockBuilderFinish(dockspaceId);

            _mappingUtilsDockSpace = dockSpace2;
        }
        else
        {
            _mappingUtilsDockSpace = node->ChildNodes_A->ID;
        }
        igDockBuilderFinish(_mappingUtilsDockSpace);
        var i = 0;

        foreach (var tab in Tabs)
        {
            if (!nodeExistsAlready)
                ImGui.SetNextWindowDockID(_mappingUtilsDockSpace, ImGuiCond.Always);

            var tabOpen = ImGui.Begin(tab.CachedNamespacedImguiName ??= $"{tab.Name}##MappingUtils", ImGuiWindowFlags.DockNodeHost);
            if (!ImGui.IsWindowAppearing())
            {
                if (ImGui.IsWindowFocused() && ImGui.GetWindowDockID() == _mappingUtilsDockSpace)
                    _focusedWindow = i;
                _firstUseSinceEnabled = false;
            }

            i++;
            OnTabToggled(tab, tabOpen);
            if (!tabOpen)
            {
                continue;
            }

            try
            {
                tab.Render(level);
            }
            finally
            {
                ImGui.End();
            }
        }

        if (_firstUseSinceEnabled)
            ImGui.SetWindowFocus(Tabs[_focusedWindow].CachedNamespacedImguiName ??=
                $"{Tabs[_focusedWindow].Name}##MappingUtils");
*/
    }

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe uint igDockBuilderSplitNode(uint nodeId, ImGuiDir splitDir,
        float sizeRatioForNodeAtDir,
        uint* outIdAtDir, uint* outIdAtOppositeDir);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint igDockBuilderRemoveNode(uint nodeId);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderFinish(uint nodeId);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint igDockBuilderAddNode(uint nodeId, ImGuiDockNodeFlags flags);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern void igDockBuilderSetNodeSize(uint nodeId, NumVector2 pos);

    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe ImGuiDockNode* igDockBuilderGetNode(uint nodeId);

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ImGuiDockNode
    {
        public uint                    ID;
        public ImGuiDockNodeFlags      SharedFlags;                // (Write) Flags shared by all nodes of a same dockspace hierarchy (inherited from the root node)
        public ImGuiDockNodeFlags      LocalFlags;                 // (Write) Flags specific to this node
        public ImGuiDockNodeFlags      LocalFlagsInWindows;        // (Write) Flags specific to this node, applied from windows
        public ImGuiDockNodeFlags      MergedFlags;                // (Read)  Effective flags (== SharedFlags | LocalFlagsInNode | LocalFlagsInWindows)
        public ImGuiDockNodeState      State;
        public ImGuiDockNode*          ParentNode;
        public ImGuiDockNode*          ChildNodes_A;
        public ImGuiDockNode*          ChildNodes_B;
        // + much more
    }
    
    enum ImGuiDockNodeState
    {
        Unknown,
        HostWindowHiddenBecauseSingleWindow,
        HostWindowHiddenBecauseWindowsAreResizing,
        HostWindowVisible,
    };
    
    private unsafe uint DockSpaceOverViewport_NoScrollbar(uint dockspaceId, ImGuiViewportPtr viewport,
        ImGuiDockNodeFlags dockspaceFlags)
    {
        if (viewport.NativePtr == null)
            viewport = ImGui.GetMainViewport();

        // Submit a window filling the entire viewport
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        //ImGui.SetNextWindowViewport(viewport.ID);

        ImGuiWindowFlags hostWindowFlags = 0;
        hostWindowFlags |= ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        hostWindowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNavFocus |
                           ImGuiWindowFlags.NoScrollbar;
        if ((dockspaceFlags & ImGuiDockNodeFlags.PassthruCentralNode) != 0)
            hostWindowFlags |= ImGuiWindowFlags.NoBackground;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVector2(0.0f, 0.0f));
        
        ImGui.Begin($"WindowOverViewport_{viewport.ID:X8}", hostWindowFlags);
        ImGui.PopStyleVar(3);

        // Submit the dockspace
        if (dockspaceId == 0)
            dockspaceId = ImGui.GetID("DockSpace");
        ImGui.DockSpace(dockspaceId, new(0.0f, 0.0f), dockspaceFlags);

        ImGui.End();

        return dockspaceId;
    }
}
