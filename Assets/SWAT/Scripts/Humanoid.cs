using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public struct HumanParts
    {
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public GameObject[] hitboxes;
    }

    // Builds a blocky person: legs, torso, head and two arms that pivot at the shoulder.
    public static class Humanoid
    {
        static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.8f, 0.69f), new Color(0.87f, 0.67f, 0.52f), new Color(0.71f, 0.5f, 0.36f),
            new Color(0.55f, 0.37f, 0.25f), new Color(0.38f, 0.25f, 0.17f),
        };

        public static Color RandomSkin()
        {
            return SkinTones[Random.Range(0, SkinTones.Length)];
        }

        public static HumanParts Build(Transform root, IShootable owner, Color shirt, Color pants, Color hands, Color headColor, bool kneeling)
        {
            float drop = kneeling ? 0.42f : 0f;
            float legHeight = 0.82f - drop;
            var hitboxes = new List<GameObject>();

            hitboxes.Add(Hit(Shapes.Box("Leg L", root, new Vector3(-0.11f, legHeight / 2f, 0f), new Vector3(0.17f, legHeight, 0.2f), pants), owner, 0.7f, false));
            hitboxes.Add(Hit(Shapes.Box("Leg R", root, new Vector3(0.11f, legHeight / 2f, 0f), new Vector3(0.17f, legHeight, 0.2f), pants), owner, 0.7f, false));
            hitboxes.Add(Hit(Shapes.Box("Torso", root, new Vector3(0f, 1.13f - drop, 0f), new Vector3(0.48f, 0.62f, 0.27f), shirt), owner, 1f, false));
            var head = Shapes.Make(PrimitiveType.Sphere, "Head", root, new Vector3(0f, 1.6f - drop, 0f), Vector3.one * 0.3f, headColor);
            hitboxes.Add(Hit(head, owner, 4f, true));

            var parts = new HumanParts();
            parts.head = head.transform;
            parts.leftArm = Arm("Arm L", root, new Vector3(-0.31f, 1.38f - drop, 0f), shirt, hands);
            parts.rightArm = Arm("Arm R", root, new Vector3(0.31f, 1.38f - drop, 0f), shirt, hands);
            parts.hitboxes = hitboxes.ToArray();
            return parts;
        }

        static Transform Arm(string name, Transform root, Vector3 shoulder, Color sleeve, Color hand)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(root, false);
            pivot.localPosition = shoulder;
            Shapes.Box("Sleeve", pivot, new Vector3(0f, -0.27f, 0f), new Vector3(0.13f, 0.56f, 0.13f), sleeve, false);
            Shapes.Box("Hand", pivot, new Vector3(0f, -0.6f, 0f), new Vector3(0.1f, 0.12f, 0.1f), hand, false);
            return pivot;
        }

        static GameObject Hit(GameObject go, IShootable owner, float multiplier, bool isHead)
        {
            var hitbox = go.AddComponent<Hitbox>();
            hitbox.Owner = owner;
            hitbox.multiplier = multiplier;
            hitbox.isHead = isHead;
            return go;
        }
    }
}
