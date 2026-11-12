using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Ipc;

namespace UselessPlayerInfo.Functions;

internal static class MoodlesIpc
{
    public enum StatusType
    {
        Positive = 0,
        Negative = 1,
        Special = 2,
    }

    public enum ChainTrigger
    {
        Dispel = 0,
        HitMaxStacks = 1,
        TimerExpired = 2,
    }

    internal readonly record struct Moodle(string Title, string Description, uint Icon, int Stacks, float Remaining, bool Negative, string Applier);

    private const long MaxShownMs = 10L * 365 * 24 * 60 * 60 * 1000;

    private static readonly Regex TagRegex = new(@"\[/?(?:color|glow|i|b|u|s)(?:=[^\]]+)?\]", RegexOptions.Compiled);

    private static ICallGateSubscriber<IPlayerCharacter, List<(int Version, Guid GUID, int IconID, string Title, string Description, string CustomVFXPath, long ExpireTicks, StatusType Type, int Stacks, int StackSteps, uint Modifiers, Guid ChainedStatus, ChainTrigger ChainTrigger, string Applier, string Dispeller, bool Permanent)>>? getByPlayer;

    public static List<Moodle> Read(IPlayerCharacter player)
    {
        var moodles = new List<Moodle>();

        try
        {
            getByPlayer ??= Plugin.PluginInterface.GetIpcSubscriber<IPlayerCharacter, List<(int, Guid, int, string, string, string, long, StatusType, int, int, uint, Guid, ChainTrigger, string, string, bool)>>("Moodles.GetStatusManagerInfoByPlayerV2");
            var statuses = getByPlayer.InvokeFunc(player);
            if (statuses == null)
            {
                return moodles;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (var status in statuses)
            {
                var title = Clean(status.Title);
                if (title.Length == 0 && status.IconID == 0)
                {
                    continue;
                }

                var left = status.ExpireTicks - now;
                var remaining = !status.Permanent && left > 0 && left < MaxShownMs ? left / 1000f : 0f;
                var icon = status.Stacks > 1 ? (uint)(status.IconID + status.Stacks - 1) : (uint)status.IconID;

                moodles.Add(new Moodle(
                    title,
                    Clean(status.Description),
                    icon,
                    status.Stacks,
                    remaining,
                    status.Type == StatusType.Negative,
                    status.Applier ?? string.Empty));
            }
        }
        catch (Exception)
        {
            moodles.Clear();
        }

        return moodles;
    }

    private static string Clean(string? text)
        => text == null ? string.Empty : TagRegex.Replace(text, string.Empty).Trim();
}
