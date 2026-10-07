using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using UnityEngine;

namespace FirstPersonView;

internal static partial class NeckCap
{
    private const int RimCount = 19;
    private const int RingCount = 2;
    private const int CapCount = (RimCount * (RingCount + 1)) + 1;
    private const int GateFirst = 0;
    private const int GateSecond = 13;
    private const int SmoothRounds = 4;
    private const float Dome = 0.06f;
    private const float UvScale = 0.00035f;
    private const float PositionTolerance = 1e-8f;
    private const float WeightTolerance = 1e-5f;
    private const float MovedTolerance = 1e-12f;
    private const float WeldScale = 2000f;
    private const float MinCross = 1e-12f;

    internal static readonly int[] RimVertex = { 1192, 929, 936, 969, 1018, 1016, 1014, 1007, 1008, 1011, 968, 966, 991, 995, 918, 1174, 1177, 1187, 1190 };
    private static readonly int[] DropTriangles = { 935, 933, 927, 921, 918, 995 };
    private static readonly float[] RingScale = { 0.62f, 0.30f };
    private static readonly float[] RingBlend = { 0.5f, 1f };
    private static readonly Vector2 UvPatch = new(619f / 2048f, 1f - (1517f / 2048f));

    internal sealed class Shape
    {
        public readonly Vector3[] Positions = new Vector3[CapCount];
        public readonly BoneWeight[] Weights = new BoneWeight[CapCount];
        public readonly Vector3[] Normals = new Vector3[CapCount];
        public readonly Vector4[] Tangents = new Vector4[CapCount];
        public readonly Vector2[] Uvs = new Vector2[CapCount];
        public readonly List<int> Indices = new();
        public Vector3 Up;
        public Vector3 Mean;
    }

    private sealed class Rim
    {
        public readonly Dictionary<(int, int, int), int> Keys = new(RimCount);
        public readonly int[] Edges = new int[RimCount];
    }

    public static bool TryApply(
        Mesh source, Vector3[] vertices, BoneWeight[] weights, List<int>[] triangles,
        [NotNullWhen(true)] out Shape? cap)
    {
        cap = null;
        int count = Constants.Clearance.BodyVertexCount;
        if (source.vertexCount != count || triangles.Length != 1 || vertices.Length != count || weights.Length != count)
            return false;

        List<int>? kept = RemoveDropped(triangles[0]);
        if (kept == null)
            return false;

        Rim? rim = FindRim(vertices, weights, kept);
        if (rim == null)
            return false;

        Vector3[] original = source.vertices;
        Vector3[] normals = source.normals;
        if (original.Length != count || normals.Length != count)
            return false;

        Vector3[] rimPoints = new Vector3[RimCount];
        BoneWeight[] rimWeights = new BoneWeight[RimCount];
        for (int i = 0; i < RimCount; i++)
        {
            rimPoints[i] = vertices[RimVertex[i]];
            rimWeights[i] = weights[RimVertex[i]];
        }

        Shape? shape = BuildShape(rimPoints, rimWeights);
        if (shape == null)
            return false;

        Shade(shape, GetRimNormals(vertices, original, normals, kept, rim));
        triangles[0] = kept;
        cap = shape;
        return true;
    }

    public static float GetHeight(Vector3 apex, Vector3 first, Vector3 second, Vector3 up, Vector3 camera)
    {
        Vector3 normal = Vector3.Cross(first - apex, second - apex);
        float length = normal.magnitude;
        if (!(length > 1e-6f))
            return float.NaN;

        if (Vector3.Dot(normal, up) < 0f)
            normal = -normal;

        return Vector3.Dot(camera - apex, normal) / length;
    }

    private static Shape? BuildShape(Vector3[] rim, BoneWeight[] rimWeights)
    {
        Shape shape = new();
        shape.Up = GetNormal(rim);
        shape.Mean = GetMean(rim);

        Vector3 axisX = Vector3.Cross(Vector3.up, shape.Up).normalized;
        Vector3 axisY = Vector3.Cross(shape.Up, axisX);
        double[] x = new double[RimCount];
        double[] y = new double[RimCount];
        for (int i = 0; i < RimCount; i++)
        {
            Vector3 offset = rim[i] - shape.Mean;
            x[i] = Vector3.Dot(offset, axisX);
            y[i] = Vector3.Dot(offset, axisY);
        }

        GetCentroid(x, y, out double centreX, out double centreY);
        double[] lambda = GetMeanValue(x, y, centreX, centreY);
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < RimCount; i++)
            centre += rim[i] * (float)lambda[i];

        for (int i = 0; i < RimCount; i++)
        {
            shape.Positions[i] = rim[i];
            shape.Weights[i] = rimWeights[i];
        }

        for (int ring = 0; ring < RingCount; ring++)
        {
            float scale = RingScale[ring];
            for (int k = 0; k < RimCount; k++)
            {
                int index = (RimCount * (ring + 1)) + k;
                shape.Positions[index] = centre + ((rim[k] - centre) * scale) + (shape.Up * (Dome * (1f - (scale * scale))));
                shape.Weights[index] = MixWeights(rimWeights, lambda, scale, k);
            }
        }

        shape.Positions[CapCount - 1] = centre + (shape.Up * Dome);
        shape.Weights[CapCount - 1] = MixWeights(rimWeights, lambda, 0f, -1);

        AddTriangles(shape);
        return IsFacingUp(shape) ? shape : null;
    }

    private static void AddTriangles(Shape shape)
    {
        for (int layer = 0; layer < RingCount; layer++)
        {
            int from = RimCount * layer;
            int to = RimCount * (layer + 1);
            for (int k = 0; k < RimCount; k++)
            {
                int next = (k + 1) % RimCount;
                AddTriangle(shape.Indices, from + k, from + next, to + k);
                AddTriangle(shape.Indices, from + next, to + next, to + k);
            }
        }

        int last = RimCount * RingCount;
        for (int k = 0; k < RimCount; k++)
            AddTriangle(shape.Indices, last + k, last + ((k + 1) % RimCount), CapCount - 1);

        if (Vector3.Dot(GetFace(shape.Positions, shape.Indices, 0), shape.Up) >= 0f)
            return;

        for (int i = 0; i < shape.Indices.Count; i += 3)
            (shape.Indices[i + 1], shape.Indices[i + 2]) = (shape.Indices[i + 2], shape.Indices[i + 1]);
    }

    private static void AddTriangle(List<int> indices, int a, int b, int c)
    {
        indices.Add(a);
        indices.Add(b);
        indices.Add(c);
    }

    private static bool IsFacingUp(Shape shape)
    {
        for (int i = 0; i < shape.Indices.Count; i += 3)
        {
            if (!(Vector3.Dot(GetFace(shape.Positions, shape.Indices, i), shape.Up) > 0f))
                return false;
        }

        return true;
    }

    private static Vector3 GetFace(Vector3[] positions, List<int> indices, int start)
    {
        Vector3 a = positions[indices[start]];
        return Vector3.Cross(positions[indices[start + 1]] - a, positions[indices[start + 2]] - a);
    }

    private static Vector3 GetNormal(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[(i + 1) % points.Length];
            sum.x += (a.y - b.y) * (a.z + b.z);
            sum.y += (a.z - b.z) * (a.x + b.x);
            sum.z += (a.x - b.x) * (a.y + b.y);
        }

        Vector3 up = sum.normalized;
        return up.z < 0f ? -up : up;
    }

    private static Vector3 GetMean(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in points)
            sum += point;

        return sum / points.Length;
    }

    private static void GetCentroid(double[] x, double[] y, out double centreX, out double centreY)
    {
        double area = 0;
        centreX = 0;
        centreY = 0;
        for (int i = 0; i < x.Length; i++)
        {
            int next = (i + 1) % x.Length;
            double cross = (x[i] * y[next]) - (x[next] * y[i]);
            area += cross;
            centreX += (x[i] + x[next]) * cross;
            centreY += (y[i] + y[next]) * cross;
        }

        centreX /= 3.0 * area;
        centreY /= 3.0 * area;
    }

    private static double[] GetMeanValue(double[] x, double[] y, double pointX, double pointY)
    {
        int count = x.Length;
        double[] radius = new double[count];
        double[] angle = new double[count];
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            double ax = x[i] - pointX;
            double ay = y[i] - pointY;
            double bx = x[next] - pointX;
            double by = y[next] - pointY;
            radius[i] = Math.Sqrt((ax * ax) + (ay * ay));
            angle[i] = Math.Atan2((ax * by) - (ay * bx), (ax * bx) + (ay * by));
        }

        double[] result = new double[count];
        double total = 0;
        for (int i = 0; i < count; i++)
        {
            result[i] = (Math.Tan(angle[(i + count - 1) % count] / 2.0) + Math.Tan(angle[i] / 2.0)) / radius[i];
            total += result[i];
        }

        for (int i = 0; i < count; i++)
            result[i] /= total;

        return result;
    }

    private static BoneWeight MixWeights(BoneWeight[] rimWeights, double[] lambda, float share, int rimIndex)
    {
        Dictionary<int, double> sums = new();
        for (int i = 0; i < rimWeights.Length; i++)
            AddWeights(sums, rimWeights[i], (1.0 - share) * lambda[i]);

        if (rimIndex >= 0)
            AddWeights(sums, rimWeights[rimIndex], share);

        List<KeyValuePair<int, double>> sorted = new(sums);
        sorted.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Key.CompareTo(b.Key));

        int kept = Math.Min(SkinBones.Count, sorted.Count);
        double total = 0;
        for (int i = 0; i < kept; i++)
            total += sorted[i].Value;

        return new BoneWeight
        {
            boneIndex0 = GetBone(sorted, 0, kept),
            weight0 = GetShare(sorted, 0, kept, total),
            boneIndex1 = GetBone(sorted, 1, kept),
            weight1 = GetShare(sorted, 1, kept, total),
            boneIndex2 = GetBone(sorted, 2, kept),
            weight2 = GetShare(sorted, 2, kept, total),
            boneIndex3 = GetBone(sorted, 3, kept),
            weight3 = GetShare(sorted, 3, kept, total),
        };
    }

    private static int GetBone(List<KeyValuePair<int, double>> sorted, int index, int kept)
    {
        return index < kept ? sorted[index].Key : 0;
    }

    private static float GetShare(List<KeyValuePair<int, double>> sorted, int index, int kept, double total)
    {
        return index < kept ? (float)(sorted[index].Value / total) : 0f;
    }

    private static void AddWeights(Dictionary<int, double> sums, BoneWeight weight, double share)
    {
        int[] bones = new int[4];
        float[] shares = new float[4];
        int found = SkinBones.GetShares(weight, bones, shares);
        for (int i = 0; i < found; i++)
            AddWeight(sums, bones[i], shares[i], share);
    }

    private static void AddWeight(Dictionary<int, double> sums, int bone, float weight, double share)
    {
        if (weight <= 0f)
            return;

        sums.TryGetValue(bone, out double current);
        sums[bone] = current + (share * weight);
    }

    private static void Shade(Shape shape, Vector3[] rimNormals)
    {
        Vector3[] faces = new Vector3[CapCount];
        HashSet<int>[] near = new HashSet<int>[CapCount];
        for (int i = 0; i < CapCount; i++)
            near[i] = new HashSet<int>();

        for (int i = 0; i < shape.Indices.Count; i += 3)
        {
            Vector3 face = GetFace(shape.Positions, shape.Indices, i);
            int a = shape.Indices[i];
            int b = shape.Indices[i + 1];
            int c = shape.Indices[i + 2];
            faces[a] += face;
            faces[b] += face;
            faces[c] += face;
            near[a].Add(b);
            near[a].Add(c);
            near[b].Add(a);
            near[b].Add(c);
            near[c].Add(a);
            near[c].Add(b);
        }

        Vector3[] normals = new Vector3[CapCount];
        for (int i = 0; i < CapCount; i++)
        {
            int ring = i / RimCount;
            Vector3 rimNormal = i == CapCount - 1 ? shape.Up : rimNormals[i % RimCount];
            float blend = ring == 0 ? 0f : (i == CapCount - 1 ? 1f : RingBlend[ring - 1]);
            normals[i] = (((1f - blend) * rimNormal) + (blend * faces[i].normalized)).normalized;
        }

        for (int round = 0; round < SmoothRounds; round++)
        {
            Vector3[] next = (Vector3[])normals.Clone();
            for (int i = RimCount; i < CapCount; i++)
            {
                Vector3 sum = Vector3.zero;
                foreach (int other in near[i])
                    sum += normals[other];

                next[i] = ((0.5f * normals[i]) + (0.5f * (sum / near[i].Count))).normalized;
            }

            normals = next;
        }

        for (int i = 0; i < CapCount; i++)
        {
            Vector3 normal = normals[i];
            Vector3 tangent = (Vector3.right - (normal * Vector3.Dot(Vector3.right, normal))).normalized;
            Vector3 back = new(0f, -1f, 0f);
            back -= normal * Vector3.Dot(back, normal);
            shape.Normals[i] = normal;
            shape.Tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, Vector3.Dot(Vector3.Cross(normal, tangent), back) > 0f ? 1f : -1f);

            Vector3 offset = shape.Positions[i] - shape.Mean;
            shape.Uvs[i] = new Vector2(UvPatch.x + (offset.x * UvScale), UvPatch.y - (offset.y * UvScale));
        }
    }

    private static Vector3[] GetRimNormals(
        Vector3[] vertices, Vector3[] original, Vector3[] normals, List<int> kept, Rim rim)
    {
        Vector3[] result = new Vector3[RimCount];
        for (int k = 0; k < RimCount; k++)
        {
            Vector3 outer = Vector3.zero;
            Vector3 all = Vector3.zero;
            AddRimNormals(vertices, original, normals, kept, rim, rim.Edges[(k + RimCount - 1) % RimCount], k, ref outer, ref all);
            AddRimNormals(vertices, original, normals, kept, rim, rim.Edges[k], k, ref outer, ref all);
            result[k] = (outer.sqrMagnitude > 0f ? outer : all).normalized;
        }

        return result;
    }

    private static void AddRimNormals(
        Vector3[] vertices, Vector3[] original, Vector3[] normals, List<int> kept, Rim rim, int triangle, int index,
        ref Vector3 outer, ref Vector3 all)
    {
        for (int e = 0; e < 3; e++)
        {
            int vertex = kept[triangle + e];
            if (!rim.Keys.TryGetValue(GetKey(vertices[vertex]), out int at) || at != index)
                continue;

            all += normals[vertex];
            if ((vertices[vertex] - original[vertex]).sqrMagnitude <= MovedTolerance)
                outer += normals[vertex];
        }
    }

    private static Rim? FindRim(Vector3[] vertices, BoneWeight[] weights, List<int> kept)
    {
        Rim rim = new();
        for (int i = 0; i < RimCount; i++)
            rim.Keys[GetKey(vertices[RimVertex[i]])] = i;

        if (rim.Keys.Count != RimCount)
            return null;

        int[] counts = new int[RimCount];
        int[] ends = new int[3];
        for (int t = 0; t < kept.Count; t += 3)
        {
            Vector3 a = vertices[kept[t]];
            Vector3 b = vertices[kept[t + 1]];
            Vector3 c = vertices[kept[t + 2]];
            if (Vector3.Cross(b - a, c - a).sqrMagnitude <= MinCross)
                continue;

            for (int e = 0; e < 3; e++)
            {
                int vertex = kept[t + e];
                ends[e] = rim.Keys.TryGetValue(GetKey(vertices[vertex]), out int at) ? at : -1;
                if (ends[e] >= 0 && !IsCopy(vertices, weights, vertex, RimVertex[ends[e]]))
                    return null;
            }

            for (int e = 0; e < 3; e++)
            {
                int from = ends[e];
                int to = ends[(e + 1) % 3];
                if (from < 0 || to < 0)
                    continue;

                if (((to - from + RimCount) % RimCount) == 1)
                {
                    counts[from]++;
                    rim.Edges[from] = t;
                }
                else if (((from - to + RimCount) % RimCount) == 1)
                {
                    counts[to]++;
                    rim.Edges[to] = t;
                }
            }
        }

        foreach (int count in counts)
        {
            if (count != 1)
                return null;
        }

        return rim;
    }

    internal static bool IsCopy(Vector3[] vertices, BoneWeight[] weights, int vertex, int other)
    {
        return (vertices[vertex] - vertices[other]).sqrMagnitude <= PositionTolerance
            && SameWeights(weights[vertex], weights[other]);
    }

    private static bool SameWeights(BoneWeight a, BoneWeight b)
    {
        return SamePair(a.boneIndex0, a.weight0, b.boneIndex0, b.weight0)
            && SamePair(a.boneIndex1, a.weight1, b.boneIndex1, b.weight1)
            && SamePair(a.boneIndex2, a.weight2, b.boneIndex2, b.weight2)
            && SamePair(a.boneIndex3, a.weight3, b.boneIndex3, b.weight3);
    }

    private static bool SamePair(int indexA, float weightA, int indexB, float weightB)
    {
        return Mathf.Abs(weightA - weightB) <= WeightTolerance && (weightA <= 0f || indexA == indexB);
    }

    private static List<int>? RemoveDropped(List<int> triangles)
    {
        int drops = DropTriangles.Length / 3;
        int[] found = new int[drops];
        List<int> kept = new(triangles.Count);
        for (int t = 0; t < triangles.Count; t += 3)
        {
            int a = triangles[t];
            int b = triangles[t + 1];
            int c = triangles[t + 2];
            int drop = FindDrop(a, b, c);
            if (drop >= 0)
            {
                found[drop]++;
                continue;
            }

            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
        }

        foreach (int count in found)
        {
            if (count != 1)
                return null;
        }

        return kept;
    }

    private static int FindDrop(int a, int b, int c)
    {
        if (a == b || b == c || a == c)
            return -1;

        for (int i = 0; i < DropTriangles.Length; i += 3)
        {
            int x = DropTriangles[i];
            int y = DropTriangles[i + 1];
            int z = DropTriangles[i + 2];
            if ((a == x || a == y || a == z) && (b == x || b == y || b == z) && (c == x || c == y || c == z))
                return i / 3;
        }

        return -1;
    }

    private static (int, int, int) GetKey(Vector3 point)
    {
        return (
            Mathf.RoundToInt(point.x * WeldScale),
            Mathf.RoundToInt(point.y * WeldScale),
            Mathf.RoundToInt(point.z * WeldScale));
    }
}
