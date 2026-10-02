using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Short-lived visual effects: bullet tracers, impact debris and light flashes.
    public static class Effects
    {
        static Transform Parent
        {
            get
            {
                var game = GameManager.Instance;
                return game != null ? game.LevelRoot : null;
            }
        }

        public static void Burst(Vector3 position, Vector3 normal, Color color, int count, float speed, float size = 0.05f, float glow = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Shapes.Box("Debris", Parent, Vector3.zero, Vector3.one * size * Random.Range(0.6f, 1.4f), color, false, glow);
                go.transform.SetPositionAndRotation(position, Random.rotation);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var piece = go.AddComponent<EffectPiece>();
                piece.velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.5f, 1.2f);
                piece.physics = true;
                piece.shrink = true;
                piece.life = Random.Range(0.4f, 0.8f);
            }
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.2f) return;
            var go = Shapes.Box("Tracer", Parent, Vector3.zero, new Vector3(0.015f, 0.015f, length), color, false, 3f);
            go.transform.SetPositionAndRotation(from + delta * 0.5f, Quaternion.LookRotation(delta));
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<EffectPiece>().life = 0.04f;
        }

        public static void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(Parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity * Shapes.PointLightScale;
            light.range = range;
            light.shadows = LightShadows.None;
            go.AddComponent<EffectPiece>().life = duration;
        }
    }
}
