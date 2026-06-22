using UnityEngine;

namespace FirstPersonView
{
    internal static class VisorRig
    {
        private static bool? immersiveVisorPresent;

        public static bool IsImmersiveVisorPresent()
        {
            immersiveVisorPresent ??=
                BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(Metadata.ImmersiveVisorGUID);
            return immersiveVisorPresent.Value;
        }

        public static void StickToCamera(LocalBodyState state)
        {
            if (state.VisorReparented)
                return;

            Transform? visor = state.LocalVisor;
            Camera? camera = state.GameplayCamera;
            if (visor == null || camera == null)
                return;

            visor.SetParent(camera.transform, worldPositionStays: true);
            state.VisorReparented = true;
        }

        public static void Restore(LocalBodyState state)
        {
            if (!state.VisorReparented)
                return;

            if (state.LocalVisor != null && state.VisorOriginalParentCaptured)
                state.LocalVisor.SetParent(state.VisorOriginalParent, worldPositionStays: true);

            state.VisorReparented = false;
        }

        public static void SuppressVanillaCrack(LocalBodyState state, bool hide)
        {
            if (hide == state.CrackHidden)
                return;

            if (state.CrackRenderers == null)
            {
                GameObject? crackObject = HUDManager.Instance != null ? HUDManager.Instance.visorCracksObject : null;
                if (crackObject == null)
                    return;

                state.CrackRenderers = crackObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            foreach (Renderer renderer in state.CrackRenderers)
            {
                if (renderer != null)
                    renderer.enabled = !hide;
            }

            state.CrackHidden = hide;
        }
    }
}