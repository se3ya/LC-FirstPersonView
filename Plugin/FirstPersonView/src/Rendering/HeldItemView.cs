using System;
using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView
{
    internal static class HeldItemView
    {
        private const int CoverageGridSize = 16;        // screen diced into this many rows/columns for coverage
        private static readonly int[] coverageRowMin = new int[CoverageGridSize];
        private static readonly int[] coverageRowMax = new int[CoverageGridSize];

        private static GrabbableObject? trackedItem;
        private static Renderer[] renderers = Array.Empty<Renderer>();
        private static Material[] materials = Array.Empty<Material>();
        private static Renderer? boundsSource;
        private static Vector3[] localVertices = Array.Empty<Vector3>();
        private static Camera? camera;
        private static bool hideActive;
        private static float fadeCurrent = 1f;      // low-passed fade smoothed toward the per frame target

        public static void ApplyHolder(LocalBodyState state, PlayerControllerB player, bool showBody, bool useVanillaArms)
        {
            GrabbableObject? held = player.currentlyHeldObjectServer;
            bool actuallyHeld = Players.IsActivelyHolding(player);

            bool inspecting = player.IsInspectingItem;

            if (actuallyHeld && held != null)
            {
                Transform? holder = (useVanillaArms || inspecting || !showBody)
                    ? player.localItemHolder
                    : player.serverItemHolder;
                if (holder != null && held.parentObject != holder)
                    held.parentObject = holder;
            }

            bool wantFade = DitherEnabled();

            GrabbableObject? tracked = actuallyHeld ? held : null;
            if (tracked != trackedItem)
            {
                ResetEffect();
                trackedItem = tracked;
                renderers = tracked != null
                    ? tracked.GetComponentsInChildren<Renderer>()
                    : Array.Empty<Renderer>();
                materials = wantFade ? MaterialDither.CollectInstancedMaterials(renderers) : Array.Empty<Material>();
                boundsSource = ResolveBoundsSource(tracked, renderers);
                localVertices = ResolveLocalVertices(boundsSource);
            }
            else if (wantFade && materials.Length == 0 && renderers.Length > 0)
            {
                materials = MaterialDither.CollectInstancedMaterials(renderers);
            }

            camera = state.GameplayCamera;
            hideActive = showBody && tracked != null && !inspecting;
        }

        public static void ApplyForCamera(Camera renderingCamera)
        {
            if (!hideActive || renderers.Length == 0)
                return;

            if (!DitherEnabled())
                return;

            float fade = renderingCamera == camera ? ComputeFade(renderingCamera) : 1f;
            ApplyFade(fade);
        }

        // dithers only when Fade Mode is Dither and the current hands mode is one we were told to dither in
        private static bool DitherEnabled()
        {
            if (ConfigManager.HeldItemFadeStyle.Value == HeldItemFadeMode.Off)
                return false;

            ItemDitherMode modes = ConfigManager.ItemDither.Value;
            return ConfigManager.Hands.Value == HandsMode.Vanilla
                ? (modes & ItemDitherMode.Vanilla) != 0
                : (modes & ItemDitherMode.ThirdPerson) != 0;
        }

        public static void RestoreAfterCamera(Camera renderingCamera)
        {
            if (renderers.Length > 0 && renderingCamera == camera)
                ApplyFade(1f);
        }

        private static float ComputeFade(Camera renderingCamera)
        {
            Renderer? source = boundsSource;
            if (source == null)
                return 1f;

            Vector3[] vertices = localVertices;
            Vector3 eye = renderingCamera.transform.position;
            float target;
            if (vertices.Length == 0)   // unreadable mesh, distance only
            {
                target = MaterialDither.ProximityFade(NearestBoxDistance(source, eye));
            }
            else
            {
                Matrix4x4 localToWorld = source.localToWorldMatrix;
                float nearestSqr = float.MaxValue;
                for (int row = 0; row < CoverageGridSize; row++)
                {
                    coverageRowMin[row] = CoverageGridSize;
                    coverageRowMax[row] = -1;
                }

                foreach (Vector3 vertex in vertices)
                {
                    Vector3 world = localToWorld.MultiplyPoint3x4(vertex);

                    float sqr = (world - eye).sqrMagnitude;
                    if (sqr < nearestSqr)
                        nearestSqr = sqr;

                    Vector3 view = renderingCamera.WorldToViewportPoint(world);
                    if (view.z <= 0f)
                        continue;   // behind the eye, occupies no screen

                    int cx = Mathf.Clamp((int)(Mathf.Clamp01(view.x) * CoverageGridSize), 0, CoverageGridSize - 1);
                    int cy = Mathf.Clamp((int)(Mathf.Clamp01(view.y) * CoverageGridSize), 0, CoverageGridSize - 1);
                    if (cx < coverageRowMin[cy]) coverageRowMin[cy] = cx;
                    if (cx > coverageRowMax[cy]) coverageRowMax[cy] = cx;
                }

                int filledCells = 0;
                for (int row = 0; row < CoverageGridSize; row++)
                {
                    if (coverageRowMax[row] >= coverageRowMin[row])
                        filledCells += coverageRowMax[row] - coverageRowMin[row] + 1;
                }

                float coverage = (float)filledCells / (CoverageGridSize * CoverageGridSize);
                target = Mathf.Min(MaterialDither.ProximityFade(Mathf.Sqrt(nearestSqr)), CoverageFade(coverage));
            }

            target = Mathf.Max(Constants.HeldItemMinFade, target);
            fadeCurrent = MaterialDither.Smooth(fadeCurrent, target, Constants.HeldItemFadeSmoothTime);
            return fadeCurrent;
        }

        // Screen coverage
        private static float CoverageFade(float coverage)
        {
            return 1f - Mathf.Clamp01(Mathf.InverseLerp(
                Constants.HeldItemCoverageFadeStart, Constants.HeldItemCoverageFadeFull, coverage));
        }

        // distance from a world point to renderers oriented box
        private static float NearestBoxDistance(Renderer renderer, Vector3 worldPoint)
        {
            Vector3 localPoint = renderer.worldToLocalMatrix.MultiplyPoint3x4(worldPoint);
            Vector3 closestLocal = renderer.localBounds.ClosestPoint(localPoint);
            Vector3 closestWorld = renderer.localToWorldMatrix.MultiplyPoint3x4(closestLocal);
            return Vector3.Distance(worldPoint, closestWorld);
        }

        private static Vector3[] ResolveLocalVertices(Renderer? source)
        {
            Mesh? mesh = source switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                null => null,
                _ => source.GetComponent<MeshFilter>()?.sharedMesh,
            };

            return mesh != null && mesh.isReadable ? mesh.vertices : Array.Empty<Vector3>();
        }

        private static Renderer? ResolveBoundsSource(GrabbableObject? item, Renderer[] itemRenderers)
        {
            if (item == null)
                return null;

            if (item.mainObjectRenderer != null)
                return item.mainObjectRenderer;

            foreach (Renderer renderer in itemRenderers)
            {
                if (renderer != null)
                    return renderer;
            }

            return null;
        }

        private static void ApplyFade(float fade)
        {
            MaterialDither.Set(renderers, materials, fade);
        }

        private static void ResetEffect()
        {
            MaterialDither.Set(renderers, materials, 1f);
            fadeCurrent = 1f;
        }

        public static void Reset()
        {
            ResetEffect();
            trackedItem = null;
            renderers = Array.Empty<Renderer>();
            materials = Array.Empty<Material>();
            boundsSource = null;
            localVertices = Array.Empty<Vector3>();
            camera = null;
            hideActive = false;
        }
    }
}