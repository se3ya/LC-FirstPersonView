using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class Players
{
    private const int ArmsLayer = 4;

    private static readonly int ReelingUpHash = Animator.StringToHash("reelingUp");
    private static readonly int HitShovelHash = Animator.StringToHash("HitShovel");
    private static readonly int HoldLungHash = Animator.StringToHash("HoldLungApparatice");

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

    public static bool IsReelingUp(PlayerControllerB player)
    {
        Animator? animator = player.playerBodyAnimator;
        return animator != null && animator.GetBool(ReelingUpHash);
    }

    public static bool IsSwingingShovelLike(PlayerControllerB player)
    {
        if (IsReelingUp(player))
            return true;

        if (!player.activatingItem)
            return false;

        Animator? animator = player.playerBodyAnimator;
        return player.currentlyHeldObjectServer is Shovel || (animator != null && IsInHitShovel(animator));
    }

    private static bool IsInHitShovel(Animator animator)
    {
        if (animator.layerCount <= ArmsLayer)
            return false;

        if (animator.GetCurrentAnimatorStateInfo(ArmsLayer).shortNameHash == HitShovelHash)
            return true;

        return animator.IsInTransition(ArmsLayer)
            && animator.GetNextAnimatorStateInfo(ArmsLayer).shortNameHash == HitShovelHash;
    }

    public static bool IsInHoldLung(PlayerControllerB player)
    {
        Animator? animator = player.playerBodyAnimator;
        if (animator == null || animator.layerCount <= ArmsLayer)
            return false;

        return !animator.IsInTransition(ArmsLayer)
            && animator.GetCurrentAnimatorStateInfo(ArmsLayer).shortNameHash == HoldLungHash;
    }

    public static void GetHeldPose(Transform hand, Item properties, out Vector3 position, out Quaternion rotation)
    {
        rotation = hand.rotation * Quaternion.Euler(properties.rotationOffset);
        position = hand.position + (hand.rotation * properties.positionOffset);
    }
}
