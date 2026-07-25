using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;

using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GameNetcodeStuff;
using UnityEngine;

using MoreCompany.Cosmetics;

namespace FirstPersonView.Compat;

internal static class MoreCompanyCompat
{
    private const int TooManyEmotesHiddenLayer = 23;

    private sealed class TrackedCosmetic
    {
        public Transform Root = null!;
        public Renderer[] Renderers = Array.Empty<Renderer>();
        public Light[] Lights = Array.Empty<Light>();
        public int[] LightCullingMasks = Array.Empty<int>();
        public int OriginalLayer;
        public ConfigEntry<bool>? FirstPersonToggle;
        public bool HiddenByFirstPersonArms;
        public bool ShowInFirstPerson = true;
    }

    private static bool _initialized;
    private static bool _isInstalled;

    private static readonly List<TrackedCosmetic> Tracked = new();
    private static int _trackedApplicationId;
    private static int _trackedCount = -1;
    private static Camera? _firstPersonCamera;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isInstalled = Chainloader.PluginInfos.ContainsKey(ModGUIDs.MoreCompany);

        if (_isInstalled)
            Plugin.Log.LogInfo("MoreCompany detected.");
    }

    public static void Reset()
    {
        Tracked.Clear();
        _trackedApplicationId = 0;
        _trackedCount = -1;
        _firstPersonCamera = null;
    }

    public static void ApplyLocalCosmeticVisibility(
        PlayerControllerB player, bool localBodyVisible, bool firstPersonArmsActive)
    {
        if (!_isInstalled || !ConfigManager.EnableMoreCompanyCompatibility.Value || !localBodyVisible)
        {
            StopTracking();
            return;
        }

        _firstPersonCamera = player.gameplayCamera;

        if (!RefreshTracked(player))
        {
            StopTracking();
            return;
        }

        UpdateRestingState(firstPersonArmsActive);
    }

    public static void ApplyForCamera(Camera renderingCamera)
    {
        if (Tracked.Count == 0)
            return;

        bool firstPerson = renderingCamera == _firstPersonCamera;

        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            SetRenderersEnabled(cosmetic, !firstPerson || cosmetic.ShowInFirstPerson);
        }
    }

    public static void RestoreAfterCamera(Camera renderingCamera)
    {
        if (Tracked.Count == 0)
            return;

        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            SetRenderersEnabled(cosmetic, true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool RefreshTracked(PlayerControllerB player)
    {
        Transform? metarig = player.transform.Find(Constants.ScavengerModelName)?.Find(Constants.MetarigName);
        if (metarig == null)
            return false;

        CosmeticApplication? cosmeticApplication = metarig.GetComponent<CosmeticApplication>();
        if (cosmeticApplication == null)
            return false;

        int applicationId = cosmeticApplication.GetInstanceID();
        int count = cosmeticApplication.spawnedCosmetics.Count;

        if (applicationId == _trackedApplicationId && count == _trackedCount && AllTrackedAlive())
            return Tracked.Count > 0;

        Tracked.Clear();
        _trackedApplicationId = applicationId;
        _trackedCount = count;

        foreach (CosmeticInstance cosmetic in cosmeticApplication.spawnedCosmetics)
        {
            if (cosmetic == null)
                continue;

            Transform root = cosmetic.transform;
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            int[] cullingMasks = new int[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                cullingMasks[i] = lights[i].cullingMask;

            Tracked.Add(new TrackedCosmetic
            {
                Root = root,
                Renderers = root.GetComponentsInChildren<Renderer>(true),
                Lights = lights,
                LightCullingMasks = cullingMasks,
                OriginalLayer = root.gameObject.layer,
                FirstPersonToggle = FirstPersonToggleFor(cosmetic.cosmeticType),
                HiddenByFirstPersonArms = cosmetic.cosmeticType == CosmeticType.R_LOWER_ARM,
            });
        }

        return Tracked.Count > 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static ConfigEntry<bool>? FirstPersonToggleFor(CosmeticType type)
    {
        return type switch
        {
            CosmeticType.HAT => ConfigManager.ShowMoreCompanyHat,
            CosmeticType.CHEST => ConfigManager.ShowMoreCompanyChest,
            CosmeticType.HIP => ConfigManager.ShowMoreCompanyHip,
            CosmeticType.R_LOWER_ARM => ConfigManager.ShowMoreCompanyRightLowerArm,
            CosmeticType.L_SHIN => ConfigManager.ShowMoreCompanyLeftShin,
            CosmeticType.R_SHIN => ConfigManager.ShowMoreCompanyRightShin,
            _ => null,
        };
    }

    private static bool AllTrackedAlive()
    {
        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                return false;
        }

        return true;
    }

    private static void UpdateRestingState(bool firstPersonArmsActive)
    {
        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            cosmetic.ShowInFirstPerson =
                (cosmetic.FirstPersonToggle == null || cosmetic.FirstPersonToggle.Value)
                && !(cosmetic.HiddenByFirstPersonArms && firstPersonArmsActive);

            GameObject cosmeticObject = cosmetic.Root.gameObject;
            if (!cosmeticObject.activeSelf)
                cosmeticObject.SetActive(true);

            RestoreCosmeticLayer(cosmetic);
            SetRenderersEnabled(cosmetic, true);
        }
    }

    private static void StopTracking()
    {
        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            SetRenderersEnabled(cosmetic, true);

            GameObject cosmeticObject = cosmetic.Root.gameObject;
            if (cosmeticObject.activeSelf != cosmetic.ShowInFirstPerson)
                cosmeticObject.SetActive(cosmetic.ShowInFirstPerson);
        }

        Reset();
    }

    private static void RestoreCosmeticLayer(TrackedCosmetic cosmetic)
    {
        if (cosmetic.OriginalLayer == TooManyEmotesHiddenLayer)
            return;
        if (cosmetic.Root.gameObject.layer == cosmetic.OriginalLayer)
            return;

        SetLayerRecursively(cosmetic.Root, cosmetic.OriginalLayer);

        for (int i = 0; i < cosmetic.Lights.Length; i++)
        {
            Light light = cosmetic.Lights[i];
            if (light != null)
                light.cullingMask = cosmetic.LightCullingMasks[i];
        }
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;

        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private static void SetRenderersEnabled(TrackedCosmetic cosmetic, bool enabled)
    {
        Renderer[] renderers = cosmetic.Renderers;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.enabled != enabled)
                renderer.enabled = enabled;
        }
    }
}
