using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using FirstPersonView.Compat;

namespace FirstPersonView;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(ModGUIDs.MoreCompany, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.TooManyEmotes, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ModGUIDs.LethalPhones, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
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

        ConfigManager.Initialize(Config);
        MoreCompanyCompat.Initialize();
        TooManyEmotesCompat.Initialize();
        LethalPhonesCompat.Initialize();
        _harmony.PatchAll();

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} is loaded!");
    }
}