using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class CameraRig
{
    private static readonly int CrouchDownHash = Animator.StringToHash("CrouchDown");
    private static readonly int CrouchIdleHash = Animator.StringToHash("CrouchIdle");
    private static readonly int CrouchWalkHash = Animator.StringToHash("CrouchWalk");

    private static readonly RaycastHit[] WallHits = new RaycastHit[32];

    private static int wallMask;
    private static int wallLayer = -1;

    public static void ApplyOffset(LocalBodyState state, PlayerControllerB player)
    {
        Camera? camera = state.GameplayCamera;
        Transform? container = player.cameraContainerTransform;
        if (camera == null || container == null || !state.CameraBaseCaptured)
            return;

        ShiftState shift = state.Shift;
        Transform camTransform = camera.transform;
        Transform? parent = camTransform.parent;

        Vector3 up = container.up;
        Vector3 flatForward = GetFlatForward(camTransform, container, player, up);
        float lookDown = Vector3.Dot(camTransform.forward, -up);
        GetAngles(container, camTransform, out float pitch, out float yaw);

        bool emoting = TooManyEmotesCompat.IsFirstPersonEmoteActive();
        bool posed = !emoting || state.Emote.Frame == Time.frameCount;

        Vector3 reference = GetReference(state, player, camTransform, parent, emoting);
        Vector3 vanilla = reference;
        float radius = GetWallRadius(camera);
        float follow = ConfigManager.HeadFollow.Value;
        if (!emoting && follow > 0f)
        {
            Quaternion yawRotation = Quaternion.LookRotation(flatForward, up);
            reference = AddFollow(state, player, container, reference, yawRotation, lookDown, follow, radius);
        }

        float heading = GetHeading(player);

        PoseTracker pose = shift.Pose;
        pose.Update(
            player,
            emoting ? TooManyEmotesCompat.GetEmoteHash() : 0,
            emoting ? TooManyEmotesCompat.GetEmotePhase() : 0f,
            emoting && TooManyEmotesCompat.IsEmoteBlending());

        float ownPitch = GetOwnPitch(state.LookPitch, state.LookFrame, Time.frameCount, pitch, emoting);
        NeedView view = BodyClearance.CreateView(camera, ownPitch, yaw);
        NeedView shown = BodyClearance.CreateView(camera, pitch, yaw);
        bool sentinels = BodyClearance.GetOffsets(state, reference, container, shift.Offsets);
        float needNow = sentinels
            ? BodyClearance.GetNeed(shift.Offsets, view)
            : BodyClearance.GetFallback(ownPitch);
        float needShown = sentinels
            ? BodyClearance.GetNeed(shift.Offsets, shown)
            : BodyClearance.GetFallback(pitch);

        float env = UpdateEnvelope(state, player, container, reference, view, pitch, follow, needNow, sentinels, posed);
        float ramp = sentinels ? shift.Samples.GetRamp(pose.Key, view) : 0f;
        bool gated = UpdateMotion(
            shift, player, camera, container, vanilla, ownPitch, yaw, out float cap, out float release);

        if (!shift.Running)
        {
            shift.Value = Mathf.Max(env, needNow);
            shift.RiseLeft = 0f;
        }
        else
        {
            ShiftInput input = new()
            {
                Delta = Time.deltaTime,
                Look = ownPitch - shift.LastLook,
                Turn = Mathf.Abs(Mathf.DeltaAngle(shift.LastYaw, heading)),
                Moving = pose.Key.IsMoving(),
                NeedNow = needNow,
                Env = env,
                EnvOld = shift.EnvOld,
                Ramp = ramp,
                KeyChanged = pose.Changed,
                GroupChanged = pose.Changed && !pose.Previous.InGroup(pose.Key),
                Gated = gated,
                Cap = cap,
                ReleaseCap = release,
            };

            MoveShift(shift, in input);
        }

        float envOwn = 0f;
        float envShown = 0f;
        if (sentinels && Mathf.Abs(pitch - ownPitch) > Constants.Camera.CoThreshold)
        {
            envOwn = shift.Samples.GetEnvelope(pose.Key, view);
            envShown = shift.Samples.GetEnvelope(pose.Key, shown);
        }

        HoldShift(shift, envShown, envOwn, needShown, Time.deltaTime, gated ? release : float.PositiveInfinity);

        shift.LastLook = ownPitch;
        shift.LastYaw = heading;
        shift.Running = true;

        float full = shift.Held * (1f - FirstPersonViewApi.ExternalSuppression);
        float travel = GetTravel(shift, player, reference, flatForward, full, radius);
        shift.BackAmount = BodyBack.GetAmount(full, travel);
        shift.BackDir = flatForward;
        shift.BackFrame = Time.frameCount;

        Vector3 target = reference + (flatForward * travel);
        camTransform.position = target;
        state.LastCameraTargetLocal = parent != null ? parent.InverseTransformPoint(target) : target;
        state.HasCameraTarget = true;
        state.CameraOffsetApplied = true;
    }

    private static float GetOwnPitch(float applied, int appliedFrame, int frame, float rendered, bool emoting)
    {
        return !emoting && appliedFrame == frame ? applied : rendered;
    }

    private static bool UpdateMotion(
        ShiftState shift, PlayerControllerB player, Camera camera, Transform container, Vector3 vanilla,
        float ownPitch, float yaw, out float cap, out float release)
    {
        Transform root = player.transform;
        Quaternion look = container.rotation * Quaternion.Euler(ownPitch, yaw, 0f);
        Vector3 offset = vanilla - root.position;
        int frame = Time.frameCount;

        bool gated = shift.MotionFrame == frame - 1;
        cap = float.NaN;
        release = float.NaN;
        if (gated)
        {
            MotionCap.Get(
                look, shift.LastRotation, offset, shift.LastOffset, root.up, camera.fieldOfView, camera.aspect,
                Time.deltaTime, out cap, out release, out _);
        }

        shift.LastRotation = look;
        shift.LastOffset = offset;
        shift.MotionFrame = frame;
        return gated;
    }

    internal static float GetPitch(Transform container, Transform camTransform)
    {
        float lookDown = Vector3.Dot(camTransform.forward, -container.up);
        return Mathf.Asin(Mathf.Clamp(lookDown, -1f, 1f)) * Mathf.Rad2Deg;
    }

    internal static void GetAngles(Transform container, Transform camTransform, out float pitch, out float yaw)
    {
        pitch = GetPitch(container, camTransform);

        Vector3 local = container.InverseTransformDirection(camTransform.forward);
        yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
    }

    private static Vector3 GetReference(
        LocalBodyState state, PlayerControllerB player, Transform camTransform, Transform? parent, bool emoting)
    {
        ShiftState shift = state.Shift;
        EmoteState emote = state.Emote;

        if (emoting && emote.SourceFrame == Time.frameCount && TooManyEmotesCompat.IsMovingWhileEmoting())
            return emote.Source;

        Vector3 local = state.CameraBaseLocalPosition;
        if (!emoting && player.inVehicleAnimation)
        {
            Vector3 found = shift.FoundFrame == Time.frameCount ? shift.FoundLocal : camTransform.localPosition;
            float epsilon = Constants.Camera.WriteEpsilon;
            bool ours = state.HasCameraTarget
                && (found - state.LastCameraTargetLocal).sqrMagnitude <= epsilon * epsilon;

            local = ours ? shift.ReferenceLocal : found;
        }

        shift.ReferenceLocal = local;
        return parent != null ? parent.TransformPoint(local) : local;
    }

    private static Vector3 AddFollow(
        LocalBodyState state, PlayerControllerB player, Transform container, Vector3 reference,
        Quaternion yawRotation, float lookDown, float amount, float radius)
    {
        Vector3 follow = FollowBodyAnchor(state, player, reference, yawRotation, lookDown) * amount;
        if (follow.sqrMagnitude < 1e-8f)
            return reference;

        Vector3 origin = container.position;
        Vector3 path = reference + follow - origin;
        float length = path.magnitude;
        if (length < 1e-4f)
            return origin;

        Vector3 direction = path / length;
        float reach = ClampTravelToWall(player, origin, direction, length, radius);
        return origin + (direction * reach);
    }

    private static float UpdateEnvelope(
        LocalBodyState state, PlayerControllerB player, Transform container, Vector3 reference,
        in NeedView view, float shownPitch, float follow, float needNow, bool sentinels, bool posed)
    {
        ShiftState shift = state.Shift;
        if (!sentinels)
        {
            shift.Env = 0f;
            shift.EnvOld = 0f;
            shift.EnvValid = false;
            return 0f;
        }

        PoseSamples samples = shift.Samples;
        PoseTracker pose = shift.Pose;

        if (shift.ChangeSeen != ConfigManager.ChangeCount)
        {
            shift.ChangeSeen = ConfigManager.ChangeCount;
            shift.EnvValid = false;
        }

        float scale = container.lossyScale.y;
        if (samples.Version != state.Clearance.Version
            || Mathf.Abs(samples.Scale - scale) > Constants.Camera.ScaleStep
            || Mathf.Abs(samples.Follow - follow) > Constants.Camera.FollowStep)
        {
            samples.Clear();
            samples.Version = state.Clearance.Version;
            samples.Scale = scale;
            samples.Follow = follow;
            shift.EnvValid = false;
        }

        if (pose.Changed || !shift.EnvValid || HasMoved(shift, view))
        {
            shift.Env = samples.GetEnvelope(pose.Key, view);
            shift.EnvOld = pose.Changed || shift.RiseLeft > 0f || shift.Pending
                ? samples.GetEnvelope(pose.Previous, view)
                : 0f;
            shift.EnvValid = true;
            shift.EnvPitch = view.Pitch;
            shift.EnvYaw = view.Yaw;
            shift.EnvFov = view.Fov;
            shift.EnvAspect = view.Aspect;
        }

        if (pose.CanSample && posed)
        {
            Vector3 pivot = GetPivot(state, player, container, reference, out float lean);
            samples.Add(pose.Key, pose.Phase, shift.Offsets, pivot, lean, shownPitch, view.Yaw);
            shift.Env = Mathf.Max(shift.Env, needNow);
        }

        return shift.Env;
    }

    private static bool HasMoved(ShiftState shift, in NeedView view) =>
        Mathf.Abs(view.Pitch - shift.EnvPitch) > Constants.Camera.PitchStep
        || Mathf.Abs(Mathf.DeltaAngle(shift.EnvYaw, view.Yaw)) > Constants.Camera.YawStep
        || Mathf.Abs(view.Fov - shift.EnvFov) > Constants.Camera.FovStep
        || Mathf.Abs(view.Aspect - shift.EnvAspect) > Constants.Camera.AspectStep;

    private static Vector3 GetPivot(
        LocalBodyState state, PlayerControllerB player, Transform container, Vector3 reference, out float lean)
    {
        Transform? spine = state.HeadBone != null ? state.HeadBone.parent : null;
        if (spine == null)
        {
            lean = 0f;
            return Vector3.zero;
        }

        lean = GetLean(player);
        Vector3 delta = spine.position - reference;
        return new Vector3(
            Vector3.Dot(delta, container.right), Vector3.Dot(delta, container.up), Vector3.Dot(delta, container.forward));
    }

    private static float GetLean(PlayerControllerB player) => player.cameraLookRig1.weight;

    private static float GetHeading(PlayerControllerB player)
    {
        Transform? body = player.thisPlayerBody;
        float bodyYaw = body != null ? body.localEulerAngles.y : 0f;
        return bodyYaw + player.ladderCameraHorizontal;
    }

    private static void MoveShift(ShiftState shift, in ShiftInput input)
    {
        float target = Mathf.Max(Mathf.Max(input.Env, input.NeedNow), input.Ramp);
        float start = shift.Value;
        float cap = input.Gated ? input.Cap : float.PositiveInfinity;

        if (input.KeyChanged)
        {
            bool blending = shift.RiseLeft > 0f && shift.GroupChanged;
            shift.Dwell = 0f;
            shift.Pending = true;
            shift.PendingLeft = Constants.Camera.PendingTime;
            if (input.GroupChanged || !blending)
            {
                shift.RiseLeft = Constants.Camera.RiseTime;
                shift.RiseFrom = start;
                shift.GroupChanged = input.GroupChanged && Constants.Camera.Release != ShiftRelease.Dwell;
            }
        }

        bool window = shift.RiseLeft > 0f;
        if (window)
        {
            if (shift.GroupChanged)
            {
                shift.Value = GetBlend(shift, target, input.Look, input.Delta, cap);
                if (shift.Value < start)
                    shift.Pending = false;
            }
            else
            {
                shift.Value = GetRise(shift, Mathf.Max(input.EnvOld, target), input.Look, input.Delta, cap);
                if (shift.Value < start)
                    shift.Pending = false;
            }

            shift.RiseLeft -= input.Delta;
        }
        else
        {
            if (input.Gated && shift.Pending)
            {
                float goal = shift.GroupChanged ? target : Mathf.Max(input.EnvOld, target);
                if (shift.Value >= goal || shift.PendingLeft <= 0f)
                    shift.Pending = false;
                else if (input.Look >= -Constants.Camera.LookThreshold)
                    shift.Value = Mathf.Min(goal, shift.Value + cap);

                shift.PendingLeft -= input.Delta;
            }

            if (Mathf.Abs(input.Look) > Constants.Camera.LookThreshold)
            {
                float looked = GetLook(start, input.Look, target, cap);
                shift.Value = input.Look > 0f ? Mathf.Max(shift.Value, looked) : looked;
                if (looked < start)
                    shift.Pending = false;
            }
        }

        ReleaseShift(shift, in input, target, start, window);
        shift.Value = Mathf.Max(shift.Value, input.NeedNow);
    }

    private static float GetRise(ShiftState shift, float goal, float look, float delta, float cap)
    {
        if (shift.Value < goal)
        {
            float low = Mathf.Min(shift.RiseFrom, shift.Value);
            float rate = Mathf.Max(0f, goal - low) / Constants.Camera.RiseTime;
            return Mathf.Min(goal, shift.Value + Mathf.Min(rate * delta, cap));
        }

        return look < -Constants.Camera.LookThreshold ? GetLook(shift.Value, look, goal, cap) : shift.Value;
    }

    private static float GetBlend(ShiftState shift, float target, float look, float delta, float cap)
    {
        if (shift.Value < target)
        {
            float rate = Mathf.Abs(target - shift.RiseFrom) / Constants.Camera.RiseTime;
            float left = (target - shift.Value) / Mathf.Max(shift.RiseLeft, delta);
            return Mathf.MoveTowards(shift.Value, target, Mathf.Min(Mathf.Max(rate, left) * delta, cap));
        }

        return look < -Constants.Camera.LookThreshold ? GetLook(shift.Value, look, target, cap) : shift.Value;
    }

    private static float GetLook(float value, float look, float target, float cap)
    {
        float step = Mathf.Min(Constants.Camera.LookRate * Mathf.Abs(look), cap);
        if (look > 0f)
            return value < target ? Mathf.Min(target, value + step) : value;

        return value > target ? Mathf.Max(target, value - step) : value;
    }

    private static void ReleaseShift(ShiftState shift, in ShiftInput input, float target, float start, bool window)
    {
        float band = Constants.Camera.DeadBand;
        ShiftRelease release = Constants.Camera.Release;
        bool looking = Mathf.Abs(input.Look) > Constants.Camera.LookThreshold;

        if (release == ShiftRelease.Dwell)
        {
            if (!looking && target < start - band)
                shift.Dwell += input.Delta;
            else
                shift.Dwell = 0f;
        }

        if (window || target >= start - band)
            return;

        float drop = GetDrop(shift, in input, release, looking);
        if (input.Gated)
            drop = Mathf.Min(drop, input.ReleaseCap);

        if (drop <= 0f)
            return;

        float fall = Mathf.Max(target, start - drop);
        if (fall >= shift.Value)
            return;

        shift.Value = fall;
        shift.Pending = false;
    }

    private static float GetDrop(ShiftState shift, in ShiftInput input, ShiftRelease release, bool looking)
    {
        float step = Constants.Camera.FallRate * input.Delta;
        return release switch
        {
            ShiftRelease.Dwell => shift.Dwell >= Constants.Camera.DwellTime ? step : 0f,
            ShiftRelease.Input => (looking ? input.Look < 0f : input.Turn > Constants.Camera.TurnThreshold || input.Moving)
                ? step
                : 0f,
            _ => Constants.Camera.TurnRate * input.Turn,
        };
    }

    private static void HoldShift(
        ShiftState shift, float envShown, float envOwn, float needShown, float delta, float cap)
    {
        float lift = Mathf.Max(0f, envShown - Mathf.Max(shift.Value, envOwn));
        if (lift >= shift.Lift)
        {
            shift.Lift = lift;
            shift.LiftAge = 0f;
        }
        else
        {
            shift.LiftAge += delta;
            if (shift.LiftAge > Constants.Camera.HoldTime)
                shift.Lift = Mathf.Max(lift, shift.Lift - Mathf.Min(Constants.Camera.HoldRate * delta, cap));
        }

        shift.Held = Mathf.Max(shift.Value + shift.Lift, needShown);
    }

    private static float GetTravel(
        ShiftState shift, PlayerControllerB player, Vector3 origin, Vector3 forward, float full, float radius)
    {
        float want = full;
        if (shift.Easing)
            want = Mathf.Min(full, Mathf.MoveTowards(shift.Travel, full, shift.EaseRate * Time.deltaTime));

        float limit = ClampTravelToWall(player, origin, forward, want, radius);
        bool blocked = limit < want - Constants.Camera.BlockEpsilon;
        if (blocked && shift.Blocked)
            limit = Mathf.Min(limit, shift.Travel + (Constants.Camera.WallRate * Time.deltaTime));

        if (blocked)
        {
            shift.Easing = false;
            want = limit;
        }
        else if (shift.Blocked)
        {
            shift.Easing = true;
            shift.EaseRate = Mathf.Max(0f, full - shift.Travel) / Constants.Camera.ReleaseTime;
            want = Mathf.Min(full, Mathf.MoveTowards(shift.Travel, full, shift.EaseRate * Time.deltaTime));
        }

        if (want >= full)
            shift.Easing = false;

        shift.Blocked = blocked;
        shift.Travel = want;
        return want;
    }

    public static void SaveEmoteBase(LocalBodyState state, PlayerControllerB player)
    {
        Transform? container = player.cameraContainerTransform;
        state.Emote.Container = container != null ? container.position : Vector3.zero;
    }

    private static Vector3 GetFlatForward(Transform camTransform, Transform container, PlayerControllerB player, Vector3 up)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(camTransform.forward, up);
        if (flatForward.sqrMagnitude < 1e-5f)
            flatForward = Vector3.ProjectOnPlane(player.transform.forward, up);

        return flatForward.sqrMagnitude < 1e-5f
            ? container.forward
            : flatForward.normalized;
    }

    public static void ClearStuckClamp(PlayerControllerB player)
    {
        if (!player.isPlayerControlled || player.isPlayerDead)
            return;

        if (player.clampLooking && !player.inSpecialInteractAnimation)
        {
            player.clampLooking = false;
            player.inVehicleAnimation = false;
        }
    }

    public static void UpdateSeatClamp(LocalBodyState state, PlayerControllerB player)
    {
        InteractTrigger? seat = player.currentTriggerInAnimationWith;
        if (!player.inVehicleAnimation || !player.clampLooking || seat == null || !seat.clampLooking)
            return;

        float limit = seat.minVerticalClamp;
        float push = GetRayBack(state);
        if (push > 0f && state.GameplayCamera != null)
            limit = Mathf.Max(limit, GetSeatReach(player, state.GameplayCamera.transform, limit, push));

        player.minVerticalClamp = Mathf.Max(limit, Mathf.Min(player.minVerticalClamp, player.cameraUp));
    }

    private static float GetSeatReach(PlayerControllerB player, Transform camTransform, float limit, float push)
    {
        Vector3 up = player.cameraContainerTransform.up;
        Vector3 flat = Vector3.ProjectOnPlane(camTransform.forward, up);
        if (flat.sqrMagnitude < 1e-5f)
            return limit;

        flat.Normalize();
        float angle = limit * Mathf.Deg2Rad;
        Vector3 eye = camTransform.position - (flat * push);
        Vector3 dir = (flat * Mathf.Cos(angle)) - (up * Mathf.Sin(angle));
        int mask = player.interactableObjectsMask | StartOfRound.Instance.collidersAndRoomMaskAndDefault;
        float reach = Physics.Raycast(eye, dir, out RaycastHit hit, player.grabDistance, mask, QueryTriggerInteraction.Collide)
            ? hit.distance
            : player.grabDistance;

        Vector3 aim = eye + (dir * reach) - camTransform.position;
        float pitch = Mathf.Atan2(-Vector3.Dot(aim, up), Vector3.Dot(aim, flat)) * Mathf.Rad2Deg;
        return Mathf.Min(pitch, Constants.Seat.MaxLookDown);
    }

    public static void AlignForInteractionRay(LocalBodyState state)
    {
        if (!state.HasCameraTarget || state.GameplayCamera == null)
            return;

        Transform camTransform = state.GameplayCamera.transform;
        Transform? parent = camTransform.parent;

        ShiftState shift = state.Shift;
        if (shift.FoundFrame != Time.frameCount)
        {
            shift.FoundLocal = camTransform.localPosition;
            shift.FoundFrame = Time.frameCount;
        }

        camTransform.position = parent != null
            ? parent.TransformPoint(state.LastCameraTargetLocal)
            : state.LastCameraTargetLocal;
    }

    public static float GetRayBack(LocalBodyState state) => state.HasCameraTarget ? state.Shift.Travel : 0f;

    public static bool TryGetEye(LocalBodyState state, out Vector3 eye)
    {
        eye = Vector3.zero;
        Camera? camera = state.GameplayCamera;
        if (!state.HasCameraTarget || camera == null)
            return false;

        Transform? parent = camera.transform.parent;
        Vector3 local = state.Shift.ReferenceLocal;
        eye = parent != null ? parent.TransformPoint(local) : local;
        return true;
    }

    public static void ApplyArmAnchor(PlayerControllerB player, bool useVanillaArms, bool seat)
    {
        Transform? container = player.cameraContainerTransform;
        if (container != null)
            ApplyArmAnchor(player, useVanillaArms, container.position, seat);
    }

    public static void ApplyArmAnchor(PlayerControllerB player, bool useVanillaArms, Vector3 framed, bool seat = false)
    {
        if (!useVanillaArms || (player.inSpecialInteractAnimation && !seat))
            return;

        Transform? arms = player.localArmsTransform;
        Camera? camera = player.gameplayCamera;
        if (arms == null || camera == null)
            return;

        Transform cam = camera.transform;
        arms.position += cam.position - framed;
        if (seat)
            return;

        arms.position += (cam.right * ConfigManager.HandOffsetX.Value)
            + (cam.up * ConfigManager.HandOffsetY.Value)
            + (cam.forward * ConfigManager.HandOffsetZ.Value);
    }

    private static Vector3 FollowBodyAnchor(
        LocalBodyState state, PlayerControllerB player, Vector3 baseWorld, Quaternion yawRotation, float lookDown)
    {
        Transform? bone = state.HeadBone;
        if (bone == null)
            return Vector3.zero;

        EyeAnchorState anchor = state.EyeAnchor;
        bool settled = IsEyeAnchorSettled(player, lookDown);
        anchor.SettledTime = settled ? anchor.SettledTime + Time.deltaTime : 0f;
        bool steady = anchor.SettledTime >= Constants.EyeAnchor.SettleTime;
        bool spawned = !player.isCrouching && !player.inSpecialInteractAnimation
            && player.specialAnimationWeight <= Constants.Spawn.EndWeight;

        if ((!anchor.Captured && spawned) || (steady && !anchor.Verified))
            CaptureEyeAnchor(anchor, bone, baseWorld, yawRotation, steady);

        if (!anchor.Captured)
            return Vector3.zero;

        Vector3 fullFollow = bone.position + (yawRotation * anchor.Local);
        Vector3 rawDeviation = Quaternion.Inverse(yawRotation) * (fullFollow - baseWorld);

        if (settled)
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Constants.EyeAnchor.DriftTau);
            anchor.Drift = Vector3.Lerp(anchor.Drift, rawDeviation, t);
            anchor.BoneLocal = Vector3.Lerp(anchor.BoneLocal, bone.InverseTransformPoint(baseWorld), t);

            float limit = Constants.EyeAnchor.DriftLimit;
            if (anchor.Drift.sqrMagnitude > limit * limit)
                anchor.Drift = anchor.Drift.normalized * limit;
        }

        Vector3 deviation = rawDeviation - anchor.Drift;
        if (deviation.sqrMagnitude > Constants.Camera.MaxFollowOffset * Constants.Camera.MaxFollowOffset)
            deviation = deviation.normalized * Constants.Camera.MaxFollowOffset;

        return yawRotation * deviation;
    }

    private static void CaptureEyeAnchor(
        EyeAnchorState anchor, Transform bone, Vector3 baseWorld, Quaternion yawRotation, bool steady)
    {
        anchor.Local = Quaternion.Inverse(yawRotation) * (baseWorld - bone.position);
        anchor.BoneLocal = bone.InverseTransformPoint(baseWorld);
        anchor.Drift = Vector3.zero;
        anchor.Captured = true;
        anchor.Verified = steady;
    }

    private static bool IsEyeAnchorSettled(PlayerControllerB player, float lookDown)
    {
        return Mathf.Abs(lookDown) <= Constants.EyeAnchor.SettledLookBand
            && !player.isWalking
            && !player.isSprinting
            && !player.inSpecialInteractAnimation
            && player.specialAnimationWeight <= Constants.Spawn.EndWeight
            && !player.performingEmote
            && !player.inVehicleAnimation
            && !player.clampLooking
            && player.inAnimationWithEnemy == null
            && !player.inShockingMinigame
            && !IsSwinging(player)
            && !player.isCrouching
            && IsCrouchSettled(player)
            && !player.isClimbingLadder
            && !player.isJumping
            && !player.isFallingFromJump;
    }

    private static bool IsSwinging(PlayerControllerB player) =>
        Players.IsSwingingShovelLike(player) || SwingTorsoSmoothing.IsActive;

    private static bool IsCrouchState(int hash) =>
        hash == CrouchDownHash || hash == CrouchIdleHash || hash == CrouchWalkHash;

    private static bool IsCrouchSettled(PlayerControllerB player)
    {
        Animator? animator = player.playerBodyAnimator;
        if (animator == null || animator.layerCount == 0)
            return true;

        int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (current == CrouchDownHash)
            return false;

        bool crouched = IsCrouchState(current);
        if (animator.IsInTransition(0)
            && crouched != IsCrouchState(animator.GetNextAnimatorStateInfo(0).shortNameHash))
        {
            return false;
        }

        return crouched == player.isCrouching;
    }

    private static float ClampTravelToWall(
        PlayerControllerB player, Vector3 origin, Vector3 dir, float distance, float radius)
    {
        StartOfRound? sor = StartOfRound.Instance;
        if (sor == null || distance <= 0f)
            return distance;

        int count = Physics.SphereCastNonAlloc(
            origin, radius, dir, WallHits, distance, GetWallMask(player) | sor.collidersAndRoomMask,
            QueryTriggerInteraction.Ignore);

        Transform root = player.transform;
        int playerBit = 1 << player.gameObject.layer;
        float limit = distance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = WallHits[i];
            Collider collider = hit.collider;
            if (collider == null
                || collider.transform.IsChildOf(root)
                || (collider.excludeLayers.value & playerBit) != 0)
            {
                continue;
            }

            bool furniture = collider.gameObject.layer == Constants.Layers.PlaceableShipObjects;
            if (furniture && !IsFurniture(collider))
                continue;

            if (hit.distance <= 0f && !(furniture && IsAhead(collider, origin, dir)))
                continue;

            limit = Mathf.Min(limit, Mathf.Max(0f, hit.distance));
        }

        return limit;
    }

    private static bool IsFurniture(Collider collider)
    {
        PlaceableShipObject? item = collider.GetComponent<PlaceableShipObject>();
        return item != null && !item.AllowPlacementOnWalls;
    }

    private static bool IsAhead(Collider collider, Vector3 origin, Vector3 dir)
    {
        if (collider is MeshCollider mesh && !mesh.convex)
            return false;

        Vector3 offset = collider.ClosestPoint(origin) - origin;
        return offset.sqrMagnitude > 1e-6f && Vector3.Dot(offset, dir) > 0f;
    }

    private static int GetWallMask(PlayerControllerB player)
    {
        int layer = player.gameObject.layer;
        if (layer == wallLayer)
            return wallMask;

        int mask = 1 << Constants.Layers.PlaceableShipObjects;
        for (int i = 0; i < 32; i++)
        {
            if (!Physics.GetIgnoreLayerCollision(layer, i))
                mask |= 1 << i;
        }

        wallMask = mask;
        wallLayer = layer;
        return mask;
    }

    private static float GetWallRadius(Camera camera)
    {
        float tall = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float wide = tall * camera.aspect;
        float corner = camera.nearClipPlane * Mathf.Sqrt(1f + (tall * tall) + (wide * wide));
        return corner + Constants.Camera.WallMargin;
    }

    public static void RestoreOffset(LocalBodyState state)
    {
        state.Shift.Reset();
        state.HasCameraTarget = false;

        if (!state.CameraOffsetApplied)
            return;

        Camera? camera = state.GameplayCamera;
        if (camera != null && state.CameraBaseCaptured)
            camera.transform.localPosition = state.CameraBaseLocalPosition;

        state.CameraOffsetApplied = false;
    }
}
