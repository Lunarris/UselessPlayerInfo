using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace UselessPlayerInfo.Windows;

internal static class Widgets
{
    public static readonly Vector4 ErrorColor = new(0.90f, 0.30f, 0.30f, 1f);
    public static readonly Vector4 WarningColor = new(0.95f, 0.75f, 0.30f, 1f);

    public static bool IconButton(FontAwesomeIcon icon, string tooltip)
    {
        bool clicked;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            clicked = ImGui.SmallButton(icon.ToIconString());
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip(tooltip);
        }

        return clicked;
    }

    public static float IconButtonsWidth(params FontAwesomeIcon[] icons)
    {
        var style = ImGui.GetStyle();
        var width = 0f;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            foreach (var icon in icons)
            {
                width += ImGui.CalcTextSize(icon.ToIconString()).X + style.FramePadding.X * 2;
            }
        }

        return width + style.ItemSpacing.X * (icons.Length - 1);
    }

    public static bool CtrlShiftIconButton(FontAwesomeIcon icon, string tooltip)
    {
        var io = ImGui.GetIO();
        using (ImRaii.Disabled(!(io.KeyCtrl && io.KeyShift)))
        {
            return IconButton(icon, $"{tooltip}\nHold Ctrl+Shift to click.");
        }
    }

    public static bool RemoveButton(bool confirm)
        => confirm ? IconButton(FontAwesomeIcon.Trash, "Remove") : CtrlShiftIconButton(FontAwesomeIcon.Trash, "Remove");

    public static void WrappedColored(Vector4 color, string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
        {
            ImGui.TextWrapped(text);
        }
    }
}

internal sealed class EditPopup
{
    private readonly string popupId;
    private string name = "";
    private string notes = "";
    private int editing = -1;
    private bool openRequested;

    public EditPopup(string popupId)
    {
        this.popupId = popupId;
    }

    public void Open(int index, string currentName, string currentNotes)
    {
        editing = index;
        name = currentName;
        notes = currentNotes;
        openRequested = true;
    }

    public (int Index, string Name, string Notes)? Draw()
    {
        (int, string, string)? result = null;

        if (openRequested)
        {
            ImGui.OpenPopup(popupId);
            openRequested = false;
        }

        using var popup = ImRaii.Popup(popupId);
        if (!popup.Success)
        {
            return null;
        }

        ImGui.SetNextItemWidth(260f);
        ImGui.InputTextWithHint("##EditName", "Name...", ref name, 64);
        ImGui.InputTextMultiline("##EditNotes", ref notes, 1000, new Vector2(260f, 80f));

        using (ImRaii.Disabled(string.IsNullOrWhiteSpace(name)))
        {
            if (ImGui.Button("Save"))
            {
                result = (editing, name.Trim(), notes.Trim());
                ImGui.CloseCurrentPopup();
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            ImGui.CloseCurrentPopup();
        }

        return result;
    }
}

internal sealed class ConfirmPopup
{
    private readonly string popupId;
    private string message = "";
    private int index = -1;
    private bool openRequested;

    public ConfirmPopup(string popupId)
    {
        this.popupId = popupId;
    }

    public void Open(int index, string message)
    {
        this.index = index;
        this.message = message;
        openRequested = true;
    }

    public int? Draw()
    {
        int? result = null;

        if (openRequested)
        {
            ImGui.OpenPopup(popupId);
            openRequested = false;
        }

        using var popup = ImRaii.Popup(popupId);
        if (!popup.Success)
        {
            return null;
        }

        ImGui.TextUnformatted(message);

        if (ImGui.Button("Remove"))
        {
            result = index;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            ImGui.CloseCurrentPopup();
        }

        return result;
    }
}
