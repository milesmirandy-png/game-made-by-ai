Editable game data goes here.

The built-in weapons, attachments, armor, equipment, officers, suspect types
and missions are defined in `Scripts/Core/DefaultContent.cs`. Use the menu
**SWAT -> Create Editable Data Assets** to export them as ScriptableObject
assets into:

- `Weapons/Resources/SWAT/Weapons` and `Weapons/Resources/SWAT/Attachments`
- `Equipment/Resources/SWAT/Equipment` and `Equipment/Resources/SWAT/Armor`
- `Officers/Resources/SWAT/Officers`
- `Enemies/Resources/SWAT/Enemies`
- `Missions/Resources/SWAT/Missions` (objectives, enemy and civilian groups are edited inside each mission)

`GameData` loads these at startup and uses them instead of the built-in
version with the same `id`. Delete an asset to go back to the default. New
assets (new ids) are added; create them with **Assets -> Create -> SWAT -> ...**.
