using System.Collections.Generic;

using UnityEngine;

namespace FirstPersonView;

internal static class BodyBack
{
    private static Transform? bone;
    private static Camera? owner;
    private static Vector3 saved;
    private static Vector3 written;

    public static float GetAmount(float full, float travel) => Mathf.Max(0f, full - travel);

    public static void Apply(Transform? hips, Camera? camera, Vector3 direction, float distance)
    {
        Restore();
        if (hips == null || !(distance > 0f))
            return;

        Vector3 target = hips.position - (direction * distance);
        float sum = target.x + target.y + target.z;
        if (float.IsNaN(sum) || float.IsInfinity(sum))
            return;

        saved = hips.localPosition;
        hips.position = target;
        written = hips.localPosition;
        bone = hips;
        owner = camera;
    }

    public static void RestoreAfter(List<Camera> cameras)
    {
        if (bone == null)
            return;

        if (owner != null && owner.isActiveAndEnabled && !cameras.Contains(owner))
            return;

        Restore();
    }

    public static void Restore()
    {
        Transform? hips = bone;
        bone = null;
        owner = null;
        if (hips == null || hips.localPosition != written)
            return;

        hips.localPosition = saved;
    }
}
