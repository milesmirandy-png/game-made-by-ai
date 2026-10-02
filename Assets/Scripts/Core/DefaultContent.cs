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
                Weapon("shotgun_ts8", "TS8 Tactical Shotgun", WeaponCategory.Shotgun, false, FireMode.SemiAuto, false, 13f, 1.3f, 7, 28, 2.8f, 14f, 9f, 3f, 8, 28f, 0.95f, 0.6f, Sound.Shotgun,
                    Roles(OfficerRole.Leader, OfficerRole.Breacher, OfficerRole.Tactical), "Wide spread, slow reload. Devastating up close, poor at range."),
                Weapon("carbine_pc9", "PC9 Precision Carbine", WeaponCategory.Carbine, false, FireMode.SemiAuto, false, 45f, 3f, 15, 60, 2.4f, 60f, 0.6f, 2f, 1, 30f, 0.9f, 0.65f, Sound.Carbine,
                    Roles(OfficerRole.Leader, OfficerRole.Recon, OfficerRole.Tactical), "Accurate and long-ranged, but slow to fire."),
                Weapon("launcher_ll40", "LL40 Less-Lethal Launcher", WeaponCategory.LessLethal, false, FireMode.SemiAuto, false, 8f, 1f, 5, 20, 2.6f, 25f, 1.5f, 2.5f, 1, 12f, 0.95f, 0.6f, Sound.LessLethal, 0,
                    "Fires low-damage impact rounds that stagger and stun. Doesn't stop everyone."),
                Weapon("pistol_p17", "P17 Service Pistol", WeaponCategory.ServicePistol, true, FireMode.SemiAuto, false, 28f, 5f, 15, 60, 1.3f, 28f, 2.2f, 1.4f, 1, 20f, 1f, 0.3f, Sound.Pistol, 0,
                    "Dependable standard sidearm."),
                Weapon("pistol_bk6", "BK6 Backup Pistol", WeaponCategory.BackupPistol, true, FireMode.SemiAuto, false, 22f, 6f, 8, 40, 1.1f, 20f, 3f, 1.2f, 1, 18f, 1f, 0.25f, Sound.Pistol, 0,
                    "Small and quick to draw, with a small magazine."),
                Weapon("pistol_h50", "H50 Heavy Sidearm", WeaponCategory.HeavyPistol, true, FireMode.SemiAuto, false, 48f, 2.2f, 7, 35, 1.6f, 30f, 2.5f, 3.2f, 1, 26f, 1f, 0.35f, Sound.HeavyPistol, 0,
                    "Hits hard, kicks hard, fires slowly."),
            };
            var launcher = list[6];
            launcher.lessLethal = true;
            launcher.stunDuration = 3f;
            launcher.tracerColor = new Color(1f, 0.6f, 0.2f);
            return list;
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

        public static List<AttachmentData> Attachments()
        {
            return new List<AttachmentData>
            {
                Attachment("light_weapon", "Weapon Light", AttachmentSlot.Light, 1f, 1f, 1f, 1f, 1.5f, 0, "A brighter, longer flashlight beam."),
                Attachment("optic_reddot", "Red Dot Optic", AttachmentSlot.Optic, 0.85f, 1f, 1f, 1f, 1f, 1, "Slightly tighter spread."),
                Attachment("muzzle_suppressor", "Suppressor", AttachmentSlot.Muzzle, 1f, 0.95f, 0.75f, 1f, 1f, 1, "Mostly cosmetic; shots carry a little less far."),
                Attachment("stock_collapsible", "Collapsible Stock", AttachmentSlot.Stock, 1f, 1.05f, 1f, 1.03f, 1f, 0, "Cosmetic variant; marginally quicker handling."),
                Attachment("stock_fixed", "Fixed Stock", AttachmentSlot.Stock, 1f, 0.9f, 1f, 0.98f, 1f, 2, "Cosmetic variant; marginally steadier."),
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
                Armor("armor_light", "Light Vest", 1, 0.2f, 1.05f, 1, 70f, false, new Color(0.22f, 0.24f, 0.26f), "Fast and roomy, but offers modest protection."),
                Armor("armor_standard", "Standard Plate Carrier", 2, 0.35f, 1f, 0, 100f, true, new Color(0.08f, 0.09f, 0.11f), "Balanced protection with a helmet."),
                Armor("armor_heavy", "Heavy Armor", 3, 0.5f, 0.88f, -1, 140f, true, new Color(0.04f, 0.04f, 0.05f), "Strong protection; slower and carries less."),
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

            var clearance = Mission("m01_clearance", "Operation Glass Desk", "Halvorsen Logistics Offices", MissionType.BuildingClearance, "office", 1, 3, 480f, 0, 1, new Color(0.2f, 0.32f, 0.5f));
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

            var rescue = Mission("m02_rescue", "Operation Lantern", "Halvorsen Logistics Offices (night)", MissionType.CivilianRescue, "office", 2, 3, 540f, 0, 2, new Color(0.3f, 0.25f, 0.45f));
            rescue.night = true;
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

            var warehouse = Mission("m03_warehouse", "Operation Cold Storage", "Kestrel Freight Warehouse", MissionType.Investigation, "warehouse", 2, 3, 600f, 1, 3, new Color(0.4f, 0.32f, 0.2f));
            warehouse.description = "A silent alarm at a freight warehouse. Find out what's going on.";
            warehouse.briefing = "0230 hours. Kestrel Freight's silent alarm tripped and the night guards stopped answering. Intelligence suggests a smuggling crew is moving goods through the building. "
                + "Reach the security booth and review the camera footage to locate the evidence, secure it, and detain the crew. Their leader may try to slip out the back.";
            warehouse.night = true;
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

            var apartment = Mission("m04_apartment", "Operation Stairwell", "Marlow Court Apartments", MissionType.Emergency, "apartment", 3, 3, 660f, 2, 4, new Color(0.45f, 0.2f, 0.2f));
            apartment.night = true;
            apartment.description = "A dangerous suspect is barricaded in an apartment block full of residents.";
            apartment.briefing = "2310 hours. Shots were reported at Marlow Court. A wanted suspect and armed associates are moving between the second floor and the roof. "
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

            return new List<MissionData> { training, clearance, rescue, warehouse, apartment };
        }
    }
}
