namespace Swat
{
    // Top-down tactical view or first person (body cam), chosen in Settings ->
    // Camera. A mission or match is built for one or the other: first person
    // gets full-height walls, door headers and ceilings, which would hide
    // everything from above. So the choice is applied when a level is built and
    // stays until the next one. Menus and headquarters always use the top-down
    // showcase camera. Online, each player picks their own view.
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

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FirstPerson = false;
        }
    }
}
