using System.Collections.Generic;
using System;

using UnityEngine.Rendering;
using UnityEngine;

namespace FirstPersonView;

internal static class CosmeticBodyView
{
    private sealed class RiggedBody
    {
        public SkinnedMeshRenderer Renderer = null!;
        public Mesh Original = null!;
        public Mesh Headless = null!;
        public Mesh? HeadlessArmless;
        public bool OriginalUpdateWhenOffscreen;
        public LODGroup? GoverningLodGroup;
        public SkinnedMeshRenderer? ShadowProxy;
    }

    internal struct RiggedBodyCandidate
    {
        public SkinnedMeshRenderer Renderer;
        public Transform HeadBone;
        public Transform? LeftArmBone;
        public Transform? RightArmBone;
    }

    private static readonly List<RiggedBody> Bodies = new();
    private static readonly HashSet<int> RejectedMeshes = new();

    private static Camera? firstPersonCamera;
    private static bool hideArms;
    private static bool active;

    public static bool Active => active;

    public static void SetFrameState(Camera? camera, bool useVanillaArms)
    {
        firstPersonCamera = camera;
        hideArms = useVanillaArms;
    }

    public static void Rebuild(List<RiggedBodyCandidate> candidates)
    {
        for (int i = Bodies.Count - 1; i >= 0; i--)
        {
            RiggedBody existing = Bodies[i];
            if (existing.Renderer == null || existing.Original == null || existing.Headless == null
                || !ContainsRenderer(candidates, existing.Renderer))
            {
                ReleaseBody(existing);
                Bodies.RemoveAt(i);
            }
        }

        foreach (RiggedBodyCandidate candidate in candidates)
        {
            try
            {
                TryTrackCandidate(candidate);
            }
            catch (Exception ex)
            {
                RejectCandidate(candidate, ex);
            }
        }

        active = Bodies.Count > 0;

        foreach (RiggedBody body in Bodies)
            UpdateShadowProxy(body);
    }

    private static void TryTrackCandidate(RiggedBodyCandidate candidate)
    {
        SkinnedMeshRenderer renderer = candidate.Renderer;
        if (renderer == null || candidate.HeadBone == null || FindBody(renderer) != null)
            return;

        Mesh source = renderer.sharedMesh;
        if (source == null || MeshSurgery.IsGenerated(source.GetInstanceID())
            || RejectedMeshes.Contains(source.GetInstanceID()))
            return;

        Mesh? headless = MeshSurgery.GetOrCreateHeadlessMesh(
            source, renderer, candidate.HeadBone);

        if (headless == null || !RemovesGeometry(source, headless))
        {
            if (RejectedMeshes.Add(source.GetInstanceID()))
                Plugin.Log.LogWarning(
                    $"Mesh '{source.name}' is rigged to head bone but no geometry could be removed from it so its head will not be hidden in first person.");
            return;
        }

        Mesh? headlessArmless = MeshSurgery.GetOrCreateHeadlessArmlessMesh(
            source, renderer, candidate.HeadBone, candidate.LeftArmBone, candidate.RightArmBone);

        RiggedBody body = new()
        {
            Renderer = renderer,
            Original = source,
            Headless = headless,
            HeadlessArmless = headlessArmless,
            OriginalUpdateWhenOffscreen = renderer.updateWhenOffscreen,
        };

        if (!renderer.updateWhenOffscreen && renderer.bounds.size.sqrMagnitude < 1e-6f)
        {
            renderer.updateWhenOffscreen = true;
            Plugin.Log.LogInfo(
                $"Cosmetic '{renderer.name}' had no usable bounds so it was culled from most cameras. Its bounds are now recalculated every frame.");
        }

        body.GoverningLodGroup = FindGoverningLodGroup(renderer);
        if (body.GoverningLodGroup != null)
        {
            body.GoverningLodGroup.ForceLOD(0);
            Plugin.Log.LogInfo(
                $"Cosmetic '{renderer.name}' is managed by its own level of detail group which culled it on cameras that were far enough away.");
        }

        Bodies.Add(body);
    }

    private static LODGroup? FindGoverningLodGroup(SkinnedMeshRenderer renderer)
    {
        Transform root = renderer.transform.root;

        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
        {
            foreach (LOD lod in group.GetLODs())
            {
                foreach (Renderer member in lod.renderers)
                {
                    if (ReferenceEquals(member, renderer))
                        return group;
                }
            }
        }

        return null;
    }

    private static void RejectCandidate(RiggedBodyCandidate candidate, Exception ex)
    {
        SkinnedMeshRenderer renderer = candidate.Renderer;
        Mesh? source = renderer != null ? renderer.sharedMesh : null;
        if (source == null)
            return;

        if (RejectedMeshes.Add(source.GetInstanceID()))
            Plugin.Log.LogWarning($"Mesh '{source.name}' could not be get ready for first person so its head will stay visible. {ex.Message}");
    }

    private static bool ContainsRenderer(List<RiggedBodyCandidate> candidates, SkinnedMeshRenderer renderer)
    {
        foreach (RiggedBodyCandidate candidate in candidates)
        {
            if (ReferenceEquals(candidate.Renderer, renderer))
                return true;
        }

        return false;
    }

    private static bool RemovesGeometry(Mesh source, Mesh headless)
    {
        if (headless.subMeshCount != source.subMeshCount)
            return true;

        for (int i = 0; i < source.subMeshCount; i++)
        {
            if (headless.GetIndexCount(i) != source.GetIndexCount(i))
                return true;
        }

        return false;
    }

    public static bool OwnsRenderer(Renderer renderer)
    {
        foreach (RiggedBody body in Bodies)
        {
            if (ReferenceEquals(body.Renderer, renderer))
                return true;
        }

        return false;
    }

    private static RiggedBody? FindBody(SkinnedMeshRenderer renderer)
    {
        foreach (RiggedBody body in Bodies)
        {
            if (ReferenceEquals(body.Renderer, renderer))
                return body;
        }

        return null;
    }

    public static void ApplyForCamera(Camera camera)
    {
        if (!active)
            return;

        bool firstPerson = camera == firstPersonCamera && FirstPersonBody.HideActive;

        foreach (RiggedBody body in Bodies)
        {
            SkinnedMeshRenderer renderer = body.Renderer;
            if (renderer == null)
                continue;

            if (firstPerson)
            {
                Mesh fpMesh = (hideArms && body.HeadlessArmless != null) ? body.HeadlessArmless : body.Headless;
                if (fpMesh != null && renderer.sharedMesh != fpMesh)
                    renderer.sharedMesh = fpMesh;
            }
            else if (body.Original != null && renderer.sharedMesh != body.Original)
            {
                renderer.sharedMesh = body.Original;
            }
        }
    }

    public static void RestoreAfterCamera(Camera camera)
    {
        if (!active)
            return;

        if (camera != firstPersonCamera)
            return;

        foreach (RiggedBody body in Bodies)
            RestoreBody(body);
    }

    private static void RestoreBody(RiggedBody body)
    {
        SkinnedMeshRenderer renderer = body.Renderer;
        if (renderer == null || body.Original == null || renderer.sharedMesh == body.Original)
            return;

        renderer.sharedMesh = body.Original;
    }

    private static void ReleaseBody(RiggedBody body)
    {
        RestoreBody(body);

        if (body.Renderer != null)
            body.Renderer.updateWhenOffscreen = body.OriginalUpdateWhenOffscreen;

        if (body.GoverningLodGroup != null)
        {
            body.GoverningLodGroup.ForceLOD(-1);
            body.GoverningLodGroup = null;
        }

        if (body.ShadowProxy != null)
            UnityEngine.Object.Destroy(body.ShadowProxy.gameObject);

        body.ShadowProxy = null;
    }

    public static void RestoreAll()
    {
        foreach (RiggedBody body in Bodies)
            ReleaseBody(body);

        Bodies.Clear();
        active = false;
    }

    public static void Reset()
    {
        RestoreAll();
        RejectedMeshes.Clear();
        firstPersonCamera = null;
        hideArms = false;
    }

    private static void UpdateShadowProxy(RiggedBody body)
    {
        SkinnedMeshRenderer renderer = body.Renderer;
        if (renderer == null)
            return;

        if (body.ShadowProxy == null)
        {
            try
            {
                GameObject proxyObject = new("FPVCosmeticShadowProxy");
                proxyObject.transform.SetParent(renderer.transform.parent, worldPositionStays: false);
                proxyObject.transform.localPosition = renderer.transform.localPosition;
                proxyObject.transform.localRotation = renderer.transform.localRotation;
                proxyObject.transform.localScale = renderer.transform.localScale;

                SkinnedMeshRenderer proxy = proxyObject.AddComponent<SkinnedMeshRenderer>();
                proxy.sharedMesh = body.Original;
                proxy.bones = renderer.bones;
                proxy.rootBone = renderer.rootBone;
                proxy.sharedMaterials = renderer.sharedMaterials;
                proxy.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                proxy.updateWhenOffscreen = true;
                body.ShadowProxy = proxy;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to create cosmetic shadow proxy. {ex.Message}");
                return;
            }
        }

        body.ShadowProxy.gameObject.layer = renderer.gameObject.layer;
        if (body.ShadowProxy.enabled != renderer.enabled)
            body.ShadowProxy.enabled = renderer.enabled;
    }
}
