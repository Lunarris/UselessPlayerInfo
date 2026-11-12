using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

public sealed class ThemeTab : ISettingsTab
{
    private readonly Configuration mutable;

    public ThemeTab(Configuration mutable)
    {
        this.mutable = mutable;
    }

    public string Name => "Theme###tab-theme";

    public void Draw(bool changed)
    {
        SettingsWidgets.Checkbox("Use dark theme", "Off uses the standard Dalamud style.", mutable.UseTheme, v => mutable.UseTheme = v);

        using var disabled = ImRaii.Disabled(!mutable.UseTheme);

        ImGui.SetNextItemWidth(150f);
        using (var combo = ImRaii.Combo("Color scheme", Theme.SchemeName(mutable.ThemeScheme)))
        {
            if (combo.Success)
            {
                foreach (var scheme in Enum.GetValues<ThemeScheme>())
                {
                    if (ImGui.Selectable(Theme.SchemeName(scheme), scheme == mutable.ThemeScheme))
                    {
                        mutable.ThemeScheme = scheme;
                    }
                }
            }
        }
    }
}
