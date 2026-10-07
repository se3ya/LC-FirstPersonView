using System;
using System.Collections.Generic;

using UnityEngine;

namespace FirstPersonView;

internal sealed class ClearancePoint
{
    public Transform[] Bones = Array.Empty<Transform>();
    public Vector3[] Locals = Array.Empty<Vector3>();
    public float[] Weights = Array.Empty<float>();

    public Vector3 GetPosition()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < Bones.Length; i++)
            sum += Bones[i].TransformPoint(Locals[i]) * Weights[i];

        return sum;
    }
}

internal static class BodyClearance
{
    private const int EdgeSteps = 10;
    private const int FaceSteps = 5;

    private static readonly int[] RimVertices = { 960, 965, 969, 1007, 1009, 1035, 1194, 5254, 6430, 7321 };

    private static readonly (int A, int B)[] RimEdges =
    {
        (960, 968), (968, 1011), (969, 1018), (1008, 1011), (1009, 1011), (1035, 1036), (5254, 5255), (7059, 7061),
    };

    private static readonly (int A, int B, int C)[] RimFaces = { (968, 974, 1011), (1008, 1009, 1011), (6906, 6908, 6909) };

    private static readonly (int Vertex, float Weight)[][] Points = CreateBlends();

    private static readonly float[] FallbackPitch = { 0f, 10f, 20f, 30f, 40f, 50f, 60f, 70f, 75f, 80f };
    private static readonly float[] FallbackNeed = { 0f, 0f, 0.01f, 0.086f, 0.159f, 0.255f, 0.365f, 0.483f, 0.55f, 0.623f };

    private static (int Vertex, float Weight)[][] CreateBlends()
    {
        List<(int Vertex, float Weight)[]> blends = new();
        foreach (int vertex in RimVertices)
            blends.Add(new[] { (vertex, 1f) });

        for (int i = 1; i < EdgeSteps; i++)
        {
            float t = (float)i / EdgeSteps;
            foreach ((int a, int b) in RimEdges)
                blends.Add(new[] { (a, 1f - t), (b, t) });
        }

        for (int i = 1; i < FaceSteps - 1; i++)
        {
            for (int j = 1; i + j < FaceSteps; j++)
            {
                float u = (float)i / FaceSteps;
                float v = (float)j / FaceSteps;
                foreach ((int a, int b, int c) in RimFaces)
                    blends.Add(new[] { (a, u), (b, v), (c, 1f - u - v) });
            }
        }

        return blends.ToArray();
    }

    public static NeedView CreateView(Camera camera, float pitch, float yaw)
    {
        float margin = Constants.Clearance.EdgeMargin + ConfigManager.LookDownMargin.Value;
        return new NeedView(pitch, yaw, camera.fieldOfView, camera.aspect, camera.nearClipPlane, margin);
    }

    public static bool GetOffsets(
        LocalBodyState state, Vector3 reference, Transform container, List<Vector3> offsets)
    {
        offsets.Clear();
        ClearancePoint[]? points = GetPoints(state);
        if (points == null)
            return false;

        Vector3 right = container.right;
        Vector3 up = container.up;
        Vector3 forward = container.forward;
        foreach (ClearancePoint point in points)
        {
            Vector3 delta = point.GetPosition() - reference;
            offsets.Add(new Vector3(Vector3.Dot(delta, right), Vector3.Dot(delta, up), Vector3.Dot(delta, forward)));
        }

        return true;
    }

    public static float GetNeed(List<Vector3> offsets, in NeedView view) => view.GetNeed(offsets);

    public static float GetFallback(float pitch)
    {
        if (pitch <= FallbackPitch[0])
            return FallbackNeed[0];

        for (int i = 1; i < FallbackPitch.Length; i++)
        {
            if (pitch <= FallbackPitch[i])
            {
                float t = Mathf.InverseLerp(FallbackPitch[i - 1], FallbackPitch[i], pitch);
                return Mathf.Lerp(FallbackNeed[i - 1], FallbackNeed[i], t);
            }
        }

        return FallbackNeed[FallbackNeed.Length - 1];
    }

    private static ClearancePoint[]? GetPoints(LocalBodyState state)
    {
        ClearanceState clearance = state.Clearance;
        Mesh? mesh = state.OriginalBodyMesh;
        SkinnedMeshRenderer? body = state.BodyRenderer;
        if (mesh == null || body == null)
            return null;

        if (!ReferenceEquals(clearance.Source, mesh) || !ReferenceEquals(clearance.Body, body))
        {
            clearance.Source = mesh;
            clearance.Body = body;
            clearance.Points = null;
            clearance.Version++;
            clearance.Points = CreatePoints(mesh, body);
        }

        return clearance.Points;
    }

    private static ClearancePoint[]? CreatePoints(Mesh mesh, SkinnedMeshRenderer body)
    {
        if (mesh.vertexCount != Constants.Clearance.BodyVertexCount)
            return null;

        Mesh? readable = MeshSurgery.GetReadable(mesh);
        if (readable == null)
            return null;

        Vector3[] vertices = readable.vertices;
        BoneWeight[] weights = readable.boneWeights;
        Matrix4x4[] bindposes = readable.bindposes;
        Transform[] bones = body.bones;
        if (vertices.Length != Constants.Clearance.BodyVertexCount || weights.Length != vertices.Length)
            return null;

        List<Transform> pointBones = new();
        List<Vector3> locals = new();
        List<float> pointWeights = new();
        int[] indices = new int[4];
        float[] shares = new float[4];

        bool AddBone(int index, float share, Vector3 position)
        {
            if (share <= 0f)
                return true;

            if (index < 0 || index >= bones.Length || index >= bindposes.Length || bones[index] == null)
                return false;

            pointBones.Add(bones[index]);
            locals.Add(bindposes[index].MultiplyPoint3x4(position));
            pointWeights.Add(share);
            return true;
        }

        ClearancePoint[] points = new ClearancePoint[Points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            pointBones.Clear();
            locals.Clear();
            pointWeights.Clear();

            foreach ((int vertex, float amount) in Points[i])
            {
                if (vertex < 0 || vertex >= vertices.Length)
                    return null;

                int found = SkinBones.GetShares(weights[vertex], indices, shares);
                if (found == 0)
                    return null;

                Vector3 position = vertices[vertex];
                for (int k = 0; k < found; k++)
                {
                    if (!AddBone(indices[k], amount * shares[k], position))
                        return null;
                }
            }

            float total = 0f;
            foreach (float share in pointWeights)
                total += share;

            if (!(total > 0f))
                return null;

            float[] normalized = pointWeights.ToArray();
            for (int w = 0; w < normalized.Length; w++)
                normalized[w] /= total;

            points[i] = new ClearancePoint
            {
                Bones = pointBones.ToArray(),
                Locals = locals.ToArray(),
                Weights = normalized,
            };
        }

        return points;
    }
}
