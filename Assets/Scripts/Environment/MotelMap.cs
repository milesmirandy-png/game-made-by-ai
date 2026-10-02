using UnityEngine;

namespace Swat
{
    // Map: Seaview Motor Inn, a single-storey motel. Six guest rooms open
    // straight onto the parking lot, each with a bathroom at the back, plus
    // an ice/vending lounge, a laundry and a separate front office.
    //
    //  z=10            +-----+-----+-----+-----+-----+-----+---+--L--+
    //   z=9 +--------+ | B1  | B2  | B3  | B4  | B5  | B6  |Ice|Laun-|
    //       | Back   | +--D--+--D--+--D--+--D--+--D--+--D--+   | dry |
    //   z=5 +--D-----D | R1  | R2  | R3  | R4  | R5  | R6  |   |     |
    //       |Reception |     |     |     |     |     |     |   |     |
    //   z=0 +---D----+ +D----+D----+D----+D----+D----+D----+-D-+--D--+
    //     x=-14     -4 0     6     12    18    24    30    36  40    46
    public static class MotelMap
    {
        const float UnitWidth = 6f;

        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "motel", "Seaview Motor Inn");
            var stucco = new Color(0.78f, 0.7f, 0.56f);
            var inside = new Color(0.86f, 0.82f, 0.74f);
            var carpet = new Color(0.45f, 0.36f, 0.3f);
            var tile = new Color(0.74f, 0.78f, 0.8f);
            var asphalt = new Color(0.22f, 0.23f, 0.25f);

            b.Area("Seaview Motor Inn", -16f, -24f, 48f, 16f);
            b.Layout.navBounds = new Bounds(new Vector3(16f, 1f, -4f), new Vector3(70f, 6f, 46f));

            // Outside
            b.Slab("Ground", -40f, -40f, 70f, 40f, -0.2f, 0f, new Color(0.34f, 0.44f, 0.28f));
            b.Room("parking", "Motel Parking", -16f, -24f, 48f, -3f, asphalt, false);
            b.Room("alley", "Service Alley", -16f, 10.2f, 48f, 16f, asphalt, false);
            b.Slab("Walkway", -14f, -3f, 46f, 0f, -0.05f, 0.025f, new Color(0.6f, 0.58f, 0.55f));
            for (float x = 2f; x <= 34f; x += UnitWidth) b.Decal(new Vector3(x + 1.5f, 0.02f, -8f), new Vector3(0.12f, 0.01f, 4f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(3f, 0f, -7f), 0f, new Color(0.6f, 0.2f, 0.15f));
            b.Car(new Vector3(9.5f, 0f, -7f), 0f, new Color(0.25f, 0.3f, 0.45f));
            b.Car(new Vector3(27f, 0f, -7f), 0f, new Color(0.85f, 0.85f, 0.8f));
            b.Car(new Vector3(33f, 0f, -7f), 0f, new Color(0.18f, 0.18f, 0.2f));
            b.Car(new Vector3(43f, 0f, -16f), 30f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Prop("Motel Sign", new Vector3(-10f, 0f, -18f), new Vector3(0.4f, 4f, 2.6f), new Color(0.2f, 0.45f, 0.6f), false);
            b.Prop("Dumpster", new Vector3(24f, 0f, 13f), new Vector3(2.2f, 1.4f, 1.2f), new Color(0.2f, 0.35f, 0.25f), true);
            b.Lamp(new Vector3(-2f, 0f, -3.5f));
            b.Lamp(new Vector3(14f, 0f, -3.5f));
            b.Lamp(new Vector3(30f, 0f, -3.5f));
            b.Lamp(new Vector3(46.5f, 0f, -3.5f));
            b.Boundary(-16f, -24f, 48f, 16f);
            b.Arrival(new Vector3(16f, 0.05f, -10f), 0f, new Vector3(21f, 0f, -12f), 0f, new Vector3(21f, 0f, -38f));
            b.Extraction(13.5f, -13f, 18.5f, -7.5f);
            b.SafeZone(-12f, -14f, -8f, -10f);

            // Guest rooms and their bathrooms
            for (int i = 0; i < 6; i++)
            {
                float x0 = i * UnitWidth;
                b.Room("unit" + (i + 1), "Motel Room " + (i + 1), x0, 0f, x0 + UnitWidth, 7f, carpet);
                b.Room("bath" + (i + 1), "Room " + (i + 1) + " Bathroom", x0, 7f, x0 + UnitWidth, 10f, tile);
            }
            b.Room("vending", "Ice and Vending Lounge", 36f, 0f, 40f, 10f, tile);
            b.Room("laundry", "Laundry", 40f, 0f, 46f, 10f, new Color(0.55f, 0.56f, 0.57f));
            b.Room("reception", "Front Office Reception", -14f, 0f, -4f, 5f, new Color(0.7f, 0.66f, 0.6f));
            b.Room("backoffice", "Back Office", -14f, 5f, -4f, 9f, carpet);

            // Main block walls: a door per guest room facing the lot.
            var front = new Gap[8];
            for (int i = 0; i < 6; i++) front[i] = Gap.Door(i * UnitWidth + 1.6f, "unit" + (i + 1) + "_door", true, 1.4f);
            front[6] = Gap.Door(38f, "vending_door");
            front[7] = Gap.Door(43.5f, "laundry_door");
            b.WallX(0f, 0f, 46f, stucco, true, front);
            b.WallX(10f, 0f, 46f, stucco, true, Gap.Locked(43f, "laundry_back", true, true));
            b.WallZ(0f, 0f, 10f, stucco, true);
            b.WallZ(46f, 0f, 10f, stucco, true);
            for (int i = 1; i <= 6; i++) b.WallZ(i * UnitWidth, 0f, 10f, inside, false);
            b.WallZ(40f, 0f, 10f, inside, false);
            var baths = new Gap[6];
            for (int i = 0; i < 6; i++) baths[i] = Gap.Door(i * UnitWidth + 4.4f, "bath" + (i + 1) + "_door", false, 1.4f);
            b.WallX(7f, 0f, 36f, inside, false, baths);

            // Front office
            b.WallX(0f, -14f, -4f, stucco, true, Gap.Door(-9f, "office_front", false, 1.8f));
            b.WallX(9f, -14f, -4f, stucco, true);
            b.WallZ(-14f, 0f, 9f, stucco, true);
            b.WallZ(-4f, 0f, 9f, stucco, true, Gap.Door(7f, "office_side"));
            b.WallX(5f, -14f, -4f, inside, false, Gap.Door(-6.5f, "backoffice_door", true));

            // Furnish each guest room the same way, with small variations.
            for (int i = 0; i < 6; i++)
            {
                float x0 = i * UnitWidth;
                b.Bed(new Vector3(x0 + 3.8f, 0f, 3.4f), i % 2 == 0 ? 180f : 0f);
                b.Table(new Vector3(x0 + 1.2f, 0f, 5.6f), new Vector3(1f, 0.75f, 0.8f), new Color(0.5f, 0.38f, 0.26f));
                b.Prop("TV Stand", new Vector3(x0 + 5.55f, 0f, 5.8f), new Vector3(0.6f, 0.7f, 1.4f), new Color(0.3f, 0.22f, 0.15f), false);
                b.Prop("Bathtub", new Vector3(x0 + 1.2f, 0f, 8.5f), new Vector3(1.6f, 0.6f, 2.4f), new Color(0.92f, 0.92f, 0.9f), true);
                b.Prop("Sink", new Vector3(x0 + 5.5f, 0f, 9.45f), new Vector3(0.6f, 0.9f, 0.5f), new Color(0.9f, 0.9f, 0.9f), false);
            }

            // Ice/vending lounge and laundry
            b.Prop("Vending Machine", new Vector3(39.4f, 0f, 8.5f), new Vector3(0.8f, 2f, 1f), new Color(0.7f, 0.15f, 0.15f), true);
            b.Prop("Ice Machine", new Vector3(36.6f, 0f, 8.8f), new Vector3(0.9f, 1.6f, 0.9f), new Color(0.8f, 0.85f, 0.9f), true);
            foreach (float z in new[] { 2.5f, 4.5f, 6.5f })
                b.Prop("Washer", new Vector3(45.4f, 0f, z), new Vector3(0.8f, 1f, 0.8f), new Color(0.88f, 0.88f, 0.88f), true);
            b.Table(new Vector3(43.5f, 0f, 6f), new Vector3(1.2f, 0.9f, 2f), new Color(0.6f, 0.6f, 0.62f));

            // Front office
            b.Prop("Reception Counter", new Vector3(-9f, 0f, 3.4f), new Vector3(4f, 1.05f, 0.7f), new Color(0.5f, 0.38f, 0.26f), true);
            b.Couch(new Vector3(-12.6f, 0f, 1.2f), 90f, new Color(0.3f, 0.4f, 0.45f));
            b.Plant(new Vector3(-4.6f, 0f, 0.6f));
            b.Console("motel_console", "Front Desk Computer", -10.5f, 4.4f, 0f, true, true);
            b.Desk(new Vector3(-11f, 0f, 7.6f), 180f);
            b.Shelf(new Vector3(-13.4f, 0f, 6.6f), 90f, 2f);

            // Security
            b.Camera(-13.4f, 0.6f, 45f);
            b.Camera(12f, -0.6f, 180f);
            b.Camera(36f, -0.6f, 180f);
            b.Camera(45.4f, 9.4f, 225f);
            b.AlarmPanel(-13.75f, 3f, 90f);
            b.Beacon(-9f, 2f);
            b.Beacon(38f, 5f);
            b.Beacon(43f, 5f);

            b.Evidence(21.5f, 8.6f);
            b.Evidence(26f, 1.5f);
            b.Evidence(-8f, 7.8f);
            b.Evidence(33.4f, 8.4f);
            b.Escape(20f, 14f);
            b.Escape(-10f, 14f);

            // Spawn pools: two spots per room, one per bathroom.
            for (int i = 0; i < 6; i++)
            {
                float x0 = i * UnitWidth;
                string unit = "unit" + (i + 1);
                b.EnemySpot(unit, x0 + 1.5f, 3.8f, 90f);
                b.EnemySpot(unit, x0 + 4.9f, 1.2f, 0f);
                b.EnemySpot("bath" + (i + 1), x0 + 3.4f, 8.6f, 180f);
                b.CivilianSpot(unit, x0 + 3.8f, 6.2f, 225f);
                b.CivilianSpot("bath" + (i + 1), x0 + 5.2f, 8.2f, 270f);
            }
            b.EnemySpot("vending", 38f, 6f, 180f);
            b.EnemySpot("laundry", 41.2f, 2.5f, 90f, new Vector3(41.2f, 0f, 2.5f), new Vector3(41.2f, 0f, 9f));
            b.EnemySpot("reception", -6f, 2f, 270f);
            b.EnemySpot("backoffice", -8f, 7.5f, 180f);
            b.CivilianSpot("reception", -9f, 4.4f, 180f);
            b.CivilianSpot("laundry", 44.6f, 9.2f, 225f);
            b.CivilianSpot("vending", 36.8f, 1.2f, 45f);
            b.CivilianSpot("backoffice", -13.2f, 8.3f, 135f);

            return b.Finish();
        }
    }
}
