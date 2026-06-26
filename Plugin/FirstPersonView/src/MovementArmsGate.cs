using GameNetcodeStuff;

namespace FirstPersonView;

// decides whether vanilla first-person arms should show for the third-person body arms
internal static class MovementArmsGate
{
    public static bool Update(LocalBodyState state, PlayerControllerB player, float deltaTime)
    {
        bool qualifies = player.isSprinting
            || player.isCrouching
            || player.isJumping
            || player.isFallingFromJump;

        if (qualifies == state.MovementArmsActive)
        {
            state.MovementArmsTimer = 0f;
        }
        else
        {
            state.MovementArmsTimer += deltaTime;
            float threshold = qualifies ? Constants.ArmSwapEngageDelay : Constants.ArmSwapReleaseDelay;
            if (state.MovementArmsTimer >= threshold)
            {
                state.MovementArmsActive = qualifies;
                state.MovementArmsTimer = 0f;
            }
        }

        return state.MovementArmsActive;
    }

    public static void Reset(LocalBodyState state)
    {
        state.MovementArmsActive = false;
        state.MovementArmsTimer = 0f;
    }
}
