using System.Collections.Generic;

using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonView;

internal static class HeldItemCopy
{
    private sealed class Hidden
    {
        public MeshRenderer Renderer = null!;
        public ShadowCastingMode Shadows;
        public bool WasOff;
        public bool Written;
    }

    private sealed class Part
    {
        public Hidden Source = null!;
        public MeshFilter SourceFilter = null!;
        public MeshRenderer Shadow = null!;
        public MeshFilter ShadowFilter = null!;
        public MeshRenderer View = null!;
        public MeshFilter ViewFilter = null!;
    }

    private static readonly List<Hidden> HiddenList = new();
    private static readonly List<Part> Parts = new();
    private static readonly HashSet<MeshRenderer> LodZero = new();
    private static readonly HashSet<MeshRenderer> InAnyLod = new();
    private static readonly List<Material> SourceMaterials = new();
    private static readonly List<Material> CopyMaterials = new();
    private static GrabbableObject? item;
    private static int skippedId;
    private static PlayerControllerB? holder;
    private static Camera? firstPersonCamera;
    private static GameObject? root;
    private static bool inFrame;
    private static bool captured;

    public static void Track(
        LocalBodyState state, PlayerControllerB player, GrabbableObject? held, bool showBody, bool useVanillaArms)
    {
        bool wanted = ConfigManager.ItemInBodyHand.Value
            && showBody
            && useVanillaArms
            && held != null;

        GrabbableObject? target = wanted ? held : null;
        if (!ReferenceEquals(target, item) || (target != null && root == null))
        {
            Clear();
            if (target != null && target.GetInstanceID() != skippedId)
                Build(target, player);
        }

        firstPersonCamera = state.GameplayCamera;
    }

    public static void Reset()
    {
        Clear();
        skippedId = 0;
        firstPersonCamera = null;
    }

    public static void Place()
    {
        if (item == null || holder == null || root == null)
            return;

        Transform? hand = holder.serverItemHolder;
        Item? properties = item.itemProperties;
        if (hand == null || properties == null)
            return;

        Players.GetHeldPose(hand, properties, out Vector3 position, out Quaternion rotation);
        root.transform.SetPositionAndRotation(position, rotation);
        root.transform.localScale = item.transform.lossyScale;

        Matrix4x4 toItem = item.transform.worldToLocalMatrix;
        foreach (Part part in Parts)
        {
            MeshRenderer source = part.Source.Renderer;
            if (source == null)
                continue;

            Matrix4x4 local = toItem * source.transform.localToWorldMatrix;
            SetPose(part.Shadow.transform, local);
            SetPose(part.View.transform, local);
            Sync(part, source);
        }
    }

    public static void BeginFrame()
    {
        if (inFrame)
            EndFrame();

        if (item == null || root == null)
        {
            if (root != null)
                root.SetActive(false);
            return;
        }

        inFrame = true;
        captured = false;
    }

    private static void Capture()
    {
        captured = true;
        foreach (Hidden hidden in HiddenList)
        {
            if (hidden.Renderer == null)
                continue;

            hidden.WasOff = hidden.Renderer.forceRenderingOff;
            hidden.Shadows = hidden.Renderer.shadowCastingMode;
            hidden.Renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (Part part in Parts)
        {
            MeshRenderer source = part.Source.Renderer;
            bool visible = source != null
                && source.enabled
                && source.gameObject.activeInHierarchy
                && !part.Source.WasOff;
            ShadowCastingMode shadows = part.Source.Shadows;
            part.Shadow.enabled = visible && shadows != ShadowCastingMode.Off;
            part.View.enabled = visible && shadows != ShadowCastingMode.ShadowsOnly;
        }
    }

    public static void ApplyForCamera(Camera camera)
    {
        if (item == null || root == null || !inFrame)
            return;

        if (!captured)
            Capture();

        bool original = (camera == firstPersonCamera && FirstPersonBody.HideActive) || camera.transform.IsChildOf(item.transform);
        foreach (Hidden hidden in HiddenList)
        {
            if (hidden.Renderer == null)
                continue;

            hidden.Written = original ? hidden.WasOff : true;
            hidden.Renderer.forceRenderingOff = hidden.Written;
        }

        foreach (Part part in Parts)
            part.View.forceRenderingOff = original;
    }

    public static void EndFrame()
    {
        if (!inFrame)
            return;

        inFrame = false;
        if (!captured)
            return;

        captured = false;
        foreach (Hidden hidden in HiddenList)
        {
            if (hidden.Renderer == null)
                continue;

            if (hidden.Renderer.forceRenderingOff == hidden.Written)
                hidden.Renderer.forceRenderingOff = hidden.WasOff;

            hidden.Renderer.shadowCastingMode = hidden.Shadows;
        }
    }

    private static void Clear()
    {
        EndFrame();
        HiddenList.Clear();
        Parts.Clear();
        if (root != null)
            Object.Destroy(root);

        root = null;
        item = null;
        holder = null;
    }

    private static void Build(GrabbableObject target, PlayerControllerB player)
    {
        if (!CanCopy(target))
        {
            skippedId = target.GetInstanceID();
            return;
        }

        try
        {
            CollectLods(target);
            root = new GameObject("FPVHeldItemCopy");
            item = target;
            holder = player;

            foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                int layer = renderer.gameObject.layer;
                if (layer == Constants.Layers.RadarDot || layer == Constants.Layers.ScanNode)
                    continue;

                Hidden hidden = new() { Renderer = renderer };
                HiddenList.Add(hidden);

                bool copied = LodZero.Contains(renderer) || !InAnyLod.Contains(renderer);
                MeshFilter? filter = renderer.GetComponent<MeshFilter>();
                if (!copied || filter == null || filter.sharedMesh == null)
                    continue;

                Parts.Add(new Part
                {
                    Source = hidden,
                    SourceFilter = filter,
                    Shadow = MakeCopy(renderer, filter, ShadowCastingMode.ShadowsOnly, out MeshFilter shadowFilter),
                    ShadowFilter = shadowFilter,
                    View = MakeCopy(renderer, filter, ShadowCastingMode.Off, out MeshFilter viewFilter),
                    ViewFilter = viewFilter,
                });
            }
        }
        finally
        {
            LodZero.Clear();
            InAnyLod.Clear();
        }
    }

    private static bool CanCopy(GrabbableObject target)
    {
        foreach (Camera camera in target.GetComponentsInChildren<Camera>(true))
        {
            if (!camera.orthographic)
                return false;
        }

        foreach (SkinnedMeshRenderer skinned in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.enabled)
                return false;
        }

        return true;
    }

    private static void CollectLods(GrabbableObject target)
    {
        LodZero.Clear();
        InAnyLod.Clear();

        foreach (LODGroup group in target.GetComponentsInChildren<LODGroup>(true))
        {
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                foreach (Renderer renderer in lods[i].renderers)
                {
                    if (renderer is not MeshRenderer mesh)
                        continue;

                    InAnyLod.Add(mesh);
                    if (i == 0)
                        LodZero.Add(mesh);
                }
            }
        }
    }

    private static MeshRenderer MakeCopy(MeshRenderer source, MeshFilter sourceFilter, ShadowCastingMode shadows, out MeshFilter filter)
    {
        GameObject part = new(source.name);
        part.layer = source.gameObject.layer;
        part.transform.SetParent(root!.transform, false);

        filter = part.AddComponent<MeshFilter>();
        filter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer copy = part.AddComponent<MeshRenderer>();
        copy.sharedMaterials = source.sharedMaterials;
        copy.shadowCastingMode = shadows;
        copy.receiveShadows = source.receiveShadows;
        copy.renderingLayerMask = source.renderingLayerMask;
        copy.lightProbeUsage = source.lightProbeUsage;
        copy.reflectionProbeUsage = source.reflectionProbeUsage;
        copy.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        copy.enabled = false;
        return copy;
    }

    private static void SetPose(Transform copy, Matrix4x4 local)
    {
        copy.localPosition = local.GetColumn(3);
        copy.localRotation = local.rotation;
        copy.localScale = local.lossyScale;
    }

    private static void Sync(Part part, MeshRenderer source)
    {
        Mesh? mesh = part.SourceFilter != null ? part.SourceFilter.sharedMesh : null;
        if (part.ShadowFilter.sharedMesh != mesh)
        {
            part.ShadowFilter.sharedMesh = mesh;
            part.ViewFilter.sharedMesh = mesh;
        }

        source.GetSharedMaterials(SourceMaterials);
        part.Shadow.GetSharedMaterials(CopyMaterials);
        if (SameMaterials())
            return;

        Material[] materials = SourceMaterials.ToArray();
        part.Shadow.sharedMaterials = materials;
        part.View.sharedMaterials = materials;
    }

    private static bool SameMaterials()
    {
        if (SourceMaterials.Count != CopyMaterials.Count)
            return false;

        for (int i = 0; i < SourceMaterials.Count; i++)
        {
            if (SourceMaterials[i] != CopyMaterials[i])
                return false;
        }

        return true;
    }
}
