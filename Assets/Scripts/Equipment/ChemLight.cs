using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A glow stick, thrown to mark a room you've cleared. It stays lit for the rest of the mission
    // (a small glowing stick everyone can see, plus a soft light where the tier has dynamic lights)
    // and shows on the tactical map.
    public static class ChemLight
    {
        public static readonly Color Glow = new Color(0.35f, 1f, 0.45f);
        public static readonly List<Vector3> Placed = new List<Vector3>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Placed.Clear();
        }

        public static void Clear()
        {
            Placed.Clear();
        }

        public static void Place(Vector3 position)
        {
            var game = GameManager.Instance;
            if (game == null || game.Level == null) return;
            RaycastHit hit;
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out hit, 3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) position = hit.point;
            var root = new GameObject("Chem Light").transform;
            root.SetParent(game.Level.root, false);
            root.position = position + Vector3.up * 0.02f;
            root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var stick = Shapes.Make(PrimitiveType.Capsule, "Stick", root, new Vector3(0f, 0.012f, 0f), new Vector3(0.025f, 0.06f, 0.025f), Glow, false, 3f).transform;
            stick.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // A faint pool of colour on the floor reads on every tier.
            DecalMesh.Single("Chem Glow", root, root.position + Vector3.up * 0.01f, new Vector2(1.4f, 1.4f), 0f, new Color(Glow.r, Glow.g, Glow.b, 0.35f), Shapes.GlowMaterial(ProceduralTextures.Radial));
            if (QualityManager.Current.dynamicLights) Shapes.PointLight(root, new Vector3(0f, 0.15f, 0f), Glow, 0.9f, 3f);
            Placed.Add(position);
            AudioManager.Play(Sound.LightPlace, position, 0.35f, 1.4f);
        }
    }
}
