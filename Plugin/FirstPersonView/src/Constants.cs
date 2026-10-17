using UnityEngine;

namespace FirstPersonView;

internal static class Constants
{
    internal static class Rig
    {
        public const string ScavengerModel = "ScavengerModel";
        public const string Metarig = "metarig";
        public const string HipsBone = "spine";
        public const string HeadBone = "spine.004";
        public const string LeftArmBone = "arm.L_upper";
        public const string RightArmBone = "arm.R_upper";
        public static readonly string[] HeadBoneHints = { "head", "helmet", "visor", "spine.004" };
    }

    internal static class Layers
    {
        public const int HelmetVisor = 7;
        public const int Room = 8;
        public const int RadarDot = 14;
        public const int ScanNode = 22;
        public const int EnemiesNotRendered = 23;
        public const int PlaceableShipObjects = 26;
        public const int Vehicle = 30;
    }

    internal static class Culling
    {
        public const float HeadStrongInfluence = 0.35f;
        public const float HeadAverageInfluence = 0.30f;
        public const float PipeBehindThreshold = 0f;
        public const float NeckColumnStart = 0.6f;
        public const float NeckColumnRadius = 0.62f;
        public const float FoldUvRange = 0.03f;
        public const float FoldAngle = 40f;
    }

    internal static class Body
    {
        public const float CosmeticScanInterval = 0.5f;
        public const float DisabledWarnDelay = 5f;
    }

    internal static class Spawn
    {
        public const float EndWeight = 0.01f;
        public const float Timeout = 5f;
    }

    internal static class ArmSwap
    {
        public const float EngageDelay = 0.05f;
        public const float ReleaseDelay = 0.25f;
    }

    internal static class Seat
    {
        public const string Couch = "SA_SitOnCouch";
        public const string Chair = "SA_SitOnElectricChair";
        public const float MaxLookDown = 80f;
    }

    internal static class Camera
    {
        public static readonly ShiftRelease Release = ShiftRelease.Turn;
        public const float MaxFollowOffset = 0.6f;
        public const float WallMargin = 0.01f;
        public const float WallRate = 1f;
        public const float WriteEpsilon = 1e-4f;
        public const float BlockEpsilon = 1e-3f;
        public const float RiseTime = 0.25f;
        public const float ReleaseTime = 0.15f;
        public const float LookRate = 0.02f;
        public const float LookThreshold = 0.01f;
        public const float TurnThreshold = 0.05f;
        public const float TurnRate = 0.005f;
        public const float FallRate = 0.06f;
        public const float DeadBand = 0.02f;
        public const float DwellTime = 3f;
        public const float HoldTime = 0.35f;
        public const float HoldRate = 0.2f;
        public const float GateAlpha = 0.25f;
        public const float ReleaseAlpha = 0.5f;
        public const float GateSlack = 0.9f;
        public const float GateFloor = 0.036f;
        public const float FootGap = 0.04f;
        public const float CoThreshold = 0.001f;
        public const float PendingTime = 1f;
        public const float PitchStep = 0.25f;
        public const float YawStep = 2f;
        public const float FovStep = 0.01f;
        public const float AspectStep = 0.001f;
        public const float ScaleStep = 0.001f;
        public const float FollowStep = 0.0001f;
        public const float RampStart = 20f;
        public const float RampEnd = 80f;
    }

    internal static class Samples
    {
        public const int BinCount = 24;
        public const float KeyWeight = 0.5f;
        public const float BandLow = 0.05f;
        public const float BandHigh = 0.95f;
        public const float BandDelay = 0.1f;
        public const float EnterDelay = 0.1f;
        public const float YawRange = 30f;
        public const float VanillaScale = 1.1216f;
        public const int ClipHold = 2;
    }

    internal static class Cap
    {
        public const float OnHeight = 0.12f;
        public const float OffHeight = 0.08f;
        public const float StandOnHeight = 0.06f;
        public const float StandOffHeight = 0.035f;
    }

    internal static class Clearance
    {
        public const int BodyVertexCount = 7998;
        public const float EdgeMargin = 0.5f;
        public const float MinSpread = 0.05f;
        public const float MinCos = 0.01f;
    }

    internal static class EyeAnchor
    {
        public const float DriftTau = 2f;
        public const float SettledLookBand = 0.05f;
        public const float DriftLimit = 0.06f;
        public const float SettleTime = 0.6f;
    }

    internal static class Swing
    {
        public const float ReturnBlendTime = 0.3f;
        public const float ReturnHit = 0.7f;
        public const float ReturnCollide = 0.3f;
        public const float TorsoTau = 0.07f;
        public const float TorsoReturnTau = 0.16f;
        public const float TorsoBlendTime = 0.25f;
        public const float TorsoTail = 0.45f;
        public const float TorsoHoldLimit = 1.5f;
    }

    internal static class HeldItem
    {
        public const float CoverageFadeRange = 0.15f;
        public const float FadeSmoothTime = 0.08f;
    }

    internal static class Cruiser
    {
        public static readonly Vector3 KeyPositionOffset = new Vector3(0, 0, -0.05f);
        public static readonly Vector3 KeyRotationOffset = Vector3.zero;
    }

    internal static class Compat
    {
        public const float LethalPhonesScanInterval = 0.5f;
    }
}
