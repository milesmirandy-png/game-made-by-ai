namespace Swat
{
    // Top-down tactical view or first person (helmet cam). V switches between them at any time: in the
    // menus it changes the setting for the next mission, in a mission it switches straight away (the
    // level's walls, doors, ceilings and lamps switch with it, see ViewParts). Settings -> Camera
    // has the same choice. Menus and headquarters always use the top-down showcase camera. Online,
    // each player picks their own view.
    public static class ViewMode
    {
        public static bool FirstPerson { get; private set; }

        public const float FirstPersonWallHeight = 2.75f;
        public const float FirstPersonDoorHeight = 2.08f;

        // Before building a mission or match level.
        public static void ApplySetting()
        {
            FirstPerson = SaveManager.Settings.cameraView == 1;
        }

        // Back in the menus.
        public static void Clear()
        {
            FirstPerson = false;
        }

        // Sets the view: saved as the setting, and in a mission at once.
        public static void Set(bool firstPerson, bool inMission)
        {
            SaveManager.Settings.cameraView = firstPerson ? 1 : 0;
            if (inMission) FirstPerson = firstPerson;
        }

        // V: the other view.
        public static void Toggle(bool inMission)
        {
            bool firstPerson = SaveManager.Settings.cameraView != 1;
            Set(firstPerson, inMission);
            SaveManager.Save();
            UIManager.Notify("View: " + (firstPerson ? "First person (helmet cam)" : "Top-down") + (inMission ? "" : " (for the next mission)"));
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FirstPerson = false;
        }
    }
}
