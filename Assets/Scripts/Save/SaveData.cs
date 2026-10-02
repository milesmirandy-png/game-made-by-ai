using System;
using System.Collections.Generic;

namespace Swat
{
    // Everything that persists between sessions. Saved as versioned JSON.
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public SettingsData settings = new SettingsData();
        public ProgressData progress = new ProgressData();
    }

    [Serializable]
    public class SettingsData
    {
        public int qualityTier = -1; // -1 = Auto
        public bool showFps;
        public float masterVolume = 0.8f;
        public float effectsVolume = 1f;
        public float voiceVolume = 1f;
        public float ambienceVolume = 0.6f;
        public float interfaceVolume = 0.8f;
        public bool muteEffects, muteVoice, muteAmbience, muteInterface;
        public float zoomSpeed = 1f;
        public float lookAhead = 0.25f;
        public int zoomPreset = 1;
        public bool planningPauses = true;
        public bool lineOfSight = true;
        public int difficulty = 1; // 0 Recruit, 1 Regular, 2 Veteran
        public List<KeyBinding> bindings = new List<KeyBinding>();
    }

    [Serializable]
    public class KeyBinding
    {
        public string action;
        public int key;
    }

    [Serializable]
    public class ProgressData
    {
        public List<MissionRecord> missions = new List<MissionRecord>();
        public List<string> unlocked = new List<string>();
        public bool trainingComplete;
        public int missionsCompleted;
        public string leaderId = "leader";
        public List<string> squadIds = new List<string> { "breacher", "medic" };
        public List<OfficerLoadout> loadouts = new List<OfficerLoadout>();
        public string lastMissionId;
        public int totalShots, totalHits, totalArrests, totalRescues;
    }

    [Serializable]
    public class MissionRecord
    {
        public string missionId;
        public bool completed;
        public int attempts;
        public int bestScore;
        public string bestRating = "-";
        public float bestTime;
    }
}
