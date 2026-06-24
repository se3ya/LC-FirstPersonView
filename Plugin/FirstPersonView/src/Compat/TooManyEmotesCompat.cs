using System.Reflection;
using System;

namespace FirstPersonView.Compat;

internal static class TooManyEmotesCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    private static PropertyInfo? _firstPersonEmotesEnabledProperty;     // ThirdPersonEmoteController.firstPersonEmotesEnabled (static)
    private static PropertyInfo? _emoteControllerLocalProperty;     // EmoteControllerPlayer.emoteControllerLocal (static)
    private static MethodInfo? _isPerformingCustomEmoteMethod;      // EmoteController.IsPerformingCustomEmote()

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        bool present = TryResolveTypes();
        _isInstalled = present && ConfigManager.EnableTooManyEmotesCompatibility.Value;

        if (present)
            Plugin.Log.LogInfo("TooManyEmotes detected.");
    }

    public static bool IsFirstPersonEmoteActive()
    {
        if (!_isInstalled
            || _firstPersonEmotesEnabledProperty == null
            || _emoteControllerLocalProperty == null
            || _isPerformingCustomEmoteMethod == null)
            return false;

        if (_firstPersonEmotesEnabledProperty.GetValue(null) is not true)
            return false;

        object? controller = _emoteControllerLocalProperty.GetValue(null);
        return controller != null && _isPerformingCustomEmoteMethod.Invoke(controller, null) is true;
    }

    private static bool TryResolveTypes()
    {
        Assembly? assembly = Reflection.FindAssembly("TooManyEmotes");
        if (assembly == null)
            return false;

        Type? controllerType = assembly.GetType("TooManyEmotes.Patches.ThirdPersonEmoteController");
        Type? emotePlayerType = assembly.GetType("TooManyEmotes.EmoteControllerPlayer");
        if (controllerType == null || emotePlayerType == null)
            return false;

        _firstPersonEmotesEnabledProperty = controllerType.GetProperty(
            "firstPersonEmotesEnabled", BindingFlags.Public | BindingFlags.Static);
        _emoteControllerLocalProperty = emotePlayerType.GetProperty(
            "emoteControllerLocal", BindingFlags.Public | BindingFlags.Static);
        _isPerformingCustomEmoteMethod = emotePlayerType.GetMethod(
            "IsPerformingCustomEmote", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

        return _firstPersonEmotesEnabledProperty != null
            && _emoteControllerLocalProperty != null
            && _isPerformingCustomEmoteMethod != null;
    }
}