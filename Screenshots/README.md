# Screenshots

**No screenshots have been captured yet.** The game was written and
compile-checked without access to a running Unity editor, so there was no way
to take real Play Mode screenshots. Nothing in this folder is concept art or a
mockup; it stays empty until someone captures the real game.

This folder (`<project>/Screenshots/`, next to `Assets/`) is the project's
screenshot directory. It sits outside `Assets/` on purpose, so Unity doesn't
import every capture as a texture.

## How to capture them

1. Open the project in Unity 6 and press **Play** in `Assets/Scenes/Boot.unity`
   (see the main README). Set the Game view to a 16:9 resolution such as
   1920x1080 (Game view toolbar -> aspect dropdown).
2. For the nicest shots use **Settings -> Graphics -> High** or **Ultra**,
   keep Post-processing and Ambient occlusion on, and turn the FPS counter off
   (F10). Hide the minimap in **Settings -> Gameplay** if it covers something.
3. Press **F12** at each moment below. The file is saved here, named
   `SWAT_<date>_<time>_<state>.png`. (The "Screenshot saved" message is
   delayed so it doesn't appear in the image.) In a built game, screenshots go
   to `<persistentDataPath>/Screenshots/`.
4. Any OS screen capture of the Game view also works.

The game now starts in the **pixel-art** style (Settings -> Graphics -> Art
style). Capture the required shots in that style; a few extra shots in
**Smooth** are useful for comparison. Note that the browser preview of the
pixel style is not a screenshot and doesn't belong in this folder.

The **Lighting** selector on the briefing screen (Mission default / Day /
Evening / Night) lets you pick the time of day for any mission.

| # | Required shot | How to get there |
| --- | --- | --- |
| 1 | Main menu | Press Play and wait on the title screen. |
| 2 | Officer selection | Main menu -> **Officer Roster** (or Briefing -> *Select officers*). Click an officer card so the details and 3D preview show. |
| 3 | Loadout screen | Main menu -> **Equipment** (or Officers -> *Loadout*). Hover a weapon to show its stats. |
| 4 | Exterior during daytime | **New Mission** (Operation Glass Desk, Day) -> deploy -> after the van arrives, stand in the parking lot facing the entrance. |
| 5 | Exterior at night | Operation Lantern or Operation Cold Storage (Night by default), or set **Lighting: Night** on any briefing. Capture outside near the street lamps. |
| 6 | Bright office interior | Operation Glass Desk (Day): walk into the open-plan office or a private office. |
| 7 | Dark hallway | Operation Lantern with a power cut (reroll the seed on the briefing until the intel says *POWER OUT*), then find a hallway lit only by emergency lights; or any night mission with the flashlight on. |
| 8 | Security room | Office Complex: find the security room (monitor wall and console). |
| 9 | Player with squadmates | Deploy with three squadmates and capture them following you; their numbered ID markers should be visible. |
| 10 | Door entry | Hold **Z** on a closed door -> **Breach & clear** or **Open & clear**; capture as the team enters. |
| 11 | Tactical map | Press **Space** (planning mode) or **Tab** (overlay) in a mission; select an officer and right-click to show a waypoint. |
| 12 | Civilian rescue | Find a civilian, press **E** (*Rescue Civilian*), and capture while they follow you toward the green safe zone. |
| 13 | Mission debriefing | Finish or fail a mission (training is quickest) and capture the debriefing once the result stamp has faded into the report. |
| 14 | Lighting and materials showcase | A representative interior with light pools, contact shading and textured floors, e.g. the Glass Desk lobby (Day) or the warehouse floor at night with the flashlight on. |

Also worth capturing (not in the required list):

- **Level Select** with the ten-level grid (main menu -> Level Select).
- **Level Creator** with the example level open (main menu -> Level Creator).
- A custom level being played (Level Creator -> Play level).
- One of the new levels, e.g. Level 5 (bank) or Level 8 (nightclub).
