using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace FirstPersonView.Compat;

// keeps cruiser ignition key in the first-person bodys hands instead of the camera anchored arms

[HarmonyPatch(typeof(VehicleController))]
public static class VehicleCompat
{
    [HarmonyPatch(nameof(VehicleController.Update))]
    [HarmonyPostfix]
    public static void Update_Key_Postfix(VehicleController __instance)
    {
        if (!__instance.IsSpawned)
            return;
        // do not attemot to patch custom vehicles, the vanilla CC has an ID of 0, this will also "just work" out the box for the Version-55 Company Cruiser mod, the ScanVan and the Company Hauler without soft dep, as they uses the 'new' keyword.
        // this is good future proofing if you ever patch anything VehicleController, incase a creator uses base methods, and you only want to touch the vanilla CC
        if (__instance.vehicleID != 0) 
            return;   
        if (!LocalBodyViewController.LocalBodyShown)
            return;
        if (!__instance.keyIsInDriverHand || !__instance.localPlayerInControl)
            return;
        if (__instance.keyObject == null)
            return;

        // twist/ pull the key during steady driving the key sits in the ignition slot.
        // it animates it from the hand
        // into the slot and turns it
        // palm and the ignition never visually turns
        if (IsIgnitionInProgress(__instance))
            return;

        PlayerControllerB driver = __instance.currentDriver;
        Transform? hand = driver != null ? driver.serverItemHolder : null;
        if (hand == null)
            return;

        // this could absolutely be improved, to fix numerous issues with the vanilla key, but i'm not touching this (for now)
        Transform key = __instance.keyObject.transform;
        key.rotation = hand.rotation * Quaternion.Euler(Constants.CruiserKeyRotationOffset);
        key.position = hand.position + (hand.rotation * Constants.CruiserKeyPositionOffset);
    }

    private static bool IsIgnitionInProgress(VehicleController vehicle)
    {
        // please stop using hilariously expensive reflection every frame, i have added the bepinex assembly publiciser 
        // which allows you to access private methods/funcs/etc, this is miles better for performance
        return vehicle.keyIgnitionCoroutine != null;
    }
}
