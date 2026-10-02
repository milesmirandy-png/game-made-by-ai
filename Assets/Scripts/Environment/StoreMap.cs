using UnityEngine;

namespace Swat
{
    // Map: Brightwater Corner Mart, a small convenience store (the first level).
    //
    //  z=15 +--L----+-----+------+
    //       | Stock |Off- | Rest-|
    //       | Room  |ice  | room |
    //  z=10 +--D----+--D--+--D---+
    //       |      Back Hall     D (side exit)
    //   z=8 +----  ------+-------+
    //       |  Shop      D Walk-in|
    //       |  Floor     | Cooler |
    //   z=0 +--D---------+--------+
    //      x=0          14       20
    public static class StoreMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "store", "Brightwater Corner Mart");
            var outside = new Color(0.72f, 0.66f, 0.56f);
            var inside = new Color(0.88f, 0.87f, 0.83f);
            var asphalt = new Color(0.22f, 0.23f, 0.25f);

            b.Area("Brightwater Corner Mart", -12f, -22f, 30f, 20f);
            b.Layout.navBounds = new Bounds(new Vector3(9f, 1f, -1f), new Vector3(46f, 6f, 46f));

            // Outside
            b.Slab("Ground", -30f, -40f, 50f, 40f, -0.2f, 0f, new Color(0.3f, 0.42f, 0.26f));
            b.Room("parking", "Parking Lot", -12f, -22f, 30f, -2f, asphalt, false);
            b.Room("sidelot", "Side Lot", 20.2f, -2f, 30f, 20f, asphalt, false);
            b.Room("alley", "Back Alley", -12f, 15.2f, 20.2f, 20f, asphalt, false);
            b.Slab("Sidewalk", -2f, -2f, 20.2f, 0f, -0.05f, 0.02f, new Color(0.58f, 0.58f, 0.56f));
            for (float x = -8f; x <= 4f; x += 3.6f) b.Decal(new Vector3(x, 0.02f, -14f), new Vector3(0.12f, 0.01f, 4.5f), new Color(0.9f, 0.9f, 0.88f));
            b.Car(new Vector3(-6.2f, 0f, -14f), 0f, new Color(0.35f, 0.5f, 0.6f));
            b.Car(new Vector3(-2.6f, 0f, -14f), 0f, new Color(0.75f, 0.7f, 0.2f));
            b.Car(new Vector3(18f, 0f, -16f), 0f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Car(new Vector3(25f, 0f, 6f), 0f, new Color(0.3f, 0.12f, 0.12f));
            b.Prop("Dumpster", new Vector3(12f, 0f, 17.5f), new Vector3(2.2f, 1.4f, 1.2f), new Color(0.2f, 0.35f, 0.25f), true);
            b.Prop("Ice Chest", new Vector3(1.5f, 0f, -1f), new Vector3(1.4f, 1f, 0.7f), new Color(0.85f, 0.9f, 0.95f), false);
            b.Lamp(new Vector3(-1f, 0f, -3f));
            b.Lamp(new Vector3(21f, 0f, -3f));
            b.Lamp(new Vector3(21f, 0f, 16f));
            b.Boundary(-12f, -22f, 30f, 20f);
            b.Arrival(new Vector3(6f, 0.05f, -8f), 0f, new Vector3(11f, 0f, -10f), 0f, new Vector3(11f, 0f, -36f));
            b.Extraction(3.5f, -11f, 8.5f, -5.5f);
            b.SafeZone(-6f, -8f, -2f, -4f);

            // Rooms
            b.Room("shop", "Shop Floor", 0f, 0f, 14f, 8f, new Color(0.82f, 0.82f, 0.8f));
            b.Room("cooler", "Walk-in Cooler", 14f, 0f, 20f, 8f, new Color(0.62f, 0.66f, 0.7f));
            b.Room("backhall", "Back Hall", 0f, 8f, 20f, 10f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("stock", "Stock Room", 0f, 10f, 7f, 15f, new Color(0.48f, 0.48f, 0.47f));
            b.Room("office", "Office", 7f, 10f, 13f, 15f, new Color(0.4f, 0.42f, 0.5f));
            b.Room("restroom", "Restroom", 13f, 10f, 20f, 15f, new Color(0.75f, 0.8f, 0.82f));

            // Walls
            b.WallX(0f, 0f, 20f, outside, true, Gap.Door(5f, "front", false, 2.2f));
            b.WallX(15f, 0f, 20f, outside, true, Gap.Locked(3.5f, "stock_back", true, true));
            b.WallZ(0f, 0f, 15f, outside, true);
            b.WallZ(20f, 0f, 15f, outside, true, Gap.Door(9f, "side_exit"));
            b.WallX(8f, 0f, 20f, inside, false, Gap.Open(11f, 2.4f));
            b.WallZ(14f, 0f, 8f, inside, false, Gap.Door(4f, "cooler_door", true));
            b.WallX(10f, 0f, 20f, inside, false, Gap.Door(3.5f, "stock_door", true), Gap.Door(10f, "office_door", true), Gap.Door(16.5f, "restroom_door", false, 1.4f));
            b.WallZ(7f, 10f, 15f, inside, false);
            b.WallZ(13f, 10f, 15f, inside, false);

            // Shop floor: three aisles and the counter.
            foreach (float x in new[] { 2.5f, 6f, 9.5f }) b.Shelf(new Vector3(x, 0f, 4.5f), 90f, 4f, 1.6f);
            b.Prop("Counter", new Vector3(12f, 0f, 2.5f), new Vector3(0.8f, 1.05f, 3.5f), new Color(0.55f, 0.42f, 0.3f), true);
            b.Prop("Register", new Vector3(12f, 1.05f, 2f), new Vector3(0.4f, 0.25f, 0.35f), new Color(0.15f, 0.15f, 0.17f), false);
            b.Prop("Drinks Fridge", new Vector3(13.5f, 0f, 6.8f), new Vector3(0.8f, 2f, 1.6f), new Color(0.75f, 0.85f, 0.95f), true);
            b.Plant(new Vector3(0.7f, 0f, 0.7f));

            // Cooler
            b.Shelf(new Vector3(19.5f, 0f, 4f), 90f, 6f, 2f);
            b.Crate(new Vector3(16.5f, 0f, 7f), 0.9f, false);
            b.Crate(new Vector3(16.5f, 0f, 1f), 0.9f, true);

            // Stock room
            b.Shelf(new Vector3(0.6f, 0f, 12.5f), 90f, 3f, 2.2f);
            b.Crate(new Vector3(5.8f, 0f, 13.8f), 1f, true);
            b.Crate(new Vector3(5.8f, 0f, 11.4f), 0.8f, false);

            // Office
            b.Desk(new Vector3(9.2f, 0f, 13.4f), 180f);
            b.Prop("Safe", new Vector3(12.4f, 0f, 11f), new Vector3(0.8f, 1f, 0.8f), new Color(0.25f, 0.25f, 0.27f), true);
            b.Console("store_console", "Store CCTV Terminal", 11.6f, 14.4f, 0f, true, true);

            // Restroom
            b.Prop("Stall Divider", new Vector3(17.5f, 0f, 13.6f), new Vector3(0.08f, 1.4f, 2.6f), new Color(0.6f, 0.65f, 0.7f), false);
            b.Prop("Sinks", new Vector3(13.4f, 0f, 13f), new Vector3(0.5f, 0.9f, 2f), new Color(0.9f, 0.9f, 0.9f), false);

            // Security
            b.Camera(0.6f, 7.4f, 135f);
            b.Camera(13.4f, 0.6f, 315f);
            b.Camera(19.4f, 9f, 270f);
            b.AlarmPanel(7.25f, 12f, 90f);
            b.Beacon(7f, 4f);
            b.Beacon(17f, 9f);

            b.Evidence(9f, 11.5f);
            b.Evidence(2f, 13.5f);
            b.Evidence(17.8f, 4.5f);
            b.Escape(3.5f, 18f);
            b.Escape(26f, 12f);

            // Suspect spawn pool
            b.EnemySpot("shop", 4.25f, 4.5f, 0f, new Vector3(4.25f, 0f, 4.5f), new Vector3(4.25f, 0f, 7.2f), new Vector3(7.75f, 0f, 7.2f), new Vector3(7.75f, 0f, 1.5f));
            b.EnemySpot("shop", 11f, 6.5f, 270f);
            b.EnemySpot("shop", 1.2f, 1.5f, 45f);
            b.EnemySpot("shop", 12.6f, 5.4f, 225f);
            b.EnemySpot("cooler", 16.5f, 4f, 270f);
            b.EnemySpot("cooler", 18.2f, 7.2f, 180f);
            b.EnemySpot("backhall", 3f, 9f, 90f, new Vector3(3f, 0f, 9f), new Vector3(18f, 0f, 9f));
            b.EnemySpot("stock", 3f, 12.5f, 0f);
            b.EnemySpot("stock", 4.5f, 14.2f, 180f);
            b.EnemySpot("office", 10.5f, 11.6f, 180f);
            b.EnemySpot("restroom", 15f, 12.5f, 0f);

            // Civilian spawn pool
            b.CivilianSpot("shop", 13.2f, 2.5f, 270f);
            b.CivilianSpot("shop", 1.2f, 7.2f, 135f);
            b.CivilianSpot("shop", 7.75f, 0.9f, 0f);
            b.CivilianSpot("shop", 11f, 1.2f, 315f);
            b.CivilianSpot("cooler", 15f, 7.2f, 135f);
            b.CivilianSpot("stock", 6.3f, 10.6f, 45f);
            b.CivilianSpot("office", 7.8f, 14.3f, 135f);
            b.CivilianSpot("restroom", 19.2f, 14.2f, 225f);

            return b.Finish();
        }
    }
}
