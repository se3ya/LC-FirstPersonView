using System.Collections.Generic;
using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;

namespace FirstPersonView;

internal static class LethalConfigCompat
{
    private static bool? _present;

    public static bool Present => _present ??= Chainloader.PluginInfos.ContainsKey(ModGUIDs.LethalConfig);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void Register(IReadOnlyList<ConfigEntryBase> entries)
    {
        foreach (ConfigEntryBase entry in entries)
        {
            switch (entry)
            {
                case ConfigEntry<float> f:
                    LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(f, requiresRestart: false));
                    break;
                case ConfigEntry<bool> b:
                    LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(b, requiresRestart: false));
                    break;
                case ConfigEntry<string> s:
                    LethalConfigManager.AddConfigItem(new TextInputFieldConfigItem(s, requiresRestart: false));
                    break;
                case ConfigEntry<HandsMode> h:
                    LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<HandsMode>(h, requiresRestart: false));
                    break;
                case ConfigEntry<HeldItemFadeMode> m:
                    LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<HeldItemFadeMode>(m, requiresRestart: false));
                    break;
                case ConfigEntry<ItemDitherMode> d:
                    LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<ItemDitherMode>(d, requiresRestart: false));
                    break;
            }
        }
    }
}
