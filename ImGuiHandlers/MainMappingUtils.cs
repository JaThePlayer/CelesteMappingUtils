using Celeste.Mod.MappingUtils.ImGuiHandlers.Tabs;
using Celeste.Mod.MappingUtils.ModIntegration;
using System.Collections.Generic;

namespace Celeste.Mod.MappingUtils.ImGuiHandlers;

public class MainMappingUtils : ImGuiHandler
{
    public static List<Tab> Tabs { get; private set; } =
    [
        new MetadataTab(),
        new FlagTab(),
        new CountersTab(),
        new CheatTab(),
        new EntityTab(),
        new StylegroundViewTab(),
        new ProfilingTab(),
        // new ParticleTab(), - exporting unimplemented, overrides Dust particle.
        // new LogTab() - not quite there yet, doesn't live reload :/
    ];

    public static bool Enabled { get; set; }

    public const int ItemWidth = 150;

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (MappingUtilsModule.Settings?.OpenMenu?.Pressed ?? false)
        {
            Enabled = !Enabled;
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

    public override void Render()
    {
        base.Render();

        FrostHelperAPI.LoadIfNeeded();

        if (Engine.Scene is not Level level || !Enabled)
        {
            return;
        }

        var open = true;
        ImGui.SetNextWindowSize(new(ItemWidth * 2.5f, -1f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Mapping Utils", ref open);
        if (ImGui.BeginTabBar("a", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.FittingPolicyScroll))
        {
            foreach (var tab in Tabs)
            {
                var tabOpen = ImGui.BeginTabItem(tab.Name);
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
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }
}
