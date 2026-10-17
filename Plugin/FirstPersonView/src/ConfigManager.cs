using System.Collections.Generic;

using BepInEx.Configuration;

namespace FirstPersonView;

public static class ConfigManager
{
    public static ConfigEntry<HeldItemFadeMode> HeldItemFadeStyle = null!;

    public static ConfigEntry<HandsMode> Hands = null!;
    public static ConfigEntry<string> VanillaArmsItems = null!;
    public static ConfigEntry<string> BodyArmsItems = null!;
    public static ConfigEntry<string> InstalledItemNames = null!;
    public static ConfigEntry<bool> VanillaArmsWhileSprinting = null!;
    public static ConfigEntry<bool> VanillaArmsWhileCrouching = null!;
    public static ConfigEntry<bool> VanillaArmsWhileJumping = null!;
    public static ConfigEntry<bool> VanillaArmsWhileWalking = null!;
    public static ConfigEntry<bool> VanillaArmsWhileEmoting = null!;
    public static ConfigEntry<bool> VanillaArmsNearWall = null!;
    public static ConfigEntry<ItemDitherMode> ItemDither = null!;
    public static ConfigEntry<float> FadeStartDistance = null!;
    public static ConfigEntry<float> FadeEndDistance = null!;
    public static ConfigEntry<float> MinVisibility = null!;
    public static ConfigEntry<bool> ItemInBodyHand = null!;

    public static ConfigEntry<float> HandOffsetX = null!;
    public static ConfigEntry<float> HandOffsetY = null!;
    public static ConfigEntry<float> HandOffsetZ = null!;
    public static ConfigEntry<float> LookDownMargin = null!;
    public static ConfigEntry<float> FadeCoverage = null!;
    public static ConfigEntry<float> HeadFollow = null!;
    public static ConfigEntry<bool> HideBodyInCruiser = null!;
    public static ConfigEntry<bool> SmoothSwingReturn = null!;

    public static ConfigEntry<bool> EnableMoreCompanyCompatibility = null!;
    public static ConfigEntry<bool> EnableTooManyEmotesCompatibility = null!;
    public static ConfigEntry<bool> EnableLethalPhonesCompatibility = null!;
    public static ConfigEntry<bool> ShowMoreCompanyHat = null!;
    public static ConfigEntry<bool> ShowMoreCompanyChest = null!;
    public static ConfigEntry<bool> ShowMoreCompanyHip = null!;
    public static ConfigEntry<bool> ShowMoreCompanyRightLowerArm = null!;
    public static ConfigEntry<bool> ShowMoreCompanyLeftShin = null!;
    public static ConfigEntry<bool> ShowMoreCompanyRightShin = null!;
    public static ConfigEntry<bool> ShowCosmetics = null!;
    public static ConfigEntry<string> HideCosmetics = null!;
    public static ConfigEntry<string> InstalledCosmeticIds = null!;

    public static int ChangeCount { get; private set; }

    internal static void Initialize(ConfigFile config, bool registerWithLethalConfig)
    {
        config.SettingChanged += (_, _) => ChangeCount++;

        EnableMoreCompanyCompatibility = config.Bind(
            "1. Compatibility",
            "Enable MoreCompany Compatibility",
            true,
            "Show your MoreCompany cosmetics in first person when MoreCompany is installed."
        );

        EnableTooManyEmotesCompatibility = config.Bind(
            "1. Compatibility",
            "Enable TooManyEmotes Compatibility",
            true,
            "Use TooManyEmotes own first-person emote camera and keep your neck out of view. False may show your neck and flicker when an emote stops."
        );

        EnableLethalPhonesCompatibility = config.Bind(
            "1. Compatibility",
            "Enable LethalPhones Compatibility",
            true,
            "Show your hands holding a LethalPhones phone."
        );

        ShowMoreCompanyHat = config.Bind(
            "1. Compatibility",
            "ShowHat",
            false,
            "Show hat cosmetics. False by default to prevent hats from clipping into the camera. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowMoreCompanyChest = config.Bind(
            "1. Compatibility",
            "ShowChest",
            true,
            "Show chest cosmetics. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowMoreCompanyHip = config.Bind(
            "1. Compatibility",
            "ShowHip",
            true,
            "Show hip cosmetics. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowMoreCompanyRightLowerArm = config.Bind(
            "1. Compatibility",
            "ShowRightLowerArm",
            true,
            "Show right lower arm cosmetics. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowMoreCompanyLeftShin = config.Bind(
            "1. Compatibility",
            "ShowLeftShin",
            true,
            "Show left shin cosmetics. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowMoreCompanyRightShin = config.Bind(
            "1. Compatibility",
            "ShowRightShin",
            true,
            "Show right shin cosmetics. Requires 'Enable MoreCompany Compatibility' and 'ShowCosmetics'."
        );

        ShowCosmetics = config.Bind(
            "1. Compatibility",
            "ShowCosmetics",
            true,
            "Show your cosmetics in first-person. Make this false if another mod replaces your player model and your cosmetics sit wrong on model."
        );

        HideCosmetics = config.Bind(
            "1. Compatibility",
            "HideCosmetics",
            "",
            "Comma separated cosmetic ids to hide in first-person for cosmetics that are in the view. Copy ids you want from InstalledCosmeticIds."
        );

        InstalledCosmeticIds = config.Bind(
            "1. Compatibility",
            "InstalledCosmeticIds",
            "",
            "Every cosmetic id you have installed. This list generates automatically, editing it does nothing."
        );

        HeldItemFadeStyle = config.Bind(
            "2. Held Item",
            "FadeMode",
            HeldItemFadeMode.Dither,
            "Held item will start to dither when it is close to or filling the players camera. " +
            "Dither: a fade that lets you see through the item. " +
            "Off: nothing dithers."
        );

        ItemDither = config.Bind(
            "2. Held Item",
            "ItemDither",
            ItemDitherMode.ThirdPerson,
            "Which hands mode the held item dithers in."
        );

        FadeStartDistance = config.Bind(
            "2. Held Item",
            "FadeStartDistance",
            0.35f,
            new ConfigDescription("Distance from camera where held item starts to dither. Requires 'FadeMode' Dither.",
                new AcceptableValueRange<float>(0.2f, 1f)));

        FadeEndDistance = config.Bind(
            "2. Held Item",
            "FadeEndDistance",
            0.1f,
            new ConfigDescription("Distance from camera where held item is dithered the most, down to 'MinVisibility'.",
                new AcceptableValueRange<float>(0f, 0.15f)));

        MinVisibility = config.Bind(
            "2. Held Item",
            "MinVisibility",
            0.11f,
            new ConfigDescription("How much of held item stays visible at strongest dither. 0.11 = 11%.",
                new AcceptableValueRange<float>(0f, 1f)));

        FadeCoverage = config.Bind(
            "2. Held Item",
            "FadeCoverage",
            0.45f,
            new ConfigDescription("How much of screen held item can cover before it starts to dither. 0.45 = 45%. Requires 'FadeMode' Dither.",
                new AcceptableValueRange<float>(0.2f, 0.85f)));

        ItemInBodyHand = config.Bind(
            "2. Held Item",
            "ItemInBodyHand",
            true,
            "Your shadow and ship monitors show held item in your body hand correctly while vanilla arms hold it. Turn off if having issues. Doesn't work for Kiwi Egg, Belt bag, Jetpack, Heart and Tongue."
        );

        HandOffsetX = config.Bind(
            "2. Held Item",
            "HandOffsetX",
            0f,
            new ConfigDescription("Moves vanilla first-person hands right, negative is left.",
                new AcceptableValueRange<float>(-1f, 1f)));

        HandOffsetY = config.Bind(
            "2. Held Item",
            "HandOffsetY",
            0f,
            new ConfigDescription("Moves vanilla first-person hands up, negative is down.",
                new AcceptableValueRange<float>(-1f, 1f)));

        HandOffsetZ = config.Bind(
            "2. Held Item",
            "HandOffsetZ",
            0f,
            new ConfigDescription("Moves vanilla first-person hands forward, negative is back.",
                new AcceptableValueRange<float>(-1f, 1f)));

        Hands = config.Bind(
            "3. Hands",
            "HandsMode",
            HandsMode.Vanilla,
            "Which arms hold an item in first person. " +
            "Vanilla: Vanilla first-person arms that aim item with the camera [ recommended ]. " +
            "ThirdPerson: body own arms hold the item, more consistent body, but with known issues. " +
            "Hybrid: items held with both hands use vanilla arms, rest items your body arms. Change it per item in '8. Item Hands'."
        );

        VanillaArmsWhileSprinting = config.Bind(
            "3. Hands",
            "Vanilla Arms While Sprinting",
            true,
            "Show vanilla first-person arms while sprinting with empty hands."
        );

        VanillaArmsWhileCrouching = config.Bind(
            "3. Hands",
            "Vanilla Arms While Crouching",
            true,
            "Show vanilla first-person arms while crouching with empty hands."
        );

        VanillaArmsWhileJumping = config.Bind(
            "3. Hands",
            "Vanilla Arms While Jumping",
            true,
            "Show vanilla first-person arms while jumping or falling with empty hands."
        );

        VanillaArmsWhileWalking = config.Bind(
            "3. Hands",
            "Vanilla Arms While Walking",
            true,
            "Show vanilla first-person arms while walking with empty hands."
        );

        VanillaArmsWhileEmoting = config.Bind(
            "3. Hands",
            "Vanilla Arms While Emoting",
            true,
            "Show vanilla first-person arms while performing an emote [ dance, point ] with empty hands."
        );

        VanillaArmsNearWall = config.Bind(
            "3. Hands",
            "Vanilla Arms Near Wall",
            true,
            "Show vanilla first-person arms when a wall is close and the body extends its hands."
        );

        HeadFollow = config.Bind(
            "4. Camera",
            "HeadFollow",
            0f,
            new ConfigDescription("How much the camera follows your head bob.",
                new AcceptableValueRange<float>(0f, 1f)));

        SmoothSwingReturn = config.Bind(
            "4. Camera",
            "SmoothSwingReturn",
            true,
            "Smooth body and arms while doing shovel swing."
        );

        HideBodyInCruiser = config.Bind(
            "4. Camera",
            "HideBodyInCruiser",
            false,
            "Hide first-person body while seated in a vehicle use vanilla first person arms."
        );

        LookDownMargin = config.Bind(
            "6. Look Down",
            "LookDownMargin",
            0f,
            new ConfigDescription("Gap in degrees kept between your player model and the bottom of the screen when looking down. Negative shows more of your body.",
                new AcceptableValueRange<float>(-10f, 10f)));

        VanillaArmsItems = config.Bind(
            "8. Item Hands",
            "VanillaArmsItems",
            "",
            "Hybrid only. Item names that use vanilla arms, comma separated."
        );

        BodyArmsItems = config.Bind(
            "8. Item Hands",
            "BodyArmsItems",
            "",
            "Hybrid only. Item names that use your body arms, comma separated. Copy names from 'InstalledItemNames'."
        );

        InstalledItemNames = config.Bind(
            "8. Item Hands",
            "InstalledItemNames",
            "",
            "Every item in game, fills in after you join lobby."
        );

        if (registerWithLethalConfig && LethalConfigCompat.Present)
            LethalConfigCompat.Register(CollectEntries(config));
    }

    private static List<ConfigEntryBase> CollectEntries(ConfigFile config)
    {
        List<ConfigEntryBase> entries = new();

        foreach (KeyValuePair<ConfigDefinition, ConfigEntryBase> entry in config)
            entries.Add(entry.Value);

        return entries;
    }
}
