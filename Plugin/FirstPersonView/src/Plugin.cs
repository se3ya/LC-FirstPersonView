using System.IO;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

using FirstPersonView.Compat;

namespace FirstPersonView;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(ModGUIDs.MoreCompany, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.TooManyEmotes, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.LethalPhones, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.LethalConfig, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    private const string GlobalConfigFolder = "FirstPersonView";

    private readonly Harmony _harmony = new(MyPluginInfo.PLUGIN_GUID);

    public static Plugin Instance { get; private set; } = null!;
    public static ManualLogSource Log { get; private set; } = null!;
    internal static Harmony Harmony { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        Harmony = _harmony;

        Log.LogInfo($"Initializing {MyPluginInfo.PLUGIN_NAME}");

        ConfigFile config = ResolveConfigFile();
        ConfigManager.Initialize(config, registerWithLethalConfig: config != Config);
        MoreCompanyCompat.Initialize();
        TooManyEmotesCompat.Initialize();
        LethalPhonesCompat.Initialize();
        _harmony.PatchAll();

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");
    }

    private ConfigFile ResolveConfigFile()
    {
        BepInPlugin metadata = MetadataHelper.GetMetadata(this);

        ConfigFile bootstrap = new(
            Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID + ".bootstrap.cfg"), true, metadata);
        bool useGlobalConfig = bootstrap.Bind("Main", "Use Global Config", false,
            "Share settings across all profiles via one global file instead of this profiles BepInEx/config. Requires a restart.").Value;

        if (!useGlobalConfig)
            return Config;

        string localPath = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID + ".cfg");

        string globalDirectory = Path.Combine(Application.persistentDataPath, GlobalConfigFolder);
        string globalPath = Path.Combine(globalDirectory, "global.cfg");
        MigrateLocalConfig(localPath, globalDirectory, globalPath);
        return new ConfigFile(globalPath, false, metadata);
    }

    private static void MigrateLocalConfig(string localPath, string globalDirectory, string globalPath)
    {
        if (File.Exists(globalPath) || !File.Exists(localPath))
            return;

        Directory.CreateDirectory(globalDirectory);
        File.Copy(localPath, globalPath);
        Log.LogInfo("Migrated existing profile config into the global config file.");
    }
}