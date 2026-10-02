# Kill Popups: a Ravenfield mutator

Popups when you get kills, like in Call of Duty and Halo. Get a headshot and
**HEADSHOT +50** pops up in the middle of your screen with a sound. Throw a
grenade into a group and you'll see **GRENADE KILL**, **DOUBLE KILL**,
**TRIPLE KILL**, **OVERKILL** and **SQUAD KILL**, with a points counter that
adds them all up.

## The popups

| Popup | How to get it | Points |
| --- | --- | --- |
| **KILLED (name)** | Any kill (small line) | +100 |
| **HEADSHOT** | Kill someone with a shot to the head | +50 |
| **GRENADE KILL** | Kill someone with a grenade | +50 |
| **EXPLOSIVE KILL** | Kill someone with a rocket, C4, tank shell or other explosion | +25 |
| **MELEE KILL** | Kill someone with a melee weapon | +75 |
| **ROADKILL** | Run someone over | +50 |
| **LONGSHOT** | Shoot someone from 150 m or more (shows the distance) | +50 |
| **POINT BLANK** | Kill someone from less than 3 m | +25 |
| **COLLATERAL** | Kill two people with one shot | +75 |
| **DOUBLE KILL** | 2 kills, each less than 4 seconds after the last | +50 |
| **TRIPLE KILL** | 3 kills in a row like that | +100 |
| **OVERKILL** | 4 kills in a row like that | +150 |
| **KILLTACULAR**, **KILLTROCITY**, **KILLIMANJARO**, **KILLTASTROPHE**, **KILLPOCALYPSE**, **KILLIONAIRE** | 5, 6, 7, 8, 9 and 10+ kills in a row like that | +50 more each time |
| **SQUAD KILL** | Wipe out a whole enemy squad (2+ soldiers). You need the last kill and at least half the kills | +250 |
| **KILLING SPREE**, **KILLING FRENZY**, **RUNNING RIOT**, **RAMPAGE**, **UNTOUCHABLE**, **INVINCIBLE**, **INCONCEIVABLE**, **UNFRIGGENBELIEVABLE** | 5, 10, 15, 20, 25, 30, 40 and 50 kills without dying | 20 × kills |
| **FIRST BLOOD** | Get the first kill of the match | +100 |
| **REVENGE** | Kill the enemy who last killed you | +75 |
| **FROM THE GRAVE** | Get a kill after you've died (your grenade was still going) | +75 |
| **DESTROYED (vehicle)** | Destroy an enemy vehicle | +150 |
| **TEAM KILL** | Kill someone on your own team | -100 |
| **SUICIDE** | Kill yourself | 0 |

The most important popup shows big in the middle of the screen. Every popup
also gets a small line underneath. A grenade that kills four people only
plays one sound (the best one), not eight.

## What's in this folder

| File | What it is |
| --- | --- |
| `KillPopups/KillPopups.txt` | The mutator's Ravenscript (Lua) code |
| `KillPopups/Editor/KillPopupsBuilder.cs` | Adds a Unity menu that builds the mutator prefab for you |
| `KillPopups/Sounds/*.wav` | The sound effects |
| `Tools/make_sounds.py` | Makes the sound effects (only needed if you want to change them) |

## How to make the mod

You need:

- **Ravenfield** on Steam.
- The **Ravenfield Tools Pack** (the official modding tools) from
  [ravenfieldgame.com/modding.html](https://ravenfieldgame.com/modding.html).
- The **Unity** version the Tools Pack asks for (Unity 2020.3 for current
  versions of the game). Get it from [Unity Hub](https://unity.com/download)
  or the Unity download archive.

Follow the Tools Pack's own instructions to open the tools project in Unity.
Then:

1. **Copy the `KillPopups` folder** (the one with `KillPopups.txt` in it)
   into the tools project's `Assets` folder. You can drag it straight into
   Unity's Project window.

2. **Build the prefab.** In Unity's top menu, click
   **Tools → Kill Popups → Build Mutator Prefab**. This makes
   `Assets/KillPopups/KillPopups.prefab` with the popup text and sounds in it,
   and hooks up the script.

   If it shows a message saying to do one thing by hand, select
   `KillPopups.prefab`, make sure it has a **ScriptedBehaviour** component
   (**Add Component → ScriptedBehaviour** if it doesn't) and drag
   `KillPopups.txt` into its **Source** slot.

3. **Make a Content Mod.** Find the example Content Mod prefab in the
   `Assets/Content` folder, select it, press **Ctrl+D** to duplicate it, rename
   the copy to `KillPopupsMod` and move it into your `KillPopups` folder.

4. **Add the mutator.** Select `KillPopupsMod`. In the Inspector, find the
   **Mutators** list and add one entry:
   - **Name:** `Kill Popups`
   - **Description:** `Popups for headshots, multi-kills, squad kills and more.`
   - **Mutator Prefab:** drag `KillPopups.prefab` here.

   If the example mod already has weapons or other things in it, remove those
   so your mod only has the mutator.

5. **Export the mod** using the Tools Pack's export (the same way as for any
   content mod). You get a `.rfc` file.

6. **Install it.** Put the exported mod folder (with the `.rfc` file in it)
   into a folder called `Mods` inside the game's `ravenfield_Data` folder. On
   Steam: right-click Ravenfield → **Manage → Browse local files** to find it.
   You can also upload it to the Steam Workshop from the game's mod menu.

7. **Play.** In Ravenfield, go to **Instant Action**, open the **Mutators**
   list, turn on **Kill Popups** and start a match.

## Changing things

Open `KillPopups.txt` in any text editor. Everything you might want to change
is at the top:

- **`SETTINGS`**: the multi-kill time (4 seconds), how far a LONGSHOT is
  (150 m), how long popups stay on screen, and turning points, sounds or the
  "KILLED (name)" lines on or off.
- **`MEDALS`**: the words, points and colors of every popup. Want it to say
  `GRENADE SHOT!` instead of `GRENADE KILL`? Change the `text` there.
- **`MULTIKILLS`** and **`STREAKS`**: the multi-kill and kill streak names.

To move the popups or change the font size, open `KillPopups.prefab` and move
or edit the `Headline`, `Score` and `Feed/Line1`–`Line5` texts. Keep their
names, because the script finds them by name.

To use your own sounds, replace the files in `KillPopups/Sounds` with your own
(keep the names) and build the prefab again. An announcer voice saying
"HEADSHOT!" works great.

After changing anything, export the mod again.

## If something doesn't work

- **"DOUBLE KILL" and "+250" are stuck on the screen:** the script isn't
  running. Check that the prefab's ScriptedBehaviour has `KillPopups.txt` in
  its Source slot.
- **No popups at all:** make sure the mutator is turned on in the Mutators
  list before you start the match. Script errors appear in the game's
  Ravenscript console, starting with `Kill Popups`.
- **Popups show as plain text in the middle of the screen:** the script
  couldn't find the `Headline` text in the prefab, so it's using the game's
  built-in message instead. Build the prefab again with the menu in step 2.
- **Grenades say EXPLOSIVE KILL:** grenade kills are spotted by the weapon's
  name (`grenade`, `frag`, `nade`...) or by being in the small gear slot. If a
  modded grenade isn't spotted, add a word from its name to `GRENADE_WORDS`
  in the script.
