using System.Reflection;
using System;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView.Compat;

internal static class LethalPhonesCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    private static Type? _playerPhoneType;
    private static FieldInfo? _toggledField;

    private const string PhoneChildName = "PhonePrefab(Clone)";

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        bool present = TryResolveTypes();
        _isInstalled = present && ConfigManager.EnableLethalPhonesCompatibility.Value;

        if (present)
            Plugin.Log.LogInfo("LethalPhones detected.");
    }

    public static bool IsLocalPhoneActive(LocalBodyState state, PlayerControllerB player)
    {
        if (!_isInstalled || _playerPhoneType == null || _toggledField == null)
            return false;

        if (state.LethalPhonesPlayerPhone == null)
        {
            Transform? phone = player.transform.Find(PhoneChildName);
            if (phone == null)
                return false;

            state.LethalPhonesPlayerPhone = phone.GetComponent(_playerPhoneType);
            if (state.LethalPhonesPlayerPhone == null)
                return false;
        }

        return _toggledField.GetValue(state.LethalPhonesPlayerPhone) is true;
    }

    private static bool TryResolveTypes()
    {
        Assembly? assembly = Reflection.FindAssembly("LethalPhones");
        if (assembly == null)
            return false;

        _playerPhoneType = assembly.GetType("Scoops.misc.PlayerPhone");
        if (_playerPhoneType == null)
            return false;

        _toggledField = _playerPhoneType.GetField(
            "toggled", BindingFlags.Public | BindingFlags.Instance);

        return _toggledField != null;
    }
}
