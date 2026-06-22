using GameNetcodeStuff;
using UnityEngine;
using FirstPersonView.Compat;

namespace FirstPersonView
{
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
                + state.CrouchBlend * Constants.CrouchEyeOffsetUp
                + state.HoldBlend * Constants.HoldingEyeOffsetUp
                + state.RunBlend * Constants.RunningEyeOffsetUp
                + state.JumpBlend * Constants.JumpingEyeOffsetUp;

            state.LadderBlend = Mathf.MoveTowards(
                state.LadderBlend, player.isClimbingLadder ? 1f : 0f, Time.deltaTime / Constants.LadderBlendTime);
            float forwardOffset = Mathf.Lerp(
                Constants.EyeOffsetForward, Constants.EyeOffsetForwardOnLadder, state.LadderBlend)
                + state.HoldBlend * Constants.HoldingEyeOffsetForward
                + state.RunBlend * Constants.RunningEyeOffsetForward
                + state.JumpBlend * Constants.JumpingEyeOffsetForward;

            Vector3 baseWorld = parent != null
                ? parent.TransformPoint(state.CameraBaseLocalPosition)
                : state.CameraBaseLocalPosition;

            Vector3 anchorWorld = FollowBodyAnchor(
                state, player, baseWorld, yawRotation,
                out Vector3 liveDev, out Vector3 smoothedDev, out float boneOff);

            Vector3 target = anchorWorld
                + flatForward * forwardOffset
                + flatRight * Constants.EyeOffsetRight
                + up * upOffset;

            if (Constants.EnableWallCollision)
                target = ResolveWallCollision(baseWorld, target);

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

            // nudge arms forward off the eye so the hands sit at a comfortable position
            float forward = ConfigManager.HandOffsetZ.Value;
            if (forward != 0f)
                arms.position += cam.forward * forward;
        }

        private static Vector3 FollowBodyAnchor(
            LocalBodyState state, PlayerControllerB player, Vector3 baseWorld, Quaternion yawRotation,
            out Vector3 liveDev, out Vector3 smoothedDev, out float boneOff)
        {
            liveDev = Vector3.zero;
            smoothedDev = Vector3.zero;
            boneOff = 0f;

            Transform? bone = state.HeadBone;
            if (bone == null)
                return baseWorld;

            if (!state.EyeAnchorCaptured)
            {
                state.EyeAnchorLocal = Quaternion.Inverse(yawRotation) * (baseWorld - bone.position);
                state.EyeAnchorBoneLocal = bone.InverseTransformPoint(baseWorld);
                state.EyeAnchorCaptured = true;
            }

            boneOff = (bone.position - baseWorld).magnitude;

            if (TooManyEmotesCompat.IsFirstPersonEmoteActive())
                return bone.TransformPoint(state.EyeAnchorBoneLocal);

            Vector3 fullFollow = bone.position + yawRotation * state.EyeAnchorLocal;
            Vector3 deviationLocal = Quaternion.Inverse(yawRotation) * (fullFollow - baseWorld);

            deviationLocal.x *= Constants.FollowStrengthHorizontal;
            deviationLocal.z *= Constants.FollowStrengthHorizontal;
            deviationLocal.y *= Constants.FollowStrengthVertical;

            deviationLocal = StabilizeSprintBob(state, player, deviationLocal);
            liveDev = deviationLocal;
            deviationLocal = NeckGuardedFollow(state, player, deviationLocal);

            if (deviationLocal.sqrMagnitude > Constants.MaxFollowOffset * Constants.MaxFollowOffset)
                deviationLocal = deviationLocal.normalized * Constants.MaxFollowOffset;

            smoothedDev = deviationLocal;
            return baseWorld + yawRotation * deviationLocal;
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

        // holds the eye forward of the neck through a crouched swing
        private static Vector3 NeckGuardedFollow(LocalBodyState state, PlayerControllerB player, Vector3 live)
        {
            if (!state.NeckGuardInitialized)
            {
                state.SwingRest = live;
                state.GuardedEyeDeviation = live;
                state.GuardedEyeVelocity = Vector3.zero;
                state.NeckGuardTail = 0f;
                state.NeckGuardInitialized = true;
                return live;
            }

            bool swinging = player.isCrouching && player.activatingItem;
            if (swinging)
                state.NeckGuardTail = Constants.NeckGuardSwingTail;
            else if (state.NeckGuardTail > 0f)
                state.NeckGuardTail -= Time.deltaTime;
            bool active = state.NeckGuardTail > 0f;

            if (!active)
                state.SwingRest = live;
            Vector3 rest = state.SwingRest;

            Vector3 target = live;
            if (active)
            {
                target.z = Mathf.Max(live.z, rest.z);
                if (swinging)
                    target.y = live.y >= rest.y
                        ? rest.y + (live.y - rest.y) * Constants.NeckGuardUpFollow      // charging
                        : rest.y + (live.y - rest.y) * Constants.NeckGuardDownFollow;
                else
                    target.y = rest.y;
            }

            float tau = active ? Constants.NeckGuardSwingTau : Constants.NeckGuardCalmTau;
            state.GuardedEyeDeviation = Vector3.SmoothDamp(
                state.GuardedEyeDeviation, target, ref state.GuardedEyeVelocity, tau);
            return state.GuardedEyeDeviation;
        }

        private static Vector3 ResolveWallCollision(Vector3 origin, Vector3 desired)
        {
            StartOfRound? sor = StartOfRound.Instance;
            if (sor == null)
                return desired;

            Vector3 delta = desired - origin;
            float distance = delta.magnitude;
            if (distance < 1e-4f)
                return desired;

            Vector3 direction = delta / distance;
            float radius = Mathf.Max(0.01f, Constants.WallCollisionRadius);

            if (Physics.SphereCast(
                    origin, radius, direction, out RaycastHit hit, distance,
                    sor.collidersAndRoomMask, QueryTriggerInteraction.Ignore))
            {
                float allowed = Mathf.Clamp(hit.distance, 0f, distance);
                return origin + direction * allowed;
            }

            return desired;
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
            state.DeviationSmoothInitialized = false;
            state.HasCameraTarget = false;

            if (!state.CameraOffsetApplied)
                return;

            Camera? camera = state.GameplayCamera;
            if (camera != null && state.CameraBaseCaptured)
                camera.transform.localPosition = state.CameraBaseLocalPosition;

            state.CameraOffsetApplied = false;
        }
    }
}