# Model converter

Turns the imported low-poly models in `SourceModels/` into the small mesh
files the game reads at run time (`Assets/Resources/SWAT/Models/*.bytes`,
loaded by `Assets/Scripts/Core/ModelLibrary.cs`). Unity never imports the
originals (they're outside `Assets/`), so no model importer, package or
material setup is involved, and the models get the game's own lit and
pixel-art materials.

```
python3 Tools/ModelConverter/convert.py SourceModels Assets/Resources/SWAT/Models previews
```

Needs Python 3 and ImageMagick (`convert` and `montage`, used to read
embedded textures and to draw the preview images). No other packages.

What it does:

- reads binary FBX (`fbx.py`) and GLB (`meshes.py`) files into triangle lists,
  turning textures into a flat colour per triangle;
- turns each model to face +Z with Y up, at real size, in Unity's left-handed
  axes, and makes sure the triangles face outward;
- **soldier**: splits it into torso, head, upper arms, forearms and legs, with
  pivots at the neck, shoulders, elbows and hips, plus hand points. The game's
  procedural animation moves these, and puts the hands on the gun;
- **police shield**: stands it upright, 1.05 m tall, handles on the back;
- **guns**: points the barrel along +Z, scales each to a set length, puts the
  origin at the grip and records the muzzle point. The table at the top of
  the gun section says which file becomes which model, how long it is, and
  which ones need their direction flipped (the automatic guess gets two
  wrong). `Assets/Scripts/Weapons/WeaponModels.cs` says which weapon uses
  which model;
- writes preview images (`render.py`, a small software renderer) so the
  results can be checked by eye. `ik_check.py` poses the soldier holding a
  gun the way the game does.

The file format is described at the top of `swm.py` and `ModelLibrary.cs`.
