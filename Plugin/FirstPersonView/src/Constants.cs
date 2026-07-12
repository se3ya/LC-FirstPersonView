using UnityEngine;

namespace FirstPersonView;

internal static class Constants
{
    // player model hierarchy
    public const string ScavengerModelName = "ScavengerModel";
    public const string MetarigName = "metarig";
    public const string HeadBoneName = "spine.004"; // spine joint
    public const string LeftArmBoneName = "arm.L_upper";
    public const string RightArmBoneName = "arm.R_upper";

    public const int EnemiesNotRenderedLayer = 23;

    // bone names
    public static readonly string[] HeadBoneNameHints = { "head", "helmet", "visor", "spine.004" };

    // head triangle culling thresholds
    public const float HeadStrongInfluenceThreshold = 0.35f;
    public const float HeadAverageInfluenceThreshold = 0.30f;

    public const float PipeCullBehindThreshold = 0f;

    // camera smoothing timing
    public const float CrouchBlendTime = 0.15f;     // blend time for the crouch eye
    public const float LadderBlendTime = 0.08f;     // ease the forward-eye offset on/off ladders
    public const float BobDampBlendTime = 0.12f;    // ramp time for the sprint/jump stabilizer
    public const float BobStabilizeTau = 0.2f;      // low pass time constant for the run/jump bob
    public const float DisableHeadBobTau = 1f;    // low pass time for the no bob eye
    public const float DisableHeadBobBlendTime = 0.15f;  // ramp the no bob eye on/off

    // vanilla arms on movement
    public const float ArmSwapEngageDelay = 0.05f;   // qualifying movement before the FP arms engage
    public const float ArmSwapReleaseDelay = 0.25f;  // non qualifying before reverting to body arms

    public const float NeckGuardCalmTau = 0.015f;        // follow tau when not swinging (responsive)
    public const float NeckGuardSwingTau = 0.12f;        // follow tau during the swing (smooths the guarded follow)
    public const float NeckGuardSwingTail = 0.6f;        // keep the guard active this long after the swing (s)
    public const float NeckGuardReleaseRamp = 0.8f;     // ease tau swing -> calm over this long
    public const float NeckGuardUpFollow = 1f;         // fraction of the up throw the eye follows during a swing
    public const float NeckGuardDownFollow = 0f;       // fraction of a below rest dip the eye follows during a swing
    public const float NeckGuardCrouchSettled = 0.9f;    // only freeze the rest anchor once crouch blend passes this
    public const float NeckGuardCrouchFloor = 0.35f;     // min forward deviation a stand -> crouch swing floors the eye at

    // camera
    public const float FollowStrengthVertical = 1f;     // follow the up/down bob fully
    public const float FollowStrengthHorizontal = 1f;       // follow forward/side fully
    public const float SprintBobReduction = 0.6f;     // fully stabilize the run/jump bob
    public const float MaxFollowOffset = 0.5f;      // how far the follow can move the eye
    public const float EyeOffsetForward = 0f;    // push the eye forward so the neck stays behind
    public const float EyeForwardFloor = 0f;     // eye may never go behind the base
    public const float EyeOffsetUp = 0f;
    public const float EyeOffsetRight = 0f;
    public const float CrouchEyeOffsetUp = 0f;   // raise the eye while crouching so it doesn't sink into the neck

    // eye nudge while holding
    public const float HoldingEyeOffsetUp = 0f;        // extra up while holding
    public const float HoldingBlendTime = 0.18f;          // ease the holding nudge in/out

    public const float RunningEyeOffsetUp = 0f;        // extra up while running
    public const float RunningBlendTime = 0.18f;          // ease the running nudge in/out

    // eye nudge while jumping
    public const float JumpingEyeOffsetUp = 0f;        // extra up while jumping
    public const float JumpingBlendTime = 0.18f;          // ease the jumping nudge in/out

    // camera wall collision
    public const float WallCollisionRadius = 0.1f;   // clearance the eye keeps from walls
    public const float WallCollisionForwardProbeRadius = 0.05f;  // probe radius for the wall check
    public const float WallCollisionForwardMargin = 0f;        // stop this far short of the wall along forward

    public const float LookDownEyeOffsetForward = 0.12f;   // extra forward push at full look-down
    public const float LookDownForwardStart = 0.08f;      // begin pushing
    public const float LookDownForwardFull = 0.8f;        // reach the full push at this amount

    public const float LookDownHoldingEyeOffsetForward = 0.12f;   // while holding two-handed
    public const float LookDownRunningEyeOffsetForward = 0.12f;   // while running
    public const float LookDownJumpingEyeOffsetForward = 0.12f;   // while jumping
    public const float LookDownLadderEyeOffsetForward = 0.12f;    // while on a ladder
    public const float HeldItemFadeStartDistance = 0.35f;   // begin fading once the item is this close
    public const float HeldItemFadeEndDistance = 0.1f;      // reach the strongest fade at this distance

    public const float HeldItemCoverageFadeStart = 0.45f;   // start fading once it covers this fraction of the screen
    public const float HeldItemCoverageFadeFull = 0.6f;     // reach the strongest fade at this fraction

    public const float HeldItemMinFade = 0.11f;

    public const float HeldItemFadeSmoothTime = 0.08f;

    public const float SeatedLookDownClamp = 75f;   // max downward look angle while seated, in degrees

    public const float CruiserCompatForwardPush = 0.2f;
    public const float CruiserCompatUpPush = 0.1f;

    public static readonly Vector3 CruiserKeyPositionOffset = new Vector3(0, 0, -0.05f);
    public static readonly Vector3 CruiserKeyRotationOffset = Vector3.zero;
}