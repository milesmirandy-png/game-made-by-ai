# SWAT: Breach & Clear

A first-person SWAT shooter made in Unity. You're a SWAT officer sent into a
building taken over by armed suspects. Breach the front door, clear every room,
rescue the hostages and arrest (or take down) the suspects.

Every mission builds a new random building: a warehouse, apartment building or
office. Each mission you beat makes the next one bigger, with more suspects who
are better shots.

The whole game is made from code. There are no models, textures, sounds or
scenes to set up. The buildings, people, guns and sound effects are all
created when you press Play.

## How to play it

You need **Unity 2022.3 or newer** (Unity 6 recommended) from
[Unity Hub](https://unity.com/download).

**Option A: open this folder as a project**

1. Download this repository (on GitHub: **Code → Download ZIP**, then unzip it).
2. In Unity Hub, click **Add → Add project from disk** and pick the unzipped folder.
3. Open it. If Hub asks which editor version to use, any 2022.3+ or Unity 6 version is fine.
4. When the editor opens, press **Play** ▶.

**Option B: put it in a project you already have**

1. In Unity Hub, create a new project with the **Universal 3D** or **3D (Built-In)** template.
2. Copy the `Assets/SWAT` folder from this repository into your project's `Assets` folder.
3. Press **Play** ▶ in the default scene.

The game starts itself in whatever scene is open, so you don't need to
drag anything into the scene. An empty scene works best.

Click inside the Game view so it captures the mouse.

## Controls

| Key | Action |
| --- | --- |
| **W A S D** | Move |
| **Mouse** | Look |
| **Left click** | Shoot |
| **Right click** | Aim down the sight |
| **Shift** | Sprint |
| **R** | Reload |
| **E** | Breach the door / restrain a suspect / rescue a hostage |
| **F** | Shout "Police! Drop your weapon!" |
| **G** | Throw a flashbang |
| **Esc** | Pause |

## How it works

- **Breach the door** with **E**. It's loud, so nearby suspects will turn
  towards the door.
- **Flashbangs** (**G**) stun every suspect who can see the blast for a few
  seconds. Don't look at it yourself.
- **Shout** (**F**) to make suspects surrender. Suspects who haven't spotted
  you yet, are stunned, or are wounded give up much more often. When a suspect
  puts their hands up, walk over and press **E** to restrain them.
- **Hostages** kneel with their hands up. Walk up to them and press **E** to
  rescue them.
- **Head shots** take a suspect down in one hit.
- The mission is complete when every suspect is restrained or down and every
  hostage is rescued.
- **Don't shoot hostages** (instant mission fail) or suspects who have
  surrendered (big penalty).

At the end of each mission you get a score and a rating from S to F. Arrests
earn more points than kills, and you also get points for health left and
finishing quickly.

## Changing things

All the code is in `Assets/SWAT/Scripts`:

| File | What it does |
| --- | --- |
| `GameManager.cs` | Starts the game, mission flow, scoring, HUD and menus |
| `LevelGenerator.cs` | Builds the random buildings, furniture, vehicles and street |
| `PlayerController.cs` | Movement, mouse look, health, shout and flashbang |
| `Weapon.cs` | The rifle: damage, fire rate, recoil, reloading, aiming |
| `Suspect.cs` | Suspect AI: spotting you, shooting, surrendering |
| `Hostage.cs` | Hostages |
| `Flashbang.cs`, `BreachDoor.cs` | Flashbangs and the front door |
| `Sfx.cs` | Sound effects (generated from code) |
| `GameInput.cs` | Keyboard and mouse (works with both of Unity's input systems) |

Some easy things to try:

- Mouse too fast or slow? Change `lookSensitivity` in `PlayerController.cs`.
- Want a bigger magazine or more damage? Edit `magazineSize` and `damage` in `Weapon.cs`.
- Suspects too good? Lower `accuracy` in `LevelGenerator.Populate`.
