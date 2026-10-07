using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;

using BepInEx.Bootstrap;
using BepInEx.Configuration;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using MoreCompany.Cosmetics;

namespace FirstPersonView;

internal static class MoreCompanyCompat
{
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
        public string CosmeticId = string.Empty;
    }

    private sealed class SpawnedParts
    {
        public Transform Root = null!;
        public Renderer[] Renderers = Array.Empty<Renderer>();
        public Light[] Lights = Array.Empty<Light>();
    }

    private static bool _initialized;
    private static bool _isInstalled;

    private static readonly List<TrackedCosmetic> Tracked = new();
    private static readonly Dictionary<int, SpawnedParts> SpawnedAtApply = new();
    private static readonly List<int> DeadParts = new();
    private static readonly HashSet<string> HiddenIds = new(StringComparer.OrdinalIgnoreCase);
    private static string _hiddenIdsSource = string.Empty;
    private static int _trackedApplicationId;
    private static int _trackedCount = -1;
    private static Camera? _firstPersonCamera;
    private static bool _hiddenWithBody;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isInstalled = Chainloader.PluginInfos.ContainsKey(ModGUIDs.MoreCompany);

        if (!_isInstalled)
            return;

        Plugin.Log.LogInfo("MoreCompany detected.");

        try
        {
            PatchCosmeticSpawn();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not hook MoreCompany cosmetic spawning. Cosmetics that reparent themselves onto bones may stay visible in first person after respawning. {ex.Message}");
        }

        try
        {
            PublishInstalledCosmeticIds();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not list the installed MoreCompany cosmetic ids. {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void PublishInstalledCosmeticIds()
    {
        List<string> ids = new(CosmeticRegistry.cosmeticInstances.Keys);
        ids.Sort(StringComparer.OrdinalIgnoreCase);

        string joined = string.Join(",", ids);
        if (ConfigManager.InstalledCosmeticIds.Value != joined)
            ConfigManager.InstalledCosmeticIds.Value = joined;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void PatchCosmeticSpawn()
    {
        Plugin.Harmony.Patch(
            AccessTools.Method(typeof(CosmeticApplication), nameof(CosmeticApplication.ApplyCosmetic)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(MoreCompanyCompat), nameof(RecordSpawnedCosmetic))));
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void RecordSpawnedCosmetic(CosmeticApplication __instance, bool __result)
    {
        if (!__result)
            return;

        try
        {
            List<CosmeticInstance> spawned = __instance.spawnedCosmetics;
            if (spawned == null || spawned.Count == 0)
                return;

            CosmeticInstance instance = spawned[spawned.Count - 1];
            if (instance == null)
                return;

            Transform root = instance.transform;
            SpawnedAtApply[instance.GetInstanceID()] = new SpawnedParts
            {
                Root = root,
                Renderers = root.GetComponentsInChildren<Renderer>(true),
                Lights = root.GetComponentsInChildren<Light>(true),
            };
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to record a spawned MoreCompany cosmetic. {ex.Message}");
        }
    }

    public static void Reset()
    {
        Tracked.Clear();
        PruneSpawnedParts();
        _trackedApplicationId = 0;
        _trackedCount = -1;
        _firstPersonCamera = null;
        _hiddenWithBody = false;
    }

    public static void ApplyLocalCosmeticVisibility(
        LocalBodyState state, PlayerControllerB player, bool localBodyVisible, bool firstPersonArmsActive,
        bool hiddenWithBody)
    {
        if (!_isInstalled || !ConfigManager.EnableMoreCompanyCompatibility.Value)
        {
            StopTracking(deactivate: true);
            return;
        }

        if (!localBodyVisible && !hiddenWithBody)
        {
            StopTracking(deactivate: false);
            return;
        }

        _firstPersonCamera = player.gameplayCamera;
        _hiddenWithBody = hiddenWithBody;
        RefreshHiddenIds();

        if (!RefreshTracked(state, player))
        {
            StopTracking(deactivate: false);
            return;
        }

        UpdateRestingState(firstPersonArmsActive);
    }

    public static void CollectRiggedBodies(Transform? headBone, List<SkinnedMeshRenderer> into)
    {
        into.Clear();
        if (headBone == null)
            return;

        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            foreach (Renderer renderer in cosmetic.Renderers)
            {
                if (renderer == null || renderer is not SkinnedMeshRenderer skinned
                    || skinned.sharedMesh == null)
                    continue;

                Transform[] bones = skinned.bones;
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != headBone)
                        continue;

                    into.Add(skinned);
                    break;
                }
            }
        }
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

            SetRenderersEnabled(cosmetic, !_hiddenWithBody && (!firstPerson || cosmetic.ShowInFirstPerson));
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

            SetRenderersEnabled(cosmetic, !_hiddenWithBody);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool RefreshTracked(LocalBodyState state, PlayerControllerB player)
    {
        CosmeticApplication? cosmeticApplication = state.MoreCompanyCosmeticApplication as CosmeticApplication;
        if (cosmeticApplication == null)
        {
            Transform? metarig = player.transform.Find(Constants.Rig.ScavengerModel)?.Find(Constants.Rig.Metarig);
            if (metarig == null)
                return false;

            cosmeticApplication = metarig.GetComponent<CosmeticApplication>();
            if (cosmeticApplication == null)
                return false;

            state.MoreCompanyCosmeticApplication = cosmeticApplication;
        }

        int applicationId = cosmeticApplication.GetInstanceID();
        int count = cosmeticApplication.spawnedCosmetics.Count;

        if (applicationId == _trackedApplicationId && count == _trackedCount && AllTrackedAlive())
            return Tracked.Count > 0;

        Tracked.Clear();
        PruneSpawnedParts();
        _trackedApplicationId = applicationId;
        _trackedCount = count;

        foreach (CosmeticInstance cosmetic in cosmeticApplication.spawnedCosmetics)
        {
            if (cosmetic == null)
                continue;

            Transform root = cosmetic.transform;

            Renderer[] renderers;
            Light[] lights;
            if (SpawnedAtApply.TryGetValue(cosmetic.GetInstanceID(), out SpawnedParts spawned) && spawned.Root != null)
            {
                renderers = spawned.Renderers;
                lights = spawned.Lights;
            }
            else
            {
                renderers = root.GetComponentsInChildren<Renderer>(true);
                lights = root.GetComponentsInChildren<Light>(true);
            }

            int originalLayer = DrawnLayerFor(root, state.ModelRoot);

            int[] cullingMasks = new int[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null)
                    cullingMasks[i] = DrawnCullingMaskFor(lights[i].cullingMask, originalLayer);
            }

            Tracked.Add(new TrackedCosmetic
            {
                Root = root,
                Renderers = renderers,
                Lights = lights,
                LightCullingMasks = cullingMasks,
                OriginalLayer = originalLayer,
                FirstPersonToggle = FirstPersonToggleFor(cosmetic.cosmeticType),
                HiddenByFirstPersonArms = cosmetic.cosmeticType == CosmeticType.R_LOWER_ARM,
                CosmeticId = cosmetic.cosmeticId ?? string.Empty,
            });
        }

        return Tracked.Count > 0;
    }

    private static void PruneSpawnedParts()
    {
        if (SpawnedAtApply.Count == 0)
            return;

        DeadParts.Clear();
        foreach (KeyValuePair<int, SpawnedParts> entry in SpawnedAtApply)
        {
            if (entry.Value.Root == null)
                DeadParts.Add(entry.Key);
        }

        foreach (int key in DeadParts)
            SpawnedAtApply.Remove(key);
    }

    private static void RefreshHiddenIds()
    {
        string configured = ConfigManager.HideCosmetics.Value ?? string.Empty;
        if (configured == _hiddenIdsSource)
            return;

        _hiddenIdsSource = configured;
        HiddenIds.Clear();

        foreach (string entry in configured.Split(','))
        {
            string trimmed = entry.Trim();
            if (trimmed.Length > 0)
                HiddenIds.Add(trimmed);
        }
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

    private static bool IsReplacementBody(TrackedCosmetic cosmetic)
    {
        foreach (Renderer renderer in cosmetic.Renderers)
        {
            if (renderer != null && CosmeticBodyView.OwnsRenderer(renderer))
                return true;
        }

        return false;
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

            cosmetic.ShowInFirstPerson = !_hiddenWithBody
                && (ConfigManager.ShowCosmetics.Value || IsReplacementBody(cosmetic))
                && (cosmetic.FirstPersonToggle == null || cosmetic.FirstPersonToggle.Value)
                && !(cosmetic.HiddenByFirstPersonArms && firstPersonArmsActive)
                && !HiddenIds.Contains(cosmetic.CosmeticId);

            GameObject cosmeticObject = cosmetic.Root.gameObject;
            if (!cosmeticObject.activeSelf)
                cosmeticObject.SetActive(true);

            RestoreCosmeticLayer(cosmetic);

            SetRenderersEnabled(cosmetic, !_hiddenWithBody);
        }
    }

    private static void StopTracking(bool deactivate)
    {
        bool active = !deactivate;

        foreach (TrackedCosmetic cosmetic in Tracked)
        {
            if (cosmetic.Root == null)
                continue;

            SetRenderersEnabled(cosmetic, true);

            GameObject cosmeticObject = cosmetic.Root.gameObject;
            if (cosmeticObject.activeSelf != active)
                cosmeticObject.SetActive(active);
        }

        Reset();
    }

    private static int DrawnLayerFor(Transform cosmeticRoot, Transform? modelRoot)
    {
        int layer = cosmeticRoot.gameObject.layer;
        if (layer != Constants.Layers.EnemiesNotRendered)
            return layer;

        return modelRoot != null && modelRoot.gameObject.layer != Constants.Layers.EnemiesNotRendered
            ? modelRoot.gameObject.layer
            : 0;
    }

    private static void RestoreCosmeticLayer(TrackedCosmetic cosmetic)
    {
        if (cosmetic.Root.gameObject.layer != cosmetic.OriginalLayer)
        {
            SetLayerRecursively(cosmetic.Root, cosmetic.OriginalLayer);
            RestoreLightCullingMasks(cosmetic);
        }

        MoveRenderersOffHiddenLayer(cosmetic);
    }

    private static void RestoreLightCullingMasks(TrackedCosmetic cosmetic)
    {
        for (int i = 0; i < cosmetic.Lights.Length; i++)
        {
            Light light = cosmetic.Lights[i];
            if (light != null)
                light.cullingMask = cosmetic.LightCullingMasks[i];
        }
    }

    private static void MoveRenderersOffHiddenLayer(TrackedCosmetic cosmetic)
    {
        Renderer[] renderers = cosmetic.Renderers;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            GameObject rendererObject = renderer.gameObject;
            if (rendererObject.layer == Constants.Layers.EnemiesNotRendered)
                rendererObject.layer = cosmetic.OriginalLayer;
        }
    }

    private static int DrawnCullingMaskFor(int cullingMask, int drawnLayer)
    {
        return cullingMask == 1 << Constants.Layers.EnemiesNotRendered ? 1 << drawnLayer : cullingMask;
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
            if (renderer == null || (!enabled && CosmeticBodyView.OwnsRenderer(renderer)))
                continue;

            if (renderer.enabled != enabled)
                renderer.enabled = enabled;
        }
    }
}
