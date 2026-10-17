<div style="display: flex; gap: 10px; flex-wrap: wrap; margin-bottom: 16px;">
  <img src="https://img.shields.io/codefactor/grade/github/se3ya/LC-FirstPersonView?style=flat&logo=codefactor&logoColor=white&color=83E6FB&cacheSeconds=1200" alt="CodeFactor Grade">
  <img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fthunderstore.io%2Fapi%2Fv1%2Fpackage-metrics%2Fseechela%First_Person_View%2F&query=%24.downloads&label=downloads&style=flat&logo=thunderstore&logoColor=white&color=83E6FB&cacheSeconds=1200" alt="Thunderstore Downloads">
  <img src="https://img.shields.io/github/v/release/se3ya/LC-FirstPersonView?style=flat&logo=github&logoColor=white&color=83E6FB&cacheSeconds=1200" alt="GitHub Release Version">
  <a href="https://discord.gg/Z9rUwTtUpx"><img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fdiscordapp.com%2Fapi%2Finvites%2FZ9rUwTtUpx%3Fwith_counts%3Dtrue&query=approximate_member_count&style=flat&logo=discord&logoColor=white&color=83E6FB&cacheSeconds=1200&label=Discord" alt="Discord"></a>
  </div>
<br><br>

---
# First-Person View
### **Makes you see your own body in first person and the camera more immerisve.**
---

## Features

- Renders players body in first person
- Configurable arms mode: vanilla first-person arms, third-person arms or `Hybrid` where items held with both hands use vanilla arms
- Held items in third-person arms mode will start to dither when they get too close to the screen or cover most of it
- Compatible with **[MoreCompany](https://old.thunderstore.io/c/lethal-company/p/notnotnotswipez/MoreCompany/)**, **[TooManyEmotes](https://old.thunderstore.io/c/lethal-company/p/FlipMods/TooManyEmotes/)**, **[LethalPhones](https://old.thunderstore.io/c/lethal-company/p/Scoops/LethalPhones/)**, **[Extended Cosmetics Addon](https://old.thunderstore.io/c/lethal-company/p/MelanieMelicious/Extended_Cosmetics_Addon_MelanieMelicious/)**, **[CruiserImproved](https://old.thunderstore.io/c/lethal-company/p/DiggC/CruiserImproved/)** and **[Camera Overhaul](https://old.thunderstore.io/c/lethal-company/p/seechela/Camera_Overhaul/)**

## Configuration

### Compatibility

- **Enable _MoreCompany_ Compatibility** - show your _MoreCompany_ cosmetics in first person when _MoreCompany_ is installed
- **Enable _TooManyEmotes_ Compatibility** - Uses _TooManyEmotes_ own first-person emote camera and keeps your neck out of view when _TooManyEmotes_ is installed. Off may show your neck and flicker when an emote stops
- **Enable _LethalPhones_ Compatibility** - Makes arms visible while using phone when _LethalPhones_ is installed.
- **Show Hat** - false by default, may make some cosmetics clip into the camera
- **Show Chest, Show Hip, Show Right Lower Arm, Show Left Shin, Show Right Shin** - toggle per slot, need Show Cosmetics on
- **Show Cosmetics** - true by default, show your cosmetics in first person. Turn off if another mod replaces your player model and cosmetics sit wrong on it. Cosmetic that replaces the body itself stays visible
- **Hide Cosmetics** - comma separated cosmetic ids to hide in first person. For cosmetics that go into the camera too much
- **Installed Cosmetic Ids** - every cosmetic id you have installed, generates automatically

### Held Item

- **Fade mode**:
  - *Dither (default)*: Held item will start to dither when it is close to or filling the players camera
  - *Off*: Disables dithering
- **Item Dither**:
  - *Vanilla*: Held item will dither while vanilla arms hold it
  - *Third Person (default)*: Held item will dither while your body arms hold it
- **Fade Start Distance** - distance from the camera where held item starts to dither
- **Fade End Distance** - distance where dither is strongest
- **Min Visibility** - how much of item stays visible at the strongest dither
- **Fade Coverage** - how much of screen held item can cover before it starts to dither
- **Item In Body Hand** - players shadow and ship monitors show held item in your body hand while vanilla arms hold it. Turn off if having issues. Doesn't work for Egg, Belt bag, Jetpack, Heart and Tongue
- **Hand Offset X / Y / Z** - moves vanilla first-person hands right, up or forward

### Hands

- **Hands Mode**:
  - *Vanilla (default)*: first-person arms
  - *Third Person*: body own arms
  - *Hybrid*: Items held with both hands like the V-type engine use vanilla arms, rest items players body arms
- **Vanilla Arms While Sprinting / Crouching / Jumping / Walking / Emoting / Near Wall** - each true by default, shows the vanilla first-person arms with their matching animation when hands are empty. Disable any of them to keep the third-person body arms instead.

### Camera

- **Head Follow** - 0 by default, which is the vanilla camera. Higher makes the camera follow your head bob, 1 fully. To turn off vanilla head bob use the game's own Head Bobbing setting
- **Smooth Swing Return** - smooths out body and arms while doing shovel swing instead of snapping, modded melee weapons like the Sledgehammer and Axe too
- **Hide Body In Cruiser** - hides first-person body while seated in vehicle. The electric chair keeps your body

### Look Down

- **Look Down Margin** - gap in degrees kept between your suit and bottom of screen when looking down. 0 by default, more moves the camera further forward, negative shows more of your body

### Item Hands

- **Vanilla Arms Items** - Hybrid only, item names that use vanilla arms, comma separated
- **Body Arms Items** - Hybrid only, item names that use your body arms
- **Installed Item Names** - every item in game, fills after you join lobby. Copy names from here

## Credits

- Developed by **[seeya](https://thunderstore.io/c/lethal-company/p/seechela/)**

## License

Distributed under the GPL v3 License.

---

### 💖 Support

If you enjoy my work, consider [supporting](https://www.buymeacoffee.com/see_ya) me. Donations are optional but greatly appreciated.

---
