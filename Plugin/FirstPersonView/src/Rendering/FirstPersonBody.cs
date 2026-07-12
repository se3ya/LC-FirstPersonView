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

    public static void ApplyBodyRendering(LocalBodyState state, bool showBody, bool useVanillaArms)
    {
        PlayerControllerB player = state.Player;
        if (player == null || !player.isPlayerControlled || player.isPlayerDead)
            return;

        if (showBody)
        {
            if (player.thisPlayerModel != null)
            {
                if (!player.thisPlayerModel.enabled)
                    player.thisPlayerModel.enabled = true;
                if (player.thisPlayerModel.shadowCastingMode != ShadowCastingMode.On)
                    player.thisPlayerModel.shadowCastingMode = ShadowCastingMode.On;
                if (player.thisPlayerModel.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                    player.thisPlayerModel.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            if (state.PlayerLodGroup != null)
            {
                if (!state.PlayerLodGroup.enabled)
                    state.PlayerLodGroup.enabled = true;
                state.PlayerLodGroup.ForceLOD(0);
            }

            if (player.thisPlayerModelLOD1 != null && player.thisPlayerModelLOD1.enabled)
                player.thisPlayerModelLOD1.enabled = false;
            if (player.thisPlayerModelLOD2 != null && player.thisPlayerModelLOD2.enabled)
                player.thisPlayerModelLOD2.enabled = false;

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

    // Vanilla never draws layer 23
    public static void ApplyFirstPersonCameraLayer(LocalBodyState state, bool showBody)
    {
        Camera? camera = state.GameplayCamera;
        if (camera == null)
            return;

        int bodyLayerBit = 1 << Constants.EnemiesNotRenderedLayer;
        bool rendered = (camera.cullingMask & bodyLayerBit) != 0;

        if (showBody && !rendered)
            camera.cullingMask |= bodyLayerBit;
        else if (!showBody && rendered)
            camera.cullingMask &= ~bodyLayerBit;
    }

    public static void HideHead(LocalBodyState state, bool hideArms)
    {
        hideArmsInFirstPerson = hideArms;
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

        if (fpArms != null)
            fpArms.enabled = fpArmsIntended && camera == hideCamera;
    }

    public static void RestoreAfterCamera(Camera camera)
    {
        if (fpArms != null && fpArms.enabled != fpArmsIntended)
            fpArms.enabled = fpArmsIntended;

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
    }
}