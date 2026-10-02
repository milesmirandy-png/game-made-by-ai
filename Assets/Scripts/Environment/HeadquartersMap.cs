using UnityEngine;

namespace Swat
{
    // The Tactical Response Unit headquarters, shown behind the menus: a
    // briefing room, armory, locker room and the garage with the SWAT van.
    // Camera anchors let each menu page frame a different part of it.
    public class HeadquartersMap
    {
        public Transform root;
        public LevelLayout layout;
        public Vector3 menuCamera, menuTarget;
        public Vector3 missionsCamera, missionsTarget;
        public Vector3 rosterCamera, rosterTarget;
        public Vector3 armoryCamera, armoryTarget;

        public static HeadquartersMap Build(Transform parent)
        {
            var hq = new HeadquartersMap();
            var b = new LevelBuilder(parent, "hq", "TRU Headquarters");
            hq.root = b.Layout.root;
            var wall = new Color(0.32f, 0.35f, 0.4f);
            var navy = new Color(0.1f, 0.13f, 0.2f);

            b.Slab("Ground", -40f, -40f, 60f, 40f, -0.2f, 0f, new Color(0.16f, 0.17f, 0.19f));
            b.Room("briefing", "Briefing Room", 0f, 0f, 14f, 10f, new Color(0.28f, 0.3f, 0.34f));
            b.Room("armory", "Armory", 14f, 0f, 24f, 10f, new Color(0.3f, 0.3f, 0.32f));
            b.Room("lockers", "Locker Room", 0f, 10f, 12f, 18f, new Color(0.34f, 0.36f, 0.4f));
            b.Room("garage", "Garage", 12f, 10f, 30f, 22f, new Color(0.24f, 0.24f, 0.26f));
            b.WallX(0f, 0f, 24f, wall, true);
            b.WallZ(0f, 0f, 18f, wall, true);
            b.WallZ(24f, 0f, 10f, wall, true);
            b.WallX(18f, 0f, 12f, wall, true);
            b.WallX(22f, 12f, 30f, wall, true, Gap.Open(21f, 8f));
            b.WallZ(30f, 10f, 22f, wall, true);
            b.WallZ(14f, 0f, 10f, wall, false, Gap.Open(5f, 2f));
            b.WallX(10f, 0f, 12f, wall, false, Gap.Open(6f, 2f));
            b.WallZ(12f, 10f, 18f, wall, false, Gap.Open(14f, 2f));
            b.WallX(10f, 24f, 30f, wall, true);

            // Briefing room: table, chairs, wall screen and the mission board.
            b.Table(new Vector3(7f, 0f, 5f), new Vector3(5f, 0.8f, 2.2f), new Color(0.2f, 0.22f, 0.26f));
            for (int i = 0; i < 4; i++)
            {
                b.Prop("Chair", new Vector3(5f + i * 1.3f, 0f, 3.2f), new Vector3(0.5f, 0.5f, 0.5f), navy, false);
                b.Prop("Chair", new Vector3(5f + i * 1.3f, 0f, 6.8f), new Vector3(0.5f, 0.5f, 0.5f), navy, false);
            }
            b.Decal(new Vector3(7f, 1.2f, 9.85f), new Vector3(5f, 1.6f, 0.06f), new Color(0.25f, 0.45f, 0.75f), 1.2f);
            for (int i = 0; i < 5; i++)
                b.Decal(new Vector3(0.15f, 0.9f, 2f + i * 1.5f), new Vector3(0.05f, 0.8f, 1.1f), new Color(0.85f, 0.8f, 0.6f), 0.3f);

            // Armory racks with a few weapons on display.
            for (int i = 0; i < 4; i++)
            {
                b.Prop("Weapon Rack", new Vector3(16f + i * 2.2f, 0f, 9.4f), new Vector3(1.8f, 1.8f, 0.4f), new Color(0.18f, 0.18f, 0.2f), false);
                var display = new GameObject("Display").transform;
                display.SetParent(b.Props, false);
                display.localPosition = new Vector3(16f + i * 2.2f, 0.7f, 9.1f);
                display.localRotation = Quaternion.Euler(-90f, 0f, 0f); // stand the weapon upright on the rack
                var weapons = GameData.AllWeapons;
                WeaponModels.Build(weapons[(i * 2) % weapons.Count], display, null);
            }
            b.Table(new Vector3(19f, 0f, 4f), new Vector3(3f, 0.9f, 1.2f), new Color(0.25f, 0.25f, 0.28f));

            // Locker room
            for (int i = 0; i < 6; i++)
                b.Prop("Locker", new Vector3(0.5f, 0f, 11f + i * 1.1f), new Vector3(0.6f, 2f, 1f), new Color(0.3f, 0.36f, 0.45f), false);
            b.Prop("Bench", new Vector3(4f, 0f, 14f), new Vector3(0.6f, 0.45f, 4f), new Color(0.45f, 0.35f, 0.25f), false);

            // Garage with the van, and an outside lot.
            VanBuilder.Build(b.Props, new Vector3(21f, 0f, 15.5f), 0f);
            b.Car(new Vector3(27f, 0f, 15f), 0f, new Color(0.95f, 0.95f, 0.95f), true);
            b.Lamp(new Vector3(14f, 0f, 25f));
            b.Lamp(new Vector3(28f, 0f, 25f));

            hq.menuCamera = new Vector3(22f, 7f, 34f);
            hq.menuTarget = new Vector3(20f, 0.5f, 16f);
            hq.missionsCamera = new Vector3(7f, 9f, -5f);
            hq.missionsTarget = new Vector3(7f, 0.5f, 5.5f);
            hq.rosterCamera = new Vector3(6f, 8f, 4f);
            hq.rosterTarget = new Vector3(3f, 0.5f, 14f);
            hq.armoryCamera = new Vector3(19f, 8f, -3f);
            hq.armoryTarget = new Vector3(19f, 0.5f, 6f);
            hq.layout = b.Finish();
            return hq;
        }
    }
}
