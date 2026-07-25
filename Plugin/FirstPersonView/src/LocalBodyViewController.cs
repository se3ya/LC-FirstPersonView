using System.Collections.Generic;
using System;

using GameNetcodeStuff;
using UnityEngine.Rendering;
using UnityEngine;

using FirstPersonView.Compat;

namespace FirstPersonView;

internal static class LocalBodyViewController
{
    private static readonly Dictionary<int, LocalBodyState> States = new();
    private static bool renderCallbacksHooked;

    internal static bool LocalBodyShown;

    public static void ResetAllStates()
    {
        foreach (LocalBodyState state in States.Values)
        {
            FirstPersonBody.RestoreHead(state);
            VisorRig.Restore(state);
            CameraRig.RestoreOffset(state);

            if (state.ShadowProxy != null)
                UnityEngine.Object.Destroy(state.ShadowProxy.gameObject);
        }

        States.Clear();
        LocalBodyShown = false;

        FirstPersonBody.Reset();
        HeldItemView.Reset();
        MoreCompanyCompat.Reset();
        MeshSurgery.ClearCaches();

        if (renderCallbacksHooked)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            renderCallbacksHooked = false;
        }
    }

    public static void Tick(PlayerControllerB player)
    {
        if (!Players.IsLocal(player))
            return;

        EnsureRenderCallbacksHooked();

        LocalBodyState state = GetOrCreateState(player);
        if (NeedsRefresh(state, player))
        {
            try
            {
                RefreshState(state, player);
            }
            catch (Exception ex)
            {
                LogStageError("State refresh", ex);
                return;
            }
        }

        bool showBody = player.isPlayerControlled && !player.isPlayerDead;
        LocalBodyShown = showBody;

        bool holdingArms = false;
        try
        {
            bool phoneActive = LethalPhonesCompat.IsLocalPhoneActive(state, player);
            holdingArms = !player.inSpecialInteractAnimation
                && (phoneActive
                    || (Players.IsActivelyHolding(player) && ConfigManager.Hands.Value == HandsMode.Vanilla));
        }
        catch (Exception ex)
        {
            LogStageError("Holding arms", ex);
        }

        bool movementArms = false;
        try
        {
            if (showBody
                && !Players.IsActivelyHolding(player)
                && !player.inSpecialInteractAnimation)
            {
                movementArms = MovementArmsGate.Update(state, player, Time.deltaTime);
            }
            else
            {
                MovementArmsGate.Reset(state);
            }
        }
        catch (Exception ex)
        {
            LogStageError("Movement arms", ex);
        }

        bool useVanillaArms = showBody && (holdingArms || movementArms);

        try
        {
            FirstPersonBody.ApplyBodyRendering(state, showBody, useVanillaArms);
            FirstPersonBody.ApplyFirstPersonCameraLayer(state, showBody);
        }
        catch (Exception ex)
        {
            LogStageError("Body rendering", ex);
        }

        try
        {
            HeldItemView.ApplyHolder(state, player, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("Held item", ex);
        }

        try
        {
            if (showBody)
                FirstPersonBody.HideHead(state, useVanillaArms);
            else
                FirstPersonBody.RestoreHead(state);
        }
        catch (Exception ex)
        {
            LogStageError("Head hiding", ex);
        }

        try
        {
            if (showBody && !VisorRig.IsImmersiveVisorPresent())
                VisorRig.StickToCamera(state);
            else
                VisorRig.Restore(state);

            VisorRig.SuppressVanillaCrack(state, VisorRig.IsImmersiveVisorPresent());
        }
        catch (Exception ex)
        {
            LogStageError("Visor", ex);
        }

        try
        {
            if (IsInCruiserImprovedSeat(player))
                CameraRig.PrepareVehicleDetach(state);
            else if (showBody && !player.inTerminalMenu)
                CameraRig.ApplyOffset(state, player);
            else
                CameraRig.RestoreOffset(state);
        }
        catch (Exception ex)
        {
            LogStageError("Camera offset", ex);
        }

        try
        {
            CameraRig.ApplyArmAnchor(player, useVanillaArms);
            CameraRig.RelaxSeatedLookClamp(player);
        }
        catch (Exception ex)
        {
            LogStageError("Arm anchor", ex);
        }

        try
        {
            MoreCompanyCompat.ApplyLocalCosmeticVisibility(player, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("MoreCompany cosmetics", ex);
        }
    }

    private static readonly HashSet<string> _loggedStageErrors = new();

    private static void LogStageError(string stage, Exception ex)
    {
        if (_loggedStageErrors.Add($"{stage}|{ex.GetType()}: {ex.Message}"))
            Plugin.Log.LogError($"{stage} failed.\n{ex}");
    }

    public static void AlignCameraForInteractionRay(PlayerControllerB player)
    {
        if (!Players.IsLocal(player))
            return;
        if (!States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return;

        bool detachForVehicle = IsInCruiserImprovedSeat(player);

        if (detachForVehicle)
            CameraRig.ApplyVehiclePush(state);
        else
            CameraRig.AlignForInteractionRay(state);
    }

    private static bool IsInCruiserImprovedSeat(PlayerControllerB player)
    {
        if (!ConfigManager.EnableCruiserImprovedCompatibility.Value || !player.inVehicleAnimation)
            return false;

        InteractTrigger? trigger = player.currentTriggerInAnimationWith;
        if (trigger == null || trigger.overridePlayerParent == null)
            return false;

        return trigger.overridePlayerParent.TryGetComponent(out VehicleController controller)
            && controller.vehicleID == 0;
    }

    private static LocalBodyState GetOrCreateState(PlayerControllerB player)
    {
        int key = player.GetInstanceID();
        if (!States.TryGetValue(key, out LocalBodyState? state))
        {
            state = new LocalBodyState();
            States[key] = state;
        }

        return state;
    }

    private static bool NeedsRefresh(LocalBodyState state, PlayerControllerB player)
    {
        return state.Player != player
            || state.ModelRoot == null
            || state.HeadBone == null
            || state.BodyRenderer == null;
    }

    private static void RefreshState(LocalBodyState state, PlayerControllerB player)
    {
        state.Player = player;

        Transform? scavengerModel = player.transform.Find(Constants.ScavengerModelName);
        state.ModelRoot = scavengerModel != null ? scavengerModel : player.transform;

        state.HeadBone = FindHeadBone(state.ModelRoot, player);

        state.LeftArmBone = FindBodyBone(player, Constants.LeftArmBoneName);
        state.RightArmBone = FindBodyBone(player, Constants.RightArmBoneName);

        state.BodyRenderer = player.thisPlayerModel;
        state.PlayerLodGroup = player.GetComponentInChildren<LODGroup>();
        state.BodyLodIndex = -1;
        CaptureOriginalBodyMesh(state);

        state.LocalVisor = player.localVisor;
        CaptureVisorOriginalParent(state);

        state.GameplayCamera = player.gameplayCamera;
        CaptureCameraBase(state);
    }

    private static void CaptureOriginalBodyMesh(LocalBodyState state)
    {
        if (state.BodyRenderer == null || state.OriginalBodyMeshCaptured || state.HeadHidden)
            return;

        Mesh current = state.BodyRenderer.sharedMesh;
        if (current == null || MeshSurgery.IsGenerated(current.GetInstanceID()))
            return;

        state.OriginalBodyMesh = current;
        state.OriginalBodyMeshCaptured = true;
    }

    private static void CaptureVisorOriginalParent(LocalBodyState state)
    {
        if (state.LocalVisor == null || state.VisorOriginalParentCaptured || state.VisorReparented)
            return;

        state.VisorOriginalParent = state.LocalVisor.parent;
        state.VisorOriginalParentCaptured = true;
    }

    private static void CaptureCameraBase(LocalBodyState state)
    {
        if (state.GameplayCamera == null || state.CameraBaseCaptured || state.CameraOffsetApplied)
            return;

        state.CameraBaseLocalPosition = state.GameplayCamera.transform.localPosition;
        state.CameraBaseCaptured = true;
    }

    private static Transform? FindHeadBone(Transform modelRoot, PlayerControllerB player)
    {
        SkinnedMeshRenderer? bodyMesh = player.thisPlayerModel;
        if (bodyMesh != null)
        {
            foreach (Transform bone in bodyMesh.bones)
            {
                if (bone != null && NameContains(bone.name, Constants.HeadBoneName))
                    return bone;
            }
        }

        return FindFirstByNameContains(modelRoot, Constants.HeadBoneName)
            ?? FindFirstByNameContains(modelRoot, "head");
    }

    private static Transform? FindBodyBone(PlayerControllerB player, string boneName)
    {
        SkinnedMeshRenderer? bodyMesh = player.thisPlayerModel;
        if (bodyMesh == null)
            return null;

        foreach (Transform bone in bodyMesh.bones)
        {
            if (bone != null && string.Equals(bone.name, boneName, StringComparison.OrdinalIgnoreCase))
                return bone;
        }

        return null;
    }

    private static Transform? FindFirstByNameContains(Transform root, string token)
    {
        Queue<Transform> queue = new();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (NameContains(current.name, token))
                return current;

            for (int i = 0; i < current.childCount; i++)
                queue.Enqueue(current.GetChild(i));
        }

        return null;
    }

    private static bool NameContains(string name, string token) =>
        name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    private static void EnsureRenderCallbacksHooked()
    {
        if (renderCallbacksHooked)
            return;

        renderCallbacksHooked = true;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private static readonly (Action<Camera> Apply, Action<Camera> Restore)[] CameraPasses =
    {
        (FirstPersonBody.ApplyForCamera, FirstPersonBody.RestoreAfterCamera),
        (HeldItemView.ApplyForCamera, HeldItemView.RestoreAfterCamera),
        (MoreCompanyCompat.ApplyForCamera, MoreCompanyCompat.RestoreAfterCamera),
    };

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (var pass in CameraPasses)
            pass.Apply(camera);
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (var pass in CameraPasses)
            pass.Restore(camera);
    }
}