using System.Reflection;
using System.Runtime.CompilerServices;
using System;

using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

using TooManyEmotes;
using TooManyEmotes.Networking;
using TooManyEmotes.Patches;

namespace FirstPersonView;

internal static class TooManyEmotesCompat
{
    private const string HarmonyId = "TooManyEmotes";

    private static bool _initialized;
    private static bool _isInstalled;
    private static bool _isOff;
    private static bool _lookFailed;
    private static bool _lookPatched;
    private static object? _hashedEmote;
    private static int _emoteHash;

    public static bool IsHooked { get; private set; }

    private static bool IsAvailable => _isInstalled && !_isOff;

    public static bool IsEnabled => IsAvailable && ConfigManager.EnableTooManyEmotesCompatibility.Value;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        _isInstalled = Reflection.FindAssembly("TooManyEmotes") != null;

        if (!_isInstalled)
            return;

        Plugin.Log.LogInfo("TooManyEmotes detected.");
        try
        {
            IsHooked = Patch("LateUpdate", nameof(EmoteLateUpdate_Postfix));
            Patch("StopPerformingEmote", nameof(StopPerformingEmote_Postfix));
            if (ConfigManager.EnableTooManyEmotesCompatibility.Value)
                PatchLookInput();
            else
                ConfigManager.EnableTooManyEmotesCompatibility.SettingChanged += OnCompatChanged;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not patch TooManyEmotes.\n{ex}");
        }
    }

    public static bool IsFirstPersonEmoteActive()
    {
        try
        {
            return IsEnabled && IsFirstPersonEmoteActiveTyped();
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    public static bool IsMovingWhileEmoting()
    {
        try
        {
            return IsAvailable && IsMovingTyped();
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    public static int GetEmoteHash()
    {
        try
        {
            return IsAvailable ? GetEmoteHashTyped() : 0;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return 0;
        }
    }

    public static float GetEmotePhase()
    {
        try
        {
            return IsAvailable ? GetEmotePhaseTyped() : 0f;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return 0f;
        }
    }

    public static bool IsEmoteBlending()
    {
        try
        {
            return IsAvailable && IsBlendingTyped();
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    private static void Disable(Exception ex)
    {
        if (_isOff)
            return;

        _isOff = true;
        IsHooked = false;
        Plugin.Log.LogWarning($"TooManyEmotes changed, first-person emote support is off. {ex.GetType().Name}: {ex.Message}");
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool IsFirstPersonEmoteActiveTyped()
    {
        if (!ThirdPersonEmoteController.firstPersonEmotesEnabled)
            return false;

        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        return controller != null && controller.IsPerformingCustomEmote();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool IsMovingTyped()
    {
        ConfigSync? sync = ConfigSync.instance;
        if (sync != null && sync.syncForceDisableMovingWhileEmoting)
            return false;

        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        UnlockableEmote? emote = controller != null ? controller.performingEmote : null;
        return ThirdPersonEmoteController.allowMovingWhileEmoting || (emote != null && emote.canMoveWhileEmoting);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int GetEmoteHashTyped()
    {
        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        UnlockableEmote? emote = controller != null ? controller.performingEmote : null;
        string? name = emote != null ? emote.emoteName : null;
        if (emote == null || name == null)
            return 0;

        if (!ReferenceEquals(emote, _hashedEmote))
        {
            _hashedEmote = emote;
            _emoteHash = Animator.StringToHash(name);
            if (_emoteHash == 0)
                _emoteHash = 1;
        }

        return _emoteHash;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static float GetEmotePhaseTyped()
    {
        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        return controller != null && controller.animator != null ? controller.currentAnimationTimeNormalized : 0f;
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool IsBlendingTyped()
    {
        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        Animator? animator = controller != null ? controller.animator : null;
        return animator != null && animator.IsInTransition(0);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool Patch(string method, string postfix)
    {
        MethodInfo? target = AccessTools.DeclaredMethod(typeof(EmoteControllerPlayer), method);
        if (target == null)
        {
            Plugin.Log.LogWarning($"Could not find EmoteControllerPlayer.{method}.");
            return false;
        }

        Plugin.Harmony.Patch(
            target,
            postfix: new HarmonyMethod(AccessTools.Method(typeof(TooManyEmotesCompat), postfix))
            {
                priority = Priority.Last,
            });
        return true;
    }

    private static void OnCompatChanged(object? sender, EventArgs args)
    {
        if (!ConfigManager.EnableTooManyEmotesCompatibility.Value)
            return;

        try
        {
            PatchLookInput();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not patch TooManyEmotes.\n{ex}");
        }
    }

    private static void PatchLookInput()
    {
        if (_lookPatched)
            return;

        _lookPatched = true;
        MethodInfo? target = AccessTools.DeclaredMethod(typeof(PlayerControllerB), "PlayerLookInput");
        if (target == null)
        {
            Plugin.Log.LogWarning("Could not find PlayerControllerB.PlayerLookInput.");
            return;
        }

        Plugin.Harmony.Patch(
            target,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(TooManyEmotesCompat), nameof(LookInput_Prefix)))
            {
                before = new[] { HarmonyId },
            });
    }

    private static void LookInput_Prefix(PlayerControllerB __instance)
    {
        try
        {
            LocalBodyViewController.OnLookInput(__instance);
        }
        catch (Exception ex)
        {
            if (!_lookFailed)
            {
                _lookFailed = true;
                Plugin.Log.LogError($"Look input update failed.\n{ex}");
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void EmoteLateUpdate_Postfix(EmoteControllerPlayer __instance)
    {
        PlayerControllerB? player = GetLocalPlayer(__instance);
        if (player != null)
            LocalBodyViewController.TickEmote(player);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static void StopPerformingEmote_Postfix(EmoteControllerPlayer __instance)
    {
        PlayerControllerB? player = GetLocalPlayer(__instance);
        if (player != null)
            LocalBodyViewController.OnEmoteStopped(player);
    }

    private static PlayerControllerB? GetLocalPlayer(EmoteControllerPlayer controller)
    {
        try
        {
            return IsAvailable ? GetLocalPlayerTyped(controller) : null;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static PlayerControllerB? GetLocalPlayerTyped(EmoteControllerPlayer controller)
    {
        return controller.isLocalPlayer ? controller.playerController : null;
    }
}
