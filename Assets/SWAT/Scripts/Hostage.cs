using UnityEngine;

namespace Swat
{
    // A kneeling hostage with their hands up. Walk up and press E to rescue them.
    // Never shoot them.
    public class Hostage : MonoBehaviour, IShootable, IInteractable
    {
        public bool IsSecured { get; private set; }
        public bool IsDead { get; private set; }

        public bool CanInteract { get { return !IsSecured && !IsDead; } }
        public string Prompt { get { return "[E] Rescue hostage"; } }

        static readonly Color[] Shirts =
        {
            new Color(0.92f, 0.92f, 0.9f), new Color(0.55f, 0.75f, 0.95f), new Color(0.95f, 0.85f, 0.4f),
            new Color(0.95f, 0.6f, 0.7f), new Color(0.85f, 0.75f, 0.6f),
        };
        static readonly Color[] Pants = { new Color(0.2f, 0.27f, 0.45f), new Color(0.6f, 0.52f, 0.38f), new Color(0.3f, 0.3f, 0.32f) };
        static readonly Color[] Hair = { new Color(0.1f, 0.07f, 0.05f), new Color(0.4f, 0.25f, 0.12f), new Color(0.85f, 0.7f, 0.4f), new Color(0.6f, 0.6f, 0.6f) };

        HumanParts parts;
        float fear;
        float fallProgress = -1f;
        Quaternion fallStart;

        public static Hostage Spawn(Transform parent, Vector3 position, float yaw)
        {
            var go = new GameObject("Hostage");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var hostage = go.AddComponent<Hostage>();
            Color skin = Humanoid.RandomSkin();
            hostage.parts = Humanoid.Build(go.transform, hostage, Shirts[Random.Range(0, Shirts.Length)], Pants[Random.Range(0, Pants.Length)], skin, skin, true);
            Shapes.Box("Hair", hostage.parts.head, new Vector3(0f, 0.3f, -0.1f), new Vector3(1.05f, 0.5f, 0.9f), Hair[Random.Range(0, Hair.Length)], false);
            return hostage;
        }

        public void Scare()
        {
            fear = 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsDead)
            {
                if (fallProgress < 1f)
                {
                    fallProgress = Mathf.Min(1f, fallProgress + dt * 2.5f);
                    transform.rotation = Quaternion.Slerp(fallStart, fallStart * Quaternion.Euler(0f, 0f, 90f), fallProgress * fallProgress);
                }
                return;
            }

            fear = Mathf.MoveTowards(fear, 0f, dt * 0.4f);
            Quaternion left, right;
            if (IsSecured)
            {
                left = Quaternion.Euler(25f, 0f, 15f);
                right = Quaternion.Euler(25f, 0f, -15f);
            }
            else
            {
                float tremble = Mathf.Sin(Time.time * 25f) * 8f * fear;
                left = Quaternion.Euler(170f + tremble, 0f, -25f);
                right = Quaternion.Euler(170f - tremble, 0f, 25f);
            }
            float blend = 1f - Mathf.Exp(-8f * dt);
            parts.leftArm.localRotation = Quaternion.Slerp(parts.leftArm.localRotation, left, blend);
            parts.rightArm.localRotation = Quaternion.Slerp(parts.rightArm.localRotation, right, blend);
        }

        public void Interact(PlayerController player)
        {
            if (!CanInteract) return;
            IsSecured = true;
            player.Play(Sfx.Click, 0.8f);
            GameManager.Instance.OnHostageSecured(this);
        }

        public void TakeHit(float damage, Vector3 point, Vector3 direction, bool fromPlayer)
        {
            if (!fromPlayer || IsDead) return;
            IsDead = true;
            foreach (var hitbox in parts.hitboxes)
            {
                var col = hitbox.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            fallProgress = 0f;
            fallStart = transform.rotation;
            GameManager.Instance.OnHostageKilled(this);
        }
    }
}
