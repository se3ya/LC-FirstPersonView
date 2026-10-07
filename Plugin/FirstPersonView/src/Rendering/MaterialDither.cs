using System.Collections.Generic;
using System;

using UnityEngine;

namespace FirstPersonView;

internal static class MaterialDither
{
    public const float Opaque = 0.999f;

    private const string CrossfadeKeyword = "LOD_FADE_CROSSFADE";
    private static readonly int LodFadeId = Shader.PropertyToID("unity_LODFade");

    private static MaterialPropertyBlock? scratch;

    public static Material[] CollectInstancedMaterials(Renderer[] renderers)
    {
        if (renderers.Length == 0)
            return Array.Empty<Material>();

        List<Material> materials = new();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            foreach (Material material in renderer.materials)
            {
                if (material != null)
                    materials.Add(material);
            }
        }

        return materials.ToArray();
    }

    public static void Set(Renderer[] renderers, Material[] materials, float fade)
    {
        ToggleKeyword(materials, true);

        Vector4 lodFade = new(fade, fade, 0f, 0f);
        foreach (Renderer renderer in renderers)
            WriteFade(renderer, lodFade);
    }

    public static void RecordBlocks(Renderer[] renderers, bool[] hadBlock)
    {
        for (int i = 0; i < renderers.Length && i < hadBlock.Length; i++)
        {
            Renderer renderer = renderers[i];
            hadBlock[i] = renderer != null && renderer.HasPropertyBlock();
        }
    }

    public static void Clear(Renderer[] renderers, Material[] materials, bool[] hadBlock)
    {
        ToggleKeyword(materials, false);

        Vector4 opaque = new(1f, 1f, 0f, 0f);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (i < hadBlock.Length && hadBlock[i])
                WriteFade(renderer, opaque);
            else
                renderer.SetPropertyBlock(null);
        }
    }

    private static void ToggleKeyword(Material[] materials, bool enable)
    {
        foreach (Material material in materials)
        {
            if (material == null)
                continue;

            if (enable)
                material.EnableKeyword(CrossfadeKeyword);
            else
                material.DisableKeyword(CrossfadeKeyword);
        }
    }

    private static void WriteFade(Renderer? renderer, Vector4 lodFade)
    {
        if (renderer == null)
            return;

        scratch ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(scratch);
        scratch.SetVector(LodFadeId, lodFade);
        renderer.SetPropertyBlock(scratch);
    }

    public static float ProximityFade(float distance)
    {
        return Mathf.Clamp01(Mathf.InverseLerp(
            ConfigManager.FadeEndDistance.Value, ConfigManager.FadeStartDistance.Value, distance));
    }

    public static float Smooth(float current, float target, float smoothTime)
    {
        float blend = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
        return Mathf.Lerp(current, target, blend);
    }
}
