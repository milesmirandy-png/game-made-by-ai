using UnityEngine;

namespace Swat
{
    // Builds a simple, recognisable low-poly model for each weapon category
    // out of a few boxes. If the WeaponData has a modelPrefab, that is used instead.
    public static class WeaponModels
    {
        static readonly Color Metal = new Color(0.07f, 0.07f, 0.08f);
        static readonly Color Polymer = new Color(0.16f, 0.16f, 0.17f);
        static readonly Color Orange = new Color(0.95f, 0.5f, 0.1f);

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

            float muzzleZ;
            switch (weapon.category)
            {
                case WeaponCategory.CompactSMG:
                    Box(parent, 0f, 0f, 0.15f, 0.07f, 0.11f, 0.3f, Polymer);
                    Box(parent, 0f, 0.01f, 0.34f, 0.03f, 0.03f, 0.08f, Metal);
                    Box(parent, 0f, -0.12f, 0.17f, 0.04f, 0.16f, 0.05f, Metal);
                    Box(parent, 0f, -0.01f, -0.04f, 0.04f, 0.06f, 0.12f, Metal);
                    muzzleZ = 0.38f;
                    break;
                case WeaponCategory.SMG:
                    Box(parent, 0f, 0f, 0.18f, 0.07f, 0.11f, 0.36f, Polymer);
                    Box(parent, 0f, 0.01f, 0.42f, 0.03f, 0.03f, 0.12f, Metal);
                    Box(parent, 0f, -0.13f, 0.2f, 0.04f, 0.18f, 0.06f, Metal);
                    Box(parent, 0f, -0.01f, -0.08f, 0.045f, 0.08f, 0.18f, Metal);
                    muzzleZ = 0.48f;
                    break;
                case WeaponCategory.CompactRifle:
                case WeaponCategory.Rifle:
                    bool full = weapon.category == WeaponCategory.Rifle;
                    Box(parent, 0f, 0f, 0.15f, 0.07f, 0.11f, 0.36f, Metal);
                    Box(parent, 0f, 0.005f, full ? 0.48f : 0.42f, 0.065f, 0.08f, full ? 0.26f : 0.18f, Polymer);
                    Box(parent, 0f, 0.015f, full ? 0.68f : 0.56f, 0.03f, 0.03f, 0.12f, Metal);
                    Box(parent, 0f, -0.13f, 0.18f, 0.045f, 0.17f, 0.075f, Metal).localRotation = Quaternion.Euler(12f, 0f, 0f);
                    Box(parent, 0f, -0.02f, -0.12f, 0.055f, 0.09f, 0.22f, Polymer);
                    muzzleZ = full ? 0.74f : 0.62f;
                    break;
                case WeaponCategory.Shotgun:
                    Box(parent, 0f, 0f, 0.06f, 0.07f, 0.1f, 0.24f, Metal);
                    Box(parent, 0f, 0.02f, 0.42f, 0.04f, 0.04f, 0.56f, Metal);
                    Box(parent, 0f, -0.03f, 0.36f, 0.04f, 0.04f, 0.42f, Polymer);
                    Box(parent, 0f, -0.035f, 0.44f, 0.065f, 0.065f, 0.14f, new Color(0.3f, 0.22f, 0.14f));
                    Box(parent, 0f, -0.03f, -0.18f, 0.055f, 0.1f, 0.24f, Polymer);
                    muzzleZ = 0.7f;
                    break;
                case WeaponCategory.Carbine:
                    Box(parent, 0f, 0f, 0.15f, 0.07f, 0.1f, 0.36f, Metal);
                    Box(parent, 0f, 0.01f, 0.56f, 0.035f, 0.035f, 0.5f, Metal);
                    Box(parent, 0f, 0.1f, 0.15f, 0.05f, 0.05f, 0.22f, Polymer);
                    Box(parent, 0f, -0.11f, 0.2f, 0.04f, 0.12f, 0.06f, Metal);
                    Box(parent, 0f, -0.02f, -0.13f, 0.06f, 0.1f, 0.24f, Polymer);
                    muzzleZ = 0.82f;
                    break;
                case WeaponCategory.LessLethal:
                    var tube = Shapes.Make(PrimitiveType.Cylinder, "Tube", parent, new Vector3(0f, 0.01f, 0.25f), new Vector3(0.11f, 0.2f, 0.11f), Polymer, false).transform;
                    tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Box(parent, 0f, 0.01f, 0.38f, 0.12f, 0.12f, 0.04f, Orange);
                    Box(parent, 0f, -0.1f, 0.05f, 0.04f, 0.12f, 0.05f, Metal);
                    Box(parent, 0f, -0.02f, -0.12f, 0.05f, 0.08f, 0.2f, Orange);
                    muzzleZ = 0.46f;
                    break;
                case WeaponCategory.HeavyPistol:
                    Box(parent, 0f, 0.02f, 0.11f, 0.05f, 0.06f, 0.26f, Metal);
                    Box(parent, 0f, -0.06f, -0.01f, 0.04f, 0.12f, 0.06f, Polymer).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    muzzleZ = 0.25f;
                    break;
                default: // service and backup pistols
                    bool backup = weapon.category == WeaponCategory.BackupPistol;
                    Box(parent, 0f, 0.02f, backup ? 0.07f : 0.09f, 0.04f, 0.05f, backup ? 0.15f : 0.2f, Metal);
                    Box(parent, 0f, -0.05f, -0.01f, 0.035f, 0.1f, 0.05f, Polymer).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                    muzzleZ = backup ? 0.15f : 0.2f;
                    break;
            }

            if (attachments != null && !weapon.isSidearm)
            {
                if (!string.IsNullOrEmpty(attachments.lightId))
                    Box(parent, 0f, -0.05f, muzzleZ - 0.18f, 0.035f, 0.035f, 0.08f, new Color(0.75f, 0.75f, 0.7f));
                if (!string.IsNullOrEmpty(attachments.opticId) && weapon.category != WeaponCategory.Carbine)
                    Box(parent, 0f, 0.085f, 0.12f, 0.04f, 0.05f, 0.08f, Metal);
                if (!string.IsNullOrEmpty(attachments.muzzleId))
                {
                    var suppressor = Shapes.Make(PrimitiveType.Cylinder, "Suppressor", parent, new Vector3(0f, 0.015f, muzzleZ + 0.08f), new Vector3(0.05f, 0.08f, 0.05f), Metal, false).transform;
                    suppressor.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    muzzleZ += 0.16f;
                }
                if (attachments.stockId == "stock_fixed")
                    Box(parent, 0f, -0.03f, -0.2f, 0.06f, 0.12f, 0.12f, Polymer);
            }
            return muzzleZ;
        }

        static Transform Box(Transform parent, float x, float y, float z, float sx, float sy, float sz, Color color)
        {
            return Shapes.Box("Part", parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), color, false).transform;
        }
    }
}
