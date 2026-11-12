using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using UselessPlayerInfo.Functions;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Windows;

public class PeeperList : ThemedWindow
{
    private static readonly Vector4 TargetingColor = new(0.40f, 0.90f, 0.40f, 1f);
    private static readonly Vector4 TargetableColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 GoneColor = new(0.50f, 0.50f, 0.50f, 1f);

    private readonly Plugin plugin;

    public PeeperList(Plugin plugin)
        : base("Useless Peeper###UselessPeeper", ImGuiWindowFlags.NoFocusOnAppearing)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
        plugin.TargetWatch.StartedTargeting += OnStartedTargeting;
    }

    public void Dispose() => plugin.TargetWatch.StartedTargeting -= OnStartedTargeting;

    private TargetWatch Watch => plugin.TargetWatch;

    private void OnStartedTargeting(Watcher watcher)
    {
        var config = plugin.Configuration;

        if (config.PeeperAlertSound)
        {
            PlaySound(config.PeeperSoundEffect);
        }

        if (config.PeeperAlertChat)
        {
            Plugin.ChatGui.Print($"{watcher.Name} «{watcher.HomeWorld}» is targeting you.");
        }

        if (config.PeeperAutoOpen)
        {
            IsOpen = true;
        }
    }

    public static unsafe void PlaySound(int chatSound)
        => UIGlobals.PlaySoundEffect((uint)(36 + Math.Clamp(chatSound, 1, 16)), null, null, 0);

    protected override void DrawContents()
    {
        DrawTable();
    }

    private void DrawTable()
    {
        var watchers = Watch.Watchers;
        if (watchers.Count == 0)
        {
            ImGui.TextDisabled("Players who target you will show up here.");
            return;
        }

        if (!ImGui.BeginTable("PeeperTable", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp))
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed);

        for (var i = 0; i < watchers.Count; i++)
        {
            var watcher = watchers[i];
            using var id = ImRaii.PushId(i);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var color = watcher.IsTargeting ? TargetingColor : watcher.IsTargetable ? TargetableColor : GoneColor;
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                if (ImGui.Selectable($"{watcher.Name}", false, ImGuiSelectableFlags.SpanAllColumns))
                {
                    TargetPlayer(watcher, false);
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Left-click to target, right-click for more options.");
            }

            DrawContextMenu(watcher);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(watcher.LastTargeted.ToLocalTime().ToString(plugin.Configuration.PeeperUse24Hour ? "HH:mm" : "h:mm tt"));
        }

        ImGui.EndTable();
    }

    private static string Tooltip(Watcher watcher)
    {
        var tags = (watcher.IsPartyMember ? " (Party)" : "") + (watcher.IsFriend ? " (Friend)" : "");
        return $"{watcher.Name} «{watcher.HomeWorld}»{tags}";
    }

    private void DrawContextMenu(Watcher watcher)
    {
        using var popup = ImRaii.ContextPopupItem("TargetWatchMenu");
        if (!popup.Success)
        {
            return;
        }

        var player = TargetWatch.FindTargetable(watcher);
        ImGui.TextDisabled(player == null ? $"{watcher.Name} «{watcher.HomeWorld}» (out of range)" : $"{watcher.Name} «{watcher.HomeWorld}»");
        ImGui.Separator();
        using (ImRaii.Disabled(player == null))
        {
            if (ImGui.MenuItem("Target"))
            {
                TargetPlayer(watcher, false);
            }

            if (ImGui.MenuItem("Set Focus Target"))
            {
                TargetPlayer(watcher, true);
            }

            if (ImGui.MenuItem("Examine") && player != null)
            {
                Examine(player);
            }

            if (ImGui.MenuItem("View Adventurer Plate") && player != null)
            {
                OpenPlate(player);
            }
        }

        if (ImGui.MenuItem("View Status Effects"))
        {
            plugin.ShowPeeperStatus(watcher);
        }

        ImGui.Separator();

        if (ImGui.MenuItem("Send Tell"))
        {
            SendTell(watcher);
        }

        if (ImGui.MenuItem("Search on Lodestone"))
        {
            Util.OpenLink($"https://na.finalfantasyxiv.com/lodestone/character/?q={Uri.EscapeDataString(watcher.Name)}&worldname={Uri.EscapeDataString(watcher.HomeWorld)}");
        }
    }

    private static void TargetPlayer(Watcher watcher, bool focus)
    {
        if (TargetWatch.FindTargetable(watcher) is not { } player)
        {
            return;
        }

        if (focus)
        {
            Plugin.TargetManager.FocusTarget = player;
        }
        else
        {
            Plugin.TargetManager.Target = player;
        }
    }

    private static unsafe void Examine(IPlayerCharacter player)
        => AgentInspect.Instance()->ExamineCharacter(player.EntityId);

    private static unsafe void OpenPlate(IPlayerCharacter player)
        => AgentCharaCard.Instance()->OpenCharaCard((GameObject*)player.Address);

    private static unsafe void SendTell(Watcher watcher)
    {
        var name = Utf8String.FromString(watcher.Name);
        var world = Utf8String.FromString(watcher.HomeWorld);
        RaptureShellModule.Instance()->SetContextTellTarget(name, world, (ushort)watcher.HomeWorldId, watcher.AccountId, watcher.ContentId, 0, true);
        name->Dtor(true);
        world->Dtor(true);
    }
}
