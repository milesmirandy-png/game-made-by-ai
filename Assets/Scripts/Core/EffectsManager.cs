using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat
{
    // Bullet tracers, impact debris and light flashes. Every effect object is
    // pooled and animated from this one Update, so firing never allocates and
    // there is no per-effect MonoBehaviour.
    public class EffectsManager : MonoBehaviour
    {
        public static EffectsManager Instance { get; private set; }

        struct Tracer { public GameObject go; public float until; }
        struct Debris { public Transform transform; public Vector3 velocity; public float age, life, size; }
        struct Flash { public Light light; public float age, life, intensity; }

        readonly Stack<GameObject> tracerPool = new Stack<GameObject>();
        readonly Stack<Transform> debrisPool = new Stack<Transform>();
        readonly Stack<Light> lightPool = new Stack<Light>();
        readonly List<Tracer> tracers = new List<Tracer>();
        readonly List<Debris> debris = new List<Debris>();
        readonly List<Flash> flashes = new List<Flash>();

        void Awake()
        {
            Instance = this;
        }

        public void SpawnTracer(Vector3 from, Vector3 to, Color color, float width = 0.04f, float duration = 0.05f)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.1f) return;
            var go = tracerPool.Count > 0 ? tracerPool.Pop() : Shapes.Box("Tracer", transform, Vector3.zero, Vector3.one, color, false, 2f);
            go.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(color, 2f);
            go.transform.SetPositionAndRotation(from + delta * 0.5f, Quaternion.LookRotation(delta));
            go.transform.localScale = new Vector3(width, width, length);
            go.SetActive(true);
            tracers.Add(new Tracer { go = go, until = Time.time + duration });
        }

        public void Burst(Vector3 position, Vector3 normal, Color color, int count, float speed, float size = 0.07f, float glow = 0f)
        {
            count = Mathf.Max(count > 0 ? 1 : 0, Mathf.RoundToInt(count * QualityManager.Current.particleScale));
            var material = Shapes.Mat(color, glow);
            for (int i = 0; i < count; i++)
            {
                Transform piece;
                if (debrisPool.Count > 0) piece = debrisPool.Pop();
                else piece = Shapes.Box("Debris", transform, Vector3.zero, Vector3.one, color, false).transform;
                piece.GetComponent<Renderer>().sharedMaterial = material;
                float pieceSize = size * Random.Range(0.6f, 1.4f);
                piece.SetPositionAndRotation(position, Random.rotation);
                piece.localScale = Vector3.one * pieceSize;
                piece.gameObject.SetActive(true);
                debris.Add(new Debris
                {
                    transform = piece,
                    velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.5f, 1.2f),
                    life = Random.Range(0.35f, 0.7f),
                    size = pieceSize,
                });
            }
        }

        // ---- Surface impacts, bullet marks and shell casings ----

        const int MaxMarks = 48;
        readonly List<MeshRenderer> marks = new List<MeshRenderer>();
        int nextMark;

        public void Impact(Vector3 point, Vector3 normal, Surface surface, bool leaveMark = true)
        {
            if (!leaveMark)
            {
                Burst(point, normal, surface == Surface.Metal ? new Color(1f, 0.85f, 0.4f) : new Color(0.55f, 0.4f, 0.24f), 3, 2.5f, 0.04f, surface == Surface.Metal ? 2f : 0f);
                return;
            }
            switch (surface)
            {
                case Surface.Metal:
                    Burst(point, normal, new Color(1f, 0.85f, 0.4f), 4, 4f, 0.035f, 2f);
                    if (Random.value < 0.3f) AudioManager.Play(Sound.RicochetMetal, point, 0.35f, Random.Range(0.9f, 1.15f), SoundCategory.Weapons);
                    Mark(point, normal, new Color(0.15f, 0.15f, 0.16f, 0.7f));
                    break;
                case Surface.Wood:
                    Burst(point, normal, new Color(0.55f, 0.4f, 0.24f), 3, 2.5f, 0.05f);
                    Mark(point, normal, new Color(0.18f, 0.12f, 0.07f, 0.75f));
                    break;
                case Surface.Glass:
                    Burst(point, normal, new Color(0.75f, 0.9f, 1f), 4, 3f, 0.04f, 0.6f);
                    AudioManager.Play(Sound.ImpactGlass, point, 0.4f, Random.Range(0.9f, 1.1f));
                    Mark(point, normal, new Color(0.85f, 0.92f, 1f, 0.6f));
                    break;
                case Surface.Carpet:
                case Surface.Grass:
                    Burst(point, normal, new Color(0.6f, 0.58f, 0.55f), 2, 1.5f, 0.04f);
                    break;
                default:
                    Burst(point, normal, new Color(0.75f, 0.72f, 0.65f), 3, 2.5f, 0.05f);
                    Mark(point, normal, new Color(0.1f, 0.1f, 0.1f, 0.65f));
                    break;
            }
        }

        // Small marks where bullets hit walls and props; the oldest is reused after 48.
        void Mark(Vector3 point, Vector3 normal, Color color)
        {
            if (QualityManager.Current.particleScale < 0.5f) return;
            MeshRenderer mark;
            if (marks.Count < MaxMarks)
            {
                mark = DecalMesh.Single("Bullet Mark", transform, Vector3.zero, new Vector2(0.09f, 0.09f), 0f, Color.white, Shapes.DecalMaterial(ProceduralTextures.Dot));
                marks.Add(mark);
            }
            else
            {
                mark = marks[nextMark];
                nextMark = (nextMark + 1) % MaxMarks;
            }
            mark.transform.SetPositionAndRotation(point + normal * 0.01f, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            mark.SetPropertyBlock(block);
            mark.gameObject.SetActive(true);
        }

        public void ClearMarks()
        {
            foreach (var mark in marks) if (mark != null) mark.gameObject.SetActive(false);
        }

        // A spent casing flipping out to the right of the gun.
        public void Shell(Vector3 position, Vector3 right, bool shotgun)
        {
            if (QualityManager.Current.particleScale < 0.5f) return;
            Transform piece;
            if (debrisPool.Count > 0) piece = debrisPool.Pop();
            else piece = Shapes.Box("Debris", transform, Vector3.zero, Vector3.one, Color.white, false).transform;
            piece.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(shotgun ? new Color(0.7f, 0.12f, 0.1f) : new Color(0.85f, 0.65f, 0.25f));
            piece.SetPositionAndRotation(position, Random.rotation);
            float size = shotgun ? 0.05f : 0.035f;
            piece.localScale = new Vector3(size * 0.6f, size * 0.6f, size * 1.6f);
            piece.gameObject.SetActive(true);
            debris.Add(new Debris
            {
                transform = piece,
                velocity = (right * Random.Range(1.6f, 2.4f) + Vector3.up * Random.Range(1.5f, 2.3f) + Random.insideUnitSphere * 0.3f),
                life = 0.9f,
                size = size,
            });
            if (Random.value < 0.35f) AudioManager.Play(Sound.Shell, position + right * 0.6f, 0.18f, Random.Range(0.9f, 1.15f), SoundCategory.Weapons);
        }

        public void FlashLight(Vector3 position, Color color, float intensity, float range, float duration)
        {
            if (!QualityManager.Current.dynamicLights) return;
            Light light;
            if (lightPool.Count > 0) light = lightPool.Pop();
            else
            {
                light = new GameObject("Flash Light").AddComponent<Light>();
                light.transform.SetParent(transform, false);
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
            }
            light.transform.position = position;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.gameObject.SetActive(true);
            flashes.Add(new Flash { light = light, life = duration, intensity = intensity });
        }

        public void ClearAll()
        {
            ClearMarks();
            for (int i = tracers.Count - 1; i >= 0; i--) ReleaseTracer(i);
            for (int i = debris.Count - 1; i >= 0; i--) ReleaseDebris(i);
            for (int i = flashes.Count - 1; i >= 0; i--) ReleaseFlash(i);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float now = Time.time;

            for (int i = tracers.Count - 1; i >= 0; i--)
                if (now >= tracers[i].until) ReleaseTracer(i);

            Vector3 gravity = Physics.gravity * dt;
            for (int i = debris.Count - 1; i >= 0; i--)
            {
                var d = debris[i];
                d.age += dt;
                if (d.age >= d.life)
                {
                    ReleaseDebris(i);
                    continue;
                }
                d.velocity += gravity;
                Vector3 p = d.transform.position + d.velocity * dt;
                if (p.y < 0.05f)
                {
                    p.y = 0.05f;
                    d.velocity = new Vector3(d.velocity.x * 0.4f, -d.velocity.y * 0.3f, d.velocity.z * 0.4f);
                }
                d.transform.position = p;
                d.transform.localScale = Vector3.one * d.size * (1f - d.age / d.life);
                debris[i] = d;
            }

            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.age += dt;
                if (f.age >= f.life)
                {
                    ReleaseFlash(i);
                    continue;
                }
                f.light.intensity = f.intensity * (1f - f.age / f.life);
                flashes[i] = f;
            }
        }

        void ReleaseTracer(int index)
        {
            var go = tracers[index].go;
            go.SetActive(false);
            tracerPool.Push(go);
            RemoveAt(tracers, index);
        }

        void ReleaseDebris(int index)
        {
            var piece = debris[index].transform;
            piece.gameObject.SetActive(false);
            debrisPool.Push(piece);
            RemoveAt(debris, index);
        }

        void ReleaseFlash(int index)
        {
            var light = flashes[index].light;
            light.gameObject.SetActive(false);
            lightPool.Push(light);
            RemoveAt(flashes, index);
        }

        // Order doesn't matter, so swap with the last item instead of shifting the list.
        static void RemoveAt<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }
    }
}
