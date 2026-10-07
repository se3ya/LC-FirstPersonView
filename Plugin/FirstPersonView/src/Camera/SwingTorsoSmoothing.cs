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
    private static Quaternion[] written = new Quaternion[0];
    private static float weight;
    private static float tail;
    private static float wait;
    private static bool frozen;
    private static bool swung;

    public static bool IsActive => weight > 0f;

    public static void Reset()
    {
        bones = new Transform[0];
        smoothed = new Quaternion[0];
        written = new Quaternion[0];
        weight = 0f;
        tail = 0f;
        wait = 0f;
        frozen = false;
        swung = false;
    }

    public static void Apply(LocalBodyState state, PlayerControllerB player)
    {
        if (!ConfigManager.SmoothSwingReturn.Value)
        {
            if (bones.Length > 0)
                Reset();

            return;
        }

        if (bones.Length == 0 || bones[0] == null)
            CollectBones(player);
        if (bones.Length == 0)
            return;

        bool holding = player.isPlayerControlled
            && !player.isPlayerDead
            && Players.IsActivelyHolding(player);
        bool swinging = holding && Players.IsSwingingShovelLike(player);

        UpdateFrozen(player, holding, swinging);
        swung = swinging;

        if (swinging || frozen)
        {
            if (tail <= 0f)
                weight = 1f;

            tail = Constants.Swing.TorsoTail;
        }
        else if (tail > 0f)
        {
            tail -= Time.deltaTime;
        }

        if (tail <= 0f)
            weight = Mathf.MoveTowards(weight, 0f, Time.deltaTime / Constants.Swing.TorsoBlendTime);

        float tau = swinging ? Constants.Swing.TorsoTau : Constants.Swing.TorsoReturnTau;
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
                written[i] = live;
                continue;
            }

            if (!frozen)
                smoothed[i] = Quaternion.Slerp(smoothed[i], live, follow);

            written[i] = Quaternion.Slerp(live, smoothed[i], weight);
            bone.localRotation = written[i];
        }

        if (head != null && weight > 0f)
            head.rotation = headRotation;
    }

    private static void UpdateFrozen(PlayerControllerB player, bool holding, bool swinging)
    {
        if (!holding)
        {
            frozen = false;
            return;
        }

        if (swinging)
        {
            wait = 0f;
            if (!swung)
                StartSwing(player);

            return;
        }

        if (!frozen)
            return;

        wait += Time.deltaTime;
        frozen = wait < Constants.Swing.TorsoHoldLimit && !Players.IsInHoldLung(player);
    }

    private static void StartSwing(PlayerControllerB player)
    {
        frozen = !player.isCrouching;
        if (!frozen)
            return;

        for (int i = 0; i < smoothed.Length; i++)
            smoothed[i] = written[i];
    }

    private static void CollectBones(PlayerControllerB player)
    {
        if (bones.Length > 0)
            Reset();

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
        written = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            bones[i] = found[i];
            smoothed[i] = found[i].localRotation;
            written[i] = smoothed[i];
        }
    }
}
