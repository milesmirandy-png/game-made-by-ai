using UnityEngine;

namespace Swat
{
    // Map: Club Halcyon, a nightclub with a large open dance floor, a bar,
    // backstage and the manager's office behind it.
    //
    //  z=28 +-------+---L------+---------+---------+
    //       |Storage| Backstage D Manager | VIP     |
    //  (L)  |       |          | Office  | Lounge  |
    //  z=20 +-----D-+-----D----+---L-----+----D----+
    //       |  Bar   (open)          |  booths     |
    //       |        |  Dance [BAR]  |             |
    //   z=6 +--------+---  ----+----D----+----D----+
    //       |Coat   D Entrance |Restrooms| Staff    D (staff exit)
    //       |Check  | Lobby    |         | Break    |
    //   z=0 +-------+----D-----+---------+----------+
    //      x=0      8         16        26         36
    //
    // The dance floor isn't one open box: an island bar with a tall bottle tower stands in the middle,
    // booth partitions screen off the east side, and pillars and the DJ booth break it up further. A
    // screen inside the entrance keeps the street from seeing in, and backstage has a real door, off
    // line from the lobby opening.
    public static class NightclubMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "nightclub", "Club Halcyon");
            var outside = new Color(0.2f, 0.2f, 0.24f);
            var inside = new Color(0.32f, 0.28f, 0.36f);
            var wood = new Color(0.32f, 0.22f, 0.16f);
            var dark = new Color(0.18f, 0.17f, 0.2f);
            var asphalt = new Color(0.2f, 0.21f, 0.23f);

            b.Area("Club Halcyon", -12f, -22f, 48f, 36f);
            b.Layout.navBounds = new Bounds(new Vector3(18f, 1f, 7f), new Vector3(66f, 6f, 64f));

            // Outside
            b.Slab("Ground", -40f, -40f, 70f, 56f, -0.2f, 0f, new Color(0.26f, 0.32f, 0.24f));
            b.Room("street", "Street", -12f, -22f, 48f, -0.2f, asphalt, false);
            b.Room("westlot", "West Lot", -12f, -0.2f, -0.2f, 28.2f, asphalt, false);
            b.Room("alley", "Back Alley", -12f, 28.2f, 48f, 36f, asphalt, false);
            b.Slab("Sidewalk", -2f, -2f, 38f, 0f, -0.05f, 0.02f, new Color(0.5f, 0.5f, 0.5f));
            b.Decal(new Vector3(12f, 1.3f, -0.15f), new Vector3(6f, 0.5f, 0.06f), new Color(1f, 0.25f, 0.85f), 2f);
            b.Prop("Velvet Rope", new Vector3(7f, 0f, -1.2f), new Vector3(3f, 0.9f, 0.08f), new Color(0.6f, 0.1f, 0.15f), false);
            for (float x = -8f; x <= 6f; x += 3.6f) b.Decal(new Vector3(x, 0.02f, -15f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-6f, 0f, -15f), 0f, new Color(0.95f, 0.8f, 0.1f));
            b.Car(new Vector3(-2.4f, 0f, -15f), 0f, new Color(0.1f, 0.1f, 0.12f));
            b.Car(new Vector3(4.8f, 0f, -15f), 0f, new Color(0.5f, 0.5f, 0.55f));
            b.Car(new Vector3(30f, 0f, -16f), 30f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Car(new Vector3(42f, 0f, 10f), 0f, new Color(0.35f, 0.1f, 0.4f));
            b.Prop("Dumpster", new Vector3(24f, 0f, 31f), new Vector3(2.2f, 1.4f, 1.2f), new Color(0.2f, 0.35f, 0.25f), true);
            b.Lamp(new Vector3(4f, 0f, -2.5f));
            b.Lamp(new Vector3(22f, 0f, -2.5f));
            b.Lamp(new Vector3(40f, 0f, 20f));
            b.Lamp(new Vector3(6f, 0f, 30f));
            b.Boundary(-12f, -22f, 48f, 36f);
            b.Arrival(new Vector3(12f, 0.05f, -9f), 0f, new Vector3(17f, 0f, -11f), 0f, new Vector3(17f, 0f, -36f));
            b.Extraction(9.5f, -12f, 14.5f, -6.5f);
            b.SafeZone(24f, -9f, 28f, -5f);

            // Rooms
            b.Room("coat", "Coat Check", 0f, 0f, 8f, 6f, wood);
            b.Room("lobby", "Entrance Lobby", 8f, 0f, 16f, 6f, dark);
            b.Room("restrooms", "Restrooms", 16f, 0f, 26f, 6f, new Color(0.45f, 0.45f, 0.5f));
            b.Room("staff", "Staff Break Room", 26f, 0f, 36f, 6f, new Color(0.5f, 0.46f, 0.4f));
            b.Room("bar", "Bar", 0f, 6f, 8f, 20f, wood);
            b.Room("dance", "Dance Floor", 8f, 6f, 36f, 20f, dark);
            b.Room("storage", "Storage", 0f, 20f, 8f, 28f, new Color(0.42f, 0.42f, 0.42f));
            b.Room("backstage", "Backstage", 8f, 20f, 18f, 28f, dark);
            b.Room("office", "Manager's Office", 18f, 20f, 26f, 28f, new Color(0.38f, 0.28f, 0.22f));
            b.Room("vip", "VIP Lounge", 26f, 20f, 36f, 28f, new Color(0.3f, 0.12f, 0.2f));

            // Walls
            b.WallX(0f, 0f, 36f, outside, true, Gap.Door(12f, "front", false, 2.4f));
            b.WallX(28f, 0f, 36f, outside, true, Gap.Locked(13f, "stage_door", true, true));
            b.WallZ(0f, 0f, 28f, outside, true, Gap.Locked(24f, "storage_exit", true, true));
            b.WallZ(36f, 0f, 28f, outside, true, Gap.Door(3f, "staff_exit"));
            b.WallX(6f, 0f, 36f, inside, false, Gap.Open(12f, 3f), Gap.Door(21f, "restroom_door"), Gap.Door(31f, "staff_door", true));
            b.WallZ(8f, 0f, 6f, inside, false, Gap.Door(3f, "coat_door"));
            b.WallZ(16f, 0f, 6f, inside, false);
            b.WallZ(26f, 0f, 6f, inside, false);
            b.WallZ(8f, 6f, 20f, inside, false, Gap.Open(10f, 3f), Gap.Open(16f, 3f));
            b.WallX(20f, 0f, 36f, inside, false, Gap.Door(6.8f, "storage_door", true), Gap.Door(15.5f, "backstage_door", true, 1.8f), Gap.Locked(22f, "office_door", true, true), Gap.Door(33.5f, "vip_door", true));
            b.WallZ(8f, 20f, 28f, inside, false);
            b.WallZ(18f, 20f, 28f, inside, false, Gap.Door(24f, "backstage_office"));
            b.WallZ(26f, 20f, 28f, inside, false);

            // Entrance and coat check
            b.Prop("Ticket Counter", new Vector3(15.2f, 0f, 3.6f), new Vector3(0.8f, 1.1f, 2.5f), new Color(0.3f, 0.2f, 0.25f), true);
            b.Partition(9.8f, 2.6f, 13.2f, 2.6f, 2f, new Color(0.22f, 0.16f, 0.22f));
            b.Prop("Coat Rack", new Vector3(2f, 0f, 4.5f), new Vector3(3f, 1.8f, 0.5f), new Color(0.25f, 0.2f, 0.18f), false);
            b.Prop("Coat Counter", new Vector3(5f, 0f, 3f), new Vector3(0.6f, 1.05f, 3f), new Color(0.4f, 0.28f, 0.2f), true);

            // Restrooms and staff room
            b.Prop("Stall Divider", new Vector3(17.8f, 0f, 1.4f), new Vector3(0.08f, 1.4f, 2.4f), new Color(0.4f, 0.4f, 0.45f), false);
            b.Prop("Stall Divider", new Vector3(19.6f, 0f, 1.4f), new Vector3(0.08f, 1.4f, 2.4f), new Color(0.4f, 0.4f, 0.45f), false);
            b.Prop("Sinks", new Vector3(25.5f, 0f, 3f), new Vector3(0.5f, 0.9f, 3f), new Color(0.85f, 0.85f, 0.85f), false);
            b.Table(new Vector3(31f, 0f, 3f), new Vector3(1.6f, 0.75f, 1f), new Color(0.6f, 0.55f, 0.48f));
            b.Prop("Lockers", new Vector3(27.5f, 0f, 0.6f), new Vector3(2.4f, 2f, 0.6f), new Color(0.4f, 0.45f, 0.5f), true);

            // Bar
            b.Prop("Bar Counter", new Vector3(5f, 0f, 13f), new Vector3(0.8f, 1.1f, 8f), new Color(0.35f, 0.22f, 0.14f), true);
            b.Prop("Bottle Shelf", new Vector3(0.4f, 0f, 13f), new Vector3(0.5f, 2f, 6f), new Color(0.25f, 0.18f, 0.12f), false);
            b.Decal(new Vector3(0.7f, 1.4f, 13f), new Vector3(0.05f, 0.4f, 5.6f), new Color(0.3f, 0.7f, 1f), 1.2f);
            for (float z = 10f; z <= 16f; z += 1.5f)
                b.Prop("Bar Stool", new Vector3(6.2f, 0f, z), new Vector3(0.4f, 0.75f, 0.4f), new Color(0.15f, 0.15f, 0.17f), false);

            // Dance floor
            b.Decal(new Vector3(21f, 0.035f, 13f), new Vector3(12f, 0.01f, 8f), new Color(0.6f, 0.25f, 0.9f), 0.7f);
            b.Prop("DJ Booth", new Vector3(19.5f, 0f, 18.6f), new Vector3(3f, 1.1f, 1f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Decal(new Vector3(19.5f, 1.12f, 18.6f), new Vector3(2.6f, 0.02f, 0.6f), new Color(0.2f, 0.9f, 1f), 1.5f);
            // The island bar: a counter all round (cover) and a bottle tower in the middle (blocks sight).
            b.Prop("Island Bar", new Vector3(22f, 0f, 13f), new Vector3(4.4f, 1.1f, 4.4f), new Color(0.3f, 0.2f, 0.14f), true);
            b.Prop("Bottle Tower", new Vector3(22f, 0f, 13f), new Vector3(1.4f, 2.4f, 3.6f), new Color(0.2f, 0.15f, 0.12f), false);
            b.Decal(new Vector3(22f, 1.6f, 13f), new Vector3(1.45f, 0.3f, 3.65f), new Color(0.9f, 0.3f, 1f), 1.2f);
            // Booths along the east side behind high-backed partitions, with a way through in the middle.
            var booth = new Color(0.32f, 0.1f, 0.2f);
            b.Partition(30f, 6.2f, 30f, 11.3f, 1.5f, booth);
            b.Partition(30f, 14.7f, 30f, 19.8f, 1.5f, booth);
            b.Couch(new Vector3(35.3f, 0f, 12.6f), 270f, booth);
            foreach (var p in new[] { new Vector3(14f, 0f, 9f), new Vector3(28f, 0f, 9f), new Vector3(14f, 0f, 17f), new Vector3(28f, 0f, 17f) })
                b.Prop("Pillar", p, new Vector3(0.6f, 2.6f, 0.6f), new Color(0.25f, 0.22f, 0.28f), true);
            // Wide columns just inside the two openings from the bar, so the bar doesn't see across the floor.
            b.Pillar(13.5f, 10.5f, 1.2f);
            b.Pillar(13.5f, 15.5f, 1.2f);
            b.Table(new Vector3(33f, 0f, 10f), new Vector3(1f, 0.75f, 1f), new Color(0.2f, 0.2f, 0.22f));
            b.Table(new Vector3(33f, 0f, 15f), new Vector3(1f, 0.75f, 1f), new Color(0.2f, 0.2f, 0.22f));
            b.Table(new Vector3(10.5f, 0f, 12.5f), new Vector3(1f, 0.75f, 1f), new Color(0.2f, 0.2f, 0.22f));
            b.Prop("Speaker", new Vector3(35.4f, 0f, 6.6f), new Vector3(0.8f, 1.6f, 0.8f), new Color(0.08f, 0.08f, 0.09f), true);
            b.Prop("Speaker", new Vector3(8.6f, 0f, 19.4f), new Vector3(0.8f, 1.6f, 0.8f), new Color(0.08f, 0.08f, 0.09f), true);
            b.Prop("Speaker Stack", new Vector3(18f, 0f, 7.1f), new Vector3(1f, 1.8f, 1f), new Color(0.08f, 0.08f, 0.09f), true);
            b.Prop("Speaker Stack", new Vector3(25.5f, 0f, 18.9f), new Vector3(1f, 1.8f, 1f), new Color(0.08f, 0.08f, 0.09f), true);

            // Storage and backstage
            b.Crate(new Vector3(2f, 0f, 26.5f), 1.2f, true);
            b.Crate(new Vector3(6f, 0f, 26.5f), 1f, false);
            b.Shelf(new Vector3(7.4f, 0f, 23f), 90f, 3f);
            b.Couch(new Vector3(9.5f, 0f, 25f), 90f, new Color(0.4f, 0.15f, 0.2f));
            b.Prop("Mirror Table", new Vector3(11.5f, 0f, 21f), new Vector3(2f, 0.8f, 0.6f), new Color(0.6f, 0.55f, 0.5f), true);
            b.Crate(new Vector3(15.5f, 0f, 26.8f), 1f, false);

            // Manager's office
            b.Desk(new Vector3(22f, 0f, 25.5f), 180f);
            b.Prop("Safe", new Vector3(25.4f, 0f, 27.4f), new Vector3(0.8f, 1f, 0.8f), new Color(0.25f, 0.25f, 0.27f), true);
            b.Shelf(new Vector3(25.4f, 0f, 22.5f), 90f, 2f);
            b.Console("club_console", "Club Security PC", 19.5f, 27.4f, 0f, true, true);

            // VIP lounge
            b.Couch(new Vector3(30f, 0f, 27.3f), 180f, new Color(0.5f, 0.1f, 0.2f));
            b.Couch(new Vector3(35.3f, 0f, 24f), 270f, new Color(0.5f, 0.1f, 0.2f));
            b.Table(new Vector3(30f, 0f, 25.5f), new Vector3(1.6f, 0.5f, 0.8f), new Color(0.15f, 0.12f, 0.12f));

            // Security
            b.Camera(8.6f, 6.6f, 45f);
            b.Camera(35.4f, 6.6f, 315f);
            b.Camera(0.6f, 19.4f, 135f);
            b.Camera(12f, -0.6f, 180f);
            b.Camera(26.6f, 27.4f, 135f);
            b.AlarmPanel(35.75f, 5f, 270f);
            b.Beacon(31.5f, 13f);
            b.Beacon(4f, 18f);
            b.Beacon(31f, 24f);

            b.Evidence(24f, 22f);
            b.Evidence(4.5f, 27f);
            b.Evidence(11f, 22f);
            b.Evidence(33f, 22.5f);
            b.Escape(-6f, 24f);
            b.Escape(13f, 33f);

            // Suspect spawn pool
            b.EnemySpot("lobby", 11f, 3.5f, 0f);
            b.EnemySpot("lobby", 13.8f, 4.6f, 270f);
            b.EnemySpot("coat", 3f, 2f, 0f);
            b.EnemySpot("restrooms", 23f, 3f, 90f);
            b.EnemySpot("staff", 33.5f, 4.5f, 180f);
            b.EnemySpot("bar", 2.5f, 12f, 90f);
            b.EnemySpot("bar", 7f, 18.5f, 180f);
            b.EnemySpot("dance", 12f, 9f, 0f);
            b.EnemySpot("dance", 30f, 12f, 270f);
            b.EnemySpot("dance", 21f, 17f, 0f, new Vector3(16f, 0f, 9f), new Vector3(26f, 0f, 9f), new Vector3(26f, 0f, 17f), new Vector3(16f, 0f, 17f));
            b.EnemySpot("dance", 11f, 17.5f, 90f);
            b.EnemySpot("storage", 4f, 23.5f, 0f);
            b.EnemySpot("backstage", 13f, 24f, 180f);
            b.EnemySpot("backstage", 16.5f, 23f, 270f);
            b.EnemySpot("office", 20f, 22f, 0f);
            b.EnemySpot("vip", 31f, 23f, 0f);
            b.EnemySpot("vip", 34f, 21.5f, 315f);

            // Civilian spawn pool
            b.CivilianSpot("dance", 10f, 7.5f, 45f);
            b.CivilianSpot("dance", 20f, 8f, 0f);
            b.CivilianSpot("dance", 25f, 11.5f, 270f);
            b.CivilianSpot("dance", 34.5f, 7.5f, 315f);
            b.CivilianSpot("dance", 18f, 14f, 90f);
            b.CivilianSpot("dance", 33.5f, 18.8f, 225f);
            b.CivilianSpot("bar", 6.2f, 7.2f, 45f);
            b.CivilianSpot("restrooms", 24.6f, 0.8f, 315f);
            b.CivilianSpot("staff", 35f, 5.4f, 225f);
            b.CivilianSpot("coat", 1f, 1f, 45f);
            b.CivilianSpot("vip", 27.2f, 27.2f, 135f);
            b.CivilianSpot("vip", 35.2f, 27.2f, 225f);
            b.CivilianSpot("backstage", 9f, 21.2f, 45f);

            return b.Finish();
        }
    }
}
