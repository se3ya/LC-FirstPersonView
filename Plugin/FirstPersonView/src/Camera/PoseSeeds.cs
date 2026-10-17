using UnityEngine;

namespace FirstPersonView;

internal static class PoseSeeds
{
    private readonly struct Step
    {
        public readonly float Pitch;
        public readonly float Slope;

        public Step(float pitch, float slope)
        {
            Pitch = pitch;
            Slope = slope;
        }
    }

    private const int IdleRow = 0;
    private const int WalkRow = 1;
    private const int SprintRow = 2;
    private const int AirRow = 3;
    private const int CrouchDownRow = 4;
    private const int CrouchIdleRow = 5;
    private const int CrouchWalkRow = 6;
    private const int TiredRow = 7;

    private const float MaxPitch = 85f;
    private const float NarrowFov = 66f;

    private static readonly int IdleHash = Animator.StringToHash("Idle1");
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int SprintHash = Animator.StringToHash("Sprint");
    private static readonly int AirHash = Animator.StringToHash("Air");
    private static readonly int CrouchDownHash = Animator.StringToHash("CrouchDown");
    private static readonly int CrouchIdleHash = Animator.StringToHash("CrouchIdle");
    private static readonly int CrouchWalkHash = Animator.StringToHash("CrouchWalk");
    private static readonly int TiredHash = Animator.StringToHash("WalkTired");

    private static readonly Step[] Idle = { new(43f, 0.74f), new(55f, 0.98f), new(75f, 1.22f) };
    private static readonly Step[] Walk = { new(29.5f, 0.83f), new(55f, 0.94f), new(70f, 1.22f) };
    private static readonly Step[] Sprint = { new(20.5f, 0.75f), new(40f, 0.99f), new(55f, 1.18f), new(75f, 1.54f) };
    private static readonly Step[] Air = { new(28f, 1.17f), new(55f, 1.36f), new(75f, 1.74f) };
    private static readonly Step[] CrouchIdle = { new(12f, 0.37f), new(50f, 0.53f), new(70f, 0.71f) };
    private static readonly Step[] CrouchWalk = { new(6f, 0.3f), new(45f, 0.47f), new(70f, 0.66f) };
    private static readonly Step[] Tired = { new(16.5f, 0.71f), new(20f, 1.19f), new(30f, 1.08f), new(70f, 1.43f) };

    private static readonly Step[][] Curves = { Idle, Walk, Sprint, Air, CrouchIdle, CrouchIdle, CrouchWalk, Tired };

    public static float Get(PoseKey key, float pitch, float fov)
    {
        int row = GetRow(key);
        return row < 0 ? 0f : GetValue(row, pitch + ConfigManager.LookDownMargin.Value, fov);
    }

    public static float GetRamp(PoseKey key, float pitch, float fov)
    {
        if (key.Base != IdleHash || key.Layer1 != 0 || key.Layer3 != 0 || key.Layer5 != 0 || key.Emote != 0 || key.Enemy != 0)
            return 0f;

        float start = Constants.Camera.RampStart;
        float end = Constants.Camera.RampEnd;
        float angle = pitch + ConfigManager.LookDownMargin.Value;
        return Mathf.Clamp((angle - start) / (end - start), 0f, 1f) * GetValue(IdleRow, end, fov);
    }

    public static bool IsCrouchDown(PoseKey key) => key.Base == CrouchDownHash;

    private static float GetValue(int row, float pitch, float fov)
    {
        float edge = Mathf.Clamp((fov - NarrowFov) / 2f, 0f, 1f);
        float angle = Mathf.Clamp(pitch, 0f, MaxPitch) + edge;
        float total = 0f;
        float slope = 0f;
        foreach (Step step in Curves[row])
        {
            total += (step.Slope - slope) * Mathf.Max(angle - step.Pitch, 0f);
            slope = step.Slope;
        }

        return total / 100f;
    }

    public static int GetRow(PoseKey key)
    {
        if (key.Layer1 != 0 || key.Layer3 != 0 || key.Layer5 != 0 || key.Melee != 0 || key.Emote != 0 || key.Enemy != 0)
            return -1;

        int state = key.Base;
        if (state == IdleHash)
            return IdleRow;

        if (state == WalkHash)
            return key.Clip == TiredHash ? TiredRow : WalkRow;

        if (state == SprintHash)
            return SprintRow;

        if (state == AirHash)
            return AirRow;

        if (state == CrouchDownHash)
            return CrouchDownRow;

        if (state == CrouchIdleHash)
            return CrouchIdleRow;

        return state == CrouchWalkHash ? CrouchWalkRow : -1;
    }
}
