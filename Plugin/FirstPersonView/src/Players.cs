using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class Players
{
    private static readonly int ReelingUpHash = Animator.StringToHash("reelingUp");

    public static bool IsLocal(PlayerControllerB player)
    {
        StartOfRound? sor = StartOfRound.Instance;
        return sor != null && sor.localPlayerController == player;
    }

    public static bool IsActivelyHolding(PlayerControllerB player)
    {
        GrabbableObject? held = player.currentlyHeldObjectServer;
        return held != null && held.isHeld && !held.isPocketed && held.playerHeldBy == player;
    }

    public static bool IsSwingingShovelLike(PlayerControllerB player)
    {
        Animator? animator = player.playerBodyAnimator;
        if (animator != null && animator.GetBool(ReelingUpHash))
            return true;

        return player.activatingItem && player.currentlyHeldObjectServer is Shovel;
    }
}