using UnityEngine;

namespace Swat
{
    // Map: Harbor Street Clinic, a walk-in medical clinic with wards, exam
    // rooms and a pharmacy off the main corridor.
    //
    //  z=26 +------+------+-------+-------+--------+
    //       |Ward A|Ward B|Exam 1 |Exam 2 |Pharmacy|
    //  z=17 +--D---+--D-[MED]-D---+--D----+---L----+
    //  (L)  |       Main Corridor   S (smoke doors)   D (ambulance bay)
    //  z=13 +--D-[LIN]+---  ---+-D-[UTL]--+---D----+
    //       | Waiting   Recep- D Records D  Staff  |
    //       | Room    | tion   |       |  Lounge  |
    //   z=0 +--D------+--------+-------+----------+
    //      x=0       10       18      26          36
    //
    // The linen closet, the medication room and the utility closet jut into the corridor so it
    // zig-zags instead of running 36 m straight, and smoke doors close it off in the middle. The wards
    // have curtains between the beds.
    public static class ClinicMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "clinic", "Harbor Street Clinic");
            var outside = new Color(0.8f, 0.8f, 0.78f);
            var inside = new Color(0.9f, 0.92f, 0.92f);
            var tile = new Color(0.82f, 0.86f, 0.86f);
            var asphalt = new Color(0.23f, 0.24f, 0.26f);

            b.Area("Harbor Street Clinic", -12f, -22f, 48f, 30f);
            b.Layout.navBounds = new Bounds(new Vector3(18f, 1f, 4f), new Vector3(66f, 6f, 58f));

            // Outside
            b.Slab("Ground", -40f, -40f, 70f, 50f, -0.2f, 0f, new Color(0.3f, 0.43f, 0.27f));
            b.Room("parking", "Clinic Parking", -12f, -22f, 48f, -0.2f, asphalt, false);
            b.Room("bay", "Ambulance Bay", 36.2f, -0.2f, 48f, 30f, asphalt, false);
            b.Room("rear", "Rear Path", -12f, 26.2f, 36.2f, 30f, asphalt, false);
            for (float x = -8f; x <= 6f; x += 3.6f) b.Decal(new Vector3(x, 0.02f, -15f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-6f, 0f, -15f), 0f, new Color(0.3f, 0.35f, 0.5f));
            b.Car(new Vector3(-2.4f, 0f, -15f), 0f, new Color(0.7f, 0.7f, 0.68f));
            b.Car(new Vector3(26f, 0f, -15f), 0f, new Color(0.55f, 0.15f, 0.12f));
            b.Car(new Vector3(32f, 0f, -17f), 30f, new Color(0.95f, 0.95f, 0.95f), true);
            var ambulance = b.Prop("Ambulance", new Vector3(41f, 0f, 6f), new Vector3(2.2f, 2.4f, 5.6f), new Color(0.95f, 0.95f, 0.93f), true);
            Shapes.Box("Stripe", ambulance.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.01f, 0.12f, 1.01f), new Color(0.85f, 0.15f, 0.12f), false);
            b.Decal(new Vector3(41f, 0.02f, 15.5f), new Vector3(8f, 0.01f, 4f), new Color(0.9f, 0.75f, 0.15f));
            b.Lamp(new Vector3(2f, 0f, -2.5f));
            b.Lamp(new Vector3(24f, 0f, -2.5f));
            b.Lamp(new Vector3(38f, 0f, 12f));
            b.Boundary(-12f, -22f, 48f, 30f);
            b.Arrival(new Vector3(14f, 0.05f, -9f), 0f, new Vector3(19f, 0f, -11f), 0f, new Vector3(19f, 0f, -36f));
            b.Extraction(11.5f, -12f, 16.5f, -6.5f);
            b.SafeZone(-7f, -9f, -3f, -5f);

            // Rooms
            b.Room("waiting", "Waiting Room", 0f, 0f, 10f, 13f, tile);
            b.Room("reception", "Reception", 10f, 0f, 18f, 13f, tile);
            b.Room("records", "Records Office", 18f, 0f, 26f, 13f, new Color(0.38f, 0.42f, 0.5f));
            b.Room("lounge", "Staff Lounge", 26f, 0f, 36f, 13f, new Color(0.7f, 0.64f, 0.55f));
            b.Room("corridor", "Main Corridor", 0f, 13f, 36f, 17f, tile);
            b.Room("linen_storage", "Linen Closet", 6.5f, 13f, 9.5f, 15f, tile);
            b.Room("med_storage", "Medication Room", 12.2f, 15f, 15.8f, 17f, tile);
            b.Room("utility", "Utility Closet", 23f, 13f, 26f, 15f, new Color(0.6f, 0.62f, 0.62f));
            b.Room("wardA", "Ward A", 0f, 17f, 7f, 26f, tile);
            b.Room("wardB", "Ward B", 7f, 17f, 14f, 26f, tile);
            b.Room("exam1", "Exam Room 1", 14f, 17f, 21f, 26f, tile);
            b.Room("exam2", "Exam Room 2", 21f, 17f, 28f, 26f, tile);
            b.Room("pharmacy", "Pharmacy", 28f, 17f, 36f, 26f, tile);

            // Walls
            b.WallX(0f, 0f, 36f, outside, true, Gap.Door(5f, "front", false, 2.2f));
            b.WallX(26f, 0f, 36f, outside, true);
            b.WallZ(0f, 0f, 26f, outside, true, Gap.Locked(21.5f, "ward_exit", true, true));
            b.WallZ(36f, 0f, 26f, outside, true, Gap.Door(15.5f, "ambulance_bay", false, 2.2f));
            b.WallZ(10f, 0f, 13f, inside, false, Gap.Open(7f, 3f));
            b.WallZ(18f, 0f, 13f, inside, false, Gap.Door(10.5f, "records_door", true));
            b.WallZ(26f, 0f, 13f, inside, false, Gap.Door(4f, "lounge_records"));
            b.WallX(13f, 0f, 36f, inside, false, Gap.Door(4.6f, "waiting_corr"), Gap.Door(8f, "linen_door", false, 1.2f), Gap.Open(14f, 2.4f), Gap.Door(20.2f, "records_corr", true),
                Gap.Door(25.2f, "utility_door", false, 1.2f), Gap.Door(31f, "lounge_corr"));
            // Closets jutting into the corridor, and the smoke doors.
            b.WallX(15f, 6.5f, 9.5f, inside, false);
            b.WallZ(6.5f, 13f, 15f, inside, false);
            b.WallZ(9.5f, 13f, 15f, inside, false);
            b.WallX(15f, 12.2f, 15.8f, inside, false, Gap.Door(14f, "med_door", true, 1.2f));
            b.WallZ(12.2f, 15f, 17f, inside, false);
            b.WallZ(15.8f, 15f, 17f, inside, false);
            b.WallX(15f, 23f, 26f, inside, false);
            b.WallZ(23f, 13f, 15f, inside, false);
            b.WallZ(26f, 13f, 15f, inside, false);
            b.WallZ(19f, 13f, 17f, inside, false, Gap.Door(15f, "smoke_doors", false, 2.2f));
            b.WallX(17f, 0f, 36f, inside, false, Gap.Door(3.5f, "wardA_door", true), Gap.Door(10.5f, "wardB_door", true), Gap.Door(17.5f, "exam1_door", true),
                Gap.Door(24.5f, "exam2_door", true), Gap.Locked(32f, "pharmacy_door", true, true));
            b.WallZ(7f, 17f, 26f, inside, false);
            b.WallZ(14f, 17f, 26f, inside, false);
            b.WallZ(21f, 17f, 26f, inside, false);
            b.WallZ(28f, 17f, 26f, inside, false);

            // Waiting room and reception
            b.Prop("Waiting Chairs", new Vector3(0.5f, 0f, 7f), new Vector3(0.6f, 0.5f, 6f), new Color(0.3f, 0.45f, 0.55f), false);
            b.Prop("Waiting Chairs", new Vector3(8f, 0f, 9.5f), new Vector3(3f, 0.5f, 0.6f), new Color(0.3f, 0.45f, 0.55f), false);
            b.Table(new Vector3(4.5f, 0f, 7f), new Vector3(1.2f, 0.45f, 0.6f), new Color(0.6f, 0.5f, 0.38f));
            b.Plant(new Vector3(9.4f, 0f, 0.6f));
            b.Prop("Room Divider", new Vector3(5.5f, 0f, 5.2f), new Vector3(3.2f, 1.6f, 0.3f), new Color(0.55f, 0.62f, 0.66f), true);
            b.Prop("Reception Counter", new Vector3(14f, 0f, 9.5f), new Vector3(5f, 1.1f, 0.7f), new Color(0.75f, 0.75f, 0.78f), true);
            b.Console("clinic_console", "Reception Computer", 12.5f, 12.4f, 0f, true, true);

            // Records office
            b.Desk(new Vector3(21f, 0f, 3f), 0f);
            b.Desk(new Vector3(23.5f, 0f, 9.5f), 180f);
            b.Shelf(new Vector3(25.4f, 0f, 9f), 90f, 3f);

            // Staff lounge
            b.Table(new Vector3(31f, 0f, 7f), new Vector3(2.4f, 0.75f, 1.2f), new Color(0.75f, 0.72f, 0.66f));
            b.Couch(new Vector3(35.2f, 0f, 10f), 90f, new Color(0.35f, 0.4f, 0.45f));
            b.Prop("Vending Machine", new Vector3(35.4f, 0f, 2f), new Vector3(0.8f, 2f, 1f), new Color(0.2f, 0.5f, 0.4f), true);

            // Corridor and closets
            b.Prop("Gurney", new Vector3(27.6f, 0f, 16.4f), new Vector3(2f, 0.8f, 0.6f), new Color(0.8f, 0.82f, 0.85f), false);
            b.Prop("Wheelchair", new Vector3(5.8f, 0f, 16.4f), new Vector3(0.6f, 0.9f, 0.6f), new Color(0.3f, 0.32f, 0.36f), false);
            b.Shelf(new Vector3(7f, 0f, 14.1f), 90f, 1.6f, 1.8f);
            b.Prop("Medicine Cabinet", new Vector3(15.3f, 0f, 16f), new Vector3(0.6f, 1.8f, 1.4f), new Color(0.85f, 0.88f, 0.9f), true);
            b.Prop("Water Heater", new Vector3(23.6f, 0f, 14.4f), new Vector3(0.7f, 1.6f, 0.7f), new Color(0.7f, 0.72f, 0.7f), true);

            // Wards and exam rooms
            foreach (float x in new[] { 2f, 5f, 9f, 12f }) b.Bed(new Vector3(x, 0f, 23.5f), 0f);
            // Privacy curtains between the beds: somewhere to hide, and nobody sees the whole ward at once.
            var curtain = new Color(0.55f, 0.72f, 0.75f);
            b.Partition(3.5f, 21.4f, 3.5f, 25.9f, 1.8f, curtain);
            b.Partition(10.5f, 21.4f, 10.5f, 25.9f, 1.8f, curtain);
            b.Prop("Exam Table", new Vector3(17.5f, 0f, 22.5f), new Vector3(0.8f, 0.9f, 2f), new Color(0.75f, 0.8f, 0.85f), true);
            b.Prop("Medical Cabinet", new Vector3(20.4f, 0f, 25.4f), new Vector3(0.8f, 1.8f, 0.6f), new Color(0.85f, 0.88f, 0.9f), true);
            b.Prop("Exam Table", new Vector3(24.5f, 0f, 22.5f), new Vector3(0.8f, 0.9f, 2f), new Color(0.75f, 0.8f, 0.85f), true);
            b.Prop("Medical Cabinet", new Vector3(27.4f, 0f, 25.4f), new Vector3(0.8f, 1.8f, 0.6f), new Color(0.85f, 0.88f, 0.9f), true);

            // Pharmacy
            b.Shelf(new Vector3(35.4f, 0f, 21.5f), 90f, 5f);
            b.Shelf(new Vector3(31f, 0f, 25.4f), 0f, 4f);
            b.Prop("Pharmacy Counter", new Vector3(31f, 0f, 19.5f), new Vector3(4f, 1.05f, 0.7f), new Color(0.75f, 0.75f, 0.78f), true);

            // Security
            b.Camera(0.6f, 12.4f, 45f);
            b.Camera(35.4f, 15.5f, 270f);
            b.Camera(18f, -0.6f, 180f);
            b.Camera(28.6f, 25.4f, 135f);
            b.AlarmPanel(17.75f, 4f, 270f);
            b.Beacon(4f, 15.5f);
            b.Beacon(30f, 15f);
            b.Beacon(14f, 5f);

            b.Evidence(33.5f, 22.5f);
            b.Evidence(29.2f, 24.2f);
            b.Evidence(21f, 6f);
            b.Evidence(26.5f, 19f);
            b.Escape(-6f, 21.5f);
            b.Escape(44f, 15.5f);

            // Suspect spawn pool
            b.EnemySpot("waiting", 3f, 3f, 0f);
            b.EnemySpot("waiting", 7.5f, 11f, 180f);
            b.EnemySpot("reception", 14f, 11.2f, 180f);
            b.EnemySpot("reception", 12f, 3f, 0f);
            b.EnemySpot("records", 21.5f, 11f, 180f);
            b.EnemySpot("records", 24.5f, 2f, 0f);
            b.EnemySpot("lounge", 29f, 4f, 0f);
            b.EnemySpot("lounge", 33.5f, 11.5f, 180f);
            b.EnemySpot("corridor", 3f, 15f, 90f, new Vector3(3f, 0f, 15f), new Vector3(17.5f, 0f, 14f));
            b.EnemySpot("corridor", 33f, 15f, 270f, new Vector3(33f, 0f, 15f), new Vector3(21f, 0f, 16f));
            b.EnemySpot("med_storage", 13.2f, 16.2f, 90f);
            b.EnemySpot("utility", 24.8f, 14.2f, 270f);
            b.EnemySpot("wardA", 3.5f, 20f, 0f);
            b.EnemySpot("wardB", 10.5f, 20.5f, 0f);
            b.EnemySpot("exam1", 19.5f, 20.5f, 270f);
            b.EnemySpot("exam2", 22.8f, 24.5f, 90f);
            b.EnemySpot("pharmacy", 30.5f, 22f, 0f);
            b.EnemySpot("pharmacy", 34f, 18.5f, 0f);

            // Civilian spawn pool
            b.CivilianSpot("waiting", 1.4f, 1.2f, 45f);
            b.CivilianSpot("waiting", 8.8f, 1.2f, 315f);
            b.CivilianSpot("waiting", 2.2f, 12f, 135f);
            b.CivilianSpot("reception", 17f, 12f, 225f);
            b.CivilianSpot("records", 19.2f, 11.6f, 135f);
            b.CivilianSpot("linen_storage", 8.8f, 14.3f, 270f);
            b.CivilianSpot("lounge", 34.6f, 5.5f, 270f);
            b.CivilianSpot("wardA", 2f, 21f, 0f);
            b.CivilianSpot("wardA", 5.8f, 19f, 315f);
            b.CivilianSpot("wardB", 8.2f, 19f, 45f);
            b.CivilianSpot("wardB", 12.8f, 21f, 315f);
            b.CivilianSpot("exam1", 15f, 25f, 135f);
            b.CivilianSpot("exam2", 22.2f, 18.2f, 45f);
            b.CivilianSpot("pharmacy", 29f, 21.5f, 90f);

            return b.Finish();
        }
    }
}
