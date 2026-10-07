using UnityEngine;

namespace FirstPersonView;

internal static partial class NeckCap
{
    internal static ClearancePoint[]? CreatePoints(Mesh source, SkinnedMeshRenderer body)
    {
        Mesh? readable = MeshSurgery.GetReadable(source);
        if (readable == null)
            return null;

        Vector3[] vertices = readable.vertices;
        BoneWeight[] weights = readable.boneWeights;
        if (vertices.Length != Constants.Clearance.BodyVertexCount || weights.Length != vertices.Length)
            return null;

        Vector3[] rim = new Vector3[RimCount];
        BoneWeight[] rimWeights = new BoneWeight[RimCount];
        for (int i = 0; i < RimCount; i++)
        {
            rim[i] = vertices[RimVertex[i]];
            rimWeights[i] = weights[RimVertex[i]];
        }

        Shape? shape = BuildShape(rim, rimWeights);
        if (shape == null)
            return null;

        Vector3[] positions = { shape.Positions[CapCount - 1], rim[GateFirst], rim[GateSecond] };
        BoneWeight[] shares = { shape.Weights[CapCount - 1], rimWeights[GateFirst], rimWeights[GateSecond] };
        Matrix4x4[] bindposes = readable.bindposes;
        Transform[] bones = body.bones;
        ClearancePoint[] points = new ClearancePoint[positions.Length];
        for (int i = 0; i < points.Length; i++)
        {
            ClearancePoint? point = CreatePoint(positions[i], shares[i], bones, bindposes);
            if (point == null)
                return null;

            points[i] = point;
        }

        return points;
    }

    private static ClearancePoint? CreatePoint(
        Vector3 position, BoneWeight weight, Transform[] bones, Matrix4x4[] bindposes)
    {
        int[] indices = new int[4];
        float[] shares = new float[4];
        int found = SkinBones.GetShares(weight, indices, shares);
        if (found == 0)
            return null;

        Transform[] pointBones = new Transform[found];
        Vector3[] locals = new Vector3[found];
        float[] amounts = new float[found];
        for (int i = 0; i < found; i++)
        {
            int bone = indices[i];
            if (bone < 0 || bone >= bones.Length || bone >= bindposes.Length || bones[bone] == null)
                return null;

            pointBones[i] = bones[bone];
            locals[i] = bindposes[bone].MultiplyPoint3x4(position);
            amounts[i] = shares[i];
        }

        return new ClearancePoint
        {
            Bones = pointBones,
            Locals = locals,
            Weights = amounts,
        };
    }
}
