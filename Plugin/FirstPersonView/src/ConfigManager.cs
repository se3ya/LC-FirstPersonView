using BepInEx.Configuration;

namespace FirstPersonView;

public static class ConfigManager
{
    public static ConfigEntry<HeldItemFadeMode> HeldItemFadeStyle = null!;

    public static ConfigEntry<HandsMode> Hands = null!;
    public static ConfigEntry<bool> VanillaArmsWhileSprinting = null!;
    public static ConfigEntry<bool> VanillaArmsWhileCrouching = null!;
    public static ConfigEntry<bool> VanillaArmsWhileJumping = null!;
    public static ConfigEntry<bool> VanillaArmsWhileWalking = null!;
    public static ConfigEntry<bool> VanillaArmsWhileEmoting = null!;
    public static ConfigEntry<bool> VanillaArmsNearWall = null!;
    public static ConfigEntry<ItemDitherMode> ItemDither = null!;

    public static ConfigEntry<float> HandOffsetZ = null!;
    public static ConfigEntry<bool> DisableHeadBob = null!;

    public static ConfigEntry<bool> EnableMoreCompanyCompatibility = null!;
    public static ConfigEntry<bool> EnableTooManyEmotesCompatibility = null!;
    public static ConfigEntry<bool> ShowMoreCompanyHat = null!;
    public static ConfigEntry<bool> ShowMoreCompanyChest = null!;
    public static ConfigEntry<bool> ShowMoreCompanyHip = null!;
    public static ConfigEntry<bool> ShowMoreCompanyRightLowerArm = null!;
    public static ConfigEntry<bool> ShowMoreCompanyLeftShin = null!;
    public static ConfigEntry<bool> ShowMoreCompanyRightShin = null!;

    internal static void Initialize(ConfigFile config)
    {
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
            "Keep the camera on your head during a TooManyEmotes first-person emote. Takes effect on restart."
        );

        ShowMoreCompanyHat = config.Bind(
            "1. Compatibility",
            "ShowHat",
            false,
            "Show hat cosmetics. False by default to prevent hats from clipping into the camera."
        );

        ShowMoreCompanyChest = config.Bind(
            "1. Compatibility",
            "ShowChest",
            true,
            "Show chest cosmetics."
        );

        ShowMoreCompanyHip = config.Bind(
            "1. Compatibility",
            "ShowHip",
            true,
            "Show hip cosmetics."
        );

        ShowMoreCompanyRightLowerArm = config.Bind(
            "1. Compatibility",
            "ShowRightLowerArm",
            true,
            "Show right lower arm cosmetics."
        );

        ShowMoreCompanyLeftShin = config.Bind(
            "1. Compatibility",
            "ShowLeftShin",
            true,
            "Show left shin cosmetics."
        );

        ShowMoreCompanyRightShin = config.Bind(
            "1. Compatibility",
            "ShowRightShin",
            true,
            "Show right shin cosmetics."
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

        HandOffsetZ = config.Bind(
            "2. Held Item",
            "HandOffsetZ",
            0f,
            new ConfigDescription("First-person hand offset.",
                new AcceptableValueRange<float>(-1f, 1f)));

        Hands = config.Bind(
            "3. Hands",
            "HandsMode",
            HandsMode.Vanilla,
            "Which arms hold an item in first person. " +
            "Vanilla: real first-person arms that aim the item with the camera [ recommended ]. " +
            "ThirdPerson: the body own arms hold the item, more consistent body, but with known issues."
        );

        VanillaArmsWhileSprinting = config.Bind(
            "3. Hands",
            "Vanilla Arms While Sprinting",
            true,
            "Show vanilla first-person arms while sprinting."
        );

        VanillaArmsWhileCrouching = config.Bind(
            "3. Hands",
            "Vanilla Arms While Crouching",
            true,
            "Show vanilla first-person arms while crouching."
        );

        VanillaArmsWhileJumping = config.Bind(
            "3. Hands",
            "Vanilla Arms While Jumping",
            true,
            "Show vanilla first-person arms while jumping or falling."
        );

        VanillaArmsWhileWalking = config.Bind(
            "3. Hands",
            "Vanilla Arms While Walking",
            true,
            "Show vanilla first-person arms while walking."
        );

        VanillaArmsWhileEmoting = config.Bind(
            "3. Hands",
            "Vanilla Arms While Emoting",
            true,
            "Show vanilla first-person arms while performing an emote (dance, point)."
        );

        VanillaArmsNearWall = config.Bind(
            "3. Hands",
            "Vanilla Arms Near Wall",
            true,
            "Show vanilla first-person arms when a wall is close and the body extends its hands."
        );

        DisableHeadBob = config.Bind(
            "4. Camera",
            "DisableHeadBob",
            true,
            "Disable head bobbing from walking and running animations."
        );
    }
}