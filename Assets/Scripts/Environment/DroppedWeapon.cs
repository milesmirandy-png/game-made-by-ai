using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // A suspect's gun on the floor, dropped when they surrender or go down. It stays there until an
    // officer secures it (hold E, or a squadmate nearby does it): left behind, it costs points at the
    // end, and a suspect who only pretended to give up can grab it again.
    public class DroppedWeapon : MonoBehaviour, IInteractable
    {
        public static readonly List<DroppedWeapon> All = new List<DroppedWeapon>();

        public bool Secured { get; private set; }
        public EnemyAI Owner { get; private set; }
        public string Prompt { get { return "[E] Secure Weapon (hold)"; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 0.3f; } }
        GameObject marker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            All.Clear();
        }

        // On the floor a little in front of where they stood, lying on its side.
        public static DroppedWeapon Drop(EnemyAI owner, WeaponData weapon, string modelOverride)
        {
            if (owner == null || weapon == null) return null;
            Vector3 spot = owner.Position + owner.transform.forward * 0.45f + owner.transform.right * Random.Range(-0.2f, 0.2f);
            RaycastHit hit;
            float floor = owner.Position.y;
            if (Physics.Raycast(spot + Vector3.up * 1f, Vector3.down, out hit, 2.5f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) floor = hit.point.y;
            // Don't drop it through a wall.
            if (Physics.Linecast(owner.Position + Vector3.up * 0.3f, spot + Vector3.up * 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) spot = owner.Position;
            var go = new GameObject("Dropped " + weapon.displayName);
            go.transform.SetParent(owner.transform.parent, false);
            go.transform.position = new Vector3(spot.x, floor + 0.04f, spot.z);
            go.transform.rotation = Quaternion.Euler(0f, owner.transform.eulerAngles.y + Random.Range(-70f, 70f), 0f);
            var gun = new GameObject("Gun").transform;
            gun.SetParent(go.transform, false);
            gun.localRotation = Quaternion.Euler(0f, 0f, 90f);
            WeaponModels.Build(weapon, gun, null, modelOverride);
            foreach (var collider in go.GetComponentsInChildren<Collider>()) Destroy(collider);
            Shapes.Toonify(go.transform);
            var dropped = go.AddComponent<DroppedWeapon>();
            dropped.Owner = owner;
            // A small marker so it's easy to find from above (gone once secured).
            dropped.marker = Shapes.Box("Marker", go.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.12f, 0.12f, 0.12f), new Color(1f, 0.45f, 0.15f), false, 2.5f);
            dropped.marker.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            All.Add(dropped);
            MissionManager.Instance.OnWeaponDropped();
            return dropped;
        }

        public static DroppedWeapon ForOwner(EnemyAI owner)
        {
            foreach (var weapon in All) if (weapon != null && weapon.Owner == owner && !weapon.Secured) return weapon;
            return null;
        }

        public float InteractDuration(PlayerController player) { return 0.8f; }
        public bool CanInteract(PlayerController player) { return !Secured; }

        public void Interact(PlayerController player)
        {
            Secure(null);
        }

        // officer: the squadmate who picked it up (null for the player).
        public void Secure(SquadAI officer)
        {
            if (Secured) return;
            Secured = true;
            AudioManager.Play(Sound.Equip, transform.position, 0.6f, 0.9f);
            MissionManager.Instance.OnWeaponSecured();
            if (officer != null) SquadCommandManager.Instance.Radio(officer, "Weapon secured.", false);
            else UIManager.Notify("Weapon secured");
            Destroy(gameObject);
        }

        // A suspect takes it back.
        public void Take()
        {
            All.Remove(this);
            MissionManager.Instance.OnWeaponTaken();
            Destroy(gameObject);
        }

        // Squadmates pick up loose weapons near them when nothing else is going on.
        public static DroppedWeapon Nearest(Vector3 from, float range)
        {
            DroppedWeapon best = null;
            float bestDistance = range * range;
            foreach (var weapon in All)
            {
                if (weapon == null || weapon.Secured) continue;
                float d = (weapon.transform.position - from).sqrMagnitude;
                if (d >= bestDistance) continue;
                if (Physics.Linecast(from + Vector3.up, weapon.transform.position + Vector3.up * 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                best = weapon;
                bestDistance = d;
            }
            return best;
        }

        public static int Unsecured
        {
            get
            {
                int count = 0;
                foreach (var weapon in All) if (weapon != null && !weapon.Secured) count++;
                return count;
            }
        }

        void OnDestroy()
        {
            All.Remove(this);
        }
    }
}
