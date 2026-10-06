using UnityEngine;

namespace Swat
{
    // A 3D preview of an officer in their current loadout, standing in the
    // HQ (locker room for the roster, armory for loadouts) where the menu
    // camera is looking. Rebuilt only when the officer or kit changes.
    public static class CharacterPreview
    {
        static Transform root;
        static string shownKey;
        static ProceduralAnimator animator;

        public static void Show(OfficerData officer, OfficerLoadout loadout, Vector3 position, float yaw)
        {
            string key = officer.id + "|" + loadout.primaryId + "|" + loadout.sidearmId + "|" + loadout.armorId + "|" + loadout.useShield + "|"
                + string.Join(",", Weapon.Ids(loadout)) + "|" + loadout.uniformIndex + "|" + loadout.headgearIndex + "," + loadout.faceIndex + "," + loadout.patchIndex + "," + loadout.patchColorIndex + "," + loadout.facialHairIndex + "," + loadout.hairColorIndex + "|" + position;
            if (root != null && key == shownKey) return;
            Hide();
            shownKey = key;
            var hq = GameManager.Instance.Headquarters;
            root = new GameObject("Officer Preview").transform;
            root.SetParent(hq.root, false);
            root.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var parts = CharacterFactory.Build(root, PlayerController.OfficerAppearance(officer, loadout, true));
            var primary = GameData.Weapon(loadout.primaryId);
            var weapon = loadout.useShield || primary == null ? GameData.Weapon(loadout.sidearmId) : primary;
            CharacterFactory.SetWeapon(parts, weapon, weapon == primary ? loadout : null);
            parts.ring.enabled = false;
            animator = new ProceduralAnimator(parts);
            animator.SetPose(loadout.useShield ? Pose.Shielding : Pose.Relaxed);
            Shapes.SetLayer(root.gameObject, Layers.Characters);
        }

        public static void Tick(float dt)
        {
            if (root == null) return;
            root.Rotate(0f, 25f * dt, 0f);
            animator.Tick(dt, 0f, false);
        }

        public static void Hide()
        {
            if (root != null) Object.Destroy(root.gameObject);
            root = null;
            animator = null;
            shownKey = null;
        }
    }
}
