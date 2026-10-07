using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonView;

internal static class VisorGlass
{
    private static readonly int ZTestForward = Shader.PropertyToID("_ZTestDepthEqualForOpaque");
    private static readonly int ZTestTransparent = Shader.PropertyToID("_ZTestTransparent");
    private static readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");

    private sealed class GlassRenderer
    {
        public Renderer Renderer = null!;
        public Material[] Originals = null!;
        public Material?[] Copies = null!;
        public int OriginalLayer;
    }

    private static readonly List<GlassRenderer> Glass = new();
    private static Transform? appliedVisor;

    public static void Apply(Transform? visor)
    {
        if (visor == null)
            return;

        if (appliedVisor != visor)
        {
            Restore();
            Capture(visor);
            appliedVisor = visor;
        }

        Sync();
    }

    public static void Restore()
    {
        foreach (GlassRenderer glass in Glass)
        {
            if (glass.Renderer != null)
            {
                glass.Renderer.sharedMaterials = glass.Originals;
                glass.Renderer.gameObject.layer = glass.OriginalLayer;
            }

            foreach (Material? copy in glass.Copies)
            {
                if (copy != null)
                    Object.Destroy(copy);
            }
        }

        Glass.Clear();
        appliedVisor = null;
    }

    private static void Capture(Transform visor)
    {
        foreach (Renderer renderer in visor.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            Material[] originals = renderer.sharedMaterials;
            Material[] swapped = (Material[])originals.Clone();
            Material?[] copies = new Material?[originals.Length];
            bool found = false;

            for (int i = 0; i < originals.Length; i++)
            {
                Material source = originals[i];
                if (source == null || !source.HasProperty(SurfaceType) || source.GetFloat(SurfaceType) < 1f)
                    continue;

                Material copy = new(source) { name = source.name + " (FPV)" };
                DrawOnTop(copy);
                copies[i] = copy;
                swapped[i] = copy;
                found = true;
            }

            if (!found)
                continue;

            Glass.Add(new GlassRenderer
            {
                Renderer = renderer,
                Originals = originals,
                Copies = copies,
                OriginalLayer = renderer.gameObject.layer,
            });

            renderer.sharedMaterials = swapped;
            renderer.gameObject.layer = Constants.Layers.HelmetVisor;
        }
    }

    private static void Sync()
    {
        foreach (GlassRenderer glass in Glass)
        {
            Renderer renderer = glass.Renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            for (int i = 0; i < glass.Copies.Length; i++)
            {
                Material? copy = glass.Copies[i];
                Material source = glass.Originals[i];
                if (copy == null || source == null)
                    continue;

                copy.CopyPropertiesFromMaterial(source);
                DrawOnTop(copy);
            }
        }
    }

    private static void DrawOnTop(Material material)
    {
        material.SetInt(ZTestForward, (int)CompareFunction.Always);
        material.SetInt(ZTestTransparent, (int)CompareFunction.Always);
    }
}
