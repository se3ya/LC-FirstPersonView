using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPersonView
{
    internal static class MaterialDither
    {
        public const string CrossfadeKeyword = "LOD_FADE_CROSSFADE";
        public static readonly int LodFadeId = Shader.PropertyToID("unity_LODFade");

        private static MaterialPropertyBlock? scratch;

        public static Material[] CollectInstancedMaterials(params Renderer[] renderers)
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
            ToggleKeyword(materials, fade < 0.999f);

            Vector4 lodFade = new(fade, fade, 0f, 0f);
            foreach (Renderer renderer in renderers)
                WriteFade(renderer, lodFade);
        }

        public static void Set(Renderer? renderer, Material[] materials, float fade)
        {
            ToggleKeyword(materials, fade < 0.999f);
            WriteFade(renderer, new Vector4(fade, fade, 0f, 0f));
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
                Constants.HeldItemFadeEndDistance, Constants.HeldItemFadeStartDistance, distance));
        }

        public static float Smooth(float current, float target, float smoothTime)
        {
            float blend = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
            return Mathf.Lerp(current, target, blend);
        }
    }
}