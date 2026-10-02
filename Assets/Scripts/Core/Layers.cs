namespace Swat
{
    // Physics layers. Characters live on an unnamed user layer so the project
    // needs no Tag Manager setup.
    public static class Layers
    {
        public const int World = 0;      // Default: floors, walls, doors, props
        public const int Characters = 9; // player, suspects, civilians

        public const int WorldMask = 1 << World;
        public const int ShootableMask = (1 << World) | (1 << Characters);
    }
}
