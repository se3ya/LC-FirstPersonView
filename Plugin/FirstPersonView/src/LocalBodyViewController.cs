using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using FirstPersonView.Compat;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonView
{
    internal static class LocalBodyViewController
    {
        private static readonly Dictionary<int, LocalBodyState> States = new();
        private static bool renderCallbacksHooked;

        private static readonly ProfilerMarker TickMarker = new("FirstPersonView.Tick");
        private static readonly ProfilerMarker RenderMarker = new("FirstPersonView.PerCamera");

        internal static bool LocalBodyShown;

        public static void ResetAllStates()
        {
            foreach (LocalBodyState state in States.Values)
            {
                FirstPersonBody.RestoreHead(state);
                VisorRig.Restore(state);
                CameraRig.RestoreOffset(state);

                if (state.ShadowProxy != null)
                    UnityEngine.Object.Destroy(state.ShadowProxy.gameObject);
            }

            States.Clear();
            LocalBodyShown = false;

            FirstPersonBody.Reset();
            HeldItemView.Reset();
            MeshSurgery.ClearCaches();

            if (renderCallbacksHooked)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                renderCallbacksHooked = false;
            }
        }

        public static void Tick(PlayerControllerB player)
        {
            if (!Players.IsLocal(player))
                return;

            using ProfilerMarker.AutoScope _ = TickMarker.Auto();

            EnsureRenderCallbacksHooked();

            LocalBodyState state = GetOrCreateState(player);
            if (NeedsRefresh(state, player))
                RefreshState(state, player);

            bool showBody = player.isPlayerControlled && !player.isPlayerDead;
            LocalBodyShown = showBody;

            bool hasReplacement = showBody && ModelReplacementCompat.HasReplacement(player);

            bool useVanillaArms = showBody && !hasReplacement && Players.IsActivelyHolding(player)
                && !player.inSpecialInteractAnimation
                && ConfigManager.Hands.Value == HandsMode.Vanilla;

            if (!hasReplacement)
                FirstPersonBody.ApplyBodyRendering(state, showBody, useVanillaArms);
            FirstPersonBody.ApplyFirstPersonCameraLayer(state, showBody);
            HeldItemView.ApplyHolder(state, player, showBody, useVanillaArms);

            if (showBody && !hasReplacement)
                FirstPersonBody.HideHead(state, useVanillaArms);
            else
                FirstPersonBody.RestoreHead(state);
            ModelReplacementCompat.SetLocalHeadHidden(player, hasReplacement);

            if (showBody && !VisorRig.IsImmersiveVisorPresent())
                VisorRig.StickToCamera(state);
            else
                VisorRig.Restore(state);

            VisorRig.SuppressVanillaCrack(state, VisorRig.IsImmersiveVisorPresent());

            if (showBody && !player.inTerminalMenu)
                CameraRig.ApplyOffset(state, player);
            else
                CameraRig.RestoreOffset(state);

            CameraRig.ApplyArmAnchor(player, useVanillaArms);
            CameraRig.RelaxSeatedLookClamp(player);

            MoreCompanyCompat.ApplyLocalCosmeticVisibility(player, showBody, useVanillaArms);
        }

        public static void AlignCameraForInteractionRay(PlayerControllerB player)
        {
            if (!Players.IsLocal(player))
                return;
            if (!States.TryGetValue(player.GetInstanceID(), out LocalBodyState? state))
                return;

            CameraRig.AlignForInteractionRay(state);
        }

        private static LocalBodyState GetOrCreateState(PlayerControllerB player)
        {
            int key = player.GetInstanceID();
            if (!States.TryGetValue(key, out LocalBodyState? state))
            {
                state = new LocalBodyState();
                States[key] = state;
            }

            return state;
        }

        private static bool NeedsRefresh(LocalBodyState state, PlayerControllerB player)
        {
            return state.Player != player
                || state.ModelRoot == null
                || state.HeadBone == null
                || state.BodyRenderer == null;
        }

        private static void RefreshState(LocalBodyState state, PlayerControllerB player)
        {
            state.Player = player;

            Transform? scavengerModel = player.transform.Find(Constants.ScavengerModelName);
            state.ModelRoot = scavengerModel != null ? scavengerModel : player.transform;

            state.HeadBone = FindHeadBone(state.ModelRoot, player);

            state.LeftArmBone = FindBodyBone(player, Constants.LeftArmBoneName);
            state.RightArmBone = FindBodyBone(player, Constants.RightArmBoneName);

            state.BodyRenderer = player.thisPlayerModel;
            if (state.BodyRenderer != null && !state.OriginalBodyMeshCaptured && !state.HeadHidden)
            {
                Mesh current = state.BodyRenderer.sharedMesh;
                if (current != null && !MeshSurgery.IsGenerated(current.GetInstanceID()))
                {
                    state.OriginalBodyMesh = current;
                    state.OriginalBodyMeshCaptured = true;
                }
            }

            state.LocalVisor = player.localVisor;
            if (state.LocalVisor != null && !state.VisorOriginalParentCaptured && !state.VisorReparented)
            {
                state.VisorOriginalParent = state.LocalVisor.parent;
                state.VisorOriginalParentCaptured = true;
            }

            state.GameplayCamera = player.gameplayCamera;
            if (state.GameplayCamera != null && !state.CameraBaseCaptured && !state.CameraOffsetApplied)
            {
                state.CameraBaseLocalPosition = state.GameplayCamera.transform.localPosition;
                state.CameraBaseCaptured = true;
            }
        }

        private static Transform? FindHeadBone(Transform modelRoot, PlayerControllerB player)
        {
            SkinnedMeshRenderer? bodyMesh = player.thisPlayerModel;
            if (bodyMesh != null)
            {
                foreach (Transform bone in bodyMesh.bones)
                {
                    if (bone != null && NameContains(bone.name, Constants.HeadBoneName))
                        return bone;
                }
            }

            return FindFirstByNameContains(modelRoot, Constants.HeadBoneName)
                ?? FindFirstByNameContains(modelRoot, "head");
        }

        private static Transform? FindBodyBone(PlayerControllerB player, string boneName)
        {
            SkinnedMeshRenderer? bodyMesh = player.thisPlayerModel;
            if (bodyMesh == null)
                return null;

            foreach (Transform bone in bodyMesh.bones)
            {
                if (bone != null && string.Equals(bone.name, boneName, StringComparison.OrdinalIgnoreCase))
                    return bone;
            }

            return null;
        }

        private static Transform? FindFirstByNameContains(Transform root, string token)
        {
            Queue<Transform> queue = new();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                Transform current = queue.Dequeue();
                if (NameContains(current.name, token))
                    return current;

                for (int i = 0; i < current.childCount; i++)
                    queue.Enqueue(current.GetChild(i));
            }

            return null;
        }

        private static bool NameContains(string name, string token) =>
            name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void EnsureRenderCallbacksHooked()
        {
            if (renderCallbacksHooked)
                return;

            renderCallbacksHooked = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            using ProfilerMarker.AutoScope _ = RenderMarker.Auto();
            FirstPersonBody.ApplyForCamera(camera);
            HeldItemView.ApplyForCamera(camera);
        }

        private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            FirstPersonBody.RestoreAfterCamera(camera);
            HeldItemView.RestoreAfterCamera(camera);
        }
    }
}