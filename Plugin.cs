using System.Linq;
using System.Net.Http;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using UselessPlayerInfo.Functions;
using UselessPlayerInfo.Windows;
using UselessPlayerInfo.Objects;

namespace UselessPlayerInfo;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    private const string MainWindowCMD = "/uselessinfo";
    private const string SettingsWindowCMD = "/ulsettings";
    private const string JobWindowCMD = "/jobinfo";
    private const string HousingListCMD = "/housinglist";
    private const string SpotListCMD = "/savedspots";
    private const string SaveHouseCMD = "/savehouse";
    private const string SaveSpotCMD = "/savespot";
    private const string PeeperListCMD = "/peep";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("UselessInfo");
    private JobWindow JobWindow { get; init; }

    private HousingListWindow HousingListWindow { get; init; }

    private SpotListWindow SpotListWindow { get; init; }

    private PeeperList PeeperListWindow { get; init; }

    private PeeperStatus PeeperStatusWindow { get; init; }

    internal TargetWatch TargetWatch { get; init; }

    private MainWindow MainWindow { get; init; }

    private SettingsWindow SettingsWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        MoveHousesToWards();

        JobWindow = new JobWindow(this);
        HousingListWindow = new HousingListWindow(this);
        SpotListWindow = new SpotListWindow(this);
        TargetWatch = new TargetWatch(Configuration);
        PeeperListWindow = new PeeperList(this);
        PeeperStatusWindow = new PeeperStatus();
        MainWindow = new MainWindow(this);
        SettingsWindow = new SettingsWindow(this);

        WindowSystem.AddWindow(JobWindow);
        WindowSystem.AddWindow(HousingListWindow);
        WindowSystem.AddWindow(SpotListWindow);
        WindowSystem.AddWindow(PeeperListWindow);
        WindowSystem.AddWindow(PeeperStatusWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(SettingsWindow);

        ApplySettings();

        CommandManager.AddHandler(MainWindowCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Very Simple Main Window for this plugin."
        });

        CommandManager.AddHandler(SettingsWindowCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the settings for this plugin."
        });

        CommandManager.AddHandler(JobWindowCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Locations Window for displaying all the player's Jobs and their corresponding levels."
        });

        CommandManager.AddHandler(HousingListCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Saved Houses Window for adding and removing saved Houses and Apartments/Chambers."
        });

        CommandManager.AddHandler(SpotListCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Saved Spots Window for saving and sharing open-world map locations."
        });

        CommandManager.AddHandler(PeeperListCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Peeper List Window, listing players who have targeted you this session."
        });

        CommandManager.AddHandler(SaveHouseCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Saves the house, apartment or room you're at. Usage: /savehouse [name]"
        });

        CommandManager.AddHandler(SaveSpotCMD, new CommandInfo(OnCommand)
        {
            HelpMessage = "Saves the spot you're standing on. Usage: /savespot [name]"
        });

        PluginInterface.UiBuilder.Draw += DrawUi;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainWindowUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleSettingsWindowUi;

        Log.Information($"UselessPlayerInfo has been loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainWindowUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleSettingsWindowUi;

        WindowSystem.RemoveAllWindows();

        JobWindow.Dispose();
        PeeperListWindow.Dispose();
        TargetWatch.Dispose();

        CommandManager.RemoveHandler(MainWindowCMD);
        CommandManager.RemoveHandler(SettingsWindowCMD);
        CommandManager.RemoveHandler(JobWindowCMD);
        CommandManager.RemoveHandler(HousingListCMD);
        CommandManager.RemoveHandler(SpotListCMD);
        CommandManager.RemoveHandler(PeeperListCMD);
        CommandManager.RemoveHandler(SaveHouseCMD);
        CommandManager.RemoveHandler(SaveSpotCMD);
    }

    private void DrawUi()
    {
        if (Configuration.HideInCombat && Condition[ConditionFlag.InCombat])
        {
            return;
        }

        Theme.Update(SettingsWindow.Preview ?? Configuration);
        WindowSystem.Draw();
    }

    public void ApplySettings()
    {
        const ImGuiWindowFlags lockFlags = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

        foreach (var window in WindowSystem.Windows)
        {
            window.RespectCloseHotkey = Configuration.CloseWithEscape;

            if (window == SettingsWindow)
            {
                continue;
            }

            window.Flags = Configuration.LockWindows ? window.Flags | lockFlags : window.Flags & ~lockFlags;
        }

        PluginInterface.UiBuilder.DisableCutsceneUiHide = !Configuration.HideInCutscenes;
        PluginInterface.UiBuilder.DisableGposeUiHide = !Configuration.HideInGpose;
    }

    private void OnCommand(string command, string args)
    {
        switch (command)
        {
            case MainWindowCMD:
                MainWindow.Toggle();
                break;
            case SettingsWindowCMD:
                SettingsWindow.Toggle();
                break;
            case JobWindowCMD:
                JobWindow.Toggle();
                break;
            case HousingListCMD:
                HousingListWindow.Toggle();
                break;
            case SpotListCMD:
                SpotListWindow.Toggle();
                break;
            case PeeperListCMD:
                PeeperListWindow.Toggle();
                break;
            case SaveHouseCMD:
                PrintSaveResult(HousingListWindow.TrySaveCurrent(args, false, out var houseMessage), houseMessage);
                break;
            case SaveSpotCMD:
                PrintSaveResult(SpotListWindow.TrySaveCurrent(args, false, out var spotMessage), spotMessage);
                break;
            default:
                break;
        }
    }

    private static void PrintSaveResult(bool saved, string message)
    {
        if (saved)
        {
            ChatGui.Print(message);
        }
        else
        {
            ChatGui.PrintError(message);
        }
    }

    private void MoveHousesToWards()
    {
        var changed = false;
        foreach (var location in Configuration.HousingLocations)
        {
            var wardId = Housing.ToWardTerritory(location.TerritoryId);
            if (wardId != location.TerritoryId)
            {
                location.TerritoryId = wardId;
                changed = true;
            }
        }

        if (changed)
        {
            Configuration.Save();
        }
    }

    public void ToggleMainWindowUi() => MainWindow.Toggle();
    public void ToggleSettingsWindowUi() => SettingsWindow.Toggle();
    public void ToggleJobWindowUi() => JobWindow.Toggle();
    public void ToggleHousingListWindowUI() => HousingListWindow.Toggle();
    public void ToggleSpotListWindowUi() => SpotListWindow.Toggle();
    public void TogglePeeperListWindowUi() => PeeperListWindow.Toggle();
    internal void ShowPeeperStatus(Watcher watcher) => PeeperStatusWindow.Show(watcher);
}
