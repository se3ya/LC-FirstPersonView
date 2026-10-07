using UnityEngine;

namespace FirstPersonView;

public static class FirstPersonViewApi
{
    private static float suppression;

    public static float ExternalSuppression
    {
        get => suppression;
        set => suppression = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
    }
}
