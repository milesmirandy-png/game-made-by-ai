using UnityEngine;

namespace Swat
{
    // Map 3: Marlow Court apartments. Floors are built side by side in the
    // world (ground floor at x 0-30, second floor at x 50-80, roof above it)
    // and connected by stairwells that move the team between them.
    //
    //  Ground floor                Second floor               Roof (z 38-54)
    //  +----+----+----------+     +----+----+-----------+
    //  | 1B | 1C |Maintenanc|     | 2D | 2E |Roof Access|--stairs up
    //  +-D--+-D--+----L-----+     +-D--+-D--+-----D-----+
    //  D        Corridor    |     |       Corridor      |
    //  +-D--+--  -+--S-+-D--+     +-D--+-L--+-S--+--D---+
    //  | 1A |Lobby|Stair|Lau|     | 2A | 2B |Stair| 2C  |
    //  +----+--D--+-----+-L-+     +----+----+-----+-----+
    public static class ApartmentMap
    {
        const float Floor2 = 50f;

        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "apartment", "Marlow Court Apartments");
            var brick = new Color(0.5f, 0.3f, 0.24f);
            var inside = new Color(0.8f, 0.75f, 0.66f);
            var wood = new Color(0.45f, 0.32f, 0.2f);
            var tile = new Color(0.6f, 0.6f, 0.58f);

            b.Layout.navBounds = new Bounds(new Vector3(36f, 1f, 18f), new Vector3(110f, 6f, 90f));

            // ---- Ground floor and street ----
            b.Area("Ground Floor", -12f, -22f, 42f, 26f);
            b.Slab("Ground", -30f, -40f, 44f, 40f, -0.2f, 0f, new Color(0.28f, 0.34f, 0.25f));
            b.Room("street", "Street", -12f, -22f, 42f, -0.2f, new Color(0.22f, 0.22f, 0.24f), false);
            b.Room("alley", "Side Alley", -12f, -0.2f, -0.2f, 22f, new Color(0.25f, 0.25f, 0.27f), false);
            for (float x = -10f; x <= 40f; x += 5f) b.Decal(new Vector3(x, 0.02f, -11f), new Vector3(2.5f, 0.01f, 0.15f), new Color(0.9f, 0.85f, 0.3f));
            b.Car(new Vector3(4f, 0f, -5f), 90f, new Color(0.5f, 0.15f, 0.12f));
            b.Car(new Vector3(28f, 0f, -5f), 90f, new Color(0.2f, 0.22f, 0.25f));
            b.Car(new Vector3(20f, 0f, -16f), 260f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Lamp(new Vector3(10f, 0f, -2f));
            b.Lamp(new Vector3(24f, 0f, -2f));
            b.Lamp(new Vector3(-6f, 0f, 9f));
            b.Boundary(-12f, -22f, 42f, 24f);
            b.Arrival(new Vector3(15f, 0.05f, -9f), 0f, new Vector3(9f, 0f, -14f), 90f, new Vector3(-30f, 0f, -14f));
            b.Extraction(12.5f, -13.5f, 17.5f, -7f);
            b.SafeZone(30f, -14f, 35f, -9f);

            b.Room("unit1a", "Apartment 1A", 0f, 0f, 10f, 8f, wood);
            b.Room("lobby", "Lobby", 10f, 0f, 20f, 8f, tile);
            b.Room("stairs1", "Stairwell", 20f, 0f, 24f, 8f, new Color(0.5f, 0.5f, 0.5f));
            b.Room("laundry", "Laundry Room", 24f, 0f, 30f, 8f, tile);
            b.Room("corridor1", "Ground Floor Corridor", 0f, 8f, 30f, 11f, new Color(0.55f, 0.45f, 0.4f));
            b.Room("unit1b", "Apartment 1B", 0f, 11f, 10f, 20f, wood);
            b.Room("unit1c", "Apartment 1C", 10f, 11f, 20f, 20f, wood);
            b.Room("maintenance", "Maintenance Room", 20f, 11f, 30f, 20f, new Color(0.42f, 0.42f, 0.42f));

            b.WallX(0f, 0f, 30f, brick, true, Gap.Door(15f, "main_entrance", false, 2f), Gap.Locked(27f, "laundry_back", true, true));
            b.WallX(20f, 0f, 30f, brick, true);
            b.WallZ(0f, 0f, 20f, brick, true, Gap.Door(9.5f, "fire_exit"));
            b.WallZ(30f, 0f, 20f, brick, true);
            b.WallX(8f, 0f, 10f, inside, false, Gap.Door(5f, "unit1a_door", true));
            b.WallX(8f, 10f, 20f, inside, false, Gap.Open(15f));
            b.WallX(8f, 20f, 24f, inside, false, Gap.Open(22f, 2f));
            b.WallX(8f, 24f, 30f, inside, false, Gap.Door(27f, "laundry_door"));
            b.WallZ(10f, 0f, 8f, inside, false);
            b.WallZ(20f, 0f, 8f, inside, false, Gap.Door(4f, "lobby_stairs"));
            b.WallZ(24f, 0f, 8f, inside, false);
            b.WallX(11f, 0f, 10f, inside, false, Gap.Door(5f, "unit1b_door", true));
            b.WallX(11f, 10f, 20f, inside, false, Gap.Door(15f, "unit1c_door", true));
            b.WallX(11f, 20f, 30f, inside, false, Gap.Locked(25f, "maintenance_door", true, true));
            b.WallZ(10f, 11f, 20f, inside, false);
            b.WallZ(20f, 11f, 20f, inside, false);

            Unit(b, 0f, 0f, 10f, 8f, true);
            Unit(b, 0f, 11f, 10f, 20f, false);
            Unit(b, 10f, 11f, 20f, 20f, false);
            b.Couch(new Vector3(17.5f, 0f, 1.2f), 180f, new Color(0.4f, 0.3f, 0.25f));
            b.Prop("Mailboxes", new Vector3(10.4f, 0f, 4.5f), new Vector3(0.4f, 1.4f, 2f), new Color(0.6f, 0.55f, 0.4f), false);
            b.Plant(new Vector3(19.2f, 0f, 7.2f));
            foreach (float z in new[] { 2f, 3.3f, 4.6f })
                b.Prop("Washer", new Vector3(29.4f, 0f, z), new Vector3(0.9f, 1f, 1.1f), new Color(0.9f, 0.9f, 0.92f), true);
            b.Shelf(new Vector3(21f, 0f, 16f), 90f, 3f);
            b.Prop("Boiler", new Vector3(28.5f, 0f, 18.5f), new Vector3(1.6f, 2.2f, 1.6f), new Color(0.5f, 0.35f, 0.25f), true);
            b.Crate(new Vector3(25f, 0f, 14f), 1f, true);
            Stairs(b, 22f, 3f, "Take the stairs up to the second floor", new Vector3(Floor2 + 22f, 0.05f, 5f), 1);

            // ---- Second floor ----
            b.Area("Second Floor", Floor2 - 4f, -4f, Floor2 + 34f, 24f);
            b.Room("unit2a", "Apartment 2A", Floor2, 0f, Floor2 + 10f, 8f, wood);
            b.Room("unit2b", "Apartment 2B", Floor2 + 10f, 0f, Floor2 + 20f, 8f, wood);
            b.Room("stairs2", "Upper Stairwell", Floor2 + 20f, 0f, Floor2 + 24f, 8f, new Color(0.5f, 0.5f, 0.5f));
            b.Room("unit2c", "Apartment 2C", Floor2 + 24f, 0f, Floor2 + 30f, 8f, wood);
            b.Room("corridor2", "Second Floor Corridor", Floor2, 8f, Floor2 + 30f, 11f, new Color(0.55f, 0.45f, 0.4f));
            b.Room("unit2d", "Apartment 2D", Floor2, 11f, Floor2 + 10f, 20f, wood);
            b.Room("unit2e", "Apartment 2E", Floor2 + 10f, 11f, Floor2 + 20f, 20f, wood);
            b.Room("roofaccess", "Roof Access", Floor2 + 20f, 11f, Floor2 + 30f, 20f, new Color(0.42f, 0.42f, 0.42f));

            b.WallX(0f, Floor2, Floor2 + 30f, brick, true);
            b.WallX(20f, Floor2, Floor2 + 30f, brick, true);
            b.WallZ(Floor2, 0f, 20f, brick, true);
            b.WallZ(Floor2 + 30f, 0f, 20f, brick, true);
            b.WallX(8f, Floor2, Floor2 + 10f, inside, false, Gap.Door(Floor2 + 5f, "unit2a_door", true));
            b.WallX(8f, Floor2 + 10f, Floor2 + 20f, inside, false, Gap.Locked(Floor2 + 15f, "unit2b_door", true, true));
            b.WallX(8f, Floor2 + 20f, Floor2 + 24f, inside, false, Gap.Open(Floor2 + 22f, 2f));
            b.WallX(8f, Floor2 + 24f, Floor2 + 30f, inside, false, Gap.Door(Floor2 + 27f, "unit2c_door", true));
            b.WallZ(Floor2 + 10f, 0f, 8f, inside, false);
            b.WallZ(Floor2 + 20f, 0f, 8f, inside, false);
            b.WallZ(Floor2 + 24f, 0f, 8f, inside, false);
            b.WallX(11f, Floor2, Floor2 + 10f, inside, false, Gap.Door(Floor2 + 5f, "unit2d_door", true));
            b.WallX(11f, Floor2 + 10f, Floor2 + 20f, inside, false, Gap.Door(Floor2 + 15f, "unit2e_door", true));
            b.WallX(11f, Floor2 + 20f, Floor2 + 30f, inside, false, Gap.Door(Floor2 + 25f, "roof_access_door"));
            b.WallZ(Floor2 + 10f, 11f, 20f, inside, false);
            b.WallZ(Floor2 + 20f, 11f, 20f, inside, false);

            Unit(b, Floor2, 0f, Floor2 + 10f, 8f, true);
            Unit(b, Floor2 + 10f, 0f, Floor2 + 20f, 8f, true);
            Unit(b, Floor2, 11f, Floor2 + 10f, 20f, false);
            Unit(b, Floor2 + 10f, 11f, Floor2 + 20f, 20f, false);
            b.Bed(new Vector3(Floor2 + 28.6f, 0f, 1.5f), 0f);
            b.Crate(new Vector3(Floor2 + 22f, 0f, 18.5f), 1f, true);
            b.Crate(new Vector3(Floor2 + 27f, 0f, 13f), 1f, false);
            Stairs(b, Floor2 + 22f, 3f, "Take the stairs down to the ground floor", new Vector3(22f, 0.05f, 5f), 0);
            Stairs(b, Floor2 + 28f, 17.5f, "Climb up to the roof", new Vector3(Floor2 + 15f, 0.05f, 42f), 2);

            // ---- Roof ----
            b.Area("Roof", Floor2 - 4f, 34f, Floor2 + 34f, 58f);
            b.Room("roof", "Rooftop", Floor2, 38f, Floor2 + 30f, 54f, new Color(0.35f, 0.35f, 0.37f), false);
            b.WallX(38f, Floor2, Floor2 + 30f, brick, true);
            b.WallX(54f, Floor2, Floor2 + 30f, brick, true);
            b.WallZ(Floor2, 38f, 54f, brick, true);
            b.WallZ(Floor2 + 30f, 38f, 54f, brick, true);
            b.Prop("AC Unit", new Vector3(Floor2 + 8f, 0f, 46f), new Vector3(2.5f, 1.4f, 2f), new Color(0.6f, 0.62f, 0.65f), true);
            b.Prop("AC Unit", new Vector3(Floor2 + 20f, 0f, 49f), new Vector3(2.5f, 1.4f, 2f), new Color(0.6f, 0.62f, 0.65f), true);
            b.Prop("Shed", new Vector3(Floor2 + 25f, 0f, 42f), new Vector3(3f, 2.2f, 2.5f), new Color(0.4f, 0.35f, 0.3f), true);
            Shapes.Make(PrimitiveType.Cylinder, "Water Tank", b.Props, new Vector3(Floor2 + 4f, 1.2f, 51f), new Vector3(2.2f, 1.2f, 2.2f), new Color(0.5f, 0.45f, 0.4f));
            Stairs(b, Floor2 + 15f, 40f, "Go back inside", new Vector3(Floor2 + 26f, 0.05f, 15f), 1);

            // Security and escape routes
            b.Camera(10.6f, 7.4f, 135f);
            b.Camera(Floor2 + 0.6f, 10.4f, 90f);
            b.AlarmPanel(10.25f, 2f, 90f);
            b.Beacon(15f, 9.5f);
            b.Beacon(Floor2 + 15f, 9.5f);
            b.Escape(-5f, 9.5f);
            b.Escape(Floor2 + 29f, 19f);
            b.Escape(Floor2 + 29f, 53f);

            // Suspects
            b.EnemySpot("ground", 5f, 5.6f, 0f);
            b.EnemySpot("ground", 13f, 3f, 90f);
            b.EnemySpot("ground", 3f, 9.5f, 90f, new Vector3(3f, 0f, 9.5f), new Vector3(28f, 0f, 9.5f));
            b.EnemySpot("ground", 6f, 14.2f, 180f);
            b.EnemySpot("ground", 15f, 16f, 180f);
            b.EnemySpot("ground", 24f, 17f, 180f);
            b.EnemySpot("ground", 26f, 4f, 270f);
            b.EnemySpot("upper", Floor2 + 5f, 4f, 0f);
            b.EnemySpot("upper", Floor2 + 15f, 4.5f, 0f);
            b.EnemySpot("upper", Floor2 + 17f, 6f, 270f);
            b.EnemySpot("upper", Floor2 + 2f, 9.5f, 90f, new Vector3(Floor2 + 2f, 0f, 9.5f), new Vector3(Floor2 + 28f, 0f, 9.5f));
            b.EnemySpot("upper", Floor2 + 6f, 16f, 180f);
            b.EnemySpot("upper", Floor2 + 15f, 15f, 180f);
            b.EnemySpot("upper", Floor2 + 25f, 15.5f, 180f);
            b.EnemySpot("upper", Floor2 + 10f, 45f, 90f);
            b.EnemySpot("upper", Floor2 + 22f, 51.5f, 180f);

            // Residents
            b.CivilianSpot("ground", 2.5f, 6.5f, 45f);
            b.CivilianSpot("ground", 3f, 18f, 135f);
            b.CivilianSpot("ground", 13.2f, 18.6f, 135f);
            b.CivilianSpot("ground", 25f, 1.6f, 45f);
            b.CivilianSpot("ground", 18.5f, 6.5f, 225f);
            b.CivilianSpot("upper", Floor2 + 2.5f, 3f, 45f);
            b.CivilianSpot("upper", Floor2 + 26.2f, 4.5f, 225f);
            b.CivilianSpot("upper", Floor2 + 3f, 18.6f, 135f);
            b.CivilianSpot("upper", Floor2 + 16f, 17.5f, 225f);
            b.CivilianSpot("upper", Floor2 + 28.6f, 12.5f, 225f);

            return b.Finish();
        }

        static void Stairs(LevelBuilder b, float x, float z, string label, Vector3 destination, int area)
        {
            b.Stairs(x, z, label, destination, 0f, area);
        }

        // Furnishes one apartment. South units have their door on the north wall, north units on the south wall.
        static void Unit(LevelBuilder b, float x0, float z0, float x1, float z1, bool doorNorth)
        {
            float back = doorNorth ? z0 : z1;
            float sign = doorNorth ? 1f : -1f;
            b.Bed(new Vector3(x0 + 1.2f, 0f, back + sign * 1.3f), doorNorth ? 180f : 0f);
            b.Couch(new Vector3(x1 - 1.6f, 0f, back + sign * 1.2f), doorNorth ? 180f : 0f, new Color(0.35f, 0.4f, 0.5f));
            b.Table(new Vector3(x0 + 3.4f, 0f, back + sign * 4.2f), new Vector3(1.2f, 0.75f, 0.8f), new Color(0.55f, 0.42f, 0.3f));
            b.Prop("Kitchen Counter", new Vector3(x1 - 0.4f, 0f, back + sign * 4.3f), new Vector3(0.6f, 0.95f, 2f), new Color(0.7f, 0.7f, 0.72f), true);
        }
    }
}
