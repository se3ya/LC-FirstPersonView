using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class SwingReturn
{
    private const int ArmsLayer = 4;
    private const string GrabAnim = "HoldLung";

    private static readonly int HitShovelHash = Animator.StringToHash("HitShovel");
    private static readonly int HitShovelCollideHash = Animator.StringToHash("HitShovelCollide");
    private static readonly int HoldHash = Animator.StringToHash("HoldLungApparatice");

    public static void Apply(LocalBodyState state, PlayerControllerB player)
    {
        GrabbableObject? held = player.currentlyHeldObjectServer;
        if (!ConfigManager.SmoothSwingReturn.Value
            || !player.isPlayerControlled
            || player.isPlayerDead
            || held == null
            || held.itemProperties == null
            || held.itemProperties.grabAnim != GrabAnim)
        {
            state.SwingReturnStarted = false;
            return;
        }

        Animator? animator = player.playerBodyAnimator;
        if (animator == null || animator.layerCount <= ArmsLayer)
            return;

        if (animator.IsInTransition(ArmsLayer))
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(ArmsLayer);
        bool hit = current.shortNameHash == HitShovelHash;
        bool collide = current.shortNameHash == HitShovelCollideHash;

        if (!hit && !collide)
        {
            state.SwingReturnStarted = false;
            return;
        }

        if (state.SwingReturnStarted)
            return;

        float start = hit ? Constants.Swing.ReturnHit : Constants.Swing.ReturnCollide;
        if (current.normalizedTime < start)
            return;

        animator.CrossFadeInFixedTime(HoldHash, Constants.Swing.ReturnBlendTime, ArmsLayer);
        state.SwingReturnStarted = true;
    }
}
