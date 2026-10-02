namespace Swat
{
    // A weapon the player is carrying: its settings plus the ammo left in it.
    public class Weapon
    {
        public WeaponData Data { get; private set; }
        public int Magazine { get; set; }
        public int Reserve { get; set; }

        public Weapon(WeaponData data)
        {
            Data = data;
            Magazine = data.magazineSize;
            Reserve = data.startingReserve;
        }

        public bool CanReload { get { return Magazine < Data.magazineSize && Reserve > 0; } }

        public void FinishReload()
        {
            int taken = System.Math.Min(Data.magazineSize - Magazine, Reserve);
            Magazine += taken;
            Reserve -= taken;
        }
    }

    // A stack of grenades or charges the player is carrying.
    public class EquipmentSlot
    {
        public EquipmentData Data { get; private set; }
        public int Count { get; set; }

        public EquipmentSlot(EquipmentData data)
        {
            Data = data;
            Count = data.startingCount;
        }
    }
}
