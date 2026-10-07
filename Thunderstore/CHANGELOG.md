# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-10-07

### Added

- `Hybrid` hands mode. 2-handed itens like the V-type engine use vanilla arms, the rest uses your body arms
  - `VanillaArmsItems` and `BodyArmsItems` pick arms per item, names are comma separated. Only work in `Hybrid` mode
  - `InstalledItemNames` lists every item in your game after you join lobby, copy names from there
  - Items that aim where you look are added to `VanillaArmsItems` once, like Flashlight, Pro-flashlight, Laser pointer, Zap gun, Spray paint and Weed killer
- `ItemInBodyHand` option, players shadow and ship monitors show correctly held item in body hand
- `HandOffsetX` and `HandOffsetY` to move vanilla hands
- `HeadFollow`, how much the camera follows players head bob
- `LookDownMargin`, a gap in degrees between players suit and the bottom of screen when looking down
- `FadeStartDistance`, `FadeEndDistance` and `MinVisibility` for how close held item starts to dither and how much of it stays visible
- Cap over players suits neck opening
- `FirstPersonViewApi.ExternalSuppression` for mods that take over the camera

### Changed

- Camera is vanilla camera, now it keep games own camera position
- Standing still the look down camera push eases in from 20° so it's smooth
- Crouching, standing up or an emote ending keep the camera where it is, it moves back only when you look up or turn
- `SmoothSwingReturn` also holds your upper body still through standing swings, so the camera isn't pushed forward
- `HideBodyInCruiser` only hides your body in vehicle seats now, electric chair keeps it
- **CruiserImproved** compatibility always works now because of fixed neck visibility
- **TooManyEmotes** first-person emotes use **TooManyEmotes** own camera
- **Camera Overhaul** shakes on top of the camera and don't show your neck
- Seat look down limits are vanilla again
- Inspecting an item always uses vanilla arms in every hands mode
- `ItemDither` follows arms holding the item instead of `HandsMode`

### Removed

- `DisableHeadBob`, camera keeps vanilla head bob now. Turn it off with games own Head Bobbing setting ([#8](https://github.com/se3ya/LC-FirstPersonView/issues/8))
- `StickCameraToHeadInAnimations`, interact animations like the ship lever keep the vanilla camera
- `Enable CruiserImproved Compatibility`, it always works now

### Fixed

- Wake up arm animation not playing when you join lobby ([#9](https://github.com/se3ya/LC-FirstPersonView/issues/9))
- Camera moving up to 37 cm up and sideways on the couch and jumping 4 to 13 cm when you leave a seat or the terminal
- Camera slowly sinking while walking
- Neck showing in **TooManyEmotes** first-person emotes when you look down, standing or walking
- Camera and body flickering for a frame when a **TooManyEmotes** emote stops, like when you crouch out of it
- Neck showing when you look down while entering the terminal, pulling the ship lever, dancing or right after the join animation
- **MoreCompany** cosmetics staying in first person after turning its compatibility off in game
- Held item dither resetting colors other mods set on the item
- Gunkfish poop on helmet clipping in and out of the screen
- Body arms flashing for a frame after dropping an item held with vanilla arms
- Camera flicking forward after a hit with modded melee weapons like the Sledgehammer, Axe or Light crowbar
- Zap gun switching to body arms while zapping
- Item you pick up with body arms floating in front of the camera for a moment
- Camera going inside several objects
- Neck showing when walking into a wall while looking down, players body is pulled back from the wall instead
- Interacting with things and picking up items aimed from the pushed camera instead of your eyes when looking down

## [1.5.2] - 2026-09-20

### Added

- `SmoothSwingReturn` option to smooth out the body while doing shovel swing

### Fixed

- Body snapping when being crouched and using shovel.

## [1.5.1] - 2026-09-11

### Fixed

- Ghost Girl not vanishing with **StarlancerAIFix**

## [1.5.0] - 2026-08-21

### Added

- Optional global config shared across all profiles
- `ShowCosmetics` option to hide all your cosmetics in first person. Made when another mod replaces your player model and cosmetics don't fit it
- `HideCosmetics` option to hide chosen cosmetic ids in first person for cosmetics that are in the view
- `InstalledCosmeticIds` automatically generated list of every cosmetic id installed
- `HideBodyInCruiser` option to hide first person body while seated in a vehicle and use the vanilla first person arms instead
- Compatibility with **Extended Cosmetics Addon**

### Fixed

- Neck sometimes being seen when looking down
- Camera sinking
- Body not visible on cameras because of another mods
- Multiple other stuff

### Changed

- `DisableIdleHeadBob` merged into `DisableHeadBob`
- Look down forward eye push now starts at steeper look down angle
- Removed extra forward eye push while on ladders
- Optimization stuff

## [1.4.1] - 2026-07-27

### Added

- `StickCameraToHeadInAnimations` option to make camera sit on your head during interact animations like the ship lever, item charging and chairs, the way vanilla does
- `DisableIdleHeadBob` option to remove up and down movements from the idle breathing animation while standing still

## [1.4.0] - 2026-07-25

### Added

- MoreCompany hat cosmetics are now visible in cameras and TooManyEmotes camera

### Fixed

- Seeing your own neck after climbing out of Cruiser or the Hauler

### Changed

- Looking down camera push was tweaked to be less forwarded but enough to not see the neck
- Looking down push while holding an two-handed items now only applies to shovel type swing

## [1.3.3] - 2026-07-24

### Fixed

- Cruiser camera with CameraOverhaul fix

### Changed

- Camera is no longer pushed forward all the time while seated in the Cruiser, it eases forward only as you look down.

## [1.3.2] - 2026-07-23

### Fixed

- Eye level being too low and head appearing inside the body

## [1.3.1] - 2026-07-17

### Fixed

- Body not rendering when another mod changes player LoD levels

## [1.3.0] - 2026-07-12

### Added

- CruiserImproved compatibility

### Changed

- Camera no longer always pushed forward, it now eases forward only as you look down to prevent seeing neck

### Fixed

- Camera being sometimes behind the neck
- Held items floating away from hand right after being picked up

## [1.2.0] - 2026-07-03

### Added

- LethalPhones compatibility

### Fixed

- Seated look clamp not clearing properly when leaving special interactions.

## [1.1.0] - 2026-07-02

### Changed

- Split _Vanilla Arms While Moving_ into separate toggles
- Improved camera forward pushing
- Improved wall collide

### Fixed

- Motion blur on vanilla arms
- Players body not rendering in cameras
- Neck and shoulder holes visible when arms are hidden

## [1.0.2] - 2026-06-26

### Changed

- Improved performance during vehicle ignition animations ([#3](https://github.com/se3ya/LC-FirstPersonView/pull/3)).

### Fixed

- Cruiser key position during ignition and removal

### Added

- Vanilla Arms While Moving option; default to true

## [1.0.1] - 2026-06-25

### Fixed

- On crouch eyes being little too high

### Added

- Disable head bobbing option (misterbubb)

## [1.0.0] - 2026-06-24

- Initial release!
