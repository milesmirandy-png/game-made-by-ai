# SWAT: Tactical Response - Final Report

This report covers what was built, what was simplified, how it was checked,
and what is still unverified. It has twelve parts: **the view switch (V)
and map rework** (newest, first), **gear, better-looking officers, Ready or
Not-style procedure and realism**, **the
tactical overhaul, new models and first person**, **Gun Game, Elimination,
spectating and pings**, **peek, slide, balance and
punch**, **online multiplayer and
the screenshot tour**, the **gun pack, NPC and arsenal
update**, the **game modes, arsenal and sprite update**, the **pixel-art
style**, the **ten levels,
main menu and Level Creator**, the **quality-of-life, graphics, lighting and
polish update**, and the original build (updated where later work changed
something).

The short version: everything is implemented in C# (plus five small shaders),
compiles in three configurations and the shaders pass a syntax check. The
owner has built the game and played it, including LAN matches with friends;
that is the only play-testing, and it happened before the four newest
parts, which have only been compiled. The two newest parts also add models
that were converted and checked outside Unity, and their first-person view
and character looks were checked with offline software renders of the same
model files (not game screenshots). No profiling or performance measurements have been
made, and no screenshots are in the repository. Treat everything below as
"implemented in code" unless it says otherwise.

# Part 0i: View switch (V) and map rework

## What was added

- **Live view switch** (`Core/ViewMode.cs`, `Environment/ViewParts.cs`,
  `GameManager`, `CameraController`, `GameInput`, `PlayerController`,
  `UIManager`, `SettingsUI`).
  - New input action Switch view (V). In a mission (Deploying, Playing,
    Paused, Debrief) it changes `ViewMode.FirstPerson` at once. Elsewhere
    it changes the saved setting, and the menu screens show the current
    view in the top corner. It is ignored while a text field has the
    keyboard, while rebinding keys, while loading and in the level creator.
  - Levels are now always built for the top-down view. Everything that
    differs in first person registers in `LevelLayout.view`:
    - walls, door leaves and frame posts stretch to their first-person
      height, with collider and texture tiling recomputed;
    - ceilings, door headers and frame tops are switched on;
    - lamps, signs, beacons, security cameras and hung decor move up.
  - Colliders keep full height in both views, so the navmesh, AI sight and
    hit detection don't change.
  - `CameraController` applies the level's view to match the camera every
    frame it changes. Walls are therefore low whenever the camera is
    overhead (dead, spectating, deploying, debrief), even with first person
    chosen; before, the van ride and debrief looked down on full-height
    walls and ceilings.
  - `PlayerController` re-syncs the look yaw and pitch from the current
    facing when the view changes, so you keep facing the same way.
  - The zoom preset default moved from V to Y. `GameInput.Load` moves a
    saved zoom preset on V to Y. If a save has another action on V,
    Switch view is left unbound rather than doubled up.
- **First-person dressing fixes** (`MapDresser`, `SecurityCamera`,
  `AlarmSystem`).
  - Ceiling lamp panels were placed at 1.43 m, which is chest height in
    first person. They now move to 2.72 m there, and lamps that would sit
    on a wall or over a nested room are skipped.
  - Exit signs move from 1.3 m to over the door frame, emergency lights to
    2.2 m, alarm beacons to 2.48 m and security cameras up 0.75 m.
  - Posters, clocks, whiteboards and electrical panels go up 0.55 m.
- **Map rework** (seven map files, plus `LevelBuilder.Pillar`,
  `LevelBuilder.Partition` and nested-room support).
  - Corridors are broken up by closets, cores and alcoves jutting in from
    alternate sides, plus fire doors, smoke doors and a mantrap.
  - Large rooms get columns, head-high partitions, island bars, pallet
    stacks and racks.
  - Doors that lined up across a corridor are staggered.
  - Apartments get bedroom and bathroom walls and doors.
  - Room ids, door ids, consoles, evidence and objective targets are
    kept. Spawn points and props were moved where the new walls needed it.
  - A room inside another room's rectangle (the closets and the
    checkpoint):
    - gets its floor 6 mm higher;
    - has no ceiling of its own;
    - wins `LevelLayout.RoomAt`, which now returns the smallest containing
      room;
    - is the only room it counts toward when investigating
      (`TacticalIntel`).

### Longest straight indoor sightline

Measured by the offline map check (below) along every 1 m line in x and z.
Walls and props 1.4 m or taller block a line. "Doors open" is the worst
case; "doors closed" is how the doors start.

| Map | Before (doors open) | After (doors open) | After (doors closed) |
| --- | --- | --- | --- |
| Offices | 35.8 m | 16.6 m | 16.6 m |
| Bank | 33.8 m | 23.8 m | 16.6 m |
| Clinic | 35.8 m | 20.0 m | 17.6 m |
| Nightclub | 35.2 m | 21.8 m | 21.6 m |
| Apartments | 29.8 m | 18.4 m | 18.4 m |
| Warehouse | 38.6 m | 22.0 m | 22.0 m |
| Factory | 39.6 m | 21.6 m | 21.6 m |
| Store (unchanged) | 19.8 m | 19.8 m | 19.6 m |
| Motel (unchanged) | 10.2 m | 10.2 m | 9.8 m |
| Training (unchanged) | 39.6 m | 39.6 m | 39.6 m (the firing range) |

## Limitations

- Nested rooms count toward their container for "suspects inside" and
  "anything left to do" checks. So a corridor isn't secured until its
  closets are; that's deliberate, but a closet's room light also lights a
  little of the corridor.
- The view switch moves visuals only. A prop that's taller than the
  top-down walls is drawn at its full height in both views, as before.
- The sightline numbers come from the map layouts, not from the game. They
  ignore diagonals, glass and lighting, and a 1 m sampling grid can miss a
  gap narrower than that.
- The new layouts and the moved spawns haven't been played. Whether the
  zig-zag corridors, partitions and new closets play well (pathing,
  squad stacking on the new doors, AI cover use) is unverified.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings at
  warning level 4).
- **Offline map check.** The map files are compiled against a recording
  stub of `LevelBuilder` and the result is checked in Python. On all ten
  maps:
  - no prop within 1.1 m of a doorway or on a spawn or evidence point;
  - no spawn inside a wall;
  - every suspect and civilian spawn reachable from the van, on a 0.2 m
    grid with a 0.36 m character radius;
  - plus the sightline measurement above.
  - All ten pass. The baseline run found one problem, an office plant on a
    civilian spawn, which is fixed.
- Top-down plans of every map were rendered from the same data and
  looked over after each change.
- **Not done:** any play-testing. Not run in Unity:
  - switching views (in a mission, paused or in the menus);
  - the stretched walls and doors and the moved lamps and decor;
  - the new layouts, and the AI and squad on them.

# Part 0h: Gear, better-looking officers, Ready or Not-style procedure and realism

## What was added

- **Gear customization** (`GearCatalog`, `OfficerLoadout`, `LoadoutUI`,
  `CharacterFactory`).
  - The loadout screen has three pages: Weapons, Armor & gear, Look.
  - Look tab: 8 headgear options, 4 faces, 4 facial hair options, 6 hair
    colours, long or rolled sleeves, 9 patch designs and 8 patch colours.
    All are stored per officer and clamped when a save is loaded.
  - The vest's look comes from the armor tier (`GearCatalog.StyleFor`):
    none, light (slick), standard (pouches and pack) and heavy (adds
    shoulder guards, collar and groin protector).
  - New `armor_none` ("No Armor"): tier 0, speed x1.08, +2 equipment
    capacity, no protection.
  - Two looks do something: the gas mask face blocks CS gas, and the
    helmet-with-NVG headgear enables night vision. The rest is cosmetic.
- **Attachments** (`AttachmentData`, `DefaultContent`, `Weapon`,
  `WeaponModels`, `WeaponSpritePack`).
  - Six slots: light, optic, muzzle, stock, underbarrel and magazine.
    There are 18 attachments.
  - New stats: magazine size, reload time, aim speed, zoom, look-ahead,
    flash size and laser. `Weapon` applies them.
  - `WeaponModels` measures each gun mesh by slicing its triangles with
    planes. That finds the top rail, muzzle, handguard underside and
    magazine well, and the attachment visuals are placed there.
  - The pixel-art icons get matching overlays.
  - The drum magazine was removed at the owner's request.
- **Smoother soldier** (`Tools/ModelConverter/subdivide.py`, `convert.py`).
  - One step of Loop subdivision with creases: open edges, colour-slot
    borders and edges sharper than 45 degrees stay sharp.
  - `soldier.bytes` is about 13,840 triangles. `soldier_low.bytes` keeps the
    original 3,460, and `CharacterFactory.SoldierModel()` uses it when the
    texture quality is Low (the Potato and Low presets).
- **Headgear and faces that fit** (`MeshKit`).
  - Caps, beanies, boonies, hair, beards and the gas mask are shells. A
    shell copies triangles of the head, splits each into four with the new
    points pushed out, and offsets the result along welded normals, so it
    follows the head.
  - Brims and the face shield are generated fans and arcs.
  - Eyes, brows and patches are placed by measuring the model at run time
    (`FrontZ`, `OuterX`), so they sit on the surface of either soldier
    model.
- **Camo kits** (`ModelLibrary.Camo`, `Progression`).
  - Four kits: Arid, Woodland, Urban and Night. The uniform's triangles are
    split into four and each piece is coloured by 3D value noise in model
    space.
  - It's geometry colour, not a texture, because the toon shader has no
    texture slot. Variant meshes are built once and cached.
- **Ready or Not-style procedure.**
  - Reports to TOC (`TocReports`, key **H**): restrained or dead suspects,
    controlled, injured or dead civilians, and downed officers within 6 m,
    under 70 degrees off the view and in sight. Squadmates report what they
    restrain. Scoring is +10 per report and -15 per person left unreported.
  - Dropped weapons (`DroppedWeapon`): a suspect drops their gun on
    surrender or death. The player secures it by holding E (0.8 s), and
    squadmates pick up loose guns within 1.8 m. Scoring is +10 secured and
    -20 left. A suspect faking a surrender grabs their gun back if it's
    still on the floor within 2.5 m. Otherwise, 6 times in 10 they give up
    for real, and the rest have a backup gun.
  - CS gas (`SmokeCloud` gas mode, `EquipmentKind.CSGas`): suspects are
    stunned, staggered and have a 12% chance per half-second tick to
    surrender (4% for leaders). Officers, the player and civilians without a mask choke. The
    player can't sprint and gets slower and less accurate. The new squad
    door order is Gas & clear.
  - Night vision (key **N**): a post-effect mode, green and amplified with a
    tube vignette. The line-of-sight system treats the player's eyes as
    seeing in the dark while it's on. Without the post pass it falls back to
    a HUD tint.
  - Chem lights (`ChemLight`): thrown glow sticks with a small light, shown
    on the tactical map.
- **Realism.**
  - Magazines (`Weapon`): magazine-fed guns keep a list of spare magazines.
    `FinishReload` takes the fullest spare that holds more than the current
    magazine and puts the current one back if it isn't empty. Shotguns,
    revolvers, less-lethal launchers, grenade launchers and pepperball guns
    keep loose rounds. Setting `Reserve` (van resupply) rebuilds full
    magazines.
  - Realistic ammo (setting, on by default, missions only): the HUD shows
    Full, Heavy, Half, Light or Empty, plus spare magazines as pips filled
    by how full each one is. The squad list and weapon wheel show
    magazines left.
  - Limb wounds (`OperatorHealth`, missions only): a hit of 6 damage or more
    below 0.85 m (local height) is a leg wound. Below 1.45 m and more than
    0.17 m off centre, it's an arm wound.
    - Leg: no sprinting and 80% speed, for the player and squadmates.
    - Arm: 1.3x spread and 85% turn rate.
    - A medical kit, a revive or a full restore treats both.
  - Suppression (`PlayerController.Suppression`): a suspect's round passing
    within 1.3 m adds 0.12-0.35 (closer adds more), and it decays at 0.45
    per second. It multiplies spread by up to 1.6 and darkens the top and
    bottom of the screen. Missions only.

## Limitations

- **Gear and looks:**
  - Shells are offset copies of the head, so a cap or beard follows the
    skull exactly; a hat can't sit loose or tilt.
  - The patch is a few flat quads, not a decal.
  - The camo is per small triangle, so at close range the blotches have
    angular edges.
- **Attachments:**
  - Placement is measured automatically. A gun with an unusual shape could
    put a grip or light slightly off.
  - Stats are first guesses.
- **Smoother soldier:** about 4x the triangles of the old model. It's still
  low-poly, but with a full squad, suspects and the game-mode teams on
  screen that adds up. Nothing has been measured.
- **Ready or Not-style procedure:**
  - TOC replies are a few fixed lines.
  - Weapons dropped by suspects aren't physical objects (no throwing or
    sliding).
  - CS gas is a sphere, so it doesn't fill a room's shape or seep under
    doors.
- **Realism:**
  - With magazines, picking up partial magazines from the van merges them
    into full ones.
  - Wounds are located from the hit point's height on the body, so the
    locations are approximate.
  - The game modes are left as they were (no wounds, no suppression, exact
    ammo count).
- **Not tuned:** none of these numbers (scoring, gas, wound penalties,
  suppression) has been tuned by playing.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings at
  warning level 4).
- The shaders pass the same syntax check as before. That includes the new
  night-vision code in the post effect (glslangValidator, with `_Time`
  added to the stub).
- The online transport tests still pass (`Tests/`, both ALL PASSED).
- Model pipeline:
  - Re-running the converter reproduces all 20 committed model files byte
    for byte, including `soldier.bytes` (smoothed) and `soldier_low.bytes`.
- Offline renders (the converter's software rasteriser, not game
  screenshots):
  - The new looks were checked with a Python port of the shell, brim and
    camo steps on the same model files. That covered every headgear, face
    and facial hair, the camo kits, and the smoothed soldier next to the
    original.
  - Attachment placement was rendered on each gun model.
  - The first-person view was rendered again with the scope and laser.
- The icon overlays were drawn by the real `WeaponSpritePack` code, run
  under Mono with a small harness.
- **Not done:** any play-testing. Not run in Unity:
  - the Look tab and the gear on characters in the game;
  - attachments in play;
  - TOC reports, dropped weapons, CS gas, night vision and chem lights;
  - magazines, the realistic ammo HUD, wounds and suppression.

# Part 0g: Tactical overhaul, new models and first person (body cam)

## What was added

- **Heavier handling** (`PlayerController`, `WeaponController`). Walk 3.3
  m/s, sprint 5.3 m/s, with momentum: 9 m/s² to speed up, 13 m/s² to slow
  down. Stamina drains at 22/s and regenerates at 10/s. With Heavy weapon
  handling on (default), the aim turns towards the cursor at 260-560°/s,
  set by the weapon's weight. Steady aim scales that by 0.7, sprinting by
  0.6 and a shield by 0.65. After a sprint the gun takes 0.9x its switch
  time to come up, with extra bloom. Moving adds spread (less while crouched
  or steady aiming). A slide costs 22 stamina, has a 1.2 s cooldown and
  blocks firing.
- **Lethality** (`Core/Lethality.cs`, missions only): police damage to
  suspects x1.6 (`EnemyHealth`), suspects' damage to officers x2
  (`EnemyWeapon`).
- **Doors** (`DoorController`, `MissionRandomizer`, `ReconCamera`,
  `SquadAI`, `SquadCommandManager`).
  - Locked, non-electronic, unpickable doors are kicked with **E** (0.6 s
    wind-up): a Breacher always succeeds, anyone else 40% of the time. Kicks
    are loud.
  - A shotgun shot at a door within 3 m breaches it (offline).
  - Traps: up to 1 + difficulty interior doors next to an armed suspect's
    room, 40% each, get a flash device on the far side. Opening the door
    sets it off: a flashbang and an alarm noise. The recon camera or the
    new Mirror order reveals it, and a revealed trap is disarmed by holding
    **E**. The squad disarms a known trap before carrying out a door order.
  - New wheel orders: Mirror under door, and Shotgun & clear (needs a
    shotgun carrier). Charges go on with **G**.
- **Shield and melee** (`OperatorHealth`, `PlayerController.Melee`).
  - A braced shield blocks every round within 70° of the front (spark and
    metal ricochet). A carried shield passes 20% of the damage within 60°.
  - Melee (**Left Alt**): reach 1.05 m, daze 2 s, 1.1 s cooldown; a shield
    bash has 1.4 m reach, a 3 s daze and a 0.8 s cooldown. It calls
    `EnemyAI.Shoved`. Civilians are stunned and told to get down. In the
    game modes it does 15 damage (25 with a shield).
- **Suspect AI** (`EnemyAI`, `EnemyController`, `EnemyWeapon`,
  `AIManager`).
  - A new Holding state: an alerted suspect may hold an ambush angle on a
    door instead of charging.
  - Chasers spread out instead of all following the same path.
  - Police shots that pass near a suspect suppress them (`AIManager.Suppress`).
  - Suspects kneel behind cover.
  - Some surrender, then pull a gun again once no officer is covering them
    (`TryFakeOut`).
- **Models** (`Core/ModelLibrary.cs`, `CharacterFactory`, `WeaponModels`,
  `ProceduralAnimator`, `Tools/ModelConverter/`).
  - The supplied FBX and GLB files are converted offline by small Python
    readers into a compact "SWM1" mesh format (19 files, about 0.9 MB in
    total). Textures become flat colours per triangle.
  - The soldier is split into torso, head, upper arms, forearms and legs,
    with hand points. The animator gives the arms a two-bone reach so both
    hands stay on the gun.
  - 15 weapons map to pack guns; suspects also get the double-barrel and
    the snub revolver. Guns not in the pack (LMG, rotary, launchers, auto
    and drum shotguns, pepperball, stun pistol) keep their box models.
- **Uniform kits** (`Progression.Kit`): ten kits with their own shirt,
  trousers, vest, pouches, helmet and gear colours, unlocked at 0-4
  completed missions.
- **First person** (`Core/ViewMode.cs`, `Player/FirstPersonRig.cs`,
  `CameraController`, `GameInput`, `PlayerController`, `HUDController`,
  `PostEffects` + `SwatPostFX.shader`, `SettingsUI`).
  - The view is chosen in Settings -> Camera and applied when a mission or
    match level is built (since Part 0i, V switches it at any time and the
    level's walls switch with it). In first person, walls are built 2.75 m high,
    with headers over openings and door frames at 2.08 m. Indoor rooms get
    ceilings with no collider and no shadow, so the navmesh, AI sight and
    lighting are as before.
  - The cursor is locked. Mouse delta (x0.07 x sensitivity) or the right
    stick turns the officer and tilts the view (±80°). Shots start at the
    camera and spread in a cone around the view direction. The aim point is
    whatever the view centre hits within 40 m, so throws, squad orders and
    pings use it. The map and radial menus free the cursor.
  - Camera: eye height 1.6 m (1.05 m crouched, 0.8 m sliding), smoothed. It
    moves out with the lean and rolls 10° at full lean. Step bob and a slow
    Perlin drift follow the Camera Shake setting and are reduced while
    aiming. Explosion shake and takedown punch carry over. The setting's
    field of view is horizontal and converted to vertical for the screen's
    aspect. Aiming down the sights zooms x0.8; scoped rifles (DM2, PC9) zoom
    x0.39 and x0.6 and show a drawn scope with the gun hidden.
  - View model: the weapon's own model, plus the soldier's forearms in the
    officer's kit (sleeves and gloves), parented to the gun. The trigger hand
    is on the grip and the other hand on the handguard (both on the grip for
    a pistol). Hip, sight, sprint low-ready, wall pull (sphere cast along
    the view), reload tilt with the support hand going for a magazine, draw
    from below, melee shove, turn sway (heavier guns lag more and settle
    slower) and step bob are all blended in code.
  - Each shot kicks the gun back and up, punches the view, and calls
    `PlayerController.AddLookKick`. The view climbs 0.35-1.85° per shot by
    weapon kick, and half of it settles back once you stop.
  - A shield is held low on the left when carried, and with its top edge
    just under the eye line when braced; the pistol rests on its right edge.
  - Your own body's renderers are set to shadows-only (or not drawn if they
    cast no shadow), so you keep your shadow. The flashlight moves to the
    view-model gun and points along the view. Muzzle flames turn to face the
    camera and are smaller.
  - HUD: a dot instead of the spread cross, fading as the sights come up
    (hit and takedown markers stay). The hit-direction chevron is relative to
    where you face. Optional overlay: REC light, date and time, unit and
    callsign. Post effect (Body cam look): barrel distortion (centre fixed,
    corners fixed), edge colour fringing, grain and a stronger vignette. It
    straightens while aiming and is off through a scope.
  - Line of sight no longer hides people in first person, because walls
    already do. In the game modes the "seen" state still decides name tags
    and the map, so they don't show through walls.
- **Settings** gets a Camera tab: camera view, field of view, body cam look
  and the top-down camera options. This also fixes the Gameplay tab, where
  the two settings added in the previous part had pushed the minimap sliders
  into the reset button.

## Limitations

- First person reuses the top-down levels. Rooms are sized for a view from
  above, so some feel large at eye level. Props and lighting were not
  re-dressed for it.
- The view model is posed procedurally. There are no hand-made animations,
  the fingers don't wrap the grip, and the forearms are rigid. Reloads don't
  show the magazine leaving the gun, and pump or bolt cycling isn't shown.
- Aiming down the sights looks over the top of the gun's model. For guns
  whose top isn't the sight (a tall rear sight, a carry handle), the line
  can sit a little high.
- The view model is drawn by the main camera, so it can still clip into
  geometry that the 0.65 m-ish wall check misses (a thin pole, another
  character).
- Other players see your third-person body as before. Online, the pitch you
  look at isn't sent, so others see you aim level.
- The models' textures are flattened to one colour per triangle, so fine
  texture detail (camo patterns, labels) is lost.
- The tactical overhaul's numbers (speeds, lethality, trap rates) are first
  guesses that haven't been played.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings at
  warning level 4).
- The online transport tests still pass (`Tests/`, both ALL PASSED).
- Model pipeline:
  - Re-running the converter reproduces the committed files byte for byte.
  - A Mono test with a copy of `ModelLibrary`'s reader parsed all 19 files.
  - Every converted mesh has positive signed volume, so its faces point
    outward.
  - Replaying the arm reach in Python with the model's joint positions puts
    the hands on their targets (the rifle support hand within 5 cm).
- The first-person view-model layout (hip, sights, sprint, pistol, shield
  carried and braced) was rendered offline with the converter's software
  rasteriser, using the same model files and transforms as
  `FirstPersonRig`. That's how the hands were checked on the grip and
  handguard, the sight line at the screen centre, and the poses moved into
  view. It is not a game screenshot.
- The shaders, including the new body cam code in the post effect, pass
  the same syntax check as before (glslangValidator on the extracted HLSL
  with a stub of Unity's include files). That checks syntax, not looks.
- **Not done:** any play-testing. Not run in Unity:
  - the first-person camera, view model, recoil and sway;
  - the body cam lens effect;
  - the tactical changes, door traps and new orders, shield blocking and
    melee;
  - the AI changes and the new models in the game;
  - the Settings Camera tab at different resolutions.

# Part 0f: Gun Game, Elimination, spectating and pings

## What was added

- **Gun Game** (`GameMode.GunGame`, `VersusMatch` "Gun Game" section). The
  ladder is 16 guns, from the RG6 rotary down to the BK6 pistol. Ladders of 8
  or 12 take evenly spaced rungs and always end on the BK6:
  - 8: RG6, KV, D20, X4, GL6, TS8, M9, BK6.
  - 12: RG6, LM8, SR3, D20, CX, AS12, B4, DM2, TS8, PC9, R6, BK6.
  A member's rung is simply their tag-out count, which every copy of an
  online match already knows: kills arrive in snapshots, and your own come
  from the host's takedown events. So no new per-player state had to be
  synced. You carry only the rung's gun: `WeaponInventory.SetOnly` makes
  primary and sidearm the same weapon, switching and the wheel skip the
  sidearm, and there are no attachments. A shield is set aside
  (`OperatorHealth.SetShield`). Bots get theirs with `ArenaBot.SetGun`. The
  host scores each team as its best climber's rung; reaching the ladder's
  length wins ("Finished the gun ladder"). Banners announce "NEXT GUN" and
  "FINAL GUN".
- **Elimination** (`GameMode.Elimination`): no respawns mid-round (bots,
  remote players and you). A round ends when one side has nobody left (both
  empty: a draw) or after 120 s (more officers still in wins; equal is a
  draw). The winner scores a round, a `RoundWon` event shows a banner on
  every screen, and after a 4 s break everyone is respawned at their base
  (`StartRound`), remote players through the existing respawn message. A
  player still loading counts as in, so a slow loader can't lose round one
  for their team. The match ends after the break of the deciding round, or
  when the match clock runs out. Snapshots carry the round number, whether
  it's over and when it started, so clients show the round clock and a
  "ROUND n" banner.
- **Spectating** (all modes): 1.5 s after you're tagged out, the camera
  (`CameraController.SpectateTarget`) follows a teammate who's still in;
  Fire picks the next. It stops when you respawn or the match ends. Fog of
  war already used your whole team's eyes, so what's shown doesn't change.
- **Pings** (new action `Ping`, middle mouse, game modes only): a ping at the
  aim point, or on an opponent your team can see within 2.5 m of it. One
  active ping per player lasts 6 s. It shows as a diamond with the sender and
  distance (pinned to the screen edge when off-screen), a ring on the
  tactical map and a radio chirp. The two nearest bots of that team go there
  for up to 12 s (`ArenaBot.Ping`), unless they're defending, holding the
  zone or carrying a flag. Online, a new `Ping` message goes from a player to
  the host and from the host to that player's teammates. The host's bots
  react to every team's pings, but the host only sees its own team's.
- **UI:** the Game Modes screen lists the five modes in shorter rows, and the
  score stepper reads "Ladder" or "Rounds to win". The HUD shows your gun and
  the next (Gun Game), and the round, who's still in and the match clock
  (Elimination, with the round clock in the middle). The team list shows
  rungs, "out this round" and whom you're watching. The tagged-out text
  moves up while you're spectating.
- **Online version:** `MessageVersion` is now 3 (new modes, round fields in
  snapshots, `Ping`), so a copy without this part is refused with "Different
  game version".

## Limitations

- In Gun Game every tag-out counts, including with a launcher grenade that
  catches several people, which can skip rungs. There's no knife or
  demotion.
- Elimination has no buy phase, side swap or overtime. A draw scores nobody,
  so a long run of draws can let the match clock decide it.
- Spectating follows bots and players on your team, not opponents, and there
  is no free camera.
- Pings are mouse-only (no gamepad button is free), and only in the game
  modes.
- Bots don't use the rotary gun's spin-up (true before this part too).

## Testing performed for this part

- All scripts compile in the three configurations (0 errors).
- The ladder rungs for 8, 12 and 16 guns were computed with the same rounding
  the game uses and checked for repeats (none).
- **Not done:** any play-testing. Not checked in play: the new modes offline or online,
  round resets with remote players, spectating, pings between two copies,
  and the new HUD layout at different resolutions.

# Part 0e: Peek, slide, balance and more punch

## What was added

- **Peek / lean** (`Player/PlayerController.cs`, hold **Left Ctrl**, a new
  remappable action `Peek`). The upper body moves up to 0.55 m to one side.
  The side is the one that shows more along your aim: from each leaned
  position the game checks how far you could see straight ahead and a little
  diagonally (raycasts) and picks the bigger gain. A movement key overrides
  this, leaning that way on screen. A sphere cast stops the lean
  0.2 m short of any wall, also while turning. Movement is held still while
  peeking. `ChestPosition` and the new `EyePosition` include the lean, so
  shots, the tactical map's line of sight (`TacticalIntel`), team visibility
  in game modes (`VersusMatch.UpdateVisibility`), and where suspects, bots
  and cameras aim and look all use the leaned position.
  **Lean hitbox** (`Core/LeanHitbox.cs`): a small capsule follows the lean
  (the body capsule stays behind the corner), so a peeking officer can be hit
  where they can be seen. It is switched off while standing straight and
  ignores your own movement capsule. A peeking officer is spotted at 80% of the
  usual range (`AIVisibility.VisibilityOf(ICombatTarget)`). Spread is ×0.85
  while peeking. The camera shifts 1.5× the lean toward that side.
  The animation (`Core/ProceduralAnimator.cs`) shifts the model sideways and
  tilts it 15°.
- **Slide** (crouch while sprinting and moving; 12 stamina; 0.5 s cooldown):
  9.5 m/s (scaled by the officer's speed and half the armour's speed penalty)
  for 0.6 s, easing to 30% speed, about 4 m in all. It steers at up to
  80°/s toward the movement keys and ends early when it runs into something.
  It ends crouched. Dust puffs, a scrape sound (new `Sound.SlideScrape`),
  noise radius 5 for the AI, a small camera kick, and +2.5° spread while
  sliding. The animation leans back with the legs out. Sprinting from a
  crouch now stands you up (when stamina is above 25%).
- **Falls** (`Core/Topple.cs`): instead of snapping flat, a downed character
  tips over away from the hit (the shot's direction, or the last hit in the
  past 0.6 s, else backwards) over 0.34 s, accelerating and shifting 0.45 m
  along the fall, then lands with a thud (new `Sound.BodyFall`) and dust. It runs as
  its own component because some owners stop animating a downed character,
  and getting up cancels it immediately. Used by suspects, civilians, officers, bots, online players
  and you.
- **Punch:**
  - Your hits play a new thud (`Sound.HitThud`), once per shot even with
    pellets, and get a spark sized by damage.
  - Hit markers pop. Takedowns get a bigger popping X with an expanding ring,
    a camera punch-in (new `CameraController.Punch`, a quick 7% zoom), a
    jolt and a 0.06 s freeze (was 0.045 s, offline only, as before).
  - Guns with heavy kick also punch the camera.
  - Being hit: suspects fire late and with 55% of their hit chance for
    0.35 s (`EnemyWeapon.Stagger`). Bots fire 0.1 s late with 1.6× aim error
    for 0.3 s. Your own aim blooms by 0.5-2° depending on the damage
    (`WeaponController.Flinch`).
  - The last suspect in a mission goes down in 1.1 s of slow motion (30%
    speed, easing back), offline only and only with the Hit stop setting
    on. All camera effects follow the Camera Shake setting.
- **Online:** movement updates (`State` and the snapshot's actor entries)
  carry two more flags (sliding, peeking) and a lean byte. `NetActor`
  shows the slide (with its sound), eases into the lean, and moves its own
  lean hitbox and chest position with it. Because the messages changed, the
  version check now includes a message version (`NetSession.MessageVersion
  = 2`), so a copy from before this update gets "Different game version"
  instead of misreading the messages. The transport itself (`NetPeer.Protocol`)
  is unchanged, so old and new copies still see each other's LAN games and the
  version message gets through.
- **HUD:** the status line shows *Sliding* and *Peeking left / right*.
  The Hit stop setting is relabelled "Hit stop and last-takedown slow-mo".

## Balance: time-to-kill table

Computed from the weapon data with a script (not measured in play), after
this part's changes. "Hits / time" is the number of hits needed and the time
from the first to the last of them at the gun's fire rate, assuming every
shot hits. Semi-automatic guns are capped at 7 trigger pulls a second. The
burst rifle includes its 0.28 s pause between bursts. Shotguns assume 6 of 8
pellets hit (close range). The rotary includes its spin-up. Targets: bots
in the game modes take 85% damage on 100 health. Armed suspects have 100
health and no armour. Armored suspects have 150 health and take 60%. A
player wears the standard plate carrier in good condition (65% damage
taken). Real fights take longer: misses, spread, movement and range all add
time.

| Weapon | Damage | Shots/s | Mag | Bot: hits / time | Armed suspect | Armored suspect | Player (standard armor) | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| K7 Compact SMG | 16 | 14.0 | 30 | 8 / 0.50 s | 7 / 0.43 s | 16 / 1.07 s | 10 / 0.64 s |  |
| V10 Submachine Gun | 19 | 11.0 | 30 | 7 / 0.55 s | 6 / 0.45 s | 14 / 1.18 s | 9 / 0.73 s |  |
| CR5 Compact Rifle | 26 | 9.5 | 30 | 5 / 0.42 s | 4 / 0.32 s | 10 / 0.95 s | 6 / 0.53 s |  |
| SR3 Service Rifle | 30 | 8.5 | 30 | 4 / 0.35 s | 4 / 0.35 s | 9 / 0.94 s | 6 / 0.59 s |  |
| TS8 Tactical Shotgun | 15 | 1.4 | 7 | 2 / 0.71 s | 2 / 0.71 s | 3 / 1.43 s | 2 / 0.71 s | 13 -> 15 per pellet, 1.3 -> 1.4/s |
| PC9 Precision Carbine | 45 | 3.0 | 15 | 3 / 0.67 s | 3 / 0.67 s | 6 / 1.67 s | 4 / 1.00 s |  |
| P17 Service Pistol | 28 | 5.0 | 15 | 5 / 0.80 s | 4 / 0.60 s | 9 / 1.60 s | 6 / 1.00 s |  |
| BK6 Backup Pistol | 24 | 6.0 | 8 | 5 / 0.67 s | 5 / 0.67 s | 11 / 1.67 s | 7 / 1.00 s | 22 -> 24 |
| H50 Heavy Sidearm | 48 | 2.5 | 7 | 3 / 0.80 s | 3 / 0.80 s | 6 / 2.00 s | 4 / 1.20 s | 2.2 -> 2.5/s |
| X4 Defense Weapon | 18 | 13.0 | 40 | 7 / 0.46 s | 6 / 0.38 s | 14 / 1.00 s | 9 / 0.62 s |  |
| B4 Burst Rifle | 28 | 14.0 | 30 | 5 / 0.49 s | 4 / 0.42 s | 9 / 0.99 s | 6 / 0.57 s |  |
| CX Bullpup Rifle | 27 | 10.0 | 30 | 5 / 0.40 s | 4 / 0.30 s | 10 / 0.90 s | 6 / 0.50 s |  |
| DM2 Marksman Rifle | 62 | 2.2 | 10 | 2 / 0.45 s | 2 / 0.45 s | 5 / 1.82 s | 3 / 0.91 s |  |
| LM8 Light MG | 21 | 12.0 | 75 | 6 / 0.42 s | 5 / 0.33 s | 12 / 0.92 s | 8 / 0.58 s | 24 -> 21 |
| AS12 Auto Shotgun | 11 | 3.2 | 8 | 2 / 0.31 s | 2 / 0.31 s | 4 / 0.94 s | 3 / 0.62 s |  |
| M9 Machine Pistol | 15 | 15.0 | 20 | 8 / 0.47 s | 7 / 0.40 s | 17 / 1.07 s | 11 / 0.67 s |  |
| R6 Revolver | 60 | 1.8 | 6 | 2 / 0.56 s | 2 / 0.56 s | 5 / 2.22 s | 3 / 1.11 s | 55 -> 60 |
| RG6 Rotary Gun | 15 | 20.0 | 150 | 8 / 0.85 s | 7 / 0.80 s | 17 / 1.30 s | 11 / 1.00 s | 14 -> 15, spin-up 0.6 -> 0.5 s |
| D20 Drum Shotgun | 9 | 4.0 | 20 | 3 / 0.50 s | 2 / 0.25 s | 5 / 1.00 s | 3 / 0.50 s |  |
| KV Vector SMG | 16 | 18.0 | 25 | 8 / 0.39 s | 7 / 0.33 s | 16 / 0.83 s | 10 / 0.50 s | 17 -> 16 |
| GL6 Marker Launcher | 70 | 1.2 | 6 | 2 / 0.83 s | 2 / 0.83 s | 4 / 2.50 s | 3 / 1.67 s |  |

What changed and why: the LM8 had the fastest kill time of any automatic and
the biggest magazine, so its damage went down. The KV vector was the fastest
killer outright, so it lost a point of damage. The TS8 pump shotgun needed two
shots at any range, which made it feel weak for a pump gun. Now all 8 pellets
point blank drop a bot or suspect in one shot. The revolver needed three hits
on a bot (46.75 per hit, just short of 50). At 60 it takes two, which suits a
six-shot gun with a slow reload. The H50 and BK6 sat at the bottom of the
sidearms. The rotary gun was the slowest to kill despite its size: slightly
more damage and a shorter spin-up. Everything else was left alone. The
script is not part of the game; the table above is its output.

## Limitations

- Peeking is keyboard and mouse only (no gamepad button is free); sliding
  works on a gamepad (B while sprinting).
- The lean is a straight sideways shift of the upper body. Peeking round a
  low obstacle (leaning over it) isn't a thing.
- Online, lean and slide changes are applied as soon as they arrive, not
  delayed with the position blending (0.1 s), so a remote player's lean can
  start a moment before the matching movement shows.
- A downed body tips over in a straight line and doesn't check for walls. In
  a tight spot part of it can end up in a wall (it is already lying down and
  not hittable).
- Slow motion only happens in missions. A game-mode match ends straight away
  on the winning takedown, so there is no slow-motion finish there.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors).
- The transport tests still pass (23 + 8 checks); the transport wasn't
  changed, only the messages on top of it.
- The time-to-kill table above comes from a script that reads the weapon
  numbers straight out of `DefaultContent.cs`.
- **Not done:** any play-testing of this part (how peeking, sliding, the
  falls or the new effects look and feel), a check that old and new copies
  refuse each other in practice, and a test of the new message fields between
  two running copies. Peeking, sliding and online sync depend on Unity
  physics and can only be checked inside Unity.

# Part 0d: Online multiplayer (peer to peer) and the screenshot tour

## What was added

- **Transport** (`Net/NetTransport.cs`, plain C#, no Unity types): UDP via
  `System.Net.Sockets` (non-blocking, polled once per frame). One player
  hosts; the others connect straight to the host's address. Each packet
  carries a cumulative acknowledgement, millisecond stamps for round-trip
  timing, reliable messages (numbered, resent until acknowledged, delivered
  once and in order, buffered when they arrive early) and unreliable messages
  (snapshots, positions, shots). Connection requests carry the player's hello
  and are retried for 10 seconds; the host can refuse with a reason (lobby
  full, match in progress, different game version: a hash of the weapon,
  officer and map lists). Keep-alives every 0.4 s; 10 seconds of silence is a
  disconnect. `NetDiscovery` finds games on the local network (LAN): the
  Game Modes screen sends a query on UDP 27778 every two seconds to the
  general broadcast address, to each network adapter's own broadcast address
  (computed from its IP and subnet mask; some routers and PCs only pass
  those) and to this computer, and hosts answer with their name, mode, map,
  players and game port; the list merges a host that answers on several
  addresses (by a per-session token) and drops hosts that stop answering.
  Malformed packets are dropped.
- **Session** (`Net/NetSession.cs`): host / join / leave, the lobby (players,
  teams, pings, the host's match settings), starting a match (everyone builds
  the same map from the host's seed; UnityEngine.Random is seeded too), the
  match setup each joining player receives (team, spawn, bases, zone, flag
  homes, roster), snapshots from the host at 15 Hz (clock, scores, zone, flags
  and everyone's position, facing, stance, weapon, health and score line),
  player positions to the host at 20 Hz, shots (flash, sound and tracers for
  everyone else), hits, tag-outs, respawns, match events and results, back to
  lobby and rematch. Host leaving or a lost connection returns players to the
  Game Modes screen with the reason.
- **Players on screen** (`Net/NetActor.cs`): on the host, a stand-in for each
  remote player that bots see, chase and shoot and that carries flags; on the
  other players' screens, everyone else. Movement is drawn 0.1 s behind,
  blending between updates (extrapolating up to 0.25 s if updates stop),
  using the sender's clock with a slowly adapting offset so uneven delivery
  doesn't make people jitter; long jumps (respawns) snap.
- **Combat:** the shooter's game checks its own hits (what you see is what you
  hit) and sends them to the host; the host applies hits on bots and on its own
  player, and passes hits on other players to their game, where armor and
  health are applied. The player who goes down reports it and the host credits
  the takedown. Same-team hits are ignored everywhere.
- **Match logic** (`Versus/VersusMatch.cs`): bots and remote players share an
  `IVersusMember` interface; the HUD, minimap and results use it, so remote
  players appear wherever bots did. Feed lines and banners became `MatchEvent`s
  the host sends and every player words from their own side ("You", "your
  flag"). A joined player's copy runs in mirror mode (no bots or scoring of its
  own). Your officer wears red on the Red Team. Team size now goes down to
  1 vs 1.
- **Rules online:** no pausing or slow motion (the pause menu opens, the match
  goes on), tactical equipment and door changes are disabled so every copy of
  the building stays the same, the host is always on Blue, joining only from
  the lobby.
- **UI:** a panel on the Game Modes screen (name, Host a game, the LAN games
  on your network with Join buttons, and Join by address), the host's lobby list (click a team to
  move a player), a lobby screen for joined players (host's settings, teams,
  team buttons, Squad, Loadout, Leave), online buttons on the results screen
  and pause menu.
- **Screenshot tour** (`Utilities/ScreenshotTour.cs`): Shift+F12 or SWAT ->
  Screenshot Tour plays through 15 moments (menus, a mission, two game-mode
  matches, the level creator) and saves a screenshot of each with
  `ScreenCapture`, then restores the game-mode settings it changed.

## Limitations

- **Never run between two real copies of the game.** The transport was tested
  outside Unity (below); the session, match sync, stand-ins and UI have only
  been compiled and reviewed.
- Internet play needs the host to forward UDP 27777 (no NAT punch-through or
  relay; those need a server). LAN discovery needs broadcasts to be allowed by
  the network and firewall; joining by address works without it.
- Hits are decided by the shooter's game, which feels responsive but trusts
  every player (fine among friends, no cheat protection). With high ping you
  can be hit just after reaching cover on your screen.
- No joining mid-match, no host migration: if the host leaves, the match ends
  for everyone.
- Equipment, door use and pausing are off online; the host is always Blue.
- A match setup with many long names can exceed one packet and relies on IP
  fragmentation (normally fine).

## Testing performed for this part

- All scripts compile in the three configurations (0 errors).
- **Transport, simulated network:** `NetTransport.cs` compiled with Mono and a
  test program that routes packets through an in-memory network with loss,
  duplication, delay and reordering: clean network; 25% loss with 5%
  duplicates and 40-160 ms delay; 50% loss with 10% duplicates and 80-380 ms
  delay (3 or 2 clients, 600-2000 reliable messages each way plus a stream of
  unreliable ones). Every reliable message arrived exactly once and in order in
  all three; the round-trip estimates came out at 34, 191 and 475 ms, matching
  the simulated delays. Also checked: refusal with a reason, giving up on an
  address nobody answers, both sides noticing silence (timeout) and a clean
  leave, 70,000 reliable messages through 20% loss (sequence numbers wrap past
  65,535), random garbage packets (no exceptions, real traffic unaffected),
  and messages of 3,000 and 7,900 bytes mixed with small ones. 23 checks pass
  (`Tests/NetTransportTest.cs`; `Tests/README.md` says how to run them).
- **Transport, real sockets:** the same code over real UDP sockets on this
  machine: LAN discovery (general broadcast, the adapter's broadcast address
  and loopback) found the host and its port, at both its loopback and network
  address, which the game merges into one entry; the machine's network
  address was found; hello and
  welcome went through, about 300 messages each way arrived, the host saw the
  player leave, and a second host on the same port was refused (Mono enables
  address reuse by default; the socket now turns it off). 8 checks pass
  (`Tests/NetSocketTest.cs`).
- **Not done:** two copies of the game playing each other, any measurement of
  bandwidth or smoothness in practice, the screenshot tour itself.

# Part 0c: Gun pack sprites, NPC models and four more guns

## What was added

- **Weapon icons from a pixel-art gun pack** (`Weapons/WeaponSprites.cs`,
  `Weapons/WeaponSpritePack.cs`, `Resources/SWAT/WeaponSprites/`): the owner
  supplied a sheet of side-view pixel-art guns from a free-to-use pack and asked
  for the game's guns to match it 1:1, so each of the 24 weapons now uses one
  of the pack's sprites unchanged (see [CREDITS.md](CREDITS.md)). The sheet was
  checked to be at native resolution (no upscaling pattern), each gun was cut
  out by flood fill on its white background, and the pale anti-aliasing
  pixels reachable from the background were removed so the guns don't get a
  white halo on dark panels. The PNGs are stored as `<weapon id>.bytes`
  TextAssets and decoded with `Texture2D.LoadImage`, so Unity's texture
  importer can't resize, compress or filter them (the project now lists the
  built-in `com.unity.modules.imageconversion` module).
  - `WeaponSpritePack` (pure C#) holds each primary weapon's muzzle, optic
    rail and light mount positions and draws a fitted suppressor, red dot or
    weapon light onto a copy of the sprite in the pack's palette (skipped where
    the gun already has one, e.g. the scoped rifles, or on launchers).
  - `WeaponSprites` keeps a point-filtered texture for whole-pixel drawing and
    a mipmapped, filtered copy for slots smaller than the sprite (weapon wheel,
    loadout list, officer cards). The code-drawn `WeaponSpriteArt` style is the
    fallback for a weapon without an image (mirrored to face the same way);
    the short-lived "weapon icon style" setting was removed.
  - The 3D guns were recoloured to the pack's charcoal steel, with orange wood
    or amber furniture where the weapon's sprite has it.
- **Four new weapons** (24 in total): RG6 rotary gun (`spinUp`: a rising
  spin-up sound, then fire), D20 drum shotgun, KV vector SMG and GL6 marker
  launcher (`blastRadius`: `WeaponEffects.Blast` tags everyone within 3.5 m
  who is in line of sight of the burst, with damage falling to a third at the
  edge). New categories with 3D models, sounds (`Rotary`, `Launcher`,
  `SpinUp`, `Burst`) and bot support. The rotary gun and launcher are
  `versusOnly`: hidden from the campaign loadout, and a saved campaign loadout
  holding one falls back to a default weapon.
- **NPC models** (`CharacterFactory`): bodies built from more parts (waist,
  chest, neck; shoulder, sleeve, forearm and hand per arm; thigh, shin, shoe
  and sole per leg), faces with a nose, brows and a beard or mouth, seven
  hairstyles, glasses or sunglasses, and clothing details (hood up, bandana,
  gloves, holster, backpack, lanyard with ID, skirt, short sleeves, sneakers,
  bandages on injured civilians). `SuspectLook` and `CivilianLook` pick these
  per role; officers and game-mode bots get holsters.

## Limitations

- The pack has no sprite of its own for attachments, so the suppressor, red
  dot and light are drawn by the game in the pack's colours; their mount
  positions were picked by hand per sprite and checked on rendered sheets.
- Each icon matches its weapon by type, not by name: the guns in the pack are
  real-world-looking designs, while the game's names stay fictional.
- The pack's name, author and link still need to be added to CREDITS.md.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors).
- **The icon compositing ran outside Unity:** `WeaponSpritePack.cs` was
  compiled with Mono together with a small program that loads every packed
  sprite, fits all three attachments and saves the result; the rendered sheet
  was inspected for each primary weapon.
- **Not done:** loading the PNGs through Unity's `Resources`/`LoadImage` path,
  seeing the new icons, NPCs or the four new guns in the running game.

# Part 0b: Game modes, arsenal and sprites

## What was added

- **Game modes** (`Scripts/Versus/`, `UI/VersusSetupUI.cs`, `UI/VersusHUD.cs`,
  `UI/VersusResultUI.cs`): Team Deathmatch, Capture the Flag and Zone Control,
  framed as training exercises with marking rounds (non-graphic: players are
  "tagged out" and respawn). A match is a `MissionData` with `mode` set, so it
  reuses the squad, loadout, deployment, pause and restart flow; the
  randomizer is skipped (no suspects, civilians, security devices or locked
  doors) and `VersusMatch` runs instead of `MissionManager`.
  - `VersusMatch`: picks the bases (blue at the arrival point, red in the
    indoor room furthest away by NavMesh path), the zone (the room most evenly
    reachable from both bases, at most 9 x 9 m), flags, spawn points, teams,
    respawns with protection, scoring, flag rules (pick up, drop on takedown,
    return by touch or after 20 s, capture only with your own flag home), zone
    control (alone in the zone pushes control your way, contested stalls),
    fog of war for the red team, a takedown feed and the end-of-match result.
  - `ArenaBot`: one class for bots on both teams: line-of-sight targeting with
    the shared `AIVisibility` rules, reaction time and aim error by skill,
    strafing while shooting, per-weapon firing rhythm (bursts for automatic
    and burst weapons), reloads, teammates sharing sightings, and objectives
    by role (roam/hunt, attack or defend a flag, hold the zone). Blue bots use
    your squadmates' names and loadouts.
  - The player respawns instead of failing (`GameManager.OnPlayerDown`);
    `OperatorHealth` gained `LastHit`, spawn protection and `RestoreFull`.
  - Doors start open (`DoorController.OpenForMatch`) and reopen when a bot
    walks up to one you closed; flashbangs daze bots.
  - HUD: score and clock, flag/zone status, takedown feed, team list, respawn
    countdown, name labels, edge-pinned flag and zone markers; the minimap
    shows bases, zone, flags and bots. The pause menu and debrief adapt.
- **Arsenal:** ten new weapons (`DefaultContent.Weapons`), new categories
  (appended to the enum so saved assets keep their values), `FireMode.Burst`,
  and per-weapon feel fields on `WeaponData` (kick, flash size, tracer width,
  pump action, steady look-ahead, crouch spread, less-lethal surrender bonus,
  shell ejection, accent colour). The loadout screen splits weapons and
  armor/gear into two tabs so the bigger arsenal fits.
- **Gun feel:** `WeaponEffects.Fired` (flame and star sprites from
  `EffectsManager.MuzzleBurst`, light flash, the gun's own sound),
  `CameraController.Kick`, stronger recoil animation, white hit flash
  (`CharacterParts.Flash`), hit spark, a shove via `AgentMover.Nudge`,
  takedown marker and sound, hit stop (`GameManager.HitStop`, setting),
  near-miss whiz, magazine/charge/pump sounds, first-shot accuracy, empty
  reload penalty. New gunshots are synthesized from a transient, a noise
  crack, a pitch-dropping thump and a rumble tail, soft-clipped.
- **Pixel-art gun sprites:** `WeaponSpriteArt` draws each category in pure C#
  (no Unity types), styled after reference sprites the user supplied: shaded
  parts with six hue-shifted tones, navy and slate steel with pale blue
  highlights, furniture from the weapon's accent colour (warm colours get
  wood grain), rounded corners, curved magazines, an outline in a darker
  shade of each part's own colour, a two-pixel white border, and attachments
  (suppressor, optic, light). `WeaponSprites` caches
  them as point-filtered textures and draws them at whole-pixel scales.
- **Character sprites:** `SwatToon.shader` (Built-in pipeline, three light
  bands, cool shadow tint, top-face highlight, rim light; additive point and
  spot lights; VertexLit fallback for shadows), applied to characters and
  their guns in pixel art by `Shapes.Toonify`. Heads sit on a pivot and are
  18% larger in pixel art; eyes, goggle lenses and squad-coloured shoulder
  pads were added; guns are drawn 25% larger in pixel art.

## Limitations

- The toon shader is Built-in only; under URP characters keep the default
  lit material. Switching art style mid-mission affects characters from the
  next deployment.
- Bots are simple: they don't use cover points, equipment or doors as
  tactics, and they can't take stairs (maps with several floors aren't
  offered). The apartment map is not in the game-mode list.
- Weapon sprites are generated from a handful of shapes per category, so two
  weapons of the same category differ only by colour and attachments
  (superseded by the gun pack in Part 0c).

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings), and
  every shader (including the new toon shader, with stand-ins for Unity's
  lighting macros) passes the HLSL syntax check.
- **The gun sprites were rendered outside Unity:** `WeaponSpriteArt.cs` was
  compiled with Mono together with a small program that saves each sprite as
  an image, and the resulting sheet was inspected. That is the real output of
  the sprite code; how it looks in the HUD also depends on the UI scale.
- **Not done:** running any of it in Unity. The game modes, bots, toon
  shading, hit stop and new sounds have never been seen, heard or played.
  Expect tuning work: bot accuracy and reaction times, spawn points that land
  in awkward places on some maps, zone placement, sound levels, and how
  strong the camera kick and hit flash feel.

# Part 0a: Pixel-art style

## What was added

The default look is now pixel art: "top-down sprites, but somewhat 3D". The
scene is still the same 3D world (no art was replaced); only how it is drawn
changed. The previous look is kept as **Art style: Smooth**.

- **Low-resolution view** (`QualityManager.UpdateRenderTarget`): the 3D camera
  renders into a point-filtered render texture about 240, 320 or 420 pixels
  tall (Settings -> Graphics -> Pixel size). The texture is a whole-number
  fraction of the screen (each game pixel is k x k screen pixels) plus a
  one-pixel border, and `UIManager` draws it under the IMGUI interface, which
  stays at full resolution.
- **Orthographic, pixel-snapped camera** (`CameraController.ApplyProjection`,
  `SnapToPixels`): the same pitch and follow behaviour, with an orthographic
  size matched to the old framing. The camera position is rounded to whole
  game pixels along the view's right and up axes so static edges don't crawl
  while it moves; the leftover fraction shifts the drawn image by up to one
  game pixel (`QualityManager.ViewRect`), so motion stays smooth.
- **Screen mapping** (`QualityManager.ScreenToViewport` / `ViewportToScreen`):
  mouse aiming, ground picking, the gamepad pointer and world-anchored UI
  (prompts, markers, `UITheme.WorldToGui`) go through the shifted view rect,
  so clicks and labels still line up.
- **Post pass** (`SwatPostFX.shader` pass 2, `PostEffects`): optional dark
  outlines where depth jumps by about half a metre (uses the camera depth
  texture, enabled only when needed), slightly stronger saturation, and an
  18-level posterize with a 2x2 ordered dither. These run even when the rest of
  post-processing is off (with a neutral grade then).
- **Textures and shadows**: procedural surface textures are made at 16-32 px
  with point filtering (`ProceduralTextures`); sun shadows are hard; MSAA is
  disabled in pixel art.
- **Settings** (`SettingsUI`, `SaveData`): Art style, Pixel size and Pixel
  outlines, saved and sanitised like the other options; the Graphics summary
  shows the pixel resolution and scale.

## Limitations

- **URP:** the pixel view, camera and snapping work, but outlines and the
  dithered palette come from the Built-in pipeline's `OnRenderImage` pass, so
  they are skipped under URP (the settings label says so).
- Texture size and filter changes apply to textures made after the change;
  switching style mid-mission updates the filter immediately but texture
  resolution only on the next mission.
- Characters are still 3D figures; the "sprite" feel comes from the low
  resolution, outlines and palette, not from hand-drawn sprites.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings), and
  the post shader passes the HLSL syntax check.
- **Not done:** running it in Unity. The pixel view, snapping, outlines and
  mouse mapping have never been seen on screen. Things to check first: that
  the outline threshold isn't too strong or too weak, that the camera doesn't
  jitter when following, that mouse aim lines up exactly, and how the HQ menu
  scene frames in orthographic view.
- **Browser preview:** to show roughly what the style looks like, the ten
  built-in maps' `LevelBuilder` calls were recorded (by compiling the real map
  files against a recording stand-in for `LevelBuilder`) and rebuilt in a
  three.js web page with the same camera angle, lighting profile values,
  low-resolution render and an equivalent outline/dither pass. That page is an
  approximation made outside Unity, not a screenshot of the game, and is not
  part of the project.

# Part 0: Levels, main menu and Level Creator

## What was added

- **Ten numbered levels plus Training.** Six new maps built with the existing
  `LevelBuilder` (Brightwater Corner Mart, Seaview Motor Inn, Sterling Mutual
  Bank, Harbor Street Clinic, Club Halcyon, Riverside Steelworks) and six new
  missions on them; the four existing operations became Levels 2, 4, 6 and 9.
  `MissionData.levelNumber` drives the "LEVEL n" labels. Unlocks ramp from 0
  to 8 completed levels; a level stays available once it has been completed
  (so older saves never lose access), and training and custom levels are
  always open. Each new map has room-tagged spawn pools, cameras, consoles,
  alarm panels, evidence spots and escape routes like the originals. New room
  kinds (medical, club, vault, shop floor) give the new interiors their own
  lighting, floors and sound.
- **Main menu hub** (`MainMenuController`): Play/Continue to the next level,
  Level Select, Training, Level Creator, Officers, Equipment, Settings,
  Credits, Quit, a ten-segment campaign bar and a "next up" card. (A main menu
  already existed; it was reworked around the level campaign.)
- **Level Select** (`MissionSelectionUI`): a two-column grid that fits Training
  plus ten levels, thumbnails for every map, status and best score, details
  panel, and a shortcut into the Level Creator.
- **Level Creator** (`LevelEditorUI` plus `Scripts/LevelEditor/`):
  - Grid editor (1 m cells, map sizes 24x18 to 64x48) with tools for rooms (15
    types), doors (5 types), people (team start, 6 suspect kinds, 4 civilian
    kinds), objectives (extraction, safe zone, evidence, console, camera,
    alarm panel) and 10 kinds of props; select/move, erase, rotate, undo (40
    steps), pan and zoom.
  - Walls are generated from the rooms (`CustomLevelGeometry`) wherever a room
    meets another room or the outside; the editor draws exactly the segments
    the game builds.
  - Validation (`CustomLevelValidator`) blocks playing until the level has a
    team start outside the building with space for the van and squad, at
    least one room, something to do, every room connected to the outside
    through doors, at most one leader, and sensible counts; it also warns
    about props near doors, missing consoles for electronic doors, etc.
  - `CustomLevelBuilder` builds the level with the same `LevelBuilder` as the
    built-in maps (so lighting, dressing, NavMesh, AI, tactical map and
    minimap all work the same) and generates a `MissionData` with objectives
    from what was placed.
  - `CustomLevelStore` saves levels as JSON in
    `<persistentDataPath>/CustomLevels/` (written to a temp file, then moved),
    lists, copies and deletes them, and creates an example level on first use.
  - Play goes through the normal briefing, officer selection and loadout
    screens; the briefing, pause menu and debriefing lead back to the creator.
    Custom levels record a best score but don't count toward campaign unlocks.

## Testing performed for this part

- All scripts compile in the three configurations (0 errors, 0 warnings).
- **Unit tests for the Level Creator's logic**, compiled with `mcs` and run
  with Mono against small stand-ins for the Unity types it uses: the example
  level validates; each of its doors fits a wall and lands in exactly one
  wall segment; the generated segments cover every wall edge exactly once;
  doors are rejected at corners, at junctions and in open ground and accepted
  on straight walls; unreachable rooms are reported until doors connect them;
  a team start whose van path runs into the building is reported. All passed.
- **A script check of the five hand-written new maps** (the motel is generated
  in loops and was checked by hand): every suspect and civilian spawn point
  lies inside the room it is tagged with, evidence and consoles are inside
  rooms, and no rooms overlap. No problems found.
- Not done: running any of this in Unity. The new maps and the Level Creator
  have never been seen or played; expect layout, balance and UI issues that
  only show in Play Mode (props placed awkwardly, AI getting stuck on new
  furniture, editor controls that need tuning).

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
| Existing missions remain playable | Mission code unchanged except fixes and new level numbers/unlock thresholds; **not played**. |
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
- **Camera:** 55 degree angled follow camera, wheel zoom with min/max, three zoom presets (V; Y since Part 0i), adjustable zoom sensitivity and look-ahead, map bounds, screen shake; showcase camera for menus.
- **Officers:** six fictional officers with name, callsign, role, portrait (drawn), 3D preview in HQ, stats (health, armor, speed, accuracy, reaction, perception, command responsiveness, capacity), personality, role ability, preferred kit, unlock rules, uniform colors.
- **Squad:** up to three AI officers; command wheel (hold Z, mouse or number keys) with 10 general orders and door orders (Stack up, Open/Breach/Flash & clear); individual (F1-F3) or group (F4) selection; follow formation, hold, regroup, move with waypoint queue, cover, stack and coordinated entry, stay behind (hold fire), return, assist civilians (treat, free, escort including down stairs), wait; stuck recovery; shout-then-shoot rules of engagement; reload and sidearm fallback; medic revives; restrain surrendered suspects; radio subtitles; labels and waypoint lines in the world; status on HUD.
- **Weapons:** seven primaries (compact SMG, SMG, compact rifle, service rifle with auto/semi, shotgun with pellet spread, precision carbine, less-lethal launcher) and three sidearms; hitscan, magazines and reserves, reload, switching, semi/auto, spread and recoil bloom, muzzle flash, impacts, tracer, dry fire, role restrictions; block models with a `modelPrefab` override slot.
- **Attachments:** weapon light, red dot, suppressor, two stocks, with small stat effects, configured as data.
- **Armor:** light, standard and heavy (reduction, speed, capacity, durability, helmet, color); ballistic shield with frontal protection, bracing and cover for teammates behind.
- **Equipment:** flashbang (radius, duration, line of sight, flash and sound), smoke (cheap sphere puffs that block AI sight and expire), breaching charge (designated doors only), door wedge (place/remove), medical kit (heal over time, short animation), portable light, recon camera (text panel, partial view); capacity-limited per officer with role discounts.
- **Suspects:** unarmed, armed hostile, guard, nervous, armored, leader (+ training dummy); state machine Idle, Patrol, Suspicious, Investigating, Alert, Chasing, Attacking, TakingCover, Searching, Fleeing, Hiding, Stunned, Surrendering, Restrained, Dead; hearing, sight with FOV/darkness/smoke/crouch, last known position, search, callouts, alarms, cover, surrender, leader escape, guards raising the alarm, questioning unarmed suspects reveals others.
- **Civilians:** office worker, security guard, visitor, injured, hiding, hostage, resident; idle, wander, panic, flee, hide, follow, wait, injured, captive, evacuated; tracked encountered/rescued/evacuated/injured/killed.
- **Doors and rooms:** Closed, Open, Locked, Wedged, Breached, Disabled; open/close, pick lock, unlock from console, kick, breach, wedge; status shown in the prompt; rooms Undiscovered -> Discovered -> Investigated -> Secured -> Complete, updated a few times per second (no physics triggers).
- **Maps:** Office Complex, Warehouse, Apartment Building (ground floor, second floor and roof linked by stairs), Training Ground, HQ with parked van; van arrival sequence on deployment. (Six more maps were added with the ten-level update.)
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
- **Bank map:** added later as Level 5 (Sterling Mutual Bank).
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

(Later, the owner built the game and played it, LAN matches with friends
included. The first build drew the world pink because Unity left the lit
shader out of the build; `Editor/BuildShaders.cs` now adds the needed shaders
to Always Included Shaders, and `Shapes` falls back to a shader that is
present.)

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
