using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal sealed class CameraBlends
{
    public float Crouch;
    public float BobDamp;
    public float ShovelSwing;
    public float Run;
    public float Walk;
    public float Jump;
    public float DisableBob;
    public float StickToHead;
    public float Ladder;

    public void Reset()
    {
        Crouch = 0f;
        BobDamp = 0f;
        ShovelSwing = 0f;
        Run = 0f;
        Walk = 0f;
        Jump = 0f;
        DisableBob = 0f;
        StickToHead = 0f;
        Ladder = 0f;
    }
}

internal sealed class BobSmoothing
{
    public Vector3 SmoothedDeviationLocal;
    public bool DeviationInitialized;
    public Vector3 SmoothedLadderLocal;
    public bool LadderInitialized;
    public float SmoothedY;
    public bool YInitialized;

    public void Reset()
    {
        SmoothedDeviationLocal = Vector3.zero;
        DeviationInitialized = false;
        SmoothedLadderLocal = Vector3.zero;
        LadderInitialized = false;
        SmoothedY = 0f;
        YInitialized = false;
    }
}

internal sealed class NeckGuardState
{
    public Vector3 SwingRest;
    public Vector3 EyeDeviation;
    public Vector3 EyeVelocity;
    public float Tail;
    public float Release;
    public float FloorZ;
    public float CrouchRestZ;
    public float CrouchRestY;
    public bool FloorActive;
    public bool Initialized;
    public bool Latched;
    public bool WasSwinging;
    public bool Engaged;

    public void Reset()
    {
        SwingRest = Vector3.zero;
        EyeDeviation = Vector3.zero;
        EyeVelocity = Vector3.zero;
        Tail = 0f;
        Release = 0f;
        FloorZ = 0f;
        CrouchRestZ = 0f;
        CrouchRestY = 0f;
        FloorActive = false;
        Initialized = false;
        Latched = false;
        WasSwinging = false;
        Engaged = false;
    }
}

internal sealed class EyeAnchorState
{
    public Vector3 Local;
    public Vector3 BoneLocal;
    public Vector3 Drift;
    public bool Captured;
    public float DriftHighSince;
    public bool DriftWarned;
}

internal sealed class LocalBodyState
{
    public PlayerControllerB Player = null!;
    public Transform ModelRoot = null!;
    public Transform? HeadBone;

    public SkinnedMeshRenderer? BodyRenderer;
    public Mesh? OriginalBodyMesh;
    public bool OriginalBodyMeshCaptured;
    public bool HeadHidden;
    public SkinnedMeshRenderer? ShadowProxy;
    public LODGroup? PlayerLodGroup;
    public int BodyLodIndex = -1;
    public float BodyLodNextScan;
    public float CosmeticBodyNextScan;

    public Transform? LeftArmBone;
    public Transform? RightArmBone;

    public Transform? LocalVisor;
    public Transform? VisorOriginalParent;
    public bool VisorOriginalParentCaptured;
    public bool VisorReparented;

    public Renderer[]? CrackRenderers;
    public bool CrackHidden;

    public Camera? GameplayCamera;
    public Vector3 CameraBaseLocalPosition;
    public bool CameraBaseCaptured;
    public bool CameraOffsetApplied;
    public Vector3 LastCameraTargetLocal;
    public bool HasCameraTarget;

    public Vector3 VehiclePush;
    public Vector3 VehiclePushResult;
    public bool VehiclePushApplied;

    public readonly CameraBlends Blends = new();
    public readonly BobSmoothing BobSmooth = new();
    public readonly NeckGuardState NeckGuard = new();
    public readonly EyeAnchorState EyeAnchor = new();

    public bool MovementArmsActive;
    public float MovementArmsTimer;
    public Component? LethalPhonesPlayerPhone;
    public float LethalPhonesNextScan;
    public Component? MoreCompanyCosmeticApplication;
}
