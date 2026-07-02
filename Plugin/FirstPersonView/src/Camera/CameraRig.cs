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

        state.CrouchBlend = Mathf.MoveTowards(
            state.CrouchBlend, player.isCrouching ? 1f : 0f, Time.deltaTime / Constants.CrouchBlendTime);

        state.HoldBlend = Mathf.MoveTowards(
            state.HoldBlend, Players.IsHoldingTwoHanded(player) ? 1f : 0f, Time.deltaTime / Constants.HoldingBlendTime);

        state.RunBlend = Mathf.MoveTowards(
            state.RunBlend, player.isSprinting ? 1f : 0f, Time.deltaTime / Constants.RunningBlendTime);

        state.JumpBlend = Mathf.MoveTowards(
            state.JumpBlend, (player.isJumping || player.isFallingFromJump) ? 1f : 0f, Time.deltaTime / Constants.JumpingBlendTime);

        float upOffset = Constants.EyeOffsetUp
            + (state.CrouchBlend * Constants.CrouchEyeOffsetUp)
            + (state.HoldBlend * Constants.HoldingEyeOffsetUp)
            + (state.RunBlend * Constants.RunningEyeOffsetUp)
            + (state.JumpBlend * Constants.JumpingEyeOffsetUp);

        state.LadderBlend = Mathf.MoveTowards(
            state.LadderBlend, player.isClimbingLadder ? 1f : 0f, Time.deltaTime / Constants.LadderBlendTime);

        float lookDown = Vector3.Dot(camTransform.forward, -up);
        float lookDownForward = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(Constants.LookDownForwardStart, Constants.LookDownForwardFull, lookDown))
            * Constants.LookDownEyeOffsetForward;

        float forwardOffset = Mathf.Lerp(
            Constants.EyeOffsetForward, Constants.EyeOffsetForwardOnLadder, state.LadderBlend)
            + lookDownForward
            + (state.HoldBlend * Constants.HoldingEyeOffsetForward)
            + (state.RunBlend * Constants.RunningEyeOffsetForward)
            + (state.JumpBlend * Constants.JumpingEyeOffsetForward);

        Vector3 baseWorld = parent != null
            ? parent.TransformPoint(state.CameraBaseLocalPosition)
            : state.CameraBaseLocalPosition;

        Vector3 anchorWorld = FollowBodyAnchor(state, player, baseWorld, yawRotation);

        Vector3 toAnchor = anchorWorld - baseWorld;
        float totalForward = Vector3.Dot(toAnchor, flatForward) + forwardOffset;
        float totalRight = Vector3.Dot(toAnchor, flatRight) + Constants.EyeOffsetRight;
        float totalUp = Vector3.Dot(toAnchor, up) + upOffset;

        totalForward = ClampTravelToWall(
            baseWorld, flatForward, totalForward,
            Constants.WallCollisionForwardProbeRadius, Constants.WallCollisionForwardMargin);

        Vector3 horizontalPoint = baseWorld + (flatForward * totalForward) + (flatRight * totalRight);
        Vector3 target = ResolveVerticalClearance(horizontalPoint, up, totalUp);

        camTransform.position = target;
        state.LastCameraTargetLocal = parent != null ? parent.InverseTransformPoint(target) : target;
        state.HasCameraTarget = true;
        state.CameraOffsetApplied = true;
    }

    public static void RelaxSeatedLookClamp(PlayerControllerB player)
    {
        if (!player.inVehicleAnimation || !player.clampLooking)
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
        LocalBodyState state, PlayerControllerB player, Vector3 baseWorld, Quaternion yawRotation)
    {
        Transform? bone = state.HeadBone;
        if (bone == null)
            return baseWorld;

        if (!state.EyeAnchorCaptured)
        {
            state.EyeAnchorLocal = Quaternion.Inverse(yawRotation) * (baseWorld - bone.position);
            state.EyeAnchorBoneLocal = bone.InverseTransformPoint(baseWorld);
            state.EyeAnchorCaptured = true;
        }

        if (TooManyEmotesCompat.IsFirstPersonEmoteActive())
            return bone.TransformPoint(state.EyeAnchorBoneLocal);

        Vector3 fullFollow = bone.position + (yawRotation * state.EyeAnchorLocal);
        Vector3 deviationLocal = Quaternion.Inverse(yawRotation) * (fullFollow - baseWorld);

        deviationLocal.x *= Constants.FollowStrengthHorizontal;
        deviationLocal.z *= Constants.FollowStrengthHorizontal;
        deviationLocal.y *= Constants.FollowStrengthVertical;
        deviationLocal.y = DampHeadBob(state, player, deviationLocal.y);

        deviationLocal = StabilizeSprintBob(state, player, deviationLocal);
        deviationLocal = NeckGuardedFollow(state, player, deviationLocal);

        if (deviationLocal.sqrMagnitude > Constants.MaxFollowOffset * Constants.MaxFollowOffset)
            deviationLocal = deviationLocal.normalized * Constants.MaxFollowOffset;

        return baseWorld + (yawRotation * deviationLocal);
    }

    private static float DampHeadBob(LocalBodyState state, PlayerControllerB player, float rawY)
    {
        if (!state.DisableBobSmoothInitialized)
        {
            state.DisableBobSmoothedY = rawY;
            state.DisableBobSmoothInitialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.DisableHeadBobTau);
            state.DisableBobSmoothedY = Mathf.Lerp(state.DisableBobSmoothedY, rawY, t);
        }

        bool removeBob = ConfigManager.DisableHeadBob.Value
            && !player.isCrouching
            && state.CrouchBlend <= 0f
            && (player.isWalking || player.isSprinting || player.isJumping || player.isFallingFromJump || player.isClimbingLadder);
        state.DisableBobBlend = Mathf.MoveTowards(
            state.DisableBobBlend, removeBob ? 1f : 0f, Time.deltaTime / Constants.DisableHeadBobBlendTime);

        return Mathf.Lerp(rawY, state.DisableBobSmoothedY, state.DisableBobBlend);
    }

    private static Vector3 StabilizeSprintBob(
        LocalBodyState state, PlayerControllerB player, Vector3 deviationLocal)
    {
        bool active = player.isSprinting || player.isJumping || player.isFallingFromJump;
        state.BobDampBlend = Mathf.MoveTowards(
            state.BobDampBlend, active ? 1f : 0f, Time.deltaTime / Constants.BobDampBlendTime);

        if (!state.DeviationSmoothInitialized)
        {
            state.SmoothedDeviationLocal = deviationLocal;
            state.DeviationSmoothInitialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.BobStabilizeTau);
            state.SmoothedDeviationLocal = Vector3.Lerp(state.SmoothedDeviationLocal, deviationLocal, t);
        }

        float damp = state.BobDampBlend * Constants.SprintBobReduction;
        return Vector3.Lerp(deviationLocal, state.SmoothedDeviationLocal, damp);
    }

    private static Vector3 NeckGuardedFollow(LocalBodyState state, PlayerControllerB player, Vector3 live)
    {
        if (!state.NeckGuardInitialized)
        {
            state.SwingRest = live;
            state.GuardedEyeDeviation = live;
            state.GuardedEyeVelocity = Vector3.zero;
            state.NeckGuardTail = 0f;
            state.NeckGuardRelease = 0f;
            state.NeckGuardLatched = false;
            state.NeckGuardWasSwinging = false;
            state.NeckGuardEngaged = false;
            state.NeckGuardFloorActive = false;
            state.NeckGuardCrouchRestZ = live.z;
            state.NeckGuardCrouchRestY = live.y;
            state.NeckGuardInitialized = true;
            return live;
        }

        bool swinging = player.isCrouching && player.activatingItem;

        if (swinging && !state.NeckGuardWasSwinging)
            state.NeckGuardLatched = state.CrouchBlend >= Constants.NeckGuardCrouchSettled;
        state.NeckGuardWasSwinging = swinging;

        if (swinging)
            state.NeckGuardTail = Constants.NeckGuardSwingTail;
        else if (state.NeckGuardTail > 0f)
            state.NeckGuardTail -= Time.deltaTime;

        bool swingWindow = swinging || state.NeckGuardTail > 0f;
        bool locked = state.NeckGuardLatched && player.isCrouching && swingWindow;

        if (player.isCrouching && !swingWindow)
        {
            state.NeckGuardCrouchRestZ = live.z;
            state.NeckGuardCrouchRestY = live.y;
        }

        bool crouchGuard = swingWindow && player.isCrouching && !state.NeckGuardLatched;
        if (crouchGuard && !state.NeckGuardFloorActive)
        {
            state.NeckGuardFloorZ = Mathf.Max(state.NeckGuardCrouchRestZ, Constants.NeckGuardCrouchFloor);
            state.NeckGuardFloorActive = true;
        }
        else if (!crouchGuard)
            state.NeckGuardFloorActive = false;

        if (state.NeckGuardEngaged && !locked && !crouchGuard)
            state.NeckGuardRelease = Constants.NeckGuardReleaseRamp;
        else if (state.NeckGuardRelease > 0f)
            state.NeckGuardRelease -= Time.deltaTime;
        state.NeckGuardEngaged = locked || crouchGuard;

        if (!locked)
            state.SwingRest = live;
        Vector3 rest = state.SwingRest;

        Vector3 target = live;
        if (locked)
        {
            target.z = Mathf.Max(live.z, rest.z);
            if (swinging)
                target.y = live.y >= rest.y
                    ? rest.y + ((live.y - rest.y) * Constants.NeckGuardUpFollow)
                    : rest.y + ((live.y - rest.y) * Constants.NeckGuardDownFollow);
            else
                target.y = rest.y;
        }
        else if (state.NeckGuardFloorActive)
        {
            target.z = Mathf.Max(live.z, state.NeckGuardFloorZ);
            float restY = state.NeckGuardCrouchRestY;
            target.y = swinging
                ? (live.y >= restY
                    ? restY + ((live.y - restY) * Constants.NeckGuardUpFollow)
                    : restY + ((live.y - restY) * Constants.NeckGuardDownFollow))
                : restY;
        }

        bool swingSmoothing = locked || (swingWindow && player.isCrouching);
        float tau;
        if (swingSmoothing)
            tau = Constants.NeckGuardSwingTau;
        else if (state.NeckGuardRelease > 0f)
            tau = Mathf.Lerp(
                Constants.NeckGuardCalmTau, Constants.NeckGuardSwingTau,
                state.NeckGuardRelease / Constants.NeckGuardReleaseRamp);
        else
            tau = Constants.NeckGuardCalmTau;

        float tauY = swingSmoothing ? Constants.NeckGuardSwingTau : Constants.NeckGuardCalmTau;

        float vx = state.GuardedEyeVelocity.x;
        float vy = state.GuardedEyeVelocity.y;
        float vz = state.GuardedEyeVelocity.z;
        state.GuardedEyeDeviation = new Vector3(
            Mathf.SmoothDamp(state.GuardedEyeDeviation.x, target.x, ref vx, tau),
            Mathf.SmoothDamp(state.GuardedEyeDeviation.y, target.y, ref vy, tauY),
            Mathf.SmoothDamp(state.GuardedEyeDeviation.z, target.z, ref vz, tau));
        state.GuardedEyeVelocity = new Vector3(vx, vy, vz);

        return state.GuardedEyeDeviation;
    }

    private static float ClampTravelToWall(Vector3 origin, Vector3 dir, float distance, float radius, float margin)
    {
        StartOfRound? sor = StartOfRound.Instance;
        if (sor == null || distance <= 0f)
            return distance;

        if (Physics.SphereCast(
                origin, Mathf.Max(0.01f, radius), dir, out RaycastHit hit, distance + margin,
                sor.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Max(0f, hit.distance - margin);
        }

        return distance;
    }

    private static Vector3 ResolveVerticalClearance(Vector3 horizontalPoint, Vector3 up, float totalUp)
    {
        Vector3 dir = totalUp >= 0f ? up : -up;
        float travel = ClampTravelToWall(horizontalPoint, dir, Mathf.Abs(totalUp), Constants.WallCollisionRadius, 0f);
        return horizontalPoint + (dir * travel);
    }

    public static void RestoreOffset(LocalBodyState state)
    {
        state.CrouchBlend = 0f;
        state.LadderBlend = 0f;
        state.BobDampBlend = 0f;
        state.HoldBlend = 0f;
        state.RunBlend = 0f;
        state.JumpBlend = 0f;
        state.NeckGuardInitialized = false;
        state.NeckGuardTail = 0f;
        state.NeckGuardRelease = 0f;
        state.NeckGuardFloorActive = false;
        state.DeviationSmoothInitialized = false;
        state.DisableBobSmoothInitialized = false;
        state.DisableBobBlend = 0f;
        state.HasCameraTarget = false;

        if (!state.CameraOffsetApplied)
            return;

        Camera? camera = state.GameplayCamera;
        if (camera != null && state.CameraBaseCaptured)
            camera.transform.localPosition = state.CameraBaseLocalPosition;

        state.CameraOffsetApplied = false;
    }
}