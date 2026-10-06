using System.Collections.Generic;
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
        public bool soldier;          // built from the imported soldier model (unless Classic characters is on)
        public Color pouches, gear;   // soldier model: pouches, and gloves/boots (zero = from the vest / dark)
        // Added with the gear update (zero values keep the full kit: helmet, NVG, goggles, balaclava, carrier).
        public int headgear;          // GearCatalog.Headgear
        public int face;              // GearCatalog.Face
        public ArmorStyle armorStyle;
        public int patch, patchColor; // GearCatalog.Patch and colour index (left shoulder and chest)
        public int camo;              // GearCatalog.CamoFor (0 = plain uniform)
        public int facialHair;        // GearCatalog.FacialHair
        public Color hairColor;       // soldier model's hair, brows and beard (zero = dark brown)
        public bool features;         // soldier model: draw eyes and brows (officers)
        public bool longSleeves;      // soldier model: sleeves down to the gloves (false = rolled up, bare forearms)
    }

    // References to the parts of a blocky character, plus helpers to show
    // poses and states. Animation itself is done by ProceduralAnimator.
    public class CharacterParts
    {
        public Transform root, model, head, leftArm, rightArm, leftLeg, rightLeg, gunRoot, muzzle, shield;
        // The imported soldier's arms bend at the elbow (null on the blocky figures, whose arms are one piece);
        // the hand points are where its hands hold the gun.
        public Transform leftForearm, rightForearm, leftHand, rightHand;
        // Where the gun is held (model space).
        public Vector3 gunHold = new Vector3(0.12f, 1.17f, 0.22f);
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

            // Officers, the game-mode teams and armoured suspects use the imported soldier model (split on the
            // joints the animator moves); everyone else, or everyone with Classic characters on, is built from boxes.
            var soldier = look.soldier ? SoldierModel() : null;
            if (soldier != null) BuildSoldier(parts, look, m, soldier);
            else BuildBlocky(parts, look, m, shoe, skin, hairColor, tactical);

            parts.gunRoot = new GameObject("Gun").transform;
            parts.gunRoot.SetParent(m, false);
            parts.gunRoot.localPosition = parts.gunHold;
            parts.muzzle = new GameObject("Muzzle").transform;
            parts.muzzle.SetParent(parts.gunRoot, false);
            parts.muzzle.localPosition = new Vector3(0f, 0f, 0.5f);
            parts.ShowWeapon(look.armed);

            if (look.shield)
            {
                // The imported riot shield (in police black with a white band), or a box if it's missing.
                var shieldModel = SaveManager.Settings.classicCharacters ? null : ModelLibrary.Get("police_shield");
                if (shieldModel != null)
                {
                    parts.shield = new GameObject("Shield").transform;
                    parts.shield.SetParent(m, false);
                    parts.shield.localPosition = new Vector3(-0.12f, 1.0f, 0.5f);
                    ModelLibrary.Spawn(shieldModel, "shield", parts.shield, Vector3.zero, (slot, original) => new Color(0.07f, 0.08f, 0.1f));
                    Shapes.Box("Label", parts.shield, new Vector3(0f, 0.12f, 0.045f), new Vector3(0.36f, 0.06f, 0.01f), new Color(0.85f, 0.85f, 0.85f), false);
                    Shapes.Box("Visor", parts.shield, new Vector3(0f, 0.37f, 0.005f), new Vector3(0.21f, 0.07f, 0.02f), new Color(0.35f, 0.6f, 0.9f), false, 1.2f);
                }
                else
                {
                    parts.shield = Shapes.Box("Shield", m, new Vector3(-0.12f, 1.0f, 0.5f), new Vector3(0.62f, 1.05f, 0.06f), new Color(0.06f, 0.07f, 0.09f), false).transform;
                    Shapes.Box("Visor", parts.shield, new Vector3(0f, 0.3f, -0.6f), new Vector3(0.6f, 0.08f, 0.5f), new Color(0.35f, 0.6f, 0.9f), false, 1.2f);
                    Shapes.Box("Label", parts.shield, new Vector3(0f, -0.05f, -0.6f), new Vector3(0.7f, 0.08f, 0.5f), new Color(0.85f, 0.85f, 0.85f), false);
                }
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

        // The original blocky figure: legs, torso, outfit, head, face and arms from boxes.
        static void BuildBlocky(CharacterParts parts, Appearance look, Transform m, Color shoe, Color skin, Color hairColor, bool tactical)
        {
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

        }

        // The smoothed soldier, or the original lower-detail one on the Potato and Low presets
        // (null with Classic characters on).
        public static ModelLibrary.Model SoldierModel()
        {
            if (SaveManager.Settings.classicCharacters) return null;
            var low = QualityManager.Current.textures == 0 ? ModelLibrary.Get("soldier_low") : null;
            return low ?? ModelLibrary.Get("soldier");
        }

        // The soldier model's colours for a look: uniform, vest, pouches, helmet, skin, gloves and boots
        // (headset, goggles, watch and torch keep their own). Also used for the first-person arms.
        public static System.Func<string, Color, Color> SoldierColors(Appearance look)
        {
            Color vest = look.vestOn ? look.vest : Shapes.Shade(look.shirt, 0.85f);
            return (slot, original) =>
            {
                switch (slot)
                {
                    case "Vest": return vest;
                    case "Pouches": case "Bag": return look.pouches.a > 0f ? look.pouches : Shapes.Shade(vest, 1.35f);
                    case "Shirt": return look.shirt;
                    case "Pants": return look.pants;
                    case "Helmet": return look.headwear;
                    case "Skin": return look.skin;
                    case "Gloves": case "Shoes": return look.gear.a > 0f ? look.gear : Gear;
                    case "Belt": case "Pads": return Shapes.Shade(look.gear.a > 0f ? look.gear : Gear, 1.5f);
                    case "Mask": return Shapes.Shade(look.headwear, 0.85f);
                    case "Scarf": return Shapes.Shade(look.shirt, 0.7f);
                    default: return original; // headset, goggles, watch, torch: their own colours
                }
            };
        }

        // The arms: the model's forearms are bare (rolled-up sleeves); long sleeves colour that skin as the
        // shirt (and carry the camouflage on), down to the gloves.
        public static System.Func<string, Color, Color> ArmColors(Appearance look)
        {
            var colors = SoldierColors(look);
            if (!look.longSleeves) return colors;
            return (slot, original) => slot == "Skin" ? colors("Shirt", original) : colors(slot, original);
        }

        public static ModelLibrary.Camo ArmCamo(Appearance look)
        {
            return GearCatalog.CamoFor(look.camo, look.longSleeves);
        }

        // The imported soldier: torso on the model, head, arms and legs on their own pivots (where the
        // animator turns them), recoloured from the look: uniform, vest, helmet, skin, gloves and boots.
        static void BuildSoldier(CharacterParts parts, Appearance look, Transform m, ModelLibrary.Model model)
        {
            Color vest = look.vestOn ? look.vest : Shapes.Shade(look.shirt, 0.85f);
            var recolor = SoldierColors(look);
            var camo = GearCatalog.CamoFor(look.camo);
            ModelLibrary.Spawn(model, "torso", m, Vector3.zero, recolor, TorsoHidden(look), camo);
            parts.leftLeg = SoldierPart(model, "legL", m, recolor, null, null, camo);
            parts.rightLeg = SoldierPart(model, "legR", m, recolor, null, null, camo);
            var armColors = ArmColors(look);
            var armCamo = ArmCamo(look);
            parts.leftArm = SoldierPart(model, "armL", m, armColors, null, null, armCamo);
            parts.rightArm = SoldierPart(model, "armR", m, armColors, null, null, armCamo);
            parts.leftForearm = SoldierPart(model, "foreL", parts.leftArm, armColors, model.Find("armL"), null, armCamo);
            parts.rightForearm = SoldierPart(model, "foreR", parts.rightArm, armColors, model.Find("armR"), null, armCamo);
            parts.leftHand = SoldierPoint(model, "handL", parts.leftForearm, model.Find("foreL"));
            parts.rightHand = SoldierPoint(model, "handR", parts.rightForearm, model.Find("foreR"));
            // The gun sits a little higher and more central than on the blocky figures, within reach of
            // these arms (the animator puts both hands on it).
            parts.gunHold = new Vector3(0.08f, 1.22f, 0.18f);
            parts.head = SoldierPart(model, "head", m, recolor, null, HeadHidden(look));
            // A slightly bigger head in pixel art, as for the blocky figures, so helmets read from above.
            if (QualityManager.PixelArt) parts.head.localScale = Vector3.one * 1.15f;
            DressHead(parts.head, look, model);
            DressArmor(parts, look, m, vest, model);
            if (look.idMarker && look.armorStyle != ArmorStyle.None)
            {
                // A radio on the right of the chest with a short antenna.
                float chest = MeshKit.FrontZ(model, "torso", "Vest", 0.1f, 1.37f, 0.155f);
                Shapes.Box("Radio", m, new Vector3(0.1f, 1.37f, chest + 0.012f), new Vector3(0.036f, 0.07f, 0.024f), new Color(0.1f, 0.1f, 0.11f), false);
                Shapes.Box("Radio Knob", m, new Vector3(0.09f, 1.412f, chest + 0.012f), new Vector3(0.01f, 0.014f, 0.01f), new Color(0.2f, 0.2f, 0.21f), false);
                Shapes.Box("Antenna", m, new Vector3(0.115f, 1.44f, chest + 0.006f), new Vector3(0.006f, 0.075f, 0.006f), new Color(0.07f, 0.07f, 0.08f), false);
            }
            if (look.patch > 0)
            {
                // The chosen patch on the left shoulder and the chest (the right shoulder keeps the squad colour).
                Color colour = GearCatalog.PatchColor(look.patchColor);
                Patch(parts.leftArm, new Vector3(MeshKit.OuterX(model, "armL", -0.2f, -0.14f, -1f, -0.09f) + 0.002f, -0.17f, 0.018f), Quaternion.Euler(0f, -90f, 0f), 0.07f, look.patch, colour);
                bool plates = look.armorStyle != ArmorStyle.None;
                Patch(m, new Vector3(-0.075f, 1.34f, plates ? 0.168f : 0.152f), Quaternion.identity, 0.06f, look.patch, colour);
            }

            if (look.idMarker)
            {
                float top = GearCatalog.HasHelmet(look.headgear) ? 0.285f : 0.268f;
                Shapes.Box("ID Top", parts.head, new Vector3(0f, top, -0.03f), new Vector3(0.1f, 0.02f, 0.14f), look.idColor, false, 1.2f);
                Shapes.Box("Patch", parts.rightArm, new Vector3(MeshKit.OuterX(model, "armR", -0.2f, -0.14f, 1f, 0.09f) - 0.002f, -0.17f, 0.018f), new Vector3(0.01f, 0.07f, 0.07f), look.idColor, false);
            }
            // Shoulder tabs in the squad (or team) colour: what you see first from above.
            Color tab = look.idMarker ? Shapes.Shade(look.idColor, 0.85f) : Shapes.Shade(vest, 1.25f);
            Shapes.Box("Shoulder Tab", parts.leftArm, new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.04f, 0.15f), tab, false);
            Shapes.Box("Shoulder Tab", parts.rightArm, new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.04f, 0.15f), tab, false);
        }

        // A part on its own pivot; within another part (a forearm in an upper arm), relative to that part's pivot.
        static Transform SoldierPart(ModelLibrary.Model model, string name, Transform parent, System.Func<string, Color, Color> recolor, ModelLibrary.Part within = null, ICollection<string> hidden = null, ModelLibrary.Camo camo = null)
        {
            var part = model.Find(name);
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = part != null ? part.pivot - (within != null ? within.pivot : Vector3.zero) : Vector3.zero;
            ModelLibrary.Spawn(model, name, pivot, Vector3.zero, recolor, hidden, camo);
            return pivot;
        }

        // ---- Gear (the loadout's Look tab) ----

        // The soldier model's head carries the helmet, night vision ("Scope"), goggles, helmet light,
        // headset ("Band") and balaclava ("Mask") as their own colour slots; the torso has the vest,
        // pouches and back pack. Leaving slots out gives the other kits.
        static HashSet<string> HeadHidden(Appearance look)
        {
            var hide = new HashSet<string>();
            var headgear = (GearCatalog.Headgear)look.headgear;
            if (headgear != GearCatalog.Headgear.HelmetFull) hide.Add("Scope");
            if (headgear >= GearCatalog.Headgear.Helmet) { hide.Add("Glasses"); hide.Add("Material.002"); }
            if (!GearCatalog.HasHelmet(look.headgear)) { hide.Add("Helmet"); hide.Add("Torch"); }
            if (headgear == GearCatalog.Headgear.Boonie || headgear == GearCatalog.Headgear.BareHead) hide.Add("Band");
            if (look.face != (int)GearCatalog.Face.Balaclava) hide.Add("Mask");
            return hide;
        }

        static HashSet<string> TorsoHidden(Appearance look)
        {
            var hide = new HashSet<string>();
            if (look.face != (int)GearCatalog.Face.Balaclava) hide.Add("Mask");
            switch (look.armorStyle)
            {
                case ArmorStyle.Light: hide.Add("Pouches"); hide.Add("Bag"); break;
                case ArmorStyle.None: hide.Add("Vest"); hide.Add("Pouches"); hide.Add("Bag"); break;
            }
            return hide;
        }

        // Headgear, face and hair pieces the model doesn't have. Hair, beards, cap crowns and the gas mask
        // are shells grown from the head's own surface (MeshKit), so they fit it; brims and the face shield
        // are their own low-poly meshes. Head space: the skull top is about 0.23 m up, the face looks along +z.
        static void DressHead(Transform head, Appearance look, ModelLibrary.Model model)
        {
            Color helmet = look.headwear.a > 0f ? look.headwear : new Color(0.12f, 0.13f, 0.15f);
            Color dark = look.gear.a > 0f ? look.gear : new Color(0.08f, 0.08f, 0.09f);
            Color hair = look.hairColor.a > 0f ? look.hairColor : GearCatalog.HairColor(1);
            string[] skin = { "Skin" };
            var headgear = (GearCatalog.Headgear)look.headgear;
            switch (headgear)
            {
                case GearCatalog.Headgear.HelmetVisor:
                    MeshKit.Piece("Face Shield", head, MeshKit.Arc(0.125f, -62f, 62f, 0.15f, 8, "face shield"), new Color(0.16f, 0.21f, 0.27f), new Vector3(0f, 0.005f, -0.03f), Quaternion.identity);
                    Shapes.Box("Shield Rim", head, new Vector3(0f, 0.157f, 0.088f), new Vector3(0.17f, 0.016f, 0.024f), Shapes.Shade(helmet, 0.8f), false);
                    break;
                case GearCatalog.Headgear.OpsCap:
                    MeshKit.Piece("Cap", head, MeshKit.Shell(model, "head", skin, (c, top) => top > 0.16f, 0.016f, 0.1f, "cap crown"), helmet, Vector3.zero, Quaternion.identity);
                    MeshKit.Piece("Brim", head, MeshKit.Brim(0.1f, -80f, 80f, 0.014f, 10, 0f, "cap brim"), Shapes.Shade(helmet, 0.88f), new Vector3(0f, 0.152f, 0.03f), Quaternion.identity);
                    break;
                case GearCatalog.Headgear.Beanie:
                    MeshKit.Piece("Beanie", head, MeshKit.Shell(model, "head", skin, (c, top) => top > 0.14f, 0.016f, 0.14f, "beanie"), dark, Vector3.zero, Quaternion.identity);
                    MeshKit.Piece("Fold", head, MeshKit.Shell(model, "head", skin, (c, top) => top > 0.14f && c.y < 0.165f, 0.024f, 0.05f, "beanie fold"), Shapes.Shade(dark, 0.8f), Vector3.zero, Quaternion.identity);
                    break;
                case GearCatalog.Headgear.Boonie:
                    MeshKit.Piece("Crown", head, MeshKit.Shell(model, "head", skin, (c, top) => top > 0.16f, 0.018f, 0.1f, "boonie crown"), look.pants, Vector3.zero, Quaternion.identity);
                    MeshKit.Piece("Brim", head, MeshKit.Brim(0.17f, 0f, 360f, 0.03f, 16, 0.095f, "boonie brim"), Shapes.Shade(look.pants, 0.92f), new Vector3(0f, 0.152f, -0.025f), Quaternion.identity);
                    break;
                case GearCatalog.Headgear.BareHead:
                    // Short hair: lower at the back than at the forehead.
                    MeshKit.Piece("Hair", head, MeshKit.Shell(model, "head", skin, (c, top) => c.y > (c.z > 0f ? 0.175f : 0.175f + c.z * 0.7f), 0.009f, 0.1f, "hair"), hair, Vector3.zero, Quaternion.identity);
                    break;
            }

            var face = (GearCatalog.Face)look.face;
            switch (face)
            {
                case GearCatalog.Face.GasMask:
                    MeshKit.Piece("Gas Mask", head, MeshKit.Shell(model, "head", new[] { "Mask" }, (c, top) => c.z > -0.02f && c.y < 0.125f, 0.012f, 0.1f, "gas mask"), new Color(0.13f, 0.13f, 0.14f), Vector3.zero, Quaternion.identity);
                    Shapes.Box("Lens", head, new Vector3(0.032f, 0.104f, 0.078f), new Vector3(0.04f, 0.032f, 0.008f), new Color(0.32f, 0.38f, 0.44f), false).transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                    Shapes.Box("Lens", head, new Vector3(-0.032f, 0.104f, 0.078f), new Vector3(0.04f, 0.032f, 0.008f), new Color(0.32f, 0.38f, 0.44f), false).transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                    var filter = Shapes.Make(PrimitiveType.Cylinder, "Filter", head, new Vector3(0f, 0.022f, 0.1f), new Vector3(0.062f, 0.024f, 0.062f), new Color(0.2f, 0.21f, 0.2f), false).transform;
                    filter.localRotation = Quaternion.Euler(70f, 0f, 0f);
                    break;
                case GearCatalog.Face.Shades:
                    Shapes.Box("Shades", head, new Vector3(0f, 0.108f, 0.072f), new Vector3(0.11f, 0.022f, 0.01f), new Color(0.04f, 0.04f, 0.05f), false);
                    Shapes.Box("Shades", head, new Vector3(0.05f, 0.108f, 0.05f), new Vector3(0.006f, 0.02f, 0.05f), new Color(0.04f, 0.04f, 0.05f), false).transform.localRotation = Quaternion.Euler(0f, 34f, 0f);
                    Shapes.Box("Shades", head, new Vector3(-0.05f, 0.108f, 0.05f), new Vector3(0.006f, 0.02f, 0.05f), new Color(0.04f, 0.04f, 0.05f), false).transform.localRotation = Quaternion.Euler(0f, -34f, 0f);
                    break;
            }

            // Eyes and brows, painted on the face as small flat pieces turned to its sides (goggles and a
            // gas mask cover them; shades cover the eyes).
            bool goggles = headgear == GearCatalog.Headgear.HelmetFull || headgear == GearCatalog.Headgear.HelmetGoggles;
            if (look.features && !goggles && face != GearCatalog.Face.GasMask)
            {
                // Sit them on this model's face (the smoothed and low-detail heads differ by a few millimetres).
                float eyeZ = MeshKit.FrontZ(model, "head", "Skin", 0.03f, 0.106f, 0.064f) + 0.001f;
                float browZ = MeshKit.FrontZ(model, "head", "Skin", 0.03f, 0.123f, 0.06f) + 0.001f;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (face != GearCatalog.Face.Shades)
                        Shapes.Box("Eye", head, new Vector3(side * 0.03f, 0.106f, eyeZ), new Vector3(0.019f, 0.011f, 0.006f), new Color(0.06f, 0.05f, 0.05f), false).transform.localRotation = Quaternion.Euler(0f, side * 34f, 0f);
                    Shapes.Box("Brow", head, new Vector3(side * 0.03f, 0.123f, browZ), new Vector3(0.027f, 0.008f, 0.006f), hair, false).transform.localRotation = Quaternion.Euler(0f, side * 34f, -side * 8f);
                }
            }

            // Facial hair shows with a bare face or shades (a balaclava or gas mask covers it).
            if (face == GearCatalog.Face.Bare || face == GearCatalog.Face.Shades)
                switch ((GearCatalog.FacialHair)look.facialHair)
                {
                    case GearCatalog.FacialHair.Stubble:
                        MeshKit.Piece("Stubble", head, MeshKit.Shell(model, "head", skin, (c, top) => c.y < 0.07f && c.z > 0f, 0.002f, 0f, "stubble"), Shapes.Shade(look.skin, 0.7f), Vector3.zero, Quaternion.identity);
                        break;
                    case GearCatalog.FacialHair.Beard:
                        MeshKit.Piece("Beard", head, MeshKit.Shell(model, "head", skin, (c, top) => c.y < 0.07f && c.z > 0f, 0.007f, 0.1f, "beard"), hair, Vector3.zero, Quaternion.identity);
                        break;
                    case GearCatalog.FacialHair.Moustache:
                        Shapes.Box("Moustache", head, new Vector3(0f, 0.07f, MeshKit.FrontZ(model, "head", "Skin", 0f, 0.07f, 0.075f) + 0.003f), new Vector3(0.045f, 0.011f, 0.01f), hair, false);
                        break;
                }
        }

        // Heavy armor adds shoulder guards and a collar shaped to the arms and neck, and a groin plate.
        static void DressArmor(CharacterParts parts, Appearance look, Transform m, Color vest, ModelLibrary.Model model)
        {
            if (look.armorStyle != ArmorStyle.Heavy) return;
            Color guard = Shapes.Shade(vest, 1.1f);
            string[] sleeve = { "Shirt" };
            MeshKit.Piece("Shoulder Guard", parts.leftArm, MeshKit.Shell(model, "armL", sleeve, (c, top) => top > -0.13f, 0.014f, 0.08f, "guard L"), guard, Vector3.zero, Quaternion.identity);
            MeshKit.Piece("Shoulder Guard", parts.rightArm, MeshKit.Shell(model, "armR", sleeve, (c, top) => top > -0.13f, 0.014f, 0.08f, "guard R"), guard, Vector3.zero, Quaternion.identity);
            MeshKit.Piece("Collar", m, MeshKit.Shell(model, "torso", new[] { "Scarf" }, (c, top) => true, 0.016f, 0.06f, "collar"), guard, Vector3.zero, Quaternion.identity);
            Shapes.Box("Groin Plate", m, new Vector3(0f, 0.9f, 0.14f), new Vector3(0.17f, 0.15f, 0.03f), guard, false);
        }

        // A patch design in boxes, flat against a surface: local +z of the rotation is the outward face.
        static void Patch(Transform parent, Vector3 position, Quaternion rotation, float size, int design, Color colour)
        {
            var root = new GameObject("Patch Design").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = rotation;
            // Dark cloth behind light designs, tan behind black ones.
            Color cloth = colour.r + colour.g + colour.b < 0.5f ? new Color(0.62f, 0.54f, 0.4f) : new Color(0.1f, 0.11f, 0.12f);
            Shapes.Box("Cloth", root, Vector3.zero, new Vector3(size, size * 0.8f, 0.006f), cloth, false);
            float t = size * 0.16f, front = 0.005f;
            System.Action<float, float, float, float, float> bar = (x, y, w, h, angle) =>
                Shapes.Box("Mark", root, new Vector3(x, y, front), new Vector3(w, h, 0.004f), colour, false).transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            switch ((GearCatalog.Patch)design)
            {
                case GearCatalog.Patch.Badge:
                    bar(0f, -size * 0.04f, size * 0.55f, size * 0.5f, 0f);
                    bar(0f, size * 0.29f, size * 0.8f, t * 0.8f, 0f);
                    break;
                case GearCatalog.Patch.Stripes:
                    for (int i = -1; i <= 1; i++) bar(0f, i * size * 0.24f, size * 0.9f, t, 0f);
                    break;
                case GearCatalog.Patch.Chevron:
                    bar(-size * 0.16f, 0f, size * 0.5f, t, -38f);
                    bar(size * 0.16f, 0f, size * 0.5f, t, 38f);
                    break;
                case GearCatalog.Patch.Star:
                    for (int i = 0; i < 3; i++) bar(0f, 0f, size * 0.62f, t, i * 60f);
                    break;
                case GearCatalog.Patch.Cross:
                    bar(0f, 0f, size * 0.6f, size * 0.2f, 0f);
                    bar(0f, 0f, size * 0.2f, size * 0.6f, 0f);
                    break;
                case GearCatalog.Patch.Diamond:
                    bar(0f, 0f, size * 0.42f, size * 0.42f, 45f);
                    break;
                case GearCatalog.Patch.Flag:
                {
                    // Stripes and a blue canton (its own colours, not the patch colour).
                    Color red = new Color(0.72f, 0.12f, 0.12f), white = new Color(0.92f, 0.92f, 0.9f);
                    float h = size * 0.8f, stripe = h / 5f;
                    for (int i = 0; i < 5; i++)
                        Shapes.Box("Stripe", root, new Vector3(0f, -h * 0.5f + stripe * (i + 0.5f), front), new Vector3(size, stripe, 0.004f), i % 2 == 0 ? red : white, false);
                    Shapes.Box("Canton", root, new Vector3(-size * 0.27f, h * 0.2f, front + 0.001f), new Vector3(size * 0.46f, h * 0.6f, 0.004f), new Color(0.15f, 0.22f, 0.45f), false);
                    break;
                }
                case GearCatalog.Patch.Target:
                    bar(0f, size * 0.27f, size * 0.6f, t * 0.7f, 0f);
                    bar(0f, -size * 0.27f, size * 0.6f, t * 0.7f, 0f);
                    bar(size * 0.27f, 0f, t * 0.7f, size * 0.6f, 0f);
                    bar(-size * 0.27f, 0f, t * 0.7f, size * 0.6f, 0f);
                    bar(0f, 0f, t, t, 0f);
                    break;
            }
        }

        static Transform SoldierPoint(ModelLibrary.Model model, string name, Transform parent, ModelLibrary.Part within)
        {
            var part = model.Find(name);
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = part != null && within != null ? part.pivot - within.pivot : new Vector3(0f, -0.28f, 0.05f);
            return point;
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
                case EnemyArchetype.Armored:
                    // Kitted out like a soldier, in black.
                    look.outfit = Swat.Outfit.Plain; look.width = Random.Range(1.0f, 1.06f); look.gloves = true;
                    look.soldier = true;
                    look.shirt = new Color(0.14f, 0.14f, 0.15f); look.pants = new Color(0.12f, 0.12f, 0.13f);
                    look.vest = new Color(0.2f, 0.19f, 0.17f); look.vestOn = true; look.headwear = new Color(0.1f, 0.1f, 0.11f);
                    break;
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
            SetWeapon(parts, weapon, attachments, null);
        }

        // modelOverride: a different imported gun model for the same weapon (suspects' variety).
        public static void SetWeapon(CharacterParts parts, WeaponData weapon, OfficerLoadout attachments, string modelOverride)
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
            float length = WeaponModels.Build(weapon, parts.gunRoot, attachments, modelOverride);
            parts.muzzle.localPosition = new Vector3(0f, 0.01f, length);
            // Guns are drawn a bit larger in pixel art so their shapes survive the low resolution.
            parts.gunRoot.localScale = Vector3.one * (QualityManager.PixelArt ? 1.25f : 1f);
            parts.ShowWeapon(true);
            Shapes.Toonify(parts.gunRoot);
            parts.CacheRenderers();
        }
    }
}
