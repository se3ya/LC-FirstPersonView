using GameNetcodeStuff;

namespace FirstPersonView;

internal static class MovementArmsGate
{
    private const float WallWeightThreshold = 0.01f;

    public static bool Update(LocalBodyState state, PlayerControllerB player, float deltaTime)
    {
        bool qualifies =
            (ConfigManager.VanillaArmsWhileSprinting.Value && player.isSprinting)
            || (ConfigManager.VanillaArmsWhileCrouching.Value && player.isCrouching)
            || (ConfigManager.VanillaArmsWhileJumping.Value && (player.isJumping || player.isFallingFromJump))
            || (ConfigManager.VanillaArmsWhileWalking.Value && player.isWalking && !player.isSprinting)
            || (ConfigManager.VanillaArmsWhileEmoting.Value && player.performingEmote)
            || (ConfigManager.VanillaArmsNearWall.Value && player.handsOnWallWeight > WallWeightThreshold);

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