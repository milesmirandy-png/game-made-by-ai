using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A thrown flashbang or smoke grenade. It flies along a simple arc (no
    // physics engine), stops if it hits a wall, then goes off when its fuse
    // runs out. Grenade objects are pooled.
    public class ThrownGrenade : MonoBehaviour
    {
        static readonly Stack<ThrownGrenade> pool = new Stack<ThrownGrenade>();
        static readonly List<ThrownGrenade> active = new List<ThrownGrenade>();

        EquipmentData data;
        Vector3 start, target;
        float age, flightTime;
        bool landed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool.Clear();
            active.Clear();
        }

        public static void Throw(EquipmentData data, Vector3 from, Vector3 to)
        {
            ThrownGrenade grenade = pool.Count > 0 ? pool.Pop() : null;
            if (grenade == null)
            {
                var go = Shapes.Make(PrimitiveType.Cylinder, "Grenade", GameManager.Instance.PoolRoot, Vector3.zero, new Vector3(0.14f, 0.09f, 0.14f), Color.gray, false);
                grenade = go.AddComponent<ThrownGrenade>();
            }
            var color = data.kind == EquipmentKind.Smoke ? new Color(0.6f, 0.6f, 0.62f) : new Color(0.2f, 0.3f, 0.2f);
            grenade.GetComponent<Renderer>().sharedMaterial = Shapes.Mat(color);
            grenade.data = data;
            grenade.start = from;
            grenade.target = to;
            grenade.age = 0f;
            grenade.landed = false;
            grenade.flightTime = 0.25f + Vector3.Distance(from, to) / 14f;
            grenade.transform.position = from;
            grenade.gameObject.SetActive(true);
            active.Add(grenade);
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

            if (!landed)
            {
                float t = Mathf.Clamp01(age / flightTime);
                Vector3 next = Vector3.Lerp(start, target, t) + Vector3.up * (2.2f * 4f * t * (1f - t));
                RaycastHit hit;
                if (Physics.Linecast(transform.position, next, out hit, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                {
                    next = hit.point + hit.normal * 0.15f;
                    landed = true;
                }
                else if (t >= 1f)
                {
                    landed = true;
                }
                transform.position = next;
                transform.Rotate(720f * dt, 0f, 0f);
            }
            else
            {
                Vector3 p = transform.position;
                p.y = Mathf.MoveTowards(p.y, 0.1f, 8f * dt);
                transform.position = p;
            }

            if (age >= data.fuseTime) Detonate();
        }

        void Detonate()
        {
            Vector3 position = transform.position;
            position.y = 0.1f;
            if (data.kind == EquipmentKind.Smoke) SmokeCloud.Spawn(position, data.radius, data.effectDuration);
            else Equipment.Flashbang(position, data);
            Release();
        }

        void Release()
        {
            gameObject.SetActive(false);
            active.Remove(this);
            pool.Push(this);
        }
    }
}
