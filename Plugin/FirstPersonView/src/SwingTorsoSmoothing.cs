using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class SwingTorsoSmoothing
{
    private static readonly string[] TorsoPaths =
    {
        "ScavengerModel/metarig/spine/spine.001",
        "ScavengerModel/metarig/spine/spine.001/spine.002",
        "ScavengerModel/metarig/spine/spine.001/spine.002/spine.003",
    };

    private static Transform[] bones = new Transform[0];
    private static Quaternion[] smoothed = new Quaternion[0];
    private static float weight;
    private static float tail;

    public static void Reset()
    {
        bones = new Transform[0];
        smoothed = new Quaternion[0];
        weight = 0f;
        tail = 0f;
    }

    public static void Apply(LocalBodyState state, PlayerControllerB player)
    {
        if (!ConfigManager.SmoothSwingReturn.Value)
        {
            weight = 0f;
            return;
        }

        if (bones.Length == 0 || bones[0] == null)
            CollectBones(player);
        if (bones.Length == 0)
            return;

        bool swinging = player.isPlayerControlled
            && !player.isPlayerDead
            && Players.IsActivelyHolding(player)
            && Players.IsSwingingShovelLike(player);

        if (swinging)
        {
            if (tail <= 0f)
                weight = 1f;

            tail = Constants.SwingTorsoTail;
        }
        else if (tail > 0f)
        {
            tail -= Time.deltaTime;
        }

        if (tail <= 0f)
            weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / Constants.SwingTorsoBlendTime);

        float tau = swinging ? Constants.SwingTorsoTau : Constants.SwingTorsoReturnTau;
        float follow = 1f - Mathf.Exp(-Time.deltaTime / tau);

        Transform? head = state.HeadBone;
        Quaternion headRotation = head != null ? head.rotation : Quaternion.identity;

        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null)
                continue;

            Quaternion live = bone.localRotation;

            if (weight <= 0f)
            {
                smoothed[i] = live;
                continue;
            }

            smoothed[i] = Quaternion.Slerp(smoothed[i], live, follow);
            bone.localRotation = Quaternion.Slerp(live, smoothed[i], weight);
        }

        if (head != null && weight > 0f)
            head.rotation = headRotation;
    }

    private static void CollectBones(PlayerControllerB player)
    {
        Transform root = player.transform;
        int count = 0;
        Transform[] found = new Transform[TorsoPaths.Length];

        foreach (string path in TorsoPaths)
        {
            Transform? bone = root.Find(path);
            if (bone != null)
                found[count++] = bone;
        }

        bones = new Transform[count];
        smoothed = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            bones[i] = found[i];
            smoothed[i] = found[i].localRotation;
        }
    }
}
