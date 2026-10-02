# SWAT: Tactical Response - Final Report

This report covers what was built, what was simplified, how it was checked,
and what is still unverified. It has two parts: the **quality-of-life,
graphics, lighting and polish update** (first), and the original build
(below it, updated where the polish update changed something).

The short version: everything is implemented in C# (plus four small shaders),
compiles in three configurations and the shaders pass a syntax check, but
**the game has not been run inside the Unity editor yet**. No play-testing, no
profiling or performance measurements, and no screenshots exist. Treat
everything below as "implemented in code" unless it says otherwise.

# Part 1: Polish update

The update followed the requested pass order (stability, lighting,
environment, characters and weapons, UI and QoL, audio, performance, final
polish). It extends the existing systems rather than replacing them: weapons,
AI, missions, officers, doors, squad orders, scoring and saves keep their
behavior, with fixes where something was broken.

## Pass 1: stability audit

Read through the flows that matter most (deployment, squad stack and entry,
objectives, pause, restart, save) and fixed what was found:

- Pause now stops world audio as well as time (`AudioManager.SetPaused`, using
  `AudioListener.pause` with menu and music sources exempt), so sounds don't
  keep playing behind the pause menu and resume where they left off.
- Officers stacking on a door no longer walk it open early; an officer on the
  far side of a closed stack door now opens it on the way through (it used to
  walk through the closed door), without counting that as the entry order.
- Sealed (disabled) doors no longer show an interaction prompt.
- The alarm system clears its static instance when a map is unloaded.
- Squad selection evicts the oldest pick instead of refusing when the squad is full.
- AI ticking ignores zero-length frames (paused time).
- Save format version 2: an older save is copied to `<save>.v1.bak` before it
  is upgraded, and every loaded setting is clamped to a valid range.

## Pass 2: lighting

| Item | Implementation |
| --- | --- |
| Lighting profiles | `LightingProfile`: Day, Evening, Night (sun color/angle/intensity, ambient trilight, fog, background, interior strength, light-pool strength, street lamps, flashlight need). Each mission has a default (`MissionData.timeOfDay`); the briefing's **Lighting** selector overrides it. |
| Interior lighting zones | `MapDresser` puts ceiling panels on a grid in every room, colored and dimmed per room type (`RoomStyle`): bright cool offices, warm break rooms, dim storage, greenish restrooms, etc. Every fixture gets a soft light pool on the floor (one combined mesh per map). Real point lights are added only where the quality preset allows. Some tubes flicker (`LightFlicker`). |
| Power cuts and dark rooms | Unpowered rooms get a darkness overlay, emergency lights by a door with an amber pool, and hallway emergency strips. Exit signs with green pools mark exits. |
| Exterior | Street lamps with pools, distance fog, profile-based sky color, extraction bollards. |
| Shadows | Off / Low / Medium / High / Very High (`QualityManager.ApplyShadows`: hard or soft, resolution, distance, cascades). The team leader's flashlight casts a soft shadow on Very High. |
| Ambient occlusion | Optional "contact shadows": soft dark strips along wall bases and blobs under props and characters, baked into combined decal meshes when the map is built. No screen-space AO (see simplifications). |
| Post-processing | `PostEffects` + `SwatPostFX.shader` for the Built-in pipeline: quarter-resolution bloom, exposure/contrast/saturation/lift/gain grading per lighting profile, vignette. Restrained values; a toggle in settings; bloom is reduced with "Reduce flashes". Under URP only an IMGUI vignette is drawn. |
| Flashlight | A soft cone decal on the floor on every preset (stronger in dark rooms), real spot light where dynamic lights are on, brightness setting, optional automatic flashlight in dark rooms and outdoors at night. |

## Pass 3: environment

- **Materials:** `ProceduralTextures` generates small grayscale tiling textures
  (concrete, dirty concrete, painted wall, brick, carpet, tile, asphalt, wood,
  metal, grass, plastic, fabric, glass, rubber) tinted by material color;
  `Shapes.TiledCube` gives boxes world-space UVs so shared materials keep the
  same texture density at any size. Texture size follows the preset (32, 64 or 128 px).
- **Room identity:** `RoomStyle` classifies every room (lobby, office,
  conference, hallway, storage, security, restroom, break room, utility,
  warehouse, residential, stairwell, range, training, garage, exterior, roof)
  and sets its floor, footstep surface, light color and strength, flicker,
  reverb and hum.
- **Props** (`EnvironmentProps`, no colliders or shadows): trash cans, filing
  cabinets, water coolers, fire extinguishers, fire alarm pull stations,
  electrical panels, posters, whiteboards, cleaning carts, toolboxes, box
  stacks, monitor walls, vending machines, coffee stations, benches, bollards,
  exit signs and clocks, placed per room type away from doors, spawns and
  walkways. Desks gain keyboards, mugs, papers, phones and ID badges.
- **Doors:** handles on both faces, frames and threshold, wood or metal
  surface, open/close/handle/locked sounds with pitch variation, and a status
  line ("Locked: breachable", "Electronic lock", "Wedged shut", "Sealed shut").
- **Atmosphere:** glowing monitors and screens, building hum in lit rooms,
  drifting dust indoors on the higher presets (max 50 particles), fog outdoors.

## Pass 4: characters and weapons

- **Officers:** helmet or cap, vest with pouches, radio and antenna, back
  plate, belt, gloves, boots, knee pads, goggles; an armband, shoulder patch
  and floating marker in the officer's ID color (role color), which matches
  the number badge on the HUD and the map.
- **Suspects and civilians:** outfits (jackets, suits, hoodies, shirts,
  hi-vis, uniforms), varied heights and builds, distinct suspect accents.
- **Animation:** walk/run blend with leg stride, idle breathing, crouch,
  aim/relaxed poses, reload dip, weapon-switch dip, small weapon sway, instant
  recoil with eased recovery, and a short non-graphic flinch when hit (player,
  squad and suspects).
- **Weapons:** rail, ejection port and sling details on long guns; shell
  casings (pooled); impacts by surface (sparks on metal, dust on concrete,
  splinters on wood, glass, dirt) with a small pool of bullet marks (not on
  doors, which move); a brighter muzzle flash visible on every preset.

## Pass 5: UI and quality of life

- **HUD:** minimap (top right; player, squad, discovered rooms, doors,
  objectives, extraction, safe zones, known threats only; toggle, size,
  opacity); status and squad panels flow below it; squad panel with ID number
  badges and current-order icons; unified interaction prompt with a key cap,
  HOLD tag, door status and progress bar; status text for sealed doors;
  subtitles with a backing strip; damage shown as a red tint from the screen
  edges plus a direction chevron; a pale "lost health" segment on the health
  bar; order confirmation ping in the world.
- **Objective feedback:** short banners near the top for OBJECTIVE UPDATED,
  OBJECTIVE COMPLETE, OPTIONAL OBJECTIVE(S) and CIVILIAN IN DANGER, with
  tones and a TOC radio line; the debriefing opens with a brief MISSION
  COMPLETE / MISSION FAILED stamp (click to skip) before the report.
- **Weapons QoL:** hold Q for a weapon wheel (weapons and equipment, with
  ammo/count), tap Q to swap, 1-4 still work; automatic reload (setting);
  optional switch to sidearm when empty; low-ammo cue; reload is cancelled by switching.
- **Interaction prompts:** consistent wording ("[E] Open Door", "[E] Secure
  Suspect", "[E] Rescue Civilian", "[E] Disable Alarm", "[E] Resupply at
  Van"...); prompts only appear for things that can be used right now.
  "Pick Up Equipment" and "Enter Vehicle" were not added because the game has
  no pick-ups or drivable vehicles (no fake prompts); the van offers a
  once-per-mission ammunition resupply instead.
- **Squad commands:** order icons on the command wheel and squad panel,
  order sound, spoken-style radio line from the leader and a delayed
  acknowledgment from the officer, confirmation ping, "Civilian located" callouts.
- **Camera:** smooth zoom with zoom-speed setting, camera smoothing slider,
  look-ahead, edge scrolling (optional), automatic wider zoom outdoors,
  screen shake Off/Low/Medium (default Low).
- **Aiming:** mouse sensitivity (values other than 1.00 switch to a locked
  game cursor driven by scaled mouse movement), aim smoothing, crosshair
  size/opacity/color, hit confirmation marker and sound (toggle), controller
  sensitivity and aim assist.
- **Settings window:** five tabs (Gameplay, Graphics, Audio, Accessibility,
  Controls). Graphics: quality preset, resolution, display mode, VSync,
  Performance Mode, FPS counter, and per-option overrides for shadows,
  anti-aliasing, effects, textures, post-processing, ambient occlusion, view
  distance and flashlight brightness. Changes apply immediately (texture
  quality on the next mission load, difficulty on the next deployment,
  resolution and display mode in a built game) and are saved.
- **Accessibility:** UI scale, text size, colorblind-friendly status colors,
  subtitles on/off and size, screen shake, reduce flashes (softer flashbang
  whiteout, steady alarm banner, weaker bloom, weaker damage tint), crosshair
  options with a live preview.
- **Controller:** with the Input System package, a fixed gamepad layout
  (twin-stick aiming, triggers to fire/steady aim, radial menus picked with
  the right stick, sprint toggle on L3), automatic switching of prompts and
  hints to gamepad buttons, and a stick-driven cursor with A to click in
  menus. Without the package the code compiles to keyboard and mouse only.
- **Menus and transitions:** a short fade between screens (not for pause),
  notifications slide in, loading screen with mission details, a real tip and
  a spinner (no progress bar, since the map builds in one step), pause menu
  with mission time and objective count, Resume / Restart / Settings /
  Controls / Quit mission / Quit to desktop.

## Pass 6: audio

- Mix categories: Master, Music, Weapons, Effects/environment, Voice/radio,
  Ambience, Interface, each with volume and mute. Weapon fire, reloads, dry
  fire, casings and ricochets are in Weapons; radio and shouts in Voice.
- Indoor reverb chosen by room style (`AudioReverbZone` on the listener;
  off on Potato), building hum in lit rooms, room tone vs wind.
- Footsteps by floor surface (concrete, carpet, tile, metal, grass, asphalt),
  louder when sprinting, quieter crouched.
- Doors: open (wood/metal), close, handle and locked sounds. Radio voice
  blips per speaker, order and objective tones, warning tone, flashlight
  click, weapon raise, menu and mission music beds. All sounds are still
  procedural placeholders generated at startup.

## Pass 7: performance (review only)

No profiling was possible (no Unity editor). The work was kept cheap by design:

- Light pools, contact shadows, darkness overlays and exit-sign glows are a
  handful of combined meshes per map, not per-object renderers or lights.
- Real fixture lights only on Medium and above; Performance Mode and the
  lower presets use pools only. Shadows, MSAA, pixel lights, render scale,
  particle counts, texture size and AI update rate all follow the preset.
- Props have no colliders and cast no shadows; procedural textures are tiny
  and cached; materials are shared by color and surface.
- Shell casings, bullet marks, effects and audio sources are pooled.
- The minimap only draws on the repaint event and reuses its buffers; prompt
  scans are throttled (10 Hz); the flashlight cone updates 4 times a second.
- Post-processing runs bloom at quarter resolution and is off on Potato/Low.

The most likely costs on slow machines, to check first when profiling: IMGUI
draw calls (minimap and planning map), real point lights on Medium+, and the
post-processing pass.

## Polish acceptance criteria: status

| Criterion | Status |
| --- | --- |
| Existing gameplay still works | Systems kept; compiles. **Not verified in Play Mode.** |
| Looks noticeably better / lighting improved / distinct interior and exterior | Implemented (profiles, zones, pools, materials, props). **Not seen in Unity.** |
| Materials consistent | Shared material library with tiled UVs. Unverified visually. |
| Characters easier to identify | ID colors, numbers and markers implemented. Unverified visually. |
| Better weapon feedback | Implemented (casings, impacts, marks, flash, sounds, recoil). Unverified. |
| Menus polished, prompts clear, squad commands easier | Implemented. Unverified. |
| Settings work | Implemented and saved; **not exercised in Unity**. |
| Controller support | Implemented for the Input System package (compiled against a stub of its API). **Not tested with a real gamepad.** |
| Accessibility options work | Implemented. Unverified. |
| Audio properly mixed | Categories and routing implemented; levels not tuned by ear. |
| Remains performant | Designed for it; **not measured**. |
| No critical Unity Console errors | **Unknown** - the project was never opened in Unity. |
| Existing missions remain playable | Mission code unchanged except fixes; **not played**. |
| Actual gameplay screenshots captured | **No.** Not possible without Unity. [Screenshots/README.md](Screenshots/README.md) lists the 14 shots and how to take them. |

## Polish features simplified or deferred

- **No baked lightmaps or light probes.** Maps are generated at runtime, so
  interior lighting is real-time lights (where allowed) plus pre-built light
  pool and shading decals.
- **Ambient occlusion is decal-based contact shading**, not SSAO or baked AO.
- **Post-processing is a small custom effect for the Built-in pipeline**
  (no Post Processing Stack or URP Volume package). Under URP only an IMGUI
  vignette is drawn.
- **No rain behind windows** (optional in the request; the maps have no
  window views that would show it).
- **Gamepad layout is fixed** (keyboard bindings remain remappable). Planning
  mode, fire mode, zoom presets and individual officer selection are keyboard-only.
- **Menus with a gamepad** use a stick-driven cursor rather than focus
  navigation between buttons.
- **No "Pick Up Equipment" / "Enter Vehicle" interactions**, because those
  systems don't exist in the game; adding prompts for them would be fake UI.
- **Squadmates don't play footstep sounds** (only the player does) to keep audio voices free.
- **Texture quality** changes apply to maps built after the change (the next
  mission); the HQ keeps the textures it was built with until restart.

# Part 2: Original build

## Unity version, packages and configuration

| Item | Value |
| --- | --- |
| Unity version | Project file targets **Unity 6 (6000.0.23f1)**. Not opened in that editor during this work. |
| Packages | Built-in modules only (`Packages/manifest.json`): ai, animation, audio, imgui, jsonserialize, particlesystem, physics, screencapture, ui, uielements, umbra, unitywebrequest. No registry packages. |
| Render pipeline | No pipeline asset is required. Materials are copies of the active pipeline's default material, so the same code is meant to work in Built-in and URP. Only the code path exists; neither pipeline was tested. |
| Input | `GameInput` reads the classic Input Manager by default. If the Input System package is installed, the `Swat` assembly definition defines `SWAT_INPUT_SYSTEM` and the same bindings are read through the Input System instead, and gamepads are supported. |
| Scenes | One runtime scene. The editor script creates `Assets/Scenes/Boot.unity` (an empty scene with the `GameManager`) the first time the project opens, or via **SWAT -> Create Boot Scene**. The game also starts in any scene through `GameBootstrap`. |
| Assembly | `Assets/Scripts/Swat.asmdef` (editor code is guarded with `#if UNITY_EDITOR`). |
| Save file | `Application.persistentDataPath/swat_tactical_response.json` (versioned JSON, now version 2; corrupt files are backed up to `.bak` and replaced; older versions are copied to `.v<n>.bak` before upgrading). |
| Shaders | `Assets/Resources/SWAT/Shaders/`: `SWAT/Unlit`, `SWAT/Decal`, `SWAT/Glow` and `Hidden/SWAT/PostFX`, plain CG vertex/fragment shaders without pipeline-specific includes, loaded from Resources so they are included in builds. |

## Architecture

Everything is created at runtime by code: the HQ diorama, the four maps,
characters, weapons, the van, materials, UI and sounds. `GameManager` swaps
between the HQ (behind the menus) and a freshly built mission map, bakes the
NavMesh for the map once at deployment (doors never trigger a rebake;
locked/wedged doors carve it with `NavMeshObstacle`), and drives the state
flow:

`MainMenu -> Headquarters -> Briefing -> OfficerSelection -> Loadout -> Loading -> Deploying -> Playing <-> Paused -> Debrief`

The spec's script names map onto the code as follows (most exist with the same name):

| Spec name | Implementation |
| --- | --- |
| GameManager, AudioManager, SaveManager, ObjectPool | Same names (`Core/`, `Audio/`, `Save/`, `Utilities/`) |
| SceneLoader | Not a separate class: `GameManager` builds/destroys maps inside one scene (`BuildMission`, `ClearMission`) |
| PlayerController, PlayerHealth, PlayerInteraction | Same names |
| PlayerInputHandler | `Core/GameInput.cs` (remappable bindings, legacy or Input System backend) |
| CameraController | Same name |
| OfficerData, OfficerController, OfficerHealth, OfficerLoadout, OfficerSelectionManager | Same names |
| SquadAI, SquadCommandManager, SquadStatusTracker | Same names |
| SquadFormationController | `Squad/SquadFormation.cs` |
| WeaponData, Weapon, WeaponController, WeaponInventory, WeaponEffects | Same names (`WeaponInventory` is in `Weapon.cs`) |
| EnemyAI, EnemyController, EnemyHealth, EnemyWeapon, AIVisibility, CoverPoint | Same names |
| CivilianAI | Same name |
| CivilianInteraction | `CivilianAI` implements `IInteractable` (follow, wait, treat, free) |
| CivilianRescueTracker | Class in `Missions/MissionStats.cs` |
| TacticalEquipment, Flashbang, SmokeGrenade, BreachingCharge, DoorWedge, MedicalKit, FlashlightController | Same names (`Equipment/`), plus `ReconCamera`, `PortableLight` |
| DoorController, SecurityCamera, SecurityConsole, AlarmSystem, ExtractionZone, RoomController | Same names (`Environment/`) |
| MissionData, MissionManager, Objective, ObjectiveTracker, MissionScoring, MissionBriefing | Same names, plus `MissionRandomizer`, `TacticalIntel` |
| UIManager, MainMenuController, HUDController, MissionSelectionUI, OfficerSelectionUI, LoadoutUI, CommandWheelUI, TacticalMapUI, PauseMenuController, MissionDebriefUI | Same names, plus `BriefingUI`, `SettingsUI`, `UITheme`, `UIIcons`, `CharacterPreview` |
| IDamageable, IInteractable | `Core/Damage.cs` (plus `ICombatTarget` for anything suspects can shoot at) |

## Features implemented

These exist in code and compile. None has been exercised in Play Mode.

- **Identity and flow:** title "SWAT: TACTICAL RESPONSE", dark navy/black/muted-blue IMGUI theme with red/blue emergency accents; full flow from main menu to debriefing and back to HQ; restart, retry with same or new seed, return to HQ/menu, quit.
- **Player:** walk, sprint with stamina, crouch, steady aim, mouse-facing movement, footsteps, noise, flashlight, role ability, health, armor condition, movement state, interaction with hold progress.
- **Camera:** 55 degree angled follow camera, wheel zoom with min/max, three zoom presets (V), adjustable zoom sensitivity and look-ahead, map bounds, screen shake; showcase camera for menus.
- **Officers:** six fictional officers with name, callsign, role, portrait (drawn), 3D preview in HQ, stats (health, armor, speed, accuracy, reaction, perception, command responsiveness, capacity), personality, role ability, preferred kit, unlock rules, uniform colors.
- **Squad:** up to three AI officers; command wheel (hold Z, mouse or number keys) with 10 general orders and door orders (Stack up, Open/Breach/Flash & clear); individual (F1-F3) or group (F4) selection; follow formation, hold, regroup, move with waypoint queue, cover, stack and coordinated entry, stay behind (hold fire), return, assist civilians (treat, free, escort including down stairs), wait; stuck recovery; shout-then-shoot rules of engagement; reload and sidearm fallback; medic revives; restrain surrendered suspects; radio subtitles; labels and waypoint lines in the world; status on HUD.
- **Weapons:** seven primaries (compact SMG, SMG, compact rifle, service rifle with auto/semi, shotgun with pellet spread, precision carbine, less-lethal launcher) and three sidearms; hitscan, magazines and reserves, reload, switching, semi/auto, spread and recoil bloom, muzzle flash, impacts, tracer, dry fire, role restrictions; block models with a `modelPrefab` override slot.
- **Attachments:** weapon light, red dot, suppressor, two stocks, with small stat effects, configured as data.
- **Armor:** light, standard and heavy (reduction, speed, capacity, durability, helmet, color); ballistic shield with frontal protection, bracing and cover for teammates behind.
- **Equipment:** flashbang (radius, duration, line of sight, flash and sound), smoke (cheap sphere puffs that block AI sight and expire), breaching charge (designated doors only), door wedge (place/remove), medical kit (heal over time, short animation), portable light, recon camera (text panel, partial view); capacity-limited per officer with role discounts.
- **Suspects:** unarmed, armed hostile, guard, nervous, armored, leader (+ training dummy); state machine Idle, Patrol, Suspicious, Investigating, Alert, Chasing, Attacking, TakingCover, Searching, Fleeing, Hiding, Stunned, Surrendering, Restrained, Dead; hearing, sight with FOV/darkness/smoke/crouch, last known position, search, callouts, alarms, cover, surrender, leader escape, guards raising the alarm, questioning unarmed suspects reveals others.
- **Civilians:** office worker, security guard, visitor, injured, hiding, hostage, resident; idle, wander, panic, flee, hide, follow, wait, injured, captive, evacuated; tracked encountered/rescued/evacuated/injured/killed.
- **Doors and rooms:** Closed, Open, Locked, Wedged, Breached, Disabled; open/close, pick lock, unlock from console, kick, breach, wedge; status shown in the prompt; rooms Undiscovered -> Discovered -> Investigated -> Secured -> Complete, updated a few times per second (no physics triggers).
- **Maps:** Office Complex, Warehouse, Apartment Building (ground floor, second floor and roof linked by stairs), Training Ground, HQ with parked van; van arrival sequence on deployment.
- **Missions:** data-driven `MissionData` (training + four operations), mandatory and optional objectives, extraction, failure (team leader down, abort), consistent scoring with rating, briefing text built from the rolled variation.
- **Randomization:** seeded per deployment (shown, rerollable, keepable): spawn points from tagged pools, patrol routes, randomly locked doors (always breachable and pickable), optional objectives (only valid ones), alarm/camera state, power outage.
- **Tactical map and planning:** walls, doors by state, rooms by state, team, visible and last-known threats, discovered civilians, evidence, consoles, active cameras, zones, objective rooms, player markers, floor tabs; planning mode pauses or slows time, selects officers, places waypoints and markers, quick orders.
- **Security:** sweeping cameras that detect officers and trigger the alarm, alarm with beacons that alerts suspects, consoles to disable cameras/alarm, unlock electronic doors and review footage.
- **HUD:** health, armor, stamina, weapon, ammo, fire mode, reload bar, equipment strip, crosshair with spread, interaction prompt, objectives with progress, mission timer and tallies, squad health/orders/ammo, radio, notifications, alarm banner, threat count, hit direction, flashbang whiteout.
- **Progression and save:** missions completed, best scores and ratings, training completion, unlocks (attachments, two officers, a mission per milestone, uniforms), mission records, lifetime stats, loadouts, squad choice, settings and key bindings in one versioned JSON file.
- **Audio:** pooled `AudioManager` with categories, per-category volume and mute; procedural placeholder sounds for weapons, reloads, footsteps, doors, equipment, radio chirps, detection, alarms, civilians, UI, mission complete/fail; indoor room tone vs outdoor wind. (Extended in the polish update: music, weapons category, reverb, surfaces.)
- **Settings:** quality preset (Auto + 5 tiers), FPS counter, volumes and mutes, difficulty, zoom sensitivity, look-ahead, default zoom preset, planning pause vs slow motion, line-of-sight fog, campaign reset, full key remapping with conflict swapping. (Greatly extended in the polish update; see Part 1.)
- **Editor tools:** create Boot scene, export editable data assets, open screenshots folder, delete save.

## Features simplified or deferred

- **Scenes, prefabs, materials and models are generated by code at runtime**
  instead of being authored as `.unity`/`.prefab`/material/model files. The
  spec asked for those to be created; generating them from code was chosen
  because this work was done without a running Unity editor, where hand-writing
  scene and prefab YAML is error-prone. The Boot scene is created by the editor
  script on first open. Separate scenes per map (`Mission_Office.unity`, etc.)
  were replaced by building each map into the one scene.
- **No bank map.** It appears only in the spec's suggested folder structure; three playable maps plus training were built.
- **Portraits and mission thumbnails** are drawn from flat shapes, not rendered images.
- **Animations** are procedural (pose blending, recoil, crouch, reload, fall) on primitive-based characters; no rigged models or animation clips. No weapon inspection animation.
- **Audio** is procedural placeholder sound. Radio and squad acknowledgments are text subtitles with a radio chirp and a short voice-like blip; there is no voice acting.
- **Vehicles:** arrival sequence only; no mission-end return sequence, no driving.
- **Camera collision avoidance** is not implemented (walls are drawn low and the camera stays above them).
- **Multiple floors** are laid out side by side and linked by stair teleports. Squadmates cross floors when following you or escorting a civilian; suspects never change floors.
- **The team leader cannot be revived;** going down ends the mission.
- **Key remapping** uses the game's own binding table (read through the Input System when it is installed) rather than an Input Action asset and its rebinding UI. Gamepad support (added in the polish update) uses a fixed layout.
- **Recon camera** is a text summary, as the spec allows; no live rendering.
- **UI** is IMGUI drawn in a 1080-pixel-high virtual space (scaled by the UI scale setting, never narrower than 1440 virtual pixels). It is laid out for 16:9; narrow aspect ratios (4:3) may overlap panels.

## Testing performed

What was actually done:

1. **Compilation outside Unity** (repeated after each pass of the polish
   update). All scripts under `Assets/Scripts` were compiled with the Mono C#
   compiler (`mcs`) against the UnityEngine module reference assemblies for
   Unity 2021.3.33 (NuGet package `unityengine.modules`), in three configurations:
   - default (classic Input Manager path);
   - `ENABLE_INPUT_SYSTEM` + `SWAT_INPUT_SYSTEM` against a hand-written stub of the Input System API surface the code uses (keyboard, mouse and gamepad);
   - `UNITY_EDITOR` against a hand-written stub of the UnityEditor APIs the editor script uses.

   Result: all three compile with **0 errors and 0 warnings** (warning level 4).
   Limits: the reference assemblies are Unity 2021.3, not Unity 6; the stubs
   only prove the code is consistent with the signatures I wrote into them;
   `mcs` is a different compiler from Unity's Roslyn compiler (the code sticks
   to C# 7 features to stay compatible with both).
2. **Shader syntax check.** Each vertex and fragment program of the four
   shaders was extracted and compiled as HLSL with `glslangValidator` against a
   small stub of the UnityCG functions they use. Result: all programs compile.
   This checks syntax and types only, not Unity's shader compiler, pipeline
   compatibility or what the shaders look like.
3. **Code review against the milestone and polish acceptance criteria**, tracing the main
   flows by reading the code (deployment, squad stack and entry, objective
   tracking, scoring, save/progression, restart cleanup). Problems found and
   fixed during that review included: training steps that could soft-lock if
   done out of order, solo training with no squad, shield carriers unable to
   finish the weapon-switch step, stacking officers walking their own door
   open, stack positions inside the breaching charge's blast radius, the
   deployment cutscene revealing hidden suspects, menu buttons clickable under
   the settings window, and the screenshot notice appearing in the screenshot.

What was **not** done: opening the project in Unity, entering Play Mode,
checking the Unity Console, verifying NavMesh generation, testing any
gameplay, testing a gamepad, listening to the audio mix, scene/prefab
reference checks (there are none to check), building a player, profiling or
measuring performance, or capturing screenshots.

## Known issues and risks

- Untested at runtime. Expect bugs that only show in Play Mode: AI getting
  stuck on specific furniture, balance (suspect accuracy, damage, scoring),
  UI elements overlapping at some resolutions, and possible API behavior
  differences between Unity 2021.3 (compiled against) and Unity 6.
- Lighting values (light pool strength, fog, grading, emergency lights) were
  chosen by reasoning, not by looking at the result; expect to tune them.
- The mouse-sensitivity game cursor (any value other than 1.00) locks the
  system cursor. If it misbehaves on a platform, setting sensitivity back to
  1.00 restores the system cursor.
- With the classic Input Manager, mouse sensitivity reads the default
  "Mouse X/Y" axes; if a project removed them, the setting has no effect.
- IMGUI draws each shape as its own draw call; the planning map can issue
  several hundred per frame. Fine on most hardware, but it is the most likely
  UI cost on very slow machines.
- Line-of-sight fog hides suspects your team can't see; with fog turned off
  in settings, all suspects are visible (intended, but it makes missions much easier).

## Performance considerations

- One `Update` in `AIManager` drives every suspect, civilian and squadmate;
  decisions run on a staggered timer (0.1 to 0.3 s depending on the preset), not every frame.
- Perception is distance and field-of-view first, then one line-of-sight
  raycast per candidate; the team's visibility pass runs five times a second.
- Hitscan raycasts for all firearms; grenades fly on a scripted arc without physics.
- Pooled tracers, debris, flash lights, grenades, smoke clouds and audio sources.
- Shared materials per color and surface, primitive meshes, small cached procedural textures.
- NavMesh baked once per deployment with a coarse voxel size; doors use carving obstacles.
- Graphics presets scale render resolution, shadows, dynamic lights, MSAA,
  effect counts and AI update rate; Auto mode steps down when the frame rate
  stays under 40 FPS.

## Exact instructions for opening and playing

1. Install Unity 6 (6000.0.x) with Unity Hub.
2. Unity Hub -> **Add -> Add project from disk** -> select this repository folder.
3. Wait for the import. Check that `Assets/Scenes/Boot.unity` exists; if not,
   run **SWAT -> Create Boot Scene**.
4. Open `Boot.unity`, press **Play**, click in the Game view.
5. Main menu -> **Training** to learn the controls, or **New Mission** to start Operation Glass Desk.
6. See [README.md](README.md) for controls and [Screenshots/README.md](Screenshots/README.md) for capturing the required screenshots.
