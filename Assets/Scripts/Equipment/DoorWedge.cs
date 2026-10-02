namespace Swat
{
    // Door wedge: jams a closed door so nobody can open it (suspects included)
    // until someone removes it with a hold-E interaction.
    public static class DoorWedge
    {
        public static bool CanUseOn(DoorController door)
        {
            return door != null && door.CanWedge;
        }

        public static bool Place(DoorController door)
        {
            if (!CanUseOn(door)) return false;
            door.PlaceWedge();
            return true;
        }
    }
}
