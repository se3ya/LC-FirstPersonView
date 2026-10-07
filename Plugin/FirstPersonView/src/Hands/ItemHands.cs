using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

using GameNetcodeStuff;
using UnityEngine;

namespace FirstPersonView;

internal static class ItemHands
{
    private static readonly HashSet<string> VanillaNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BodyNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> WarnedNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex AimPattern = new("(?i:muzzle|barrel|nozzle|ray ?point|ray ?origin|weapon ?tip|gun ?tip)|(?<![a-z])aim|Aim");
    private static readonly Regex ParticlePattern = new("(?i:spray|shoot|flame|muzzle)");
    private static bool listsParsed;

    public static void Init()
    {
        ConfigManager.VanillaArmsItems.SettingChanged += OnSettingChanged;
        ConfigManager.BodyArmsItems.SettingChanged += OnSettingChanged;
    }

    public static bool UseVanillaArms(PlayerControllerB player, GrabbableObject item) =>
        player.IsInspectingItem || Decide(item);

    public static void Reset()
    {
        listsParsed = false;
    }

    public static void PublishNames()
    {
        StartOfRound? sor = StartOfRound.Instance;
        if (sor == null || sor.allItemsList == null || sor.allItemsList.itemsList == null)
            return;

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> listed = new(StringComparer.OrdinalIgnoreCase);
        Fill(seen, ConfigManager.InstalledItemNames.Value);
        Fill(listed, ConfigManager.VanillaArmsItems.Value + "," + ConfigManager.BodyArmsItems.Value);

        SortedSet<string> names = new(seen, StringComparer.OrdinalIgnoreCase);
        List<string> added = new();
        foreach (Item item in sor.allItemsList.itemsList)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName) || item.itemName.Contains(","))
                continue;

            string name = item.itemName.Trim();
            names.Add(name);
            if (!seen.Contains(name) && !listed.Contains(name) && !item.twoHanded && !item.twoHandedAnimation && PointsWithItem(item.spawnPrefab))
            {
                listed.Add(name);
                added.Add(name);
            }
        }

        if (added.Count > 0)
        {
            string current = ConfigManager.VanillaArmsItems.Value.Trim().TrimEnd(',').Trim();
            string extra = string.Join(", ", added);
            ConfigManager.VanillaArmsItems.Value = current.Length > 0 ? current + ", " + extra : extra;
            Plugin.Log.LogInfo($"Added to 'VanillaArmsItems': {extra}");
        }

        string joined = string.Join(", ", names);
        if (ConfigManager.InstalledItemNames.Value != joined)
            ConfigManager.InstalledItemNames.Value = joined;
    }

    private static void OnSettingChanged(object sender, EventArgs e) => Reset();

    private static bool Decide(GrabbableObject item)
    {
        HandsMode mode = ConfigManager.Hands.Value;
        if (mode != HandsMode.Hybrid)
            return mode != HandsMode.ThirdPerson;

        if (!listsParsed)
        {
            Fill(VanillaNames, ConfigManager.VanillaArmsItems.Value);
            Fill(BodyNames, ConfigManager.BodyArmsItems.Value);
            listsParsed = true;
        }

        Item? properties = item.itemProperties;
        string name = properties != null ? (properties.itemName ?? "").Trim() : "";
        bool inVanilla = VanillaNames.Contains(name);
        bool inBody = BodyNames.Contains(name);

        if (inVanilla && inBody && WarnedNames.Add(name))
            Plugin.Log.LogWarning($"Item '{name}' is in both 'VanillaArmsItems' and 'BodyArmsItems'. Using vanilla arms.");

        if (inVanilla)
            return true;
        if (inBody)
            return false;

        return properties != null && (properties.twoHanded || properties.twoHandedAnimation);
    }

    private static bool PointsWithItem(GameObject? prefab)
    {
        if (prefab == null)
            return false;

        try
        {
            GrabbableObject? grabbable = prefab.GetComponentInChildren<GrabbableObject>(true);
            if (grabbable == null)
                return false;

            if (grabbable is FlashlightItem || grabbable is PatcherTool || grabbable is SprayPaintItem || grabbable is ShotgunItem)
                return true;

            foreach (Camera camera in prefab.GetComponentsInChildren<Camera>(true))
            {
                if (!camera.orthographic)
                    return true;
            }

            return prefab.GetComponentInChildren<LineRenderer>(true) != null || HasAimField(grabbable, prefab.transform);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool HasAimField(GrabbableObject item, Transform root)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type? type = item.GetType(); type != null && type != typeof(GrabbableObject); type = type.BaseType)
        {
            FieldInfo[] fields;
            try
            {
                fields = type.GetFields(flags);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (FieldInfo field in fields)
            {
                if (IsAimField(item, root, field))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAimField(GrabbableObject item, Transform root, FieldInfo field)
    {
        try
        {
            object? value = field.GetValue(item);
            if (value is Transform point)
                return point != root && point.IsChildOf(root) && AimPattern.IsMatch(field.Name);

            return (value is ParticleSystem || value is ParticleSystem[]) && ParticlePattern.IsMatch(field.Name);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Fill(HashSet<string> names, string list)
    {
        names.Clear();
        foreach (string part in list.Split(','))
        {
            string name = part.Trim();
            if (name.Length > 0)
                names.Add(name);
        }
    }
}
