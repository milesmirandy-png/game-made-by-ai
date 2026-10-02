using UnityEngine;

namespace Swat
{
    public enum HeadStyle { Helmet, Cap, Hair, Balaclava }

    public struct Appearance
    {
        public Color shirt, pants, skin, headwear, vest, ring;
        public HeadStyle head;
        public bool vestOn;
        public bool shield;
        public bool armed;
    }

    // References to the parts of a blocky character, plus helpers to show
    // poses and states. Animation itself is done by ProceduralAnimator.
    public class CharacterParts
    {
        public Transform root, model, leftArm, rightArm, gunRoot, muzzle, shield;
        public Renderer ring;
        public GameObject alertMarker, visuals;
        public Light flashlight;
        public WeaponData weapon;

        public void SetArms(Vector3 left, Vector3 right)
        {
            leftArm.localRotation = Quaternion.Euler(left);
            rightArm.localRotation = Quaternion.Euler(right);
        }

        public void SetRingColor(Color color)
        {
            if (ring != null) ring.sharedMaterial = Shapes.Mat(color, 1.5f);
        }

        // Hides everything visual (used for suspects the team can't currently see).
        public void SetVisible(bool visible)
        {
            if (visuals != null && visuals.activeSelf != visible) visuals.SetActive(visible);
        }

        public void ShowWeapon(bool show)
        {
            if (gunRoot != null && gunRoot.gameObject.activeSelf != show) gunRoot.gameObject.SetActive(show);
        }

        public void Fall()
        {
            model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.localPosition = new Vector3(0f, 0.2f, 0f);
            if (ring != null) ring.enabled = false;
            if (alertMarker != null) alertMarker.SetActive(false);
            ShowWeapon(false);
            if (flashlight != null) flashlight.enabled = false;
        }
    }

    // Builds the low-poly people. Each character stands on a coloured ring so
    // you can tell who is who from above: blue police, red suspects, yellow civilians.
    public static class CharacterFactory
    {
        static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.8f, 0.69f), new Color(0.87f, 0.67f, 0.52f), new Color(0.71f, 0.5f, 0.36f),
            new Color(0.55f, 0.37f, 0.25f), new Color(0.38f, 0.25f, 0.17f),
        };

        public static Color Skin(int index) { return SkinTones[Mathf.Abs(index) % SkinTones.Length]; }
        public static Color RandomSkin() { return SkinTones[Random.Range(0, SkinTones.Length)]; }

        public static CharacterParts Build(Transform root, Appearance look)
        {
            var parts = new CharacterParts { root = root };
            parts.visuals = new GameObject("Visuals");
            parts.visuals.transform.SetParent(root, false);
            var v = parts.visuals.transform;

            parts.model = new GameObject("Model").transform;
            parts.model.SetParent(v, false);
            var m = parts.model;

            Shapes.Box("Legs", m, new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.8f, 0.26f), look.pants, false);
            Shapes.Box("Torso", m, new Vector3(0f, 1.1f, 0f), new Vector3(0.54f, 0.62f, 0.32f), look.shirt, false);
            if (look.vestOn) Shapes.Box("Vest", m, new Vector3(0f, 1.12f, 0f), new Vector3(0.58f, 0.46f, 0.36f), look.vest, false);
            Shapes.Make(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.32f, look.skin, false);
            switch (look.head)
            {
                case HeadStyle.Helmet:
                    Shapes.Make(PrimitiveType.Sphere, "Helmet", m, new Vector3(0f, 1.7f, -0.01f), new Vector3(0.36f, 0.22f, 0.38f), look.headwear, false);
                    break;
                case HeadStyle.Cap:
                    Shapes.Box("Cap", m, new Vector3(0f, 1.74f, 0f), new Vector3(0.32f, 0.08f, 0.32f), look.headwear, false);
                    Shapes.Box("Brim", m, new Vector3(0f, 1.71f, 0.18f), new Vector3(0.26f, 0.03f, 0.12f), look.headwear, false);
                    break;
                case HeadStyle.Balaclava:
                    Shapes.Make(PrimitiveType.Sphere, "Mask", m, new Vector3(0f, 1.63f, 0f), Vector3.one * 0.34f, look.headwear, false);
                    Shapes.Box("Eyes", m, new Vector3(0f, 1.66f, 0.15f), new Vector3(0.22f, 0.05f, 0.05f), look.skin, false);
                    break;
                default:
                    Shapes.Box("Hair", m, new Vector3(0f, 1.71f, -0.04f), new Vector3(0.3f, 0.12f, 0.28f), look.headwear, false);
                    break;
            }

            parts.leftArm = Arm("Arm L", m, new Vector3(-0.33f, 1.34f, 0f), look.shirt, look.skin);
            parts.rightArm = Arm("Arm R", m, new Vector3(0.33f, 1.34f, 0f), look.shirt, look.skin);

            parts.gunRoot = new GameObject("Gun").transform;
            parts.gunRoot.SetParent(m, false);
            parts.gunRoot.localPosition = new Vector3(0.12f, 1.17f, 0.22f);
            parts.muzzle = new GameObject("Muzzle").transform;
            parts.muzzle.SetParent(parts.gunRoot, false);
            parts.muzzle.localPosition = new Vector3(0f, 0f, 0.5f);
            parts.ShowWeapon(look.armed);

            if (look.shield)
            {
                parts.shield = Shapes.Box("Shield", m, new Vector3(-0.12f, 1.0f, 0.5f), new Vector3(0.62f, 1.05f, 0.06f), new Color(0.06f, 0.07f, 0.09f), false).transform;
                Shapes.Box("Visor", parts.shield, new Vector3(0f, 0.3f, -0.6f), new Vector3(0.6f, 0.08f, 0.5f), new Color(0.35f, 0.6f, 0.9f), false, 1.2f);
                Shapes.Box("Label", parts.shield, new Vector3(0f, -0.05f, -0.6f), new Vector3(0.7f, 0.08f, 0.5f), new Color(0.85f, 0.85f, 0.85f), false);
            }

            parts.ring = Shapes.Make(PrimitiveType.Cylinder, "Ring", v, new Vector3(0f, 0.04f, 0f), new Vector3(0.95f, 0.01f, 0.95f), look.ring, false, 1.5f).GetComponent<Renderer>();

            parts.alertMarker = new GameObject("Alert Marker");
            parts.alertMarker.transform.SetParent(v, false);
            Shapes.Box("Bar", parts.alertMarker.transform, new Vector3(0f, 2.45f, 0f), new Vector3(0.12f, 0.4f, 0.12f), new Color(1f, 0.2f, 0.1f), false, 3f);
            Shapes.Box("Dot", parts.alertMarker.transform, new Vector3(0f, 2.12f, 0f), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.2f, 0.1f), false, 3f);
            parts.alertMarker.SetActive(false);
            return parts;
        }

        static Transform Arm(string name, Transform parent, Vector3 shoulder, Color sleeve, Color hand)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = shoulder;
            Shapes.Box("Sleeve", pivot, new Vector3(0f, -0.25f, 0f), new Vector3(0.13f, 0.5f, 0.13f), sleeve, false);
            Shapes.Box("Hand", pivot, new Vector3(0f, -0.55f, 0f), new Vector3(0.11f, 0.12f, 0.11f), hand, false);
            return pivot;
        }

        // Adds a flashlight (a spot light) to a character. Off by default.
        public static Light AddFlashlight(CharacterParts parts, float range)
        {
            var light = new GameObject("Flashlight").AddComponent<Light>();
            light.transform.SetParent(parts.model, false);
            light.transform.localPosition = new Vector3(0.12f, 1.25f, 0.5f);
            light.type = LightType.Spot;
            light.spotAngle = 55f;
            light.range = range;
            light.intensity = 2.2f * Shapes.PointLightScale;
            light.color = new Color(1f, 0.96f, 0.85f);
            light.shadows = LightShadows.None;
            light.enabled = false;
            parts.flashlight = light;
            return light;
        }

        // Replaces the gun in the character's hands. Attachments are cosmetic shapes on the model.
        public static void SetWeapon(CharacterParts parts, WeaponData weapon, OfficerLoadout attachments)
        {
            parts.weapon = weapon;
            for (int i = parts.gunRoot.childCount - 1; i >= 0; i--)
            {
                var child = parts.gunRoot.GetChild(i);
                if (child != parts.muzzle) Object.Destroy(child.gameObject);
            }
            if (weapon == null)
            {
                parts.ShowWeapon(false);
                return;
            }
            float length = WeaponModels.Build(weapon, parts.gunRoot, attachments);
            parts.muzzle.localPosition = new Vector3(0f, 0.01f, length);
            parts.ShowWeapon(true);
        }
    }
}
