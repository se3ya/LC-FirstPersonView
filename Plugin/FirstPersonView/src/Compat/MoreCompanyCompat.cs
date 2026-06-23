using System.Collections;
using System.Reflection;
using System;

using BepInEx.Bootstrap;
using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView.Compat;

internal static class MoreCompanyCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    private static Type? _cosmeticApplicationType;
    private static Type? _cosmeticInstanceType;
    private static FieldInfo? _spawnedCosmeticsField;
    private static FieldInfo? _cosmeticTypeField;

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

        if (!TryResolveSpawnedCosmetics(player, out IEnumerable cosmetics))
            return;

        foreach (object cosmetic in cosmetics)
        {
            if (cosmetic is not Component cosmeticComponent)
                continue;

            bool visible = ShouldShowCosmetic(cosmetic, firstPersonArmsActive);
            if (cosmeticComponent.gameObject.activeSelf != visible)
                cosmeticComponent.gameObject.SetActive(visible);
        }
    }

    private static bool TryResolveSpawnedCosmetics(PlayerControllerB player, out IEnumerable cosmetics)
    {
        cosmetics = Array.Empty<object>();

        if (!TryResolveTypes())
            return false;

        if (_cosmeticApplicationType == null || _spawnedCosmeticsField == null)
            return false;

        Transform? metarig = player.transform.Find(Constants.ScavengerModelName)?.Find(Constants.MetarigName);
        if (metarig == null)
            return false;

        Component? cosmeticApplication = metarig.GetComponent(_cosmeticApplicationType);
        if (cosmeticApplication == null)
            return false;

        object? cosmeticsObject = _spawnedCosmeticsField.GetValue(cosmeticApplication);
        if (cosmeticsObject is not IEnumerable resolved)
            return false;

        cosmetics = resolved;
        return true;
    }

    private static bool ShouldShowCosmetic(object cosmetic, bool firstPersonArmsActive)
    {
        if (!ConfigManager.ShowMoreCompanyCosmetics.Value)
            return false;

        if (_cosmeticTypeField == null)
            return true;

        object? cosmeticType = _cosmeticTypeField.GetValue(cosmetic);
        string typeName = cosmeticType?.ToString() ?? string.Empty;

        if (firstPersonArmsActive && typeName.IndexOf("ARM", StringComparison.OrdinalIgnoreCase) >= 0)
            return false;

        return typeName switch
        {
            "HAT" => ConfigManager.ShowMoreCompanyHat.Value,
            "CHEST" => ConfigManager.ShowMoreCompanyChest.Value,
            "HIP" => ConfigManager.ShowMoreCompanyHip.Value,
            "R_LOWER_ARM" => ConfigManager.ShowMoreCompanyRightLowerArm.Value,
            "L_SHIN" => ConfigManager.ShowMoreCompanyLeftShin.Value,
            "R_SHIN" => ConfigManager.ShowMoreCompanyRightShin.Value,
            _ => true,
        };
    }

    private static bool TryResolveTypes()
    {
        if (_cosmeticApplicationType != null && _spawnedCosmeticsField != null && _cosmeticInstanceType != null)
            return true;

        Assembly? moreCompanyAssembly = Reflection.FindAssembly("MoreCompany");
        if (moreCompanyAssembly == null)
            return false;

        _cosmeticApplicationType = moreCompanyAssembly.GetType("MoreCompany.Cosmetics.CosmeticApplication");
        _cosmeticInstanceType = moreCompanyAssembly.GetType("MoreCompany.Cosmetics.CosmeticInstance");

        if (_cosmeticApplicationType == null || _cosmeticInstanceType == null)
            return false;

        _spawnedCosmeticsField = _cosmeticApplicationType.GetField("spawnedCosmetics", BindingFlags.Public | BindingFlags.Instance);
        _cosmeticTypeField = _cosmeticInstanceType.GetField("cosmeticType", BindingFlags.Public | BindingFlags.Instance);

        return _spawnedCosmeticsField != null;
    }
}