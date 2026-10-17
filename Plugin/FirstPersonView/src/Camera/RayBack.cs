using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class RayBack
{
    private static bool hoverFromEye;

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

    public static Ray GetHoverRay(Vector3 origin, Vector3 direction, PlayerControllerB player)
    {
        hoverFromEye = false;
        Ray ray = GetRay(origin, direction, player);
        float back = LocalBodyViewController.GetRayBack(player);
        if (back <= 0f || !HasWornBag(player) || !LocalBodyViewController.TryGetEye(player, out Vector3 eye))
            return ray;

        int mask = player.interactableObjectsMask;
        if (Physics.Raycast(ray, out RaycastHit hit, player.grabDistance + back, mask) && !IsIgnored(hit))
            return ray;

        Ray eyeRay = new(eye, direction);
        if (!Physics.Raycast(eyeRay, out RaycastHit eyeHit, player.grabDistance, mask) || !IsOwnBag(eyeHit, player))
            return ray;

        hoverFromEye = true;
        return eyeRay;
    }

    public static float GetHoverReach(float distance, PlayerControllerB player) =>
        hoverFromEye ? distance : GetReach(distance, player);

    private static bool IsIgnored(RaycastHit hit)
    {
        int layer = hit.collider.gameObject.layer;
        return layer == Constants.Layers.Room || layer == Constants.Layers.Vehicle;
    }

    private static bool HasWornBag(PlayerControllerB player)
    {
        if (player.ItemOnlySlot is BeltBagItem && player.ItemOnlySlot.isPocketed)
            return true;

        foreach (GrabbableObject item in player.ItemSlots)
        {
            if (item is BeltBagItem && item.isPocketed)
                return true;
        }

        return false;
    }

    private static bool IsOwnBag(RaycastHit hit, PlayerControllerB player)
    {
        if (!hit.collider.CompareTag("InteractTrigger"))
            return false;

        BeltBagItem? bag = hit.collider.GetComponentInParent<BeltBagItem>();
        return bag != null && bag.isPocketed && bag.playerHeldBy == player;
    }
}
