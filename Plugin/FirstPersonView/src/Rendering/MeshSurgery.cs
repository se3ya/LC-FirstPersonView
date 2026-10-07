using System.Collections.Generic;
using System;

using UnityEngine.Rendering;
using UnityEngine;

namespace FirstPersonView;

internal static class MeshSurgery
{
    private const float MinCross = 1e-12f;

    private static readonly Dictionary<int, Mesh> HeadlessMeshCache = new();
    private static readonly Dictionary<int, Mesh> HeadlessArmlessMeshCache = new();
    private static readonly Dictionary<int, Mesh> OwnMeshCache = new();
    private static readonly Dictionary<int, Mesh> OwnArmlessMeshCache = new();
    private static readonly Dictionary<int, Mesh> CapMeshCache = new();
    private static readonly Dictionary<int, Mesh?> ReadableCopies = new();
    private static readonly HashSet<int> GeneratedMeshIds = new();
    private static readonly HashSet<int> LoggedUnreadableMeshes = new();
    private static readonly HashSet<int> LoggedCapMeshes = new();
    private static readonly HashSet<int> FailedHeadlessMeshes = new();
    private static readonly HashSet<int> FailedHeadlessArmlessMeshes = new();
    private static readonly HashSet<int> FailedOwnMeshes = new();
    private static readonly HashSet<int> FailedOwnArmlessMeshes = new();

    public static bool IsGenerated(int meshInstanceId) => GeneratedMeshIds.Contains(meshInstanceId);

    private static int DominantBoneIndex(BoneWeight bw)
    {
        int bone = bw.boneIndex0;
        float weight = bw.weight0;
        if (bw.weight1 > weight) { weight = bw.weight1; bone = bw.boneIndex1; }
        if (bw.weight2 > weight) { weight = bw.weight2; bone = bw.boneIndex2; }
        if (bw.weight3 > weight) { weight = bw.weight3; bone = bw.boneIndex3; }
        return weight > 0f ? bone : -1;
    }

    private static bool[]? BuildPipeMask(Mesh mesh, SkinnedMeshRenderer skinned, Transform headBone)
    {
        Transform? neck = headBone.parent;
        Transform[] bones = skinned.bones;
        if (neck == null || mesh.bindposes == null || mesh.bindposes.Length == 0)
            return null;

        int bindposeCount = mesh.bindposes.Length;
        int neckIndex = Array.IndexOf(bones, neck);
        int headIndex = Array.IndexOf(bones, headBone);
        if (neckIndex < 0 || headIndex < 0 || neckIndex >= bindposeCount || headIndex >= bindposeCount)
            return null;

        bool hasForward = TryGetBodyForward(mesh, bones, headIndex, neckIndex, out Vector3 forward);

        Vector3 neckPos = BoneBindPosition(mesh, neckIndex);
        Vector3 neckAxis = BoneBindPosition(mesh, headIndex) - neckPos;
        float neckSpan = neckAxis.magnitude;
        if (neckSpan > 1e-6f)
            neckAxis /= neckSpan;

        float columnStart = neckSpan * Constants.Culling.NeckColumnStart;
        float columnRadius = neckSpan * Constants.Culling.NeckColumnRadius;

        BoneWeight[] weights = mesh.boneWeights;
        Vector3[] vertices = mesh.vertices;
        if (weights.Length < vertices.Length)
            hasForward = false;

        bool[] mask = new bool[vertices.Length];

        int marked = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 offset = vertices[i] - neckPos;

            if (hasForward
                && DominantBoneIndex(weights[i]) == neckIndex
                && Vector3.Dot(offset, forward) < Constants.Culling.PipeBehindThreshold)
            {
                mask[i] = true;
                marked++;
                continue;
            }

            if (neckSpan <= 1e-6f)
                continue;

            float along = Vector3.Dot(offset, neckAxis);
            if (along < columnStart)
                continue;
            if ((offset - (neckAxis * along)).sqrMagnitude > columnRadius * columnRadius)
                continue;

            mask[i] = true;
            marked++;
        }

        return marked > 0 ? mask : null;
    }

    private static bool[]? BuildColumnMask(Mesh mesh, SkinnedMeshRenderer skinned, Transform headBone)
    {
        Transform? neck = headBone.parent;
        Transform[] bones = skinned.bones;
        if (neck == null || mesh.bindposes == null || mesh.bindposes.Length == 0)
            return null;

        int bindposeCount = mesh.bindposes.Length;
        int neckIndex = Array.IndexOf(bones, neck);
        int headIndex = Array.IndexOf(bones, headBone);
        if (neckIndex < 0 || headIndex < 0 || neckIndex >= bindposeCount || headIndex >= bindposeCount)
            return null;

        Vector3 neckPos = BoneBindPosition(mesh, neckIndex);
        Vector3 neckAxis = BoneBindPosition(mesh, headIndex) - neckPos;
        float neckSpan = neckAxis.magnitude;
        if (neckSpan <= 1e-6f)
            return null;

        neckAxis /= neckSpan;
        float columnStart = neckSpan * Constants.Culling.NeckColumnStart;
        float columnRadius = neckSpan * Constants.Culling.NeckColumnRadius;

        Vector3[] vertices = mesh.vertices;
        bool[] mask = new bool[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 offset = vertices[i] - neckPos;
            float along = Vector3.Dot(offset, neckAxis);
            if (along < columnStart)
                continue;
            if ((offset - (neckAxis * along)).sqrMagnitude > columnRadius * columnRadius)
                continue;

            mask[i] = true;
        }

        return mask;
    }

    private static bool TryGetBodyForward(Mesh mesh, Transform[] bones, int headIndex, int neckIndex, out Vector3 forward)
    {
        forward = Vector3.zero;
        int bindposeCount = mesh.bindposes.Length;
        int toeL = FindBoneIndex(bones, "toe.L"), heelL = FindBoneIndex(bones, "heel.02.L");
        int toeR = FindBoneIndex(bones, "toe.R"), heelR = FindBoneIndex(bones, "heel.02.R");
        if (toeL < 0 || heelL < 0 || toeR < 0 || heelR < 0
            || toeL >= bindposeCount || heelL >= bindposeCount
            || toeR >= bindposeCount || heelR >= bindposeCount)
            return false;

        Vector3 raw = (BoneBindPosition(mesh, toeL) - BoneBindPosition(mesh, heelL))
            + (BoneBindPosition(mesh, toeR) - BoneBindPosition(mesh, heelR));
        Vector3 up = BoneBindPosition(mesh, headIndex) - BoneBindPosition(mesh, neckIndex);
        if (up.sqrMagnitude > 1e-6f)
            raw -= up.normalized * Vector3.Dot(raw, up.normalized);
        if (raw.sqrMagnitude < 1e-6f)
            return false;

        forward = raw.normalized;
        return true;
    }

    private static Vector3 BoneBindPosition(Mesh mesh, int boneIndex)
    {
        Matrix4x4 boneToMesh = mesh.bindposes[boneIndex].inverse;
        return new Vector3(boneToMesh.m03, boneToMesh.m13, boneToMesh.m23);
    }

    private static int FindBoneIndex(Transform[] bones, string boneName)
    {
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null && string.Equals(bones[i].name, boneName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    public static void ClearCaches()
    {
        foreach (Mesh mesh in HeadlessMeshCache.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        foreach (Mesh mesh in HeadlessArmlessMeshCache.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        foreach (Mesh mesh in OwnMeshCache.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        foreach (Mesh mesh in OwnArmlessMeshCache.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        foreach (Mesh mesh in CapMeshCache.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        foreach (Mesh? mesh in ReadableCopies.Values)
        {
            if (mesh != null)
                UnityEngine.Object.Destroy(mesh);
        }

        HeadlessMeshCache.Clear();
        HeadlessArmlessMeshCache.Clear();
        OwnMeshCache.Clear();
        OwnArmlessMeshCache.Clear();
        CapMeshCache.Clear();
        ReadableCopies.Clear();
        GeneratedMeshIds.Clear();
        LoggedUnreadableMeshes.Clear();
        LoggedCapMeshes.Clear();
        FailedHeadlessMeshes.Clear();
        FailedHeadlessArmlessMeshes.Clear();
        FailedOwnMeshes.Clear();
        FailedOwnArmlessMeshes.Clear();
    }

    public static Mesh? GetOrCreateHeadlessMesh(
        Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone)
    {
        int meshId = sourceMesh.GetInstanceID();
        if (HeadlessMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
            return cached;

        if (FailedHeadlessMeshes.Contains(meshId))
            return null;

        Mesh? readable = GetReadable(sourceMesh, meshId);
        if (readable == null)
            return null;

        bool[]? pipeMask = BuildPipeMask(readable, skinned, headBone);
        HashSet<int> headBones = GetHeadBoneIndices(skinned, headBone);
        Mesh? built = TryBuildFilteredMesh(readable, headBones, pipeMask, "_FPVHeadless");
        if (built != null)
        {
            HeadlessMeshCache[meshId] = built;
            GeneratedMeshIds.Add(built.GetInstanceID());
        }
        else
        {
            FailedHeadlessMeshes.Add(meshId);
        }

        return built;
    }

    public static Mesh? GetOrCreateHeadlessArmlessMesh(
        Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone, Transform? leftArm, Transform? rightArm)
    {
        int meshId = sourceMesh.GetInstanceID();
        if (HeadlessArmlessMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
            return cached;

        if (FailedHeadlessArmlessMeshes.Contains(meshId))
            return null;

        Mesh? readable = GetReadable(sourceMesh, meshId);
        if (readable == null)
            return null;

        Mesh? built;
        try
        {
            built = BuildHeadlessArmlessMesh(readable, skinned, headBone, leftArm, rightArm, "_FPVHeadlessArmless");
        }
        catch (Exception)
        {
            FailedHeadlessArmlessMeshes.Add(meshId);
            return null;
        }

        if (built != null)
        {
            HeadlessArmlessMeshCache[meshId] = built;
            GeneratedMeshIds.Add(built.GetInstanceID());
        }
        else
        {
            FailedHeadlessArmlessMeshes.Add(meshId);
        }

        return built;
    }

    public static Mesh? GetOwnMesh(Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone)
    {
        int meshId = sourceMesh.GetInstanceID();
        if (OwnMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
            return cached;

        if (FailedOwnMeshes.Contains(meshId))
            return null;

        Mesh? readable = GetReadable(sourceMesh, meshId);
        if (readable == null)
            return null;

        bool[]? pipeMask = BuildPipeMask(readable, skinned, headBone);
        bool[]? columnMask = BuildColumnMask(readable, skinned, headBone);
        HashSet<int> headBones = GetHeadBoneIndices(skinned, headBone);
        Mesh? built = TryBuildFilteredMesh(readable, headBones, pipeMask, "_FPVHeadless", neckCap: true, columnMask);
        if (built != null)
        {
            OwnMeshCache[meshId] = built;
            GeneratedMeshIds.Add(built.GetInstanceID());
        }
        else
        {
            FailedOwnMeshes.Add(meshId);
        }

        return built;
    }

    public static Mesh? GetOwnArmlessMesh(
        Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone, Transform? leftArm, Transform? rightArm)
    {
        int meshId = sourceMesh.GetInstanceID();
        if (OwnArmlessMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
            return cached;

        if (FailedOwnArmlessMeshes.Contains(meshId))
            return null;

        Mesh? readable = GetReadable(sourceMesh, meshId);
        if (readable == null)
            return null;

        Mesh? built;
        try
        {
            built = BuildHeadlessArmlessMesh(
                readable, skinned, headBone, leftArm, rightArm, "_FPVHeadlessArmless", neckCap: true);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to build '{sourceMesh.name}_FPVHeadlessArmless' with the neck cap. {ex.Message}");
            FailedOwnArmlessMeshes.Add(meshId);
            return null;
        }

        if (built != null)
        {
            OwnArmlessMeshCache[meshId] = built;
            GeneratedMeshIds.Add(built.GetInstanceID());
        }
        else
        {
            FailedOwnArmlessMeshes.Add(meshId);
        }

        return built;
    }

    public static Mesh? GetCapMesh(Mesh sourceMesh)
    {
        Mesh? readable = GetReadable(sourceMesh);
        return readable != null && CapMeshCache.TryGetValue(readable.GetInstanceID(), out Mesh cap) && cap != null ? cap : null;
    }

    private static Mesh? BuildHeadlessArmlessMesh(
        Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone, Transform? leftArm, Transform? rightArm,
        string nameSuffix, bool neckCap = false)
    {
        BoneWeight[] boneWeights = sourceMesh.boneWeights;
        if (boneWeights == null || boneWeights.Length == 0)
            return null;

        HashSet<int> headBones = GetHeadBoneIndices(skinned, headBone);
        HashSet<int> armBones = GetArmBoneIndices(skinned, leftArm, rightArm);
        if (headBones.Count == 0 && armBones.Count == 0)
            return null;

        bool[]? pipeMask = BuildPipeMask(sourceMesh, skinned, headBone);

        int vertexCount = boneWeights.Length;
        float[] headInfluence = new float[vertexCount];
        bool[] headDominant = new bool[vertexCount];
        bool[] armDominant = new bool[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            BoneWeight bw = boneWeights[i];
            headInfluence[i] = InfluenceOf(bw, headBones);
            headDominant[i] = IsDominatedBy(bw, headBones) || (pipeMask != null && pipeMask[i]);
            armDominant[i] = IsDominatedBy(bw, armBones);
        }

        Vector3[] foldedVertices = (Vector3[])sourceMesh.vertices.Clone();
        BoneWeight[] foldedWeights = (BoneWeight[])boneWeights.Clone();
        int[]? targets = neckCap ? NewTargets(vertexCount) : null;

        FoldDominantVerticesToNearestSeam(sourceMesh, headDominant, foldedVertices, foldedWeights, targets: targets);

        if (!FoldDominantVerticesToNearestSeam(sourceMesh, armDominant, foldedVertices, foldedWeights, headDominant))
            return null;

        List<int>[] kept = FilterTriangles(sourceMesh, headInfluence, headDominant, armDominant);
        if (neckCap)
        {
            if (!TryCap(sourceMesh, foldedVertices, foldedWeights, kept))
                return null;

            bool[]? columnMask = BuildColumnMask(sourceMesh, skinned, headBone);
            FixNeckFolds(kept, sourceMesh, boneWeights, headBones, columnMask, targets!, foldedVertices);
        }

        return CreateMesh(sourceMesh, nameSuffix, foldedVertices, foldedWeights, kept, neckCap);
    }

    private static List<int>[] FilterTriangles(
        Mesh sourceMesh, float[] headInfluence, bool[] headDominant, bool[]? armDominant = null)
    {
        List<int>[] result = new List<int>[sourceMesh.subMeshCount];
        for (int subMesh = 0; subMesh < result.Length; subMesh++)
        {
            int[] triangles = sourceMesh.GetTriangles(subMesh);
            List<int> kept = new(triangles.Length);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                if (ShouldCullTriangle(a, b, c, headInfluence, headDominant))
                    continue;
                if (armDominant != null && armDominant[a] && armDominant[b] && armDominant[c])
                    continue;

                kept.Add(a);
                kept.Add(b);
                kept.Add(c);
            }

            result[subMesh] = kept;
        }

        return result;
    }

    private static Mesh CreateMesh(
        Mesh sourceMesh, string nameSuffix, Vector3[] vertices, BoneWeight[] weights, List<int>[] kept, bool capped)
    {
        Mesh filteredMesh = UnityEngine.Object.Instantiate(sourceMesh);
        try
        {
            filteredMesh.name = sourceMesh.name + nameSuffix + (capped ? "Capped" : string.Empty);
            filteredMesh.vertices = vertices;
            filteredMesh.boneWeights = weights;

            for (int subMesh = 0; subMesh < kept.Length; subMesh++)
                filteredMesh.SetTriangles(kept[subMesh], subMesh);

            filteredMesh.RecalculateBounds();
        }
        catch (Exception)
        {
            UnityEngine.Object.Destroy(filteredMesh);
            throw;
        }

        return filteredMesh;
    }

    private static bool TryCap(Mesh sourceMesh, Vector3[] vertices, BoneWeight[] weights, List<int>[] kept)
    {
        int meshId = sourceMesh.GetInstanceID();
        try
        {
            if (NeckCap.TryApply(sourceMesh, vertices, weights, kept, out NeckCap.Shape? cap))
            {
                SaveCap(sourceMesh, cap);
                return true;
            }

            if (sourceMesh.vertexCount == Constants.Clearance.BodyVertexCount && LoggedCapMeshes.Add(meshId))
                Plugin.Log.LogWarning($"Could not close the neck hole on '{sourceMesh.name}', its rim does not match the vanilla suit.");
        }
        catch (Exception ex)
        {
            if (LoggedCapMeshes.Add(meshId))
                Plugin.Log.LogWarning($"Failed to close the neck hole on '{sourceMesh.name}'. {ex.Message} The hole stays open.");
        }

        return false;
    }

    private static void SaveCap(Mesh sourceMesh, NeckCap.Shape cap)
    {
        int meshId = sourceMesh.GetInstanceID();
        if (CapMeshCache.TryGetValue(meshId, out Mesh saved) && saved != null)
            return;

        CapMeshCache[meshId] = CreateCapMesh(sourceMesh, cap);
        Plugin.Log.LogDebug($"Built the neck cap for '{sourceMesh.name}'.");
    }

    private static Mesh CreateCapMesh(Mesh sourceMesh, NeckCap.Shape cap)
    {
        Mesh capMesh = new();
        try
        {
            capMesh.name = sourceMesh.name + "_FPVNeckCap";
            capMesh.vertices = cap.Positions;
            capMesh.normals = cap.Normals;
            capMesh.tangents = cap.Tangents;
            capMesh.uv = cap.Uvs;
            capMesh.boneWeights = cap.Weights;
            capMesh.bindposes = sourceMesh.bindposes;
            capMesh.SetTriangles(cap.Indices, 0);
            capMesh.RecalculateBounds();
        }
        catch (Exception)
        {
            UnityEngine.Object.Destroy(capMesh);
            throw;
        }

        return capMesh;
    }

    private static List<int> CollectSeamVertices(Mesh mesh, bool[] dominantCut)
    {
        HashSet<int> seam = new();
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            int[] triangles = mesh.GetTriangles(subMesh);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                if (!dominantCut[a] && !dominantCut[b] && !dominantCut[c])
                    continue;

                if (!dominantCut[a]) seam.Add(a);
                if (!dominantCut[b]) seam.Add(b);
                if (!dominantCut[c]) seam.Add(c);
            }
        }

        return new List<int>(seam);
    }

    private static bool FoldDominantVerticesToNearestSeam(
        Mesh sourceMesh, bool[] dominantCut, Vector3[] foldedVertices, BoneWeight[] foldedWeights,
        bool[]? avoidTargets = null, int[]? targets = null)
    {
        List<int> seam = CollectSeamVertices(sourceMesh, dominantCut);
        if (seam.Count == 0)
            return false;

        List<int> candidates = seam;
        if (avoidTargets != null)
        {
            List<int> preferred = new(seam.Count);
            foreach (int v in seam)
            {
                if (v >= 0 && v < avoidTargets.Length && !avoidTargets[v])
                    preferred.Add(v);
            }

            if (preferred.Count > 0)
                candidates = preferred;
        }

        Vector3[] lookupVertices = (Vector3[])foldedVertices.Clone();
        BoneWeight[] lookupWeights = (BoneWeight[])foldedWeights.Clone();

        for (int i = 0; i < dominantCut.Length; i++)
        {
            if (!dominantCut[i])
                continue;

            int target = NearestVertex(lookupVertices[i], candidates, lookupVertices);
            foldedVertices[i] = lookupVertices[target];
            foldedWeights[i] = lookupWeights[target];
            if (targets != null)
                targets[i] = target;
        }

        return true;
    }

    private static int[] NewTargets(int count)
    {
        int[] targets = new int[count];
        Array.Fill(targets, -1);
        return targets;
    }

    private static void FixNeckFolds(
        List<int>[] kept, Mesh source, BoneWeight[] weights, HashSet<int> headBones, bool[]? columnMask, int[] targets,
        Vector3[] folded)
    {
        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        Vector2[] uvs = source.uv;
        float cutoff = Mathf.Cos(Constants.Culling.FoldAngle * Mathf.Deg2Rad);

        bool[] neck = new bool[weights.Length];
        int[] corners = new int[weights.Length];
        for (int i = 0; i < neck.Length; i++)
        {
            neck[i] = targets[i] >= 0 && (IsDominatedBy(weights[i], headBones) || (columnMask != null && columnMask[i]));
            corners[i] = -1;
        }

        int GetCorner(int vertex)
        {
            if (!neck[vertex])
                return vertex;

            if (corners[vertex] >= 0)
                return corners[vertex];

            int target = targets[vertex];
            int best = vertex;
            float limit = Constants.Culling.FoldUvRange * Constants.Culling.FoldUvRange;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i != target && !NeckCap.IsCopy(positions, weights, i, target))
                    continue;

                float range = uvs.Length == positions.Length ? (uvs[i] - uvs[vertex]).sqrMagnitude : 0f;
                if (range >= limit || Vector3.Dot(normals[i].normalized, normals[vertex].normalized) < cutoff)
                    continue;

                best = i;
                limit = range;
            }

            corners[vertex] = best;
            return best;
        }

        for (int subMesh = 0; subMesh < kept.Length; subMesh++)
        {
            List<int> list = kept[subMesh];
            List<int> result = new(list.Count);
            for (int i = 0; i < list.Count; i += 3)
            {
                int a = list[i];
                int b = list[i + 1];
                int c = list[i + 2];
                if (!neck[a] && !neck[b] && !neck[c])
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    continue;
                }

                if (Vector3.Cross(folded[b] - folded[a], folded[c] - folded[a]).sqrMagnitude <= MinCross)
                    continue;

                result.Add(GetCorner(a));
                result.Add(GetCorner(b));
                result.Add(GetCorner(c));
            }

            kept[subMesh] = result;
        }
    }

    private static int NearestVertex(Vector3 point, List<int> candidates, Vector3[] vertices)
    {
        int best = candidates[0];
        float bestDistance = float.MaxValue;
        foreach (int v in candidates)
        {
            float distance = (vertices[v] - point).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = v;
            }
        }

        return best;
    }

    private static Mesh? TryBuildFilteredMesh(
        Mesh sourceMesh, HashSet<int> removalBoneIndices, bool[]? pipeMask, string nameSuffix, bool neckCap = false,
        bool[]? columnMask = null)
    {
        try
        {
            return BuildMeshWithoutBones(sourceMesh, removalBoneIndices, pipeMask, nameSuffix, neckCap, columnMask);
        }
        catch (Exception ex)
        {
            string capText = neckCap ? " with the neck cap" : string.Empty;
            Plugin.Log.LogWarning($"Failed to build '{sourceMesh.name}{nameSuffix}'{capText}. {ex.Message}");
            return null;
        }
    }

    private static Mesh? BuildMeshWithoutBones(
        Mesh sourceMesh, HashSet<int> removalBoneIndices, bool[]? pipeMask, string nameSuffix, bool neckCap = false,
        bool[]? columnMask = null)
    {
        BoneWeight[] boneWeights = sourceMesh.boneWeights;
        if (boneWeights == null || boneWeights.Length == 0)
            return null;

        if (removalBoneIndices.Count == 0)
            return null;

        float[] influence = new float[boneWeights.Length];
        bool[] dominant = new bool[boneWeights.Length];
        for (int i = 0; i < boneWeights.Length; i++)
        {
            BoneWeight bw = boneWeights[i];
            influence[i] = InfluenceOf(bw, removalBoneIndices);
            dominant[i] = IsDominatedBy(bw, removalBoneIndices) || (pipeMask != null && pipeMask[i]);
        }

        Vector3[] foldedVertices = (Vector3[])sourceMesh.vertices.Clone();
        BoneWeight[] foldedWeights = (BoneWeight[])boneWeights.Clone();
        int[]? targets = neckCap ? NewTargets(boneWeights.Length) : null;
        FoldDominantVerticesToNearestSeam(sourceMesh, dominant, foldedVertices, foldedWeights, targets: targets);

        List<int>[] kept = FilterTriangles(sourceMesh, influence, dominant);
        if (neckCap)
        {
            if (!TryCap(sourceMesh, foldedVertices, foldedWeights, kept))
                return null;

            FixNeckFolds(kept, sourceMesh, boneWeights, removalBoneIndices, columnMask, targets!, foldedVertices);
        }

        return CreateMesh(sourceMesh, nameSuffix, foldedVertices, foldedWeights, kept, neckCap);
    }

    private static float InfluenceOf(BoneWeight bw, HashSet<int> set)
    {
        float total = 0f;
        if (set.Contains(bw.boneIndex0)) total += bw.weight0;
        if (set.Contains(bw.boneIndex1)) total += bw.weight1;
        if (set.Contains(bw.boneIndex2)) total += bw.weight2;
        if (set.Contains(bw.boneIndex3)) total += bw.weight3;
        return total;
    }

    private static bool ShouldCullTriangle(int a, int b, int c, float[] headInfluence, bool[] dominantHead)
    {
        float influenceA = HeadInfluenceAt(a, headInfluence);
        float influenceB = HeadInfluenceAt(b, headInfluence);
        float influenceC = HeadInfluenceAt(c, headInfluence);

        int strongInfluenceCount =
            StrongInfluenceCount(influenceA)
            + StrongInfluenceCount(influenceB)
            + StrongInfluenceCount(influenceC);

        int dominantHeadCount =
            DominantHeadCount(a, dominantHead)
            + DominantHeadCount(b, dominantHead)
            + DominantHeadCount(c, dominantHead);

        if (dominantHeadCount >= 2 || strongInfluenceCount >= 2)
            return true;

        float averageInfluence = (influenceA + influenceB + influenceC) / 3f;
        return dominantHeadCount >= 1 && averageInfluence >= Constants.Culling.HeadAverageInfluence;
    }

    private static float HeadInfluenceAt(int index, float[] headInfluence)
    {
        return index >= 0 && index < headInfluence.Length ? headInfluence[index] : 0f;
    }

    private static int StrongInfluenceCount(float influence)
    {
        return influence >= Constants.Culling.HeadStrongInfluence ? 1 : 0;
    }

    private static int DominantHeadCount(int index, bool[] dominantHead)
    {
        return index >= 0 && index < dominantHead.Length && dominantHead[index] ? 1 : 0;
    }

    private static bool IsDominatedBy(BoneWeight bw, HashSet<int> set)
    {
        int dominantBone = bw.boneIndex0;
        float dominantWeight = bw.weight0;
        if (bw.weight1 > dominantWeight) { dominantWeight = bw.weight1; dominantBone = bw.boneIndex1; }
        if (bw.weight2 > dominantWeight) { dominantWeight = bw.weight2; dominantBone = bw.boneIndex2; }
        if (bw.weight3 > dominantWeight) { dominantWeight = bw.weight3; dominantBone = bw.boneIndex3; }
        return dominantWeight > 0f && set.Contains(dominantBone);
    }

    private static HashSet<int> GetHeadBoneIndices(SkinnedMeshRenderer skinned, Transform headBone)
    {
        HashSet<int> indices = new();
        Transform[] bones = skinned.bones;

        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null)
                continue;

            if (bone == headBone || bone.IsChildOf(headBone) || ContainsHeadHint(bone.name))
                indices.Add(i);
        }

        return indices;
    }

    private static HashSet<int> GetArmBoneIndices(SkinnedMeshRenderer skinned, Transform? leftArm, Transform? rightArm)
    {
        HashSet<int> indices = new();
        Transform[] bones = skinned.bones;

        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null)
                continue;

            bool isLeft = leftArm != null && (bone == leftArm || bone.IsChildOf(leftArm));
            bool isRight = rightArm != null && (bone == rightArm || bone.IsChildOf(rightArm));
            if (isLeft || isRight)
                indices.Add(i);
        }

        return indices;
    }

    private static bool ContainsHeadHint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (string hint in Constants.Rig.HeadBoneHints)
        {
            if (value!.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    internal static Mesh? GetReadable(Mesh sourceMesh) => GetReadable(sourceMesh, sourceMesh.GetInstanceID());

    private static Mesh? GetReadable(Mesh sourceMesh, int meshId)
    {
        if (sourceMesh.isReadable)
            return sourceMesh;

        if (ReadableCopies.TryGetValue(meshId, out Mesh? cached))
            return cached;

        Mesh? copy = null;
        try
        {
            Mesh candidate = MakeReadableMeshCopy(sourceMesh);
            if (HasUsableSkinningData(candidate))
                copy = candidate;
            else
                UnityEngine.Object.Destroy(candidate);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Mesh '{sourceMesh.name}' could not be copied from the GPU. {ex.Message}");
        }

        if (copy == null && LoggedUnreadableMeshes.Add(meshId))
            Plugin.Log.LogWarning($"Mesh '{sourceMesh.name}' is not readable and its buffers could not be read back from the GPU so the head cant be hidden on this model.");

        ReadableCopies[meshId] = copy;
        return copy;
    }

    private static bool HasUsableSkinningData(Mesh candidate)
    {
        BoneWeight[] weights = candidate.boneWeights;
        Vector3[] positions = candidate.vertices;
        if (weights.Length == 0 || candidate.bindposes.Length == 0 || positions.Length == 0)
            return false;

        int step = Mathf.Max(1, weights.Length / 512);

        bool weighted = false;
        for (int i = 0; i < weights.Length && !weighted; i += step)
        {
            BoneWeight bw = weights[i];
            weighted = bw.weight0 + bw.weight1 + bw.weight2 + bw.weight3 > 0.5f;
        }

        if (!weighted)
            return false;

        for (int i = 0; i < positions.Length; i += step)
        {
            if (positions[i].sqrMagnitude > 1e-10f)
                return true;
        }

        return false;
    }

    private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
    {
        Mesh meshCopy = new()
        {
            name = nonReadableMesh.name,
            indexFormat = nonReadableMesh.indexFormat,
        };

        bool completed = false;
        try
        {
            PopulateReadableMeshCopy(nonReadableMesh, meshCopy);
            completed = true;
        }
        finally
        {
            if (!completed)
                UnityEngine.Object.Destroy(meshCopy);
        }

        return meshCopy;
    }

    private static void ReadBuffersInto(Mesh nonReadableMesh, Mesh meshCopy)
    {
        nonReadableMesh.vertexBufferTarget |= GraphicsBuffer.Target.Vertex;
        int streamCount = nonReadableMesh.vertexBufferCount;
        if (streamCount > 0)
        {
            meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());

            for (int stream = 0; stream < streamCount; stream++)
            {
                using GraphicsBuffer vertexBuffer = nonReadableMesh.GetVertexBuffer(stream);
                int size = vertexBuffer.stride * vertexBuffer.count;
                byte[] data = new byte[size];
                vertexBuffer.GetData(data);
                meshCopy.SetVertexBufferData(data, 0, 0, size, stream);
            }
        }

        nonReadableMesh.indexBufferTarget |= GraphicsBuffer.Target.Index;
        meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
        using (GraphicsBuffer indexBuffer = nonReadableMesh.GetIndexBuffer())
        {
            int size = indexBuffer.stride * indexBuffer.count;
            byte[] data = new byte[size];
            indexBuffer.GetData(data);
            meshCopy.SetIndexBufferParams(indexBuffer.count, nonReadableMesh.indexFormat);
            meshCopy.SetIndexBufferData(data, 0, 0, size);
        }
    }

    private static void PopulateReadableMeshCopy(Mesh nonReadableMesh, Mesh meshCopy)
    {
        GraphicsBuffer.Target originalVertexTarget = nonReadableMesh.vertexBufferTarget;
        GraphicsBuffer.Target originalIndexTarget = nonReadableMesh.indexBufferTarget;

        try
        {
            ReadBuffersInto(nonReadableMesh, meshCopy);
        }
        finally
        {
            nonReadableMesh.vertexBufferTarget = originalVertexTarget;
            nonReadableMesh.indexBufferTarget = originalIndexTarget;
        }

        int indexOffset = 0;
        for (int i = 0; i < meshCopy.subMeshCount; i++)
        {
            int subMeshIndexCount = (int)nonReadableMesh.GetIndexCount(i);
            meshCopy.SetSubMesh(i, new SubMeshDescriptor(indexOffset, subMeshIndexCount));
            indexOffset += subMeshIndexCount;
        }

        meshCopy.bindposes = nonReadableMesh.bindposes;
        meshCopy.RecalculateBounds();
    }
}
