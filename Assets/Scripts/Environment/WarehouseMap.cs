using UnityEngine;

namespace Swat
{
    // Map 2: Kestrel Freight warehouse.
    //
    //  z=32 +--------------D-------------+--------+---------+
    //       |                            | Site   | Manager |
    //       |       Storage Aisles       D Office D         |
    //  z=20 D                            +---D----+----D----+
    //       |                            D Break  D Security|
    //  z=10 +----    ----------    ------+--------+---------+
    //       |        Loading Bays        E   Restricted     |
    //   z=0 +--bay1----bay2(L)---bay3----+-------E----------+
    //      x=0                          30                 48
    //
    // The shelving rows are staggered, so the cross aisles don't line up, and pallet stacks stand in
    // the long aisles against alternate sides, so they zig-zag: nowhere in the aisles can you see
    // the full 30 m. The loading bays have pallets and the forklift for cover.
    public static class WarehouseMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "warehouse", "Kestrel Freight Warehouse");
            var outside = new Color(0.45f, 0.47f, 0.5f);
            var inside = new Color(0.62f, 0.63f, 0.62f);
            var concrete = new Color(0.46f, 0.46f, 0.45f);
            var asphalt = new Color(0.22f, 0.23f, 0.25f);

            b.Area("Kestrel Freight", -12f, -22f, 60f, 40f);
            b.Layout.navBounds = new Bounds(new Vector3(24f, 1f, 9f), new Vector3(76f, 6f, 64f));

            b.Slab("Ground", -40f, -40f, 90f, 60f, -0.2f, 0f, new Color(0.3f, 0.36f, 0.26f));
            b.Room("yard", "Loading Yard", -12f, -20f, 60f, -0.2f, asphalt, false);
            b.Room("westlane", "West Lane", -10f, -0.2f, -0.2f, 32f, asphalt, false);
            b.Room("northlane", "North Lane", -10f, 32.2f, 50f, 38f, asphalt, false);
            for (float x = 2f; x <= 28f; x += 9f) b.Decal(new Vector3(x + 4f, 0.02f, -3f), new Vector3(4f, 0.01f, 0.2f), new Color(1f, 0.8f, 0.1f));
            b.Truck(new Vector3(6f, 0f, -10f), 0f);
            b.Truck(new Vector3(24f, 0f, -12f), 10f);
            b.Car(new Vector3(52f, 0f, -12f), 90f, new Color(0.25f, 0.3f, 0.4f));
            b.Car(new Vector3(30f, 0f, -6f), 15f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Lamp(new Vector3(2f, 0f, -2f));
            b.Lamp(new Vector3(20f, 0f, -2f));
            b.Lamp(new Vector3(38f, 0f, -2f));
            b.Lamp(new Vector3(-6f, 0f, 20f));
            b.Boundary(-10f, -20f, 58f, 38f);
            b.Arrival(new Vector3(36f, 0.05f, -7f), 0f, new Vector3(40f, 0f, -9f), 0f, new Vector3(40f, 0f, -34f));
            b.Extraction(33f, -11f, 37.6f, -4.5f);
            b.SafeZone(44f, -12f, 48f, -7f);

            b.Room("bays", "Loading Bays", 0f, 0f, 30f, 10f, concrete);
            b.Room("aisles", "Storage Aisles", 0f, 10f, 30f, 32f, concrete);
            b.Room("restricted", "Restricted Storage", 30f, 0f, 48f, 10f, new Color(0.4f, 0.4f, 0.42f));
            b.Room("breakroom", "Break Room", 30f, 10f, 39f, 20f, new Color(0.62f, 0.55f, 0.45f));
            b.Room("booth", "Security Booth", 39f, 10f, 48f, 20f, new Color(0.3f, 0.33f, 0.38f));
            b.Room("office", "Site Office", 30f, 20f, 39f, 32f, new Color(0.36f, 0.42f, 0.52f));
            b.Room("manager", "Manager's Office", 39f, 20f, 48f, 32f, new Color(0.42f, 0.3f, 0.22f));

            b.WallX(0f, 0f, 48f, outside, true, Gap.Open(6f, 4f), Gap.Locked(15f, "bay2", true, false, 4f), Gap.Open(24f, 4f), Gap.Electronic(39f, "restricted_gate"));
            b.WallX(32f, 0f, 48f, outside, true, Gap.Locked(10f, "north_exit", true, true));
            b.WallZ(0f, 0f, 32f, outside, true, Gap.Door(20f, "west_door"));
            b.WallZ(48f, 0f, 32f, outside, true);

            b.WallX(10f, 0f, 30f, inside, false, Gap.Open(8f, 4f), Gap.Open(22f, 4f));
            b.WallZ(30f, 0f, 10f, inside, false, Gap.Electronic(5f, "restricted_door"));
            b.WallZ(30f, 10f, 20f, inside, false, Gap.Door(17.5f, "aisles_break", true));
            b.WallZ(30f, 20f, 32f, inside, false, Gap.Door(26f, "aisles_office", true));
            b.WallX(10f, 30f, 48f, inside, false);
            b.WallZ(39f, 10f, 20f, inside, false, Gap.Door(13f, "break_booth"));
            b.WallX(20f, 30f, 39f, inside, false, Gap.Door(34f, "break_office", true));
            b.WallX(20f, 39f, 48f, inside, false, Gap.Door(44f, "booth_manager", true));
            b.WallZ(39f, 20f, 32f, inside, false, Gap.Door(29f, "office_manager"));

            // Loading bays
            var pallet = new Color(0.62f, 0.5f, 0.32f);
            b.Crate(new Vector3(3.5f, 0f, 4f), 1.2f, true);
            b.Prop("Pallet Stack", new Vector3(7.6f, 0f, 5.6f), new Vector3(2f, 1.8f, 1.4f), pallet, true);
            b.Prop("Pallet Stack", new Vector3(11.4f, 0f, 1.8f), new Vector3(2f, 1.6f, 1.4f), pallet, true);
            b.Prop("Pallet Stack", new Vector3(3f, 0f, 8.4f), new Vector3(2f, 1.6f, 1.2f), pallet, true);
            b.Prop("Pallet Stack", new Vector3(25.2f, 0f, 8.6f), new Vector3(2f, 1.6f, 1.2f), pallet, true);
            b.Prop("Pallet Stack", new Vector3(21.5f, 0f, 5.6f), new Vector3(1.4f, 1.8f, 2f), pallet, true);
            b.Crate(new Vector3(19f, 0f, 3.2f), 1.2f, true);
            b.Prop("Pallet Stack", new Vector3(20f, 0f, 1f), new Vector3(2f, 1.6f, 1.4f), pallet, true);
            b.Crate(new Vector3(27f, 0f, 4.4f), 1.2f, true);
            b.Prop("Forklift", new Vector3(15f, 0f, 7.2f), new Vector3(1.2f, 1.8f, 2.2f), new Color(0.95f, 0.7f, 0.1f), true, 90f);

            // Storage aisles: four rows of tall shelving, each broken in different places so the cross
            // aisles don't line up from one row to the next.
            var rows = new[]
            {
                new { z = 14f, runs = new[] { 1.5f, 8f, 11f, 18f, 21f, 28.5f } },
                new { z = 18.5f, runs = new[] { 0.5f, 4f, 7f, 14f, 17f, 24f, 26.5f, 28.6f } },
                new { z = 23f, runs = new[] { 1.5f, 10f, 13f, 20f, 23f, 28.5f } },
                new { z = 27.5f, runs = new[] { 0.5f, 6f, 9f, 16f, 19f, 26f } },
            };
            foreach (var row in rows)
                for (int i = 0; i + 1 < row.runs.Length; i += 2)
                    b.Shelf(new Vector3((row.runs[i] + row.runs[i + 1]) * 0.5f, 0f, row.z), 0f, row.runs[i + 1] - row.runs[i], 2.4f);
            // Pallet stacks in each aisle between the rows: three per aisle against alternate sides, each
            // more than half the aisle deep, so the aisle zig-zags.
            float[] laneSouth = { 10f, 14.25f, 18.75f, 23.25f, 27.75f };
            float[] laneNorth = { 13.75f, 18.25f, 22.75f, 27.25f, 32f };
            for (int lane = 0; lane < laneSouth.Length; lane++)
            {
                float[] xs = lane == 0 ? new[] { 3f, 15f, 27f } : new[] { 6f, 15f, 24f };
                for (int k = 0; k < 3; k++)
                {
                    bool south = (k + lane) % 2 == 0;
                    float z = south ? laneSouth[lane] + 1.1f : laneNorth[lane] - 1.1f;
                    b.Prop("Pallet Stack", new Vector3(xs[k], 0f, z), new Vector3(2f, 1.9f, 2.2f), pallet, true);
                }
            }

            // Restricted storage
            b.Crate(new Vector3(34f, 0f, 4f), 1.2f, true);
            b.Crate(new Vector3(44f, 0f, 6f), 1.2f, false);
            b.Crate(new Vector3(40f, 0f, 2.5f), 1f, false);

            // Break room
            b.Table(new Vector3(34.5f, 0f, 15f), new Vector3(2.4f, 0.75f, 1.2f), new Color(0.75f, 0.72f, 0.66f));
            b.Prop("Vending Machine", new Vector3(38.4f, 0f, 11.5f), new Vector3(0.8f, 2f, 1f), new Color(0.2f, 0.4f, 0.7f), true);
            b.Couch(new Vector3(37f, 0f, 18.8f), 0f, new Color(0.35f, 0.3f, 0.25f));

            // Security booth
            b.Console("booth_console", "Security Booth Console", 41.5f, 18.8f, 0f, true, true);
            b.Prop("Lockers", new Vector3(47.4f, 0f, 12.5f), new Vector3(0.6f, 2f, 2.4f), new Color(0.4f, 0.45f, 0.5f), true);

            // Site office
            b.Desk(new Vector3(33f, 0f, 24f), 0f);
            b.Desk(new Vector3(36.5f, 0f, 24f), 0f);
            b.Desk(new Vector3(33f, 0f, 29f), 180f);
            b.Shelf(new Vector3(38.6f, 0f, 22.5f), 90f, 1.6f);

            // Manager's office
            b.Desk(new Vector3(44f, 0f, 27f), 180f, 2f);
            b.Prop("Safe", new Vector3(47.2f, 0f, 31f), new Vector3(0.8f, 1f, 0.8f), new Color(0.25f, 0.25f, 0.27f), true);
            b.Couch(new Vector3(46.8f, 0f, 24.5f), 270f, new Color(0.25f, 0.2f, 0.18f));

            // Security
            b.Camera(1f, 9.4f, 45f);
            b.Camera(0.6f, 31.4f, 135f);
            b.Camera(47.4f, 0.6f, 315f);
            b.Camera(12f, -0.6f, 180f);
            b.AlarmPanel(39.25f, 17f, 90f);
            b.Beacon(15f, 12f);
            b.Beacon(15f, 25f);
            b.Beacon(35f, 5f);
            b.Beacon(44f, 15f);

            b.Evidence(36f, 7f);
            b.Evidence(45f, 3f);
            b.Evidence(32.5f, 2f);
            b.Evidence(36.5f, 28.5f);
            b.Evidence(46f, 29f);
            b.Evidence(20.5f, 21f);
            b.Escape(10f, 35f);
            b.Escape(-5f, 20f);

            b.EnemySpot("bays", 10f, 5f, 90f, new Vector3(4f, 0f, 7.5f), new Vector3(27f, 0f, 7.5f));
            b.EnemySpot("bays", 20f, 2.5f, 0f);
            b.EnemySpot("bays", 28f, 8.5f, 270f);
            b.EnemySpot("aisles", 10f, 16.2f, 0f, new Vector3(10f, 0f, 12f), new Vector3(10f, 0f, 30f), new Vector3(20f, 0f, 30f), new Vector3(20f, 0f, 12f));
            b.EnemySpot("aisles", 3.5f, 21.8f, 90f);
            b.EnemySpot("aisles", 26.5f, 24.4f, 270f);
            b.EnemySpot("aisles", 11f, 30.6f, 180f);
            b.EnemySpot("aisles", 27.5f, 13.1f, 270f);
            b.EnemySpot("restricted", 36f, 5.5f, 270f);
            b.EnemySpot("restricted", 42f, 8.5f, 180f);
            b.EnemySpot("breakroom", 34.5f, 12.4f, 0f);
            b.EnemySpot("booth", 44f, 15f, 270f);
            b.EnemySpot("office", 34.5f, 30.6f, 180f);
            b.EnemySpot("office", 31.5f, 21.6f, 45f);
            b.EnemySpot("office", 43f, 30.2f, 180f);

            b.CivilianSpot("bays", 2.5f, 1.5f, 45f);
            b.CivilianSpot("aisles", 2.5f, 30.5f, 135f);
            b.CivilianSpot("aisles", 28.5f, 30.5f, 225f);
            b.CivilianSpot("breakroom", 32f, 11.6f, 45f);
            b.CivilianSpot("booth", 47f, 18.6f, 225f);
            b.CivilianSpot("restricted", 46.5f, 8.8f, 225f);
            b.CivilianSpot("restricted", 31.5f, 8.5f, 135f);
            b.CivilianSpot("office", 31.5f, 31f, 135f);
            b.CivilianSpot("manager", 40.5f, 31f, 135f);

            return b.Finish();
        }
    }
}
