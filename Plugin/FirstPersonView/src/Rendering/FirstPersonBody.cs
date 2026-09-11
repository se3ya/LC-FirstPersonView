using System;

using GameNetcodeStuff;
using UnityEngine.Rendering;
using UnityEngine;

namespace FirstPersonView;

internal static class FirstPersonBody
{
    private static SkinnedMeshRenderer? hideRenderer;
    private static Mesh? originalMesh;
    private static Mesh? headlessMesh;
    private static Mesh? headlessArmlessMesh;
    private static bool hideArmsInFirstPerson;
    private static Camera? hideCamera;
    private static bool hideActive;
    private static SkinnedMeshRenderer? fpArms;
    private static bool fpArmsIntended;
    private static bool bodyDisabledWarned;
    private static float bodyDisabledSince;
    private static bool bodyLayerMoveLogged;

    public static bool HideActive => hideActive;

    public static void ApplyBodyRendering(LocalBodyState state, bool showBody, bool useVanillaArms)
    {
        PlayerControllerB player = state.Player;
        if (player == null || !player.isPlayerControlled || player.isPlayerDead)
            return;

        if (showBody)
        {
            if (player.thisPlayerModel != null)
            {
                WarnIfBodyRendererDisabled(player.thisPlayerModel);
                RestoreBodyLayer(state, player.thisPlayerModel);

                if (player.thisPlayerModel.shadowCastingMode != ShadowCastingMode.On)
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                if (player.thisPlayerModel.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                    player.thisPlayerModel.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            bool modelReplaced = IsPlayerModelReplaced(player);

            if (state.PlayerLodGroup != null)
            {
                if (!state.PlayerLodGroup.enabled)
                    state.PlayerLodGroup.enabled = true;
                if (player.thisPlayerModel != null
                    && (state.BodyLodIndex < 0 || Time.unscaledTime >= state.BodyLodNextScan))
                {
                    state.BodyLodIndex = FindLodIndex(state.PlayerLodGroup, player.thisPlayerModel);
                    state.BodyLodNextScan = Time.unscaledTime + 0.5f;
                }

                state.PlayerLodGroup.ForceLOD(
                    modelReplaced ? -1 : (state.BodyLodIndex < 0 ? 0 : state.BodyLodIndex));
            }

            if (!modelReplaced)
            {
                if (player.thisPlayerModelLOD1 != null && player.thisPlayerModelLOD1.enabled)
                    player.thisPlayerModelLOD1.enabled = false;
                if (player.thisPlayerModelLOD2 != null && player.thisPlayerModelLOD2.enabled)
                    player.thisPlayerModelLOD2.enabled = false;
            }

            SetFpArmsEnabled(player, useVanillaArms);
        }
        else
        {
            if (player.thisPlayerModel != null
                && player.thisPlayerModel.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            SetFpArmsEnabled(player, true);
        }
    }

    private static void RestoreBodyLayer(LocalBodyState state, SkinnedMeshRenderer body)
    {
        if (body.gameObject.layer != Constants.EnemiesNotRenderedLayer)
            return;

        Transform? root = state.ModelRoot;
        int target = root != null && root.gameObject.layer != Constants.EnemiesNotRenderedLayer
            ? root.gameObject.layer
            : 0;

        body.gameObject.layer = target;

        if (bodyLayerMoveLogged)
            return;

        bodyLayerMoveLogged = true;
        Plugin.Log.LogInfo($"Your body renderer was on layer {Constants.EnemiesNotRenderedLayer} which most cameras do not draw. Moved it to layer {target} so it shows on cameras.");
    }

    private static void WarnIfBodyRendererDisabled(SkinnedMeshRenderer body)
    {
        if (bodyDisabledWarned)
            return;

        if (body.enabled || CosmeticBodyView.Active)
        {
            bodyDisabledSince = 0f;
            return;
        }

        if (bodyDisabledSince <= 0f)
        {
            bodyDisabledSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - bodyDisabledSince < Constants.BodyDisabledWarnDelay)
            return;

        bodyDisabledWarned = true;
        Plugin.Log.LogError("Your body renderer was disabled by another mod and nothing rigged to your skeleton replaced it so First-Person View cannot show your body. Mods such as ModelReplacementAPI are not supported and won't be supported.");
    }

    private static void SetFpArmsEnabled(PlayerControllerB player, bool enabled)
    {
        fpArms = player.thisPlayerModelArms;
        fpArmsIntended = enabled;
        if (fpArms == null)
            return;

        if (fpArms.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
            fpArms.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        if (fpArms.enabled != enabled)
            fpArms.enabled = enabled;
    }

    private static int FindLodIndex(LODGroup group, Renderer target)
    {
        LOD[] lods = group.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            Renderer[] renderers = lods[i].renderers;
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == target)
                    return i;
            }
        }
        return 0;
    }

    private static bool IsPlayerModelReplaced(PlayerControllerB player)
    {
        return player.thisPlayerModel != null && !player.thisPlayerModel.enabled;
    }

    public static void HideHead(LocalBodyState state, bool hideArms)
    {
        hideArmsInFirstPerson = hideArms;
        hideCamera = state.GameplayCamera;
        state.HeadHidden = true;

        SkinnedMeshRenderer? body = state.BodyRenderer;
        Mesh? original = state.OriginalBodyMesh;
        if (body == null || original == null || state.HeadBone == null)
        {
            hideActive = false;
            UpdateShadowProxy(state, active: false);
            return;
        }

        Mesh? headless = MeshSurgery.GetOrCreateHeadlessMesh(original, body, state.HeadBone);
        if (headless == null)
        {
            hideActive = false;
            UpdateShadowProxy(state, active: false);
            return;
        }

        if (body.sharedMesh != original)
            body.sharedMesh = original;

        headlessArmlessMesh = hideArms
            ? MeshSurgery.GetOrCreateHeadlessArmlessMesh(original, body, state.HeadBone, state.LeftArmBone, state.RightArmBone)
            : null;

        hideRenderer = body;
        originalMesh = original;
        headlessMesh = headless;
        hideCamera = state.GameplayCamera;
        hideActive = true;
        UpdateShadowProxy(state, active: true);
    }

    public static void RestoreHead(LocalBodyState state)
    {
        hideActive = false;
        hideCamera = state.GameplayCamera;

        SkinnedMeshRenderer? body = state.BodyRenderer;
        if (body != null && state.OriginalBodyMesh != null && body.sharedMesh != state.OriginalBodyMesh)
            body.sharedMesh = state.OriginalBodyMesh;

        UpdateShadowProxy(state, active: false);
        state.HeadHidden = false;
    }

    private static void UpdateShadowProxy(LocalBodyState state, bool active)
    {
        if (!active)
        {
            if (state.ShadowProxy != null)
                state.ShadowProxy.enabled = false;
            return;
        }

        SkinnedMeshRenderer? body = state.BodyRenderer;
        Mesh? full = state.OriginalBodyMesh;
        if (body == null || full == null)
            return;

        if (!body.enabled)
        {
            if (state.ShadowProxy != null)
                state.ShadowProxy.enabled = false;
            return;
        }

        if (state.ShadowProxy == null)
        {
            try
            {
                GameObject proxyObject = new("FPVHeadShadowProxy");
                proxyObject.transform.SetParent(body.transform.parent, worldPositionStays: false);
                proxyObject.transform.localPosition = body.transform.localPosition;
                proxyObject.transform.localRotation = body.transform.localRotation;
                proxyObject.transform.localScale = body.transform.localScale;

                SkinnedMeshRenderer proxy = proxyObject.AddComponent<SkinnedMeshRenderer>();
                proxy.sharedMesh = full;
                proxy.bones = body.bones;
                proxy.rootBone = body.rootBone;
                proxy.sharedMaterials = body.sharedMaterials;
                proxy.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                proxy.updateWhenOffscreen = true;
                state.ShadowProxy = proxy;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to create head shadow proxy. {ex.Message}");
                return;
            }
        }

        state.ShadowProxy.gameObject.layer = body.gameObject.layer;
        if (!state.ShadowProxy.enabled)
            state.ShadowProxy.enabled = true;
    }

    public static void ApplyForCamera(Camera camera)
    {
        SetArmsVisibleForCamera(fpArmsIntended && camera == hideCamera);

        SkinnedMeshRenderer? body = hideRenderer;
        if (!hideActive || body == null)
            return;

        if (camera == hideCamera)
        {
            Mesh? fpMesh = (hideArmsInFirstPerson && headlessArmlessMesh != null)
                ? headlessArmlessMesh
                : headlessMesh;
            if (fpMesh != null && body.sharedMesh != fpMesh)
                body.sharedMesh = fpMesh;
        }
        else if (originalMesh != null && body.sharedMesh != originalMesh)
        {
            body.sharedMesh = originalMesh;
        }
    }

    private static void SetArmsVisibleForCamera(bool visible)
    {
        if (fpArms == null)
            return;

        if (fpArms.enabled != visible)
            fpArms.enabled = visible;
    }

    public static void RestoreAfterCamera(Camera camera)
    {
        if (camera == hideCamera)
            SetArmsVisibleForCamera(fpArmsIntended);

        SkinnedMeshRenderer? body = hideRenderer;
        if (body == null || originalMesh == null)
            return;

        if (camera == hideCamera && body.sharedMesh != originalMesh)
            body.sharedMesh = originalMesh;
    }

    public static void Reset()
    {
        hideActive = false;
        hideRenderer = null;
        originalMesh = null;
        headlessMesh = null;
        headlessArmlessMesh = null;
        hideArmsInFirstPerson = false;
        hideCamera = null;
        fpArms = null;
        fpArmsIntended = false;
        bodyDisabledWarned = false;
        bodyDisabledSince = 0f;
        bodyLayerMoveLogged = false;
    }
}
