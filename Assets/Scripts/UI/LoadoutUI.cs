using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // Loadout for each deploying officer: primary, sidearm, attachments, armor,
    // shield, tactical equipment (limited by capacity) and looks (uniform,
    // headgear, face, patch). Changes are validated and saved immediately.
    // The officer is previewed in 3D in the HQ armory.
    public class LoadoutUI
    {
        static readonly AttachmentSlot[] Slots = { AttachmentSlot.Optic, AttachmentSlot.Muzzle, AttachmentSlot.Underbarrel, AttachmentSlot.Light, AttachmentSlot.Magazine, AttachmentSlot.Stock };
        int tab, page;   // page: 0 weapons, 1 armor and equipment, 2 looks
        WeaponData hovered;

        public void Draw(GameManager game)
        {
            float w = UITheme.Width, h = UITheme.Height;
            UITheme.Fill(new Rect(0f, 0f, 760f, h), new Color(0.02f, 0.03f, 0.05f, 0.82f));
            UITheme.Fill(new Rect(w - 620f, 0f, 620f, h), new Color(0.02f, 0.03f, 0.05f, 0.82f));
            UITheme.Header(new Rect(60f, 36f, 680f, 60f), "Loadout", game.BrowseMode ? "Equipment for every available officer" : "Equip your team");

            var team = Team(game);
            if (team.Count == 0) return;
            tab = Mathf.Clamp(tab, 0, team.Count - 1);
            float tx = 60f;
            for (int i = 0; i < team.Count; i++)
            {
                float tw = game.BrowseMode ? 108f : 160f;
                string label = game.BrowseMode ? team[i].callsign : (i == 0 ? "YOU: " : "") + team[i].callsign;
                if (UITheme.Button(new Rect(tx, 110f, tw - 6f, 40f), label, true, i == tab, 16)) tab = i;
                tx += tw;
            }

            var officer = team[tab];
            var loadout = GameData.LoadoutFor(officer);
            hovered = null;
            bool changed = DrawLeft(new Rect(60f, 160f, 680f, h - 260f), officer, loadout);
            changed |= DrawRight(new Rect(w - 590f, 36f, 540f, h - 136f), officer, loadout);
            if (changed)
            {
                LoadoutRules.Validate(officer, loadout);
                SaveManager.Save();
            }
            CharacterPreview.Show(officer, loadout, game.Headquarters.armoryTarget + new Vector3(0f, -0.5f, 0.6f), 180f);

            if (game.BrowseMode)
            {
                if (UITheme.Button(new Rect(60f, h - 90f, 200f, 50f), "< Main menu")) game.GoToMainMenu();
                if (UITheme.Button(new Rect(280f, h - 90f, 200f, 50f), "Roster  >")) game.OpenOfficerSelection(true);
            }
            else
            {
                if (UITheme.Button(new Rect(60f, h - 90f, 220f, 50f), "< Officers")) game.OpenOfficerSelection(false);
                string warning = TeamWarning(team);
                if (!string.IsNullOrEmpty(warning)) UITheme.Text(new Rect(300f, h - 84f, w - 700f, 40f), warning, 15, UITheme.Warn, TextAnchor.MiddleLeft);
                var mission = OfficerSelectionManager.Mission;
                bool versus = mission != null && mission.IsVersus;
                if (versus && UITheme.Button(new Rect(w - 720f, h - 90f, 320f, 50f), "< Match setup", true, false, 18)) game.OpenVersusSetup();
                if (UITheme.Button(new Rect(w - 380f, h - 90f, 320f, 50f), versus ? "START MATCH  >" : "DEPLOY  >", true, true, 22)) game.Deploy();
            }
        }

        static List<OfficerData> Team(GameManager game)
        {
            var list = new List<OfficerData>();
            if (game.BrowseMode)
            {
                foreach (var officer in GameData.AllOfficers) if (Progression.IsAvailable(officer)) list.Add(officer);
                return list;
            }
            list.Add(OfficerSelectionManager.Leader);
            list.AddRange(OfficerSelectionManager.Squad);
            return list;
        }

        // Soft mission checks (nothing here blocks deployment; every mission is completable).
        static string TeamWarning(List<OfficerData> team)
        {
            var current = OfficerSelectionManager.Mission;
            if (current != null && current.IsVersus) return null;
            int charges = 0, kits = 0;
            foreach (var officer in team)
            {
                var loadout = GameData.LoadoutFor(officer);
                charges += loadout.CountOf("breach_charge");
                kits += loadout.CountOf("medkit");
            }
            var mission = OfficerSelectionManager.Mission;
            if (mission == null) return null;
            if (charges == 0 && mission.randomLockChance > 0f) return "No breaching charges in the team: locked doors will need lockpicking or a Breacher's kick.";
            if (kits == 0 && !mission.isTraining) return "No medical kits in the team.";
            return null;
        }

        bool DrawLeft(Rect rect, OfficerData officer, OfficerLoadout loadout)
        {
            UITheme.Panel(rect);
            bool changed = false;
            float x = rect.x + 20f, cw = rect.width - 40f, y = rect.y + 14f;
            UITheme.Text(new Rect(x, y, cw * 0.5f, 24f), officer.displayName + "  -  " + UITheme.RoleName(officer.role), 18, UITheme.TextColor, TextAnchor.UpperLeft, true);
            // The arsenal no longer fits on one page with the gear, so weapons, gear and looks have their own tabs.
            float pw = 120f;
            if (UITheme.Button(new Rect(rect.xMax - 20f - pw * 3f - 12f, y - 4f, pw, 32f), "Weapons", true, page == 0, 15)) page = 0;
            if (UITheme.Button(new Rect(rect.xMax - 20f - pw * 2f - 6f, y - 4f, pw, 32f), "Armor & gear", true, page == 1, 15)) page = 1;
            if (UITheme.Button(new Rect(rect.xMax - 20f - pw, y - 4f, pw, 32f), "Look", true, page == 2, 15)) page = 2;
            y += 36f;
            if (page == 2) return DrawLooks(ref y, x, cw, rect, loadout);
            if (page == 0)
            {
                Section(ref y, x, cw, loadout.useShield ? "PRIMARY WEAPON (not usable with the shield)" : "PRIMARY WEAPON");
                var primaries = new List<WeaponData>();
                var sidearms = new List<WeaponData>();
                // Game-mode-only weapons are offered only when preparing a game-mode match.
                var forMission = OfficerSelectionManager.Mission;
                bool versus = forMission != null && forMission.IsVersus;
                foreach (var weapon in GameData.AllWeapons)
                    if (!weapon.versusOnly || versus || GameManager.Instance.BrowseMode) (weapon.isSidearm ? sidearms : primaries).Add(weapon);
                changed |= WeaponGrid(ref y, x, cw, primaries, officer, loadout, true);
                Section(ref y, x, cw, "SIDEARM");
                changed |= WeaponGrid(ref y, x, cw, sidearms, officer, loadout, false);
                return changed;
            }

            Section(ref y, x, cw, "ARMOR");
            var armors = GameData.AllArmor;
            float aw = (cw - 8f * (armors.Count - 1)) / Mathf.Max(1, armors.Count);
            for (int i = 0; i < armors.Count; i++)
            {
                var armor = armors[i];
                bool available = Progression.IsAvailable(armor);
                var r = new Rect(x + i * (aw + 8f), y, aw, 40f);
                if (UITheme.Button(r, armor.displayName, available, loadout.armorId == armor.id, 15))
                {
                    loadout.armorId = armor.id;
                    changed = true;
                }
            }
            y += 46f;
            var current = GameData.Armor(loadout.armorId);
            if (current != null)
                UITheme.Text(new Rect(x, y, cw, 20f), "Protection " + Mathf.RoundToInt(current.damageReduction * 100f) + "%   Speed " + Mathf.RoundToInt(current.speedMultiplier * 100f) + "%   Capacity "
                    + (current.capacityBonus >= 0 ? "+" : "") + current.capacityBonus, 14, UITheme.Dim);
            if (current != null) UITheme.Text(new Rect(x, y + 20f, cw, 20f), current.description, 13, UITheme.Faint);
            y += 20f;
            y += 24f;
            if (officer.role == OfficerRole.Shield)
            {
                bool shield = UITheme.Toggle(new Rect(x, y, cw, 28f), "Carry ballistic shield (sidearm only, slower, strong frontal protection, -2 capacity)", loadout.useShield);
                if (shield != loadout.useShield)
                {
                    loadout.useShield = shield;
                    changed = true;
                }
                y += 32f;
            }

            // Equipment with capacity.
            int capacity = LoadoutRules.Capacity(officer, loadout);
            int used = LoadoutRules.Used(officer, loadout);
            Section(ref y, x, cw, "TACTICAL EQUIPMENT   " + used + " / " + capacity + " capacity");
            UITheme.Bar(new Rect(x, y - 4f, cw, 5f), capacity > 0 ? used / (float)capacity : 0f, used > capacity ? UITheme.Bad : UITheme.Accent);
            y += 6f;
            foreach (var item in GameData.AllEquipment)
            {
                bool available = Progression.IsAvailable(item);
                int count = loadout.CountOf(item.id);
                var row = new Rect(x, y, cw, 32f);
                UIIcons.Equipment(new Rect(row.x, row.y, 32f, 32f), item.kind, available ? UITheme.TextColor : UITheme.Faint);
                int cost = LoadoutRules.Cost(officer, item);
                UITheme.Text(new Rect(row.x + 40f, row.y, cw * 0.55f, 32f), item.displayName + "  <size=13><color=#8a96a8>cost " + cost + (item.consumable ? "" : ", reusable") + "</color></size>", 16, available ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleLeft);
                if (UITheme.Hover(row)) UITheme.Text(new Rect(x, rect.yMax - 46f, cw, 40f), item.description, 14, UITheme.Dim);
                if (UITheme.Button(new Rect(row.xMax - 120f, row.y + 2f, 34f, 28f), "-", count > 0, false, 18))
                {
                    loadout.SetCount(item.id, count - 1);
                    changed = true;
                }
                UITheme.Text(new Rect(row.xMax - 82f, row.y, 44f, 32f), count.ToString(), 18, count > 0 ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleCenter, true);
                if (UITheme.Button(new Rect(row.xMax - 34f, row.y + 2f, 34f, 28f), "+", available && LoadoutRules.CanAdd(officer, loadout, item), false, 18))
                {
                    loadout.SetCount(item.id, count + 1);
                    changed = true;
                }
                y += 34f;
            }
            return changed;
        }

        bool WeaponGrid(ref float y, float x, float cw, List<WeaponData> weapons, OfficerData officer, OfficerLoadout loadout, bool primary)
        {
            bool changed = false;
            float bw = (cw - 8f) * 0.5f;
            for (int i = 0; i < weapons.Count; i++)
            {
                var weapon = weapons[i];
                bool unlocked = Progression.IsAvailable(weapon);
                bool allowed = weapon.AllowedFor(officer.role);
                var r = new Rect(x + (i % 2) * (bw + 8f), y + (i / 2) * 42f, bw, 38f);
                bool selected = primary ? loadout.primaryId == weapon.id : loadout.sidearmId == weapon.id;
                string label = weapon.displayName + (!unlocked ? "  (locked)" : !allowed ? "  (role)" : "");
                if (UITheme.Button(r, string.Empty, unlocked && allowed, selected))
                {
                    if (primary) loadout.primaryId = weapon.id;
                    else loadout.sidearmId = weapon.id;
                    changed = true;
                }
                UIIcons.Weapon(new Rect(r.x + 4f, r.y + 3f, 92f, 32f), weapon, unlocked && allowed ? UITheme.TextColor : UITheme.Faint);
                UITheme.Text(new Rect(r.x + 102f, r.y, r.width - 106f, r.height), label, 15, unlocked && allowed ? UITheme.TextColor : UITheme.Faint, TextAnchor.MiddleLeft, selected);
                if (UITheme.Hover(r)) hovered = weapon;
            }
            y += Mathf.Ceil(weapons.Count / 2f) * 42f + 6f;
            return changed;
        }

        static void Section(ref float y, float x, float w, string title)
        {
            UITheme.Text(new Rect(x, y, w, 22f), title, 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 24f;
        }

        bool DrawRight(Rect rect, OfficerData officer, OfficerLoadout loadout)
        {
            UITheme.Panel(rect);
            bool changed = false;
            float x = rect.x + 22f, cw = rect.width - 44f, y = rect.y + 18f;
            var weapon = hovered ?? GameData.Weapon(loadout.useShield ? loadout.sidearmId : loadout.primaryId);
            if (weapon != null)
            {
                UIIcons.Weapon(new Rect(x, y, 150f, 56f), weapon, UITheme.TextColor, weapon == GameData.Weapon(loadout.primaryId) ? loadout : null);
                UITheme.Text(new Rect(x + 166f, y, cw - 166f, 28f), weapon.displayName, 21, UITheme.TextColor, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(x + 166f, y + 30f, cw - 166f, 22f), Category(weapon.category) + (weapon.lessLethal ? "  |  less-lethal" : ""), 15, UITheme.Accent);
                y += 66f;
                float dh = UITheme.TextHeight(weapon.description, 14, cw);
                UITheme.Text(new Rect(x, y, cw, dh), weapon.description, 14, UITheme.Dim);
                y += dh + 8f;
                var preview = new Weapon(weapon, weapon == GameData.Weapon(loadout.primaryId) ? loadout : null);
                Stat(ref y, x, cw, "Damage", weapon.damage * Mathf.Max(1, weapon.pellets) / 120f, weapon.pellets > 1 ? weapon.pellets + " x " + weapon.damage.ToString("0") : weapon.damage.ToString("0"));
                Stat(ref y, x, cw, "Fire rate", weapon.fireRate / 15f, weapon.fireRate.ToString("0.0") + "/s");
                Stat(ref y, x, cw, "Magazine", preview.MagazineSize / 40f, preview.MagazineSize + " + " + weapon.startingReserve);
                Stat(ref y, x, cw, "Reload speed", Mathf.InverseLerp(3.5f, 1f, preview.ReloadTime), preview.ReloadTime.ToString("0.0") + "s");
                Stat(ref y, x, cw, "Range", weapon.range / 50f, weapon.range.ToString("0") + "m");
                Stat(ref y, x, cw, "Accuracy", Mathf.InverseLerp(8f, 0.5f, preview.Spread), (10f - preview.Spread).ToString("0.0"));
                Stat(ref y, x, cw, "Recoil control", Mathf.InverseLerp(2f, 0.2f, preview.Recoil), (10f - preview.Recoil * 4f).ToString("0.0"));
                Stat(ref y, x, cw, "Handling", Mathf.InverseLerp(0.85f, 1.05f, weapon.moveSpeedMultiplier * preview.MoveMultiplier), Mathf.RoundToInt(weapon.moveSpeedMultiplier * preview.MoveMultiplier * 100f) + "%");
                Stat(ref y, x, cw, "Noise", Mathf.InverseLerp(10f, 32f, preview.NoiseRadius), preview.NoiseRadius.ToString("0") + "m");
                string modeName = weapon.fireMode == FireMode.FullAuto ? "Automatic" : weapon.fireMode == FireMode.Burst ? weapon.burstCount + "-round burst" : "Semi-automatic";
                UITheme.Text(new Rect(x, y, cw, 22f), "Mode: " + modeName + (weapon.canToggleFireMode && weapon.fireMode != FireMode.SemiAuto ? " (toggle semi with " + UITheme.KeyFor(InputAction.FireMode) + ")" : ""), 15, UITheme.Dim);
                y += 30f;
            }

            UITheme.Text(new Rect(x, y, cw, 22f), "ATTACHMENTS (primary weapon)", 14, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            string hint = null;
            foreach (var slot in Slots)
            {
                var options = new List<AttachmentData> { null };
                foreach (var attachment in GameData.AllAttachments) if (attachment.slot == slot) options.Add(attachment);
                string currentId = Get(loadout, slot);
                int index = 0;
                for (int i = 1; i < options.Count; i++) if (options[i].id == currentId) index = i;
                var names = new string[options.Count];
                for (int i = 0; i < options.Count; i++)
                    names[i] = options[i] == null ? "None" : options[i].displayName + (Progression.IsAvailable(options[i]) ? "" : " (locked)");
                var row = new Rect(x, y, cw, 34f);
                int next = UITheme.Stepper(row, SlotName(slot), index, names);
                if (UITheme.Hover(row)) hint = options[index] != null ? options[index].description : "Nothing fitted.";
                if (next != index)
                {
                    // Skip locked options in the direction of travel.
                    int step = (next - index + options.Count) % options.Count == 1 ? 1 : -1;
                    int guard = 0;
                    while (options[next] != null && !Progression.IsAvailable(options[next]) && guard++ < options.Count) next = (next + step + options.Count) % options.Count;
                    Set(loadout, slot, options[next] != null ? options[next].id : null);
                    changed = true;
                }
                y += 38f;
            }
            string note = hint ?? (loadout.useShield ? "Attachments apply to the primary weapon, which stays in the van while using the shield." : "Point at an attachment to see what it does.");
            UITheme.Text(new Rect(x, y, cw, 36f), note, 13, hint != null ? UITheme.Dim : UITheme.Faint);
            return changed;
        }

        static string SlotName(AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Underbarrel: return "Grip / laser";
                default: return slot.ToString();
            }
        }

        // ---- Looks: uniform, headgear, face and a patch ----

        bool DrawLooks(ref float y, float x, float cw, Rect rect, OfficerLoadout loadout)
        {
            bool changed = false;
            Section(ref y, x, cw, "UNIFORM");
            var uniforms = new string[Progression.UniformNames.Length];
            for (int i = 0; i < uniforms.Length; i++) uniforms[i] = Progression.UniformNames[i] + (Progression.UniformAvailable(i) ? "" : " (locked)");
            int uniform = UITheme.Stepper(new Rect(x, y, cw, 34f), "Uniform", loadout.uniformIndex, uniforms);
            if (uniform != loadout.uniformIndex)
            {
                int step = (uniform - loadout.uniformIndex + uniforms.Length) % uniforms.Length == 1 ? 1 : -1;
                int guard = 0;
                while (!Progression.UniformAvailable(uniform) && guard++ < uniforms.Length) uniform = (uniform + step + uniforms.Length) % uniforms.Length;
                loadout.uniformIndex = uniform;
                changed = true;
            }
            UITheme.Fill(new Rect(x + cw * 0.38f + 40f, y + 36f, cw * 0.62f - 80f, 6f), Progression.Uniform(loadout.uniformIndex));
            y += 44f;
            int sleeves = loadout.longSleeves ? 0 : 1;
            if (Pick(ref y, x, cw, "Sleeves", ref sleeves, SleeveNames))
            {
                loadout.longSleeves = sleeves == 0;
                changed = true;
            }
            y += 4f;

            Section(ref y, x, cw, "HEAD AND FACE");
            changed |= Pick(ref y, x, cw, "Headgear", ref loadout.headgearIndex, GearCatalog.HeadgearNames);
            changed |= Pick(ref y, x, cw, "Face", ref loadout.faceIndex, GearCatalog.FaceNames);
            changed |= Pick(ref y, x, cw, "Facial hair", ref loadout.facialHairIndex, GearCatalog.FacialHairNames);
            changed |= Pick(ref y, x, cw, "Hair colour", ref loadout.hairColorIndex, GearCatalog.HairColorNames);
            y += 8f;

            Section(ref y, x, cw, "PATCH (left shoulder and chest)");
            changed |= Pick(ref y, x, cw, "Design", ref loadout.patchIndex, GearCatalog.PatchNames);
            changed |= Pick(ref y, x, cw, "Colour", ref loadout.patchColorIndex, GearCatalog.PatchColorNames);
            UITheme.Fill(new Rect(x + cw * 0.38f + 40f, y - 2f, cw * 0.62f - 80f, 6f), GearCatalog.PatchColor(loadout.patchColorIndex));
            y += 16f;

            Section(ref y, x, cw, "VEST");
            var armor = GameData.Armor(loadout.armorId);
            string vest;
            switch (GearCatalog.StyleFor(armor))
            {
                case ArmorStyle.None: vest = "No armor: barebones, just a belt and kneepads."; break;
                case ArmorStyle.Light: vest = "Light vest: a slick plate carrier, no pouches."; break;
                case ArmorStyle.Heavy: vest = "Heavy armor: plates, pouches, shoulder guards, collar and groin protector."; break;
                default: vest = "Standard plate carrier with pouches and a pack."; break;
            }
            UITheme.Text(new Rect(x, y, cw, 40f), vest + " The vest follows the armor you pick under Armor & gear.", 14, UITheme.Dim);
            y += 44f;
            UITheme.Text(new Rect(x, y, cw, 60f), "Mostly looks: protection comes from the armor. Two pieces do more: the gas mask keeps CS gas out, and the helmet with NVG gives night vision (N). The right shoulder keeps the officer's role colour so the squad stays easy to tell apart.", 13, UITheme.Faint);
            return changed;
        }

        static readonly string[] SleeveNames = { "Long", "Rolled up" };

        static bool Pick(ref float y, float x, float cw, string label, ref int value, string[] options)
        {
            int next = UITheme.Stepper(new Rect(x, y, cw, 34f), label, Mathf.Clamp(value, 0, options.Length - 1), options);
            y += 38f;
            if (next == value) return false;
            value = next;
            return true;
        }

        static void Stat(ref float y, float x, float w, string label, float fraction, string value)
        {
            UITheme.StatBar(new Rect(x, y, w, 22f), label, fraction, value);
            y += 24f;
        }

        static string Get(OfficerLoadout loadout, AttachmentSlot slot)
        {
            switch (slot)
            {
                case AttachmentSlot.Light: return loadout.lightId;
                case AttachmentSlot.Optic: return loadout.opticId;
                case AttachmentSlot.Muzzle: return loadout.muzzleId;
                default: return loadout.stockId;
            }
        }

        static void Set(OfficerLoadout loadout, AttachmentSlot slot, string id)
        {
            switch (slot)
            {
                case AttachmentSlot.Light: loadout.lightId = id; break;
                case AttachmentSlot.Optic: loadout.opticId = id; break;
                case AttachmentSlot.Muzzle: loadout.muzzleId = id; break;
                default: loadout.stockId = id; break;
            }
        }

        public static string Category(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.CompactSMG: return "Compact submachine gun";
                case WeaponCategory.SMG: return "Submachine gun";
                case WeaponCategory.CompactRifle: return "Compact rifle";
                case WeaponCategory.Rifle: return "Service rifle";
                case WeaponCategory.Shotgun: return "Tactical shotgun";
                case WeaponCategory.Carbine: return "Precision carbine";
                case WeaponCategory.LessLethal: return "Less-lethal launcher";
                case WeaponCategory.ServicePistol: return "Service pistol";
                case WeaponCategory.BackupPistol: return "Backup pistol";
                case WeaponCategory.PDW: return "Personal defense weapon";
                case WeaponCategory.BurstRifle: return "Burst rifle";
                case WeaponCategory.Bullpup: return "Bullpup rifle";
                case WeaponCategory.Marksman: return "Marksman rifle";
                case WeaponCategory.LMG: return "Light machine gun";
                case WeaponCategory.AutoShotgun: return "Automatic shotgun";
                case WeaponCategory.Pepperball: return "Pepperball launcher (less-lethal)";
                case WeaponCategory.MachinePistol: return "Machine pistol";
                case WeaponCategory.Revolver: return "Revolver";
                case WeaponCategory.StunPistol: return "Stun pistol (less-lethal)";
                case WeaponCategory.Rotary: return "Rotary gun (game modes)";
                case WeaponCategory.DrumShotgun: return "Drum-fed automatic shotgun";
                case WeaponCategory.VectorSMG: return "Submachine gun";
                case WeaponCategory.GrenadeLauncher: return "Marking-grenade launcher (game modes)";
                default: return "Heavy sidearm";
            }
        }
    }
}
