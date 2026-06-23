using System.Reflection;
using System;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView.Compat;

internal static class ModelReplacementCompat
{
    private static bool _initialized;
    private static bool _isInstalled;

    private static Type? _bodyReplacementType;
    private static FieldInfo? _cosmeticAvatarField;
    private static MethodInfo? _getAvatarBoneMethod;

    private static Transform? _scaledHeadBone;
    private static Vector3 _scaledHeadOriginalScale = Vector3.one;

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isInstalled = ConfigManager.EnableModelReplacementCompatibility.Value && TryResolveTypes();

        if (_isInstalled)
            Plugin.Log.LogInfo("ModelReplacementAPI detected.");
    }

    public static bool HasReplacement(PlayerControllerB player)
    {
        return _isInstalled && _bodyReplacementType != null && player.GetComponent(_bodyReplacementType) != null;
    }

    public static void SetLocalHeadHidden(PlayerControllerB player, bool hidden)
    {
        if (!_isInstalled)
            return;

        Transform? head = hidden ? ResolveHeadBone(player) : null;
        if (head == _scaledHeadBone)
        {
            if (head != null)
                head.localScale = Vector3.zero;   // keep it collapsed
            return;
        }

        if (_scaledHeadBone != null)
            _scaledHeadBone.localScale = _scaledHeadOriginalScale;

        _scaledHeadBone = head;
        if (head != null)
        {
            _scaledHeadOriginalScale = head.localScale;
            head.localScale = Vector3.zero;
        }
    }

    private static Transform? ResolveHeadBone(PlayerControllerB player)
    {
        if (_bodyReplacementType == null || _cosmeticAvatarField == null || _getAvatarBoneMethod == null)
            return null;

        Component? body = player.GetComponent(_bodyReplacementType);
        if (body == null)
            return null;

        object? avatar = _cosmeticAvatarField.GetValue(body);
        if (avatar == null)
            return null;   // model not loaded yet

        return _getAvatarBoneMethod.Invoke(avatar, new object[] { Constants.HeadBoneName }) as Transform;
    }

    private static bool TryResolveTypes()
    {
        Assembly? assembly = Reflection.FindAssembly("ModelReplacementAPI");
        if (assembly == null)
            return false;

        _bodyReplacementType = assembly.GetType("ModelReplacement.BodyReplacementBase");
        Type? avatarType = assembly.GetType("ModelReplacement.AvatarBodyUpdater.AvatarUpdater");
        if (_bodyReplacementType == null || avatarType == null)
            return false;

        _cosmeticAvatarField = _bodyReplacementType.GetField(
            "cosmeticAvatar", BindingFlags.Public | BindingFlags.Instance);
        _getAvatarBoneMethod = avatarType.GetMethod(
            "GetAvatarTransformFromBoneName",
            BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);

        return _cosmeticAvatarField != null && _getAvatarBoneMethod != null;
    }
}