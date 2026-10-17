using System;

using GameNetcodeStuff;
using UnityEngine.Rendering;
using UnityEngine;

namespace FirstPersonView;

internal static class FirstPersonBody
{
    private static readonly CapGate Gate = new();

    private static SkinnedMeshRenderer? hideRenderer;
    private static Mesh? originalMesh;
    private static Mesh? headlessMesh;
    private static Mesh? headlessArmlessMesh;
    private static Mesh? ownMesh;
    private static Mesh? ownArmlessMesh;
    private static LocalBodyState? gateState;
    private static Mesh? gateSource;
    private static SkinnedMeshRenderer? gateBody;
    private static ClearancePoint[]? gatePoints;
    private static bool gateFailed;
    private static SkinnedMeshRenderer? capRenderer;
    private static Material? capSuit;
    private static bool capIntended;
    private static float capHeight = float.NaN;
    private static bool hideArmsInFirstPerson;
    private static Camera? hideCamera;
    private static bool hideActive;
    private static SkinnedMeshRenderer? fpArms;
    private static bool fpArmsIntended;
    private static bool bodyDisabledWarned;
    private static float bodyDisabledSince;
    private static bool bodyLayerMoveLogged;
    private static SkinnedMeshRenderer? shadowProxy;
    private static bool shadowMuted;

    public static bool HideActive => hideActive;
    internal static bool VanillaArmsShown => fpArmsIntended;

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
        if (body.gameObject.layer != Constants.Layers.EnemiesNotRendered)
            return;

        Transform? root = state.ModelRoot;
        int target = root != null && root.gameObject.layer != Constants.Layers.EnemiesNotRendered
            ? root.gameObject.layer
            : 0;

        body.gameObject.layer = target;

        if (bodyLayerMoveLogged)
            return;

        bodyLayerMoveLogged = true;
        Plugin.Log.LogInfo($"Your body renderer was on layer {Constants.Layers.EnemiesNotRendered} which most cameras do not draw. Moved it to layer {target} so it shows on cameras.");
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

        if (Time.unscaledTime - bodyDisabledSince < Constants.Body.DisabledWarnDelay)
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

        ownMesh = MeshSurgery.GetOwnMesh(original, body, state.HeadBone);
        ownArmlessMesh = hideArms
            ? MeshSurgery.GetOwnArmlessMesh(original, body, state.HeadBone, state.LeftArmBone, state.RightArmBone)
            : null;

        Mesh? capMesh = MeshSurgery.GetCapMesh(original);
        if (capMesh != null && (ownMesh != null || ownArmlessMesh != null))
            SetupCap(state, original, body, capMesh);

        hideRenderer = body;
        originalMesh = original;
        headlessMesh = headless;
        hideActive = true;
        UpdateShadowProxy(state, active: true);
    }

    private static void SetupCap(LocalBodyState state, Mesh source, SkinnedMeshRenderer body, Mesh capMesh)
    {
        gateState = state;
        if (ReferenceEquals(gateSource, source) && ReferenceEquals(gateBody, body))
            return;

        gateSource = source;
        gateBody = body;
        gatePoints = null;
        Gate.Reset();
        DestroyCap();

        try
        {
            gatePoints = NeckCap.CreatePoints(source, body);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to create the neck cap points on '{source.name}'. {ex.Message} The cap stays off.");
            return;
        }

        if (gatePoints == null)
        {
            Plugin.Log.LogDebug($"Could not skin the neck cap points on '{source.name}', the cap stays off.");
            return;
        }

        try
        {
            capRenderer = CreateCap(body, capMesh);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to create the neck cap on '{source.name}'. {ex.Message} The hole stays open.");
        }
    }

    private static SkinnedMeshRenderer CreateCap(SkinnedMeshRenderer body, Mesh capMesh)
    {
        GameObject capObject = new("FPVNeckCap");
        try
        {
            Transform bodyTransform = body.transform;
            capObject.transform.SetParent(bodyTransform.parent, worldPositionStays: false);
            capObject.transform.localPosition = bodyTransform.localPosition;
            capObject.transform.localRotation = bodyTransform.localRotation;
            capObject.transform.localScale = bodyTransform.localScale;
            capObject.layer = body.gameObject.layer;

            SkinnedMeshRenderer cap = capObject.AddComponent<SkinnedMeshRenderer>();
            cap.enabled = false;
            cap.sharedMesh = capMesh;
            cap.bones = body.bones;
            cap.rootBone = body.rootBone;
            cap.sharedMaterial = body.sharedMaterial;
            cap.shadowCastingMode = ShadowCastingMode.Off;
            cap.receiveShadows = body.receiveShadows;
            cap.lightProbeUsage = body.lightProbeUsage;
            cap.reflectionProbeUsage = body.reflectionProbeUsage;
            cap.renderingLayerMask = body.renderingLayerMask;
            cap.allowOcclusionWhenDynamic = body.allowOcclusionWhenDynamic;
            cap.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            cap.updateWhenOffscreen = true;
            return cap;
        }
        catch (Exception)
        {
            UnityEngine.Object.Destroy(capObject);
            throw;
        }
    }

    private static void DestroyCap()
    {
        SkinnedMeshRenderer? cap = capRenderer;
        capRenderer = null;
        capSuit = null;
        capIntended = false;
        if (cap != null)
            UnityEngine.Object.Destroy(cap.gameObject);
    }

    public static void UpdateCap()
    {
        SkinnedMeshRenderer? cap = capRenderer;
        SkinnedMeshRenderer? body = hideRenderer;
        if (cap == null || body == null)
        {
            capIntended = false;
            capHeight = float.NaN;
            SetCapVisible(false);
            return;
        }

        bool active = hideActive && body.enabled && GetOwn() != null;
        capHeight = active ? GetCapHeight() : float.NaN;
        capIntended = active && Gate.Update(capHeight, IsStanding(), IsCrouching());

        int layer = body.gameObject.layer;
        if (cap.gameObject.layer != layer)
            cap.gameObject.layer = layer;

        Material suit = body.sharedMaterial;
        if (capSuit != suit)
        {
            cap.sharedMaterial = suit;
            capSuit = suit;
        }

        SetCapVisible(capIntended);
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

        if (state.ShadowProxy.gameObject.layer != body.gameObject.layer)
            state.ShadowProxy.gameObject.layer = body.gameObject.layer;
        if (!state.ShadowProxy.enabled)
            state.ShadowProxy.enabled = true;

        shadowProxy = state.ShadowProxy;
    }

    public static void ApplyForCamera(Camera camera)
    {
        SetArmsVisibleForCamera(fpArmsIntended && camera == hideCamera);
        SetCapVisible(capIntended && hideActive && camera == hideCamera);

        SkinnedMeshRenderer? body = hideRenderer;
        if (!hideActive || body == null)
            return;

        if (camera == hideCamera)
        {
            Mesh? fpMesh = GetHideMesh();
            if (fpMesh != null && body.sharedMesh != fpMesh)
                body.sharedMesh = fpMesh;

            if (shadowProxy != null && shadowProxy.enabled && body.shadowCastingMode == ShadowCastingMode.On)
            {
                body.shadowCastingMode = ShadowCastingMode.Off;
                shadowMuted = true;
            }
        }
        else if (originalMesh != null && body.sharedMesh != originalMesh)
        {
            body.sharedMesh = originalMesh;
        }
    }

    internal static Mesh? GetHideMesh()
    {
        if (!hideActive)
            return null;

        Mesh? own = GetOwn();
        if (own != null)
            return own;

        return (hideArmsInFirstPerson && headlessArmlessMesh != null)
            ? headlessArmlessMesh
            : headlessMesh;
    }

    private static Mesh? GetOwn()
    {
        return hideArmsInFirstPerson && headlessArmlessMesh != null ? ownArmlessMesh : ownMesh;
    }

    private static bool IsStanding() => gateState != null && gateState.Shift.Pose.Key.IsStanding();

    private static bool IsCrouching() => gateState != null && gateState.Player.isCrouching;

    private static float GetCapHeight()
    {
        try
        {
            LocalBodyState? state = gateState;
            ClearancePoint[]? points = gatePoints;
            if (state == null || points == null || !state.CameraBaseCaptured)
                return float.NaN;

            Camera? camera = state.GameplayCamera;
            Transform? head = state.HeadBone;
            if (camera == null || head == null || head.parent == null)
                return float.NaN;

            Transform? parent = camera.transform.parent;
            Vector3 local = state.HasCameraTarget ? state.Shift.ReferenceLocal : state.CameraBaseLocalPosition;
            Vector3 vanilla = parent != null ? parent.TransformPoint(local) : local;
            float scale = parent != null ? Mathf.Max(parent.lossyScale.y, 0.01f) : 1f;
            Vector3 up = head.position - head.parent.position;
            float height = NeckCap.GetHeight(
                points[0].GetPosition(), points[1].GetPosition(), points[2].GetPosition(), up, vanilla);
            return height / scale;
        }
        catch (Exception ex)
        {
            if (!gateFailed)
            {
                gateFailed = true;
                Plugin.Log.LogWarning($"Failed to measure the neck cap height. {ex.Message} The cap stays off.");
            }

            return float.NaN;
        }
    }

    private static void SetArmsVisibleForCamera(bool visible)
    {
        if (fpArms == null)
            return;

        if (fpArms.enabled != visible)
            fpArms.enabled = visible;
    }

    private static void SetCapVisible(bool visible)
    {
        SkinnedMeshRenderer? cap = capRenderer;
        if (cap != null && cap.enabled != visible)
            cap.enabled = visible;
    }

    public static void RestoreAfterCamera(Camera camera)
    {
        if (camera == hideCamera)
        {
            SetArmsVisibleForCamera(fpArmsIntended);
            SetCapVisible(capIntended && hideActive);
        }

        SkinnedMeshRenderer? body = hideRenderer;
        if (body == null)
            return;

        if (shadowMuted && camera == hideCamera)
        {
            if (body.shadowCastingMode == ShadowCastingMode.Off)
                body.shadowCastingMode = ShadowCastingMode.On;

            shadowMuted = false;
        }

        if (originalMesh != null && camera == hideCamera && body.sharedMesh != originalMesh)
            body.sharedMesh = originalMesh;
    }

    public static void Reset()
    {
        if (shadowMuted && hideRenderer != null && hideRenderer.shadowCastingMode == ShadowCastingMode.Off)
            hideRenderer.shadowCastingMode = ShadowCastingMode.On;

        shadowMuted = false;
        shadowProxy = null;
        hideActive = false;
        hideRenderer = null;
        originalMesh = null;
        headlessMesh = null;
        headlessArmlessMesh = null;
        ownMesh = null;
        ownArmlessMesh = null;
        gateState = null;
        gateSource = null;
        gateBody = null;
        gatePoints = null;
        gateFailed = false;
        capHeight = float.NaN;
        Gate.Reset();
        DestroyCap();
        hideArmsInFirstPerson = false;
        hideCamera = null;
        fpArms = null;
        fpArmsIntended = false;
        bodyDisabledWarned = false;
        bodyDisabledSince = 0f;
        bodyLayerMoveLogged = false;
    }
}
