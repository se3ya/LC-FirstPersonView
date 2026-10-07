using System.Collections.Generic;
using System;

using GameNetcodeStuff;
using UnityEngine.Rendering;
using UnityEngine;

namespace FirstPersonView;

internal static class LocalBodyViewController
{
    private static readonly Dictionary<int, LocalBodyState> States = new();
    private static bool renderCallbacksHooked;
    private static InteractTrigger? seatTrigger;
    private static bool seatVehicle;
    private static bool hideWasOn;
    private static bool hideCleared;

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

        BodyBack.Restore();
        States.Clear();
        LocalBodyShown = false;
        seatTrigger = null;
        seatVehicle = false;
        hideWasOn = false;
        hideCleared = false;

        FirstPersonBody.Reset();
        HeldItemView.Reset();
        MoreCompanyCompat.Reset();
        CosmeticBodyView.Reset();
        VisorShadow.Reset();
        SwingTorsoSmoothing.Reset();
        ItemHands.Reset();
        HeldItemCopy.Reset();
        VisorGlass.Restore();
        MeshSurgery.ClearCaches();

        if (renderCallbacksHooked)
        {
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            renderCallbacksHooked = false;
        }
    }

    public static void Tick(PlayerControllerB player)
    {
        if (!Players.IsLocal(player))
            return;

        HookRenderCallbacks();
        hideCleared = hideWasOn && !FirstPersonBody.HideActive;

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

        bool alive = player.isPlayerControlled && !player.isPlayerDead;
        bool hideForSeat = alive && (HideBodyForVehicle(player) || IsCouch(player));
        bool spawning = alive && IsSpawning(state, player);
        bool showBody = alive && !hideForSeat && !spawning;
        LocalBodyShown = showBody;
        bool seated = IsSeated(player);

        GrabbableObject? held = player.currentlyHeldObjectServer;
        bool holding = Players.IsActivelyHolding(player);
        GrabbableObject? inHand = holding ? held : null;

        bool holdingArms = false;
        try
        {
            bool heldVanilla = inHand != null && ItemHands.UseVanillaArms(player, inHand);
            bool phoneActive = LethalPhonesCompat.IsLocalPhoneActive(state, player);
            bool special = player.inSpecialInteractAnimation && !player.inShockingMinigame;
            GrabbableObject? grabbing = player.isGrabbingObjectAnimation ? player.currentlyGrabbingObject : null;
            bool grabbingVanilla = grabbing != null
                && !grabbing.isHeld
                && ItemHands.UseVanillaArms(player, grabbing);
            holdingArms = !special && (phoneActive || grabbingVanilla || heldVanilla);
        }
        catch (Exception ex)
        {
            LogStageError("Holding arms", ex);
        }

        bool movementArms = false;
        try
        {
            if (showBody
                && !holding
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

        if (holdingArms)
            state.VanillaArmsUntil = Time.time + Constants.ArmSwap.ReleaseDelay;
        else if (holding)
            state.VanillaArmsUntil = 0f;

        if (seated)
            state.SeatArmsUntil = Time.time + Constants.ArmSwap.ReleaseDelay;
        else if (player.inSpecialInteractAnimation)
            state.SeatArmsUntil = 0f;

        bool releaseArms = !holding
            && !player.inSpecialInteractAnimation
            && Time.time < state.VanillaArmsUntil;

        bool seatArms = seated
            || (!player.inSpecialInteractAnimation && Time.time < state.SeatArmsUntil);

        bool useVanillaArms = showBody && (holdingArms || releaseArms || movementArms || seatArms);

        try
        {
            FirstPersonBody.ApplyBodyRendering(state, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("Body rendering", ex);
        }

        try
        {
            HeldItemView.ApplyHolder(state, player, inHand, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("Held item", ex);
        }

        try
        {
            HeldItemCopy.Track(state, player, inHand, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("Held item copy", ex);
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
            if (showBody)
                VisorGlass.Apply(state.LocalVisor);
            else
                VisorGlass.Restore();
        }
        catch (Exception ex)
        {
            LogStageError("Visor glass", ex);
        }

        try
        {
            VisorShadow.Track(state.LocalVisor, state.GameplayCamera, showBody);
        }
        catch (Exception ex)
        {
            LogStageError("Visor shadow", ex);
        }

        try
        {
            SwingReturn.Apply(state, player);
        }
        catch (Exception ex)
        {
            LogStageError("Swing return", ex);
        }

        try
        {
            SwingTorsoSmoothing.Apply(state, player);
        }
        catch (Exception ex)
        {
            LogStageError("Swing torso", ex);
        }

        try
        {
            if (showBody)
            {
                if (!DeferEmoteCamera(state, player, useVanillaArms))
                    CameraRig.ApplyOffset(state, player);
            }
            else
            {
                CameraRig.RestoreOffset(state);
            }
        }
        catch (Exception ex)
        {
            LogStageError("Camera offset", ex);
        }

        SaveCamera(state);

        try
        {
            FirstPersonBody.UpdateCap();
        }
        catch (Exception ex)
        {
            LogStageError("Neck cap", ex);
        }

        try
        {
            if (showBody)
                VisorRig.Follow(state, player);
        }
        catch (Exception ex)
        {
            LogStageError("Visor follow", ex);
        }

        try
        {
            if (state.Emote.Frame != Time.frameCount)
                CameraRig.ApplyArmAnchor(player, useVanillaArms, seatArms);

            CameraRig.ClearStuckClamp(player);
        }
        catch (Exception ex)
        {
            LogStageError("Arm anchor", ex);
        }

        try
        {
            MoreCompanyCompat.ApplyLocalCosmeticVisibility(
                state, player, showBody, useVanillaArms, hideForSeat || spawning);
        }
        catch (Exception ex)
        {
            LogStageError("MoreCompany cosmetics", ex);
        }

        try
        {
            RefreshCosmeticBodies(state, showBody, useVanillaArms);
        }
        catch (Exception ex)
        {
            LogStageError("Cosmetic body", ex);
        }

        try
        {
            if (state.Emote.Frame != Time.frameCount)
                ApplyBack(state, player);
        }
        catch (Exception ex)
        {
            LogStageError("Body back", ex);
        }

        hideWasOn = FirstPersonBody.HideActive;
    }

    private static void ApplyBack(LocalBodyState state, PlayerControllerB player)
    {
        ShiftState shift = state.Shift;
        bool active = LocalBodyShown
            && FirstPersonBody.HideActive
            && !hideCleared
            && !player.inSpecialInteractAnimation
            && !player.inVehicleAnimation
            && shift.BackFrame == Time.frameCount;

        if (active)
            BodyBack.Apply(state.HipsBone, state.GameplayCamera, shift.BackDir, shift.BackAmount);
        else
            BodyBack.Restore();
    }

    public static void TickEmote(PlayerControllerB? player)
    {
        if (player == null || !States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return;

        EmoteState emote = state.Emote;
        if (emote.Frame != Time.frameCount)
            return;

        try
        {
            CameraRig.ApplyOffset(state, player);
            emote.Frame = -1;
            SaveCamera(state);
            VisorRig.Follow(state, player);

            if (!TooManyEmotesCompat.IsFirstPersonEmoteActive())
            {
                Vector3 framed = player.localArmsMatchCamera
                    ? emote.Container
                    : player.cameraContainerTransform.position;
                CameraRig.ApplyArmAnchor(player, emote.VanillaArms, framed);
            }

            ApplyBack(state, player);
        }
        catch (Exception ex)
        {
            LogStageError("Emote camera", ex);
        }
    }

    public static void OnEmoteStopped(PlayerControllerB? player)
    {
        if (player == null || !LocalBodyShown || !TooManyEmotesCompat.IsEnabled
            || !States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return;

        EmoteState emote = state.Emote;
        int age = Time.frameCount - emote.TickFrame;
        if (age < 0 || age > 1)
            return;

        try
        {
            Camera? camera = state.GameplayCamera;
            if (camera != null)
                camera.transform.localRotation = emote.CameraRotation;

            if (age != 0)
                return;

            bool vanillaArms = FirstPersonBody.VanillaArmsShown;
            FirstPersonBody.ApplyBodyRendering(state, true, vanillaArms);
            MoreCompanyCompat.ApplyLocalCosmeticVisibility(state, player, true, vanillaArms, false);

            GrabbableObject? held = Players.IsActivelyHolding(player) ? player.currentlyHeldObjectServer : null;
            if (held != null)
                HeldItemView.SetHolder(player, held, vanillaArms, true);

            if (camera != null && emote.Frame != Time.frameCount && state.CameraOffsetApplied)
                camera.transform.position = emote.CameraPosition;
        }
        catch (Exception ex)
        {
            LogStageError("Emote stop", ex);
        }
    }

    private static void SaveCamera(LocalBodyState state)
    {
        Camera? camera = state.GameplayCamera;
        if (camera == null)
            return;

        EmoteState emote = state.Emote;
        emote.TickFrame = Time.frameCount;
        emote.CameraPosition = camera.transform.position;
        emote.CameraRotation = camera.transform.localRotation;
    }

    private static bool DeferEmoteCamera(LocalBodyState state, PlayerControllerB player, bool useVanillaArms)
    {
        EmoteState emote = state.Emote;
        if (emote.Frame == Time.frameCount - 1)
            emote.Fallback = true;

        bool active = TooManyEmotesCompat.IsFirstPersonEmoteActive();
        if (!active)
            emote.Fallback = false;

        bool defer = active && TooManyEmotesCompat.IsHooked && !emote.Fallback;
        emote.Frame = defer ? Time.frameCount : -1;
        if (defer)
        {
            emote.VanillaArms = useVanillaArms;
            CameraRig.SaveEmoteBase(state, player);
        }

        return defer;
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

        CameraRig.AlignForInteractionRay(state);
    }

    public static float GetRayBack(PlayerControllerB player)
    {
        if (!Players.IsLocal(player))
            return 0f;
        if (!States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return 0f;

        return CameraRig.GetRayBack(state);
    }

    public static void OnLookInput(PlayerControllerB player)
    {
        if (!Players.IsLocal(player) || !ConfigManager.EnableTooManyEmotesCompatibility.Value)
            return;
        if (!States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return;

        Transform? container = player.cameraContainerTransform;
        if (container == null)
            return;

        EmoteState emote = state.Emote;
        emote.Source = GetPinned(player, container.position);
        emote.SourceFrame = Time.frameCount;
    }

    public static void OnLookApplied(PlayerControllerB player)
    {
        if (!Players.IsLocal(player) || !States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
            return;

        Camera? camera = state.GameplayCamera;
        Transform? container = player.cameraContainerTransform;
        if (camera == null || container == null || TooManyEmotesCompat.IsFirstPersonEmoteActive())
            return;

        state.LookPitch = CameraRig.GetPitch(container, camera.transform);
        state.LookFrame = Time.frameCount;
    }

    private static Vector3 GetPinned(PlayerControllerB player, Vector3 position)
    {
        IngamePlayerSettings? settings = IngamePlayerSettings.Instance;
        Transform? arms = player.playerModelArmsMetarig;
        if (settings == null || arms == null || settings.settings.headBobbing || player.inSpecialInteractAnimation)
            return position;

        position.y = arms.position.y;
        return position;
    }

    private static readonly List<SkinnedMeshRenderer> _riggedBodyScratch = new();

    private static readonly List<CosmeticBodyView.RiggedBodyCandidate> _candidateScratch = new();

    private static void RefreshCosmeticBodies(LocalBodyState state, bool showBody, bool useVanillaArms)
    {
        if (!showBody || !state.HeadHidden || state.HeadBone == null)
        {
            if (CosmeticBodyView.Active)
                CosmeticBodyView.RestoreAll();
            state.CosmeticBodyNextScan = 0f;
            return;
        }

        CosmeticBodyView.SetFrameState(state.GameplayCamera, useVanillaArms);

        if (Time.unscaledTime < state.CosmeticBodyNextScan)
            return;

        state.CosmeticBodyNextScan = Time.unscaledTime + Constants.Body.CosmeticScanInterval;

        _candidateScratch.Clear();

        MoreCompanyCompat.CollectRiggedBodies(state.HeadBone, _riggedBodyScratch);
        foreach (SkinnedMeshRenderer renderer in _riggedBodyScratch)
        {
            _candidateScratch.Add(new CosmeticBodyView.RiggedBodyCandidate
            {
                Renderer = renderer,
                HeadBone = state.HeadBone,
                LeftArmBone = state.LeftArmBone,
                RightArmBone = state.RightArmBone,
            });
        }

        CosmeticBodyView.Rebuild(_candidateScratch);
    }

    public static void OnSpawnAnimation(PlayerControllerB player)
    {
        if (Players.IsLocal(player))
            GetOrCreateState(player).SpawnUntil = Time.time + Constants.Spawn.Timeout;
    }

    private static bool IsSpawning(LocalBodyState state, PlayerControllerB player)
    {
        if (Time.time >= state.SpawnUntil)
            return false;

        if (player.inSpecialInteractAnimation)
            return true;

        state.SpawnUntil = 0f;
        return false;
    }

    private static bool HideBodyForVehicle(PlayerControllerB player)
    {
        return ConfigManager.HideBodyInCruiser.Value
            && player.inVehicleAnimation
            && IsVehicleSeat(player.currentTriggerInAnimationWith);
    }

    private static string? GetSeatName(PlayerControllerB player)
    {
        if (!player.inSpecialInteractAnimation)
            return null;

        InteractTrigger? trigger = player.currentTriggerInAnimationWith;
        return trigger != null ? trigger.animationString : null;
    }

    private static bool IsCouch(PlayerControllerB player) => GetSeatName(player) == Constants.Seat.Couch;

    private static bool IsSeated(PlayerControllerB player)
    {
        if (ConfigManager.Hands.Value == HandsMode.ThirdPerson)
            return false;

        string? name = GetSeatName(player);
        return name == Constants.Seat.Couch || name == Constants.Seat.Chair;
    }

    private static bool IsVehicleSeat(InteractTrigger? trigger)
    {
        if (trigger == null)
            return false;

        if (!ReferenceEquals(trigger, seatTrigger))
        {
            seatTrigger = trigger;
            Transform? parent = trigger.overridePlayerParent;
            seatVehicle = parent != null && parent.GetComponentInParent<VehicleController>() != null;
        }

        return seatVehicle;
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

        Transform? scavengerModel = player.transform.Find(Constants.Rig.ScavengerModel);
        state.ModelRoot = scavengerModel != null ? scavengerModel : player.transform;

        state.HeadBone = FindHeadBone(state.ModelRoot, player);

        state.LeftArmBone = FindBodyBone(player, Constants.Rig.LeftArmBone);
        state.RightArmBone = FindBodyBone(player, Constants.Rig.RightArmBone);
        state.HipsBone = FindBodyBone(player, Constants.Rig.HipsBone);

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
                if (bone != null && NameContains(bone.name, Constants.Rig.HeadBone))
                    return bone;
            }
        }

        return FindFirstByNameContains(modelRoot, Constants.Rig.HeadBone)
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

    private static void HookRenderCallbacks()
    {
        if (renderCallbacksHooked)
            return;

        renderCallbacksHooked = true;
        RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        RenderPipelineManager.endContextRendering += OnEndContextRendering;
    }

    private static readonly (Action<Camera> Apply, Action<Camera> Restore)[] CameraPasses =
    {
        (FirstPersonBody.ApplyForCamera, FirstPersonBody.RestoreAfterCamera),
        (HeldItemView.ApplyForCamera, HeldItemView.RestoreAfterCamera),
        (MoreCompanyCompat.ApplyForCamera, MoreCompanyCompat.RestoreAfterCamera),
        (CosmeticBodyView.ApplyForCamera, CosmeticBodyView.RestoreAfterCamera),
        (VisorShadow.ApplyForCamera, VisorShadow.RestoreAfterCamera),
    };

    private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        try
        {
            PickupPose.Apply();
        }
        catch (Exception ex)
        {
            LogStageError("Pickup", ex);
        }

        try
        {
            HeldItemCopy.Place();
        }
        catch (Exception ex)
        {
            LogStageError("Held item copy", ex);
        }

        try
        {
            HeldItemCopy.BeginFrame();
        }
        catch (Exception ex)
        {
            LogStageError("Held item copy", ex);
        }
    }

    private static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        try
        {
            BodyBack.RestoreAfter(cameras);
        }
        catch (Exception ex)
        {
            LogStageError("Body back", ex);
        }

        try
        {
            HeldItemCopy.EndFrame();
        }
        catch (Exception ex)
        {
            LogStageError("Held item copy", ex);
        }
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (var pass in CameraPasses)
        {
            try
            {
                pass.Apply(camera);
            }
            catch (Exception ex)
            {
                LogStageError("Camera pass", ex);
            }
        }

        try
        {
            HeldItemCopy.ApplyForCamera(camera);
        }
        catch (Exception ex)
        {
            LogStageError("Held item copy", ex);
        }
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        foreach (var pass in CameraPasses)
        {
            try
            {
                pass.Restore(camera);
            }
            catch (Exception ex)
            {
                LogStageError("Camera pass", ex);
            }
        }
    }
}
