using UnityEngine;

namespace FirstPersonView;

internal static class SkinBones
{
    public static readonly int Count = GetCount();

    public static int GetShares(BoneWeight weight, int[] bones, float[] shares) => GetShares(weight, Count, bones, shares);

    public static int GetShares(BoneWeight weight, int count, int[] bones, float[] shares)
    {
        int[] indices = { weight.boneIndex0, weight.boneIndex1, weight.boneIndex2, weight.boneIndex3 };
        float[] values = { weight.weight0, weight.weight1, weight.weight2, weight.weight3 };
        int found = 0;
        float total = 0f;
        while (found < count && found < 4)
        {
            int best = -1;
            for (int slot = 0; slot < 4; slot++)
            {
                if (values[slot] > 0f && (best < 0 || values[slot] > values[best]))
                    best = slot;
            }

            if (best < 0)
                break;

            bones[found] = indices[best];
            shares[found] = values[best];
            total += values[best];
            values[best] = 0f;
            found++;
        }

        for (int i = 0; i < found; i++)
            shares[i] /= total;

        return found;
    }

    private static int GetCount()
    {
        return QualitySettings.skinWeights switch
        {
            SkinWeights.OneBone => 1,
            SkinWeights.TwoBones => 2,
            _ => 4,
        };
    }
}
