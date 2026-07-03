using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using UnityEngine;

using MoreCompany.Cosmetics;

namespace FirstPersonView.Compat;

internal static class MoreCompanyCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isInstalled = Chainloader.PluginInfos.ContainsKey(ModGUIDs.MoreCompany);

        if (_isInstalled)
            Plugin.Log.LogInfo("MoreCompany detected.");
    }

    public static void ApplyLocalCosmeticVisibility(
        PlayerControllerB player, bool localBodyVisible, bool firstPersonArmsActive)
    {
        if (!_isInstalled || !ConfigManager.EnableMoreCompanyCompatibility.Value || !localBodyVisible)
            return;

        ApplyLocalCosmeticVisibilityTyped(player, firstPersonArmsActive);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void ApplyLocalCosmeticVisibilityTyped(PlayerControllerB player, bool firstPersonArmsActive)
    {
        Transform? metarig = player.transform.Find(Constants.ScavengerModelName)?.Find(Constants.MetarigName);
        if (metarig == null)
            return;

        CosmeticApplication? cosmeticApplication = metarig.GetComponent<CosmeticApplication>();
        if (cosmeticApplication == null)
            return;

        foreach (CosmeticInstance cosmetic in cosmeticApplication.spawnedCosmetics)
        {
            if (cosmetic == null)
                continue;

            bool visible = ShouldShowCosmetic(cosmetic.cosmeticType, firstPersonArmsActive);
            if (cosmetic.gameObject.activeSelf != visible)
                cosmetic.gameObject.SetActive(visible);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool ShouldShowCosmetic(CosmeticType type, bool firstPersonArmsActive)
    {
        if (firstPersonArmsActive && type == CosmeticType.R_LOWER_ARM)
            return false;

        return type switch
        {
            CosmeticType.HAT => ConfigManager.ShowMoreCompanyHat.Value,
            CosmeticType.CHEST => ConfigManager.ShowMoreCompanyChest.Value,
            CosmeticType.HIP => ConfigManager.ShowMoreCompanyHip.Value,
            CosmeticType.R_LOWER_ARM => ConfigManager.ShowMoreCompanyRightLowerArm.Value,
            CosmeticType.L_SHIN => ConfigManager.ShowMoreCompanyLeftShin.Value,
            CosmeticType.R_SHIN => ConfigManager.ShowMoreCompanyRightShin.Value,
            _ => true,
        };
    }
}
