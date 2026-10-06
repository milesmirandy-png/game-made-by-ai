using System.Collections.Generic;

namespace Swat
{
    // Civilian tallies for the HUD, objectives and debriefing.
    public class CivilianRescueTracker
    {
        public int total, encountered, rescued, evacuated, injured, killed, treated, initiallyInjured;

        public int Remaining { get { return total - evacuated - killed; } }
    }

    // Everything counted during a mission.
    public class MissionStats
    {
        public int suspectsTotal, suspectsEncountered, suspectsArrested, suspectsKilled, suspectsEscaped;
        public int officersDowned, unauthorizedForce, doorsBreached, ordersGiven, evidenceSecured, evidenceTotal;
        public int shotsFired, shotsHit;
        // Ready or Not-style paperwork: suspects' guns left on the floor and secured, and reports to TOC.
        public int armedSuspects, weaponsDropped, weaponsSecured, tocReports;
        public bool alarmTriggered, camerasDisabled, footageReviewed, leaderEscaped, playerDowned;
        public readonly Dictionary<EquipmentKind, int> equipmentUsed = new Dictionary<EquipmentKind, int>();
        public readonly CivilianRescueTracker civilians = new CivilianRescueTracker();

        public void UsedEquipment(EquipmentKind kind)
        {
            int count;
            equipmentUsed.TryGetValue(kind, out count);
            equipmentUsed[kind] = count + 1;
        }
    }
}
