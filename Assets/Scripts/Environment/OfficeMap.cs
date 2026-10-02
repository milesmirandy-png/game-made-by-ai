using UnityEngine;

namespace Swat
{
    // Map 1: Halvorsen Logistics office complex.
    //
    //  z=26 +-------+-----+------------+---------+---------+
    //       |Storage|Rest-| Conference |Security | Manager |
    //  z=13 +---D---+--D--+--D------D--+---E-----+----D----+
    //       |                  Hallway                     D (emergency exit)
    //  z=10 +--   --+------    ---------+----D---+----D----+
    //       |Recep- D      Open Office  D Office A D Off. B |
    //   z=0 +--D----+-----------------------------+---------+
    //      x=0     12                    26       31        36
    public static class OfficeMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "office", "Halvorsen Logistics Offices");
            var outside = new Color(0.6f, 0.57f, 0.52f);
            var inside = new Color(0.86f, 0.84f, 0.79f);
            var carpet = new Color(0.36f, 0.42f, 0.52f);
            var asphalt = new Color(0.23f, 0.24f, 0.26f);

            b.Area("Halvorsen Logistics", -14f, -26f, 50f, 32f);
            b.Layout.navBounds = new Bounds(new Vector3(18f, 1f, 3f), new Vector3(70f, 6f, 62f));

            // Outside
            b.Slab("Ground", -40f, -40f, 80f, 50f, -0.2f, 0f, new Color(0.32f, 0.44f, 0.27f));
            b.Room("parking", "Parking Lot", -12f, -24f, 50f, -2f, asphalt, false);
            b.Room("eastlot", "East Lot", 36.2f, -2f, 48f, 26f, asphalt, false);
            b.Room("alley", "Back Alley", -2f, 26.2f, 38f, 31f, asphalt, false);
            b.Slab("Sidewalk", -2f, -2f, 36.2f, 0f, -0.05f, 0.02f, new Color(0.58f, 0.58f, 0.56f));
            for (float x = -6f; x <= 34f; x += 3.6f) b.Decal(new Vector3(x, 0.02f, -15f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-4.2f, 0f, -15f), 0f, new Color(0.7f, 0.15f, 0.12f));
            b.Car(new Vector3(-0.6f, 0f, -15f), 0f, new Color(0.2f, 0.3f, 0.55f));
            b.Car(new Vector3(20f, 0f, -15f), 0f, new Color(0.8f, 0.8f, 0.78f));
            b.Car(new Vector3(23.6f, 0f, -15f), 0f, new Color(0.15f, 0.15f, 0.16f));
            b.Car(new Vector3(30.8f, 0f, -15f), 0f, new Color(0.45f, 0.5f, 0.35f));
            b.Car(new Vector3(2.5f, 0f, -6.5f), 20f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Car(new Vector3(42f, 0f, 4f), 90f, new Color(0.35f, 0.3f, 0.25f));
            b.Lamp(new Vector3(-1f, 0f, -3f));
            b.Lamp(new Vector3(16f, 0f, -3f));
            b.Lamp(new Vector3(37.5f, 0f, -3f));
            b.Boundary(-12f, -24f, 48f, 31f);
            b.Arrival(new Vector3(7f, 0.05f, -8f), 0f, new Vector3(11.5f, 0f, -9f), 0f, new Vector3(11.5f, 0f, -36f));
            b.Extraction(5.2f, -11f, 9.8f, -5.5f);
            b.SafeZone(-2.5f, -13f, 1.5f, -9f);

            // Rooms
            b.Room("lobby", "Reception", 0f, 0f, 12f, 10f, new Color(0.72f, 0.7f, 0.66f));
            b.Room("openoffice", "Open Office", 12f, 0f, 26f, 10f, carpet);
            b.Room("officeA", "Office A", 26f, 0f, 31f, 10f, carpet);
            b.Room("officeB", "Office B", 31f, 0f, 36f, 10f, carpet);
            b.Room("hallway", "Hallway", 0f, 10f, 36f, 13f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("storage", "Storage Room", 0f, 13f, 8f, 26f, new Color(0.48f, 0.48f, 0.47f));
            b.Room("restroom", "Restrooms", 8f, 13f, 13f, 26f, new Color(0.75f, 0.8f, 0.82f));
            b.Room("conference", "Conference Room", 13f, 13f, 23f, 26f, new Color(0.45f, 0.4f, 0.36f));
            b.Room("security", "Security Room", 23f, 13f, 30f, 26f, new Color(0.3f, 0.33f, 0.38f));
            b.Room("manager", "Manager's Office", 30f, 13f, 36f, 26f, new Color(0.42f, 0.3f, 0.22f));

            // Outside walls
            b.WallX(0f, 0f, 36f, outside, true, Gap.Door(6f, "front", false, 2.2f));
            b.WallX(26f, 0f, 36f, outside, true, Gap.Locked(4f, "back_door", true, true));
            b.WallZ(0f, 0f, 26f, outside, true);
            b.WallZ(36f, 0f, 26f, outside, true, Gap.Door(11.5f, "emergency_exit"));

            // Inside walls
            b.WallZ(12f, 0f, 10f, inside, false, Gap.Door(5f, "lobby_open", false, 1.8f));
            b.WallZ(26f, 0f, 10f, inside, false, Gap.Door(7f, "open_officeA", true));
            b.WallZ(31f, 0f, 10f, inside, false, Gap.Door(3f, "officeA_officeB", true));
            b.WallX(10f, 0f, 12f, inside, false, Gap.Open(6f));
            b.WallX(10f, 12f, 26f, inside, false, Gap.Open(19f));
            b.WallX(10f, 26f, 31f, inside, false, Gap.Door(28.5f, "officeA_hall", true));
            b.WallX(10f, 31f, 36f, inside, false, Gap.Locked(33.5f, "officeB_hall", true, false));
            b.WallX(13f, 0f, 8f, inside, false, Gap.Locked(4f, "storage_hall", true, true));
            b.WallX(13f, 8f, 13f, inside, false, Gap.Door(10.5f, "restroom_hall", false, 1.4f));
            b.WallX(13f, 13f, 23f, inside, false, Gap.Door(15f, "conf_west", true), Gap.Door(21f, "conf_east", true));
            b.WallX(13f, 23f, 30f, inside, false, Gap.Electronic(26.5f, "security_door"));
            b.WallX(13f, 30f, 36f, inside, false, Gap.Door(33f, "manager_hall", true));
            b.WallZ(8f, 13f, 26f, inside, false);
            b.WallZ(13f, 13f, 26f, inside, false);
            b.WallZ(23f, 13f, 26f, inside, false, Gap.Door(20f, "conf_security", true));
            b.WallZ(30f, 13f, 26f, inside, false);

            // Reception
            b.Couch(new Vector3(10f, 0f, 2.2f), 0f, new Color(0.3f, 0.36f, 0.45f));
            b.Table(new Vector3(10f, 0f, 3.4f), new Vector3(1.2f, 0.45f, 0.6f), new Color(0.4f, 0.28f, 0.18f));
            b.Plant(new Vector3(0.8f, 0f, 0.8f));
            b.Plant(new Vector3(11.2f, 0f, 0.8f));
            b.Plant(new Vector3(0.8f, 0f, 9.2f));
            b.Console("front_desk", "Front Desk Terminal", 3.2f, 6.8f, 180f, true, false);

            // Open office
            foreach (float x in new[] { 15f, 18.5f, 22f })
            {
                b.Desk(new Vector3(x, 0f, 3f), 0f);
                b.Desk(new Vector3(x, 0f, 7f), 180f);
            }
            b.Prop("Printer", new Vector3(25.5f, 0f, 1f), new Vector3(0.8f, 1.2f, 0.6f), new Color(0.75f, 0.75f, 0.72f), false);

            // Offices
            b.Desk(new Vector3(28.5f, 0f, 4f), 180f);
            b.Shelf(new Vector3(30.6f, 0f, 8f), 90f, 1.6f);
            b.Desk(new Vector3(33.5f, 0f, 5f), 180f);
            b.Shelf(new Vector3(35.6f, 0f, 2f), 90f, 1.6f);

            // Hallway
            b.Prop("Water Cooler", new Vector3(24.5f, 0f, 12.6f), new Vector3(0.4f, 1.2f, 0.4f), new Color(0.75f, 0.85f, 0.95f), false);

            // Storage
            b.Shelf(new Vector3(1.5f, 0f, 16.5f), 90f, 3f, 2.2f);
            b.Shelf(new Vector3(1.5f, 0f, 22f), 90f, 3f, 2.2f);
            b.Shelf(new Vector3(6.5f, 0f, 23.5f), 0f, 2.5f, 2.2f);
            b.Crate(new Vector3(5.5f, 0f, 17f), 1.2f, false);
            b.Crate(new Vector3(6.5f, 0f, 20f), 1f, true);

            // Restrooms
            foreach (float z in new[] { 17.5f, 20.5f, 23.5f })
                b.Prop("Stall Divider", new Vector3(12.1f, 0f, z), new Vector3(1.8f, 1.4f, 0.08f), new Color(0.6f, 0.65f, 0.7f), false);
            b.Prop("Sinks", new Vector3(8.5f, 0f, 15f), new Vector3(0.5f, 0.9f, 2.5f), new Color(0.9f, 0.9f, 0.9f), false);

            // Conference
            b.Table(new Vector3(18f, 0f, 19.5f), new Vector3(5f, 0.8f, 2.2f), new Color(0.35f, 0.25f, 0.17f));
            b.Decal(new Vector3(18f, 1.1f, 25.85f), new Vector3(3f, 1.4f, 0.06f), new Color(0.3f, 0.5f, 0.8f), 1f);

            // Security room
            b.Console("security_console", "Security Console", 26.5f, 24.8f, 0f, true, true);
            b.Prop("Server Rack", new Vector3(29.3f, 0f, 22f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Prop("Server Rack", new Vector3(29.3f, 0f, 20.5f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Prop("Lockers", new Vector3(23.6f, 0f, 16f), new Vector3(0.6f, 2f, 2.4f), new Color(0.4f, 0.45f, 0.5f), true);

            // Manager's office
            b.Desk(new Vector3(33f, 0f, 21f), 180f, 2f);
            b.Couch(new Vector3(33f, 0f, 25f), 0f, new Color(0.35f, 0.2f, 0.15f));
            b.Shelf(new Vector3(35.6f, 0f, 17f), 90f, 1.6f);

            // Security
            b.Camera(0.6f, 9.4f, 45f);
            b.Camera(0.6f, 12.4f, 90f);
            b.Camera(35.4f, 12.4f, 270f);
            b.Camera(9f, -0.6f, 180f);
            b.AlarmPanel(0.25f, 11.5f, 90f);
            b.Beacon(6f, 11.5f);
            b.Beacon(18f, 11.5f);
            b.Beacon(30f, 11.5f);
            b.Beacon(6f, 5f);

            // Evidence spots and escape routes
            b.Evidence(6.5f, 18.5f);
            b.Evidence(24.5f, 18f);
            b.Evidence(34f, 18f);
            b.Evidence(16f, 24f);
            b.Escape(4f, 29f);
            b.Escape(44f, 11.5f);

            // Suspect spawn pool
            b.EnemySpot("lobby", 5f, 3.5f, 0f, new Vector3(5f, 0f, 3.5f), new Vector3(10f, 0f, 6f), new Vector3(4f, 0f, 8.5f));
            b.EnemySpot("lobby", 9f, 8.5f, 180f);
            b.EnemySpot("openoffice", 16.5f, 5f, 90f, new Vector3(16.5f, 0f, 5f), new Vector3(24f, 0f, 5f));
            b.EnemySpot("openoffice", 20.5f, 8.8f, 180f);
            b.EnemySpot("openoffice", 24.3f, 2.2f, 270f);
            b.EnemySpot("officeA", 28.5f, 7.5f, 270f);
            b.EnemySpot("officeB", 33.5f, 7.8f, 180f);
            b.EnemySpot("officeB", 34.5f, 2.5f, 0f);
            b.EnemySpot("hallway", 3f, 11.5f, 90f, new Vector3(3f, 0f, 11.5f), new Vector3(33f, 0f, 11.5f));
            b.EnemySpot("hallway", 31f, 11.5f, 270f, new Vector3(31f, 0f, 11.5f), new Vector3(8f, 0f, 11.5f));
            b.EnemySpot("storage", 4f, 15f, 0f, new Vector3(4f, 0f, 15f), new Vector3(3.5f, 0f, 24f));
            b.EnemySpot("storage", 6.5f, 21.8f, 180f);
            b.EnemySpot("restroom", 9.8f, 19f, 0f);
            b.EnemySpot("conference", 18f, 16.5f, 0f);
            b.EnemySpot("conference", 14.5f, 22.5f, 90f);
            b.EnemySpot("conference", 21.5f, 23.5f, 180f);
            b.EnemySpot("security", 26.5f, 21f, 180f);
            b.EnemySpot("security", 24.6f, 15.5f, 90f, new Vector3(24.6f, 0f, 15.5f), new Vector3(28.5f, 0f, 15.5f));
            b.EnemySpot("manager", 33f, 23f, 180f);
            b.EnemySpot("manager", 31.5f, 16f, 90f);

            // Civilian spawn pool
            b.CivilianSpot("lobby", 11.3f, 0.9f, 180f);
            b.CivilianSpot("lobby", 1.5f, 2f, 45f);
            b.CivilianSpot("openoffice", 14f, 8.6f, 135f);
            b.CivilianSpot("openoffice", 23.5f, 5f, 270f);
            b.CivilianSpot("openoffice", 17.5f, 1.2f, 0f);
            b.CivilianSpot("officeA", 27f, 1.2f, 45f);
            b.CivilianSpot("officeB", 35f, 8.8f, 225f);
            b.CivilianSpot("storage", 2.8f, 25f, 135f);
            b.CivilianSpot("restroom", 12.2f, 25f, 180f);
            b.CivilianSpot("conference", 16.5f, 21.6f, 90f);
            b.CivilianSpot("conference", 19.5f, 21.6f, 270f);
            b.CivilianSpot("conference", 21.8f, 16f, 0f);
            b.CivilianSpot("security", 28.6f, 23.8f, 200f);
            b.CivilianSpot("manager", 35f, 24.2f, 225f);

            return b.Finish();
        }
    }
}
