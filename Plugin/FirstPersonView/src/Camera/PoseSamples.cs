using System.Collections.Generic;

using UnityEngine;

namespace FirstPersonView;

internal sealed class PoseSamples
{
    public const float LeanScale = 0.46f;
    public const float LeanWeight = 0.45f;

    private sealed class Bin
    {
        public readonly List<Vector3> Offsets = new();
        public Vector3 Pivot;
        public float Lean;
        public float Pitch;
        public float Yaw;
        public bool Filled;
    }

    private sealed class KeyBins
    {
        public readonly Bin?[] Bins = new Bin?[Constants.Samples.BinCount];
        public int Count;
    }

    private readonly Dictionary<PoseKey, KeyBins> keys = new();

    public int Version;
    public float Scale;
    public float Follow;

    public void Clear()
    {
        keys.Clear();
    }

    public void Add(
        PoseKey key, float phase, List<Vector3> offsets, Vector3 pivot, float lean, float pitch, float yaw)
    {
        if (PoseSeeds.IsCrouchDown(key))
            return;

        if (!keys.TryGetValue(key, out KeyBins? set))
        {
            set = new KeyBins();
            keys[key] = set;
        }

        int index = Mathf.Clamp((int)(phase * Constants.Samples.BinCount), 0, Constants.Samples.BinCount - 1);
        Bin? bin = set.Bins[index];
        if (bin == null)
        {
            bin = new Bin();
            set.Bins[index] = bin;
        }

        if (!bin.Filled)
        {
            bin.Filled = true;
            set.Count++;
        }

        bin.Offsets.Clear();
        bin.Offsets.AddRange(offsets);
        bin.Pivot = pivot;
        bin.Lean = lean;
        bin.Pitch = pitch;
        bin.Yaw = yaw;
    }


    public float GetEnvelope(PoseKey key, in NeedView view)
    {
        float best = Follow > 0f
            ? 0f
            : PoseSeeds.Get(key, view.Pitch, view.Fov) * (Scale / Constants.Samples.VanillaScale);
        if (!keys.TryGetValue(key, out KeyBins? set))
            return best;

        foreach (Bin? bin in set.Bins)
        {
            if (bin == null || !bin.Filled)
                continue;

            float need = GetCorrected(bin, view);
            if (need > best)
                best = need;
        }

        return best;
    }

    public float GetRamp(PoseKey key, in NeedView view)
    {
        if (Follow > 0f)
            return 0f;

        return PoseSeeds.GetRamp(key, view.Pitch, view.Fov) * (Scale / Constants.Samples.VanillaScale);
    }

    private static float GetCorrected(Bin bin, in NeedView view)
    {
        if (Mathf.Abs(Mathf.DeltaAngle(bin.Yaw, view.Yaw)) > Constants.Samples.YawRange)
            return 0f;

        float angle = LeanScale * (bin.Lean / LeanWeight) * (view.Pitch - bin.Pitch);
        return view.GetNeed(bin.Offsets, bin.Pivot, angle);
    }
}
