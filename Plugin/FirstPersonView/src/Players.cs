using GameNetcodeStuff;

namespace FirstPersonView;

internal static class Players
{
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

    public static bool IsHoldingTwoHanded(PlayerControllerB player) => player.twoHanded;
}