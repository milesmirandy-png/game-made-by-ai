using System;
using System.Collections.Generic;
using UnityEngine;

namespace Swat
{
    public enum MissionType { BuildingClearance, CivilianRescue, Investigation, Emergency, Training }

    // Mission = the normal SWAT operation; the others are team-versus-team exercises (game modes).
    public enum GameMode { Mission, TeamDeathmatch, CaptureTheFlag, ZoneControl, GunGame, Elimination, VipEscort, RapidDeployment }

    public enum ObjectiveType
    {
        EnterBuilding, RescueCivilians, SecureSuspects, ArrestSuspects, SecureRoom, InvestigateRoom, SecureEvidence,
        ApprehendLeader, UseConsole, ReachExtraction, NoCivilianCasualties, NoOfficerDown, AlarmNotTriggered,
        TreatInjured, TimeLimit, DisableCameras,
        TrainingMove, TrainingShootTargets, TrainingReload, TrainingSwitchWeapon, TrainingOpenDoor, TrainingBreach,
        TrainingFlashbang, TrainingCommandSquad, TrainingRestrain, TrainingEscort,
    }

    public enum CivilianType { OfficeWorker, SecurityGuard, Visitor, Injured, Hiding, Hostage, Resident }

    [Serializable]
    public class ObjectiveDefinition
    {
        public ObjectiveType type;
        [Tooltip("Room id, area tag or similar, depending on type")] public string targetId;
        public int count;
        public int points = 100;
        public string description;

        public ObjectiveDefinition() { }
        public ObjectiveDefinition(ObjectiveType type, string description, int points, string targetId = null, int count = 0)
        {
            this.type = type;
            this.description = description;
            this.points = points;
            this.targetId = targetId;
            this.count = count;
        }
    }

    [Serializable]
    public class EnemyGroup
    {
        public string enemyId;
        public int count = 1;
        [Tooltip("Spawn point tag from the map, empty = anywhere")] public string spawnTag;

        public EnemyGroup() { }
        public EnemyGroup(string enemyId, int count, string spawnTag = null)
        {
            this.enemyId = enemyId;
            this.count = count;
            this.spawnTag = spawnTag;
        }
    }

    [Serializable]
    public class CivilianGroup
    {
        public CivilianType type;
        public int count = 1;
        public string spawnTag;

        public CivilianGroup() { }
        public CivilianGroup(CivilianType type, int count, string spawnTag = null)
        {
            this.type = type;
            this.count = count;
            this.spawnTag = spawnTag;
        }
    }

    // Everything that defines a mission. Maps are fixed layouts; the
    // mission chooses objectives, who is inside and how it varies per run.
    [CreateAssetMenu(menuName = "SWAT/Mission", fileName = "NewMission")]
    public class MissionData : ScriptableObject
    {
        public string id = "mission";
        public string displayName = "Mission";
        public string location = "Location";
        public MissionType missionType;
        [TextArea] public string description;
        [TextArea(4, 10)] public string briefing;
        [Tooltip("office, warehouse, apartment or training")] public string mapId = "office";
        [Range(1, 3)] public int difficulty = 1;
        [Range(0, 3)] public int maxSquad = 3;
        [Tooltip("Seconds; finishing faster earns a time bonus")] public float parTime = 420f;
        public List<ObjectiveDefinition> objectives = new List<ObjectiveDefinition>();
        [Tooltip("Optional objectives are drawn at random from this pool")] public List<ObjectiveDefinition> optionalPool = new List<ObjectiveDefinition>();
        public int optionalCount = 2;
        [Tooltip("Objectives unlock one after another (used by training)")] public bool sequentialObjectives;
        public List<EnemyGroup> enemies = new List<EnemyGroup>();
        public List<CivilianGroup> civilians = new List<CivilianGroup>();
        public List<EquipmentCount> bonusEquipment = new List<EquipmentCount>();
        [Tooltip("Lighting profile: Day, Evening or Night")] public TimeOfDay timeOfDay;
        [Tooltip("Old setting kept for compatibility: treated as Night if Time Of Day is left at Day")] public bool night;
        [Range(0f, 1f)] public float powerOutageChance;
        [Range(0f, 1f)] public float alarmArmedChance = 1f;
        [Range(0f, 1f)] public float camerasActiveChance = 1f;
        [Range(0f, 1f), Tooltip("Chance for each lockable door to be locked")] public float randomLockChance = 0.3f;
        [Tooltip("0 = new random seed each deployment")] public int seed;
        public int unlockAfterMissions;
        public int sortOrder;
        [Tooltip("Shown as LEVEL n on the level select; 0 = not part of the numbered campaign")] public int levelNumber;
        public bool isTraining;
        [Tooltip("Made in the level creator (not part of campaign progress)")] public bool isCustom;
        public Color thumbnailColor = new Color(0.2f, 0.3f, 0.45f);
        [Tooltip("Game modes: a team exercise on this map instead of a mission")] public GameMode mode;

        public bool IsVersus { get { return mode != GameMode.Mission; } }
        // Game modes: the arcade guns (rotary gun, drum shotgun, marker launcher...) are allowed too.
        [HideInInspector] public bool arcadeWeapons;

        public TimeOfDay DefaultTime { get { return timeOfDay != TimeOfDay.Day ? timeOfDay : night ? TimeOfDay.Night : TimeOfDay.Day; } }

        // "LEVEL 3", "TRAINING" or "CUSTOM LEVEL" for headers and cards.
        public string LevelLabel { get { return isTraining ? "TRAINING" : isCustom ? "CUSTOM LEVEL" : levelNumber > 0 ? "LEVEL " + levelNumber : "OPERATION"; } }
    }
}
