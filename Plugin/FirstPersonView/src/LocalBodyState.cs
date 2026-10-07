using System.Collections.Generic;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal enum ShiftRelease
{
    Dwell,
    Input,
    Turn,
}

internal struct ShiftInput
{
    public float Delta;
    public float Look;
    public float Turn;
    public bool Moving;
    public float NeedNow;
    public float Env;
    public float EnvOld;
    public float Ramp;
    public bool KeyChanged;
    public bool GroupChanged;
    public bool Gated;
    public float Cap;
    public float ReleaseCap;
}

internal sealed class ShiftState
{
    public bool Running;
    public float Value;
    public float Env;
    public float EnvOld;
    public float Travel;

    public float RiseLeft;
    public float RiseFrom;
    public bool GroupChanged;
    public float Dwell;
    public float LastLook;
    public float LastYaw;

    public float Lift;
    public float LiftAge;
    public float Held;

    public bool Pending;
    public float PendingLeft;
    public int MotionFrame = -1;
    public Quaternion LastRotation = Quaternion.identity;
    public Vector3 LastOffset;

    public bool EnvValid;
    public float EnvPitch;
    public float EnvYaw;
    public float EnvFov;
    public float EnvAspect;

    public bool Blocked;
    public bool Easing;
    public float EaseRate;

    public float BackAmount;
    public Vector3 BackDir;
    public int BackFrame = -1;

    public int ChangeSeen;

    public Vector3 ReferenceLocal;
    public Vector3 FoundLocal;
    public int FoundFrame = -1;

    public readonly List<Vector3> Offsets = new();
    public readonly PoseTracker Pose = new();
    public readonly PoseSamples Samples = new();

    public void Reset()
    {
        Running = false;
        Value = 0f;
        Env = 0f;
        EnvOld = 0f;
        Travel = 0f;
        RiseLeft = 0f;
        GroupChanged = false;
        Dwell = 0f;
        Lift = 0f;
        LiftAge = 0f;
        Held = 0f;
        Pending = false;
        PendingLeft = 0f;
        MotionFrame = -1;
        LastRotation = Quaternion.identity;
        LastOffset = Vector3.zero;
        EnvValid = false;
        Blocked = false;
        Easing = false;
        BackAmount = 0f;
        BackDir = Vector3.zero;
        BackFrame = -1;
        Pose.Reset();
    }
}

internal sealed class ClearanceState
{
    public Mesh? Source;
    public SkinnedMeshRenderer? Body;
    public ClearancePoint[]? Points;
    public int Version;
}

internal sealed class EyeAnchorState
{
    public Vector3 Local;
    public Vector3 BoneLocal;
    public Vector3 Drift;
    public bool Captured;
    public bool Verified;
    public float SettledTime;
    public float DriftHighSince;
    public bool DriftWarned;
}

internal sealed class EmoteState
{
    public int Frame = -1;
    public bool Fallback;
    public bool VanillaArms;
    public Vector3 Container;
    public Vector3 Source;
    public int SourceFrame = -1;
    public int TickFrame = -1;
    public Vector3 CameraPosition;
    public Quaternion CameraRotation = Quaternion.identity;
}

internal sealed class LocalBodyState
{
    public PlayerControllerB Player = null!;
    public Transform ModelRoot = null!;
    public Transform? HeadBone;
    public Transform? HipsBone;

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
    public float LookPitch;
    public int LookFrame = -1;

    public readonly ShiftState Shift = new();
    public readonly ClearanceState Clearance = new();
    public readonly EyeAnchorState EyeAnchor = new();
    public readonly EmoteState Emote = new();

    public bool MovementArmsActive;
    public float MovementArmsTimer;
    public float VanillaArmsUntil;
    public float SeatArmsUntil;
    public bool SwingReturnStarted;
    public float SpawnUntil;
    public Component? LethalPhonesPlayerPhone;
    public float LethalPhonesNextScan;
    public Component? MoreCompanyCosmeticApplication;
}
