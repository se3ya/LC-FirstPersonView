using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using FirstPersonView.Compat;

namespace FirstPersonView
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    [BepInDependency(Metadata.MoreCompanyGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Metadata.TooManyEmotesGUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new(Metadata.GUID);

        public static Plugin Instance { get; private set; } = null!;
        public static ManualLogSource Log { get; private set; } = null!;
        internal static Harmony Harmony { get; private set; } = null!;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Harmony = _harmony;

            Log.LogInfo($"Initializing {Metadata.PLUGIN_NAME}");

            ConfigManager.Initialize(Config);
            MoreCompanyCompat.Initialize();
            ModelReplacementCompat.Initialize();
            TooManyEmotesCompat.Initialize();
            _harmony.PatchAll();

            Log.LogInfo($"{Metadata.PLUGIN_NAME} is loaded!");
        }
    }
}