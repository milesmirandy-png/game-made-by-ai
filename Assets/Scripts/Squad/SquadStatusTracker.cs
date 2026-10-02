namespace Swat
{
    // Read-only summaries of the squad for the HUD, debriefing and abilities.
    public static class SquadStatusTracker
    {
        public static int Total { get { return AIManager.Instance != null ? AIManager.Instance.Officers.Count : 0; } }

        public static int Alive
        {
            get
            {
                int count = 0;
                if (AIManager.Instance == null) return 0;
                foreach (var officer in AIManager.Instance.Officers) if (officer.IsAlive) count++;
                return count;
            }
        }

        public static int Down { get { return Total - Alive; } }

        public static int ThreatsSeenByTeam { get { return TacticalIntel.Instance != null ? TacticalIntel.Instance.ThreatsVisible : 0; } }

        // "Healthy", "Wounded", "Critical" or "DOWN".
        public static string Condition(OperatorHealth health)
        {
            if (!health.IsAlive) return "DOWN";
            if (health.Fraction > 0.66f) return "Healthy";
            if (health.Fraction > 0.33f) return "Wounded";
            return "Critical";
        }
    }
}
