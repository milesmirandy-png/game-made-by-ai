using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    // The tactical map, drawn with flat UI shapes from the level layout (no
    // second camera). Shows walls, doors, discovered rooms by state, the
    // team, known and last-known threats, discovered civilians, evidence,
    // objective rooms, zones and player markers. Only what TacticalIntel
    // knows is drawn.
    //
    // Overlay (Tab): read-only, the game keeps running.
    // Planning mode (Space): time paused or slowed; left-click an officer to
    // select, left-click elsewhere to place/remove a marker, right-click to
    // send the selected officers there, Shift+right-click to add a waypoint.
    public class TacticalMapUI
    {
        int viewArea = -1;
        Rect mapRect;
        Bounds view;
        float scale;
        Vector2 offset;
        // Minimap mode: same drawing code, clipped to a small player-centred window.
        bool mini;
        Rect clip;
        readonly Dictionary<string, Objective> objectiveRooms = new Dictionary<string, Objective>();
        const float MinimapRadius = 20f; // metres shown either side of the player

        // A small always-on map in the corner. Shows only what the team knows
        // (same rules as the tactical map), centred on the player, north up.
        public void DrawMinimap(GameManager game, Rect rect, float opacity)
        {
            var level = game.Level;
            var player = game.Player;
            if (level == null || player == null || Event.current.type != EventType.Repaint) return;
            float oldAlpha = UITheme.Alpha;
            UITheme.Alpha = Mathf.Clamp(opacity, 0.2f, 1f);
            UITheme.Fill(rect, new Color(0.01f, 0.02f, 0.04f, 0.82f));
            mini = true;
            clip = rect;
            mapRect = rect;
            viewArea = level.AreaAt(player.Position);
            scale = rect.width / (MinimapRadius * 2f);
            float depth = rect.height / scale;
            view = new Bounds(new Vector3(player.Position.x, 0f, player.Position.z), new Vector3(MinimapRadius * 2f, 1f, depth));
            offset = new Vector2(rect.x, rect.y);
            DrawRooms(level);
            DrawWalls(level);
            DrawDoors(level);
            DrawZones(level);
            DrawObjects(level);
            DrawPeople(game);
            DrawMarkers();
            mini = false;
            UITheme.Frame(rect, new Color(UITheme.Line.r, UITheme.Line.g, UITheme.Line.b, 0.9f));
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width, 2f), UITheme.Accent);
            if (level.areas.Count > 1 && viewArea >= 0 && viewArea < level.areas.Count)
                UITheme.ShadowText(new Rect(rect.x + 6f, rect.yMax - 20f, rect.width - 12f, 18f), level.areas[viewArea].name, 12, UITheme.Dim, TextAnchor.LowerLeft);
            UITheme.Alpha = oldAlpha;
        }

        void Box(Rect r, Color color)
        {
            if (mini)
            {
                r = Rect.MinMaxRect(Mathf.Max(r.xMin, clip.xMin), Mathf.Max(r.yMin, clip.yMin), Mathf.Min(r.xMax, clip.xMax), Mathf.Min(r.yMax, clip.yMax));
                if (r.width <= 0f || r.height <= 0f) return;
            }
            UITheme.Fill(r, color);
        }

        void BoxFrame(Rect r, Color color, float thickness = 1f)
        {
            Box(new Rect(r.x, r.y, r.width, thickness), color);
            Box(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Box(new Rect(r.x, r.y, thickness, r.height), color);
            Box(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        bool Inside(Vector2 p, float margin)
        {
            return !mini || (p.x > clip.xMin + margin && p.x < clip.xMax - margin && p.y > clip.yMin + margin && p.y < clip.yMax - margin);
        }

        void Label(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false)
        {
            if (mini && (!Inside(new Vector2(r.xMin, r.yMin), -1f) || !Inside(new Vector2(r.xMax, r.yMax), -1f))) return;
            UITheme.Text(r, text, size, color, anchor, bold);
        }

        void Dot(Vector2 p, float radius, Color color)
        {
            if (Inside(p, radius * 0.5f)) UITheme.Dot(p, radius, color);
        }

        void RingAt(Vector2 p, float radius, Color color, float thickness)
        {
            if (Inside(p, radius * 0.5f)) UITheme.Ring(p, radius, color, thickness);
        }

        void Line(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if (Inside(a, 0f) && Inside(b, 0f)) UITheme.LineTo(a, b, color, thickness);
        }

        public void Draw(GameManager game, bool planning)
        {
            var level = game.Level;
            var player = game.Player;
            if (level == null || player == null) return;
            float w = UITheme.Width, h = UITheme.Height;

            if (planning)
            {
                UITheme.Fill(new Rect(0f, 0f, w, h), new Color(0.01f, 0.02f, 0.04f, 0.9f));
                mapRect = new Rect(40f, 90f, w - 520f, h - 150f);
                UITheme.Header(new Rect(40f, 24f, 900f, 60f), "Planning Mode", SaveManager.Settings.planningPauses ? "Game paused" : "Game slowed");
            }
            else
            {
                // Between the objectives panel and the squad panel, so both stay readable.
                mapRect = new Rect(500f, 110f, Mathf.Max(400f, w - 860f), h - 330f);
                UITheme.Fill(mapRect, new Color(0.01f, 0.02f, 0.04f, 0.82f));
            }
            UITheme.Frame(mapRect, UITheme.Line);

            int playerArea = level.AreaAt(player.Position);
            if (viewArea < 0 || viewArea >= level.areas.Count || !planning) viewArea = playerArea;
            if (level.areas.Count > 1)
            {
                float tx = mapRect.x;
                for (int i = 0; i < level.areas.Count; i++)
                {
                    string label = level.areas[i].name + (i == playerArea ? " (you)" : "");
                    if (planning) { if (UITheme.Button(new Rect(tx, mapRect.y - 38f, 200f, 32f), label, true, i == viewArea, 15)) viewArea = i; }
                    else if (i == viewArea) UITheme.Text(new Rect(mapRect.x + 10f, mapRect.y + 6f, 300f, 22f), label, 15, UITheme.Accent, TextAnchor.UpperLeft, true);
                    tx += 208f;
                }
            }

            ComputeView(level);
            DrawRooms(level);
            DrawWalls(level);
            DrawDoors(level);
            DrawZones(level);
            DrawObjects(level);
            DrawPeople(game);
            DrawMarkers();
            DrawLegend(new Rect(mapRect.x, mapRect.yMax + 8f, mapRect.width, 22f));
            if (planning)
            {
                HandleInput(game);
                DrawPlanningPanel(game, new Rect(w - 460f, 90f, 420f, h - 150f));
            }
        }

        void ComputeView(LevelLayout level)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var room in level.rooms)
            {
                if (room.Area != viewArea) continue;
                if (!any) { b = room.Bounds; any = true; }
                else b.Encapsulate(room.Bounds);
            }
            if (!any) b = level.areas[viewArea].bounds;
            b.Expand(new Vector3(2f, 0f, 2f));
            view = b;
            float pad = 20f;
            scale = Mathf.Min((mapRect.width - pad * 2f) / b.size.x, (mapRect.height - pad * 2f) / b.size.z);
            offset = new Vector2(mapRect.x + (mapRect.width - b.size.x * scale) * 0.5f, mapRect.y + (mapRect.height - b.size.z * scale) * 0.5f);
        }

        Vector2 ToMap(Vector3 world)
        {
            return new Vector2(offset.x + (world.x - view.min.x) * scale, offset.y + (view.max.z - world.z) * scale);
        }

        Vector3 ToWorld(Vector2 map)
        {
            return new Vector3(view.min.x + (map.x - offset.x) / scale, 0f, view.max.z - (map.y - offset.y) / scale);
        }

        Rect ToMap(Bounds bounds)
        {
            Vector2 a = ToMap(new Vector3(bounds.min.x, 0f, bounds.max.z));
            return new Rect(a.x, a.y, bounds.size.x * scale, bounds.size.z * scale);
        }

        bool InView(Vector3 world, LevelLayout level)
        {
            return level.AreaAt(world) == viewArea;
        }

        void DrawRooms(LevelLayout level)
        {
            objectiveRooms.Clear();
            foreach (var objective in MissionManager.Instance.Objectives)
                if ((objective.Type == ObjectiveType.SecureRoom || objective.Type == ObjectiveType.InvestigateRoom || objective.Type == ObjectiveType.TrainingFlashbang)
                    && !string.IsNullOrEmpty(objective.TargetId) && objective.State != ObjectiveState.Pending) objectiveRooms[objective.TargetId] = objective;

            foreach (var room in level.rooms)
            {
                if (room.Area != viewArea) continue;
                var r = ToMap(room.Bounds);
                if (!room.Indoor) { Box(r, new Color(0.08f, 0.1f, 0.12f, 0.5f)); continue; }
                Color fill;
                switch (room.State)
                {
                    case RoomState.Undiscovered: fill = new Color(0.05f, 0.06f, 0.08f, 0.9f); break;
                    case RoomState.Discovered: fill = new Color(0.13f, 0.17f, 0.24f, 0.95f); break;
                    case RoomState.Investigated: fill = new Color(0.14f, 0.24f, 0.4f, 0.95f); break;
                    case RoomState.Secured: fill = new Color(0.12f, 0.32f, 0.26f, 0.95f); break;
                    default: fill = new Color(0.15f, 0.4f, 0.28f, 0.95f); break;
                }
                Box(r, fill);
                if (room.IsDark && room.State != RoomState.Undiscovered) Box(r, new Color(0f, 0f, 0f, 0.3f));
                Objective objective;
                if (objectiveRooms.TryGetValue(room.Id, out objective))
                {
                    var color = objective.State == ObjectiveState.Completed ? UITheme.Good : UITheme.Warn;
                    BoxFrame(r, color, 2f);
                    Label(new Rect(r.x + 4f, r.yMax - 20f, r.width - 8f, 18f), objective.State == ObjectiveState.Completed ? "OBJECTIVE DONE" : "OBJECTIVE", 11, color, TextAnchor.LowerLeft, true);
                }
                if (room.State != RoomState.Undiscovered && r.width > 50f)
                    Label(new Rect(r.x + 4f, r.y + 3f, r.width - 8f, 16f), room.DisplayName, 11, UITheme.Dim, TextAnchor.UpperLeft);
                else if (room.State == RoomState.Undiscovered && r.width > 50f)
                    Label(new Rect(r.x, r.y, r.width, r.height), "?", 16, UITheme.Faint, TextAnchor.MiddleCenter);
            }
        }

        void DrawWalls(LevelLayout level)
        {
            float t = Mathf.Max(2f, 0.2f * scale);
            var color = new Color(0.62f, 0.68f, 0.76f);
            foreach (var wall in level.walls)
            {
                if (wall.area != viewArea) continue;
                Vector2 a = ToMap(new Vector3(wall.a.x, 0f, wall.a.y));
                Vector2 b = ToMap(new Vector3(wall.b.x, 0f, wall.b.y));
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - t * 0.5f, Mathf.Min(a.y, b.y) - t * 0.5f, Mathf.Max(a.x, b.x) + t * 0.5f, Mathf.Max(a.y, b.y) + t * 0.5f);
                Box(r, color);
            }
        }

        void DrawDoors(LevelLayout level)
        {
            foreach (var door in level.doors)
            {
                if (door.Area != viewArea) continue;
                bool known = (door.RoomFront != null && door.RoomFront.State != RoomState.Undiscovered) || (door.RoomBack != null && door.RoomBack.State != RoomState.Undiscovered)
                    || (door.RoomFront != null && !door.RoomFront.Indoor) || (door.RoomBack != null && !door.RoomBack.Indoor);
                Color color;
                if (!known) color = new Color(0.4f, 0.4f, 0.45f);
                else switch (door.State)
                {
                    case DoorState.Locked: color = UITheme.Bad; break;
                    case DoorState.Wedged: color = UITheme.Warn; break;
                    case DoorState.Open: color = new Color(0.3f, 0.35f, 0.4f); break;
                    case DoorState.Breached: color = new Color(0.9f, 0.5f, 0.2f); break;
                    case DoorState.Disabled: color = new Color(0.2f, 0.2f, 0.22f); break;
                    default: color = new Color(0.75f, 0.55f, 0.35f); break;
                }
                Vector2 c = ToMap(door.transform.position);
                Vector3 along = door.transform.right * door.Width * 0.5f;
                Vector2 a = ToMap(door.transform.position - along), b = ToMap(door.transform.position + along);
                float t = Mathf.Max(3f, 0.3f * scale);
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - t * 0.5f, Mathf.Min(a.y, b.y) - t * 0.5f, Mathf.Max(a.x, b.x) + t * 0.5f, Mathf.Max(a.y, b.y) + t * 0.5f);
                Box(r, color);
                if (door.Breachable && known && door.State != DoorState.Breached) Dot(c, 2f, UITheme.Warn);
            }
        }

        void DrawZones(LevelLayout level)
        {
            if (VersusMatch.Active)
            {
                DrawVersusAreas(VersusMatch.Instance);
                return;
            }
            if (level.extraction != null && level.extraction.Area == viewArea)
            {
                var r = ToMap(level.extraction.Bounds);
                BoxFrame(r, UITheme.Accent, 2f);
                Label(new Rect(r.x, r.yMax + 2f, Mathf.Max(r.width, 90f), 16f), "EXTRACTION", 11, UITheme.Accent, TextAnchor.UpperLeft, true);
            }
            foreach (var zone in level.safeZones)
            {
                if (zone.Area != viewArea) continue;
                var r = ToMap(zone.Bounds);
                BoxFrame(r, UITheme.Good, 2f);
                Label(new Rect(r.x, r.yMax + 2f, Mathf.Max(r.width, 90f), 16f), "SAFE ZONE", 11, UITheme.Good, TextAnchor.UpperLeft, true);
            }
            foreach (var stairs in level.stairs)
            {
                if (!InView(stairs.transform.position, level)) continue;
                Vector2 c = ToMap(stairs.transform.position);
                Box(new Rect(c.x - 6f, c.y - 6f, 12f, 12f), new Color(0.3f, 0.75f, 0.45f));
                Label(new Rect(c.x + 8f, c.y - 8f, 140f, 16f), "Stairs", 11, UITheme.Good);
            }
        }

        void DrawObjects(LevelLayout level)
        {
            foreach (var item in level.evidence)
            {
                if (!item.Revealed || item.Secured || !InView(item.transform.position, level)) continue;
                Vector2 c = ToMap(item.transform.position);
                Line(c + new Vector2(0f, -6f), c + new Vector2(6f, 0f), UITheme.Warn, 3f);
                Line(c + new Vector2(6f, 0f), c + new Vector2(0f, 6f), UITheme.Warn, 3f);
                Line(c + new Vector2(0f, 6f), c + new Vector2(-6f, 0f), UITheme.Warn, 3f);
                Line(c + new Vector2(-6f, 0f), c + new Vector2(0f, -6f), UITheme.Warn, 3f);
            }
            foreach (var console in level.consoles)
            {
                var room = level.RoomAt(console.transform.position);
                if (!InView(console.transform.position, level) || (room != null && room.State == RoomState.Undiscovered)) continue;
                Vector2 c = ToMap(console.transform.position);
                Box(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), new Color(0.3f, 0.85f, 0.95f));
            }
            foreach (var cam in SecurityCamera.All)
            {
                if (cam == null || !cam.Active || !InView(cam.transform.position, level)) continue;
                var room = level.RoomAt(cam.transform.position);
                if (room != null && room.State == RoomState.Undiscovered) continue;
                Dot(ToMap(cam.transform.position), 3f, UITheme.Bad);
            }
        }

        // Game modes: bases, the zone, the flags and your team's pings.
        void DrawVersusAreas(VersusMatch match)
        {
            foreach (var ping in match.Pings)
            {
                Vector2 p = ToMap(ping.position);
                var color = ping.enemy ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.85f, 0.3f);
                RingAt(p, 7f, color, 2f);
                Label(new Rect(p.x - 40f, p.y - 22f, 80f, 16f), ping.enemy ? "ENEMY" : "PING", 11, color, TextAnchor.UpperCenter, true);
            }
            for (int side = 0; side < 2; side++)
            {
                Vector2 b = ToMap(match.Bases[side]);
                var color = VersusHUD.SideColor(side);
                RingAt(b, Mathf.Clamp(1.3f * scale, 6f, 14f), color, 2f);
                Label(new Rect(b.x - 40f, b.y + 10f, 80f, 16f), side == 0 ? "SWAT BASE" : "SUSPECT BASE", 11, color, TextAnchor.UpperCenter, true);
            }
            if (match.Mode == GameMode.ZoneControl)
            {
                var r = ToMap(match.Zone);
                var color = match.ZoneOwner < 0 ? Color.white : VersusHUD.SideColor(match.ZoneOwner);
                BoxFrame(r, color, 2f);
                Label(new Rect(r.x, r.yMax + 2f, Mathf.Max(r.width, 60f), 16f), "ZONE", 11, color, TextAnchor.UpperLeft, true);
            }
            if (match.Mode == GameMode.CaptureTheFlag)
                for (int side = 0; side < 2; side++)
                {
                    var flag = match.Flags[side];
                    if (flag == null) continue;
                    var carrier = flag.carrier as IVersusMember;
                    if (carrier != null && carrier.Side != match.MySide && !carrier.Seen) continue;
                    Vector2 p = ToMap(flag.position);
                    var color = VersusHUD.SideColor(side);
                    Box(new Rect(p.x - 1f, p.y - 10f, 2f, 10f), Color.white);
                    Box(new Rect(p.x + 1f, p.y - 10f, 8f, 5f), color);
                }
            if (match.Mode == GameMode.VipEscort)
            {
                var gold = new Color(1f, 0.82f, 0.25f);
                Vector2 exit = ToMap(match.VipExit);
                RingAt(exit, Mathf.Clamp(1.1f * scale, 6f, 12f), gold, 2f);
                Label(new Rect(exit.x - 50f, exit.y + 10f, 100f, 16f), "EXTRACTION", 11, gold, TextAnchor.UpperCenter, true);
            }
            if (match.Mode == GameMode.RapidDeployment)
            {
                var red = new Color(1f, 0.35f, 0.25f);
                for (int i = 0; i < match.Bombs.Count; i++)
                {
                    var bomb = match.Bombs[i];
                    if (bomb.disarmed) continue;
                    Vector2 p = ToMap(bomb.position);
                    Box(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), red);
                    Label(new Rect(p.x - 40f, p.y + 6f, 80f, 16f), "DEVICE " + (i + 1), 10, red, TextAnchor.UpperCenter, true);
                }
            }
        }

        void DrawVersusPeople(VersusMatch match, float r)
        {
            foreach (var other in match.Others)
            {
                if (!other.IsAlive || (other.Side != match.MySide && !other.Seen)) continue;
                Dot(ToMap(other.Position), r * (other.IsHuman ? 1.1f : 0.9f), VersusHUD.SideColor(other.Side));
            }
        }

        void DrawPeople(GameManager game)
        {
            var level = game.Level;
            var intel = TacticalIntel.Instance;
            float r = Mathf.Clamp(0.4f * scale, 4f, 9f);
            if (VersusMatch.Active) DrawVersusPeople(VersusMatch.Instance, r);

            foreach (var civilian in AIManager.Instance.Civilians)
            {
                if (!intel.IsDiscovered(civilian) || civilian.IsEvacuated || civilian.Area != viewArea) continue;
                Color color = !civilian.IsAlive ? new Color(0.4f, 0.4f, 0.4f) : civilian.State == CivilianState.Injured || civilian.State == CivilianState.Captive ? UITheme.Warn : UITheme.Good;
                Dot(ToMap(civilian.Position), r, color);
            }

            foreach (var enemy in AIManager.Instance.Enemies)
            {
                var contact = intel.ContactFor(enemy);
                if (!contact.everSeen) continue;
                if (enemy.IsNeutralized)
                {
                    if (enemy.Escaped || level.AreaAt(enemy.Position) != viewArea) continue;
                    Vector2 p = ToMap(enemy.Position);
                    Line(p + new Vector2(-r, -r), p + new Vector2(r, r), new Color(0.55f, 0.55f, 0.6f), 2f);
                    Line(p + new Vector2(-r, r), p + new Vector2(r, -r), new Color(0.55f, 0.55f, 0.6f), 2f);
                    continue;
                }
                bool live = intel.IsRevealed(enemy);
                Vector3 where = live ? enemy.Position : contact.lastSeen;
                if (level.AreaAt(where) != viewArea) continue;
                Vector2 m = ToMap(where);
                if (live)
                {
                    Dot(m, r, enemy.State == EnemyState.Surrendering || enemy.State == EnemyState.Incapacitated ? UITheme.Warn : UITheme.Bad);
                }
                else
                {
                    float age = Time.time - contact.lastSeenTime;
                    var color = UITheme.Bad;
                    color.a = Mathf.Clamp(1f - age / 60f, 0.3f, 0.9f);
                    RingAt(m, r, color, 2f);
                    Label(new Rect(m.x - 10f, m.y - 9f, 20f, 18f), "?", 12, color, TextAnchor.MiddleCenter, true);
                }
            }

            var squad = SquadCommandManager.Instance;
            for (int i = 0; i < squad.Squad.Count; i++)
            {
                var officer = squad.Squad[i];
                if (officer.Area != viewArea) continue;
                Vector2 p = ToMap(officer.Position);
                Vector3 previous = officer.Position;
                foreach (var waypoint in officer.Waypoints)
                {
                    Vector2 next = ToMap(waypoint);
                    Line(ToMap(previous), next, new Color(0.36f, 0.62f, 0.95f, 0.6f), 2f);
                    Dot(next, 3f, UITheme.Accent);
                    previous = waypoint;
                }
                Color color = !officer.IsAlive ? UITheme.Bad : officer.Selected ? UITheme.Accent : new Color(0.55f, 0.7f, 0.95f);
                Dot(p, r + 1f, color);
                Label(new Rect(p.x - 10f, p.y - 10f, 20f, 20f), (i + 1).ToString(), 11, Color.black, TextAnchor.MiddleCenter, true);
                if (officer.Selected) RingAt(p, r + 5f, UITheme.Accent, 2f);
            }

            var player = game.Player;
            if (level.AreaAt(player.Position) == viewArea)
            {
                Vector2 p = ToMap(player.Position);
                Dot(p, r + 2f, Color.white);
                Vector2 dir = new Vector2(player.AimDirection.x, -player.AimDirection.z);
                Line(p, p + dir * (r + 10f), Color.white, 3f);
            }
        }

        void DrawMarkers()
        {
            foreach (var marker in TacticalIntel.Instance.Markers)
            {
                if (GameManager.Instance.Level.AreaAt(marker) != viewArea) continue;
                Vector2 p = ToMap(marker);
                RingAt(p, 8f, UITheme.Warn, 2f);
                Box(new Rect(p.x - 1f, p.y - 12f, 2f, 24f), UITheme.Warn);
                Box(new Rect(p.x - 12f, p.y - 1f, 24f, 2f), UITheme.Warn);
            }
            // Chem lights marking cleared rooms.
            foreach (var light in ChemLight.Placed)
            {
                if (GameManager.Instance.Level.AreaAt(light) != viewArea) continue;
                Vector2 p = ToMap(light);
                Box(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), ChemLight.Glow);
            }
        }

        void DrawLegend(Rect rect)
        {
            float x = rect.x;
            Legend(ref x, rect.y, Color.white, "You");
            Legend(ref x, rect.y, new Color(0.55f, 0.7f, 0.95f), "Squad");
            Legend(ref x, rect.y, UITheme.Bad, "Threat");
            Legend(ref x, rect.y, new Color(UITheme.Bad.r, UITheme.Bad.g, UITheme.Bad.b, 0.5f), "Last known");
            Legend(ref x, rect.y, UITheme.Good, "Civilian");
            Legend(ref x, rect.y, UITheme.Warn, "Needs help / marker");
            Legend(ref x, rect.y, new Color(0.3f, 0.85f, 0.95f), "Console");
        }

        static void Legend(ref float x, float y, Color color, string label)
        {
            UITheme.Dot(new Vector2(x + 6f, y + 10f), 5f, color);
            UITheme.Text(new Rect(x + 16f, y, 160f, 20f), label, 13, UITheme.Dim, TextAnchor.MiddleLeft);
            x += 26f + label.Length * 7.5f;
        }

        // ---- Planning ----

        void HandleInput(GameManager game)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !mapRect.Contains(e.mousePosition)) return;
            Vector3 world = ToWorld(e.mousePosition);
            var squad = SquadCommandManager.Instance;
            if (e.button == 0)
            {
                // Click an officer to toggle selection, anywhere else to place or remove a marker.
                for (int i = 0; i < squad.Squad.Count; i++)
                {
                    var officer = squad.Squad[i];
                    if (officer.Area == viewArea && Vector2.Distance(ToMap(officer.Position), e.mousePosition) < 14f)
                    {
                        squad.ToggleSelect(i);
                        e.Use();
                        return;
                    }
                }
                TacticalIntel.Instance.ToggleMarker(world);
                AudioManager.Ui(Sound.Click, 0.4f);
                e.Use();
            }
            else if (e.button == 1)
            {
                if (game.Level.AreaAt(world) != viewArea || !NavMeshPoint(ref world))
                {
                    UIManager.Notify("Can't move there");
                    e.Use();
                    return;
                }
                bool sameFloor = true;
                foreach (var officer in squad.Targets()) if (officer.Area != viewArea) sameFloor = false;
                if (!sameFloor) UIManager.Notify("Some selected officers are on another floor; they will use the stairs only when following you.");
                squad.PlanWaypoint(world, e.shift);
                e.Use();
            }
        }

        static bool NavMeshPoint(ref Vector3 point)
        {
            UnityEngine.AI.NavMeshHit hit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(point, out hit, 1.5f, UnityEngine.AI.NavMesh.AllAreas)) return false;
            point = hit.position;
            return true;
        }

        void DrawPlanningPanel(GameManager game, Rect rect)
        {
            UITheme.Panel(rect);
            var squad = SquadCommandManager.Instance;
            float x = rect.x + 20f, cw = rect.width - 40f, y = rect.y + 16f;
            UITheme.Text(new Rect(x, y, cw, 22f), "SQUAD", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            if (squad.Squad.Count == 0)
            {
                UITheme.Text(new Rect(x, y, cw, 40f), "You deployed without squadmates. Markers can still be placed.", 15, UITheme.Dim);
                y += 44f;
            }
            for (int i = 0; i < squad.Squad.Count; i++)
            {
                var officer = squad.Squad[i];
                var r = new Rect(x, y, cw, 76f);
                if (UITheme.Button(r, string.Empty, officer.IsAlive, officer.Selected)) squad.ToggleSelect(i);
                UITheme.RoleIcon(new Rect(r.x + 10f, r.y + 10f, 32f, 32f), officer.Data.role);
                UITheme.Text(new Rect(r.x + 52f, r.y + 6f, cw - 60f, 22f), (i + 1) + ". " + officer.Data.displayName + " \"" + officer.Data.callsign + "\"", 15, UITheme.TextColor, TextAnchor.UpperLeft, true);
                UITheme.Text(new Rect(r.x + 52f, r.y + 28f, cw - 60f, 20f), UITheme.RoleName(officer.Data.role) + "  |  " + officer.Status, 13, officer.IsAlive ? UITheme.Dim : UITheme.Bad);
                UITheme.Bar(new Rect(r.x + 52f, r.y + 52f, cw * 0.4f, 6f), officer.Health.Fraction, officer.Health.Fraction > 0.5f ? UITheme.Good : UITheme.Warn);
                var inv = officer.Inventory;
                UITheme.Text(new Rect(r.x + 60f + cw * 0.4f, r.y + 46f, cw * 0.5f, 20f),
                    "Flash " + inv.CountOf(EquipmentKind.Flashbang) + "  Charge " + inv.CountOf(EquipmentKind.BreachingCharge) + "  Med " + inv.CountOf(EquipmentKind.MedicalKit), 12, UITheme.Dim);
                y += 82f;
            }
            if (UITheme.Button(new Rect(x, y, cw, 34f), "Select whole squad", squad.Squad.Count > 0, !squad.AnySelected, 15)) squad.SelectAll();
            y += 44f;

            UITheme.Text(new Rect(x, y, cw, 22f), "QUICK ORDERS  (" + squad.SelectionLabel + ")", 15, UITheme.Accent, TextAnchor.UpperLeft, true);
            y += 26f;
            var player = game.Player;
            float bw = (cw - 8f) * 0.5f;
            if (UITheme.Button(new Rect(x, y, bw, 34f), "Follow me", squad.Squad.Count > 0, false, 15)) squad.Issue(SquadOrder.Follow, player.Position, null, DoorAction.None);
            if (UITheme.Button(new Rect(x + bw + 8f, y, bw, 34f), "Hold position", squad.Squad.Count > 0, false, 15)) squad.Issue(SquadOrder.Hold, player.Position, null, DoorAction.None);
            y += 40f;
            if (UITheme.Button(new Rect(x, y, bw, 34f), "Regroup", squad.Squad.Count > 0, false, 15)) squad.Issue(SquadOrder.Regroup, player.Position, null, DoorAction.None);
            if (UITheme.Button(new Rect(x + bw + 8f, y, bw, 34f), "Assist civilians", squad.Squad.Count > 0, false, 15)) squad.Issue(SquadOrder.AssistCivilians, player.Position, null, DoorAction.None);
            y += 48f;

            string help = "Left-click an officer: select\nLeft-click the map: place/remove marker\nRight-click: move selected officers there\nShift + right-click: add a waypoint\n"
                + UITheme.KeyFor(InputAction.PlanningMode) + " or Esc: resume";
            UITheme.Text(new Rect(x, y, cw, 120f), help, 14, UITheme.Dim);
            y += 124f;
            SaveManager.Settings.planningPauses = UITheme.Toggle(new Rect(x, y, cw, 28f), "Pause the game while planning (off = slow motion)", SaveManager.Settings.planningPauses);
            if (UITheme.Button(new Rect(x, rect.yMax - 60f, cw, 44f), "Resume mission", true, true, 18)) game.SetPlanning(false);
        }
    }
}
