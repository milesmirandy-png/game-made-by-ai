using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The built-in game content: weapons, attachments, armor, equipment,
    // officers, suspects and missions. GameData uses these unless an edited
    // ScriptableObject asset with the same id exists in a Resources folder
    // (SWAT > Create Editable Data Assets makes those for you).
    public static class DefaultContent
    {
        static int Roles(params OfficerRole[] roles)
        {
            int mask = 0;
            foreach (var role in roles) mask |= 1 << (int)role;
            return mask;
        }

        static WeaponData Weapon(string id, string name, WeaponCategory category, bool sidearm, FireMode mode, bool toggle,
            float damage, float rate, int mag, int reserve, float reload, float range, float spread, float recoil, int pellets,
            float noise, float move, float switchTime, Sound sound, int roles, string description)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.name = id;
            w.id = id;
            w.displayName = name;
            w.category = category;
            w.isSidearm = sidearm;
            w.fireMode = mode;
            w.canToggleFireMode = toggle;
            w.damage = damage;
            w.fireRate = rate;
            w.magazineSize = mag;
            w.startingReserve = reserve;
            w.reloadTime = reload;
            w.range = range;
            w.spread = spread;
            w.recoil = recoil;
            w.pellets = pellets;
            w.noiseRadius = noise;
            w.moveSpeedMultiplier = move;
            w.switchTime = switchTime;
            w.fireSound = sound;
            w.allowedRoles = roles;
            w.description = description;
            return w;
        }

        public static List<WeaponData> Weapons()
        {
            var noShield = Roles(OfficerRole.Leader, OfficerRole.Breacher, OfficerRole.Medic, OfficerRole.Recon, OfficerRole.Tactical);
            var list = new List<WeaponData>
            {
                Weapon("smg_compact", "K7 Compact SMG", WeaponCategory.CompactSMG, false, FireMode.FullAuto, false, 16f, 14f, 30, 120, 1.7f, 22f, 4.5f, 0.7f, 1, 20f, 1f, 0.45f, Sound.CompactSmg, 0,
                    "Light and very fast-firing. Best at close range."),
                Weapon("smg_v10", "V10 Submachine Gun", WeaponCategory.SMG, false, FireMode.FullAuto, true, 19f, 11f, 30, 120, 1.9f, 26f, 3.5f, 0.75f, 1, 21f, 0.98f, 0.5f, Sound.Smg, 0,
                    "Balanced, controllable and reliable indoors."),
                Weapon("rifle_compact", "CR5 Compact Rifle", WeaponCategory.CompactRifle, false, FireMode.FullAuto, true, 26f, 9.5f, 30, 90, 2.1f, 36f, 2.2f, 1f, 1, 27f, 0.95f, 0.55f, Sound.Rifle, 0,
                    "General-purpose rifle with a short barrel for tight corridors."),
                Weapon("rifle_service", "SR3 Service Rifle", WeaponCategory.Rifle, false, FireMode.FullAuto, true, 30f, 8.5f, 30, 90, 2.3f, 45f, 1.6f, 1.1f, 1, 28f, 0.92f, 0.6f, Sound.Rifle, noShield,
                    "Standard-issue rifle. Switch between automatic and semi-automatic with B."),
                Weapon("shotgun_ts8", "TS8 Tactical Shotgun", WeaponCategory.Shotgun, false, FireMode.SemiAuto, false, 15f, 1.4f, 7, 28, 2.8f, 14f, 9f, 3f, 8, 28f, 0.95f, 0.6f, Sound.Shotgun,
                    Roles(OfficerRole.Leader, OfficerRole.Breacher, OfficerRole.Tactical), "Wide spread, slow reload. Devastating up close, poor at range."),
                Weapon("carbine_pc9", "PC9 Precision Carbine", WeaponCategory.Carbine, false, FireMode.SemiAuto, false, 45f, 3f, 15, 60, 2.4f, 60f, 0.6f, 2f, 1, 30f, 0.9f, 0.65f, Sound.Carbine,
                    Roles(OfficerRole.Leader, OfficerRole.Recon, OfficerRole.Tactical), "Accurate and long-ranged, but slow to fire."),
                Weapon("launcher_ll40", "LL40 Less-Lethal Launcher", WeaponCategory.LessLethal, false, FireMode.SemiAuto, false, 8f, 1f, 5, 20, 2.6f, 25f, 1.5f, 2.5f, 1, 12f, 0.95f, 0.6f, Sound.LessLethal, 0,
                    "Fires low-damage impact rounds that stagger and stun. Doesn't stop everyone."),
                Weapon("pistol_p17", "P17 Service Pistol", WeaponCategory.ServicePistol, true, FireMode.SemiAuto, false, 28f, 5f, 15, 60, 1.3f, 28f, 2.2f, 1.4f, 1, 20f, 1f, 0.3f, Sound.Pistol, 0,
                    "Dependable standard sidearm."),
                Weapon("pistol_bk6", "BK6 Backup Pistol", WeaponCategory.BackupPistol, true, FireMode.SemiAuto, false, 24f, 6f, 8, 40, 1.1f, 20f, 3f, 1.2f, 1, 18f, 1f, 0.25f, Sound.Pistol, 0,
                    "Small and quick to draw, with a small magazine."),
                Weapon("pistol_h50", "H50 Heavy Sidearm", WeaponCategory.HeavyPistol, true, FireMode.SemiAuto, false, 48f, 2.5f, 7, 35, 1.6f, 30f, 2.5f, 3.2f, 1, 26f, 1f, 0.35f, Sound.HeavyPistol, 0,
                    "Hits hard, kicks hard, fires slowly."),
            };
            var launcher = list[6];
            launcher.lessLethal = true;
            launcher.stunDuration = 3f;
            launcher.tracerColor = new Color(1f, 0.6f, 0.2f);

            // The arsenal update: ten more weapons (all fictional game abstractions).
            var heavy = Roles(OfficerRole.Leader, OfficerRole.Breacher, OfficerRole.Tactical);
            var precise = Roles(OfficerRole.Leader, OfficerRole.Recon, OfficerRole.Tactical);
            list.Add(Weapon("pdw_x4", "X4 Defense Weapon", WeaponCategory.PDW, false, FireMode.FullAuto, true, 18f, 13f, 40, 160, 2f, 30f, 2.8f, 0.55f, 1, 20f, 1f, 0.45f, Sound.Pdw, 0,
                "Big top-loaded magazine and very little kick. Easy to control on the move."));
            list.Add(Weapon("rifle_b4", "B4 Burst Rifle", WeaponCategory.BurstRifle, false, FireMode.Burst, true, 28f, 14f, 30, 90, 2.2f, 42f, 1.4f, 0.8f, 1, 27f, 0.94f, 0.55f, Sound.BurstRifle, 0,
                "Fires three quick rounds per pull. Switch to single shots with B."));
            list.Add(Weapon("rifle_cx", "CX Bullpup Rifle", WeaponCategory.Bullpup, false, FireMode.FullAuto, true, 27f, 10f, 30, 90, 2.6f, 40f, 1.8f, 0.95f, 1, 27f, 0.97f, 0.55f, Sound.Rifle, 0,
                "A long barrel in a short body: rifle range with compact handling, but slower to reload."));
            list.Add(Weapon("dmr_dm2", "DM2 Marksman Rifle", WeaponCategory.Marksman, false, FireMode.SemiAuto, false, 62f, 2.2f, 10, 40, 2.6f, 70f, 0.35f, 2.6f, 1, 32f, 0.88f, 0.7f, Sound.Marksman, precise,
                "Hard-hitting and pinpoint accurate. Hold steady aim to see much further."));
            list.Add(Weapon("lmg_lm8", "LM8 Light MG", WeaponCategory.LMG, false, FireMode.FullAuto, false, 21f, 12f, 75, 150, 4.2f, 45f, 3.2f, 0.85f, 1, 30f, 0.84f, 0.9f, Sound.Lmg, heavy,
                "A huge magazine and a long reload. Crouch to steady it."));
            list.Add(Weapon("shotgun_as12", "AS12 Auto Shotgun", WeaponCategory.AutoShotgun, false, FireMode.SemiAuto, false, 11f, 3.2f, 8, 32, 3f, 13f, 10f, 2.4f, 8, 28f, 0.93f, 0.6f, Sound.AutoShotgun, heavy,
                "Fires as fast as you can pull the trigger. Brutal up close, useless at range."));
            list.Add(Weapon("pepper_pb3", "PB3 Pepperball", WeaponCategory.Pepperball, false, FireMode.SemiAuto, false, 4f, 5f, 15, 60, 2.2f, 22f, 2f, 0.5f, 1, 10f, 1f, 0.5f, Sound.Pepperball, 0,
                "Rapid less-lethal rounds that briefly stun. Several hits wear down a suspect's resolve."));
            list.Add(Weapon("mp_m9", "M9 Machine Pistol", WeaponCategory.MachinePistol, true, FireMode.FullAuto, true, 15f, 15f, 20, 80, 1.5f, 18f, 5f, 0.9f, 1, 19f, 1f, 0.3f, Sound.MachinePistol, 0,
                "A sidearm that empties its magazine in a heartbeat. Wild but fast."));
            list.Add(Weapon("revolver_r6", "R6 Revolver", WeaponCategory.Revolver, true, FireMode.SemiAuto, false, 60f, 1.8f, 6, 36, 2.4f, 32f, 1.8f, 3.4f, 1, 27f, 1f, 0.35f, Sound.Revolver, 0,
                "Six heavy rounds and a slow reload. Every shot counts."));
            list.Add(Weapon("stun_s2", "S2 Stun Pistol", WeaponCategory.StunPistol, true, FireMode.SemiAuto, false, 2f, 1f, 1, 8, 2.5f, 9f, 1.5f, 0.6f, 1, 6f, 1f, 0.3f, Sound.Zap, 0,
                "One short-range stun dart per load. Almost silent, very likely to make a suspect give up."));

            // Heavier hardware, inspired by classic arcade shooters. The rotary gun and the marking-round
            // launcher are game-mode only (they don't fit a SWAT entry).
            list.Add(Weapon("rotary_rg6", "RG6 Rotary Gun", WeaponCategory.Rotary, false, FireMode.FullAuto, false, 15f, 20f, 150, 150, 5f, 40f, 4.5f, 0.5f, 1, 34f, 0.75f, 1.2f, Sound.Rotary, 0,
                "Spins up, then pours out rounds. Very heavy: you move slowly while carrying it."));
            list.Add(Weapon("shotgun_d20", "D20 Drum Shotgun", WeaponCategory.DrumShotgun, false, FireMode.FullAuto, false, 9f, 4f, 20, 40, 3.6f, 12f, 11f, 2f, 8, 30f, 0.9f, 0.7f, Sound.AutoShotgun, heavy,
                "A fully automatic shotgun with a 20-round drum. Clears a room, kicks like a mule."));
            list.Add(Weapon("smg_kv", "KV Vector SMG", WeaponCategory.VectorSMG, false, FireMode.FullAuto, true, 16f, 18f, 25, 125, 1.8f, 22f, 3.2f, 0.4f, 1, 21f, 1f, 0.45f, Sound.CompactSmg, 0,
                "Blistering fire rate and almost no muzzle climb, but it empties fast."));
            list.Add(Weapon("launcher_gl6", "GL6 Marker Launcher", WeaponCategory.GrenadeLauncher, false, FireMode.SemiAuto, false, 70f, 1.2f, 6, 18, 4f, 30f, 1f, 3f, 1, 30f, 0.85f, 0.8f, Sound.Launcher, 0,
                "Six marking grenades that burst on impact and tag everyone close by. Game modes only."));

            // Look and feel per weapon: furniture colour of the 3D model (matching its sprite), kick, muzzle flash and extras.
            foreach (var w in list)
            {
                switch (w.id)
                {
                    case "smg_compact": Feel(w, Polymer, 0.6f, 0.45f); break;
                    case "smg_v10": Feel(w, Polymer, 0.7f, 0.5f); break;
                    case "rifle_compact": Feel(w, Wood, 0.9f, 0.6f); break;
                    case "rifle_service": Feel(w, Wood, 1f, 0.7f); break;
                    case "shotgun_ts8": Feel(w, Polymer, 2.2f, 0.95f); w.pumpAction = true; w.tracerWidth = 0.035f; break;
                    case "carbine_pc9": Feel(w, Wood, 1.6f, 0.8f); w.steadyLookAhead = 3f; w.pumpAction = true; break;
                    case "launcher_ll40": Feel(w, Polymer, 1.4f, 0.5f); w.ejectsShells = false; w.tracerWidth = 0.08f; break;
                    case "pistol_p17": Feel(w, Polymer, 0.7f, 0.4f); break;
                    case "pistol_bk6": Feel(w, Polymer, 0.6f, 0.35f); break;
                    case "pistol_h50": Feel(w, Wood, 1.5f, 0.55f); break;
                    case "pdw_x4": Feel(w, Polymer, 0.55f, 0.45f); break;
                    case "rifle_b4": Feel(w, Wood, 0.9f, 0.65f); w.burstCount = 3; break;
                    case "rifle_cx": Feel(w, Polymer, 1f, 0.7f); break;
                    case "dmr_dm2": Feel(w, Wood, 2f, 0.9f); w.steadyLookAhead = 7f; w.pumpAction = true; w.tracerWidth = 0.06f; break;
                    case "lmg_lm8": Feel(w, Wood, 0.9f, 0.75f); w.crouchSpread = 0.45f; break;
                    case "shotgun_as12": Feel(w, Polymer, 1.8f, 0.9f); w.tracerWidth = 0.035f; break;
                    case "pepper_pb3":
                        Feel(w, Polymer, 0.3f, 0.25f);
                        w.lessLethal = true; w.stunDuration = 0.8f; w.surrenderBonus = 0.06f; w.ejectsShells = false;
                        w.tracerColor = new Color(0.85f, 0.4f, 1f);
                        break;
                    case "mp_m9": Feel(w, Polymer, 0.6f, 0.4f); break;
                    case "revolver_r6": Feel(w, Polymer, 1.9f, 0.6f); w.ejectsShells = false; break;
                    // The arcade guns don't belong in 1999 police work: game modes only, with Arcade weapons on.
                    case "rotary_rg6": Feel(w, Polymer, 0.7f, 0.7f); w.spinUp = 0.5f; w.versusOnly = w.arcade = true; w.crouchSpread = 0.6f; break;
                    case "shotgun_d20": Feel(w, Polymer, 1.9f, 0.9f); w.tracerWidth = 0.035f; w.versusOnly = w.arcade = true; break;
                    case "smg_kv": Feel(w, Amber, 0.45f, 0.45f); w.versusOnly = w.arcade = true; break;
                    case "launcher_gl6":
                        Feel(w, Amber, 2.4f, 0.8f);
                        w.blastRadius = 3.5f; w.versusOnly = w.arcade = true; w.ejectsShells = false; w.tracerWidth = 0.09f;
                        w.tracerColor = new Color(1f, 0.6f, 0.2f);
                        break;
                    case "stun_s2":
                        Feel(w, Polymer, 0.3f, 0.2f);
                        w.lessLethal = true; w.stunDuration = 3.5f; w.surrenderBonus = 0.5f; w.ejectsShells = false;
                        w.tracerColor = new Color(0.45f, 0.9f, 1f); w.tracerWidth = 0.025f;
                        break;
                }
            }
            return list;
        }

        // The sprite pack's furniture: charcoal polymer, orange wood and a lighter amber.
        static readonly Color Polymer = new Color(0.24f, 0.24f, 0.23f);
        static readonly Color Wood = new Color(0.78f, 0.4f, 0.14f);
        static readonly Color Amber = new Color(0.86f, 0.56f, 0.2f);

        static void Feel(WeaponData w, Color accent, float kick, float flash)
        {
            w.accent = accent;
            w.kick = kick;
            w.flashSize = flash;
        }

        static AttachmentData Attachment(string id, string name, AttachmentSlot slot, float spread, float recoil, float noise, float move, float light, int unlock, string description)
        {
            var a = ScriptableObject.CreateInstance<AttachmentData>();
            a.name = id;
            a.id = id;
            a.displayName = name;
            a.slot = slot;
            a.spreadMultiplier = spread;
            a.recoilMultiplier = recoil;
            a.noiseMultiplier = noise;
            a.moveMultiplier = move;
            a.lightRangeMultiplier = light;
            a.unlockAfterMissions = unlock;
            a.description = description;
            return a;
        }

        // The extra effects and the look of an attachment.
        static AttachmentData Tune(AttachmentData a, AttachmentLook look, float magazine = 1f, float reload = 1f, float aim = 1f, float zoom = 1f, float lookAhead = 0f, float flash = 1f, bool laser = false)
        {
            a.look = look;
            a.magazineMultiplier = magazine;
            a.reloadMultiplier = reload;
            a.aimSpeedMultiplier = aim;
            a.zoom = zoom;
            a.lookAhead = lookAhead;
            a.flashMultiplier = flash;
            a.laser = laser;
            return a;
        }

        // Attachments go on the primary weapon. Effects are small multipliers on the weapon's stats
        // (spread, recoil, noise, movement, light range), plus magazine size, reload time, how fast the
        // sights come up, optic zoom and muzzle flash.
        public static List<AttachmentData> Attachments()
        {
            return new List<AttachmentData>
            {
                // Lights
                Tune(Attachment("light_weapon", "Weapon Light", AttachmentSlot.Light, 1f, 1f, 1f, 1f, 1.5f, 0, "A brighter, longer flashlight beam."), AttachmentLook.Light),
                Tune(Attachment("light_high", "High-Output Light", AttachmentSlot.Light, 1f, 1f, 1f, 0.99f, 1.9f, 3, "Floods a room with light; a little heavier on the rail."), AttachmentLook.Light),
                // Optics
                Tune(Attachment("optic_reflex", "Mini Reflex Sight", AttachmentSlot.Optic, 0.9f, 1f, 1f, 1f, 1f, 0, "A tiny open sight: slightly tighter spread and quick to the eye."), AttachmentLook.Reflex, aim: 1.1f),
                Tune(Attachment("optic_reddot", "Red Dot Sight", AttachmentSlot.Optic, 0.85f, 1f, 1f, 1f, 1f, 1, "A tube sight with a red dot: tighter spread."), AttachmentLook.RedDot),
                Tune(Attachment("optic_holo", "Holographic Sight", AttachmentSlot.Optic, 0.82f, 1f, 1f, 0.99f, 1f, 2, "A wide window and a ring reticle: tighter spread, quick to pick up."), AttachmentLook.Holo, aim: 1.05f),
                Tune(Attachment("optic_scope", "2.5x Scope", AttachmentSlot.Optic, 0.72f, 1f, 1f, 0.97f, 1f, 4, "Magnified: much tighter spread and a longer view when steady aiming, but slower to the eye."), AttachmentLook.Scope, aim: 0.8f, zoom: 2.5f, lookAhead: 3f),
                // Muzzle devices
                Tune(Attachment("muzzle_flashhider", "Flash Hider", AttachmentSlot.Muzzle, 1f, 0.97f, 1f, 1f, 1f, 0, "Hides most of the muzzle flash."), AttachmentLook.FlashHider, flash: 0.5f),
                Tune(Attachment("muzzle_suppressor", "Suppressor", AttachmentSlot.Muzzle, 1f, 0.95f, 0.75f, 1f, 1f, 1, "Quieter shots that carry less far, and only a faint flash."), AttachmentLook.Suppressor, flash: 0.35f),
                Tune(Attachment("muzzle_comp", "Compensator", AttachmentSlot.Muzzle, 1f, 0.86f, 1.05f, 1f, 1f, 2, "Less muzzle climb; a bit louder."), AttachmentLook.Compensator, flash: 1.15f),
                Tune(Attachment("muzzle_brake", "Muzzle Brake", AttachmentSlot.Muzzle, 1f, 0.78f, 1.12f, 1f, 1f, 3, "Much less recoil, but loud and bright."), AttachmentLook.Brake, flash: 1.35f),
                // Stocks
                Tune(Attachment("stock_collapsible", "Collapsible Stock", AttachmentSlot.Stock, 1f, 1.05f, 1f, 1.03f, 1f, 0, "Marginally quicker handling."), AttachmentLook.None),
                Tune(Attachment("stock_skeleton", "Skeleton Stock", AttachmentSlot.Stock, 1f, 1.08f, 1f, 1.04f, 1f, 1, "A bare frame: lighter and quicker to the sights, kicks a little more."), AttachmentLook.SkeletonStock, aim: 1.08f),
                Tune(Attachment("stock_fixed", "Fixed Stock", AttachmentSlot.Stock, 1f, 0.9f, 1f, 0.98f, 1f, 2, "Marginally steadier."), AttachmentLook.FixedStock),
                // Grips and lasers
                Tune(Attachment("grip_vertical", "Vertical Grip", AttachmentSlot.Underbarrel, 1f, 0.88f, 1f, 0.99f, 1f, 0, "Less recoil."), AttachmentLook.VerticalGrip),
                Tune(Attachment("rail_laser", "Laser Module", AttachmentSlot.Underbarrel, 0.9f, 1f, 1f, 1f, 1f, 2, "A red aim laser (a dot where you aim in first person) and slightly tighter spread."), AttachmentLook.Laser, laser: true),
                // Magazines
                Tune(Attachment("mag_quick", "Quick-Pull Magazine", AttachmentSlot.Magazine, 1f, 1f, 1f, 1f, 1f, 0, "A pull tab on the magazine: faster reloads."), AttachmentLook.QuickMag, reload: 0.82f),
                Tune(Attachment("mag_extended", "Extended Magazine", AttachmentSlot.Magazine, 1f, 1f, 1f, 0.98f, 1f, 1, "Half again as many rounds per magazine; slower reloads."), AttachmentLook.ExtendedMag, magazine: 1.5f, reload: 1.12f),
            };
        }

        static ArmorData Armor(string id, string name, int tier, float reduction, float speed, int capacity, float durability, bool helmet, Color color, string description)
        {
            var a = ScriptableObject.CreateInstance<ArmorData>();
            a.name = id;
            a.id = id;
            a.displayName = name;
            a.tier = tier;
            a.damageReduction = reduction;
            a.speedMultiplier = speed;
            a.capacityBonus = capacity;
            a.durability = durability;
            a.helmet = helmet;
            a.color = color;
            a.description = description;
            return a;
        }

        public static List<ArmorData> ArmorList()
        {
            return new List<ArmorData>
            {
                // Barebones: no plates at all. Quickest and carries the most, but every hit lands in full.
                Armor("armor_none", "No Vest", 0, 0f, 1.08f, 2, 0f, false, new Color(0.2f, 0.2f, 0.2f), "Barebones: BDUs, a duty belt and kneepads. The quickest and carries the most, but nothing stops a round."),
                Armor("armor_light", "Concealable Vest", 1, 0.2f, 1.05f, 1, 70f, false, new Color(0.22f, 0.24f, 0.26f), "Soft body armor with no pouches: fast and roomy, stops handgun rounds better than rifle rounds."),
                Armor("armor_standard", "Tactical Vest", 2, 0.35f, 1f, 0, 100f, true, new Color(0.08f, 0.09f, 0.11f), "A raid vest over soft armor with a trauma plate, pouches and POLICE panels: balanced protection."),
                Armor("armor_heavy", "Heavy Tactical Vest", 3, 0.5f, 0.88f, -1, 140f, true, new Color(0.04f, 0.04f, 0.05f), "Ceramic plates, shoulder guards, collar and groin protector: strong protection, slower and carries less."),
            };
        }

        static EquipmentData Item(string id, string name, EquipmentKind kind, int cost, int max, float radius, float fuse, float duration, float throwRange, float noise, float heal, float useTime, bool consumable, string description)
        {
            var e = ScriptableObject.CreateInstance<EquipmentData>();
            e.name = id;
            e.id = id;
            e.displayName = name;
            e.kind = kind;
            e.capacityCost = cost;
            e.maxCarry = max;
            e.radius = radius;
            e.fuseTime = fuse;
            e.effectDuration = duration;
            e.throwRange = throwRange;
            e.noiseRadius = noise;
            e.healAmount = heal;
            e.useTime = useTime;
            e.consumable = consumable;
            e.description = description;
            return e;
        }

        public static List<EquipmentData> EquipmentList()
        {
            return new List<EquipmentData>
            {
                Item("flashbang", "Flashbang", EquipmentKind.Flashbang, 1, 4, 7f, 1.4f, 5f, 12f, 25f, 0f, 0f, true, "Disorients everyone in sight of the blast for a few seconds."),
                Item("smoke", "Smoke Grenade", EquipmentKind.Smoke, 1, 3, 4f, 1.2f, 14f, 12f, 8f, 0f, 0f, true, "A thick cloud that blocks sight lines."),
                Item("breach_charge", "Breaching Charge", EquipmentKind.BreachingCharge, 2, 4, 3.5f, 2.5f, 3.5f, 0f, 30f, 0f, 1.5f, true, "Opens doors marked with yellow stripes. Stand back."),
                Item("door_wedge", "Door Wedge", EquipmentKind.DoorWedge, 1, 3, 0f, 0f, 0f, 0f, 0f, 0f, 1f, true, "Jams a closed door so nobody can open it. Remove with E."),
                Item("medkit", "Medical Kit", EquipmentKind.MedicalKit, 2, 4, 0f, 0f, 0f, 0f, 0f, 40f, 2.5f, true, "Restores some health to you, a teammate or a civilian."),
                Item("portable_light", "Portable Light", EquipmentKind.PortableLight, 1, 3, 9f, 0f, 180f, 4f, 0f, 0f, 0.5f, true, "Drop it to light up a dark room."),
                Item("recon_camera", "Recon Camera", EquipmentKind.ReconCamera, 2, 1, 0f, 0f, 0f, 0f, 0f, 0f, 1f, false, "Peek under a closed door to count who is inside. Reusable."),
                Item("cs_gas", "CS Gas", EquipmentKind.CSGas, 1, 3, 4.5f, 1.3f, 16f, 12f, 10f, 0f, 0f, true, "Choking gas: suspects inside cough, stagger and give up more easily. Anyone without a gas mask chokes too (Look tab: Face - Gas mask)."),
                Item("chem_light", "Chem Lights", EquipmentKind.ChemLight, 0, 8, 2.5f, 0.7f, 0f, 8f, 0f, 0f, 0f, true, "Glow sticks to mark rooms you've cleared. They stay lit all mission and show on the map."),
            };
        }

        static OfficerData Officer(string id, string name, string callsign, OfficerRole role, float health, float armorRating, float speed, float accuracy,
            float reaction, float perception, float responsiveness, int capacity, string ability, string abilityText, float cooldown, int unlock, string personality)
        {
            var o = ScriptableObject.CreateInstance<OfficerData>();
            o.name = id;
            o.id = id;
            o.displayName = name;
            o.callsign = callsign;
            o.role = role;
            o.maxHealth = health;
            o.armorRating = armorRating;
            o.moveSpeed = speed;
            o.accuracy = accuracy;
            o.reactionTime = reaction;
            o.perceptionRange = perception;
            o.commandResponsiveness = responsiveness;
            o.equipmentCapacity = capacity;
            o.abilityName = ability;
            o.abilityDescription = abilityText;
            o.abilityCooldown = cooldown;
            o.unlockAfterMissions = unlock;
            o.personality = personality;
            return o;
        }

        static OfficerLoadout Kit(string officerId, string primary, string sidearm, string armor, bool shield, string light, params EquipmentCount[] items)
        {
            var kit = new OfficerLoadout { officerId = officerId, primaryId = primary, sidearmId = sidearm, armorId = armor, useShield = shield, lightId = light };
            kit.equipment.AddRange(items);
            return kit;
        }

        public static List<OfficerData> Officers()
        {
            var leader = Officer("leader", "Sgt. Dana Reyes", "Lead", OfficerRole.Leader, 100f, 0f, 1f, 0.7f, 0.35f, 18f, 1.3f, 9,
                "Coordinate", "For 10 seconds your squad reacts faster and shoots more accurately. Also shows how many threats your team currently sees.", 40f, 0,
                "Calm under pressure and methodical. Twelve years with the unit; plans every entry twice.");
            leader.defaultLoadout = Kit("leader", "rifle_service", "pistol_p17", "armor_standard", false, "light_weapon",
                new EquipmentCount("flashbang", 2), new EquipmentCount("smoke", 1), new EquipmentCount("breach_charge", 1));

            var shield = Officer("shield", "Ofc. Tomas Brandt", "Bulwark", OfficerRole.Shield, 110f, 0.05f, 0.92f, 0.6f, 0.45f, 16f, 1f, 6,
                "Brace", "Plant the shield: much stronger frontal protection, and squadmates close behind take less damage. Toggle with T.", 2f, 0,
                "Quiet, patient and immovable. Always first through the door and last to leave.");
            shield.defaultLoadout = Kit("shield", "smg_v10", "pistol_p17", "armor_heavy", true, "light_weapon", new EquipmentCount("flashbang", 2));

            var breacher = Officer("breacher", "Ofc. Kenji Moraes", "Ram", OfficerRole.Breacher, 100f, 0f, 1f, 0.62f, 0.4f, 16f, 1f, 8,
                "Door Kick", "Kick open a locked door without a charge (not reinforced ones). Places breaching charges much faster.", 30f, 0,
                "Former firefighter. Knows every lock, hinge and door frame by heart.");
            breacher.defaultLoadout = Kit("breacher", "shotgun_ts8", "pistol_p17", "armor_standard", false, null,
                new EquipmentCount("breach_charge", 3), new EquipmentCount("door_wedge", 2), new EquipmentCount("flashbang", 1));

            var medic = Officer("medic", "Ofc. Priya Lindqvist", "Patch", OfficerRole.Medic, 95f, 0f, 1.02f, 0.6f, 0.42f, 17f, 1f, 8,
                "Stabilize", "Treat yourself or the nearest injured teammate for 25 health over 3 seconds. Medical kits heal 50% more.", 45f, 0,
                "Paramedic turned officer. Keeps the team on its feet and talks civilians through the worst day of their lives.");
            medic.defaultLoadout = Kit("medic", "smg_v10", "pistol_p17", "armor_light", false, null,
                new EquipmentCount("medkit", 3), new EquipmentCount("smoke", 2), new EquipmentCount("flashbang", 1));

            var recon = Officer("recon", "Ofc. Samuel Okafor", "Hawk", OfficerRole.Recon, 90f, 0f, 1.06f, 0.72f, 0.33f, 24f, 1.1f, 7,
                "Scan", "Mark threats within 12 metres on your map for 6 seconds, even through walls.", 25f, 1,
                "Sharp-eyed and talkative. Spots what everyone else walks past.");
            recon.defaultLoadout = Kit("recon", "carbine_pc9", "pistol_bk6", "armor_light", false, "light_weapon",
                new EquipmentCount("recon_camera", 1), new EquipmentCount("flashbang", 1), new EquipmentCount("portable_light", 1));

            var tactical = Officer("tactical", "Ofc. Hanna Novak", "Spark", OfficerRole.Tactical, 100f, 0f, 1f, 0.64f, 0.4f, 17f, 1f, 11,
                "Overcharge", "Your next flashbang or less-lethal round is 50% more effective.", 30f, 2,
                "Gadget specialist. Prefers to end things without a single shot fired.");
            tactical.defaultLoadout = Kit("tactical", "rifle_compact", "pistol_p17", "armor_standard", false, "light_weapon",
                new EquipmentCount("flashbang", 3), new EquipmentCount("smoke", 2), new EquipmentCount("portable_light", 1), new EquipmentCount("door_wedge", 1));

            return new List<OfficerData> { leader, shield, breacher, medic, recon, tactical };
        }

        static EnemyData Enemy(string id, string name, EnemyArchetype archetype, float health, float reduction, float walk, float run, float detection, float fov,
            float accuracy, float reaction, float damage, float rate, int burst, float surrender, float flee, bool armed, float help, Color shirt, int headwear)
        {
            var e = ScriptableObject.CreateInstance<EnemyData>();
            e.name = id;
            e.id = id;
            e.displayName = name;
            e.archetype = archetype;
            e.maxHealth = health;
            e.damageReduction = reduction;
            e.walkSpeed = walk;
            e.runSpeed = run;
            e.detectionRange = detection;
            e.fieldOfView = fov;
            e.accuracy = accuracy;
            e.reactionTime = reaction;
            e.weaponDamage = damage;
            e.fireRate = rate;
            e.burstSize = burst;
            e.surrenderChance = surrender;
            e.fleeChance = flee;
            e.armed = armed;
            e.callForHelpRadius = help;
            e.shirtColor = shirt;
            e.headwear = headwear;
            return e;
        }

        public static List<EnemyData> Enemies()
        {
            var nervous = Enemy("nervous", "Nervous Suspect", EnemyArchetype.Nervous, 90f, 0f, 2.2f, 4.8f, 14f, 120f, 0.25f, 0.9f, 8f, 2f, 2, 0.6f, 0.5f, true, 8f, new Color(0.55f, 0.42f, 0.15f), 0);
            nervous.erratic = true;
            var dummy = Enemy("dummy", "Training Dummy", EnemyArchetype.TrainingDummy, 100f, 0f, 1.5f, 3f, 10f, 140f, 0f, 1f, 0f, 0f, 0, 1f, 0f, false, 0f, new Color(0.9f, 0.45f, 0.1f), 1);
            dummy.respondsToAlarms = false;
            return new List<EnemyData>
            {
                Enemy("suspect_unarmed", "Unarmed Suspect", EnemyArchetype.UnarmedSuspect, 80f, 0f, 2f, 4.5f, 14f, 120f, 0f, 0.6f, 0f, 0f, 0, 0.55f, 0.6f, false, 10f, new Color(0.42f, 0.36f, 0.3f), 0),
                Enemy("hostile", "Armed Suspect", EnemyArchetype.Hostile, 100f, 0f, 2f, 4f, 16f, 110f, 0.45f, 0.7f, 9f, 3.5f, 3, 0.35f, 0.15f, true, 12f, new Color(0.55f, 0.12f, 0.1f), 2),
                Enemy("guard", "Hostile Guard", EnemyArchetype.Guard, 100f, 0f, 2f, 4f, 18f, 120f, 0.42f, 0.65f, 9f, 3f, 3, 0.3f, 0.1f, true, 20f, new Color(0.25f, 0.25f, 0.3f), 1),
                nervous,
                Enemy("armored", "Armored Hostile", EnemyArchetype.Armored, 150f, 0.4f, 1.6f, 3.2f, 16f, 100f, 0.5f, 0.6f, 12f, 3f, 3, 0.1f, 0.05f, true, 12f, new Color(0.18f, 0.18f, 0.2f), 3),
                Enemy("leader", "Suspect Leader", EnemyArchetype.Leader, 130f, 0.1f, 2f, 4.4f, 17f, 120f, 0.5f, 0.55f, 11f, 3f, 3, 0.25f, 0.6f, true, 18f, new Color(0.1f, 0.1f, 0.12f), 1),
                dummy,
            };
        }

        static ObjectiveDefinition O(ObjectiveType type, string text, int points, string target = null, int count = 0)
        {
            return new ObjectiveDefinition(type, text, points, target, count);
        }

        static MissionData Mission(string id, string name, string location, MissionType type, string map, int difficulty, int squad, float par, int unlock, int order, Color color)
        {
            var m = ScriptableObject.CreateInstance<MissionData>();
            m.name = id;
            m.id = id;
            m.displayName = name;
            m.location = location;
            m.missionType = type;
            m.mapId = map;
            m.difficulty = difficulty;
            m.maxSquad = squad;
            m.parTime = par;
            m.unlockAfterMissions = unlock;
            m.sortOrder = order;
            m.thumbnailColor = color;
            return m;
        }

        public static List<MissionData> Missions()
        {
            var training = Mission("m00_training", "TRU Qualification Course", "TRU Training Facility", MissionType.Training, "training", 1, 2, 600f, 0, 0, new Color(0.25f, 0.4f, 0.3f));
            training.isTraining = true;
            training.timeOfDay = TimeOfDay.Day;
            training.sequentialObjectives = true;
            training.description = "Learn movement, shooting, doors, equipment and squad commands.";
            training.briefing = "Welcome to the Tactical Response Unit qualification course. Instructors will walk you through each skill in turn. "
                + "Follow the objective list on the left: each step unlocks the next. Nobody here is hostile; the suspect and the civilian are volunteers.";
            training.objectives.AddRange(new[]
            {
                O(ObjectiveType.TrainingMove, "Walk to the marker at the firing range (WASD)", 50, "zone_range"),
                O(ObjectiveType.TrainingShootTargets, "Shoot 5 targets (aim with the mouse, Left click to fire)", 100, null, 5),
                O(ObjectiveType.TrainingReload, "Reload your weapon (R)", 50),
                O(ObjectiveType.TrainingSwitchWeapon, "Switch to your sidearm (Q or 2), then back", 50),
                O(ObjectiveType.TrainingOpenDoor, "Open the training house door (E)", 50, "door_train_entry"),
                O(ObjectiveType.TrainingBreach, "Breach the locked door (select the charge with 3/4, then G)", 100, "door_train_breach"),
                O(ObjectiveType.TrainingFlashbang, "Throw a flashbang into the marked room (select it, then G)", 100, "room_flash"),
                O(ObjectiveType.TrainingCommandSquad, "Hold Z, point at the marked door and order your squad to Stack Up", 100, "door_train_stack"),
                O(ObjectiveType.TrainingRestrain, "Shout at the volunteer suspect (X), then restrain them (E)", 100),
                O(ObjectiveType.TrainingEscort, "Ask the civilian volunteer to follow you (E) and lead them to the safe zone", 100),
                O(ObjectiveType.ReachExtraction, "Return to the start area", 50),
            });
            training.enemies.Add(new EnemyGroup("dummy", 1, "dummy"));
            training.civilians.Add(new CivilianGroup(CivilianType.Visitor, 1, "volunteer"));
            training.bonusEquipment.Add(new EquipmentCount("breach_charge", 1));
            training.bonusEquipment.Add(new EquipmentCount("flashbang", 1));
            training.optionalCount = 0;
            training.alarmArmedChance = 0f;
            training.camerasActiveChance = 0f;
            training.randomLockChance = 0f;

            var clearance = Mission("m01_clearance", "Operation Glass Desk", "Halvorsen Logistics Offices", MissionType.BuildingClearance, "office", 1, 3, 480f, 0, 2, new Color(0.2f, 0.32f, 0.5f));
            clearance.levelNumber = 2;
            clearance.description = "Armed suspects have occupied an office building. Clear it room by room.";
            clearance.briefing = "0612 hours. Port Avalon PD received reports of armed individuals entering the Halvorsen Logistics offices before opening time. "
                + "Several staff are believed to be inside. Enter the building, locate and protect the civilians, secure every suspect and clear the security and storage rooms. "
                + "The front desk terminal can disable the building's cameras. Extract at the van when the building is secure.";
            clearance.objectives.AddRange(new[]
            {
                O(ObjectiveType.EnterBuilding, "Enter the building", 100),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 400),
                O(ObjectiveType.RescueCivilians, "Evacuate all civilians to the safe zone", 300),
                O(ObjectiveType.SecureRoom, "Secure the Security Room", 200, "security"),
                O(ObjectiveType.SecureRoom, "Secure the Storage Room", 200, "storage"),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 200),
            });
            clearance.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 3 suspects", 150, null, 3),
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 150),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
            });
            clearance.enemies.AddRange(new[] { new EnemyGroup("hostile", 5), new EnemyGroup("guard", 2), new EnemyGroup("nervous", 1), new EnemyGroup("suspect_unarmed", 1) });
            clearance.civilians.AddRange(new[] { new CivilianGroup(CivilianType.OfficeWorker, 3), new CivilianGroup(CivilianType.Visitor, 1), new CivilianGroup(CivilianType.Hiding, 1) });
            clearance.camerasActiveChance = 0.8f;
            clearance.timeOfDay = TimeOfDay.Day;

            var rescue = Mission("m02_rescue", "Operation Lantern", "Halvorsen Logistics Offices (night)", MissionType.CivilianRescue, "office", 2, 3, 540f, 2, 4, new Color(0.3f, 0.25f, 0.45f));
            rescue.levelNumber = 4;
            rescue.night = true;
            rescue.timeOfDay = TimeOfDay.Night;
            rescue.powerOutageChance = 0.6f;
            rescue.description = "A night shift is trapped inside during a hostage situation. Get them out.";
            rescue.briefing = "2140 hours. A disgruntled former employee and associates are holding the Halvorsen night shift in the conference room. "
                + "The power may be cut: expect dark rooms and bring lights. Some staff are injured or hiding. "
                + "Priority is civilian safety: find everyone, treat the injured and escort them to the safe zone by the van.";
            rescue.objectives.AddRange(new[]
            {
                O(ObjectiveType.RescueCivilians, "Evacuate all civilians to the safe zone", 400),
                O(ObjectiveType.SecureRoom, "Secure the Conference Room", 200, "conference"),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 200),
            });
            rescue.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 200),
                O(ObjectiveType.TreatInjured, "Treat every injured civilian", 150),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.TimeLimit, "Finish within 7 minutes", 150, null, 420),
            });
            rescue.enemies.AddRange(new[] { new EnemyGroup("hostile", 4), new EnemyGroup("nervous", 2), new EnemyGroup("guard", 1) });
            rescue.civilians.AddRange(new[]
            {
                new CivilianGroup(CivilianType.OfficeWorker, 2), new CivilianGroup(CivilianType.Injured, 2),
                new CivilianGroup(CivilianType.Hiding, 1), new CivilianGroup(CivilianType.Hostage, 2, "conference"),
            });
            rescue.alarmArmedChance = 0.5f;
            rescue.camerasActiveChance = 0.5f;
            rescue.bonusEquipment.Add(new EquipmentCount("portable_light", 1));

            var warehouse = Mission("m03_warehouse", "Operation Cold Storage", "Kestrel Freight Warehouse", MissionType.Investigation, "warehouse", 2, 3, 600f, 4, 6, new Color(0.4f, 0.32f, 0.2f));
            warehouse.levelNumber = 6;
            warehouse.description = "A silent alarm at a freight warehouse. Find out what's going on.";
            warehouse.briefing = "0230 hours. Kestrel Freight's silent alarm tripped and the night guards stopped answering. Intelligence suggests a smuggling crew is moving goods through the building. "
                + "Reach the security booth and review the camera footage to locate the evidence, secure it, and detain the crew. Their leader may try to slip out the back.";
            warehouse.night = true;
            warehouse.timeOfDay = TimeOfDay.Night;
            warehouse.powerOutageChance = 0.25f;
            warehouse.objectives.AddRange(new[]
            {
                O(ObjectiveType.UseConsole, "Review the footage at the security booth console", 200, "booth_console"),
                O(ObjectiveType.SecureEvidence, "Secure 3 pieces of evidence", 300, null, 3),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 200),
            });
            warehouse.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.ApprehendLeader, "Arrest the crew leader alive", 250),
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 150),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150),
            });
            warehouse.enemies.AddRange(new[] { new EnemyGroup("guard", 3), new EnemyGroup("hostile", 4), new EnemyGroup("armored", 1), new EnemyGroup("leader", 1, "office") });
            warehouse.civilians.AddRange(new[] { new CivilianGroup(CivilianType.SecurityGuard, 1), new CivilianGroup(CivilianType.OfficeWorker, 1), new CivilianGroup(CivilianType.Hostage, 1, "restricted") });

            var apartment = Mission("m04_apartment", "Operation Stairwell", "Marlow Court Apartments", MissionType.Emergency, "apartment", 3, 3, 660f, 7, 9, new Color(0.45f, 0.2f, 0.2f));
            apartment.levelNumber = 9;
            apartment.timeOfDay = TimeOfDay.Evening;
            apartment.description = "A dangerous suspect is barricaded in an apartment block full of residents.";
            apartment.briefing = "1950 hours. Shots were reported at Marlow Court. A wanted suspect and armed associates are moving between the second floor and the roof. "
                + "Evacuate the residents, investigate apartment 2B and the maintenance room, and take the suspect into custody. Use the stairwell to change floors; your squad will follow.";
            apartment.objectives.AddRange(new[]
            {
                O(ObjectiveType.RescueCivilians, "Evacuate all residents", 400),
                O(ObjectiveType.InvestigateRoom, "Investigate apartment 2B", 150, "unit2b"),
                O(ObjectiveType.InvestigateRoom, "Investigate the maintenance room", 150, "maintenance"),
                O(ObjectiveType.ApprehendLeader, "Secure the dangerous suspect", 300),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 200),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 200),
            });
            apartment.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 200),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 3 suspects", 150, null, 3),
                O(ObjectiveType.TreatInjured, "Treat every injured resident", 150),
            });
            apartment.enemies.AddRange(new[] { new EnemyGroup("hostile", 4), new EnemyGroup("nervous", 2), new EnemyGroup("armored", 1), new EnemyGroup("leader", 1, "upper") });
            apartment.civilians.AddRange(new[] { new CivilianGroup(CivilianType.Resident, 4), new CivilianGroup(CivilianType.Injured, 1), new CivilianGroup(CivilianType.Hiding, 1) });
            apartment.camerasActiveChance = 0.3f;
            apartment.alarmArmedChance = 0.4f;

            var list = new List<MissionData> { training, clearance, rescue, warehouse, apartment };
            list.AddRange(NewLevels());
            return list;
        }

        // Levels 1, 3, 5, 7, 8 and 10, each on its own map.
        static IEnumerable<MissionData> NewLevels()
        {
            var store = Mission("m05_store", "Operation Late Shift", "Brightwater Corner Mart", MissionType.BuildingClearance, "store", 1, 3, 300f, 0, 1, new Color(0.25f, 0.42f, 0.42f));
            store.levelNumber = 1;
            store.timeOfDay = TimeOfDay.Evening;
            store.description = "A robbery in progress at a corner store. A small, quick first job.";
            store.briefing = "1915 hours. A clerk at the Brightwater Corner Mart triggered a hold-up alarm. Several suspects are inside with the clerk and a few customers. "
                + "Go in through the front, shout before you shoot, get the civilians out to the safe zone and secure everyone else. Check the back office: the store's CCTV terminal is there.";
            store.objectives.AddRange(new[]
            {
                O(ObjectiveType.EnterBuilding, "Enter the store", 100),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.RescueCivilians, "Evacuate all civilians to the safe zone", 300),
                O(ObjectiveType.SecureRoom, "Secure the Office", 150, "office"),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            store.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 2 suspects", 150, null, 2),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 100),
                O(ObjectiveType.TimeLimit, "Finish within 5 minutes", 100, null, 300),
            });
            store.enemies.AddRange(new[] { new EnemyGroup("hostile", 2), new EnemyGroup("nervous", 1), new EnemyGroup("suspect_unarmed", 1) });
            store.civilians.AddRange(new[] { new CivilianGroup(CivilianType.OfficeWorker, 1, "shop"), new CivilianGroup(CivilianType.Visitor, 2), new CivilianGroup(CivilianType.Hiding, 1) });
            store.alarmArmedChance = 0.6f;
            store.camerasActiveChance = 0.8f;
            store.randomLockChance = 0.25f;
            store.optionalCount = 2;

            var motel = Mission("m06_motel", "Operation Vacancy", "Seaview Motor Inn", MissionType.Emergency, "motel", 2, 3, 480f, 1, 3, new Color(0.45f, 0.35f, 0.22f));
            motel.levelNumber = 3;
            motel.timeOfDay = TimeOfDay.Evening;
            motel.description = "A wanted suspect and his crew are holed up in a roadside motel full of guests.";
            motel.briefing = "2010 hours. A wanted suspect was seen entering the Seaview Motor Inn with armed associates, and guests report a hostage in one of the rooms. "
                + "Each guest room opens onto the parking lot, so stack up on every door. Search room 5, where the suspect was registered, evacuate the guests and take the suspect alive if you can.";
            motel.objectives.AddRange(new[]
            {
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.ApprehendLeader, "Secure the wanted suspect", 250),
                O(ObjectiveType.RescueCivilians, "Evacuate all guests to the safe zone", 300),
                O(ObjectiveType.InvestigateRoom, "Search Room 5", 150, "unit5"),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            motel.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 3 suspects", 150, null, 3),
                O(ObjectiveType.TreatInjured, "Treat every injured guest", 100),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
            });
            motel.enemies.AddRange(new[] { new EnemyGroup("hostile", 3), new EnemyGroup("nervous", 2), new EnemyGroup("suspect_unarmed", 1), new EnemyGroup("leader", 1, "unit5") });
            motel.civilians.AddRange(new[] { new CivilianGroup(CivilianType.Resident, 4), new CivilianGroup(CivilianType.Injured, 1), new CivilianGroup(CivilianType.Hostage, 1, "unit2") });
            motel.randomLockChance = 0.45f;
            motel.alarmArmedChance = 0.5f;
            motel.camerasActiveChance = 0.7f;

            var bank = Mission("m07_bank", "Operation Safe Deposit", "Sterling Mutual Bank", MissionType.CivilianRescue, "bank", 2, 3, 600f, 3, 5, new Color(0.22f, 0.38f, 0.3f));
            bank.levelNumber = 5;
            bank.timeOfDay = TimeOfDay.Day;
            bank.description = "An armed robbery has turned into a hostage situation at a bank branch.";
            bank.briefing = "1130 hours. An armed crew entered Sterling Mutual Bank minutes before a cash delivery. Staff and customers are being held in the banking hall while the crew works on the vault. "
                + "Get the hostages out, secure the vault and arrest the crew's leader. The vault and security room have electronic locks: use the teller terminal or the security console, or breach them.";
            bank.objectives.AddRange(new[]
            {
                O(ObjectiveType.RescueCivilians, "Evacuate all hostages and staff", 400),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.ApprehendLeader, "Arrest the crew leader", 250),
                O(ObjectiveType.SecureRoom, "Secure the Vault", 200, "vault"),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            bank.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 200),
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 150),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.TimeLimit, "Finish within 9 minutes", 150, null, 540),
            });
            bank.enemies.AddRange(new[] { new EnemyGroup("hostile", 5), new EnemyGroup("armored", 1), new EnemyGroup("nervous", 1), new EnemyGroup("leader", 1, "vault") });
            bank.civilians.AddRange(new[]
            {
                new CivilianGroup(CivilianType.OfficeWorker, 3), new CivilianGroup(CivilianType.Visitor, 3),
                new CivilianGroup(CivilianType.Hostage, 2, "hall"), new CivilianGroup(CivilianType.Hiding, 1),
            });
            bank.alarmArmedChance = 0.8f;
            bank.randomLockChance = 0.35f;

            var clinic = Mission("m08_clinic", "Operation Triage", "Harbor Street Clinic", MissionType.CivilianRescue, "clinic", 2, 3, 600f, 5, 7, new Color(0.3f, 0.45f, 0.5f));
            clinic.levelNumber = 7;
            clinic.timeOfDay = TimeOfDay.Evening;
            clinic.powerOutageChance = 0.3f;
            clinic.description = "Armed robbers are after the clinic's pharmacy. Patients and staff are caught in the middle.";
            clinic.briefing = "1840 hours. A group forced its way into the Harbor Street Clinic looking for the pharmacy stock. Several patients were hurt in the panic and staff are hiding in the wards. "
                + "Treat the injured, get everyone out and secure the pharmacy. Medical kits will be in short supply: plan who carries them.";
            clinic.objectives.AddRange(new[]
            {
                O(ObjectiveType.RescueCivilians, "Evacuate all patients and staff", 350),
                O(ObjectiveType.TreatInjured, "Treat every injured patient", 250),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.SecureRoom, "Secure the Pharmacy", 200, "pharmacy"),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            clinic.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 200),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 4 suspects", 150, null, 4),
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 100),
                O(ObjectiveType.TimeLimit, "Finish within 10 minutes", 150, null, 600),
            });
            clinic.enemies.AddRange(new[] { new EnemyGroup("hostile", 4), new EnemyGroup("nervous", 2), new EnemyGroup("guard", 1), new EnemyGroup("armored", 1) });
            clinic.civilians.AddRange(new[]
            {
                new CivilianGroup(CivilianType.Injured, 3), new CivilianGroup(CivilianType.OfficeWorker, 2),
                new CivilianGroup(CivilianType.Visitor, 2), new CivilianGroup(CivilianType.Hiding, 1),
            });
            clinic.bonusEquipment.Add(new EquipmentCount("medkit", 2));
            clinic.alarmArmedChance = 0.6f;
            clinic.camerasActiveChance = 0.6f;

            var club = Mission("m09_nightclub", "Operation Last Call", "Club Halcyon", MissionType.Investigation, "nightclub", 3, 3, 660f, 6, 8, new Color(0.42f, 0.18f, 0.45f));
            club.levelNumber = 8;
            club.night = true;
            club.timeOfDay = TimeOfDay.Night;
            club.powerOutageChance = 0.5f;
            club.description = "A crowded nightclub is the front for an armed crew. Find the evidence and get the crowd out.";
            club.briefing = "0110 hours. Club Halcyon's manager is suspected of running an armed crew from the back office. A fight broke out and shots were fired; the crowd is trapped inside and the lights may go out. "
                + "Evacuate the patrons, secure two pieces of evidence and arrest the manager. Bring flashlights and expect people everywhere: check your targets.";
            club.objectives.AddRange(new[]
            {
                O(ObjectiveType.RescueCivilians, "Evacuate all patrons and staff", 400),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.ApprehendLeader, "Arrest the club manager", 250),
                O(ObjectiveType.SecureEvidence, "Secure 2 pieces of evidence", 200, null, 2),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            club.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 200),
                O(ObjectiveType.TreatInjured, "Treat every injured patron", 150),
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 150),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 150),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
            });
            club.enemies.AddRange(new[] { new EnemyGroup("hostile", 4), new EnemyGroup("guard", 2), new EnemyGroup("armored", 1), new EnemyGroup("nervous", 1), new EnemyGroup("leader", 1, "office") });
            club.civilians.AddRange(new[]
            {
                new CivilianGroup(CivilianType.Visitor, 5, "dance"), new CivilianGroup(CivilianType.Hiding, 2),
                new CivilianGroup(CivilianType.Injured, 1), new CivilianGroup(CivilianType.OfficeWorker, 1),
            });
            club.bonusEquipment.Add(new EquipmentCount("portable_light", 1));
            club.alarmArmedChance = 0.7f;

            var factory = Mission("m10_factory", "Operation Iron Gate", "Riverside Steelworks", MissionType.Investigation, "factory", 3, 3, 900f, 8, 10, new Color(0.38f, 0.3f, 0.26f));
            factory.levelNumber = 10;
            factory.night = true;
            factory.timeOfDay = TimeOfDay.Night;
            factory.powerOutageChance = 0.35f;
            factory.description = "The final operation: take down a heavily armed smuggling crew in a closed steelworks.";
            factory.briefing = "0300 hours. The crew behind the Kestrel Freight shipments is using the closed Riverside Steelworks as a base. Expect armored suspects, guards on patrol and a night watchman held in the boiler room. "
                + "Get into the control room, access the plant console, secure three pieces of evidence and arrest the crew's leader. The control room door is electronic: the foreman's terminal can open it.";
            factory.objectives.AddRange(new[]
            {
                O(ObjectiveType.UseConsole, "Access the plant control console", 200, "plant_console"),
                O(ObjectiveType.SecureEvidence, "Secure 3 pieces of evidence", 300, null, 3),
                O(ObjectiveType.ApprehendLeader, "Arrest the crew leader", 300),
                O(ObjectiveType.SecureSuspects, "Secure all suspects", 300),
                O(ObjectiveType.RescueCivilians, "Evacuate all civilians", 250),
                O(ObjectiveType.ReachExtraction, "Return to the SWAT van", 150),
            });
            factory.optionalPool.AddRange(new[]
            {
                O(ObjectiveType.AlarmNotTriggered, "Don't let the alarm go off", 200),
                O(ObjectiveType.DisableCameras, "Disable the security cameras", 100),
                O(ObjectiveType.NoOfficerDown, "No officer goes down", 200),
                O(ObjectiveType.NoCivilianCasualties, "No civilian casualties", 150),
                O(ObjectiveType.ArrestSuspects, "Arrest at least 5 suspects", 200, null, 5),
            });
            factory.enemies.AddRange(new[]
            {
                new EnemyGroup("hostile", 6), new EnemyGroup("guard", 3), new EnemyGroup("armored", 2),
                new EnemyGroup("nervous", 1), new EnemyGroup("leader", 1, "control"),
            });
            factory.civilians.AddRange(new[] { new CivilianGroup(CivilianType.SecurityGuard, 1), new CivilianGroup(CivilianType.OfficeWorker, 2), new CivilianGroup(CivilianType.Hostage, 1, "boiler") });
            factory.bonusEquipment.Add(new EquipmentCount("breach_charge", 1));
            factory.alarmArmedChance = 0.9f;
            factory.randomLockChance = 0.4f;

            return new[] { store, motel, bank, clinic, club, factory };
        }
    }
}
