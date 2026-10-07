using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Swat
{
    // The SWAT 4-style objective modes, played in rounds:
    //   VIP Escort: one SWAT player each round is the VIP (sidearm only, no respawn). SWAT wins the
    //   round by getting them to the extraction point; the suspects by tagging them out or running
    //   out the clock.
    //   Rapid Deployment: three devices somewhere in the building. SWAT wins the round by disarming
    //   them all (hold E, four seconds each); the suspects by holding them until the clock runs out.
    // Plus arrests: an opponent dazed by a flashbang, a shove or a less-lethal round can be
    // restrained (hold E), which counts double in Team Deathmatch.
    public partial class VersusMatch
    {
        public const float ObjectiveRoundLength = 180f;
        public const float DisarmTime = 4f;
        const int RoundReasonTime = 1, RoundReasonVipOut = 2, RoundReasonVipSafe = 3, RoundReasonDisarmed = 4;

        // ---- VIP Escort ----

        public int VipId { get; private set; }
        public Vector3 VipExit { get; private set; }
        public ICombatTarget Vip { get { return VipId >= 0 ? TargetById(VipId) : null; } }
        bool vipDown;
        Transform vipMarker;
        ArenaBot vipBot;
        WeaponData vipBotGun;

        // The extraction point is the room furthest from SWAT's base; the suspects start about halfway.
        void PickVipExit()
        {
            VipId = -1;
            VipExit = Bases[1];
            var path = new NavMeshPath();
            float far = Mathf.Max(1f, PathLengthTo(Bases[0], VipExit, path));
            Vector3 best = Bases[1];
            float bestScore = float.MaxValue;
            foreach (var room in level.rooms)
            {
                if (!room.Indoor) continue;
                var size = room.Bounds.size;
                if (size.x < 3.5f || size.z < 3.5f) continue;
                Vector3 center = OnNavMesh(room.Bounds.center);
                if (AIManager.FlatDistance(center, VipExit) < 10f || AIManager.FlatDistance(center, Bases[0]) < 10f) continue;
                float length = PathLengthTo(Bases[0], center, path);
                if (length < 0f) continue;
                float score = Mathf.Abs(length / far - 0.55f);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = center;
                }
            }
            Bases[1] = best;
            spawns[1].Clear();
            for (int i = 0; i < 8; i++) spawns[1].Add(OnNavMesh(best + Random.insideUnitSphere * 3f));
            Vector3 face = Bases[0] - Bases[1];
            baseYaw[1] = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            BasePad(1, Bases[1]);
            SetVipExit(VipExit);
        }

        static float PathLengthTo(Vector3 from, Vector3 to, NavMeshPath path)
        {
            if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1f;
            return PathLength(path);
        }

        // The extraction point everyone sees (online, the host sends where it is).
        void SetVipExit(Vector3 point)
        {
            VipId = -1;
            VipExit = point;
            var gold = new Color(1f, 0.8f, 0.2f);
            var pad = Shapes.Make(PrimitiveType.Cylinder, "VIP Extraction", level.root, point + Vector3.up * 0.03f, new Vector3(3.2f, 0.01f, 3.2f), gold, false, 1.4f);
            spawned.Add(pad);
            var beacon = Shapes.Box("Extraction Beacon", level.root, point + Vector3.up * 1.4f, new Vector3(0.12f, 2.8f, 0.12f), gold, false, 1.8f);
            spawned.Add(beacon);
            vipMarker = Shapes.Box("VIP Marker", level.root, point + Vector3.up * 2.3f, new Vector3(0.28f, 0.28f, 0.28f), gold, false, 2.5f).transform;
            vipMarker.rotation = Quaternion.Euler(45f, 0f, 45f);
            vipMarker.gameObject.SetActive(false);
            spawned.Add(vipMarker.gameObject);
        }

        // Host or offline: the next SWAT player in turn is the VIP this round.
        void ChooseVip()
        {
            var team = members[0];
            if (team.Count == 0) return;
            var pick = team[(Round - 1) % team.Count];
            Post(new MatchEvent { kind = MatchEventKind.VipChosen, a = IdOf(pick), b = -1, side = 0, weapon = -1 });
        }

        void ApplyVip(int id)
        {
            // Last round's VIP gets their own guns back.
            if (VipId == MyId && player != null && id != MyId) player.Weapons.RestoreLoadout();
            if (vipBot != null && vipBotGun != null) vipBot.SetGun(vipBotGun);
            vipBot = null;
            vipBotGun = null;
            VipId = id;
            vipDown = false;
            string name = Name(id);
            if (id == MyId && player != null)
            {
                var sidearm = GameData.Weapon(player.Loadout != null ? player.Loadout.sidearmId : null) ?? GameData.Weapon("pistol_p17");
                player.Weapons.SetOnlyWeapon(sidearm);
                UIManager.Banner("YOU ARE THE VIP", "Sidearm only. Reach the gold extraction point; your team covers you", BannerKind.Good);
            }
            else
            {
                var bot = Find(id) as ArenaBot;
                if (bot != null && !Mirror)
                {
                    vipBot = bot;
                    vipBotGun = bot.Gun != null ? bot.Gun.Data : null;
                    bot.SetGun(GameData.Weapon("pistol_p17"));
                }
                if (MySide == 0) UIManager.Banner("ESCORT THE VIP", name + " is the VIP: get them to the gold extraction point", BannerKind.Info);
                else UIManager.Banner("STOP THE VIP", name + " is the VIP: tag them out before they reach the extraction point", BannerKind.Info);
            }
            Announce(name + " is the VIP", 0);
        }

        // Who's out until the next round: everyone in Elimination, the VIP in VIP Escort.
        bool OutForRound(int id)
        {
            return Mode == GameMode.Elimination || (Mode == GameMode.VipEscort && id == VipId && id >= 0);
        }

        bool IsIn(ICombatTarget target)
        {
            if (target == null) return false;
            var remote = target as NetActor;
            return remote != null ? !remote.Down : target.IsAlive;
        }

        int VipRoundOutcome(out int reason)
        {
            reason = 0;
            var vip = Vip;
            if (vip == null) return -2;
            if (vipDown || !IsIn(vip)) { reason = RoundReasonVipOut; return 1; }
            if (AIManager.FlatDistance(vip.Position, VipExit) < 2.2f) { reason = RoundReasonVipSafe; return 0; }
            if (RoundTimeLeft <= 0f) { reason = RoundReasonTime; return 1; }
            return -2;
        }

        bool VipObjective(ArenaBot bot, out Vector3 point, out bool run)
        {
            run = true;
            point = bot.Position;
            var vip = Vip;
            if (bot.NetId == VipId)
            {
                point = VipExit;
                return true;
            }
            if (bot.Side == 0)
            {
                // Escorts stay around the VIP, between them and trouble.
                if (vip == null || !IsIn(vip)) { point = VipExit; return true; }
                float angle = (bot.NetId * 97f) % 360f;
                point = OnNavMesh(vip.Position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.5f);
                run = AIManager.FlatDistance(bot.Position, vip.Position) > 5f;
                return true;
            }
            // Suspects: hunters go for the VIP, the rest hold the extraction point.
            if (bot.Role == BotRole.Attack && vip != null && IsIn(vip))
            {
                point = vip.Position;
                return true;
            }
            if (Time.time > bot.RoamUntil)
            {
                Vector2 offset = Random.insideUnitCircle * 7f;
                bot.RoamPoint = OnNavMesh(VipExit + new Vector3(offset.x, 0f, offset.y));
                bot.RoamUntil = Time.time + Random.Range(4f, 8f);
            }
            point = bot.RoamPoint;
            run = false;
            return true;
        }

        // ---- Rapid Deployment ----

        public class Bomb
        {
            public Vector3 position;
            public bool disarmed;
            public BombDevice device;
        }

        public readonly List<Bomb> Bombs = new List<Bomb>();
        readonly Dictionary<ArenaBot, float> botDisarm = new Dictionary<ArenaBot, float>();

        public int BombsLeft
        {
            get
            {
                int count = 0;
                foreach (var bomb in Bombs) if (!bomb.disarmed) count++;
                return count;
            }
        }

        // Three devices in rooms well away from SWAT's base and from each other.
        void PlaceBombs()
        {
            var path = new NavMeshPath();
            var rooms = new List<KeyValuePair<float, Vector3>>();
            foreach (var room in level.rooms)
            {
                if (!room.Indoor) continue;
                var size = room.Bounds.size;
                if (size.x < 3f || size.z < 3f) continue;
                Vector3 center = OnNavMesh(room.Bounds.center + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
                float length = PathLengthTo(Bases[0], center, path);
                if (length > 8f) rooms.Add(new KeyValuePair<float, Vector3>(length, center));
            }
            rooms.Sort((a, b) => b.Key.CompareTo(a.Key));
            var chosen = new List<Vector3>();
            foreach (var room in rooms)
            {
                if (chosen.Count >= 3) break;
                bool clear = true;
                foreach (var other in chosen) if (AIManager.FlatDistance(other, room.Value) < 9f) clear = false;
                if (clear) chosen.Add(room.Value);
            }
            for (int i = 0; chosen.Count < 3 && i < rooms.Count; i++)
                if (!chosen.Contains(rooms[i].Value)) chosen.Add(rooms[i].Value);
            SetBombs(chosen);
        }

        void SetBombs(IList<Vector3> points)
        {
            Bombs.Clear();
            botDisarm.Clear();
            if (points == null) return;
            for (int i = 0; i < points.Count; i++)
            {
                var bomb = new Bomb { position = points[i] };
                bomb.device = BombDevice.Create(level.root, this, i, points[i]);
                spawned.Add(bomb.device.gameObject);
                Bombs.Add(bomb);
            }
        }

        void ResetBombs()
        {
            botDisarm.Clear();
            foreach (var bomb in Bombs)
            {
                bomb.disarmed = false;
                if (bomb.device != null) bomb.device.SetDisarmed(false);
            }
        }

        // From the device itself (E held long enough).
        public void RequestDisarm(int index)
        {
            if (Mirror) NetSession.Instance.SendDisarm(index);
            else Disarm(index, MyId);
        }

        // Host or offline.
        public void Disarm(int index, int byId)
        {
            if (Mode != GameMode.RapidDeployment || RoundOver || index < 0 || index >= Bombs.Count || Bombs[index].disarmed) return;
            Post(new MatchEvent { kind = MatchEventKind.BombDisarmed, a = byId, b = index, side = 0, weapon = -1 });
        }

        void ApplyDisarm(int index, int byId)
        {
            if (index < 0 || index >= Bombs.Count) return;
            Bombs[index].disarmed = true;
            if (Bombs[index].device != null) Bombs[index].device.SetDisarmed(true);
            int left = BombsLeft;
            Announce(Name(byId) + " disarmed a device (" + left + " left)", 0);
            AudioManager.Play2D(MySide == 0 ? Sound.ObjectiveTone : Sound.Warning, 0.55f, 1f, SoundCategory.Interface);
            if (left > 0) UIManager.Banner("DEVICE DISARMED", left + (left == 1 ? " device" : " devices") + " left", MySide == 0 ? BannerKind.Good : BannerKind.Bad);
        }

        int BombRoundOutcome(out int reason)
        {
            reason = 0;
            if (Bombs.Count > 0 && BombsLeft == 0) { reason = RoundReasonDisarmed; return 0; }
            if (RoundTimeLeft <= 0f) { reason = RoundReasonTime; return 1; }
            return -2;
        }

        // SWAT bots next to a live device, with nobody to fight, work on it.
        void UpdateBotDisarming(float dt)
        {
            if (RoundOver) return;
            foreach (var bot in Bots)
            {
                if (bot.Side != 0 || !bot.IsAlive || bot.Engaged) { botDisarm.Remove(bot); continue; }
                int index = NearestLiveBomb(bot.Position, 1.6f);
                if (index < 0) { botDisarm.Remove(bot); continue; }
                float time;
                botDisarm.TryGetValue(bot, out time);
                time += dt;
                if (time >= DisarmTime)
                {
                    botDisarm.Remove(bot);
                    Disarm(index, bot.NetId);
                }
                else botDisarm[bot] = time;
            }
        }

        int NearestLiveBomb(Vector3 from, float within)
        {
            int best = -1;
            float bestDistance = within;
            for (int i = 0; i < Bombs.Count; i++)
            {
                if (Bombs[i].disarmed) continue;
                float d = AIManager.FlatDistance(from, Bombs[i].position);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        bool BombObjective(ArenaBot bot, out Vector3 point, out bool run)
        {
            run = true;
            point = bot.Position;
            int index = NearestLiveBomb(bot.Position, float.MaxValue);
            if (index < 0) return false;
            if (bot.Side == 0)
            {
                point = Bombs[index].position;
                run = AIManager.FlatDistance(bot.Position, point) > 3f;
                return true;
            }
            // Suspects guard the devices, spread between them.
            if (Time.time > bot.RoamUntil)
            {
                var live = new List<int>();
                for (int i = 0; i < Bombs.Count; i++) if (!Bombs[i].disarmed) live.Add(i);
                int pick = live[(bot.NetId + Mathf.FloorToInt(Time.time / 20f)) % live.Count];
                Vector2 offset = Random.insideUnitCircle * 5f;
                bot.RoamPoint = OnNavMesh(Bombs[pick].position + new Vector3(offset.x, 0f, offset.y));
                bot.RoamUntil = Time.time + Random.Range(5f, 9f);
            }
            point = bot.RoamPoint;
            run = false;
            return true;
        }

        // Online, not hosting: the VIP and the devices as the host has them (catches up a client that
        // missed the events while loading).
        public void ApplyObjectives(int vip, int disarmed)
        {
            if (Mode == GameMode.VipEscort && vip != VipId && vip >= 0) ApplyVip(vip);
            if (Mode != GameMode.RapidDeployment) return;
            for (int i = 0; i < Bombs.Count && i < 8; i++)
            {
                bool done = (disarmed & (1 << i)) != 0;
                if (Bombs[i].disarmed == done) continue;
                Bombs[i].disarmed = done;
                if (Bombs[i].device != null) Bombs[i].device.SetDisarmed(done);
            }
        }

        // ---- Both, and arrests ----

        void UpdateObjectiveVisuals()
        {
            if (vipMarker != null)
            {
                var vip = Vip;
                bool show = vip != null && IsIn(vip);
                if (vipMarker.gameObject.activeSelf != show) vipMarker.gameObject.SetActive(show);
                if (show)
                {
                    vipMarker.position = vip.Position + Vector3.up * (2.25f + Mathf.Sin(Time.time * 3f) * 0.06f);
                    vipMarker.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                }
            }
        }

        static string RoundReasonText(int reason)
        {
            switch (reason)
            {
                case RoundReasonTime: return " (time ran out)";
                case RoundReasonVipOut: return " (the VIP was tagged out)";
                case RoundReasonVipSafe: return " (the VIP got out)";
                case RoundReasonDisarmed: return " (every device disarmed)";
                default: return "";
            }
        }

        static string RoundReasonBanner(int reason)
        {
            switch (reason)
            {
                case RoundReasonTime: return ": TIME";
                case RoundReasonVipOut: return ": VIP DOWN";
                case RoundReasonVipSafe: return ": VIP EXTRACTED";
                case RoundReasonDisarmed: return ": DEVICES DISARMED";
                default: return "";
            }
        }

        // You restrained a dazed opponent (offline or hosting; bots only).
        public void ArrestBot(ArenaBot bot)
        {
            if (!running || Mirror || bot == null || !bot.IsAlive || bot.Side == MySide) return;
            bot.Arrested();
            bot.RespawnAt = OutForRound(bot.NetId) ? float.MaxValue : Time.time + RespawnDelay;
            PlayerKills++;
            if (Mode == GameMode.TeamDeathmatch) Score[MySide] += 2f;
            if (Mode == GameMode.VipEscort && bot.NetId == VipId) vipDown = true;
            Post(new MatchEvent { kind = MatchEventKind.Arrest, a = MyId, b = bot.NetId, side = bot.Side, weapon = -1 });
        }
    }
}
