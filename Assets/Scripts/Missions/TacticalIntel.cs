using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // What the team knows. A few times a second it checks what the player
    // and squadmates can actually see, advances room states (undiscovered ->
    // discovered -> investigated -> secured -> complete), remembers where
    // threats were last seen and which civilians have been found. The
    // tactical map only draws what is in here. With the "line of sight"
    // setting on, suspects and civilians nobody can see are hidden in the
    // 3D view too.
    public class TacticalIntel : MonoBehaviour
    {
        public class Contact
        {
            public bool everSeen, visible;
            public Vector3 lastSeen;
            public float lastSeenTime, revealedUntil;
        }

        const float Interval = 0.2f;
        const float SightRange = 28f;
        const float DarkSightRange = 5f;

        public static TacticalIntel Instance { get; private set; }

        public readonly List<Vector3> Markers = new List<Vector3>();
        public int ThreatsVisible { get; private set; }
        public int CiviliansFound { get { return discovered.Count; } }

        readonly Dictionary<EnemyAI, Contact> contacts = new Dictionary<EnemyAI, Contact>();
        readonly HashSet<CivilianAI> discovered = new HashSet<CivilianAI>();
        readonly HashSet<CivilianAI> civiliansVisible = new HashSet<CivilianAI>();
        readonly Dictionary<RoomController, float> occupancy = new Dictionary<RoomController, float>();
        readonly List<Vector3> eyes = new List<Vector3>();
        readonly List<Vector3> facings = new List<Vector3>();
        readonly List<float> fovs = new List<float>();
        float nextUpdate;

        void Awake()
        {
            Instance = this;
        }

        public void Begin()
        {
            contacts.Clear();
            discovered.Clear();
            civiliansVisible.Clear();
            occupancy.Clear();
            Markers.Clear();
            ThreatsVisible = 0;
            nextUpdate = 0f;
            // Nobody has been seen yet (this also keeps the deployment cutscene from revealing anyone).
            if (!SaveManager.Settings.lineOfSight) return;
            foreach (var enemy in AIManager.Instance.Enemies) enemy.SetSeen(false);
            foreach (var civilian in AIManager.Instance.Civilians) civilian.SetSeen(false);
        }

        public Contact ContactFor(EnemyAI enemy)
        {
            Contact contact;
            if (!contacts.TryGetValue(enemy, out contact))
            {
                contact = new Contact();
                contacts[enemy] = contact;
            }
            return contact;
        }

        public bool IsDiscovered(CivilianAI civilian) { return discovered.Contains(civilian); }

        public bool IsRevealed(EnemyAI enemy)
        {
            Contact contact;
            return contacts.TryGetValue(enemy, out contact) && (contact.visible || Time.time < contact.revealedUntil);
        }

        // Marks suspects and civilians within the radius on the map for a while,
        // even through walls. Returns how many suspects were found.
        public int RevealAround(Vector3 position, float radius, float seconds)
        {
            var ai = AIManager.Instance;
            float sqr = radius * radius;
            int found = 0;
            foreach (var enemy in ai.Enemies)
            {
                if (enemy.IsNeutralized || FlatSqr(enemy.Position - position) > sqr) continue;
                var contact = ContactFor(enemy);
                contact.everSeen = true;
                contact.lastSeen = enemy.Position;
                contact.lastSeenTime = Time.time;
                contact.revealedUntil = Mathf.Max(contact.revealedUntil, Time.time + seconds);
                found++;
            }
            foreach (var civilian in ai.Civilians)
                if (civilian.IsAlive && FlatSqr(civilian.Position - position) < sqr && discovered.Add(civilian))
                    MissionManager.Instance.OnCivilianEncountered(civilian);
            foreach (var room in GameManager.Instance.Level.rooms)
            {
                var b = room.Bounds;
                float dx = Mathf.Max(0f, Mathf.Max(b.min.x - position.x, position.x - b.max.x));
                float dz = Mathf.Max(0f, Mathf.Max(b.min.z - position.z, position.z - b.max.z));
                if (dx * dx + dz * dz < sqr) room.Advance(RoomState.Discovered);
            }
            return found;
        }

        static float FlatSqr(Vector3 v)
        {
            return v.x * v.x + v.z * v.z;
        }

        // Security footage: every suspect's position (for a while) and the evidence.
        public void RevealAll(float seconds, bool includeEvidence)
        {
            foreach (var enemy in AIManager.Instance.Enemies)
            {
                if (enemy.IsNeutralized) continue;
                var contact = ContactFor(enemy);
                contact.everSeen = true;
                contact.lastSeen = enemy.Position;
                contact.lastSeenTime = Time.time;
                contact.revealedUntil = Mathf.Max(contact.revealedUntil, Time.time + seconds);
            }
            var level = GameManager.Instance.Level;
            foreach (var room in level.rooms) room.Advance(RoomState.Discovered);
            if (includeEvidence) foreach (var item in level.evidence) item.Revealed = true;
        }

        public void ToggleMarker(Vector3 point)
        {
            for (int i = 0; i < Markers.Count; i++)
            {
                if ((Markers[i] - point).sqrMagnitude < 1.5f * 1.5f)
                {
                    Markers.RemoveAt(i);
                    return;
                }
            }
            if (Markers.Count >= 8) Markers.RemoveAt(0);
            Markers.Add(point);
        }

        void Update()
        {
            var game = GameManager.Instance;
            if (game == null || !game.IsPlaying || game.Level == null || game.Player == null) return;
            if (Time.time < nextUpdate) return;
            nextUpdate = Time.time + Interval;

            GatherObservers(game);
            UpdateEnemies(game);
            UpdateCivilians(game);
            UpdateEvidence(game);
            UpdateRooms(game);
        }

        void GatherObservers(GameManager game)
        {
            eyes.Clear();
            facings.Clear();
            fovs.Clear();
            var player = game.Player;
            if (player.IsAlive)
            {
                eyes.Add(player.Position + Vector3.up * 1.6f);
                facings.Add(player.AimDirection);
                fovs.Add(200f);
            }
            foreach (var officer in AIManager.Instance.Officers)
            {
                if (!officer.IsAlive) continue;
                eyes.Add(officer.Position + Vector3.up * 1.6f);
                facings.Add(officer.transform.forward);
                fovs.Add(240f);
            }
        }

        bool Observed(Vector3 target)
        {
            bool lit = AIVisibility.IsLit(target);
            for (int i = 0; i < eyes.Count; i++)
            {
                float range = lit ? SightRange : DarkSightRange;
                if (AIVisibility.CanSee(eyes[i], facings[i], fovs[i], range, target, 1f)) return true;
            }
            return false;
        }

        void UpdateEnemies(GameManager game)
        {
            bool fog = SaveManager.Settings.lineOfSight;
            int threats = 0;
            foreach (var enemy in AIManager.Instance.Enemies)
            {
                var contact = ContactFor(enemy);
                bool neutralized = enemy.IsNeutralized;
                contact.visible = !neutralized && Observed(enemy.Head - Vector3.up * 0.3f);
                if (contact.visible)
                {
                    if (!contact.everSeen) MissionManager.Instance.Stats.suspectsEncountered++;
                    contact.everSeen = true;
                    contact.lastSeen = enemy.Position;
                    contact.lastSeenTime = Time.time;
                    if (enemy.IsArmedThreat) threats++;
                }
                bool show = !fog || neutralized || contact.visible || Time.time < contact.revealedUntil;
                enemy.SetSeen(show);
            }
            ThreatsVisible = threats;
        }

        void UpdateCivilians(GameManager game)
        {
            bool fog = SaveManager.Settings.lineOfSight;
            foreach (var civilian in AIManager.Instance.Civilians)
            {
                bool visible = civilian.IsAlive && !civilian.IsEvacuated && Observed(civilian.Position + Vector3.up * 1.2f);
                if (visible && discovered.Add(civilian)) MissionManager.Instance.OnCivilianEncountered(civilian);
                if (visible) civiliansVisible.Add(civilian);
                else civiliansVisible.Remove(civilian);
                civilian.SetSeen(!fog || visible || discovered.Contains(civilian) && civilian.UnderPoliceControl);
            }
        }

        void UpdateEvidence(GameManager game)
        {
            var player = game.Player;
            foreach (var item in game.Level.evidence)
                if (!item.Revealed && (item.transform.position - player.Position).sqrMagnitude < 36f
                    && !Physics.Linecast(player.ChestPosition, item.transform.position + Vector3.up * 0.3f, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    item.Revealed = true;
        }

        void UpdateRooms(GameManager game)
        {
            var level = game.Level;
            var player = game.Player;
            foreach (var room in level.rooms)
            {
                bool occupied = player.IsAlive && room.Contains(player.Position);
                if (!occupied)
                    foreach (var officer in AIManager.Instance.Officers)
                        if (officer.IsAlive && room.Contains(officer.Position)) { occupied = true; break; }

                if (occupied)
                {
                    room.Advance(RoomState.Discovered);
                    float time;
                    occupancy.TryGetValue(room, out time);
                    time += Interval;
                    occupancy[room] = time;
                    if (time >= 1.5f) room.Advance(RoomState.Investigated);
                }
                if (!room.Indoor) continue;

                if (room.State >= RoomState.Investigated && room.State < RoomState.Secured && !AIManager.Instance.AnySuspectIn(room))
                    room.Advance(RoomState.Secured);
                if (room.State == RoomState.Secured && RoomComplete(room, level))
                    room.Advance(RoomState.Complete);
            }

            // Looking through an open doorway reveals the room beyond.
            foreach (var door in level.doors)
            {
                if (!door.IsPassable) continue;
                Vector3 doorPosition = door.transform.position;
                bool near = player.IsAlive && AIManager.FlatDistance(player.Position, doorPosition) < 3f;
                if (!near)
                    foreach (var officer in AIManager.Instance.Officers)
                        if (officer.IsAlive && AIManager.FlatDistance(officer.Position, doorPosition) < 3f) { near = true; break; }
                if (!near) continue;
                if (door.RoomFront != null) door.RoomFront.Advance(RoomState.Discovered);
                if (door.RoomBack != null) door.RoomBack.Advance(RoomState.Discovered);
            }
        }

        static bool RoomComplete(RoomController room, LevelLayout level)
        {
            foreach (var civilian in AIManager.Instance.Civilians)
                if (civilian.NeedsHelp && civilian.State != CivilianState.Waiting && room.Contains(civilian.Position)) return false;
            foreach (var item in level.evidence)
                if (!item.Secured && room.Contains(item.transform.position)) return false;
            return true;
        }
    }
}
