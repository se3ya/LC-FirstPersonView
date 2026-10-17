using System;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class HeldItemView
{
    private const int CoverageGridSize = 16;
    private static readonly int[] coverageRowMin = new int[CoverageGridSize];
    private static readonly int[] coverageRowMax = new int[CoverageGridSize];

    private static GrabbableObject? trackedItem;
    private static Renderer[] renderers = Array.Empty<Renderer>();
    private static bool[] hadBlock = Array.Empty<bool>();
    private static MotionVectorGenerationMode[] originalMotionModes = Array.Empty<MotionVectorGenerationMode>();
    private static Material[] materials = Array.Empty<Material>();
    private static bool materialsResolved;
    private static Renderer? boundsSource;
    private static Vector3[] localVertices = Array.Empty<Vector3>();
    private static Camera? camera;
    private static bool hideActive;
    private static bool vanillaArms;
    private static bool fadeApplied;
    private static float fadeCurrent = 1f;

    public static void SetHolder(PlayerControllerB player, GrabbableObject held, bool useVanillaArms, bool showBody)
    {
        Transform? holder = useVanillaArms || player.IsInspectingItem || !showBody
            ? player.localItemHolder
            : player.serverItemHolder;
        if (holder != null && held.parentObject != holder)
            held.parentObject = holder;
    }

    public static void ApplyHolder(
        LocalBodyState state, PlayerControllerB player, GrabbableObject? held, bool showBody, bool useVanillaArms)
    {
        vanillaArms = useVanillaArms;

        bool inspecting = player.IsInspectingItem;

        if (held != null)
            SetHolder(player, held, useVanillaArms, showBody);

        bool wantFade = DitherEnabled();

        GrabbableObject? tracked = held;
        if (!ReferenceEquals(tracked, trackedItem))
        {
            ResetEffect();
            RestoreMotionVectors();
            trackedItem = tracked;
            renderers = tracked != null
                ? tracked.GetComponentsInChildren<Renderer>()
                : Array.Empty<Renderer>();
            hadBlock = renderers.Length > 0 ? new bool[renderers.Length] : Array.Empty<bool>();
            SuppressMotionVectors();
            materials = wantFade ? MaterialDither.CollectInstancedMaterials(renderers) : Array.Empty<Material>();
            materialsResolved = wantFade;
            boundsSource = GetBoundsSource(tracked, renderers);
            localVertices = GetLocalVertices(boundsSource);
        }
        else if (wantFade && !materialsResolved && renderers.Length > 0)
        {
            materials = MaterialDither.CollectInstancedMaterials(renderers);
            materialsResolved = true;
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

        float fade = renderingCamera == camera ? GetFade(renderingCamera) : 1f;
        ApplyFade(fade);
    }

    private static bool DitherEnabled()
    {
        if (ConfigManager.HeldItemFadeStyle.Value == HeldItemFadeMode.Off)
            return false;

        ItemDitherMode modes = ConfigManager.ItemDither.Value;
        return vanillaArms
            ? (modes & ItemDitherMode.Vanilla) != 0
            : (modes & ItemDitherMode.ThirdPerson) != 0;
    }

    public static void RestoreAfterCamera(Camera renderingCamera)
    {
        if (renderers.Length > 0 && renderingCamera == camera)
            ApplyFade(1f);
    }

    private static float GetFade(Camera renderingCamera)
    {
        Renderer? source = boundsSource;
        if (source == null)
            return 1f;

        Vector3[] vertices = localVertices;
        Vector3 eye = renderingCamera.transform.position;
        float target;
        if (vertices.Length == 0)
        {
            target = MaterialDither.ProximityFade(NearestBoxDistance(source, eye));
        }
        else
        {
            Matrix4x4 localToWorld = source.localToWorldMatrix;
            Matrix4x4 viewProj = renderingCamera.projectionMatrix * renderingCamera.worldToCameraMatrix;
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

                float w = (viewProj.m30 * world.x) + (viewProj.m31 * world.y) + (viewProj.m32 * world.z) + viewProj.m33;
                if (w <= 0f)
                    continue;

                float vx = ((((viewProj.m00 * world.x) + (viewProj.m01 * world.y) + (viewProj.m02 * world.z) + viewProj.m03) / w) * 0.5f) + 0.5f;
                float vy = ((((viewProj.m10 * world.x) + (viewProj.m11 * world.y) + (viewProj.m12 * world.z) + viewProj.m13) / w) * 0.5f) + 0.5f;
                int cx = Mathf.Clamp((int)(Mathf.Clamp01(vx) * CoverageGridSize), 0, CoverageGridSize - 1);
                int cy = Mathf.Clamp((int)(Mathf.Clamp01(vy) * CoverageGridSize), 0, CoverageGridSize - 1);
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

        target = Mathf.Max(ConfigManager.MinVisibility.Value, target);
        fadeCurrent = MaterialDither.Smooth(fadeCurrent, target, Constants.HeldItem.FadeSmoothTime);
        return fadeCurrent;
    }

    private static float CoverageFade(float coverage)
    {
        float start = ConfigManager.FadeCoverage.Value;
        return 1f - Mathf.Clamp01(Mathf.InverseLerp(start, start + Constants.HeldItem.CoverageFadeRange, coverage));
    }

    private static float NearestBoxDistance(Renderer renderer, Vector3 worldPoint)
    {
        Vector3 localPoint = renderer.worldToLocalMatrix.MultiplyPoint3x4(worldPoint);
        Vector3 closestLocal = renderer.localBounds.ClosestPoint(localPoint);
        Vector3 closestWorld = renderer.localToWorldMatrix.MultiplyPoint3x4(closestLocal);
        return Vector3.Distance(worldPoint, closestWorld);
    }

    private static Vector3[] GetLocalVertices(Renderer? source)
    {
        Mesh? mesh = source switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            null => null,
            _ => source.GetComponent<MeshFilter>()?.sharedMesh,
        };

        return mesh != null && mesh.isReadable ? mesh.vertices : Array.Empty<Vector3>();
    }

    private static Renderer? GetBoundsSource(GrabbableObject? item, Renderer[] itemRenderers)
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
        if (fade >= MaterialDither.Opaque)
        {
            ClearFade();
            return;
        }

        if (!fadeApplied)
            MaterialDither.RecordBlocks(renderers, hadBlock);

        MaterialDither.Set(renderers, materials, fade);
        fadeApplied = true;
    }

    private static void ClearFade()
    {
        if (!fadeApplied)
            return;

        fadeApplied = false;
        MaterialDither.Clear(renderers, materials, hadBlock);
    }

    private static void ResetEffect()
    {
        ClearFade();
        fadeCurrent = 1f;
    }

    private static void SuppressMotionVectors()
    {
        originalMotionModes = renderers.Length > 0
            ? new MotionVectorGenerationMode[renderers.Length]
            : Array.Empty<MotionVectorGenerationMode>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            originalMotionModes[i] = renderer.motionVectorGenerationMode;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }

    private static void RestoreMotionVectors()
    {
        for (int i = 0; i < renderers.Length && i < originalMotionModes.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
                renderer.motionVectorGenerationMode = originalMotionModes[i];
        }
        originalMotionModes = Array.Empty<MotionVectorGenerationMode>();
    }

    public static void Reset()
    {
        ResetEffect();
        RestoreMotionVectors();
        trackedItem = null;
        renderers = Array.Empty<Renderer>();
        hadBlock = Array.Empty<bool>();
        materials = Array.Empty<Material>();
        materialsResolved = false;
        boundsSource = null;
        localVertices = Array.Empty<Vector3>();
        camera = null;
        hideActive = false;
        vanillaArms = false;
    }
}
