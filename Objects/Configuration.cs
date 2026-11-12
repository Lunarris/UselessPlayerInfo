using Dalamud.Configuration;
using System.Collections.Generic;
using System;

namespace UselessPlayerInfo.Objects;

public enum ListSort
{
    AsAdded,
    Name,
    Location,
}

public enum ThemeScheme
{
    Midnight,
    Slate,
    Ember,
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<HousingLocation> HousingLocations { get; set; } = new();

    public List<WorldArea> WorldAreas { get; set; } = new();

    public bool LockWindows { get; set; } = false;
    public bool CloseWithEscape { get; set; } = true;
    public bool HideInCutscenes { get; set; } = true;
    public bool HideInGpose { get; set; } = true;
    public bool HideInCombat { get; set; } = false;
    public bool UseTheme { get; set; } = false;
    public ThemeScheme ThemeScheme { get; set; } = ThemeScheme.Midnight;

    public bool ShowLocation { get; set; } = true;
    public bool ShowWeather { get; set; } = true;
    public bool ShowForecast { get; set; } = true;
    public int ForecastCount { get; set; } = 2;
    public bool ShowNavigation { get; set; } = false;

    public bool HideLockedJobs { get; set; } = false;
    public bool ShowExperienceBars { get; set; } = true;
    public bool OpenCurrentRoleTab { get; set; } = false;

    public ListSort HousingSort { get; set; } = ListSort.AsAdded;
    public bool ConfirmHousingRemove { get; set; } = false;
    public bool ShowHousingNotes { get; set; } = false;

    public ListSort SpotSort { get; set; } = ListSort.AsAdded;
    public bool ConfirmSpotRemove { get; set; } = false;
    public int SpotDecimals { get; set; } = 1;

    public bool PeeperUse24Hour { get; set; } = false;
    public bool PeeperIgnoreParty { get; set; } = false;
    public bool PeeperIgnoreFriends { get; set; } = false;
    public bool PeeperAlertSound { get; set; } = false;
    public int PeeperSoundEffect { get; set; } = 1;
    public bool PeeperAlertChat { get; set; } = false;
    public int PeeperMaxEntries { get; set; } = 100;
    public bool PeeperClearOnZoneChange { get; set; } = false;
    public bool PeeperAutoOpen { get; set; } = false;

    public void CopySettingsFrom(Configuration other)
    {
        LockWindows = other.LockWindows;
        CloseWithEscape = other.CloseWithEscape;
        HideInCutscenes = other.HideInCutscenes;
        HideInGpose = other.HideInGpose;
        HideInCombat = other.HideInCombat;
        UseTheme = other.UseTheme;
        ThemeScheme = other.ThemeScheme;

        ShowWeather = other.ShowWeather;
        ShowForecast = other.ShowForecast;
        ForecastCount = other.ForecastCount;
        ShowNavigation = other.ShowNavigation;
        ShowLocation = other.ShowLocation;

        HideLockedJobs = other.HideLockedJobs;
        ShowExperienceBars = other.ShowExperienceBars;
        OpenCurrentRoleTab = other.OpenCurrentRoleTab;

        HousingSort = other.HousingSort;
        ConfirmHousingRemove = other.ConfirmHousingRemove;
        ShowHousingNotes = other.ShowHousingNotes;

        SpotSort = other.SpotSort;
        ConfirmSpotRemove = other.ConfirmSpotRemove;
        SpotDecimals = other.SpotDecimals;

        PeeperUse24Hour = other.PeeperUse24Hour;
        PeeperIgnoreParty = other.PeeperIgnoreParty;
        PeeperIgnoreFriends = other.PeeperIgnoreFriends;
        PeeperAlertSound = other.PeeperAlertSound;
        PeeperSoundEffect = other.PeeperSoundEffect;
        PeeperAlertChat = other.PeeperAlertChat;
        PeeperMaxEntries = other.PeeperMaxEntries;
        PeeperClearOnZoneChange = other.PeeperClearOnZoneChange;
        PeeperAutoOpen = other.PeeperAutoOpen;
    }

    public void Save()
    {
        try
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to save plugin configuration.");
        }
    }
}
