using System.Collections.Generic;
using System;

using GameNetcodeStuff;
using HarmonyLib;

using FirstPersonView.Compat;

namespace FirstPersonView.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class LocalBodyViewPatches
{
    private static readonly HashSet<string> _loggedTickErrors = new();

    [HarmonyPatch("ConnectClientToPlayerObject")]
    [HarmonyPostfix]
    private static void ConnectClientToPlayerObject_Postfix(PlayerControllerB __instance)
    {
        if (StartOfRound.Instance?.localPlayerController == __instance)
        {
            VehicleCompat.EnsurePatched();
            LocalBodyViewController.ResetAllStates();
        }
    }

    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
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
        LocalBodyViewController.AlignCameraForInteractionRay(__instance);
    }
}