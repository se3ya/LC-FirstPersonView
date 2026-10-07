using System.Collections.Generic;
using System;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal enum Posture
{
    Standing,
    Crouch,
    Other,
}

internal readonly struct PoseKey : IEquatable<PoseKey>
{
    private static readonly int IdleHash = Animator.StringToHash("Idle1");
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int SprintHash = Animator.StringToHash("Sprint");
    private static readonly int SidewaysHash = Animator.StringToHash("WalkSideways");
    private static readonly int WalkAltHash = Animator.StringToHash("Walk 0");
    private static readonly int HinderedHash = Animator.StringToHash("WalkHindered");
    private static readonly int LimpHash = Animator.StringToHash("LimpWalk");
    private static readonly int CrouchDownHash = Animator.StringToHash("CrouchDown");
    private static readonly int CrouchIdleHash = Animator.StringToHash("CrouchIdle");
    private static readonly int CrouchWalkHash = Animator.StringToHash("CrouchWalk");

    public static readonly int AirHash = Animator.StringToHash("Air");

    public readonly int Base;
    public readonly int Clip;
    public readonly int Layer1;
    public readonly int Layer3;
    public readonly int Layer5;
    public readonly int Melee;
    public readonly int Emote;
    public readonly int Enemy;

    public PoseKey(int baseState, int clip, int layer1, int layer3, int layer5, int melee, int emote, int enemy)
    {
        Base = baseState;
        Clip = clip;
        Layer1 = layer1;
        Layer3 = layer3;
        Layer5 = layer5;
        Melee = melee;
        Emote = emote;
        Enemy = enemy;
    }

    public bool Equals(PoseKey other) =>
        Base == other.Base
        && Clip == other.Clip
        && Layer1 == other.Layer1
        && Layer3 == other.Layer3
        && Layer5 == other.Layer5
        && Melee == other.Melee
        && Emote == other.Emote
        && Enemy == other.Enemy;

    public override bool Equals(object? obj) => obj is PoseKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Base;
            hash = (hash * 31) + Clip;
            hash = (hash * 31) + Layer1;
            hash = (hash * 31) + Layer3;
            hash = (hash * 31) + Layer5;
            hash = (hash * 31) + Melee;
            hash = (hash * 31) + Emote;
            hash = (hash * 31) + Enemy;
            return hash;
        }
    }

    public Posture GetPosture()
    {
        if (Layer1 != 0 || Layer3 != 0 || Layer5 != 0 || Emote != 0 || Enemy != 0)
            return Posture.Other;

        if (Base == IdleHash || IsMoving())
            return Posture.Standing;

        if (Base == CrouchIdleHash || Base == CrouchWalkHash || Base == CrouchDownHash)
            return Posture.Crouch;

        return Posture.Other;
    }

    public bool IsStanding() =>
        Layer1 == 0 && Layer5 == 0 && Emote == 0 && Enemy == 0 && (Base == IdleHash || IsMoving());

    public bool IsMoving() =>
        Base == WalkHash || Base == SprintHash || Base == AirHash
        || Base == SidewaysHash || Base == WalkAltHash || Base == HinderedHash || Base == LimpHash;

    public bool InGroup(PoseKey other)
    {
        Posture posture = GetPosture();
        return posture == other.GetPosture() && (posture != Posture.Other || Equals(other));
    }
}

internal sealed class PoseTracker
{
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LandHash = Animator.StringToHash("JumpLand");
    private static readonly int FallHash = Animator.StringToHash("FallNoJump");
    private static readonly int HurtHash = Animator.StringToHash("Hurt1");
    private static readonly int OverheatHash = Animator.StringToHash("GunOverheat");

    private static readonly int EnemyHash = Animator.StringToHash("Enemy");
    private static readonly Dictionary<int, int> ClipHashes = new();
    private static readonly Dictionary<Type, bool> MeleeTypes = new();


    private readonly List<AnimatorClipInfo> clips = new();
    private readonly float[] bandTime = new float[6];

    private object? enemy;
    private int enemyHash;
    private int clipHeld;

    public PoseKey Key;
    public PoseKey Previous;
    public bool HasKey;
    public bool Changed;
    public bool CanSample;
    public float Phase;
    public float KeyTime;

    public PoseTracker()
    {
        Reset();
    }

    public void Reset()
    {
        HasKey = false;
        Changed = false;
        CanSample = false;
        clipHeld = 0;
        for (int i = 0; i < bandTime.Length; i++)
            bandTime[i] = float.NegativeInfinity;
    }

    public void Update(PlayerControllerB player, int emote, float emotePhase, bool emoteBlend)
    {
        float now = Time.time;
        Animator? animator = player.playerBodyAnimator;

        PoseKey key;
        int defining = 0;
        bool settled = true;

        if (emote != 0)
        {
            key = new PoseKey(0, 0, 0, 0, 0, 0, emote, 0);
            Phase = emotePhase - Mathf.Floor(emotePhase);
            settled = !emoteBlend;
            clipHeld = 0;
        }
        else if (animator == null || animator.layerCount == 0)
        {
            key = new PoseKey(0, 0, 0, 0, 0, 0, 0, 0);
            settled = false;
            clipHeld = 0;
        }
        else
        {
            bool moving = animator.IsInTransition(0);
            AnimatorStateInfo state = moving
                ? animator.GetNextAnimatorStateInfo(0)
                : animator.GetCurrentAnimatorStateInfo(0);

            int baseState = state.shortNameHash;
            if (baseState == JumpHash || baseState == LandHash || baseState == FallHash)
                baseState = PoseKey.AirHash;

            int layer1 = GetLayerKey(animator, player, 1);
            int layer3 = GetLayerKey(animator, player, 3);
            int layer5 = GetLayerKey(animator, player, 5);
            bool known = TryGetClip(animator, moving, out int clip);

            bool quick = Constants.Camera.Release != ShiftRelease.Dwell && IsQuickPlaying(animator);
            if (quick && IsQuick(layer5))
                layer5 = 0;

            defining = layer5 != 0 ? 5 : layer3 != 0 ? 3 : layer1 != 0 ? 1 : 0;
            float time = animator.GetCurrentAnimatorStateInfo(defining).normalizedTime;
            Phase = time - Mathf.Floor(time);

            settled = !moving;
            settled &= CheckLayer(animator, 1, layer1, now);
            settled &= CheckLayer(animator, 3, layer3, now);
            settled &= CheckLayer(animator, 5, layer5, now);
            settled &= !quick;

            if (!known && HasKey && clipHeld < Constants.Samples.ClipHold)
            {
                clipHeld++;
                key = Key;
                settled = false;
            }
            else
            {
                clipHeld = 0;
                key = new PoseKey(
                    baseState, clip, layer1, layer3, layer5, IsHoldingMelee(player) ? 1 : 0, 0, GetEnemyKey(player));
            }
        }

        Changed = HasKey && !key.Equals(Key);
        if (Changed)
            Previous = Key;

        if (!HasKey || Changed)
            KeyTime = now;

        Key = key;
        HasKey = true;
        CanSample = settled && now - KeyTime >= Constants.Samples.EnterDelay;
    }

    private bool CheckLayer(Animator animator, int layer, int keyed, float now)
    {
        if (layer >= animator.layerCount)
            return true;

        float weight = animator.GetLayerWeight(layer);
        if (weight > Constants.Samples.BandLow && weight < Constants.Samples.BandHigh)
            bandTime[layer] = now;

        if (now - bandTime[layer] < Constants.Samples.BandDelay)
            return false;

        return keyed == 0 || !animator.IsInTransition(layer);
    }

    private static bool IsQuick(int hash) => hash == HurtHash || hash == OverheatHash;

    private static bool IsQuickPlaying(Animator animator)
    {
        if (animator.layerCount <= 5)
            return false;

        if (IsQuick(animator.GetCurrentAnimatorStateInfo(5).shortNameHash))
            return true;

        return animator.IsInTransition(5) && IsQuick(animator.GetNextAnimatorStateInfo(5).shortNameHash);
    }

    private static bool IsHoldingMelee(PlayerControllerB player)
    {
        if (!Players.IsActivelyHolding(player))
            return false;

        GrabbableObject held = player.currentlyHeldObjectServer;
        if (held is Shovel)
            return true;

        Type type = held.GetType();
        if (MeleeTypes.TryGetValue(type, out bool melee))
            return melee;

        for (Type? current = type; current != null && !melee; current = current.BaseType)
            melee = current.Name == "JMeleeWeapon";

        MeleeTypes[type] = melee;
        return melee;
    }

    private static int GetLayerKey(Animator animator, PlayerControllerB player, int layer)
    {
        if (layer >= animator.layerCount
            || animator.GetLayerWeight(layer) < Constants.Samples.KeyWeight
            || animator.GetCurrentAnimatorClipInfoCount(layer) <= 0)
        {
            return 0;
        }

        if (layer == 5 && player.inVehicleAnimation)
        {
            InteractTrigger? trigger = player.currentTriggerInAnimationWith;
            if (trigger != null)
                return trigger.GetInstanceID();
        }

        return animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
    }

    private bool TryGetClip(Animator animator, bool next, out int hash)
    {
        hash = 0;
        clips.Clear();
        if (next)
            animator.GetNextAnimatorClipInfo(0, clips);
        else
            animator.GetCurrentAnimatorClipInfo(0, clips);

        AnimationClip? best = null;
        float weight = 0f;
        for (int i = 0; i < clips.Count; i++)
        {
            AnimatorClipInfo info = clips[i];
            if (info.clip != null && info.weight > weight)
            {
                best = info.clip;
                weight = info.weight;
            }
        }

        if (best == null)
            return false;

        int id = best.GetInstanceID();
        if (!ClipHashes.TryGetValue(id, out hash))
        {
            string name = best.name;
            hash = Animator.StringToHash(name);
            ClipHashes[id] = hash;
        }

        return true;
    }

    private int GetEnemyKey(PlayerControllerB player)
    {
        EnemyAI? other = player.inAnimationWithEnemy;
        if (other == null)
            return 0;

        EnemyType? type = other.enemyType;
        if (type == null)
            return EnemyHash;

        if (!ReferenceEquals(type, enemy))
        {
            enemy = type;
            enemyHash = Animator.StringToHash(type.enemyName ?? "Enemy");
        }

        return enemyHash;
    }
}
