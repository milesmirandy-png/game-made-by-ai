using UnityEngine;

namespace Swat
{
    // References to the parts of a blocky character model, plus a few poses.
    public class CharacterParts
    {
        public Transform model;
        public Transform leftArm;
        public Transform rightArm;
        public Transform gun;
        public Transform muzzle;
        public Renderer ring;
        public GameObject alertMarker;

        public void SetArms(Vector3 leftEuler, Vector3 rightEuler)
        {
            leftArm.localRotation = Quaternion.Euler(leftEuler);
            rightArm.localRotation = Quaternion.Euler(rightEuler);
        }

        public void PoseAiming() { SetArms(new Vector3(-80f, 0f, 25f), new Vector3(-85f, 0f, -10f)); }
        public void PoseHandsUp() { SetArms(new Vector3(180f, 0f, -20f), new Vector3(180f, 0f, 20f)); }
        public void PoseCuffed() { SetArms(new Vector3(25f, 0f, 15f), new Vector3(25f, 0f, -15f)); }
        public void PoseRelaxed() { SetArms(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 6f)); }

        public void SetRingColor(Color color)
        {
            if (ring != null) ring.sharedMaterial = Shapes.Mat(color, 1.5f);
        }

        // Tips the model over backwards.
        public void Fall()
        {
            model.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.localPosition = new Vector3(0f, 0.2f, 0f);
            if (ring != null) ring.enabled = false;
            if (alertMarker != null) alertMarker.SetActive(false);
            if (gun != null) gun.gameObject.SetActive(false);
        }
    }

    // Builds the low-poly people. Each character gets a coloured ring on the
    // floor so you can tell who is who from above (blue police, red suspects,
    // yellow civilians).
    public static class CharacterFactory
    {
        public static CharacterParts Build(Transform root, Color shirt, Color pants, Color skin, Color headwear, Color ringColor, bool armed, bool helmet)
        {
            var parts = new CharacterParts();
            parts.model = new GameObject("Model").transform;
            parts.model.SetParent(root, false);
            var m = parts.model;

            Shapes.Box("Legs", m, new Vector3(0f, 0.4f, 0f), new Vector3(0.42f, 0.8f, 0.26f), pants, false);
            Shapes.Box("Torso", m, new Vector3(0f, 1.1f, 0f), new Vector3(0.54f, 0.62f, 0.32f), shirt, false);
            Shapes.Make(PrimitiveType.Sphere, "Head", m, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.32f, skin, false);
            if (helmet)
                Shapes.Make(PrimitiveType.Sphere, "Helmet", m, new Vector3(0f, 1.7f, -0.01f), new Vector3(0.36f, 0.22f, 0.38f), headwear, false);
            else
                Shapes.Box("Hair", m, new Vector3(0f, 1.71f, -0.04f), new Vector3(0.3f, 0.12f, 0.28f), headwear, false);

            parts.leftArm = Arm("Arm L", m, new Vector3(-0.33f, 1.34f, 0f), shirt, skin);
            parts.rightArm = Arm("Arm R", m, new Vector3(0.33f, 1.34f, 0f), shirt, skin);

            if (armed)
            {
                parts.gun = Shapes.Box("Gun", m, new Vector3(0.1f, 1.2f, 0.45f), new Vector3(0.08f, 0.11f, 0.5f), new Color(0.06f, 0.06f, 0.07f), false).transform;
                parts.muzzle = new GameObject("Muzzle").transform;
                parts.muzzle.SetParent(parts.gun, false);
                parts.muzzle.localPosition = new Vector3(0f, 0f, 0.55f);
                parts.PoseAiming();
            }
            else
            {
                parts.PoseRelaxed();
            }

            parts.ring = Shapes.Make(PrimitiveType.Cylinder, "Ring", root, new Vector3(0f, 0.04f, 0f), new Vector3(0.95f, 0.01f, 0.95f), ringColor, false, 1.5f).GetComponent<Renderer>();

            parts.alertMarker = new GameObject("Alert Marker");
            parts.alertMarker.transform.SetParent(root, false);
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

        static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.8f, 0.69f), new Color(0.87f, 0.67f, 0.52f), new Color(0.71f, 0.5f, 0.36f),
            new Color(0.55f, 0.37f, 0.25f), new Color(0.38f, 0.25f, 0.17f),
        };

        public static Color RandomSkin()
        {
            return SkinTones[Random.Range(0, SkinTones.Length)];
        }
    }
}
