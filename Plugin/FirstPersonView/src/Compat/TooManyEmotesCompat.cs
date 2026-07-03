using System.Runtime.CompilerServices;

using TooManyEmotes;
using TooManyEmotes.Patches;

namespace FirstPersonView.Compat;

internal static class TooManyEmotesCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        bool present = Reflection.FindAssembly("TooManyEmotes") != null;
        _isInstalled = present && ConfigManager.EnableTooManyEmotesCompatibility.Value;

        if (present)
            Plugin.Log.LogInfo("TooManyEmotes detected.");
    }

    public static bool IsFirstPersonEmoteActive()
    {
        return _isInstalled && IsFirstPersonEmoteActiveTyped();
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static bool IsFirstPersonEmoteActiveTyped()
    {
        if (!ThirdPersonEmoteController.firstPersonEmotesEnabled)
            return false;

        EmoteControllerPlayer? controller = EmoteControllerPlayer.emoteControllerLocal;
        return controller != null && controller.IsPerformingCustomEmote();
    }
}
