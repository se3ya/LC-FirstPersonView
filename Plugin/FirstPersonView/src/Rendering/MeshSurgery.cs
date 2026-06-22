using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonView
{
    // hide the local players own head and their arms while the vanilla arms hold an item
    internal static class MeshSurgery
    {
        private static readonly Dictionary<int, Mesh> HeadlessMeshCache = new();
        private static readonly Dictionary<int, Mesh> HeadlessArmlessMeshCache = new();
        private static readonly Dictionary<int, Mesh?> ReadableCopies = new();
        private static readonly HashSet<int> GeneratedMeshIds = new();   // meshes we built, so we don't capture one as a source
        private static readonly HashSet<int> LoggedUnreadableMeshes = new();

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
            if (neck == null || mesh.bindposes == null || mesh.bindposes.Length < bones.Length)
                return null;

            int neckIndex = Array.IndexOf(bones, neck);
            int headIndex = Array.IndexOf(bones, headBone);
            if (neckIndex < 0 || headIndex < 0)
                return null;

            if (!TryGetBodyForward(mesh, bones, headIndex, neckIndex, out Vector3 forward))
                return null;

            Vector3 neckPos = BoneBindPosition(mesh, neckIndex);
            BoneWeight[] weights = mesh.boneWeights;
            Vector3[] vertices = mesh.vertices;
            bool[] mask = new bool[vertices.Length];

            int marked = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (DominantBoneIndex(weights[i]) != neckIndex)
                    continue;
                if (Vector3.Dot(vertices[i] - neckPos, forward) < Constants.PipeCullBehindThreshold)
                {
                    mask[i] = true;
                    marked++;
                }
            }

            return marked > 0 ? mask : null;
        }

        private static bool TryGetBodyForward(Mesh mesh, Transform[] bones, int headIndex, int neckIndex, out Vector3 forward)
        {
            forward = Vector3.zero;
            int toeL = FindBoneIndex(bones, "toe.L"), heelL = FindBoneIndex(bones, "heel.02.L");
            int toeR = FindBoneIndex(bones, "toe.R"), heelR = FindBoneIndex(bones, "heel.02.R");
            if (toeL < 0 || heelL < 0 || toeR < 0 || heelR < 0)
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

            foreach (Mesh? mesh in ReadableCopies.Values)
            {
                if (mesh != null)
                    UnityEngine.Object.Destroy(mesh);
            }

            HeadlessMeshCache.Clear();
            HeadlessArmlessMeshCache.Clear();
            ReadableCopies.Clear();
            GeneratedMeshIds.Clear();
            LoggedUnreadableMeshes.Clear();
        }

        public static Mesh? GetOrCreateHeadlessMesh(Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone)
        {
            int meshId = sourceMesh.GetInstanceID();
            if (HeadlessMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
                return cached;

            Mesh? readable = ResolveReadable(sourceMesh, meshId);
            if (readable == null)
                return null;

            bool[]? pipeMask = BuildPipeMask(readable, skinned, headBone);
            Mesh? built = TryBuildFilteredMesh(readable, GetHeadBoneIndices(skinned, headBone), pipeMask, "_FPVHeadless");
            if (built != null)
            {
                HeadlessMeshCache[meshId] = built;
                GeneratedMeshIds.Add(built.GetInstanceID());
            }

            return built;
        }

        public static Mesh? GetOrCreateHeadlessArmlessMesh(
            Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone, Transform? leftArm, Transform? rightArm)
        {
            int meshId = sourceMesh.GetInstanceID();
            if (HeadlessArmlessMeshCache.TryGetValue(meshId, out Mesh cached) && cached != null)
                return cached;

            Mesh? readable = ResolveReadable(sourceMesh, meshId);
            if (readable == null)
                return null;

            Mesh? built;
            try
            {
                built = BuildHeadlessArmlessMesh(readable, skinned, headBone, leftArm, rightArm, "_FPVHeadlessArmless");
            }
            catch (Exception)
            {
                return null;
            }

            if (built != null)
            {
                HeadlessArmlessMeshCache[meshId] = built;
                GeneratedMeshIds.Add(built.GetInstanceID());
            }

            return built;
        }

        // hides the arms by folding each arm vertex onto the nearest shoulder seam vertex
        private static Mesh? BuildHeadlessArmlessMesh(
            Mesh sourceMesh, SkinnedMeshRenderer skinned, Transform headBone, Transform? leftArm, Transform? rightArm,
            string nameSuffix)
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

            List<int> seam = CollectSeamVertices(sourceMesh, headInfluence, headDominant, armDominant);
            if (seam.Count == 0)
                return null;   // the arms don't border the torso here, so theres nothing to fold onto

            Vector3[] vertices = sourceMesh.vertices;
            Vector3[] foldedVertices = (Vector3[])vertices.Clone();
            BoneWeight[] foldedWeights = (BoneWeight[])boneWeights.Clone();
            for (int i = 0; i < vertexCount; i++)
            {
                if (!armDominant[i])
                    continue;
                int target = NearestVertex(vertices[i], seam, vertices);
                foldedVertices[i] = vertices[target];
                foldedWeights[i] = boneWeights[target];
            }

            Mesh filteredMesh = UnityEngine.Object.Instantiate(sourceMesh);
            filteredMesh.name = sourceMesh.name + nameSuffix;
            filteredMesh.vertices = foldedVertices;
            filteredMesh.boneWeights = foldedWeights;

            for (int subMesh = 0; subMesh < filteredMesh.subMeshCount; subMesh++)
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
                    if (armDominant[a] && armDominant[b] && armDominant[c])
                        continue;   // all three verts fold to the seam, so the triangle would draw nothing

                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }

                filteredMesh.SetTriangles(kept, subMesh);
            }

            filteredMesh.RecalculateBounds();

            return filteredMesh;
        }

        private static List<int> CollectSeamVertices(
            Mesh mesh, float[] headInfluence, bool[] headDominant, bool[] armDominant)
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

                    if (ShouldCullTriangle(a, b, c, headInfluence, headDominant))
                        continue;
                    if (!armDominant[a] && !armDominant[b] && !armDominant[c])
                        continue;

                    if (!armDominant[a]) seam.Add(a);
                    if (!armDominant[b]) seam.Add(b);
                    if (!armDominant[c]) seam.Add(c);
                }
            }

            return new List<int>(seam);
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

        private static Mesh? TryBuildFilteredMesh(Mesh sourceMesh, HashSet<int> removalBoneIndices, bool[]? pipeMask, string nameSuffix)
        {
            try
            {
                return BuildMeshWithoutBones(sourceMesh, removalBoneIndices, pipeMask, nameSuffix);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to build '{sourceMesh.name}{nameSuffix}'. {ex.Message}");
                return null;
            }
        }

        // removes every triangle dominantly weighted to one of removalBoneIndices keeping the rest of the body.
        private static Mesh? BuildMeshWithoutBones(Mesh sourceMesh, HashSet<int> removalBoneIndices, bool[]? pipeMask, string nameSuffix)
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

            Mesh filteredMesh = UnityEngine.Object.Instantiate(sourceMesh);
            filteredMesh.name = sourceMesh.name + nameSuffix;

            for (int subMesh = 0; subMesh < filteredMesh.subMeshCount; subMesh++)
            {
                int[] triangles = sourceMesh.GetTriangles(subMesh);
                List<int> kept = new(triangles.Length);

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i];
                    int b = triangles[i + 1];
                    int c = triangles[i + 2];

                    if (ShouldCullTriangle(a, b, c, influence, dominant))
                        continue;

                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }

                filteredMesh.SetTriangles(kept, subMesh);
            }

            filteredMesh.RecalculateBounds();

            return filteredMesh;
        }

        // sum weight of the bones in set that influence this vertex.
        private static float InfluenceOf(BoneWeight bw, HashSet<int> set)
        {
            float total = 0f;
            if (set.Contains(bw.boneIndex0)) total += bw.weight0;
            if (set.Contains(bw.boneIndex1)) total += bw.weight1;
            if (set.Contains(bw.boneIndex2)) total += bw.weight2;
            if (set.Contains(bw.boneIndex3)) total += bw.weight3;
            return total;
        }

        // vertexs strongest weighted bone belongs to set
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

        // shoulder bones and everything below them.
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

        private static bool ShouldCullTriangle(int a, int b, int c, float[] headInfluence, bool[] dominantHead)
        {
            float influenceA = a >= 0 && a < headInfluence.Length ? headInfluence[a] : 0f;
            float influenceB = b >= 0 && b < headInfluence.Length ? headInfluence[b] : 0f;
            float influenceC = c >= 0 && c < headInfluence.Length ? headInfluence[c] : 0f;

            int strongInfluenceCount = 0;
            if (influenceA >= Constants.HeadStrongInfluenceThreshold) strongInfluenceCount++;
            if (influenceB >= Constants.HeadStrongInfluenceThreshold) strongInfluenceCount++;
            if (influenceC >= Constants.HeadStrongInfluenceThreshold) strongInfluenceCount++;

            int dominantHeadCount = 0;
            if (a >= 0 && a < dominantHead.Length && dominantHead[a]) dominantHeadCount++;
            if (b >= 0 && b < dominantHead.Length && dominantHead[b]) dominantHeadCount++;
            if (c >= 0 && c < dominantHead.Length && dominantHead[c]) dominantHeadCount++;

            if (dominantHeadCount >= 2 || strongInfluenceCount >= 2)
                return true;

            float averageInfluence = (influenceA + influenceB + influenceC) / 3f;
            return dominantHeadCount >= 1 && averageInfluence >= Constants.HeadAverageInfluenceThreshold;
        }

        private static bool ContainsHeadHint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (string hint in Constants.HeadBoneNameHints)
            {
                if (value!.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        // sourceMesh if it's readable, else a copy pulled back from its GPU buffers
        private static Mesh? ResolveReadable(Mesh sourceMesh, int meshId)
        {
            if (sourceMesh.isReadable)
                return sourceMesh;

            if (ReadableCopies.TryGetValue(meshId, out Mesh? cached))
                return cached;

            Mesh? copy = null;
            try
            {
                Mesh candidate = MakeReadableMeshCopy(sourceMesh);
                if (candidate.boneWeights.Length > 0 && candidate.bindposes.Length > 0)
                    copy = candidate;
                else
                    UnityEngine.Object.Destroy(candidate);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Mesh '{sourceMesh.name}' could not be copied from the GPU. {ex.Message}");
            }

            if (copy == null && LoggedUnreadableMeshes.Add(meshId))
                Plugin.Log.LogWarning($"Mesh '{sourceMesh.name}' is not readable; the head can't be hidden on this model.");

            ReadableCopies[meshId] = copy;
            return copy;
        }

        // read a non-readable mesh back from the GPU
        private static Mesh MakeReadableMeshCopy(Mesh nonReadableMesh)
        {
            Mesh meshCopy = new()
            {
                name = nonReadableMesh.name,
                indexFormat = nonReadableMesh.indexFormat,
            };

            nonReadableMesh.vertexBufferTarget = GraphicsBuffer.Target.Vertex;
            if (nonReadableMesh.vertexBufferCount > 0)
            {
                using GraphicsBuffer vertexBuffer = nonReadableMesh.GetVertexBuffer(0);
                int size = vertexBuffer.stride * vertexBuffer.count;
                byte[] data = new byte[size];
                vertexBuffer.GetData(data);
                meshCopy.SetVertexBufferParams(nonReadableMesh.vertexCount, nonReadableMesh.GetVertexAttributes());
                meshCopy.SetVertexBufferData(data, 0, 0, size);
            }

            nonReadableMesh.indexBufferTarget = GraphicsBuffer.Target.Index;
            meshCopy.subMeshCount = nonReadableMesh.subMeshCount;
            using (GraphicsBuffer indexBuffer = nonReadableMesh.GetIndexBuffer())
            {
                int size = indexBuffer.stride * indexBuffer.count;
                byte[] data = new byte[size];
                indexBuffer.GetData(data);
                meshCopy.SetIndexBufferParams(indexBuffer.count, nonReadableMesh.indexFormat);
                meshCopy.SetIndexBufferData(data, 0, 0, size);
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

            return meshCopy;
        }
    }
}