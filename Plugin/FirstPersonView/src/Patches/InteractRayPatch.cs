using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace FirstPersonView;

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class InteractRayPatch
{
    private static readonly FieldInfo GrabDistance = AccessTools.Field(typeof(PlayerControllerB), "grabDistance");
    private static readonly FieldInfo InteractRay = AccessTools.Field(typeof(PlayerControllerB), "interactRay");
    private static readonly MethodInfo RayMethod = AccessTools.Method(typeof(RayBack), nameof(RayBack.GetRay));
    private static readonly MethodInfo ReachMethod = AccessTools.Method(typeof(RayBack), nameof(RayBack.GetReach));
    private static readonly MethodInfo HoverRayMethod = AccessTools.Method(typeof(RayBack), nameof(RayBack.GetHoverRay));
    private static readonly MethodInfo HoverReachMethod = AccessTools.Method(typeof(RayBack), nameof(RayBack.GetHoverReach));

    [HarmonyPatch("SetHoverTipAndCurrentInteractTrigger")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> SetHoverTipAndCurrentInteractTrigger_Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return Patch(instructions, "SetHoverTipAndCurrentInteractTrigger", HoverRayMethod, HoverReachMethod);
    }

    [HarmonyPatch("BeginGrabObject")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> BeginGrabObject_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch(instructions, "BeginGrabObject", RayMethod, ReachMethod);
    }

    private static List<CodeInstruction> Patch(
        IEnumerable<CodeInstruction> instructions, string method, MethodInfo rayMethod, MethodInfo reachMethod)
    {
        List<CodeInstruction> code = new(instructions);
        try
        {
            int ray = FindOne(code, IsRay);
            int reach = FindOne(code, IsReach);
            if (ray < 0 || reach < 0)
            {
                Plugin.Log.LogWarning($"Could not patch the interaction ray in {method}.");
                return code;
            }

            List<CodeInstruction> patched = new(code.Count + 3);
            for (int i = 0; i < code.Count; i++)
            {
                CodeInstruction ci = code[i];
                if (i == ray)
                {
                    patched.Add(new CodeInstruction(OpCodes.Ldarg_0) { labels = ci.labels, blocks = ci.blocks });
                    patched.Add(new CodeInstruction(OpCodes.Call, rayMethod));
                    continue;
                }

                patched.Add(ci);
                if (i == reach)
                {
                    patched.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    patched.Add(new CodeInstruction(OpCodes.Call, reachMethod));
                }
            }

            return patched;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Interaction ray patch failed in {method}.\n{ex}");
            return code;
        }
    }

    private static int FindOne(List<CodeInstruction> code, Func<List<CodeInstruction>, int, bool> match)
    {
        int found = -1;
        for (int i = 0; i < code.Count; i++)
        {
            if (!match(code, i))
                continue;

            if (found >= 0)
                return -1;

            found = i;
        }

        return found;
    }

    private static bool IsRay(List<CodeInstruction> code, int index) =>
        code[index].opcode == OpCodes.Newobj
        && code[index].operand is ConstructorInfo constructor
        && constructor.DeclaringType == typeof(Ray)
        && index + 1 < code.Count
        && code[index + 1].StoresField(InteractRay);

    private static bool IsReach(List<CodeInstruction> code, int index) => code[index].LoadsField(GrabDistance);
}
