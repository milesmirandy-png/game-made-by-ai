# Credits

## Weapon icons

The weapon icons in the loadout, HUD and weapon wheel are side-view pixel-art
guns from a **free-to-use pixel-art sprite pack** that the project owner
supplied and confirmed is free to use. They are shown pixel for pixel; the only
changes are:

- each gun was cut out of the pack's sheet onto a transparent background, and
  the pale edge pixels left over from the sheet's white background were
  removed so the guns sit cleanly on dark panels;
- the suppressor, red dot and weapon light that appear when those attachments
  are fitted are drawn on top by the game in the pack's colours
  (`Assets/Scripts/Weapons/WeaponSpritePack.cs`).

Files: `Assets/Resources/SWAT/WeaponSprites/<weapon id>.bytes` (PNG images; the
`.bytes` extension makes Unity load them as raw data so they stay pixel-exact).

Pack name, author and link: _to be filled in by the project owner._

## 3D models

Supplied by the project owner (downloaded as free low-poly models; the zip
names follow Poly Pizza's "<model> by <author>" pattern). Free low-poly
models like these are usually CC0 or CC-BY; CC-BY needs this credit. Check
the license on each model's page:

- **Police Shield** by **CreativeTrio**: the riot shield carried by shield
  officers (recoloured police black, with a white band and a blue viewport
  added).
- **Low Poly Soldier Character** ("Soldier") by **Umair Yaqub**: the officers,
  the game-mode teams and armoured suspects. It's split at the neck, shoulders,
  elbows and hips so the game's animation can move it, and recoloured for each
  officer and team.
- **Low Poly Weapons Pack** (author not named in the download): the guns in
  characters' hands. These are the AK, M4-style and bullpup rifles, two sniper
  rifles, five SMGs, the pump and double-barrel shotguns, three pistols and two
  revolvers. The ammo boxes in the pack aren't used.

The originals are in `SourceModels/`. `Tools/ModelConverter/` converts them to
the game's small mesh files in `Assets/Resources/SWAT/Models/`. The converter
fixes their orientation and scale, splits the soldier into parts and turns
textures into flat colours. How to run it is in `Tools/ModelConverter/README.md`.

Model source links: _to be filled in by the project owner._

## Everything else

Code, level layouts, the other (procedural) 3D models, procedural textures and
procedural audio were made for this project. All agencies, places, companies and people
are fictional.
