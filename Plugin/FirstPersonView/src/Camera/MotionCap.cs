using UnityEngine;

namespace FirstPersonView;

internal static class MotionCap
{
    private const float MinSlope = 0.001f;
    private const float MinPush = 1e-6f;
    private const float MinLength = 1e-4f;
    private const float MinDistance = 0.2f;
    private const float FarDistance = 8f;
    private const float ProbeStep = 0.01f;

    private static readonly float[] PointX = { 0f, 0f, 1f };
    private static readonly float[] PointY = { 0f, -0.75f, -1f };

    public static void Get(
        Quaternion look, Quaternion last, Vector3 offset, Vector3 lastOffset, Vector3 up,
        float fov, float aspect, float delta, out float cap, out float release, out float motion)
    {
        Get(
            look, last, offset, lastOffset, up, fov, aspect, delta,
            Constants.Camera.GateAlpha, Constants.Camera.ReleaseAlpha, out cap, out release, out motion);
    }

    public static void Get(
        Quaternion look, Quaternion last, Vector3 offset, Vector3 lastOffset, Vector3 up,
        float fov, float aspect, float delta, float gateAlpha, float releaseAlpha,
        out float cap, out float release, out float motion)
    {
        Quaternion back = Quaternion.Inverse(look);
        Vector3 spin = back * GetSpin(look * Quaternion.Inverse(last));
        Vector3 move = back * (offset - lastOffset);
        Vector3 down = back * -up;
        Vector3 flat = GetFlat(down);

        float height = Vector3.Dot(offset, up) + Constants.Camera.FootGap;
        float half = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float floor = Constants.Camera.GateFloor * delta;

        cap = float.MaxValue;
        release = float.MaxValue;
        motion = 0f;
        for (int i = 0; i < PointX.Length; i++)
        {
            Vector3 ray = new Vector3(PointX[i] * half * aspect, PointY[i] * half, 1f).normalized;
            float slope = Vector3.Dot(ray, down);
            float distance = slope > MinSlope ? height / slope : FarDistance;
            distance = Mathf.Max(distance, MinDistance);

            float seen = Vector3.Cross(spin, ray).magnitude
                + (Vector3.Cross(move, ray).magnitude / distance * Mathf.Rad2Deg);
            float push = Mathf.Max(Vector3.Cross(flat, ray).magnitude / distance * Mathf.Rad2Deg * ProbeStep, MinPush);
            float gate = Constants.Camera.GateSlack / push;

            cap = Mathf.Min(cap, gate * ((gateAlpha * seen) + floor));
            release = Mathf.Min(release, gate * ((releaseAlpha * seen) + floor));
            if (i == 1)
                motion = seen;
        }

        cap = cap >= 0f ? cap * ProbeStep : float.MaxValue;
        release = release >= 0f ? release * ProbeStep : float.MaxValue;
    }

    private static Vector3 GetSpin(Quaternion turn)
    {
        if (turn.w < 0f)
            turn = new Quaternion(-turn.x, -turn.y, -turn.z, -turn.w);

        Vector3 axis = new(turn.x, turn.y, turn.z);
        float length = axis.magnitude;
        if (length < 1e-9f)
            return Vector3.zero;

        return axis * (2f * Mathf.Atan2(length, turn.w) * Mathf.Rad2Deg / length);
    }

    private static Vector3 GetFlat(Vector3 down)
    {
        Vector3 flat = new(-down.z * down.x, -down.z * down.y, 1f - (down.z * down.z));
        float length = flat.magnitude;
        return length > MinLength ? flat / length : new Vector3(0f, 0f, 1f);
    }
}
