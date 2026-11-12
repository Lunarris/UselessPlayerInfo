using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows.SettingsTabs;

internal static class SettingsWidgets
{
    public static void Checkbox(string label, string? help, bool value, Action<bool> set)
    {
        if (ImGui.Checkbox(label, ref value))
        {
            set(value);
        }

        Help(help);
    }

    public static void Slider(string label, string? help, int value, int min, int max, Action<int> set)
    {
        ImGui.SetNextItemWidth(150f);
        if (ImGui.SliderInt(label, ref value, min, max))
        {
            set(value);
        }

        Help(help);
    }

    public static void SortCombo(string label, string locationHelp, ListSort value, Action<ListSort> set)
    {
        ImGui.SetNextItemWidth(150f);
        using (var combo = ImRaii.Combo(label, SortName(value)))
        {
            if (combo.Success)
            {
                foreach (var option in Enum.GetValues<ListSort>())
                {
                    if (ImGui.Selectable(SortName(option), option == value))
                    {
                        set(option);
                    }
                }
            }
        }

        Help($"Location sorts by {locationHelp}");
    }

    public static void Heading(string text)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(text);
    }

    private static void Help(string? help)
    {
        if (help != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(help);
        }
    }

    private static string SortName(ListSort sort) => sort switch
    {
        ListSort.Name => "Name",
        ListSort.Location => "Location",
        _ => "As added",
    };
}
