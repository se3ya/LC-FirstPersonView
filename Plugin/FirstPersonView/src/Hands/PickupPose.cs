using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class PickupPose
{
    public static void Apply()
    {
        StartOfRound? sor = StartOfRound.Instance;
        PlayerControllerB? player = sor != null ? sor.localPlayerController : null;
        if (player == null || !player.isGrabbingObjectAnimation || !LocalBodyViewController.LocalBodyShown)
            return;

        GrabbableObject? item = player.currentlyGrabbingObject;
        Transform? hand = player.serverItemHolder;
        if (item == null || hand == null || item.isHeld || item.parentObject != player.localItemHolder)
            return;

        Item? properties = item.itemProperties;
        if (properties == null || FirstPersonBody.VanillaArmsShown)
            return;

        Players.GetHeldPose(hand, properties, out Vector3 position, out Quaternion rotation);
        item.transform.SetPositionAndRotation(position, rotation);
    }
}
