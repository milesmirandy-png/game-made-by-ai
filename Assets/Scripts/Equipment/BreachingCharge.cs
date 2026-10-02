namespace Swat
{
    // Breaching charge: only works on doors marked as breachable (yellow
    // stripes). The blast is handled by DoorController.PlaceCharge/Detonate;
    // it opens the door and stuns people close behind it, nothing else breaks.
    public static class BreachingCharge
    {
        public static bool CanUseOn(DoorController door)
        {
            return door != null && door.Breachable && !door.ChargePlaced
                && (door.State == DoorState.Locked || door.State == DoorState.Closed || door.State == DoorState.Wedged);
        }

        public static bool Place(DoorController door, PlayerController player)
        {
            if (!CanUseOn(door)) return false;
            door.PlaceCharge(player.Position, true);
            return true;
        }
    }
}
