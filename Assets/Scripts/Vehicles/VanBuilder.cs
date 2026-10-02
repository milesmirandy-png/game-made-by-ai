using UnityEngine;

namespace Swat
{
    // The fictional Tactical Response Unit van: a low-poly decorative vehicle
    // with flashing light bar. Used at headquarters and for mission arrival.
    public static class VanBuilder
    {
        public static Transform Build(Transform parent, Vector3 position, float yaw)
        {
            var van = new GameObject("TRU Van").transform;
            van.SetParent(parent, false);
            van.localPosition = position;
            van.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var body = new Color(0.07f, 0.08f, 0.12f);
            var trim = new Color(0.75f, 0.75f, 0.78f);

            var shell = Shapes.Box("Body", van, new Vector3(0f, 1.45f, -0.4f), new Vector3(2.3f, 2.3f, 4.6f), body);
            shell.layer = Layers.World;
            Shapes.Box("Cab", van, new Vector3(0f, 1.1f, 2.5f), new Vector3(2.3f, 1.6f, 1.4f), body);
            Shapes.Box("Windshield", van, new Vector3(0f, 1.45f, 3.21f), new Vector3(2f, 0.6f, 0.02f), new Color(0.15f, 0.2f, 0.25f), false);
            Shapes.Box("Stripe", van, new Vector3(0f, 1.3f, -0.4f), new Vector3(2.32f, 0.25f, 4.62f), trim, false);
            Shapes.Box("Roof Number", van, new Vector3(0f, 2.61f, -0.6f), new Vector3(1.2f, 0.01f, 1.6f), trim, false);
            Shapes.Box("Rear Doors", van, new Vector3(0f, 1.4f, -2.71f), new Vector3(2.1f, 2f, 0.02f), Shapes.Shade(body, 1.4f), false);
            foreach (float x in new[] { -1.1f, 1.1f })
                foreach (float z in new[] { -1.8f, 2.4f })
                {
                    var wheel = Shapes.Make(PrimitiveType.Cylinder, "Wheel", van, new Vector3(x, 0.38f, z), new Vector3(0.76f, 0.13f, 0.76f), new Color(0.05f, 0.05f, 0.05f), false);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            var red = Shapes.Box("Light Red", van, new Vector3(-0.4f, 2.68f, 0.6f), new Vector3(0.7f, 0.14f, 0.3f), new Color(1f, 0.1f, 0.1f), false, 4f);
            var blue = Shapes.Box("Light Blue", van, new Vector3(0.4f, 2.68f, 0.6f), new Vector3(0.7f, 0.14f, 0.3f), new Color(0.15f, 0.35f, 1f), false, 4f);
            var bar = van.gameObject.AddComponent<LightBar>();
            bar.Init(red.GetComponent<Renderer>(), blue.GetComponent<Renderer>());
            return van;
        }
    }
}
