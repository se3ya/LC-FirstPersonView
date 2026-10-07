using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class RayBack
{
    public static Ray GetRay(Vector3 origin, Vector3 direction, PlayerControllerB player)
    {
        float back = LocalBodyViewController.GetRayBack(player);
        return back > 0f ? new Ray(origin - (direction * back), direction) : new Ray(origin, direction);
    }

    public static float GetReach(float distance, PlayerControllerB player)
    {
        float back = LocalBodyViewController.GetRayBack(player);
        return back > 0f ? distance + back : distance;
    }
}
