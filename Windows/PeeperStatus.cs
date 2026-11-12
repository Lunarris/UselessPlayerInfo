using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using UselessPlayerInfo.Functions;

namespace UselessPlayerInfo.Windows;

public class PeeperStatus : ThemedWindow
{
    private const byte DetrimentalCategory = 2;
    private const long MoodlesRefreshMs = 500;
    private static readonly Vector2 IconSize = new(24, 32);

    private string name = "";
    private uint homeWorldId;
    private string homeWorld = "";
    private List<MoodlesIpc.Moodle> moodles = [];
    private long moodlesAt;

    public PeeperStatus()
        : base("Useless Status Effects###UselessPeeperStatus", ImGuiWindowFlags.NoFocusOnAppearing)
    {
        SizeCondition = ImGuiCond.FirstUseEver;
        Size = new Vector2(380, 420);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    internal void Show(Watcher watcher)
    {
        name = watcher.Name;
        homeWorldId = watcher.HomeWorldId;
        homeWorld = watcher.HomeWorld;
        WindowName = $"{name} - Status Effects###UselessPeeperStatus";
        moodles = [];
        moodlesAt = 0;
        IsOpen = true;
    }

    protected override void DrawContents()
    {
        ImGui.TextUnformatted($"{name} «{homeWorld}»");

        var player = TargetWatch.FindNearby(name, homeWorldId);
        if (player == null)
        {
            ImGui.TextDisabled("Out of range. Status effects show while the player is nearby.");
            return;
        }

        var now = Environment.TickCount64;
        if (now - moodlesAt >= MoodlesRefreshMs)
        {
            moodlesAt = now;
            moodles = MoodlesIpc.Read(player);
        }

        var effects = Read(player);
        foreach (var moodle in moodles)
        {
            effects.Add(new Effect(moodle.Title, moodle.Description, moodle.Icon, moodle.Stacks, moodle.Remaining, moodle.Negative, true, moodle.Applier));
        }

        if (effects.Count == 0)
        {
            ImGui.TextDisabled("No status effects.");
            return;
        }

        using var child = ImRaii.Child("##PeeperStatusList", new Vector2(-1, -1));
        if (!child.Success)
        {
            return;
        }

        DrawSection("Buffs", effects.Where(e => !e.Detrimental));
        DrawSection("Debuffs", effects.Where(e => e.Detrimental));
    }

    private static void DrawSection(string heading, IEnumerable<Effect> effects)
    {
        var list = effects.ToList();
        if (list.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"{heading} ({list.Count})");
        ImGui.Separator();

        foreach (var effect in list)
        {
            DrawEffect(effect);
        }
    }

    private static void DrawEffect(Effect effect)
    {
        var size = IconSize * ImGuiHelpers.GlobalScale;
        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(effect.Icon)).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, size);

        ImGui.SameLine();
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(effect.Stacks > 1 ? $"{effect.Name} x{effect.Stacks}" : effect.Name);
            if (effect.Remaining > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(FormatTime(effect.Remaining));
            }

            if (effect.IsMoodle)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("Moodle");
                if (effect.Applier.Length > 0 && ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Applied by {effect.Applier}");
                }
            }

            if (effect.Description.Length > 0)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]))
                {
                    ImGui.TextWrapped(effect.Description);
                }
            }
        }

        ImGui.Spacing();
    }

    private static string FormatTime(float seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Ceiling(seconds));
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }

        return time.TotalMinutes >= 1 ? $"{time.Minutes}m {time.Seconds}s" : $"{time.Seconds}s";
    }

    private static List<Effect> Read(IPlayerCharacter player)
    {
        var effects = new List<Effect>();
        foreach (var status in player.StatusList)
        {
            if (status.StatusId == 0 || !status.GameData.IsValid)
            {
                continue;
            }

            var row = status.GameData.Value;
            var statusName = row.Name.ExtractText();
            if (row.Icon == 0 || statusName.Length == 0)
            {
                continue;
            }

            var stacks = row.MaxStacks > 0 ? status.Param : 0;
            effects.Add(new Effect(
                statusName,
                row.Description.ExtractText().Trim(),
                stacks > 1 ? row.Icon + (uint)stacks - 1 : row.Icon,
                stacks,
                status.RemainingTime,
                row.StatusCategory == DetrimentalCategory,
                false,
                string.Empty));
        }

        return effects;
    }

    private readonly record struct Effect(string Name, string Description, uint Icon, int Stacks, float Remaining, bool Detrimental, bool IsMoodle, string Applier);
}
