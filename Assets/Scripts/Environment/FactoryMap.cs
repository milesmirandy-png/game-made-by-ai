using UnityEngine;

namespace Swat
{
    // Map: Riverside Steelworks, a large closed factory (the final level).
    //
    //  z=36 +---L------+------------------+--------+--------+
    //       | Boiler   D                  |Foreman's| Control|
    //       | Room     |  Assembly Floor  D Office  | Room   |
    //  z=24 +---D------+                  +---D-----+---E----+
    //  (D)  | Tool     D                  D  Upper Corridor  D
    //       | Shop     |                  |                  |
    //  z=12 +---D------+---  -----  ------+---D-----+---D----+
    //       |  Loading  (open) Production Floor     D Locker |
    //       |  Dock     |                           |  Room  |
    //   z=0 +--(roll door)-----+-----D--------------+--------+
    //      x=0         18     (front)              40       52
    public static class FactoryMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "factory", "Riverside Steelworks");
            b.ExteriorWall = SurfaceKind.Concrete;
            var outside = new Color(0.46f, 0.47f, 0.48f);
            var inside = new Color(0.6f, 0.6f, 0.58f);
            var concrete = new Color(0.44f, 0.44f, 0.43f);
            var asphalt = new Color(0.21f, 0.22f, 0.24f);

            b.Area("Riverside Steelworks", -12f, -22f, 64f, 44f);
            b.Layout.navBounds = new Bounds(new Vector3(26f, 1f, 11f), new Vector3(82f, 6f, 72f));

            // Outside
            b.Slab("Ground", -40f, -40f, 90f, 60f, -0.2f, 0f, new Color(0.3f, 0.37f, 0.26f));
            b.Room("yard", "Factory Yard", -12f, -22f, 64f, -0.2f, asphalt, false);
            b.Room("westlane", "West Lane", -12f, -0.2f, -0.2f, 36.2f, asphalt, false);
            b.Room("eastlane", "East Lane", 52.2f, -0.2f, 64f, 36.2f, asphalt, false);
            b.Room("riverpath", "River Path", -12f, 36.2f, 64f, 44f, asphalt, false);
            b.Slab("River", -40f, 44.5f, 90f, 60f, -0.25f, -0.05f, new Color(0.16f, 0.24f, 0.3f));
            for (float x = 3f; x <= 15f; x += 4f) b.Decal(new Vector3(x, 0.02f, -2.5f), new Vector3(0.2f, 0.01f, 4f), new Color(1f, 0.8f, 0.1f));
            b.Truck(new Vector3(9f, 0f, -7f), 0f);
            b.Prop("Container", new Vector3(-4f, 0f, -12f), new Vector3(2.5f, 2.6f, 6f), new Color(0.6f, 0.22f, 0.15f), true);
            b.Prop("Container", new Vector3(48f, 0f, -14f), new Vector3(6f, 2.6f, 2.5f), new Color(0.2f, 0.35f, 0.55f), true);
            b.Car(new Vector3(42f, 0f, -6f), 90f, new Color(0.3f, 0.3f, 0.32f));
            b.Car(new Vector3(24f, 0f, -17f), 20f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Lamp(new Vector3(2f, 0f, -2.5f));
            b.Lamp(new Vector3(22f, 0f, -2.5f));
            b.Lamp(new Vector3(44f, 0f, -2.5f));
            b.Lamp(new Vector3(-6f, 0f, 20f));
            b.Lamp(new Vector3(58f, 0f, 20f));
            b.Lamp(new Vector3(20f, 0f, 40f));
            b.Boundary(-12f, -22f, 64f, 44f);
            b.Arrival(new Vector3(29f, 0.05f, -9f), 0f, new Vector3(34f, 0f, -11f), 0f, new Vector3(34f, 0f, -36f));
            b.Extraction(26.5f, -12f, 31.5f, -6.5f);
            b.SafeZone(40f, -16f, 44f, -12f);

            // Rooms
            b.Room("dock", "Loading Dock", 0f, 0f, 18f, 12f, concrete);
            b.Room("production", "Production Floor", 18f, 0f, 40f, 12f, concrete);
            b.Room("lockers", "Locker Room", 40f, 0f, 52f, 12f, new Color(0.55f, 0.58f, 0.6f));
            b.Room("toolshop", "Tool Shop", 0f, 12f, 14f, 24f, concrete);
            b.Room("boiler", "Boiler Room", 0f, 24f, 14f, 36f, new Color(0.38f, 0.37f, 0.36f));
            b.Room("assembly", "Assembly Floor", 14f, 12f, 34f, 36f, concrete);
            b.Room("corridor", "Upper Corridor", 34f, 12f, 52f, 18f, new Color(0.56f, 0.56f, 0.58f));
            b.Room("foreman", "Foreman's Office", 34f, 18f, 43f, 36f, new Color(0.38f, 0.42f, 0.5f));
            b.Room("control", "Control Room", 43f, 18f, 52f, 36f, new Color(0.3f, 0.33f, 0.38f));

            // Walls
            b.WallX(0f, 0f, 52f, outside, true, Gap.Open(9f, 5f), Gap.Door(29f, "front", false, 2.2f));
            b.WallX(36f, 0f, 52f, outside, true, Gap.Locked(7f, "boiler_exit", true, true));
            b.WallZ(0f, 0f, 36f, outside, true, Gap.Door(18f, "toolshop_side"));
            b.WallZ(52f, 0f, 36f, outside, true, Gap.Door(15f, "corridor_east"));
            b.WallZ(18f, 0f, 12f, inside, false, Gap.Open(6f, 3f));
            b.WallZ(40f, 0f, 12f, inside, false, Gap.Door(6f, "locker_door", true));
            b.WallX(12f, 0f, 52f, inside, false, Gap.Door(7f, "dock_tool", true), Gap.Open(16f, 3f), Gap.Open(26f, 4f), Gap.Door(37f, "prod_corr"), Gap.Door(46f, "locker_corr", true));
            b.WallZ(14f, 12f, 36f, inside, false, Gap.Door(18f, "tool_assembly"), Gap.Door(30f, "boiler_assembly", true));
            b.WallX(24f, 0f, 14f, inside, false, Gap.Door(7f, "tool_boiler", true));
            b.WallZ(34f, 12f, 36f, inside, false, Gap.Door(15f, "assembly_corr"), Gap.Door(27f, "assembly_foreman", true));
            b.WallX(18f, 34f, 52f, inside, false, Gap.Door(38.5f, "foreman_door"), Gap.Electronic(47.5f, "control_door"));
            b.WallZ(43f, 18f, 36f, inside, false);

            // Loading dock
            b.Crate(new Vector3(2.5f, 0f, 9.5f), 1.2f, true);
            b.Crate(new Vector3(15f, 0f, 9.5f), 1.2f, false);
            b.Crate(new Vector3(15.5f, 0f, 3f), 1.2f, true);
            b.Prop("Pallet", new Vector3(4f, 0f, 3.5f), new Vector3(1.2f, 0.3f, 1.2f), new Color(0.55f, 0.42f, 0.25f), false);
            b.Prop("Forklift", new Vector3(12f, 0f, 6f), new Vector3(1.2f, 1.8f, 2.2f), new Color(0.95f, 0.7f, 0.1f), true, 90f);

            // Production floor: machines in two rows and a conveyor.
            foreach (float x in new[] { 22f, 28f, 34f })
            {
                b.Prop("Press Machine", new Vector3(x, 0f, 3.5f), new Vector3(2f, 2.2f, 2f), new Color(0.35f, 0.38f, 0.42f), true);
                b.Prop("Press Machine", new Vector3(x, 0f, 9f), new Vector3(2f, 2.2f, 2f), new Color(0.35f, 0.38f, 0.42f), true);
            }
            b.Prop("Conveyor", new Vector3(28f, 0f, 6.25f), new Vector3(10f, 0.9f, 0.8f), new Color(0.25f, 0.26f, 0.28f), true);

            // Locker room
            b.Prop("Lockers", new Vector3(51.4f, 0f, 5f), new Vector3(0.6f, 2f, 6f), new Color(0.4f, 0.45f, 0.5f), true);
            b.Prop("Lockers", new Vector3(50f, 0f, 0.6f), new Vector3(3f, 2f, 0.6f), new Color(0.4f, 0.45f, 0.5f), true);
            b.Prop("Bench", new Vector3(46f, 0f, 5f), new Vector3(3f, 0.45f, 0.5f), new Color(0.5f, 0.38f, 0.26f), false);

            // Tool shop
            b.Prop("Workbench", new Vector3(3f, 0f, 21f), new Vector3(3f, 0.9f, 1f), new Color(0.5f, 0.38f, 0.26f), true);
            b.Prop("Workbench", new Vector3(10f, 0f, 15f), new Vector3(3f, 0.9f, 1f), new Color(0.5f, 0.38f, 0.26f), true);
            b.Shelf(new Vector3(0.6f, 0f, 15f), 90f, 3f);

            // Boiler room
            b.Prop("Boiler Tank", new Vector3(4f, 0f, 31f), new Vector3(3f, 2.5f, 3f), new Color(0.45f, 0.3f, 0.22f), true);
            b.Prop("Boiler Tank", new Vector3(10f, 0f, 33f), new Vector3(2.5f, 2.4f, 2.5f), new Color(0.45f, 0.3f, 0.22f), true);
            b.Prop("Pipe Rack", new Vector3(12.8f, 0f, 27f), new Vector3(0.6f, 2f, 3f), new Color(0.5f, 0.5f, 0.52f), true);

            // Assembly floor: steel frames, crates and a parked crane cart.
            b.Prop("Steel Frame", new Vector3(20f, 0f, 20f), new Vector3(4f, 1.6f, 1.2f), new Color(0.4f, 0.32f, 0.28f), true);
            b.Prop("Steel Frame", new Vector3(28f, 0f, 20f), new Vector3(4f, 1.6f, 1.2f), new Color(0.4f, 0.32f, 0.28f), true);
            b.Prop("Steel Frame", new Vector3(24f, 0f, 28f), new Vector3(1.2f, 1.6f, 4f), new Color(0.4f, 0.32f, 0.28f), true);
            b.Crate(new Vector3(17f, 0f, 33f), 1.2f, true);
            b.Crate(new Vector3(31f, 0f, 33.5f), 1.2f, false);
            b.Crate(new Vector3(30.5f, 0f, 24f), 1f, true);
            b.Prop("Crane Cart", new Vector3(18f, 0f, 26f), new Vector3(1.6f, 1.4f, 2.4f), new Color(0.9f, 0.65f, 0.1f), true);

            // Foreman's office and control room
            b.Desk(new Vector3(38.5f, 0f, 30f), 180f);
            b.Desk(new Vector3(37f, 0f, 22f), 0f);
            b.Shelf(new Vector3(42.4f, 0f, 26f), 90f, 2f);
            b.Console("foreman_terminal", "Foreman Terminal", 40.5f, 35.4f, 0f, true, false);
            b.Console("plant_console", "Plant Control Console", 47.5f, 35.4f, 0f, true, true);
            b.Prop("Server Rack", new Vector3(51.4f, 0f, 24f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Prop("Server Rack", new Vector3(51.4f, 0f, 25.2f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Table(new Vector3(47.5f, 0f, 27f), new Vector3(2f, 0.8f, 1.2f), new Color(0.35f, 0.35f, 0.38f));

            // Security
            b.Camera(0.6f, 11.4f, 45f);
            b.Camera(39.4f, 0.6f, 315f);
            b.Camera(34.6f, 35.4f, 135f);
            b.Camera(51.4f, 12.6f, 225f);
            b.Camera(29f, -0.6f, 180f);
            b.AlarmPanel(39.75f, 10f, 270f);
            b.Beacon(9f, 6f);
            b.Beacon(29f, 6f);
            b.Beacon(24f, 24f);
            b.Beacon(43f, 15f);

            b.Evidence(49f, 31f);
            b.Evidence(36.5f, 33f);
            b.Evidence(7f, 27.5f);
            b.Evidence(49.5f, 2.5f);
            b.Evidence(22f, 33f);
            b.Escape(7f, 41f);
            b.Escape(58f, 15f);

            // Suspect spawn pool
            b.EnemySpot("dock", 5f, 6.5f, 90f, new Vector3(5f, 0f, 6.5f), new Vector3(9f, 0f, 9f), new Vector3(9f, 0f, 2f));
            b.EnemySpot("dock", 14f, 7.5f, 270f);
            b.EnemySpot("dock", 2.5f, 1.5f, 45f);
            b.EnemySpot("production", 20f, 6.25f, 90f, new Vector3(20f, 0f, 1.2f), new Vector3(37f, 0f, 1.2f), new Vector3(37f, 0f, 11f), new Vector3(20f, 0f, 11f));
            b.EnemySpot("production", 31f, 11f, 180f);
            b.EnemySpot("production", 38f, 4.5f, 270f);
            b.EnemySpot("lockers", 43f, 3f, 0f);
            b.EnemySpot("lockers", 49f, 9f, 180f);
            b.EnemySpot("toolshop", 4f, 15f, 0f);
            b.EnemySpot("toolshop", 10.5f, 21.5f, 180f);
            b.EnemySpot("boiler", 7.5f, 27f, 0f);
            b.EnemySpot("boiler", 11f, 30f, 270f);
            b.EnemySpot("assembly", 18f, 15f, 0f);
            b.EnemySpot("assembly", 30f, 16f, 270f);
            b.EnemySpot("assembly", 20f, 31f, 90f);
            b.EnemySpot("assembly", 28f, 31f, 180f);
            b.EnemySpot("assembly", 24f, 23f, 0f, new Vector3(17f, 0f, 23f), new Vector3(29.2f, 0f, 23f));
            b.EnemySpot("corridor", 37f, 15f, 90f, new Vector3(37f, 0f, 15f), new Vector3(50f, 0f, 15f));
            b.EnemySpot("foreman", 38f, 25f, 90f);
            b.EnemySpot("foreman", 40.5f, 33f, 180f);
            b.EnemySpot("control", 46f, 22f, 0f);
            b.EnemySpot("control", 50f, 31f, 180f);

            // Civilian spawn pool
            b.CivilianSpot("dock", 1.2f, 11f, 135f);
            b.CivilianSpot("production", 39f, 1.2f, 315f);
            b.CivilianSpot("production", 19.2f, 11f, 135f);
            b.CivilianSpot("lockers", 41f, 1f, 45f);
            b.CivilianSpot("toolshop", 1.2f, 23f, 135f);
            b.CivilianSpot("boiler", 1.5f, 26f, 45f);
            b.CivilianSpot("boiler", 12.5f, 34.8f, 225f);
            b.CivilianSpot("assembly", 15.2f, 35f, 135f);
            b.CivilianSpot("assembly", 33f, 13f, 315f);
            b.CivilianSpot("foreman", 42f, 35f, 225f);
            b.CivilianSpot("control", 44f, 34.8f, 135f);

            return b.Finish();
        }
    }
}
