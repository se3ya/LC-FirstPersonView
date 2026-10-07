using System.Collections.Generic;

using UnityEngine;

namespace FirstPersonView;

internal readonly struct NeedView
{
    public readonly float Pitch;
    public readonly float Yaw;
    public readonly float Fov;
    public readonly float Aspect;

    private readonly float sinPitch;
    private readonly float cosPitch;
    private readonly float flatCos;
    private readonly float sinTheta;
    private readonly float cosTheta;
    private readonly float spread;
    private readonly float sideSlope;
    private readonly float near;
    private readonly float sinYaw;
    private readonly float cosYaw;

    public NeedView(float pitch, float yaw, float fov, float aspect, float near, float margin)
    {
        Pitch = pitch;
        Yaw = yaw;
        Fov = fov;
        Aspect = aspect;

        float p = pitch * Mathf.Deg2Rad;
        float half = fov * 0.5f * Mathf.Deg2Rad;
        float edge = margin * Mathf.Deg2Rad;
        float theta = half + edge;
        float wide = Mathf.Atan(Mathf.Tan(half) * Mathf.Max(aspect, 0.1f));
        float turn = yaw * Mathf.Deg2Rad;

        sinPitch = Mathf.Sin(p);
        cosPitch = Mathf.Cos(p);
        flatCos = Mathf.Max(cosPitch, Constants.Clearance.MinCos);
        sinTheta = Mathf.Sin(theta);
        cosTheta = Mathf.Cos(theta);
        spread = Mathf.Sin(p + theta);
        sideSlope = 1f / Mathf.Tan(wide + edge);
        this.near = near;
        sinYaw = Mathf.Sin(turn);
        cosYaw = Mathf.Cos(turn);
    }

    public float GetNeed(List<Vector3> offsets) => GetNeed(offsets, Vector3.zero, 0f);

    public float GetNeed(List<Vector3> offsets, Vector3 pivot, float angle)
    {
        if (spread < Constants.Clearance.MinSpread)
            return 0f;

        float lean = angle * Mathf.Deg2Rad;
        float sinLean = Mathf.Sin(lean);
        float cosLean = Mathf.Cos(lean);
        float pivotZ = (pivot.x * sinYaw) + (pivot.z * cosYaw);

        float best = 0f;
        for (int i = 0; i < offsets.Count; i++)
        {
            Vector3 offset = offsets[i];
            float x = (offset.x * cosYaw) - (offset.z * sinYaw);
            float turnZ = (offset.x * sinYaw) + (offset.z * cosYaw);

            float qy = offset.y - pivot.y;
            float qz = turnZ - pivotZ;
            float y = pivot.y + (qy * cosLean) - (qz * sinLean);
            float z = pivotZ + (qy * sinLean) + (qz * cosLean);

            float rise = (y * cosPitch) + (z * sinPitch);
            float ahead = (z * cosPitch) - (y * sinPitch);
            float bottom = ((rise * cosTheta) + (ahead * sinTheta)) / spread;
            float front = (ahead - near) / flatCos;
            float side = (ahead - (Mathf.Abs(x) * sideSlope)) / flatCos;

            float need = Mathf.Min(bottom, Mathf.Min(front, side));
            if (need > best)
                best = need;
        }

        return best;
    }
}
