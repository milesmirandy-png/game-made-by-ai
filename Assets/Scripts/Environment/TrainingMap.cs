using UnityEngine;

namespace Swat
{
    // TRU training facility: a firing range with pop-up targets and a small
    // training house with doors to open, breach and stack on, a flashbang
    // room, a volunteer "suspect" and a volunteer "civilian".
    public static class TrainingMap
    {
        public static LevelLayout Build(Transform parent)
        {
            var b = new LevelBuilder(parent, "training", "TRU Training Facility");
            var wall = new Color(0.55f, 0.58f, 0.6f);
            var inner = new Color(0.78f, 0.78f, 0.75f);

            b.Area("Training Facility", -10f, -14f, 50f, 30f);
            b.Layout.navBounds = new Bounds(new Vector3(20f, 1f, 8f), new Vector3(64f, 6f, 48f));
            b.Slab("Ground", -30f, -30f, 70f, 50f, -0.2f, 0f, new Color(0.3f, 0.4f, 0.26f));
            b.Room("yard", "Training Yard", -8f, -12f, 48f, -0.2f, new Color(0.24f, 0.25f, 0.27f), false);
            b.Boundary(-8f, -12f, 48f, 28f);
            b.Arrival(new Vector3(9f, 0.05f, -5.5f), 0f, new Vector3(16f, 0f, -7f), 0f, new Vector3(16f, 0f, -30f));
            b.Extraction(6f, -8.5f, 12f, -3f);
            b.SafeZone(-1f, -8.5f, 4f, -3f);
            b.Lamp(new Vector3(3f, 0f, -1.5f));
            b.Lamp(new Vector3(30f, 0f, -1.5f));

            b.Room("range", "Firing Range", 0f, 0f, 16f, 24f, new Color(0.45f, 0.45f, 0.44f));
            b.Room("hall", "House Entry", 16f, 0f, 40f, 6f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("roomA", "Room A", 16f, 6f, 26f, 15f, new Color(0.5f, 0.45f, 0.4f));
            b.Room("roomB", "Breach Room", 26f, 6f, 34f, 15f, new Color(0.5f, 0.45f, 0.4f));
            b.Room("room_flash", "Flash Room", 34f, 6f, 40f, 15f, new Color(0.55f, 0.5f, 0.35f));
            b.Room("backhall", "Back Corridor", 16f, 15f, 40f, 17f, new Color(0.6f, 0.6f, 0.62f));
            b.Room("roomC", "Suspect Room", 16f, 17f, 29f, 24f, new Color(0.45f, 0.4f, 0.45f));
            b.Room("roomD", "Volunteer Room", 29f, 17f, 40f, 24f, new Color(0.4f, 0.45f, 0.45f));

            b.WallX(0f, 0f, 40f, wall, true, Gap.Open(8f, 3f));
            b.WallX(24f, 0f, 40f, wall, true);
            b.WallZ(0f, 0f, 24f, wall, true);
            b.WallZ(40f, 0f, 24f, wall, true);
            b.WallZ(16f, 0f, 24f, inner, false, Gap.Open(3f, 2.4f));
            b.WallX(6f, 16f, 40f, inner, false, Gap.Door(22f, "door_train_entry"), Gap.Locked(30f, "door_train_breach", true, false), Gap.Door(37f, "door_train_flash"));
            b.WallZ(26f, 6f, 15f, inner, false);
            b.WallZ(34f, 6f, 15f, inner, false);
            b.WallX(15f, 16f, 40f, inner, false, Gap.Open(21f, 2f), Gap.Open(30f, 2f), Gap.Open(37f, 2f));
            b.WallX(17f, 16f, 40f, inner, false, Gap.Door(23.5f, "door_train_stack"), Gap.Door(34.5f, "door_train_volunteer"));
            b.WallZ(29f, 17f, 24f, inner, false);

            // Firing range: shooting line, lane dividers and targets.
            b.Decal(new Vector3(8f, 0.04f, 6.5f), new Vector3(15f, 0.01f, 0.15f), new Color(1f, 0.85f, 0.1f), 0.5f);
            foreach (float x in new[] { 3.2f, 6.4f, 9.6f, 12.8f })
                b.Prop("Lane Divider", new Vector3(x, 0f, 13f), new Vector3(0.1f, 1.1f, 12f), new Color(0.35f, 0.35f, 0.37f), false);
            foreach (float x in new[] { 1.6f, 4.8f, 8f, 11.2f, 14.4f }) b.Layout.trainingTargets.Add(new Vector3(x, 0f, 21f));
            b.Layout.trainingTargets.Add(new Vector3(4.8f, 0f, 15f));
            b.Layout.trainingTargets.Add(new Vector3(11.2f, 0f, 15f));
            b.Zone("zone_range", 5f, 2f, 11f, 6f);
            b.Decal(new Vector3(8f, 0.04f, 4f), new Vector3(6f, 0.01f, 4f), new Color(0.2f, 0.6f, 1f), 0.6f);

            // Training house props
            b.Table(new Vector3(21f, 0f, 11f), new Vector3(1.6f, 0.8f, 0.9f), new Color(0.5f, 0.38f, 0.26f));
            b.Crate(new Vector3(31.5f, 0f, 12.5f), 1f, false);
            b.Decal(new Vector3(37f, 0.04f, 10.5f), new Vector3(5f, 0.01f, 8f), new Color(1f, 0.85f, 0.1f), 0.4f);
            b.Couch(new Vector3(37f, 0f, 23.2f), 0f, new Color(0.35f, 0.4f, 0.5f));
            b.Table(new Vector3(20f, 0f, 22.5f), new Vector3(1.2f, 0.75f, 0.8f), new Color(0.55f, 0.42f, 0.3f));

            b.EnemySpot("dummy", 24.5f, 20.5f, 180f);
            b.CivilianSpot("volunteer", 34f, 20.5f, 200f);

            return b.Finish();
        }
    }
}
