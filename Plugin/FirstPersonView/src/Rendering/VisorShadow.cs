using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonView;

internal static class VisorShadow
{
    private static readonly List<Renderer> Renderers = new();
    private static readonly List<Renderer> Changed = new();
    private static readonly List<ShadowCastingMode> Modes = new();
    private static readonly List<Renderer> Hidden = new();
    private static Transform? visor;
    private static Camera? owner;
    private static int childCount = -1;
    private static bool active;

    public static void Track(Transform? localVisor, Camera? camera, bool show)
    {
        if (localVisor != visor || (localVisor != null && localVisor.childCount != childCount))
        {
            Renderers.Clear();
            if (localVisor != null)
                localVisor.GetComponentsInChildren(includeInactive: true, Renderers);

            visor = localVisor;
            childCount = localVisor != null ? localVisor.childCount : -1;
        }

        owner = camera;
        active = show && localVisor != null && camera != null;
    }

    public static void ApplyForCamera(Camera camera)
    {
        if (!active || camera != owner || !FirstPersonBody.HideActive)
            return;

        Restore();
        foreach (Renderer renderer in Renderers)
        {
            if (renderer == null)
                continue;

            ShadowCastingMode mode = renderer.shadowCastingMode;
            if (mode == ShadowCastingMode.Off)
                continue;

            if (mode == ShadowCastingMode.ShadowsOnly && !renderer.forceRenderingOff)
            {
                renderer.forceRenderingOff = true;
                Hidden.Add(renderer);
            }

            Changed.Add(renderer);
            Modes.Add(mode);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    public static void RestoreAfterCamera(Camera camera)
    {
        if (camera == owner)
            Restore();
    }

    public static void Reset()
    {
        Restore();
        Renderers.Clear();
        visor = null;
        owner = null;
        childCount = -1;
        active = false;
    }

    private static void Restore()
    {
        for (int i = 0; i < Changed.Count; i++)
        {
            Renderer renderer = Changed[i];
            if (renderer != null && renderer.shadowCastingMode == ShadowCastingMode.Off)
                renderer.shadowCastingMode = Modes[i];
        }

        foreach (Renderer renderer in Hidden)
        {
            if (renderer != null && renderer.forceRenderingOff)
                renderer.forceRenderingOff = false;
        }

        Changed.Clear();
        Modes.Clear();
        Hidden.Clear();
    }
}
