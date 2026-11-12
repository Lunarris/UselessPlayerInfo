using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using UselessPlayerInfo.Objects;
using UselessPlayerInfo.Windows.SettingsTabs;

namespace UselessPlayerInfo.Windows;

public sealed class SettingsWindow : ThemedWindow
{
    private readonly Plugin plugin;
    private readonly Configuration mutable = new();
    private readonly List<ISettingsTab> tabs;
    private int currentTab;

    public SettingsWindow(Plugin plugin)
        : base("Useless Settings###UselessSettings", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;

        tabs =
        [
            new GeneralTab(mutable),
            new MainTab(mutable),
            new JobsTab(mutable),
            new HousingTab(mutable),
            new SpotsTab(mutable),
            new PeepersTab(mutable),
            new ThemeTab(mutable),
        ];
    }

    private bool previewing;

    public Configuration? Preview => previewing ? mutable : null;

    public override void OnOpen()
    {
        mutable.CopySettingsFrom(plugin.Configuration);
        previewing = true;
    }

    public override void OnClose() => previewing = false;

    protected override void DrawContents()
    {
        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

        using (var table = ImRaii.Table("##UselessSettingsTable", 2))
        {
            if (table.Success)
            {
                ImGui.TableSetupColumn("tab", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("settings", ImGuiTableColumnFlags.WidthStretch);

                ImGui.TableNextColumn();

                var changed = false;
                for (var i = 0; i < tabs.Count; i++)
                {
                    if (ImGui.Selectable(tabs[i].Name, currentTab == i))
                    {
                        currentTab = i;
                        changed = true;
                    }
                }

                ImGui.TableNextColumn();

                var height = ImGui.GetContentRegionAvail().Y - footerHeight;
                using var child = ImRaii.Child("##UselessSettingsPage", new Vector2(-1, height));
                if (child.Success)
                {
                    tabs[currentTab].Draw(changed);
                }
            }
        }

        ImGui.Separator();

        var save = ImGui.Button("Save");

        ImGui.SameLine();
        if (ImGui.Button("Save and Close"))
        {
            save = true;
            IsOpen = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Discard"))
        {
            IsOpen = false;
        }

        if (save)
        {
            plugin.Configuration.CopySettingsFrom(mutable);
            plugin.Configuration.Save();
            plugin.ApplySettings();
        }
    }
}
