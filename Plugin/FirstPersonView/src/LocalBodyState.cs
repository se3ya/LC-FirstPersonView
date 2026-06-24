using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

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
    public float CrouchBlend;
    public float LadderBlend;
    public float BobDampBlend;
    public float HoldBlend;
    public float RunBlend;
    public float JumpBlend;
    public Vector3 SmoothedDeviationLocal;
    public bool DeviationSmoothInitialized;
    public Vector3 SwingRest;
    public Vector3 GuardedEyeDeviation;
    public Vector3 GuardedEyeVelocity;
    public float NeckGuardTail;
    public float NeckGuardRelease;
    public float NeckGuardFloorZ;
    public float NeckGuardCrouchRestZ;
    public float NeckGuardCrouchRestY;
    public bool NeckGuardFloorActive;
    public bool NeckGuardInitialized;
    public bool NeckGuardLatched;
    public bool NeckGuardWasSwinging;
    public bool NeckGuardEngaged;
    public Vector3 EyeAnchorLocal;
    public Vector3 EyeAnchorBoneLocal;
    public bool EyeAnchorCaptured;
}