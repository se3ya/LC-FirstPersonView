using GameNetcodeStuff;
using UnityEngine;

using FirstPersonView.Compat;

namespace FirstPersonView;

internal static class CameraRig
{
    public static void ApplyOffset(LocalBodyState state, PlayerControllerB player)
    {
        Camera? camera = state.GameplayCamera;
        if (camera == null || !state.CameraBaseCaptured)
            return;

        state.VehiclePushApplied = false;

        Transform camTransform = camera.transform;
        Transform? parent = camTransform.parent;

        Vector3 up = parent != null ? parent.up : Vector3.up;
        Vector3 flatForward = Vector3.ProjectOnPlane(camTransform.forward, up);
        if (flatForward.sqrMagnitude < 1e-5f)
            flatForward = Vector3.ProjectOnPlane(player.transform.forward, up);

        flatForward = flatForward.sqrMagnitude < 1e-5f
            ? (parent != null ? parent.forward : Vector3.forward)
            : flatForward.normalized;
        Vector3 flatRight = Vector3.Cross(up, flatForward);
        Quaternion yawRotation = Quaternion.LookRotation(flatForward, up);

        CameraBlends blends = state.Blends;

        blends.Crouch = Mathf.MoveTowards(
            blends.Crouch, player.isCrouching ? 1f : 0f, Time.deltaTime / Constants.CrouchBlendTime);

        blends.ShovelSwing = Mathf.MoveTowards(
            blends.ShovelSwing, Players.IsSwingingShovelLike(player) ? 1f : 0f, Time.deltaTime / Constants.ShovelSwingBlendTime);

        blends.Run = Mathf.MoveTowards(
            blends.Run, player.isSprinting ? 1f : 0f, Time.deltaTime / Constants.RunningBlendTime);

        blends.Walk = Mathf.MoveTowards(
            blends.Walk, (player.isWalking && !player.isSprinting) ? 1f : 0f, Time.deltaTime / Constants.WalkingBlendTime);

        blends.Jump = Mathf.MoveTowards(
            blends.Jump, (player.isJumping || player.isFallingFromJump) ? 1f : 0f, Time.deltaTime / Constants.JumpingBlendTime);

        float lookDown = Vector3.Dot(camTransform.forward, -up);
        float lookDownRamp = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(Constants.LookDownForwardStart, Constants.LookDownForwardFull, lookDown));

        float lookDownEyeOffsetForward = player.inVehicleAnimation
            ? Constants.LookDownCruiserEyeOffsetForward
            : Constants.LookDownEyeOffsetForward;

        float statePush = Mathf.Max(blends.ShovelSwing * Constants.LookDownShovelSwingEyeOffsetForward,
            Mathf.Max(blends.Run * Constants.LookDownRunningEyeOffsetForward,
                Mathf.Max(blends.Walk * Constants.LookDownWalkingEyeOffsetForward,
                    blends.Jump * Constants.LookDownJumpingEyeOffsetForward)));

        float forwardOffset = lookDownRamp * (lookDownEyeOffsetForward + statePush);

        Vector3 baseWorld = parent != null
            ? parent.TransformPoint(state.CameraBaseLocalPosition)
            : state.CameraBaseLocalPosition;

        Vector3 anchorWorld = FollowBodyAnchor(state, player, baseWorld, yawRotation, lookDown);

        Vector3 toAnchor = anchorWorld - baseWorld;
        float followForward = Vector3.Dot(toAnchor, flatForward);
        float totalForward = followForward + forwardOffset;
        float totalRight = Vector3.Dot(toAnchor, flatRight);
        float totalUp = Vector3.Dot(toAnchor, up);

        float lookDownForwardFloor = Mathf.Max(forwardOffset, Constants.EyeForwardFloor);
        if (!player.inSpecialInteractAnimation)
            totalForward = Mathf.Max(totalForward, lookDownForwardFloor);

        totalForward = ClampTravelToWall(
            baseWorld, flatForward, totalForward, Constants.WallCollisionForwardProbeRadius);

        Vector3 horizontalPoint = baseWorld + (flatForward * totalForward) + (flatRight * totalRight);
        Vector3 target = ResolveVerticalClearance(horizontalPoint, up, totalUp);

        camTransform.position = target;
        state.LastCameraTargetLocal = parent != null ? parent.InverseTransformPoint(target) : target;
        state.HasCameraTarget = true;
        state.CameraOffsetApplied = true;
    }

    public static void PrepareVehicleDetach(LocalBodyState state)
    {
        state.CameraOffsetApplied = false;
        state.HasCameraTarget = false;
        state.NeckGuard.Initialized = false;
    }

    public static void ApplyVehiclePush(LocalBodyState state)
    {
        Camera? camera = state.GameplayCamera;
        if (camera == null)
            return;

        Transform camT = camera.transform;
        Transform? parent = camT.parent;
        Vector3 up = parent != null ? parent.up : Vector3.up;

        float lookDown = Vector3.Dot(camT.forward, -up);
        float lookDownRamp = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(Constants.LookDownForwardStart, Constants.LookDownForwardFull, lookDown));

        Vector3 push = new Vector3(0f, 0f, lookDownRamp * Constants.LookDownCruiserImprovedEyeOffsetForward);

        Vector3 current = camT.localPosition;
        bool previousPushIntact = state.VehiclePushApplied
            && (current - state.VehiclePushResult).sqrMagnitude < Constants.VehiclePushMatchEpsilonSqr;

        Vector3 seatBase = previousPushIntact ? current - state.VehiclePush : current;
        Vector3 result = seatBase + push;

        camT.localPosition = result;
        state.VehiclePush = push;
        state.VehiclePushResult = result;
        state.VehiclePushApplied = true;
    }

    public static void RelaxSeatedLookClamp(PlayerControllerB player)
    {
        if (!player.isPlayerControlled || player.isPlayerDead)
            return;

        if (player.clampLooking && !player.inSpecialInteractAnimation)
        {
            player.clampLooking = false;
            player.inVehicleAnimation = false;
            return;
        }

        if (!player.clampLooking)
            return;

        if (player.minVerticalClamp < Constants.SeatedLookDownClamp)
            player.minVerticalClamp = Constants.SeatedLookDownClamp;
    }

    public static void AlignForInteractionRay(LocalBodyState state)
    {
        if (!state.HasCameraTarget || state.GameplayCamera == null)
            return;

        Transform camTransform = state.GameplayCamera.transform;
        Transform? parent = camTransform.parent;
        camTransform.position = parent != null
            ? parent.TransformPoint(state.LastCameraTargetLocal)
            : state.LastCameraTargetLocal;
    }

    public static void ApplyArmAnchor(PlayerControllerB player, bool useVanillaArms)
    {
        if (!useVanillaArms || player.inSpecialInteractAnimation)
            return;

        Transform? arms = player.localArmsTransform;
        Camera? camera = player.gameplayCamera;
        Transform? container = player.cameraContainerTransform;
        if (arms == null || camera == null || container == null)
            return;

        Transform cam = camera.transform;
        arms.position += cam.position - container.position;

        float forward = ConfigManager.HandOffsetZ.Value;
        if (forward != 0f)
            arms.position += cam.forward * forward;
    }

    private static Vector3 FollowBodyAnchor(
        LocalBodyState state, PlayerControllerB player, Vector3 baseWorld, Quaternion yawRotation,
        float lookDown)
    {
        Transform? bone = state.HeadBone;
        if (bone == null)
            return baseWorld;

        EyeAnchorState anchor = state.EyeAnchor;

        if (!anchor.Captured && !player.isCrouching)
        {
            anchor.Local = Quaternion.Inverse(yawRotation) * (baseWorld - bone.position);
            anchor.BoneLocal = bone.InverseTransformPoint(baseWorld);
            anchor.Drift = Vector3.zero;
            anchor.Captured = true;
        }

        if (!anchor.Captured)
            return baseWorld;


        if (TooManyEmotesCompat.IsFirstPersonEmoteActive())
            return bone.TransformPoint(anchor.BoneLocal);

        CameraBlends blends = state.Blends;
        bool stickToHead = ConfigManager.StickCameraToHeadInAnimations.Value
            && IsInteractTriggerAnimation(player);
        blends.StickToHead = Mathf.MoveTowards(
            blends.StickToHead, stickToHead ? 1f : 0f, Time.deltaTime / Constants.StickToHeadBlendTime);

        Vector3 fullFollow = bone.position + (yawRotation * anchor.Local);
        Vector3 rawDeviation = Quaternion.Inverse(yawRotation) * (fullFollow - baseWorld);

        if (IsEyeAnchorSettled(state, player, lookDown))
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.EyeAnchorDriftTau);
            anchor.Drift = Vector3.Lerp(anchor.Drift, rawDeviation, t);

            float limit = Constants.EyeAnchorDriftLimit;
            if (anchor.Drift.sqrMagnitude > limit * limit)
                anchor.Drift = anchor.Drift.normalized * limit;
        }

        WarnOnPersistentDrift(anchor);

        Vector3 deviationLocal = rawDeviation - anchor.Drift;

        deviationLocal.x *= Constants.FollowStrengthHorizontal;
        deviationLocal.z *= Constants.FollowStrengthHorizontal;
        deviationLocal.y *= Constants.FollowStrengthVertical;
        deviationLocal.y = DampHeadBob(state, player, deviationLocal.y);

        deviationLocal = StabilizeSprintBob(state, player, deviationLocal);
        deviationLocal = StabilizeLadder(state, player, deviationLocal);
        deviationLocal = NeckGuardedFollow(state, player, deviationLocal);

        if (deviationLocal.sqrMagnitude > Constants.MaxFollowOffset * Constants.MaxFollowOffset)
            deviationLocal = deviationLocal.normalized * Constants.MaxFollowOffset;

        Vector3 followWorld = baseWorld + (yawRotation * deviationLocal);

        if (blends.StickToHead > 0f)
            followWorld = Vector3.Lerp(followWorld, bone.TransformPoint(anchor.BoneLocal), blends.StickToHead);

        return followWorld;
    }

    private static bool IsInteractTriggerAnimation(PlayerControllerB player)
    {
        InteractTrigger? trigger = player.currentTriggerInAnimationWith;

        return player.inSpecialInteractAnimation
            && trigger != null
            && !trigger.isLadder
            && !player.isClimbingLadder
            && !player.inVehicleAnimation
            && !player.inShockingMinigame;
    }


    private static void WarnOnPersistentDrift(EyeAnchorState anchor)
    {
        if (anchor.DriftWarned)
            return;

        float threshold = Constants.EyeAnchorDriftWarnThreshold;
        if (anchor.Drift.sqrMagnitude < threshold * threshold)
        {
            anchor.DriftHighSince = 0f;
            return;
        }

        if (anchor.DriftHighSince <= 0f)
        {
            anchor.DriftHighSince = Time.unscaledTime;
            return;
        }

        if (Time.unscaledTime - anchor.DriftHighSince < Constants.EyeAnchorDriftWarnDelay)
            return;

        anchor.DriftWarned = true;
        Plugin.Log.LogWarning(
            $"Eye anchor drift held at {anchor.Drift.magnitude:F3}m for over {Constants.EyeAnchorDriftWarnDelay:F0}s. "
            + "The head bone rest pose does not match the captured anchor, the forward eye offset may be reduced.");
    }

    private static bool IsEyeAnchorSettled(LocalBodyState state, PlayerControllerB player, float lookDown)
    {
        return Mathf.Abs(lookDown) <= Constants.EyeAnchorSettledLookBand
            && !player.isWalking
            && !player.isSprinting
            && !player.inSpecialInteractAnimation
            && !player.isCrouching
            && state.Blends.Crouch <= 0f
            && !player.isClimbingLadder
            && !player.isJumping
            && !player.isFallingFromJump
            && !state.NeckGuard.Engaged;
    }

    private static float DampHeadBob(LocalBodyState state, PlayerControllerB player, float rawY)
    {
        BobSmoothing smooth = state.BobSmooth;
        CameraBlends blends = state.Blends;

        bool allowed = !player.isCrouching
            && blends.Crouch <= 0f
            && ConfigManager.DisableHeadBob.Value;

        if (!smooth.YInitialized)
        {
            smooth.SmoothedY = rawY;
            smooth.YInitialized = true;
        }

        bool converged = Mathf.Abs(rawY - smooth.SmoothedY) <= Constants.DisableHeadBobEngageBand;
        bool removeBob = allowed && (blends.DisableBob > 0f || converged);

        if (removeBob && blends.DisableBob <= 0f)
            smooth.SmoothedY = rawY;

        float tau = removeBob ? Constants.DisableHeadBobTau : Constants.DisableHeadBobBlendTime;
        float t = 1f - Mathf.Exp(-Time.deltaTime / tau);
        smooth.SmoothedY = Mathf.Lerp(smooth.SmoothedY, rawY, t);

        blends.DisableBob = Mathf.MoveTowards(
            blends.DisableBob, removeBob ? 1f : 0f, Time.deltaTime / Constants.DisableHeadBobBlendTime);

        return Mathf.Lerp(rawY, smooth.SmoothedY, blends.DisableBob);
    }

    private static Vector3 StabilizeSprintBob(
        LocalBodyState state, PlayerControllerB player, Vector3 deviationLocal)
    {
        BobSmoothing smooth = state.BobSmooth;
        CameraBlends blends = state.Blends;

        bool active = player.isSprinting || player.isJumping || player.isFallingFromJump;
        blends.BobDamp = Mathf.MoveTowards(
            blends.BobDamp, active ? 1f : 0f, Time.deltaTime / Constants.BobDampBlendTime);

        if (!smooth.DeviationInitialized)
        {
            smooth.SmoothedDeviationLocal = deviationLocal;
            smooth.DeviationInitialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.BobStabilizeTau);
            smooth.SmoothedDeviationLocal = Vector3.Lerp(smooth.SmoothedDeviationLocal, deviationLocal, t);
        }

        float damp = blends.BobDamp * Constants.SprintBobReduction;
        return Vector3.Lerp(deviationLocal, smooth.SmoothedDeviationLocal, damp);
    }

    private static bool IsLadderSequence(PlayerControllerB player)
    {
        if (player.isClimbingLadder)
            return true;

        InteractTrigger? trigger = player.currentTriggerInAnimationWith;

        return player.inSpecialInteractAnimation && trigger != null && trigger.isLadder;
    }

    private static Vector3 StabilizeLadder(
        LocalBodyState state, PlayerControllerB player, Vector3 deviationLocal)
    {
        BobSmoothing smooth = state.BobSmooth;
        CameraBlends blends = state.Blends;

        bool onLadder = IsLadderSequence(player);
        float blendTime = onLadder ? Constants.LadderBlendTime : Constants.LadderReleaseTime;
        blends.Ladder = Mathf.MoveTowards(
            blends.Ladder, onLadder ? 1f : 0f, Time.deltaTime / blendTime);

        if (!smooth.LadderInitialized)
        {
            smooth.SmoothedLadderLocal = deviationLocal;
            smooth.LadderInitialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.LadderStabilizeTau);
            smooth.SmoothedLadderLocal = Vector3.Lerp(smooth.SmoothedLadderLocal, deviationLocal, t);
        }

        return Vector3.Lerp(deviationLocal, smooth.SmoothedLadderLocal, blends.Ladder);
    }

    private static Vector3 NeckGuardedFollow(LocalBodyState state, PlayerControllerB player, Vector3 live)
    {
        NeckGuardState guard = state.NeckGuard;

        if (!guard.Initialized)
        {
            guard.SwingRest = live;
            guard.EyeDeviation = live;
            guard.EyeVelocity = Vector3.zero;
            guard.Tail = 0f;
            guard.Release = 0f;
            guard.Latched = false;
            guard.WasSwinging = false;
            guard.Engaged = false;
            guard.FloorActive = false;
            guard.CrouchRestZ = live.z;
            guard.Initialized = true;
            return live;
        }

        bool swinging = player.isCrouching && player.activatingItem;

        if (swinging && !guard.WasSwinging)
            guard.Latched = state.Blends.Crouch >= Constants.NeckGuardCrouchSettled;
        guard.WasSwinging = swinging;

        if (swinging)
            guard.Tail = Constants.NeckGuardSwingTail;
        else if (guard.Tail > 0f)
            guard.Tail -= Time.deltaTime;

        bool swingWindow = swinging || guard.Tail > 0f;
        bool locked = guard.Latched && swingWindow;

        if (player.isCrouching && !swingWindow)
        {
            guard.CrouchRestZ = live.z;
        }

        bool crouchGuard = swingWindow && !guard.Latched;
        if (crouchGuard && !guard.FloorActive)
        {
            guard.FloorZ = Mathf.Max(guard.CrouchRestZ, Constants.NeckGuardCrouchFloor);
            guard.CrouchRestY = live.y;
            guard.FloorActive = true;
        }
        else if (!crouchGuard)
            guard.FloorActive = false;

        if (guard.Engaged && !locked && !crouchGuard)
            guard.Release = Constants.NeckGuardReleaseRamp;
        else if (guard.Release > 0f)
            guard.Release -= Time.deltaTime;
        guard.Engaged = locked || crouchGuard;

        if (!locked)
            guard.SwingRest = live;
        Vector3 rest = guard.SwingRest;

        float stance = player.isCrouching ? 1f : state.Blends.Crouch;

        Vector3 target = live;
        if (locked)
        {
            target.z = Mathf.Max(live.z, Mathf.Lerp(live.z, rest.z, stance));
            if (swinging)
                target.y = live.y >= rest.y
                    ? rest.y + ((live.y - rest.y) * Constants.NeckGuardUpFollow)
                    : rest.y + ((live.y - rest.y) * Constants.NeckGuardDownFollow);
            else
                target.y = Mathf.Lerp(live.y, Mathf.Min(live.y, rest.y), stance);
        }
        else if (guard.FloorActive)
        {
            target.z = Mathf.Max(live.z, Mathf.Lerp(live.z, guard.FloorZ, stance));
            target.y = Mathf.Lerp(live.y, Mathf.Min(live.y, guard.CrouchRestY), stance);
        }

        bool swingSmoothing = locked || (swingWindow && player.isCrouching);
        float tau;
        if (swingSmoothing)
            tau = Constants.NeckGuardSwingTau;
        else if (guard.Release > 0f)
            tau = Mathf.Lerp(
                Constants.NeckGuardCalmTau, Constants.NeckGuardSwingTau,
                guard.Release / Constants.NeckGuardReleaseRamp);
        else
            tau = Constants.NeckGuardCalmTau;

        float tauY = swingWindow ? Constants.NeckGuardSwingTau : tau;

        float vx = guard.EyeVelocity.x;
        float vy = guard.EyeVelocity.y;
        float vz = guard.EyeVelocity.z;
        guard.EyeDeviation = new Vector3(
            Mathf.SmoothDamp(guard.EyeDeviation.x, target.x, ref vx, tau),
            Mathf.SmoothDamp(guard.EyeDeviation.y, target.y, ref vy, tauY),
            Mathf.SmoothDamp(guard.EyeDeviation.z, target.z, ref vz, tau));
        guard.EyeVelocity = new Vector3(vx, vy, vz);

        return guard.EyeDeviation;
    }

    private static float ClampTravelToWall(Vector3 origin, Vector3 dir, float distance, float radius)
    {
        StartOfRound? sor = StartOfRound.Instance;
        if (sor == null || distance <= 0f)
            return distance;

        if (Physics.SphereCast(
                origin, Mathf.Max(0.01f, radius), dir, out RaycastHit hit, distance,
                sor.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(0f, hit.distance);
        }

        return distance;
    }

    private static Vector3 ResolveVerticalClearance(Vector3 horizontalPoint, Vector3 up, float totalUp)
    {
        Vector3 dir = totalUp >= 0f ? up : -up;
        float travel = ClampTravelToWall(horizontalPoint, dir, Mathf.Abs(totalUp), Constants.WallCollisionRadius);
        return horizontalPoint + (dir * travel);
    }

    public static void RestoreOffset(LocalBodyState state)
    {
        state.Blends.Reset();
        state.BobSmooth.Reset();
        state.NeckGuard.Reset();
        state.HasCameraTarget = false;
        state.VehiclePushApplied = false;

        if (!state.CameraOffsetApplied)
            return;

        Camera? camera = state.GameplayCamera;
        if (camera != null && state.CameraBaseCaptured)
            camera.transform.localPosition = state.CameraBaseLocalPosition;

        state.CameraOffsetApplied = false;
    }
}
