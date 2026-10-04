using UnityEngine;

namespace Swat
{
    public enum HeadStyle { Helmet, Cap, Hair, Balaclava }
    public enum Outfit { Plain, Tactical, Jacket, Suit, HiVis, Uniform, Hoodie, Shirt }
    public enum HairStyle { Short, Long, Ponytail, Bun, Buzz, Curly, Bald }

    public struct Appearance
    {
        public Color shirt, pants, skin, headwear, vest, ring;
        public HeadStyle head;
        public bool vestOn;
        public bool shield;
        public bool armed;
        // Added in the polish update (all optional; zero values mean "default").
        public Outfit outfit;
        public Color accent;      // jacket, tie or trim color
        public Color idColor;     // squad identification color (helmet band, shoulder patch, helmet top)
        public bool idMarker;
        public float height, width;
        // Added with the character update (all optional; zero values mean "default").
        public HairStyle hair;        // for HeadStyle.Hair
        public bool shortSleeves, skirt, backpack, lanyard, beard, gloves, sneakers, holster, bandage, hoodUp, bandana;
        public int glasses;           // 0 none, 1 glasses, 2 sunglasses
        public Color shoes, bag;      // shoe and backpack colour
    }

    // References to the parts of a blocky character, plus helpers to show
    // poses and states. Animation itself is done by ProceduralAnimator.
    public class CharacterParts
    {
        public Transform root, model, head, leftArm, rightArm, leftLeg, rightLeg, gunRoot, muzzle, shield;
        public Renderer ring;
        public GameObject alertMarker, visuals;
        public Light flashlight;
        public WeaponData weapon;

        // Hit flash: the whole figure turns white for a few frames when hit (no gore, just a blink).
        Renderer[] bodyRenderers;
        float flashUntil;
        bool flashing;
        static MaterialPropertyBlock flashBlock;

        public void CacheRenderers()
        {
            var list = new System.Collections.Generic.List<Renderer>();
            if (model != null) list.AddRange(model.GetComponentsInChildren<Renderer>(true));
            bodyRenderers = list.ToArray();
        }

        public void Flash(Color color, float seconds)
        {
            if (bodyRenderers == null || seconds <= 0f) return;
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();
            flashBlock.Clear();
            flashBlock.SetColor("_Color", color);
            flashBlock.SetColor("_BaseColor", color); // URP Lit
            foreach (var renderer in bodyRenderers) if (renderer != null) renderer.SetPropertyBlock(flashBlock);
            flashUntil = Time.time + seconds;
            flashing = true;
        }

        public void UpdateFlash()
        {
            if (flashing && Time.time >= flashUntil) ClearFlash();
        }

        public void ClearFlash()
        {
            if (!flashing) return;
            flashing = false;
            foreach (var renderer in bodyRenderers) if (renderer != null) renderer.SetPropertyBlock(null);
        }

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

        // Down: lying flat (snap), or left standing for Topple to tip over (animated).
        public void Fall(bool snap = true)
        {
            ClearFlash();
            if (snap)
            {
                model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                model.localPosition = new Vector3(0f, 0.2f, 0f);
            }
            if (ring != null) ring.enabled = false;
            if (alertMarker != null) alertMarker.SetActive(false);
            ShowWeapon(false);
            if (flashlight != null) flashlight.enabled = false;
        }

        // Back on their feet (respawning in the game modes).
        public void Rise()
        {
            Topple.Stop(model);
            model.localRotation = Quaternion.identity;
            model.localPosition = Vector3.zero;
            if (ring != null) ring.enabled = true;
            ShowWeapon(weapon != null);
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

        static readonly Color Gear = new Color(0.07f, 0.075f, 0.085f);
        static readonly Color Boot = new Color(0.06f, 0.06f, 0.065f);

        public static CharacterParts Build(Transform root, Appearance look)
        {
            var parts = new CharacterParts { root = root };
            parts.visuals = new GameObject("Visuals");
            parts.visuals.transform.SetParent(root, false);
            var v = parts.visuals.transform;

            parts.model = new GameObject("Model").transform;
            parts.model.SetParent(v, false);
            float height = look.height > 0f ? look.height : 1f;
            float width = look.width > 0f ? look.width : 1f;
            parts.model.localScale = new Vector3(width, height, width);
            var m = parts.model;
            bool tactical = look.outfit == Swat.Outfit.Tactical;

            Color shoe = look.shoes.a > 0f ? look.shoes : Boot;
            Color skin = look.skin;
            Color hairColor = look.headwear;

            // Legs swing from the hips: thigh, shin and shoe (white soles on sneakers).
            parts.leftLeg = Leg("Leg L", m, new Vector3(-0.11f, 0.8f, 0f), look, shoe, tactical);
            parts.rightLeg = Leg("Leg R", m, new Vector3(0.11f, 0.8f, 0f), look, shoe, tactical);
            if (look.skirt) Shapes.Box("Skirt", m, new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.34f, 0.34f), look.pants, false);
            else Shapes.Box("Hips", m, new Vector3(0f, 0.78f, 0f), new Vector3(0.42f, 0.14f, 0.26f), look.pants, false);

            // A broader chest over a narrower waist: shoulders read clearly from above.
            Shapes.Box("Waist", m, new Vector3(0f, 0.97f, 0f), new Vector3(0.46f, 0.34f, 0.28f), look.shirt, false);
            Shapes.Box("Chest", m, new Vector3(0f, 1.24f, 0f), new Vector3(0.56f, 0.34f, 0.32f), look.shirt, false);
            Shapes.Box("Neck", m, new Vector3(0f, 1.44f, 0f), new Vector3(0.13f, 0.08f, 0.13f), skin, false);
            bool casual = look.outfit == Swat.Outfit.Plain || look.outfit == Swat.Outfit.Shirt || look.outfit == Swat.Outfit.Jacket;
            if (casual && !look.skirt)
            {
                Shapes.Box("Belt", m, new Vector3(0f, 0.85f, 0f), new Vector3(0.47f, 0.05f, 0.29f), new Color(0.16f, 0.11f, 0.08f), false);
                Shapes.Box("Buckle", m, new Vector3(0f, 0.85f, 0.15f), new Vector3(0.07f, 0.05f, 0.01f), new Color(0.75f, 0.68f, 0.45f), false);
            }
            DressOutfit(look, m);
            if (look.vestOn)
            {
                Shapes.Box("Vest", m, new Vector3(0f, 1.12f, 0f), new Vector3(0.58f, 0.46f, 0.36f), look.vest, false);
                if (tactical)
                {
                    // Pouches, radio and back plate.
                    for (int i = -1; i <= 1; i++)
                        Shapes.Box("Pouch", m, new Vector3(i * 0.15f, 0.98f, 0.2f), new Vector3(0.12f, 0.14f, 0.06f), Shapes.Shade(look.vest, 1.35f), false);
                    Shapes.Box("Radio", m, new Vector3(-0.2f, 1.3f, 0.19f), new Vector3(0.07f, 0.12f, 0.05f), Gear, false);
                    Shapes.Box("Antenna", m, new Vector3(-0.22f, 1.44f, 0.19f), new Vector3(0.015f, 0.18f, 0.015f), Gear, false);
                    Shapes.Box("Back Plate", m, new Vector3(0f, 1.14f, -0.2f), new Vector3(0.44f, 0.36f, 0.05f), Shapes.Shade(look.vest, 0.85f), false);
                    Shapes.Box("Label", m, new Vector3(0f, 1.2f, -0.226f), new Vector3(0.3f, 0.07f, 0.005f), new Color(0.85f, 0.85f, 0.82f), false);
                    Shapes.Box("Hydration", m, new Vector3(0f, 1.0f, -0.24f), new Vector3(0.22f, 0.2f, 0.05f), Shapes.Shade(look.vest, 0.7f), false);
                }
            }
            if (tactical) Shapes.Box("Belt", m, new Vector3(0f, 0.84f, 0f), new Vector3(0.5f, 0.07f, 0.3f), Gear, false);
            if (look.backpack)
            {
                Color bag = look.bag.a > 0f ? look.bag : new Color(0.25f, 0.3f, 0.38f);
                Shapes.Box("Backpack", m, new Vector3(0f, 1.12f, -0.23f), new Vector3(0.38f, 0.42f, 0.16f), bag, false);
                Shapes.Box("Flap", m, new Vector3(0f, 1.3f, -0.25f), new Vector3(0.36f, 0.08f, 0.17f), Shapes.Shade(bag, 0.8f), false);
                for (int side = -1; side <= 1; side += 2)
                    Shapes.Box("Strap", m, new Vector3(side * 0.13f, 1.25f, 0.165f), new Vector3(0.05f, 0.24f, 0.01f), Shapes.Shade(bag, 0.7f), false);
            }
            if (look.lanyard)
            {
                Shapes.Box("Lanyard", m, new Vector3(0f, 1.3f, 0.165f), new Vector3(0.02f, 0.18f, 0.01f), new Color(0.2f, 0.4f, 0.8f), false);
                Shapes.Box("ID Card", m, new Vector3(0f, 1.17f, 0.17f), new Vector3(0.08f, 0.1f, 0.01f), new Color(0.95f, 0.95f, 0.92f), false);
            }

            // The head sits on its own pivot. In pixel art it is drawn a little larger
            // ("chibi" proportions), so faces and helmets stay readable at low resolution.
            float headScale = QualityManager.PixelArt ? 1.18f : 1f;
            parts.head = new GameObject("Head Pivot").transform;
            parts.head.SetParent(m, false);
            parts.head.localPosition = new Vector3(0f, 1.62f + (headScale - 1f) * 0.12f, 0f);
            parts.head.localScale = Vector3.one * headScale;
            var hd = parts.head;
            Shapes.Make(PrimitiveType.Sphere, "Head", hd, Vector3.zero, Vector3.one * 0.32f, skin, false);
            Shapes.Box("Nose", hd, new Vector3(0f, -0.01f, 0.155f), new Vector3(0.05f, 0.06f, 0.04f), Shapes.Shade(skin, 0.92f), false);
            bool eyes = true, face = true;
            switch (look.head)
            {
                case HeadStyle.Helmet:
                    Shapes.Make(PrimitiveType.Sphere, "Helmet", hd, new Vector3(0f, 0.08f, -0.01f), new Vector3(0.36f, 0.22f, 0.38f), look.headwear, false);
                    if (tactical)
                    {
                        Shapes.Box("Goggles", hd, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.24f, 0.05f, 0.05f), new Color(0.1f, 0.12f, 0.14f), false);
                        Shapes.Box("Lens", hd, new Vector3(0f, 0.04f, 0.176f), new Vector3(0.18f, 0.03f, 0.01f), new Color(0.35f, 0.62f, 0.85f), false, 1.2f);
                        Shapes.Box("Mount", hd, new Vector3(0f, 0.15f, 0.16f), new Vector3(0.08f, 0.06f, 0.05f), Gear, false);
                        eyes = false;
                    }
                    break;
                case HeadStyle.Cap:
                    if (look.hoodUp) Hood(hd, look.shirt);
                    Shapes.Box("Cap", hd, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.08f, 0.32f), look.headwear, false);
                    Shapes.Box("Brim", hd, new Vector3(0f, 0.09f, 0.18f), new Vector3(0.26f, 0.03f, 0.12f), look.headwear, false);
                    break;
                case HeadStyle.Balaclava:
                    Shapes.Make(PrimitiveType.Sphere, "Mask", hd, new Vector3(0f, 0.01f, 0f), Vector3.one * 0.34f, look.headwear, false);
                    Shapes.Box("Eyes", hd, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.22f, 0.05f, 0.05f), skin, false);
                    eyes = face = false;
                    break;
                default:
                    if (look.hoodUp) Hood(hd, look.shirt);
                    else DrawHair(hd, look.hair, hairColor);
                    break;
            }
            if (look.glasses > 0 && look.head != HeadStyle.Balaclava && !(look.head == HeadStyle.Helmet && tactical))
            {
                bool sun = look.glasses == 2;
                Shapes.Box("Glasses", hd, new Vector3(0f, 0.025f, 0.155f), new Vector3(0.25f, sun ? 0.06f : 0.05f, 0.03f), sun ? new Color(0.05f, 0.05f, 0.06f) : new Color(0.2f, 0.18f, 0.16f), false);
                if (!sun)
                    for (int side = -1; side <= 1; side += 2)
                        Shapes.Box("Lens", hd, new Vector3(side * 0.06f, 0.025f, 0.168f), new Vector3(0.07f, 0.035f, 0.005f), new Color(0.7f, 0.82f, 0.9f), false);
                eyes = false;
            }
            if (eyes)
            {
                // Two dark pixels for eyes and a brow line above: enough to show which way someone faces.
                var eye = new Color(0.08f, 0.07f, 0.07f);
                Shapes.Box("Eye L", hd, new Vector3(-0.06f, 0.02f, 0.148f), new Vector3(0.045f, 0.05f, 0.03f), eye, false);
                Shapes.Box("Eye R", hd, new Vector3(0.06f, 0.02f, 0.148f), new Vector3(0.045f, 0.05f, 0.03f), eye, false);
                if (look.head == HeadStyle.Hair || look.head == HeadStyle.Cap)
                    for (int side = -1; side <= 1; side += 2)
                        Shapes.Box("Brow", hd, new Vector3(side * 0.06f, 0.065f, 0.147f), new Vector3(0.06f, 0.015f, 0.03f), Shapes.Shade(hairColor, 0.8f), false);
            }
            if (face)
            {
                if (look.beard) Shapes.Box("Beard", hd, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.25f, 0.1f, 0.12f), Shapes.Shade(hairColor, 0.9f), false);
                else Shapes.Box("Mouth", hd, new Vector3(0f, -0.07f, 0.148f), new Vector3(0.06f, 0.015f, 0.02f), Shapes.Shade(skin, 0.6f), false);
                if (look.bandana) Shapes.Box("Bandana", hd, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.3f, 0.13f, 0.13f), look.accent.a > 0f ? look.accent : new Color(0.6f, 0.12f, 0.12f), false);
            }
            if (look.bandage) Shapes.Box("Bandage", hd, new Vector3(0f, 0.09f, 0f), new Vector3(0.335f, 0.05f, 0.335f), new Color(0.95f, 0.95f, 0.92f), false);

            if (look.idMarker)
            {
                // Squad identification: colored helmet band, a marker on top (visible from above) and a shoulder patch.
                float top = look.head == HeadStyle.Helmet ? 0.08f : 0.12f;
                Shapes.Box("ID Band", hd, new Vector3(0f, top - 0.02f, -0.01f), new Vector3(0.37f, 0.035f, 0.39f), look.idColor, false);
                Shapes.Box("ID Top", hd, new Vector3(0f, top + 0.1f, -0.03f), new Vector3(0.12f, 0.02f, 0.16f), look.idColor, false, 1.2f);
            }
            if (tactical)
            {
                // Shoulder pads in the squad colour: the first thing you see from above.
                Color pad = look.idMarker ? Shapes.Shade(look.idColor, 0.85f) : Shapes.Shade(look.vestOn ? look.vest : look.shirt, 1.25f);
                Shapes.Box("Shoulder L", m, new Vector3(-0.3f, 1.42f, 0f), new Vector3(0.17f, 0.07f, 0.24f), pad, false);
                Shapes.Box("Shoulder R", m, new Vector3(0.3f, 1.42f, 0f), new Vector3(0.17f, 0.07f, 0.24f), pad, false);
            }

            Color hand = tactical || look.gloves ? Gear : skin;
            Color sleeve = look.outfit == Swat.Outfit.Suit || look.outfit == Swat.Outfit.Jacket ? look.accent : look.shirt;
            bool shortSleeves = look.shortSleeves && !tactical && casual && look.outfit != Swat.Outfit.Jacket;
            parts.leftArm = Arm("Arm L", m, new Vector3(-0.33f, 1.34f, 0f), sleeve, hand, shortSleeves ? skin : sleeve);
            parts.rightArm = Arm("Arm R", m, new Vector3(0.33f, 1.34f, 0f), sleeve, hand, shortSleeves ? skin : sleeve);
            if (look.idMarker)
                Shapes.Box("Patch", parts.rightArm, new Vector3(0.075f, -0.12f, 0f), new Vector3(0.01f, 0.1f, 0.1f), look.idColor, false);
            if (look.holster) Shapes.Box("Holster", parts.rightLeg, new Vector3(0.11f, -0.2f, 0f), new Vector3(0.06f, 0.2f, 0.13f), Gear, false);
            if (look.bandage && !shortSleeves) Shapes.Box("Arm Bandage", parts.leftArm, new Vector3(0f, -0.36f, 0f), new Vector3(0.145f, 0.07f, 0.145f), new Color(0.95f, 0.95f, 0.92f), false);

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
            // Soft contact shadow so characters sit on the floor even without real-time shadows.
            DecalMesh.Single("Blob Shadow", v, root.position + Vector3.up * 0.035f, new Vector2(0.95f, 0.95f), 0f, new Color(0f, 0f, 0f, 0.4f), Shapes.DecalMaterial(ProceduralTextures.Radial));

            parts.alertMarker = new GameObject("Alert Marker");
            parts.alertMarker.transform.SetParent(v, false);
            Shapes.Box("Bar", parts.alertMarker.transform, new Vector3(0f, 2.45f, 0f), new Vector3(0.12f, 0.4f, 0.12f), new Color(1f, 0.2f, 0.1f), false, 3f);
            Shapes.Box("Dot", parts.alertMarker.transform, new Vector3(0f, 2.12f, 0f), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.2f, 0.1f), false, 3f);
            parts.alertMarker.SetActive(false);
            Shapes.Toonify(m);
            parts.CacheRenderers();
            return parts;
        }

        // Clothing details layered over the torso.
        static void DressOutfit(Appearance look, Transform m)
        {
            switch (look.outfit)
            {
                case Swat.Outfit.Jacket:
                    Shapes.Box("Jacket L", m, new Vector3(-0.17f, 1.08f, 0.02f), new Vector3(0.22f, 0.66f, 0.34f), look.accent, false);
                    Shapes.Box("Jacket R", m, new Vector3(0.17f, 1.08f, 0.02f), new Vector3(0.22f, 0.66f, 0.34f), look.accent, false);
                    Shapes.Box("Collar", m, new Vector3(0f, 1.4f, -0.02f), new Vector3(0.5f, 0.08f, 0.3f), Shapes.Shade(look.accent, 0.85f), false);
                    break;
                case Swat.Outfit.Suit:
                    Shapes.Box("Jacket", m, new Vector3(0f, 1.08f, -0.01f), new Vector3(0.56f, 0.66f, 0.33f), look.accent, false);
                    Shapes.Box("Shirt", m, new Vector3(0f, 1.24f, 0.16f), new Vector3(0.14f, 0.3f, 0.02f), new Color(0.93f, 0.93f, 0.9f), false);
                    Shapes.Box("Tie", m, new Vector3(0f, 1.18f, 0.175f), new Vector3(0.05f, 0.3f, 0.01f), new Color(0.55f, 0.1f, 0.12f), false);
                    break;
                case Swat.Outfit.Shirt:
                    Shapes.Box("Tie", m, new Vector3(0f, 1.18f, 0.165f), new Vector3(0.05f, 0.32f, 0.01f), look.accent, false);
                    Shapes.Box("Collar", m, new Vector3(0f, 1.39f, 0.02f), new Vector3(0.3f, 0.05f, 0.3f), Shapes.Shade(look.shirt, 1.05f), false);
                    break;
                case Swat.Outfit.HiVis:
                    Shapes.Box("Hi-Vis", m, new Vector3(0f, 1.1f, 0f), new Vector3(0.57f, 0.5f, 0.35f), new Color(0.85f, 0.95f, 0.15f), false);
                    Shapes.Box("Stripe", m, new Vector3(0f, 1.0f, 0f), new Vector3(0.58f, 0.05f, 0.36f), new Color(0.75f, 0.75f, 0.75f), false, 0.6f);
                    break;
                case Swat.Outfit.Uniform:
                    Shapes.Box("Badge", m, new Vector3(-0.14f, 1.25f, 0.165f), new Vector3(0.07f, 0.08f, 0.01f), new Color(0.85f, 0.75f, 0.3f), false);
                    Shapes.Box("Belt", m, new Vector3(0f, 0.84f, 0f), new Vector3(0.5f, 0.07f, 0.3f), Gear, false);
                    Shapes.Box("Pocket", m, new Vector3(0.14f, 1.22f, 0.165f), new Vector3(0.1f, 0.1f, 0.01f), Shapes.Shade(look.shirt, 0.85f), false);
                    break;
                case Swat.Outfit.Hoodie:
                    if (!look.hoodUp) Shapes.Box("Hood", m, new Vector3(0f, 1.45f, -0.14f), new Vector3(0.34f, 0.2f, 0.12f), Shapes.Shade(look.shirt, 0.9f), false);
                    Shapes.Box("Pocket", m, new Vector3(0f, 0.95f, 0.165f), new Vector3(0.3f, 0.12f, 0.01f), Shapes.Shade(look.shirt, 0.85f), false);
                    break;
            }
        }

        static Transform Leg(string name, Transform parent, Vector3 hip, Appearance look, Color shoe, bool tactical)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hip;
            Color shin = look.skirt ? look.skin : look.pants;
            Shapes.Box("Thigh", pivot, new Vector3(0f, -0.2f, 0f), new Vector3(0.18f, 0.42f, 0.24f), look.pants, false);
            Shapes.Box("Shin", pivot, new Vector3(0f, -0.56f, 0f), new Vector3(0.16f, 0.34f, 0.21f), shin, false);
            Shapes.Box("Shoe", pivot, new Vector3(0f, -0.74f, 0.04f), new Vector3(0.2f, 0.12f, 0.32f), shoe, false);
            if (look.sneakers) Shapes.Box("Sole", pivot, new Vector3(0f, -0.795f, 0.04f), new Vector3(0.21f, 0.03f, 0.33f), new Color(0.92f, 0.92f, 0.9f), false);
            if (tactical) Shapes.Box("Knee Pad", pivot, new Vector3(0f, -0.42f, 0.13f), new Vector3(0.16f, 0.12f, 0.04f), Gear, false);
            return pivot;
        }

        // Shoulder cap, upper arm, forearm (skin for short sleeves) and hand; all swing from the shoulder.
        static Transform Arm(string name, Transform parent, Vector3 shoulder, Color sleeve, Color hand, Color forearm)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = shoulder;
            Shapes.Box("Shoulder", pivot, new Vector3(0f, -0.03f, 0f), new Vector3(0.16f, 0.13f, 0.18f), sleeve, false);
            Shapes.Box("Sleeve", pivot, new Vector3(0f, -0.17f, 0f), new Vector3(0.14f, 0.26f, 0.14f), sleeve, false);
            Shapes.Box("Forearm", pivot, new Vector3(0f, -0.4f, 0f), new Vector3(0.13f, 0.22f, 0.13f), forearm, false);
            Shapes.Box("Hand", pivot, new Vector3(0f, -0.56f, 0f), new Vector3(0.12f, 0.12f, 0.12f), hand, false);
            return pivot;
        }

        // Hair shapes that read from above (the top of the head is what you mostly see).
        static void DrawHair(Transform head, HairStyle style, Color color)
        {
            switch (style)
            {
                case HairStyle.Buzz:
                    Shapes.Box("Hair", head, new Vector3(0f, 0.115f, -0.015f), new Vector3(0.3f, 0.06f, 0.3f), color, false);
                    break;
                case HairStyle.Bald:
                    Shapes.Box("Fringe", head, new Vector3(0f, 0.01f, -0.11f), new Vector3(0.31f, 0.08f, 0.11f), color, false);
                    break;
                case HairStyle.Curly:
                    Shapes.Make(PrimitiveType.Sphere, "Curls", head, new Vector3(0f, 0.09f, -0.02f), new Vector3(0.38f, 0.27f, 0.37f), color, false);
                    break;
                default:
                    Shapes.Box("Hair", head, new Vector3(0f, 0.09f, -0.04f), new Vector3(0.31f, 0.13f, 0.29f), color, false);
                    if (style == HairStyle.Long)
                    {
                        Shapes.Box("Hair Back", head, new Vector3(0f, -0.08f, -0.13f), new Vector3(0.31f, 0.3f, 0.08f), color, false);
                        for (int side = -1; side <= 1; side += 2)
                            Shapes.Box("Hair Side", head, new Vector3(side * 0.15f, -0.03f, -0.04f), new Vector3(0.04f, 0.2f, 0.18f), color, false);
                    }
                    else if (style == HairStyle.Ponytail)
                        Shapes.Box("Ponytail", head, new Vector3(0f, 0.0f, -0.19f), new Vector3(0.08f, 0.22f, 0.08f), color, false);
                    else if (style == HairStyle.Bun)
                        Shapes.Box("Bun", head, new Vector3(0f, 0.17f, -0.1f), new Vector3(0.13f, 0.1f, 0.13f), color, false);
                    break;
            }
        }

        // A hood pulled up: top, back and sides, leaving the face open.
        static void Hood(Transform head, Color color)
        {
            var c = Shapes.Shade(color, 0.92f);
            Shapes.Box("Hood Top", head, new Vector3(0f, 0.12f, -0.02f), new Vector3(0.36f, 0.12f, 0.34f), c, false);
            Shapes.Box("Hood Back", head, new Vector3(0f, -0.03f, -0.13f), new Vector3(0.36f, 0.28f, 0.1f), c, false);
            for (int side = -1; side <= 1; side += 2)
                Shapes.Box("Hood Side", head, new Vector3(side * 0.17f, 0.0f, 0.0f), new Vector3(0.04f, 0.28f, 0.3f), c, false);
        }

        // ---- Appearance presets ----

        static readonly Color[] CasualShirts =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.55f, 0.75f, 0.95f), new Color(0.95f, 0.85f, 0.4f), new Color(0.95f, 0.6f, 0.7f),
            new Color(0.6f, 0.85f, 0.6f), new Color(0.75f, 0.55f, 0.85f), new Color(0.95f, 0.65f, 0.35f),
        };
        static readonly Color[] Hair = { new Color(0.1f, 0.07f, 0.05f), new Color(0.4f, 0.25f, 0.12f), new Color(0.85f, 0.7f, 0.4f), new Color(0.6f, 0.6f, 0.6f), new Color(0.55f, 0.2f, 0.1f), new Color(0.2f, 0.12f, 0.07f) };
        static readonly Color[] Pants = { new Color(0.25f, 0.3f, 0.45f), new Color(0.2f, 0.2f, 0.22f), new Color(0.45f, 0.4f, 0.32f), new Color(0.3f, 0.32f, 0.3f), new Color(0.18f, 0.24f, 0.4f), new Color(0.5f, 0.45f, 0.38f) };
        static readonly Color[] Shoes = { new Color(0.06f, 0.06f, 0.065f), new Color(0.3f, 0.2f, 0.12f), new Color(0.85f, 0.85f, 0.85f), new Color(0.2f, 0.25f, 0.4f), new Color(0.6f, 0.15f, 0.15f) };
        static readonly Color[] Bags = { new Color(0.25f, 0.3f, 0.38f), new Color(0.55f, 0.2f, 0.18f), new Color(0.2f, 0.35f, 0.25f), new Color(0.4f, 0.32f, 0.2f), new Color(0.12f, 0.12f, 0.14f) };

        static HairStyle RandomHair(bool varied)
        {
            float roll = Random.value;
            if (!varied) return roll < 0.45f ? HairStyle.Short : roll < 0.75f ? HairStyle.Buzz : roll < 0.88f ? HairStyle.Bald : HairStyle.Curly;
            return roll < 0.26f ? HairStyle.Short : roll < 0.42f ? HairStyle.Long : roll < 0.54f ? HairStyle.Ponytail : roll < 0.63f ? HairStyle.Bun
                : roll < 0.75f ? HairStyle.Buzz : roll < 0.85f ? HairStyle.Curly : HairStyle.Bald;
        }

        // Shoes, faces and accessories shared by suspects and civilians.
        static void Accessorize(ref Appearance look, bool suspect)
        {
            look.hair = RandomHair(!suspect || Random.value < 0.3f);
            look.sneakers = Random.value < (suspect ? 0.6f : 0.5f);
            look.shoes = look.sneakers ? Shoes[Random.Range(0, Shoes.Length)] : Shoes[Random.Range(0, 2)];
            look.beard = look.hair != HairStyle.Long && look.hair != HairStyle.Ponytail && look.hair != HairStyle.Bun && Random.value < (suspect ? 0.35f : 0.18f);
            look.glasses = Random.value < (suspect ? 0.15f : 0.22f) ? (suspect ? 2 : (Random.value < 0.25f ? 2 : 1)) : 0;
            look.bag = Bags[Random.Range(0, Bags.Length)];
        }

        static Color Vary(Color c, float amount)
        {
            float k = 1f + Random.Range(-amount, amount);
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        // Suspects: the archetype's colors with variations in clothing, headwear and build.
        public static Appearance SuspectLook(EnemyData data)
        {
            var look = new Appearance
            {
                shirt = Vary(data.shirtColor, 0.18f),
                pants = Pants[Random.Range(0, Pants.Length)],
                skin = RandomSkin(),
                headwear = data.headwear == 3 ? new Color(0.12f, 0.12f, 0.13f) : data.headwear == 0 ? Hair[Random.Range(0, Hair.Length)] : new Color(0.08f, 0.08f, 0.09f),
                head = data.headwear == 0 ? HeadStyle.Hair : data.headwear == 1 ? HeadStyle.Cap : data.headwear == 2 ? HeadStyle.Balaclava : HeadStyle.Helmet,
                vestOn = data.damageReduction > 0.2f,
                vest = new Color(0.12f, 0.12f, 0.12f),
                ring = Color.red,
                armed = data.armed,
                height = Random.Range(0.94f, 1.06f),
                width = Random.Range(0.95f, 1.08f),
            };
            Accessorize(ref look, true);
            look.gloves = Random.value < 0.5f;
            switch (data.archetype)
            {
                case EnemyArchetype.Guard: look.outfit = Swat.Outfit.Uniform; look.glasses = 0; look.holster = true; break;
                case EnemyArchetype.Leader:
                    look.outfit = Random.value < 0.5f ? Swat.Outfit.Suit : Swat.Outfit.Jacket;
                    look.accent = new Color(0.12f, 0.12f, 0.14f);
                    look.glasses = Random.value < 0.5f ? 2 : 0;
                    look.sneakers = false;
                    look.shoes = Shoes[0];
                    break;
                case EnemyArchetype.Nervous:
                    look.outfit = Swat.Outfit.Hoodie;
                    look.hoodUp = Random.value < 0.5f;
                    look.backpack = Random.value < 0.25f;
                    break;
                case EnemyArchetype.Armored: look.outfit = Swat.Outfit.Plain; look.width = Random.Range(1.05f, 1.12f); look.gloves = true; break;
                case EnemyArchetype.TrainingDummy: look.outfit = Swat.Outfit.HiVis; look.beard = false; look.glasses = 0; break;
                default:
                    float roll = Random.value;
                    look.outfit = roll < 0.4f ? Swat.Outfit.Jacket : roll < 0.7f ? Swat.Outfit.Hoodie : Swat.Outfit.Plain;
                    look.accent = Vary(new Color(0.18f, 0.17f, 0.16f), 0.4f);
                    if (look.head == HeadStyle.Hair && Random.value < 0.35f) { look.head = HeadStyle.Cap; look.headwear = Vary(new Color(0.15f, 0.15f, 0.18f), 0.5f); }
                    look.hoodUp = look.outfit == Swat.Outfit.Hoodie && Random.value < 0.4f;
                    look.shortSleeves = look.outfit == Swat.Outfit.Plain && Random.value < 0.5f;
                    // A bandana over the lower face (instead of a balaclava) for some.
                    look.bandana = look.head != HeadStyle.Balaclava && Random.value < 0.25f;
                    if (look.bandana) look.accent = Random.value < 0.5f ? new Color(0.6f, 0.12f, 0.12f) : new Color(0.12f, 0.12f, 0.14f);
                    break;
            }
            return look;
        }

        // Civilians: outfits by type, clearly different from police and suspects (and on a yellow ring).
        public static Appearance CivilianLook(CivilianType type, Color ring)
        {
            var look = new Appearance
            {
                shirt = CasualShirts[Random.Range(0, CasualShirts.Length)],
                pants = Pants[Random.Range(0, Pants.Length)],
                skin = RandomSkin(),
                headwear = Hair[Random.Range(0, Hair.Length)],
                head = HeadStyle.Hair,
                ring = ring,
                height = Random.Range(0.93f, 1.05f),
                width = Random.Range(0.94f, 1.06f),
                accent = new Color(Random.Range(0.1f, 0.6f), Random.Range(0.1f, 0.4f), Random.Range(0.2f, 0.6f)),
            };
            Accessorize(ref look, false);
            float roll = Random.value;
            switch (type)
            {
                case CivilianType.SecurityGuard:
                    look.outfit = Swat.Outfit.Uniform;
                    look.shirt = new Color(0.5f, 0.55f, 0.6f);
                    look.pants = new Color(0.15f, 0.17f, 0.22f);
                    look.head = HeadStyle.Cap;
                    look.headwear = new Color(0.15f, 0.17f, 0.22f);
                    look.holster = true;
                    look.sneakers = false;
                    look.shoes = Shoes[0];
                    break;
                case CivilianType.OfficeWorker:
                case CivilianType.Injured:
                case CivilianType.Hostage:
                    if (roll < 0.2f) { look.outfit = Swat.Outfit.Suit; look.accent = new Color(0.16f, 0.17f, 0.22f); look.shirt = new Color(0.93f, 0.93f, 0.9f); look.sneakers = false; look.shoes = Shoes[Random.Range(0, 2)]; }
                    else if (roll < 0.32f) look.outfit = Swat.Outfit.HiVis; // maintenance worker
                    else look.outfit = Swat.Outfit.Shirt;
                    look.lanyard = type == CivilianType.OfficeWorker && look.outfit != Swat.Outfit.HiVis && Random.value < 0.6f;
                    break;
                case CivilianType.Visitor:
                    look.outfit = roll < 0.5f ? Swat.Outfit.Jacket : Swat.Outfit.Plain;
                    look.backpack = Random.value < 0.4f;
                    break;
                default: // residents and people hiding
                    look.outfit = roll < 0.35f ? Swat.Outfit.Hoodie : roll < 0.5f ? Swat.Outfit.HiVis : Swat.Outfit.Plain;
                    look.backpack = Random.value < 0.15f;
                    break;
            }
            look.shortSleeves = (look.outfit == Swat.Outfit.Plain || look.outfit == Swat.Outfit.Shirt) && Random.value < 0.4f;
            look.skirt = (look.outfit == Swat.Outfit.Plain || look.outfit == Swat.Outfit.Shirt || look.outfit == Swat.Outfit.Suit) && Random.value < 0.18f;
            look.bandage = type == CivilianType.Injured;
            return look;
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
            // Guns are drawn a bit larger in pixel art so their shapes survive the low resolution.
            parts.gunRoot.localScale = Vector3.one * (QualityManager.PixelArt ? 1.25f : 1f);
            parts.ShowWeapon(true);
            Shapes.Toonify(parts.gunRoot);
            parts.CacheRenderers();
        }
    }
}
