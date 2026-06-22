using System;
using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace FirstPersonView.Compat
{
    // keeps cruiser ignition key in the first-person bodys hands instead of the camera anchored arms
    // needs improvements
    internal static class VehicleCompat
    {
        private static bool _patched;
        private static FieldInfo? _ignitionCoroutineField;

        public static void EnsurePatched()
        {
            if (_patched)
                return;

            _patched = true;

            HarmonyMethod postfix = new(typeof(VehicleCompat), nameof(KeyPostfix));
            foreach (Type type in CollectVehicleTypes())
            {
                TryPatch(type, "Update", postfix);
                TryPatch(type, "LateUpdate", postfix);
            }
        }

        private static void TryPatch(Type type, string methodName, HarmonyMethod postfix)
        {
            MethodInfo? method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null)
                return;

            try
            {
                Plugin.Harmony.Patch(method, postfix: postfix);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Vehicle key compat: could not patch {type.Name}.{methodName}. {ex.Message}");
            }
        }

        private static IEnumerable<Type> CollectVehicleTypes()
        {
            Type baseType = typeof(VehicleController);
            yield return baseType;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetLoadableTypes())
                {
                    if (type != baseType && baseType.IsAssignableFrom(type))
                        yield return type;
                }
            }
        }

        private static void KeyPostfix(VehicleController __instance)
        {
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

            Transform key = __instance.keyObject.transform;
            key.rotation = hand.rotation * Quaternion.Euler(Constants.CruiserKeyRotationOffset);
            key.position = hand.position + hand.rotation * Constants.CruiserKeyPositionOffset;
        }

        private static bool IsIgnitionInProgress(VehicleController vehicle)
        {
            _ignitionCoroutineField ??= typeof(VehicleController).GetField(
                "keyIgnitionCoroutine", BindingFlags.Instance | BindingFlags.NonPublic);
            return _ignitionCoroutineField != null && _ignitionCoroutineField.GetValue(vehicle) != null;
        }
    }
}