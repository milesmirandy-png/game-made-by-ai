using UnityEngine;

namespace Swat
{
    // The 3D gun in a character's hands. Most weapons use a model from the
    // imported low-poly weapon pack (ImportedModel says which); the rest are
    // built out of boxes, in the same colours as the pixel-art sprites:
    // charcoal steel, dark steel and the weapon's accent colour for the
    // furniture (stock, grip, handguard). If the WeaponData has a modelPrefab,
    // that is used instead. +z is the muzzle.
    public static class WeaponModels
    {
        // Charcoal steel and polymer, as in the weapon sprites.
        static readonly Color Steel = new Color(0.34f, 0.35f, 0.34f);
        static readonly Color DarkSteel = new Color(0.14f, 0.14f, 0.13f);
        static readonly Color Polymer = new Color(0.2f, 0.2f, 0.19f);
        static readonly Color Orange = new Color(0.95f, 0.5f, 0.1f);
        static readonly Color Yellow = new Color(0.95f, 0.78f, 0.12f);

        // Which imported model each weapon uses (the closest look in the pack). Weapons not listed
        // (the LMG, rotary gun, launchers, auto and drum shotguns, pepperball and stun pistol) stay boxes.
        public static string ImportedModel(string weaponId)
        {
            switch (weaponId)
            {
                case "rifle_compact": return "gun_ak";
                case "rifle_service": case "rifle_b4": return "gun_m4";
                case "rifle_cx": return "gun_bullpup";
                case "dmr_dm2": return "gun_awp";
                case "carbine_pc9": return "gun_bolt";
                case "pdw_x4": return "gun_p90";
                case "smg_compact": return "gun_uzi";
                case "smg_v10": return "gun_mp5";
                case "mp_m9": return "gun_skorpion";
                case "smg_kv": return "gun_vector";
                case "shotgun_ts8": return "gun_pump";
                case "pistol_p17": return "gun_pistol";
                case "pistol_bk6": return "gun_pistol2";
                case "pistol_h50": return "gun_heavy";
                case "revolver_r6": return "gun_revolver";
                default: return null;
            }
        }

        // Models that already have a scope or sight on top (no extra optic box).
        static bool HasOwnSight(string modelId)
        {
            return modelId == "gun_awp" || modelId == "gun_bolt" || modelId == "gun_bullpup" || modelId == "gun_p90";
        }

        // Where attachments go on a gun, in gun space (+z to the muzzle, +y up, origin at the grip).
        public struct Mounts
        {
            public float front, muzzleY;        // the muzzle
            public float railY, railZ;          // top of the receiver, where an optic sits
            public float underY, underZ;        // underside of the handguard (grips)
            public float sideX, sideY, sideZ;   // side of the handguard (light on the right, laser on the left)
            public float magY, magZ;            // bottom of the magazine
            public float rearZ, stockY;         // back end of the stock
            public bool ownSight;               // the model already has a scope or sight on top
        }

        // What the built gun ended up with: first person aims through the optic and puts the laser dot out.
        public struct GunInfo
        {
            public float front;       // muzzle distance from the grip, after any muzzle device
            public bool hasSight;     // an optic to look through (red dot, reflex, holographic)
            public float sightY;      // its centre line, above the grip
            public float zoom;        // magnified optic (1 = none)
            public bool hasLaser;
            public Vector3 laser;     // where the laser beam starts
        }

        // Returns the distance from the grip to the muzzle.
        public static float Build(WeaponData weapon, Transform parent, OfficerLoadout attachments)
        {
            return Build(weapon, parent, attachments, null);
        }

        // modelOverride: use this imported model instead of the usual one (suspects' variety).
        public static float Build(WeaponData weapon, Transform parent, OfficerLoadout attachments, string modelOverride)
        {
            GunInfo info;
            return Build(weapon, parent, attachments, modelOverride, out info);
        }

        public static float Build(WeaponData weapon, Transform parent, OfficerLoadout attachments, string modelOverride, out GunInfo info)
        {
            info = new GunInfo { zoom = 1f };
            if (weapon.modelPrefab != null)
            {
                var model = Object.Instantiate(weapon.modelPrefab, parent, false);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                var muzzle = model.transform.Find("Muzzle");
                info.front = muzzle != null ? muzzle.localPosition.z : 0.5f;
                return info.front;
            }
            string imported = SaveManager.Settings.classicCharacters ? null : modelOverride ?? ImportedModel(weapon.id);
            var importedModel = ModelLibrary.Get(imported);
            if (importedModel != null && ModelLibrary.Spawn(importedModel, "gun", parent, Vector3.zero) != null)
            {
                var point = importedModel.Find("muzzle");
                var gun = importedModel.Find("gun");
                var mounts = Measure(gun.mesh, point != null ? point.pivot.z : 0.5f, point != null ? point.pivot.y : 0.015f);
                mounts.ownSight = HasOwnSight(imported);
                info.front = mounts.front;
                if (attachments != null && !weapon.isSidearm) Attach(parent, mounts, attachments, ref info);
                return info.front;
            }

            Color wood = weapon.accent;
            float muzzleZ;
            bool rail = false;
            switch (weapon.category)
            {
                case WeaponCategory.CompactSMG:
                    Box(parent, 0f, 0f, 0.14f, 0.07f, 0.11f, 0.32f, Steel);
                    Box(parent, 0f, 0.01f, 0.34f, 0.03f, 0.03f, 0.08f, DarkSteel);
                    Box(parent, 0f, -0.13f, 0.12f, 0.045f, 0.2f, 0.06f, Polymer);
                    Box(parent, 0f, -0.01f, -0.06f, 0.04f, 0.05f, 0.12f, DarkSteel);
                    muzzleZ = 0.38f;
                    break;
                case WeaponCategory.SMG:
                case WeaponCategory.PDW:
                    bool pdw = weapon.category == WeaponCategory.PDW;
                    Box(parent, 0f, 0f, 0.16f, pdw ? 0.08f : 0.07f, pdw ? 0.13f : 0.11f, 0.38f, pdw ? wood : Steel);
                    Box(parent, 0f, 0.01f, 0.4f, 0.03f, 0.03f, 0.1f, DarkSteel);
                    if (pdw) Box(parent, 0f, 0.075f, 0.16f, 0.06f, 0.03f, 0.3f, Polymer); // magazine on top
                    else
                    {
                        Box(parent, 0f, 0.005f, 0.33f, 0.065f, 0.08f, 0.12f, Polymer);
                        Box(parent, 0f, -0.13f, 0.24f, 0.04f, 0.18f, 0.05f, DarkSteel).localRotation = Quaternion.Euler(-10f, 0f, 0f);
                    }
                    Box(parent, 0f, -0.1f, 0.08f, 0.045f, 0.13f, 0.05f, pdw ? wood : Polymer);
                    Box(parent, 0f, -0.01f, -0.1f, 0.05f, 0.08f, 0.18f, pdw ? wood : DarkSteel);
                    muzzleZ = 0.46f;
                    rail = !pdw;
                    break;
                case WeaponCategory.CompactRifle:
                case WeaponCategory.Rifle:
                case WeaponCategory.BurstRifle:
                {
                    bool full = weapon.category == WeaponCategory.Rifle;
                    float barrel = full ? 0.3f : weapon.category == WeaponCategory.BurstRifle ? 0.24f : 0.18f;
                    Box(parent, 0f, 0f, 0.13f, 0.07f, 0.11f, 0.34f, Steel);
                    Box(parent, 0f, 0.005f, 0.3f + barrel * 0.5f, 0.065f, 0.08f, barrel, wood);
                    Box(parent, 0f, 0.015f, 0.32f + barrel + 0.06f, 0.03f, 0.03f, 0.12f, DarkSteel);
                    Box(parent, 0f, -0.13f, 0.2f, 0.045f, 0.17f, 0.075f, weapon.category == WeaponCategory.BurstRifle ? Polymer : DarkSteel).localRotation = Quaternion.Euler(full ? 14f : 6f, 0f, 0f);
                    Box(parent, 0f, -0.1f, 0.04f, 0.045f, 0.13f, 0.05f, wood).localRotation = Quaternion.Euler(-18f, 0f, 0f);
                    Box(parent, 0f, -0.02f, -0.14f, 0.055f, 0.1f, 0.24f, full ? wood : Polymer);
                    muzzleZ = 0.38f + barrel + 0.06f;
                    rail = !full;
                    break;
                }
                case WeaponCategory.Bullpup:
                    Box(parent, 0f, 0f, 0.12f, 0.08f, 0.13f, 0.56f, wood);
                    Box(parent, 0f, 0.01f, 0.46f, 0.03f, 0.03f, 0.14f, DarkSteel);
                    Box(parent, 0f, -0.12f, -0.06f, 0.045f, 0.14f, 0.07f, DarkSteel);
                    Box(parent, 0f, -0.1f, 0.12f, 0.045f, 0.12f, 0.05f, Polymer);
                    muzzleZ = 0.53f;
                    rail = true;
                    break;
                case WeaponCategory.Carbine:
                case WeaponCategory.Marksman:
                {
                    bool scope = weapon.category == WeaponCategory.Marksman;
                    Box(parent, 0f, 0f, 0.13f, 0.07f, 0.1f, 0.3f, Steel);
                    Box(parent, 0f, 0.01f, 0.58f, 0.035f, 0.035f, 0.56f, DarkSteel);
                    Box(parent, 0f, -0.01f, 0.4f, 0.06f, 0.07f, 0.34f, wood);
                    Box(parent, 0f, -0.1f, 0.18f, 0.04f, 0.1f, 0.07f, DarkSteel);
                    Box(parent, 0f, -0.02f, -0.15f, 0.06f, 0.11f, 0.28f, wood);
                    if (scope)
                    {
                        Box(parent, 0f, 0.1f, 0.14f, 0.05f, 0.05f, 0.28f, DarkSteel);
                        Box(parent, 0f, 0.1f, 0.285f, 0.04f, 0.04f, 0.01f, new Color(0.38f, 0.72f, 1f), 1.2f);
                    }
                    muzzleZ = 0.86f;
                    break;
                }
                case WeaponCategory.LMG:
                    Box(parent, 0f, 0f, 0.14f, 0.09f, 0.12f, 0.38f, Steel);
                    Box(parent, 0f, 0.01f, 0.5f, 0.07f, 0.08f, 0.3f, Polymer);
                    Box(parent, 0f, 0.015f, 0.74f, 0.04f, 0.04f, 0.22f, DarkSteel);
                    Box(parent, 0f, -0.14f, 0.16f, 0.1f, 0.16f, 0.14f, wood); // box magazine
                    Box(parent, 0f, 0.1f, 0.18f, 0.03f, 0.03f, 0.16f, DarkSteel); // carry handle
                    Box(parent, -0.03f, -0.08f, 0.76f, 0.015f, 0.12f, 0.015f, DarkSteel).localRotation = Quaternion.Euler(25f, 0f, 0f);
                    Box(parent, 0.03f, -0.08f, 0.76f, 0.015f, 0.12f, 0.015f, DarkSteel).localRotation = Quaternion.Euler(25f, 0f, 0f);
                    Box(parent, 0f, -0.02f, -0.16f, 0.06f, 0.11f, 0.26f, Polymer);
                    muzzleZ = 0.86f;
                    break;
                case WeaponCategory.Shotgun:
                    Box(parent, 0f, 0f, 0.06f, 0.07f, 0.1f, 0.24f, Steel);
                    Box(parent, 0f, 0.02f, 0.42f, 0.04f, 0.04f, 0.56f, DarkSteel);
                    Box(parent, 0f, -0.03f, 0.36f, 0.04f, 0.04f, 0.42f, DarkSteel);
                    Box(parent, 0f, -0.035f, 0.42f, 0.065f, 0.065f, 0.16f, wood);
                    Box(parent, 0f, -0.04f, -0.2f, 0.06f, 0.11f, 0.3f, wood);
                    muzzleZ = 0.7f;
                    break;
                case WeaponCategory.AutoShotgun:
                    Box(parent, 0f, 0f, 0.12f, 0.08f, 0.12f, 0.36f, Steel);
                    Box(parent, 0f, 0.01f, 0.42f, 0.045f, 0.045f, 0.26f, DarkSteel);
                    Box(parent, 0f, 0.01f, 0.56f, 0.07f, 0.06f, 0.05f, DarkSteel);
                    Box(parent, 0f, -0.14f, 0.18f, 0.06f, 0.17f, 0.1f, DarkSteel);
                    Box(parent, 0f, -0.1f, 0.03f, 0.045f, 0.13f, 0.05f, wood);
                    Box(parent, 0f, -0.02f, -0.16f, 0.06f, 0.1f, 0.26f, wood);
                    muzzleZ = 0.58f;
                    rail = true;
                    break;
                case WeaponCategory.LessLethal:
                case WeaponCategory.Pepperball:
                {
                    bool pepper = weapon.category == WeaponCategory.Pepperball;
                    if (pepper)
                    {
                        Box(parent, 0f, 0.01f, 0.32f, 0.035f, 0.035f, 0.3f, DarkSteel);
                        Box(parent, 0f, 0.13f, 0.12f, 0.12f, 0.12f, 0.16f, wood); // hopper
                    }
                    else
                    {
                        var tube = Shapes.Make(PrimitiveType.Cylinder, "Tube", parent, new Vector3(0f, 0.01f, 0.25f), new Vector3(0.11f, 0.2f, 0.11f), DarkSteel, false).transform;
                        tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        Box(parent, 0f, 0.01f, 0.43f, 0.12f, 0.12f, 0.04f, Orange);
                    }
                    Box(parent, 0f, 0f, 0.08f, 0.07f, 0.1f, 0.22f, pepper ? Polymer : Steel);
                    Box(parent, 0f, -0.1f, 0.05f, 0.04f, 0.12f, 0.05f, pepper ? Polymer : Orange);
                    Box(parent, 0f, -0.02f, -0.12f, 0.05f, 0.08f, 0.2f, pepper ? wood : DarkSteel);
                    muzzleZ = pepper ? 0.48f : 0.46f;
                    break;
                }
                case WeaponCategory.Rotary:
                    for (int i = 0; i < 3; i++)
                        Box(parent, (i - 1) * 0.035f, 0.01f + (i == 1 ? 0.03f : 0f), 0.48f, 0.03f, 0.03f, 0.5f, i == 1 ? DarkSteel : Steel);
                    Box(parent, 0f, 0.02f, 0.3f, 0.12f, 0.1f, 0.04f, DarkSteel);
                    Box(parent, 0f, 0.02f, 0.6f, 0.12f, 0.1f, 0.04f, DarkSteel);
                    Box(parent, 0f, 0.01f, 0.06f, 0.14f, 0.15f, 0.3f, Steel);
                    Box(parent, 0f, 0.11f, 0.06f, 0.03f, 0.04f, 0.2f, DarkSteel);
                    Box(parent, 0.11f, -0.07f, 0.04f, 0.1f, 0.14f, 0.18f, wood); // ammo box
                    Box(parent, 0f, -0.06f, -0.12f, 0.05f, 0.12f, 0.05f, Polymer);
                    muzzleZ = 0.74f;
                    break;
                case WeaponCategory.DrumShotgun:
                    Box(parent, 0f, 0f, 0.12f, 0.08f, 0.12f, 0.36f, Steel);
                    Box(parent, 0f, 0.01f, 0.42f, 0.045f, 0.045f, 0.26f, DarkSteel);
                    Box(parent, 0f, 0.01f, 0.56f, 0.07f, 0.06f, 0.05f, DarkSteel);
                    var drum = Shapes.Make(PrimitiveType.Cylinder, "Drum", parent, new Vector3(0f, -0.13f, 0.18f), new Vector3(0.2f, 0.05f, 0.2f), wood, false).transform;
                    drum.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Box(parent, 0f, -0.1f, 0.0f, 0.045f, 0.13f, 0.05f, Polymer);
                    Box(parent, 0f, -0.02f, -0.17f, 0.06f, 0.1f, 0.26f, Polymer);
                    muzzleZ = 0.58f;
                    rail = true;
                    break;
                case WeaponCategory.VectorSMG:
                    Box(parent, 0f, 0.01f, 0.14f, 0.07f, 0.11f, 0.34f, wood);
                    Box(parent, 0f, -0.07f, 0.12f, 0.065f, 0.1f, 0.2f, wood).localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    Box(parent, 0f, 0.01f, 0.4f, 0.045f, 0.045f, 0.2f, DarkSteel);
                    Box(parent, 0f, -0.15f, 0.2f, 0.04f, 0.17f, 0.06f, DarkSteel);
                    Box(parent, 0f, -0.01f, -0.12f, 0.04f, 0.06f, 0.18f, Polymer);
                    muzzleZ = 0.5f;
                    rail = true;
                    break;
                case WeaponCategory.GrenadeLauncher:
                    var cylinder = Shapes.Make(PrimitiveType.Cylinder, "Drum", parent, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.17f, 0.08f, 0.17f), wood, false).transform;
                    cylinder.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Box(parent, 0f, 0.03f, 0.36f, 0.09f, 0.09f, 0.26f, DarkSteel);
                    Box(parent, 0f, -0.08f, 0.33f, 0.04f, 0.1f, 0.05f, Polymer);
                    Box(parent, 0f, -0.08f, 0.0f, 0.045f, 0.12f, 0.05f, wood);
                    Box(parent, 0f, 0f, -0.17f, 0.05f, 0.07f, 0.24f, DarkSteel);
                    muzzleZ = 0.5f;
                    break;
                case WeaponCategory.Revolver:
                    Box(parent, 0f, 0.02f, 0.16f, 0.035f, 0.045f, 0.22f, Steel);
                    Box(parent, 0f, 0.01f, 0.02f, 0.06f, 0.07f, 0.08f, Steel);
                    Box(parent, 0f, -0.07f, -0.04f, 0.04f, 0.12f, 0.06f, wood).localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    muzzleZ = 0.27f;
                    break;
                case WeaponCategory.StunPistol:
                    Box(parent, 0f, 0.02f, 0.08f, 0.055f, 0.07f, 0.18f, Polymer);
                    Box(parent, 0f, 0.02f, 0.2f, 0.06f, 0.075f, 0.06f, Yellow);
                    Box(parent, 0f, -0.06f, -0.01f, 0.045f, 0.11f, 0.05f, Polymer).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    muzzleZ = 0.23f;
                    break;
                case WeaponCategory.MachinePistol:
                    Box(parent, 0f, 0.02f, 0.1f, 0.045f, 0.055f, 0.22f, Steel);
                    Box(parent, 0f, 0.02f, 0.235f, 0.05f, 0.05f, 0.05f, DarkSteel);
                    Box(parent, 0f, -0.08f, -0.01f, 0.035f, 0.17f, 0.05f, Polymer).localRotation = Quaternion.Euler(-10f, 0f, 0f);
                    muzzleZ = 0.26f;
                    break;
                case WeaponCategory.HeavyPistol:
                    Box(parent, 0f, 0.02f, 0.11f, 0.05f, 0.06f, 0.26f, Steel);
                    Box(parent, 0f, -0.06f, -0.01f, 0.04f, 0.12f, 0.06f, wood).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    muzzleZ = 0.25f;
                    break;
                default: // service and backup pistols
                    bool backup = weapon.category == WeaponCategory.BackupPistol;
                    Box(parent, 0f, 0.02f, backup ? 0.07f : 0.09f, 0.04f, 0.05f, backup ? 0.15f : 0.2f, Steel);
                    Box(parent, 0f, -0.05f, -0.01f, 0.035f, 0.1f, 0.05f, Polymer).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    muzzleZ = backup ? 0.15f : 0.2f;
                    break;
            }

            // A highlight strip along the top of long guns (it catches the light like the sprites' glint)
            // and a short top rail where the sprite has one.
            if (!weapon.isSidearm) Box(parent, 0f, 0.058f, 0.13f, 0.03f, 0.008f, 0.22f, Shapes.Shade(Steel, 1.5f));
            if (rail) Box(parent, 0f, 0.068f, 0.16f, 0.028f, 0.012f, 0.26f, DarkSteel);

            info.front = muzzleZ;
            if (attachments != null && !weapon.isSidearm)
            {
                var mounts = new Mounts
                {
                    front = muzzleZ, muzzleY = 0.015f,
                    railY = rail ? 0.074f : 0.062f, railZ = 0.12f,
                    underY = -0.05f, underZ = muzzleZ * 0.55f,
                    sideX = 0.05f, sideY = -0.005f, sideZ = muzzleZ * 0.62f,
                    magY = -0.2f, magZ = 0.2f,
                    rearZ = -0.27f, stockY = -0.02f,
                    ownSight = weapon.category == WeaponCategory.Carbine || weapon.category == WeaponCategory.Marksman,
                };
                Attach(parent, mounts, attachments, ref info);
            }
            return info.front;
        }

        // ---- Attachments ----

        static readonly Color LightBody = new Color(0.7f, 0.7f, 0.66f);
        static readonly Color Coyote = new Color(0.47f, 0.38f, 0.25f);
        static readonly Color Reticle = new Color(1f, 0.12f, 0.08f);

        // Mount points from the model's own shape: the highest point over the receiver, the
        // underside and sides of the handguard, the bottom of the magazine and the back of the stock.
        static Mounts Measure(Mesh mesh, float front, float muzzleY)
        {
            var m = new Mounts { front = front, muzzleY = muzzleY };
            var v = mesh != null ? mesh.vertices : new Vector3[0];
            var t = mesh != null ? mesh.triangles : new int[0];
            float rear = front;
            foreach (var p in v) if (p.z < rear) rear = p.z;
            m.rearZ = rear;
            float lo, hi, wide;
            m.railZ = Mathf.Clamp(front * 0.06f, -0.04f, 0.08f);
            Span(v, t, m.railZ - 0.035f, m.railZ + 0.035f, out lo, out hi, out wide);
            m.railY = hi;
            m.underZ = front * 0.5f;
            Span(v, t, m.underZ - 0.025f, m.underZ + 0.025f, out lo, out hi, out wide);
            m.underY = lo;
            m.sideZ = front * 0.62f;
            Span(v, t, m.sideZ - 0.02f, m.sideZ + 0.02f, out lo, out hi, out wide);
            m.sideY = (lo + hi) * 0.5f;
            m.sideX = wide + 0.016f;
            m.magZ = front * 0.2f;
            Span(v, t, m.magZ - 0.02f, m.magZ + 0.02f, out lo, out hi, out wide);
            m.magY = lo;
            Span(v, t, rear + 0.01f, rear + 0.05f, out lo, out hi, out wide);
            m.stockY = (lo + hi) * 0.5f;
            return m;
        }

        // The gun's outline across a band of its length: the low-poly models have long faces with no
        // vertices in the middle, so each triangle is cut by slices through the band.
        static void Span(Vector3[] v, int[] triangles, float z0, float z1, out float lo, out float hi, out float wide)
        {
            lo = 0f; hi = 0f; wide = 0.02f;
            bool any = false;
            for (int k = 0; k <= 2; k++)
            {
                float z = Mathf.Lerp(z0, z1, k * 0.5f);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = v[triangles[i + e]], b = v[triangles[i + (e + 1) % 3]];
                        if ((a.z - z) * (b.z - z) > 0f) continue;
                        Vector3 p = Mathf.Abs(b.z - a.z) < 1e-6f ? a : Vector3.Lerp(a, b, (z - a.z) / (b.z - a.z));
                        if (!any || p.y < lo) lo = p.y;
                        if (!any || p.y > hi) hi = p.y;
                        if (Mathf.Abs(p.x) > wide) wide = Mathf.Abs(p.x);
                        any = true;
                    }
            }
            if (!any) { lo = -0.04f; hi = 0.05f; }
        }

        // Builds the loadout's attachments at the gun's mount points.
        static void Attach(Transform parent, Mounts m, OfficerLoadout loadout, ref GunInfo info)
        {
            foreach (var id in Weapon.Ids(loadout))
            {
                var attachment = GameData.Attachment(id);
                if (attachment == null) continue;
                switch (attachment.look)
                {
                    case AttachmentLook.Light:
                    {
                        bool bright = attachment.lightRangeMultiplier > 1.7f;
                        float size = bright ? 0.036f : 0.03f;
                        Box(parent, m.sideX, m.sideY, m.sideZ, size, size, bright ? 0.09f : 0.075f, bright ? DarkSteel : LightBody);
                        Box(parent, m.sideX, m.sideY, m.sideZ + (bright ? 0.047f : 0.04f), size * 0.85f, size * 0.85f, 0.006f, new Color(1f, 0.95f, 0.75f), 0.8f);
                        break;
                    }
                    case AttachmentLook.Laser:
                        Box(parent, -m.sideX, m.sideY, m.sideZ, 0.026f, 0.022f, 0.05f, DarkSteel);
                        Box(parent, -m.sideX, m.sideY + 0.004f, m.sideZ + 0.026f, 0.007f, 0.007f, 0.004f, Reticle, 2f);
                        info.hasLaser = true;
                        info.laser = new Vector3(-m.sideX, m.sideY + 0.004f, m.sideZ + 0.03f);
                        break;
                    case AttachmentLook.RedDot:
                    case AttachmentLook.Reflex:
                    case AttachmentLook.Holo:
                    case AttachmentLook.Scope:
                        if (m.ownSight) break;
                        Optic(parent, m, attachment.look, ref info);
                        break;
                    case AttachmentLook.Suppressor:
                        Cylinder(parent, new Vector3(0f, m.muzzleY, info.front + 0.08f), 0.05f, 0.16f, DarkSteel);
                        Cylinder(parent, new Vector3(0f, m.muzzleY, info.front + 0.161f), 0.034f, 0.004f, Shapes.Shade(DarkSteel, 0.6f));
                        info.front += 0.16f;
                        break;
                    case AttachmentLook.FlashHider:
                        Cylinder(parent, new Vector3(0f, m.muzzleY, info.front + 0.025f), 0.028f, 0.05f, DarkSteel);
                        for (int i = 0; i < 3; i++)
                            Box(parent, 0f, m.muzzleY, info.front + 0.03f, 0.03f, 0.004f, 0.03f, Shapes.Shade(DarkSteel, 0.5f)).localRotation = Quaternion.Euler(0f, 0f, i * 60f);
                        info.front += 0.05f;
                        break;
                    case AttachmentLook.Compensator:
                        Box(parent, 0f, m.muzzleY, info.front + 0.028f, 0.034f, 0.034f, 0.056f, Steel);
                        Box(parent, 0f, m.muzzleY + 0.018f, info.front + 0.016f, 0.02f, 0.004f, 0.009f, Shapes.Shade(DarkSteel, 0.5f));
                        Box(parent, 0f, m.muzzleY + 0.018f, info.front + 0.038f, 0.02f, 0.004f, 0.009f, Shapes.Shade(DarkSteel, 0.5f));
                        info.front += 0.056f;
                        break;
                    case AttachmentLook.Brake:
                        Box(parent, 0f, m.muzzleY, info.front + 0.033f, 0.05f, 0.03f, 0.066f, DarkSteel);
                        for (int i = 0; i < 3; i++)
                            Box(parent, 0f, m.muzzleY, info.front + 0.014f + i * 0.019f, 0.052f, 0.01f, 0.008f, Shapes.Shade(DarkSteel, 0.45f));
                        info.front += 0.066f;
                        break;
                    case AttachmentLook.VerticalGrip:
                        Box(parent, 0f, m.underY - 0.004f, m.underZ, 0.026f, 0.01f, 0.05f, DarkSteel);
                        Box(parent, 0f, m.underY - 0.05f, m.underZ, 0.028f, 0.09f, 0.032f, Polymer).localRotation = Quaternion.Euler(8f, 0f, 0f);
                        break;
                    case AttachmentLook.AngledGrip:
                        Box(parent, 0f, m.underY - 0.016f, m.underZ - 0.01f, 0.026f, 0.03f, 0.08f, Polymer).localRotation = Quaternion.Euler(24f, 0f, 0f);
                        break;
                    case AttachmentLook.ExtendedMag:
                        Box(parent, 0f, m.magY - 0.03f, m.magZ + 0.006f, 0.03f, 0.07f, 0.048f, Polymer).localRotation = Quaternion.Euler(8f, 0f, 0f);
                        break;
                    case AttachmentLook.QuickMag:
                        Box(parent, 0f, m.magY - 0.008f, m.magZ, 0.012f, 0.018f, 0.034f, Coyote);
                        break;
                    case AttachmentLook.FixedStock:
                        Box(parent, 0f, m.stockY - 0.005f, m.rearZ - 0.012f, 0.05f, 0.12f, 0.024f, Shapes.Shade(DarkSteel, 0.7f));
                        break;
                    case AttachmentLook.SkeletonStock:
                        Box(parent, 0f, m.stockY + 0.035f, m.rearZ - 0.04f, 0.012f, 0.012f, 0.09f, DarkSteel);
                        Box(parent, 0f, m.stockY - 0.035f, m.rearZ - 0.04f, 0.012f, 0.012f, 0.09f, DarkSteel);
                        Box(parent, 0f, m.stockY, m.rearZ - 0.085f, 0.04f, 0.1f, 0.012f, DarkSteel);
                        break;
                }
            }
        }

        // Optics are open frames, not solid boxes, so first person can look through them at the
        // glowing reticle in the middle.
        static void Optic(Transform parent, Mounts m, AttachmentLook look, ref GunInfo info)
        {
            float y = m.railY, z = m.railZ;
            switch (look)
            {
                case AttachmentLook.Reflex:
                    Box(parent, 0f, y + 0.003f, z, 0.026f, 0.006f, 0.04f, DarkSteel);
                    Box(parent, 0.013f, y + 0.021f, z + 0.012f, 0.004f, 0.03f, 0.006f, DarkSteel);
                    Box(parent, -0.013f, y + 0.021f, z + 0.012f, 0.004f, 0.03f, 0.006f, DarkSteel);
                    Box(parent, 0f, y + 0.037f, z + 0.012f, 0.03f, 0.004f, 0.006f, DarkSteel);
                    Box(parent, 0f, y + 0.021f, z + 0.012f, 0.004f, 0.004f, 0.002f, Reticle, 3f);
                    info.sightY = y + 0.021f;
                    break;
                case AttachmentLook.RedDot:
                    Box(parent, 0f, y + 0.007f, z, 0.022f, 0.014f, 0.03f, DarkSteel);
                    Tube(parent, new Vector3(0f, y + 0.031f, z), 0.017f, 0.06f, DarkSteel);
                    Box(parent, 0f, y + 0.031f, z + 0.028f, 0.004f, 0.004f, 0.002f, Reticle, 3f);
                    info.sightY = y + 0.031f;
                    break;
                case AttachmentLook.Holo:
                    Box(parent, 0f, y + 0.009f, z, 0.04f, 0.018f, 0.07f, DarkSteel);
                    Box(parent, 0.02f, y + 0.034f, z + 0.02f, 0.005f, 0.032f, 0.026f, DarkSteel);
                    Box(parent, -0.02f, y + 0.034f, z + 0.02f, 0.005f, 0.032f, 0.026f, DarkSteel);
                    Box(parent, 0f, y + 0.052f, z + 0.02f, 0.045f, 0.005f, 0.03f, DarkSteel);
                    // A ring of four short ticks round a centre dot.
                    float c = y + 0.034f, r = 0.008f;
                    Box(parent, 0f, c, z + 0.02f, 0.003f, 0.003f, 0.002f, Reticle, 3f);
                    Box(parent, 0f, c + r, z + 0.02f, 0.005f, 0.0015f, 0.002f, Reticle, 3f);
                    Box(parent, 0f, c - r, z + 0.02f, 0.005f, 0.0015f, 0.002f, Reticle, 3f);
                    Box(parent, r, c, z + 0.02f, 0.0015f, 0.005f, 0.002f, Reticle, 3f);
                    Box(parent, -r, c, z + 0.02f, 0.0015f, 0.005f, 0.002f, Reticle, 3f);
                    info.sightY = c;
                    break;
                default: // magnified scope
                    Box(parent, 0f, y + 0.01f, z - 0.04f, 0.014f, 0.02f, 0.012f, DarkSteel);
                    Box(parent, 0f, y + 0.01f, z + 0.04f, 0.014f, 0.02f, 0.012f, DarkSteel);
                    Tube(parent, new Vector3(0f, y + 0.036f, z), 0.016f, 0.14f, DarkSteel);
                    Tube(parent, new Vector3(0f, y + 0.036f, z + 0.085f), 0.022f, 0.03f, DarkSteel);
                    Tube(parent, new Vector3(0f, y + 0.036f, z - 0.08f), 0.02f, 0.025f, DarkSteel);
                    Box(parent, 0f, y + 0.036f, z + 0.099f, 0.032f, 0.032f, 0.003f, new Color(0.38f, 0.72f, 1f), 1.2f);
                    info.sightY = y + 0.036f;
                    info.zoom = 2.5f;
                    return;
            }
            info.hasSight = true;
        }

        // An open tube of eight flat strips (no end caps to block the view through it).
        static void Tube(Transform parent, Vector3 centre, float radius, float length, Color color)
        {
            float width = radius * 0.85f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                Box(parent, centre.x + Mathf.Sin(a) * radius, centre.y + Mathf.Cos(a) * radius, centre.z, width, 0.004f, length, color).localRotation = Quaternion.Euler(0f, 0f, -i * 45f);
            }
        }

        // A cylinder along the gun (diameter, length in metres).
        static Transform Cylinder(Transform parent, Vector3 position, float diameter, float length, Color color)
        {
            var cylinder = Shapes.Make(PrimitiveType.Cylinder, "Part", parent, position, new Vector3(diameter, length * 0.5f, diameter), color, false).transform;
            cylinder.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return cylinder;
        }

        static Transform Box(Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color, float glow = 0f)
        {
            return Shapes.Box("Part", parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color, false, glow).transform;
        }
    }
}
