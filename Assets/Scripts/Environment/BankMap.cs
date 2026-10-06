using UnityEngine;

namespace Swat
{
    // Map: Sterling Mutual Bank, a branch with a public banking hall, teller
    // line, loan offices, a staff corridor and the secure rooms at the back.
    //
    //  z=24 +-------+-------+--------+---------+
    //       | Vault |Safe   |Security|Manager's|
    //       |       |Deposit| Room   | Office  |
    //  z=16 +----E--+---L---+--E-----+------D--+
    //       | Staff Corridor  [M]  Staff Corridor L (rear exit)
    //  z=13 +-D-----+-----------L-----+----D----+
    //       | Break |  Banking Hall   D  Loan   |
    //  (side L)Room D  (teller line)  | Offices |
    //   z=0 +-------+-------D---------+---------+
    //      x=0      8                24        34
    //
    // [M] is the mantrap: a security checkpoint across the staff corridor with two offset doors, so
    // the corridor can't be seen down end to end and the secure rooms can't be rushed. Doors that
    // used to line up across the corridor (break room / vault, teller line / security room) are
    // staggered, columns break up the banking hall and partitions split the loan offices.
    public static class BankMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "bank", "Sterling Mutual Bank");
            var stone = new Color(0.7f, 0.68f, 0.62f);
            var inside = new Color(0.86f, 0.84f, 0.8f);
            var marble = new Color(0.78f, 0.76f, 0.72f);
            var carpet = new Color(0.3f, 0.34f, 0.42f);
            var asphalt = new Color(0.23f, 0.24f, 0.26f);

            b.Area("Sterling Mutual Bank", -12f, -22f, 46f, 30f);
            b.Layout.navBounds = new Bounds(new Vector3(17f, 1f, 4f), new Vector3(64f, 6f, 58f));

            // Outside
            b.Slab("Ground", -40f, -40f, 70f, 50f, -0.2f, 0f, new Color(0.3f, 0.42f, 0.27f));
            b.Room("plaza", "Bank Plaza", -12f, -22f, 46f, -0.2f, asphalt, false);
            b.Room("rearlot", "Rear Lot", 34.2f, -0.2f, 46f, 30f, asphalt, false);
            b.Room("lane", "Service Lane", -12f, 24.2f, 34.2f, 30f, asphalt, false);
            b.Slab("Steps", 10f, -2f, 22f, 0f, -0.05f, 0.03f, new Color(0.66f, 0.64f, 0.6f));
            for (float x = -8f; x <= 6f; x += 3.6f) b.Decal(new Vector3(x, 0.02f, -15f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-6.2f, 0f, -15f), 0f, new Color(0.15f, 0.15f, 0.17f));
            b.Car(new Vector3(-2.6f, 0f, -15f), 0f, new Color(0.55f, 0.55f, 0.6f));
            b.Car(new Vector3(4.6f, 0f, -15f), 0f, new Color(0.4f, 0.12f, 0.1f));
            b.Car(new Vector3(30f, 0f, -16f), 30f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Car(new Vector3(40f, 0f, 6f), 0f, new Color(0.2f, 0.25f, 0.3f));
            b.Prop("Planter", new Vector3(11.5f, 0f, -1f), new Vector3(2f, 0.6f, 0.8f), new Color(0.55f, 0.53f, 0.5f), true);
            b.Prop("Planter", new Vector3(20.5f, 0f, -1f), new Vector3(2f, 0.6f, 0.8f), new Color(0.55f, 0.53f, 0.5f), true);
            b.Prop("Cash Machine", new Vector3(26f, 0f, -0.5f), new Vector3(1f, 1.8f, 0.6f), new Color(0.3f, 0.32f, 0.36f), false);
            b.Lamp(new Vector3(4f, 0f, -2.5f));
            b.Lamp(new Vector3(28f, 0f, -2.5f));
            b.Lamp(new Vector3(38f, 0f, 14f));
            b.Boundary(-12f, -22f, 46f, 30f);
            b.Arrival(new Vector3(16f, 0.05f, -9f), 0f, new Vector3(21f, 0f, -11f), 0f, new Vector3(21f, 0f, -36f));
            b.Extraction(13.5f, -12f, 18.5f, -6.5f);
            b.SafeZone(-6f, -9f, -2f, -5f);

            // Rooms
            b.Room("breakroom", "Break Room", 0f, 0f, 8f, 13f, new Color(0.7f, 0.66f, 0.58f));
            b.Room("hall", "Banking Hall", 8f, 0f, 24f, 13f, marble);
            b.Room("loans", "Loan Offices", 24f, 0f, 34f, 13f, carpet);
            b.Room("corridor", "Staff Corridor", 0f, 13f, 34f, 16f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("checkpoint", "Security Checkpoint", 15f, 13f, 17f, 16f, new Color(0.5f, 0.52f, 0.55f));
            b.Room("vault", "Vault", 0f, 16f, 8f, 24f, new Color(0.45f, 0.47f, 0.5f));
            b.Room("deposit", "Safe Deposit Room", 8f, 16f, 16f, 24f, new Color(0.5f, 0.5f, 0.52f));
            b.Room("security", "Security Room", 16f, 16f, 24f, 24f, new Color(0.3f, 0.33f, 0.38f));
            b.Room("manager", "Manager's Office", 24f, 16f, 34f, 24f, new Color(0.42f, 0.3f, 0.22f));

            // Walls
            b.WallX(0f, 0f, 34f, stone, true, Gap.Door(16f, "front", false, 2.4f));
            b.WallX(24f, 0f, 34f, stone, true);
            b.WallZ(0f, 0f, 24f, stone, true, Gap.Locked(6f, "break_side", true, true));
            b.WallZ(34f, 0f, 24f, stone, true, Gap.Locked(14.5f, "rear_exit", true, true));
            b.WallZ(8f, 0f, 13f, inside, false, Gap.Door(6.5f, "break_hall", true));
            b.WallZ(24f, 0f, 13f, inside, false, Gap.Door(11.2f, "hall_loans", true));
            b.WallX(13f, 0f, 34f, inside, false, Gap.Door(2.2f, "break_corr"), Gap.Locked(21.5f, "teller_door", true, true), Gap.Door(29f, "loans_corr", true));
            b.WallX(16f, 0f, 34f, inside, false, Gap.Electronic(5.2f, "vault_door"), Gap.Locked(12f, "deposit_door", true, true), Gap.Electronic(18.6f, "security_door"), Gap.Door(31.4f, "manager_door", true));
            // The mantrap: a door near the south wall on the west side, near the north wall on the east.
            b.WallZ(15f, 13f, 16f, inside, false, Gap.Door(13.8f, "checkpoint_west", true, 1.2f));
            b.WallZ(17f, 13f, 16f, inside, false, Gap.Door(15.2f, "checkpoint_east", true, 1.2f));
            b.WallZ(8f, 16f, 24f, inside, false);
            b.WallZ(16f, 16f, 24f, inside, false);
            b.WallZ(24f, 16f, 24f, inside, false);

            // Banking hall: public side, teller counter, staff side.
            b.Prop("Teller Counter", new Vector3(16f, 0f, 9f), new Vector3(12f, 1.1f, 0.7f), new Color(0.45f, 0.32f, 0.22f), true);
            b.Decal(new Vector3(16f, 1.6f, 9f), new Vector3(12f, 0.9f, 0.04f), new Color(0.6f, 0.75f, 0.85f), 0.2f);
            b.Prop("Teller Desk", new Vector3(16f, 0f, 11.8f), new Vector3(6f, 0.8f, 0.6f), new Color(0.5f, 0.38f, 0.26f), true);
            b.Prop("Queue Rail", new Vector3(16f, 0f, 5.5f), new Vector3(4f, 0.9f, 0.08f), new Color(0.75f, 0.65f, 0.3f), false);
            b.Couch(new Vector3(11f, 0f, 1.3f), 0f, new Color(0.25f, 0.3f, 0.4f));
            b.Couch(new Vector3(21f, 0f, 1.3f), 0f, new Color(0.25f, 0.3f, 0.4f));
            b.Plant(new Vector3(8.6f, 0f, 0.6f));
            b.Plant(new Vector3(23.4f, 0f, 0.6f));
            b.Pillar(11f, 6.5f, 0.7f);
            b.Pillar(21f, 6.5f, 0.7f);
            b.Console("teller_terminal", "Teller Terminal", 11f, 12.4f, 0f, true, false);

            // Break room
            b.Table(new Vector3(4f, 0f, 5f), new Vector3(2.4f, 0.75f, 1.2f), new Color(0.75f, 0.72f, 0.66f));
            b.Prop("Vending Machine", new Vector3(7.4f, 0f, 1f), new Vector3(0.8f, 2f, 1f), new Color(0.2f, 0.4f, 0.7f), true);
            b.Couch(new Vector3(1.2f, 0f, 9.5f), 90f, new Color(0.35f, 0.3f, 0.25f));

            // Loan offices: a partition splits the desks from the waiting side, with a gap by the hall door.
            b.Desk(new Vector3(28f, 0f, 3.2f), 0f);
            b.Desk(new Vector3(32f, 0f, 3.2f), 0f);
            b.Desk(new Vector3(29.5f, 0f, 10.5f), 180f);
            b.Partition(26.6f, 7f, 34f, 7f);
            b.Partition(30f, 2f, 30f, 6.9f, 1.5f);
            b.Partition(25.4f, 9.6f, 25.4f, 12.7f);
            b.Shelf(new Vector3(33.6f, 0f, 9.5f), 90f, 2f);
            b.Couch(new Vector3(27.4f, 0f, 8.6f), 0f, new Color(0.25f, 0.3f, 0.4f));

            // Vault and safe deposit
            b.Prop("Deposit Boxes", new Vector3(0.5f, 0f, 20f), new Vector3(0.8f, 2.2f, 6f), new Color(0.6f, 0.62f, 0.66f), true);
            b.Prop("Deposit Boxes", new Vector3(4.5f, 0f, 23.5f), new Vector3(5f, 2.2f, 0.8f), new Color(0.6f, 0.62f, 0.66f), true);
            b.Prop("Cash Cart", new Vector3(5.5f, 0f, 19f), new Vector3(1.2f, 1f, 0.8f), new Color(0.5f, 0.52f, 0.55f), true);
            b.Prop("Deposit Lockers", new Vector3(8.5f, 0f, 20f), new Vector3(0.8f, 2f, 5f), new Color(0.55f, 0.57f, 0.6f), true);
            b.Prop("Deposit Lockers", new Vector3(15.5f, 0f, 20f), new Vector3(0.8f, 2f, 5f), new Color(0.55f, 0.57f, 0.6f), true);
            b.Table(new Vector3(12f, 0f, 21f), new Vector3(1.6f, 0.9f, 0.8f), new Color(0.4f, 0.4f, 0.42f));

            // Security room
            b.Console("security_console", "Security Console", 20f, 23.4f, 0f, true, true);
            b.Prop("Server Rack", new Vector3(23.4f, 0f, 18f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);
            b.Prop("Server Rack", new Vector3(23.4f, 0f, 19.2f), new Vector3(0.8f, 2.2f, 0.8f), new Color(0.12f, 0.12f, 0.14f), true);

            // Manager's office
            b.Desk(new Vector3(29f, 0f, 21f), 180f, 2f);
            b.Couch(new Vector3(32.5f, 0f, 19.5f), 0f, new Color(0.35f, 0.2f, 0.15f));
            b.Prop("Safe", new Vector3(33.4f, 0f, 23.2f), new Vector3(0.8f, 1f, 0.8f), new Color(0.25f, 0.25f, 0.27f), true);

            // Security
            b.Camera(8.6f, 0.6f, 45f);
            b.Camera(23.4f, 0.6f, 315f);
            b.Camera(0.6f, 14.5f, 90f);
            b.Camera(33.4f, 14.5f, 270f);
            b.Camera(16f, -0.6f, 180f);
            b.AlarmPanel(8.25f, 6f, 90f);
            b.Beacon(16f, 6f);
            b.Beacon(19.5f, 14.5f);
            b.Beacon(4f, 20f);

            b.Evidence(3f, 20.5f);
            b.Evidence(12f, 18.5f);
            b.Evidence(26f, 22.5f);
            b.Evidence(18f, 18.5f);
            b.Escape(40f, 14.5f);
            b.Escape(-6f, 6f);

            // Suspect spawn pool
            b.EnemySpot("hall", 12f, 4f, 0f);
            b.EnemySpot("hall", 20.5f, 3.5f, 0f);
            b.EnemySpot("hall", 16f, 7f, 180f, new Vector3(12f, 0f, 7f), new Vector3(20f, 0f, 7f));
            b.EnemySpot("hall", 14.5f, 10.4f, 180f);
            b.EnemySpot("hall", 21.5f, 11.2f, 270f);
            b.EnemySpot("loans", 28f, 5.2f, 180f);
            b.EnemySpot("loans", 32f, 11.5f, 180f);
            b.EnemySpot("loans", 25.5f, 6.2f, 90f);
            b.EnemySpot("breakroom", 3f, 3f, 0f);
            b.EnemySpot("breakroom", 6f, 11f, 180f);
            b.EnemySpot("corridor", 6f, 14.5f, 90f, new Vector3(6f, 0f, 14.5f), new Vector3(14f, 0f, 14.5f));
            b.EnemySpot("corridor", 30f, 14.5f, 270f, new Vector3(30f, 0f, 14.5f), new Vector3(18f, 0f, 14.5f));
            b.EnemySpot("checkpoint", 16f, 14.5f, 0f);
            b.EnemySpot("vault", 3f, 21.5f, 90f);
            b.EnemySpot("vault", 6.5f, 17.5f, 0f);
            b.EnemySpot("deposit", 12f, 22.8f, 180f);
            b.EnemySpot("security", 18.5f, 21f, 90f);
            b.EnemySpot("manager", 26.5f, 19f, 0f);
            b.EnemySpot("manager", 31.5f, 22.8f, 180f);

            // Civilian spawn pool
            b.CivilianSpot("hall", 9.4f, 2.5f, 45f);
            b.CivilianSpot("hall", 22.6f, 2.8f, 315f);
            b.CivilianSpot("hall", 13.5f, 7.6f, 90f);
            b.CivilianSpot("hall", 18.5f, 7.8f, 270f);
            b.CivilianSpot("hall", 16.5f, 10.2f, 180f);
            b.CivilianSpot("hall", 19.5f, 10.2f, 180f);
            b.CivilianSpot("loans", 25.2f, 1.2f, 45f);
            b.CivilianSpot("loans", 31.8f, 1.2f, 0f);
            b.CivilianSpot("loans", 32.8f, 12.2f, 225f);
            b.CivilianSpot("breakroom", 1.2f, 1.2f, 45f);
            b.CivilianSpot("vault", 7.2f, 22.4f, 225f);
            b.CivilianSpot("deposit", 14.4f, 17f, 0f);
            b.CivilianSpot("manager", 25.2f, 23.2f, 135f);

            return b.Finish();
        }
    }
}
