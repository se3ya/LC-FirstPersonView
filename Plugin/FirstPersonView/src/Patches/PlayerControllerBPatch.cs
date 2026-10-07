using System.Collections.Generic;
using System;

using GameNetcodeStuff;
using HarmonyLib;

namespace FirstPersonView;

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class PlayerControllerBPatch
{
    private static readonly HashSet<string> _loggedTickErrors = new();

    [HarmonyPatch("ConnectClientToPlayerObject")]
    [HarmonyPostfix]
    private static void ConnectClientToPlayerObject_Postfix(PlayerControllerB __instance)
    {
        if (StartOfRound.Instance?.localPlayerController == __instance)
        {
            // you do not need to do "ensure patched" for the vehicle controller, just do a harmony postfix
            LocalBodyViewController.ResetAllStates();
            try
            {
                ItemHands.PublishNames();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not list the installed item names.\n{ex}");
            }
        }
    }

    [HarmonyPatch("SpawnPlayerAnimation")]
    [HarmonyPostfix]
    private static void SpawnPlayerAnimation_Postfix(PlayerControllerB __instance)
    {
        LocalBodyViewController.OnSpawnAnimation(__instance);
    }

    [HarmonyPatch("PlayerLookInput")]
    [HarmonyPostfix]
    private static void PlayerLookInput_Postfix(PlayerControllerB __instance)
    {
        try
        {
            LocalBodyViewController.OnLookApplied(__instance);
        }
        catch (Exception ex)
        {
            if (_loggedTickErrors.Add($"look|{ex.GetType()}: {ex.Message}"))
                Plugin.Log.LogError($"Look pitch update failed.\n{ex}");
        }
    }

    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("com.seeya.cameraoverhaul")]
    private static void LateUpdate_Postfix(PlayerControllerB __instance)
    {
        try
        {
            LocalBodyViewController.Tick(__instance);
        }
        catch (Exception ex)
        {
            if (_loggedTickErrors.Add($"{ex.GetType()}: {ex.Message}"))
                Plugin.Log.LogError($"Local body update failed.\n{ex}");
        }
    }

    [HarmonyPatch("SetHoverTipAndCurrentInteractTrigger")]
    [HarmonyPrefix]
    private static void SetHoverTipAndCurrentInteractTrigger_Prefix(PlayerControllerB __instance)
    {
        try
        {
            LocalBodyViewController.AlignCameraForInteractionRay(__instance);
        }
        catch (Exception ex)
        {
            if (_loggedTickErrors.Add($"ray|{ex.GetType()}: {ex.Message}"))
                Plugin.Log.LogError($"Interaction ray alignment failed.\n{ex}");
        }
    }
}
