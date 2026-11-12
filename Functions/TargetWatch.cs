using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo.Functions;

internal sealed class TargetWatch : IDisposable
{
    private const float TargetRange = 50f;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);

    private readonly Configuration config;
    private readonly List<Watcher> watchers = new();
    private readonly HashSet<(string Name, uint WorldId)> hidden = new();
    private DateTime nextCheck = DateTime.MinValue;

    public IReadOnlyList<Watcher> Watchers => watchers;
    public int HiddenCount => hidden.Count;
    public int TargetingNow => watchers.Count(w => w.IsTargeting);

    public event Action<Watcher>? StartedTargeting;

    public TargetWatch(Configuration config)
    {
        this.config = config;
        Plugin.Framework.Update += OnUpdate;
        Plugin.ClientState.Logout += OnLogout;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.ClientState.Logout -= OnLogout;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public void Clear() => watchers.Clear();

    public void Remove(Watcher watcher) => watchers.Remove(watcher);

    public void Hide(Watcher watcher)
    {
        hidden.Add((watcher.Name, watcher.HomeWorldId));
        watchers.Remove(watcher);
    }

    public void ClearHidden() => hidden.Clear();

    public static IPlayerCharacter? FindTargetable(Watcher watcher)
    {
        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            return null;
        }

        return Plugin.ObjectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.Name.TextValue == watcher.Name && p.HomeWorld.RowId == watcher.HomeWorldId && IsTargetable(p, localPlayer));
    }

    public static IPlayerCharacter? FindNearby(string name, uint homeWorldId)
        => Plugin.ObjectTable.PlayerObjects
            .OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.Name.TextValue == name && p.HomeWorld.RowId == homeWorldId);

    private static bool IsTargetable(IPlayerCharacter player, IPlayerCharacter localPlayer)
        => player.IsTargetable && Vector3.Distance(player.Position, localPlayer.Position) <= TargetRange;

    private void OnLogout(int type, int code)
    {
        watchers.Clear();
        hidden.Clear();
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        if (config.PeeperClearOnZoneChange)
        {
            watchers.Clear();
        }
    }

    private bool IsIgnored(Watcher watcher)
        => (config.PeeperIgnoreParty && watcher.IsPartyMember) || (config.PeeperIgnoreFriends && watcher.IsFriend);

    private static unsafe void ReadIds(IPlayerCharacter player, Watcher watcher)
    {
        var character = (Character*)player.Address;
        watcher.ContentId = character->ContentId;
        watcher.AccountId = character->AccountId;
    }

    private void OnUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < nextCheck)
        {
            return;
        }

        nextCheck = now + CheckInterval;

        var localPlayer = Plugin.ObjectTable.LocalPlayer;
        if (localPlayer == null)
        {
            return;
        }

        var targetable = new HashSet<Watcher>();
        var targeting = new HashSet<Watcher>();

        foreach (var player in Plugin.ObjectTable.PlayerObjects.OfType<IPlayerCharacter>())
        {
            if (player.GameObjectId == localPlayer.GameObjectId)
            {
                continue;
            }

            var key = (player.Name.TextValue, player.HomeWorld.RowId);
            var watcher = watchers.FirstOrDefault(w => w.Name == key.Item1 && w.HomeWorldId == key.Item2);
            var isTargeting = player.TargetObjectId == localPlayer.GameObjectId;

            if (watcher == null)
            {
                if (!isTargeting || hidden.Contains(key))
                {
                    continue;
                }

                watcher = new Watcher
                {
                    Name = key.Item1,
                    HomeWorldId = key.Item2,
                    HomeWorld = player.HomeWorld.Value.Name.ToString(),
                };
                watchers.Insert(0, watcher);
            }

            if (IsTargetable(player, localPlayer))
            {
                targetable.Add(watcher);
            }

            ReadIds(player, watcher);
            watcher.Level = player.Level;
            watcher.Job = player.ClassJob.Value.Abbreviation.ToString();
            watcher.IsPartyMember = player.StatusFlags.HasFlag(StatusFlags.PartyMember) || player.StatusFlags.HasFlag(StatusFlags.AllianceMember);
            watcher.IsFriend = player.StatusFlags.HasFlag(StatusFlags.Friend);

            if (isTargeting)
            {
                targeting.Add(watcher);
                if (!watcher.IsTargeting)
                {
                    watcher.LastTargeted = now;
                    MoveToTop(watcher);

                    if (!IsIgnored(watcher))
                    {
                        StartedTargeting?.Invoke(watcher);
                    }
                }
            }
        }

        foreach (var watcher in watchers)
        {
            watcher.IsTargetable = targetable.Contains(watcher);
            watcher.IsTargeting = targeting.Contains(watcher);
        }

        watchers.RemoveAll(IsIgnored);

        if (watchers.Count > config.PeeperMaxEntries)
        {
            watchers.RemoveRange(config.PeeperMaxEntries, watchers.Count - config.PeeperMaxEntries);
        }
    }

    private void MoveToTop(Watcher watcher)
    {
        watchers.Remove(watcher);
        watchers.Insert(0, watcher);
    }
}

internal sealed class Watcher
{
    public string Name { get; init; } = "";
    public uint HomeWorldId { get; init; }
    public string HomeWorld { get; init; } = "";
    public DateTime LastTargeted { get; set; }
    public ulong ContentId { get; set; }
    public ulong AccountId { get; set; }
    public byte Level { get; set; }
    public string Job { get; set; } = "";
    public bool IsTargeting { get; set; }
    public bool IsTargetable { get; set; }
    public bool IsPartyMember { get; set; }
    public bool IsFriend { get; set; }
}
