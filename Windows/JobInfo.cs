using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using UselessPlayerInfo.Functions;

namespace UselessPlayerInfo.Windows;

public class JobWindow : ThemedWindow
{
    private const float IconSize = 24f;

    private static readonly (JobRole Role, string Label, Vector4 Color)[] RoleTabs =
    {
        (JobRole.Tank, "TNK", new Vector4(0.35f, 0.60f, 0.95f, 1f)),
        (JobRole.Healer, "HLR", new Vector4(0.40f, 0.80f, 0.45f, 1f)),
        (JobRole.Melee, "mDPS", new Vector4(0.90f, 0.30f, 0.30f, 1f)),
        (JobRole.PhysicalRanged, "prDPS", new Vector4(0.85f, 0.65f, 0.20f, 1f)),
        (JobRole.MagicalRanged, "mrDPS", new Vector4(0.70f, 0.40f, 0.90f, 1f)),
        (JobRole.LimitedJob, "LMT", new Vector4(0.90f, 0.800f, 0.60f, 1f)),
        (JobRole.Hand, "DoH", new Vector4(0.75f, 0.55f, 0.35f, 1f)),
        (JobRole.Land, "DoL", new Vector4(0.55f, 0.75f, 0.55f, 1f)),
    };

    private readonly Plugin plugin;
    private bool selectCurrentRole;

    public JobWindow(Plugin plugin)
        : base("Useless Jobs Info", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public static void Dispose() { }

    public override void OnOpen() => selectCurrentRole = plugin.Configuration.OpenCurrentRoleTab;

    protected override void DrawContents()
    {
        using (var child = ImRaii.Child("JobInfoWindow", Vector2.Zero, true))
        {
            if (child.Success)
            {
                var localPlayer = Plugin.ObjectTable.LocalPlayer;
                if (localPlayer == null)
                {
                    ImGui.TextUnformatted("Local player is currently not loaded.");
                    return;
                }

                if (!localPlayer.ClassJob.IsValid)
                {
                    ImGui.TextUnformatted("Your current job is currently not valid.");
                    return;
                }

                DrawJobIcon(localPlayer.ClassJob.RowId, IconSize);
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted($"{localPlayer.ClassJob.Value.NameEnglish} ({localPlayer.ClassJob.Value.Abbreviation}) - Level {localPlayer.Level}");
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                DrawMaxLevelSummary();
                ImGui.Spacing();

                var currentRole = Jobs.All.Where(j => j.Id == localPlayer.ClassJob.RowId).Select(j => (JobRole?)j.Role).FirstOrDefault();

                if (ImGui.BeginTabBar("JobRoleTabs", ImGuiTabBarFlags.None))
                {
                    foreach (var (role, label, color) in RoleTabs)
                    {
                        var flags = selectCurrentRole && role == currentRole ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
                        if (ImGui.BeginTabItem(label, flags))
                        {
                            DrawRoleTab(role, color, plugin.Configuration.HideLockedJobs, plugin.Configuration.ShowExperienceBars);
                            ImGui.EndTabItem();
                        }
                    }

                    ImGui.EndTabBar();
                }

                selectCurrentRole = false;
            }
        }
    }

    private static readonly Vector4 MaxLevelColor = new(1.00f, 0.82f, 0.30f, 1f);

    private static unsafe int GetLevelCap()
        => 50 + 10 * PlayerState.Instance()->MaxExpansion;

    private static void DrawMaxLevelSummary()
    {
        var cap = GetLevelCap();
        var total = 0;
        var capped = 0;

        foreach (var (_, id, role) in Jobs.All)
        {
            if (role == JobRole.LimitedJob)
            {
                continue;
            }

            total++;
            if (GetJobLevel(id) >= cap)
            {
                capped++;
            }
        }

        ImGui.TextColored(MaxLevelColor, $"{capped} / {total} jobs at max level ({cap})");
    }

    private static void DrawRoleTab(JobRole role, Vector4 color, bool hideLocked, bool showBars)
    {
        ImGui.Spacing();

        var cap = GetLevelCap();

        if (ImGui.BeginTable($"JobTable_{role}", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("##Icon", ImGuiTableColumnFlags.WidthFixed, IconSize, 0);
            ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 50f, 0);
            ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthFixed, 40f, 0);
            ImGui.TableSetupColumn("Experience", ImGuiTableColumnFlags.WidthStretch, 0);

            foreach (var (abbrev, id, jobRole) in Jobs.All)
            {
                if (jobRole != role)
                {
                    continue;
                }

                var level = GetJobLevel(id);
                if (hideLocked && level == 0)
                {
                    continue;
                }

                var isCapped = role != JobRole.LimitedJob && level >= cap;

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                DrawJobIcon(id, IconSize);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(color, abbrev);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (isCapped)
                {
                    ImGui.TextColored(MaxLevelColor, level.ToString());
                }
                else
                {
                    ImGui.TextUnformatted(level > 0 ? level.ToString() : "-");
                }

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (isCapped)
                {
                    ImGui.TextColored(MaxLevelColor, "Max level");
                }
                else if (level > 0)
                {
                    DrawExperience(id, level, showBars);
                }
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
    }

    private static void DrawExperience(uint jobId, int level, bool showBar)
    {
        if (!Plugin.DataManager.GetExcelSheet<ParamGrow>().TryGetRow((uint)level, out var grow) || grow.ExpToNext <= 0
            || !Plugin.DataManager.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var classJob))
        {
            return;
        }

        var experience = Plugin.PlayerState.GetClassJobExperience(classJob);
        var fraction = Math.Clamp((float)experience / grow.ExpToNext, 0f, 1f);
        var text = $"{experience:N0} / {grow.ExpToNext:N0}";
        if (showBar)
        {
            ImGui.ProgressBar(fraction, new Vector2(-1, 0), text);
        }
        else
        {
            ImGui.TextUnformatted(text);
        }
    }

    private static void DrawJobIcon(uint jobId, float size)
    {
        var icon = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(Jobs.GetIconId(jobId))).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, new Vector2(size, size));
    }

    private static int GetJobLevel(uint jobId)
    {
        if (!Plugin.DataManager.GetExcelSheet<ClassJob>().TryGetRow(jobId, out var classJob))
        {
            return 0;
        }

        return Plugin.PlayerState.GetClassJobLevel(classJob);
    }
}
