using UnityEngine;

namespace Swat
{
    // Builds a small low-poly model for each weapon category out of boxes, in the
    // same colours as the pixel-art sprites: charcoal steel, dark steel and the
    // weapon's accent colour for the furniture (stock, grip, handguard). If the
    // WeaponData has a modelPrefab, that is used instead. +z is the muzzle.
    public static class WeaponModels
    {
        // Charcoal steel and polymer, as in the weapon sprites.
        static readonly Color Steel = new Color(0.34f, 0.35f, 0.34f);
        static readonly Color DarkSteel = new Color(0.14f, 0.14f, 0.13f);
        static readonly Color Polymer = new Color(0.2f, 0.2f, 0.19f);
        static readonly Color Orange = new Color(0.95f, 0.5f, 0.1f);
        static readonly Color Yellow = new Color(0.95f, 0.78f, 0.12f);

        // Returns the distance from the grip to the muzzle.
        public static float Build(WeaponData weapon, Transform parent, OfficerLoadout attachments)
        {
            if (weapon.modelPrefab != null)
            {
                var model = Object.Instantiate(weapon.modelPrefab, parent, false);
                foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                var muzzle = model.transform.Find("Muzzle");
                return muzzle != null ? muzzle.localPosition.z : 0.5f;
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

            if (attachments != null && !weapon.isSidearm)
            {
                if (!string.IsNullOrEmpty(attachments.lightId))
                    Box(parent, 0f, -0.05f, muzzleZ - 0.2f, 0.035f, 0.035f, 0.08f, new Color(0.75f, 0.75f, 0.7f));
                if (!string.IsNullOrEmpty(attachments.opticId) && weapon.category != WeaponCategory.Carbine && weapon.category != WeaponCategory.Marksman)
                    Box(parent, 0f, 0.09f, 0.12f, 0.045f, 0.05f, 0.08f, DarkSteel);
                if (!string.IsNullOrEmpty(attachments.muzzleId))
                {
                    var suppressor = Shapes.Make(PrimitiveType.Cylinder, "Suppressor", parent, new Vector3(0f, 0.015f, muzzleZ + 0.08f), new Vector3(0.05f, 0.08f, 0.05f), DarkSteel, false).transform;
                    suppressor.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    muzzleZ += 0.16f;
                }
                if (attachments.stockId == "stock_fixed")
                    Box(parent, 0f, -0.03f, -0.22f, 0.06f, 0.12f, 0.12f, Polymer);
            }
            return muzzleZ;
        }

        static Transform Box(Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color, float glow = 0f)
        {
            return Shapes.Box("Part", parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color, false, glow).transform;
        }
    }
}
