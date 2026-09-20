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

    public const float CosmeticBodyScanInterval = 0.5f;   // rescan for cosmetics rigged to player skeleton

    public const float LethalPhonesScanInterval = 0.5f;

    public const float BodyDisabledWarnDelay = 5f;

    public const float NeckColumnCullStart = 0.6f;
    public const float NeckColumnCullRadius = 0.62f;

    // camera smoothing timing
    public const float CrouchBlendTime = 0.15f;     // blend time for the crouch eye
    public const float BobDampBlendTime = 0.12f;    // ramp time for the sprint/jump stabilizer
    public const float BobStabilizeTau = 0.2f;      // low pass time constant for the run/jump bob
    public const float LadderBlendTime = 0.08f;     // ease the ladder eye stabilizer on
    public const float LadderReleaseTime = 0.6f;    // hand the eye back to the live follow after the ladder
    public const float LadderStabilizeTau = 0.45f;  // low pass time for the ladder mount head swing
    public const float DisableHeadBobTau = 1f;    // low pass time for the no bob eye
    public const float DisableHeadBobBlendTime = 0.15f;  // ramp the no bob eye on/off
    public const float DisableHeadBobEngageBand = 0.06f;

    // vanilla arms on movement
    public const float ArmSwapEngageDelay = 0.05f;   // qualifying movement before the FP arms engage
    public const float ArmSwapReleaseDelay = 0.25f;  // non qualifying before reverting to body arms

    public const float EyeAnchorDriftTau = 2f;           // recentre eye anchor over this long, while standing
    public const float EyeAnchorSettledLookBand = 0.05f;  // only recentre while the view is within this much of level, up or down
    public const float EyeAnchorDriftLimit = 0.06f;      // drift may never take more of eye travel than this
    public const float EyeAnchorDriftWarnThreshold = 0.06f;  // drift above this is larger than normal head bob
    public const float EyeAnchorDriftWarnDelay = 5f;     // only warn once the drift has stayed high this long

    public const float StickToHeadBlendTime = 0.12f;  // ease the eye onto the head bone for interact animations

    public const float VehiclePushMatchEpsilonSqr = 1e-8f;   // treat the seat camera as untouched within this squared distance

    // camera
    public const float FollowStrengthVertical = 1f;     // follow the up/down bob fully
    public const float FollowStrengthHorizontal = 1f;       // follow forward/side fully
    public const float SprintBobReduction = 0.6f;     // fully stabilize the run/jump bob
    public const float MaxFollowOffset = 0.5f;      // how far the follow can move the eye
    public const float EyeForwardFloor = 0f;     // eye may never go behind the base

    public const float SwingReturnBlendTime = 0.3f;
    public const float SwingReturnHit = 0.7f;
    public const float SwingReturnCollide = 0.3f;
    public const float SwingTorsoTau = 0.07f;
    public const float SwingTorsoReturnTau = 0.16f;
    public const float SwingTorsoBlendTime = 0.25f;
    public const float SwingTorsoTail = 0.45f;

    public const float RunningBlendTime = 0.18f;          // ease the running nudge in/out

    public const float WalkingBlendTime = 0.18f;          // ease the walking nudge in/out

    // eye nudge while jumping
    public const float JumpingBlendTime = 0.18f;          // ease the jumping nudge in/out

    // camera wall collision
    public const float WallCollisionRadius = 0.1f;   // clearance the eye keeps from walls
    public const float WallCollisionForwardProbeRadius = 0.05f;  // probe radius for the wall check

    public const float LookDownEyeOffsetForward = 0.13f;   // extra forward push at full look down
    public const float LookDownCruiserImprovedEyeOffsetForward = 0.26f;   // while seated in cruiser with CruiserImproved compat enabled
    public const float LookDownForwardStart = 0.45f;      // begin pushing
    public const float LookDownForwardFull = 0.95f;        // reach the full push at this amount

    public const float LookDownRunningEyeOffsetForward = 0.12f;   // while running
    public const float LookDownWalkingEyeOffsetForward = 0.02f;   // while walking
    public const float LookDownJumpingEyeOffsetForward = 0.12f;   // while jumping
    public const float LookDownCruiserEyeOffsetForward = 0.13f;    // seated in the cruiser with CruiserImproved compat off

    public const float HeldItemFadeStartDistance = 0.35f;   // begin fading once the item is this close
    public const float HeldItemFadeEndDistance = 0.1f;      // reach the strongest fade at this distance

    public const float HeldItemCoverageFadeStart = 0.45f;   // start fading once it covers this fraction of the screen
    public const float HeldItemCoverageFadeFull = 0.6f;     // reach the strongest fade at this fraction

    public const float HeldItemMinFade = 0.11f;

    public const float HeldItemFadeSmoothTime = 0.08f;

    public const float SeatedLookDownClamp = 75f;   // max downward look angle while seated, in degrees

    public static readonly Vector3 CruiserKeyPositionOffset = new Vector3(0, 0, -0.05f);
    public static readonly Vector3 CruiserKeyRotationOffset = Vector3.zero;
}