using UnityEngine;

namespace Swat
{
    // Small reusable set-dressing props built from a few boxes with shared
    // materials: no colliders, no shadows, so they never block navigation or
    // cost much. Each returns its root so callers can position it.
    public static class EnvironmentProps
    {
        static readonly Color DarkMetal = new Color(0.22f, 0.23f, 0.25f);
        static readonly Color LightMetal = new Color(0.55f, 0.57f, 0.6f);
        static readonly Color SafetyRed = new Color(0.78f, 0.1f, 0.08f);

        static Transform Root(string name, Transform parent, Vector3 position, float yaw)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            return root;
        }

        static GameObject Part(string name, Transform root, Vector3 local, Vector3 size, Color color, float glow = 0f)
        {
            var go = Shapes.Box(name, root, local, size, color, false, glow);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static Transform TrashCan(Transform parent, Vector3 position)
        {
            var root = Root("Trash Can", parent, position, 0f);
            var can = Shapes.Make(PrimitiveType.Cylinder, "Can", root, new Vector3(0f, 0.22f, 0f), new Vector3(0.32f, 0.22f, 0.32f), new Color(0.2f, 0.22f, 0.24f), false);
            can.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Part("Lid", root, new Vector3(0f, 0.45f, 0f), new Vector3(0.3f, 0.03f, 0.3f), new Color(0.15f, 0.16f, 0.17f));
            return root;
        }

        public static Transform FilingCabinet(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Filing Cabinet", parent, position, yaw);
            Part("Body", root, new Vector3(0f, 0.65f, 0f), new Vector3(0.5f, 1.3f, 0.6f), LightMetal);
            for (int i = 0; i < 4; i++) Part("Handle", root, new Vector3(0f, 0.25f + i * 0.31f, 0.31f), new Vector3(0.16f, 0.03f, 0.03f), DarkMetal);
            return root;
        }

        public static Transform WaterCooler(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Water Cooler", parent, position, yaw);
            Part("Base", root, new Vector3(0f, 0.5f, 0f), new Vector3(0.34f, 1f, 0.34f), new Color(0.88f, 0.88f, 0.86f));
            var bottle = Shapes.Make(PrimitiveType.Cylinder, "Bottle", root, new Vector3(0f, 1.2f, 0f), new Vector3(0.26f, 0.2f, 0.26f), new Color(0.45f, 0.7f, 0.95f), false, 0.4f);
            bottle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        public static Transform FireExtinguisher(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Fire Extinguisher", parent, position, yaw);
            Part("Bracket", root, new Vector3(0f, 0.95f, -0.03f), new Vector3(0.22f, 0.3f, 0.02f), DarkMetal);
            var body = Shapes.Make(PrimitiveType.Cylinder, "Body", root, new Vector3(0f, 0.85f, 0.07f), new Vector3(0.14f, 0.2f, 0.14f), SafetyRed, false);
            body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Part("Sign", root, new Vector3(0f, 1.3f, -0.02f), new Vector3(0.18f, 0.18f, 0.02f), SafetyRed, 0.3f);
            return root;
        }

        public static Transform ElectricalPanel(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Electrical Panel", parent, position, yaw);
            Part("Box", root, new Vector3(0f, 1.0f, 0.06f), new Vector3(0.55f, 0.75f, 0.12f), new Color(0.6f, 0.62f, 0.58f));
            Part("Warning", root, new Vector3(0f, 1.2f, 0.125f), new Vector3(0.2f, 0.1f, 0.01f), new Color(0.95f, 0.8f, 0.1f));
            Part("Indicator", root, new Vector3(0.18f, 0.75f, 0.125f), new Vector3(0.04f, 0.04f, 0.01f), new Color(0.2f, 1f, 0.3f), 2f);
            return root;
        }

        public static Transform Poster(Transform parent, Vector3 position, float yaw, Color color)
        {
            var root = Root("Poster", parent, position, yaw);
            Part("Frame", root, new Vector3(0f, 0.95f, 0.01f), new Vector3(0.6f, 0.45f, 0.02f), new Color(0.12f, 0.12f, 0.13f));
            Part("Print", root, new Vector3(0f, 0.95f, 0.025f), new Vector3(0.54f, 0.39f, 0.01f), color);
            Part("Stripe", root, new Vector3(0f, 0.84f, 0.031f), new Vector3(0.54f, 0.06f, 0.005f), Shapes.Shade(color, 0.5f));
            return root;
        }

        public static Transform Whiteboard(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Whiteboard", parent, position, yaw);
            Part("Board", root, new Vector3(0f, 1.0f, 0.02f), new Vector3(1.4f, 0.7f, 0.03f), new Color(0.95f, 0.95f, 0.93f));
            Part("Tray", root, new Vector3(0f, 0.63f, 0.06f), new Vector3(1.2f, 0.03f, 0.06f), LightMetal);
            Part("Notes", root, new Vector3(-0.3f, 1.05f, 0.04f), new Vector3(0.5f, 0.04f, 0.005f), new Color(0.2f, 0.35f, 0.75f));
            return root;
        }

        public static Transform CleaningCart(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Cleaning Cart", parent, position, yaw);
            Part("Cart", root, new Vector3(0f, 0.35f, 0f), new Vector3(0.5f, 0.5f, 0.8f), new Color(0.85f, 0.75f, 0.15f));
            Part("Bucket", root, new Vector3(0f, 0.68f, 0.2f), new Vector3(0.3f, 0.18f, 0.3f), new Color(0.2f, 0.4f, 0.85f));
            Part("Mop", root, new Vector3(0.15f, 0.9f, -0.25f), new Vector3(0.04f, 1.1f, 0.04f), new Color(0.6f, 0.45f, 0.3f));
            return root;
        }

        public static Transform Toolbox(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Toolbox", parent, position, yaw);
            Part("Box", root, new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.25f), SafetyRed);
            Part("Handle", root, new Vector3(0f, 0.28f, 0f), new Vector3(0.3f, 0.04f, 0.04f), DarkMetal);
            return root;
        }

        public static Transform BoxStack(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Boxes", parent, position, yaw);
            var cardboard = new Color(0.62f, 0.48f, 0.3f);
            Part("Box", root, new Vector3(0f, 0.2f, 0f), new Vector3(0.55f, 0.4f, 0.45f), cardboard);
            Part("Box", root, new Vector3(0.05f, 0.55f, 0.02f), new Vector3(0.45f, 0.3f, 0.38f), Shapes.Shade(cardboard, 0.9f)).transform.localRotation = Quaternion.Euler(0f, 12f, 0f);
            Part("Tape", root, new Vector3(0f, 0.405f, 0f), new Vector3(0.08f, 0.01f, 0.45f), new Color(0.75f, 0.68f, 0.5f));
            return root;
        }

        public static Transform MonitorBank(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Monitor Bank", parent, position, yaw);
            Part("Rack", root, new Vector3(0f, 0.55f, 0f), new Vector3(1.5f, 1.1f, 0.35f), DarkMetal);
            for (int i = 0; i < 3; i++)
                for (int row = 0; row < 2; row++)
                    Part("Screen", root, new Vector3(-0.48f + i * 0.48f, 1.25f + row * 0.32f, 0.02f), new Vector3(0.42f, 0.27f, 0.04f), new Color(0.45f, 0.7f, 1f), 1.4f);
            return root;
        }

        public static Transform VendingMachine(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Vending Machine", parent, position, yaw);
            Part("Body", root, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 0.7f), new Color(0.6f, 0.08f, 0.1f));
            Part("Window", root, new Vector3(-0.08f, 1.05f, 0.355f), new Vector3(0.5f, 1.1f, 0.01f), new Color(0.75f, 0.85f, 0.9f), 0.8f);
            return root;
        }

        public static Transform CoffeeStation(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Coffee Station", parent, position, yaw);
            Part("Counter", root, new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.9f, 0.5f), new Color(0.35f, 0.3f, 0.28f));
            Part("Machine", root, new Vector3(-0.2f, 1.08f, 0f), new Vector3(0.3f, 0.36f, 0.3f), new Color(0.1f, 0.1f, 0.11f));
            Part("Light", root, new Vector3(-0.2f, 1.12f, 0.155f), new Vector3(0.05f, 0.03f, 0.01f), new Color(1f, 0.4f, 0.1f), 2f);
            Part("Cups", root, new Vector3(0.25f, 0.95f, 0f), new Vector3(0.15f, 0.1f, 0.15f), new Color(0.92f, 0.92f, 0.9f));
            return root;
        }

        public static Transform Bench(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Bench", parent, position, yaw);
            Part("Seat", root, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.06f, 0.45f), new Color(0.45f, 0.33f, 0.22f));
            Part("Leg", root, new Vector3(-0.7f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.4f), DarkMetal);
            Part("Leg", root, new Vector3(0.7f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.4f), DarkMetal);
            return root;
        }

        public static Transform Bollard(Transform parent, Vector3 position)
        {
            var root = Root("Bollard", parent, position, 0f);
            var post = Shapes.Make(PrimitiveType.Cylinder, "Post", root, new Vector3(0f, 0.45f, 0f), new Vector3(0.2f, 0.45f, 0.2f), new Color(0.85f, 0.7f, 0.1f), false);
            post.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Part("Band", root, new Vector3(0f, 0.75f, 0f), new Vector3(0.21f, 0.06f, 0.21f), new Color(0.1f, 0.1f, 0.1f));
            return root;
        }

        public static Transform ExitSign(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Exit Sign", parent, position, yaw);
            Part("Housing", root, new Vector3(0f, 0f, 0f), new Vector3(0.42f, 0.16f, 0.06f), new Color(0.9f, 0.9f, 0.88f));
            Part("Face", root, new Vector3(0f, 0f, 0.032f), new Vector3(0.36f, 0.11f, 0.005f), new Color(0.15f, 0.95f, 0.35f), 1.6f);
            return root;
        }

        public static Transform Clock(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("Wall Clock", parent, position, yaw);
            var face = Shapes.Make(PrimitiveType.Cylinder, "Face", root, new Vector3(0f, 1.15f, 0.02f), new Vector3(0.3f, 0.015f, 0.3f), new Color(0.95f, 0.95f, 0.92f), false);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Hand", root, new Vector3(0f, 1.18f, 0.04f), new Vector3(0.02f, 0.1f, 0.005f), new Color(0.1f, 0.1f, 0.1f));
            return root;
        }
    }
}
