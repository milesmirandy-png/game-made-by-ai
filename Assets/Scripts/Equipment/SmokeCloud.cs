using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A temporary cloud of smoke built from a handful of grey spheres (no
    // particle system or transparent shaders, so it's cheap on any GPU).
    // Suspects can't see through it, and anyone standing inside is hard to spot.
    // CS gas is the same cloud, pale yellow, and everyone inside without a gas
    // mask chokes: suspects are dazed and give up more easily, civilians
    // cower, squadmates slow down, and the team leader's aim and pace suffer.
    public class SmokeCloud : MonoBehaviour
    {
        const int MaxPuffs = 14;
        const float GrowTime = 1.2f;
        const float FadeTime = 2f;

        static readonly Stack<SmokeCloud> pool = new Stack<SmokeCloud>();
        static readonly List<SmokeCloud> active = new List<SmokeCloud>();

        Transform[] puffs;
        Vector3[] offsets;
        float[] sizes;
        int puffCount;
        float radius, duration, age, nextGasTick;
        bool gas;

        public float CurrentRadius { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool.Clear();
            active.Clear();
        }

        public static void Spawn(Vector3 position, float radius, float duration)
        {
            Spawn(position, radius, duration, false);
        }

        public static void Spawn(Vector3 position, float radius, float duration, bool gas)
        {
            SmokeCloud cloud = pool.Count > 0 ? pool.Pop() : Create();
            cloud.gas = gas;
            cloud.nextGasTick = 0f;
            for (int i = 0; i < MaxPuffs; i++)
            {
                float shade = Random.Range(0.72f, 0.85f);
                cloud.puffs[i].GetComponent<Renderer>().sharedMaterial = Shapes.Mat(gas ? new Color(shade + 0.08f, shade + 0.06f, shade * 0.62f) : new Color(shade, shade, shade + 0.02f));
            }
            cloud.transform.position = position;
            cloud.radius = radius;
            cloud.duration = duration;
            cloud.age = 0f;
            cloud.CurrentRadius = 0f;
            cloud.puffCount = Mathf.Clamp(Mathf.RoundToInt(MaxPuffs * QualityManager.Current.particleScale), 5, MaxPuffs);
            for (int i = 0; i < MaxPuffs; i++)
            {
                Vector2 circle = Random.insideUnitCircle * radius * 0.65f;
                cloud.offsets[i] = new Vector3(circle.x, Random.Range(0.5f, 1.6f), circle.y);
                cloud.sizes[i] = Random.Range(1.6f, 2.6f) * radius / 4f;
                cloud.puffs[i].gameObject.SetActive(i < cloud.puffCount);
                cloud.puffs[i].localScale = Vector3.zero;
            }
            cloud.gameObject.SetActive(true);
            active.Add(cloud);
            AudioManager.Play(Sound.Smoke, position, 0.8f);
        }

        static SmokeCloud Create()
        {
            var go = new GameObject("Smoke Cloud");
            go.transform.SetParent(GameManager.Instance.PoolRoot, false);
            var cloud = go.AddComponent<SmokeCloud>();
            cloud.puffs = new Transform[MaxPuffs];
            cloud.offsets = new Vector3[MaxPuffs];
            cloud.sizes = new float[MaxPuffs];
            for (int i = 0; i < MaxPuffs; i++)
            {
                float shade = Random.Range(0.72f, 0.85f);
                cloud.puffs[i] = Shapes.Make(PrimitiveType.Sphere, "Puff", go.transform, Vector3.zero, Vector3.zero, new Color(shade, shade, shade + 0.02f), false).transform;
            }
            return cloud;
        }

        // True if the line between two points passes through any smoke.
        public static bool Blocks(Vector3 from, Vector3 to)
        {
            for (int i = 0; i < active.Count; i++)
            {
                var cloud = active[i];
                Vector3 center = cloud.transform.position + Vector3.up;
                Vector3 segment = to - from;
                float t = Mathf.Clamp01(Vector3.Dot(center - from, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                if ((from + segment * t - center).sqrMagnitude < cloud.CurrentRadius * cloud.CurrentRadius * 0.8f) return true;
            }
            return false;
        }

        public static bool Contains(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
            {
                Vector3 offset = point - active[i].transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < active[i].CurrentRadius * active[i].CurrentRadius) return true;
            }
            return false;
        }

        // Inside a cloud of CS gas.
        public static bool InGas(Vector3 point)
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (!active[i].gas) continue;
                Vector3 offset = point - active[i].transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < active[i].CurrentRadius * active[i].CurrentRadius) return true;
            }
            return false;
        }

        public static void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) active[i].Release();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (age >= duration)
            {
                Release();
                return;
            }

            float size = Mathf.Min(1f, age / GrowTime) * Mathf.Clamp01((duration - age) / FadeTime);
            CurrentRadius = radius * size;
            if (gas && age >= nextGasTick)
            {
                nextGasTick = age + 0.5f;
                Choke();
            }
            for (int i = 0; i < puffCount; i++)
            {
                float drift = Mathf.Sin(age * 0.6f + i) * 0.15f;
                puffs[i].localPosition = offsets[i] * (0.6f + 0.4f * size) + new Vector3(drift, 0f, drift);
                puffs[i].localScale = Vector3.one * sizes[i] * size;
            }
        }

        // Everyone inside without a mask: twice a second.
        void Choke()
        {
            var ai = AIManager.Instance;
            if (ai == null) return;
            float r2 = CurrentRadius * CurrentRadius;
            Vector3 centre = transform.position;
            foreach (var enemy in ai.Enemies)
                if (enemy != null && Flat(enemy.Position - centre) < r2) enemy.Gassed();
            foreach (var civilian in ai.Civilians)
                if (civilian != null && civilian.IsAlive && Flat(civilian.Position - centre) < r2) civilian.Gassed();
            foreach (var officer in ai.Officers)
                if (officer != null && officer.IsAlive && Flat(officer.Position - centre) < r2) officer.Gassed();
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player != null && player.IsAlive && Flat(player.Position - centre) < r2) player.Gassed();
        }

        static float Flat(Vector3 v) { return v.x * v.x + v.z * v.z; }

        void Release()
        {
            gameObject.SetActive(false);
            active.Remove(this);
            pool.Push(this);
        }
    }
}
