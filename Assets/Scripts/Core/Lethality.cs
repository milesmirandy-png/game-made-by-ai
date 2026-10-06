namespace Swat
{
    // Missions play like a real entry: gunfights are short and every hit matters.
    // Police rounds hit suspects 1.6x as hard (three rifle hits instead of four,
    // a point-blank shotgun blast is enough), and suspects' rounds hit officers twice as
    // hard. Armour still takes its share off. The game modes keep their own
    // balance (time-to-kill table in REPORT.md), so these only apply to suspects.
    public static class Lethality
    {
        public const float ToSuspects = 1.6f;
        public const float ToPolice = 2f;
    }
}
