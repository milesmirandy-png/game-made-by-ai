using System;
using System.Collections.Generic;

namespace Swat
{
    // Everything that persists between sessions. Saved as versioned JSON.
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public SettingsData settings = new SettingsData();
        public ProgressData progress = new ProgressData();
    }

    // Version 2 added the graphics, camera, aiming, audio-mix and accessibility
    // options. Older files load fine: missing fields keep the defaults below.
    [Serializable]
    public class SettingsData
    {
        // Graphics
        public int qualityTier = -1;       // -1 = Auto
        public bool showFps;
        public bool performanceMode;
        public int shadowQuality = -1;     // -1 = preset, 0 off, 1 low, 2 medium, 3 high, 4 very high
        public int antiAliasing = -1;      // -1 = preset, otherwise 0/2/4/8 MSAA samples
        public int effectsQuality = -1;    // -1 = preset, 0 low, 1 medium, 2 high
        public int textureQuality = -1;    // -1 = preset, 0 low, 1 medium, 2 high (size of generated surface textures)
        public int vSync = -1;             // -1 = preset, 0 off, 1 on
        public bool postProcessing = true;
        public bool ambientOcclusion = true;
        public float viewDistance = 1f;
        public int resolutionWidth, resolutionHeight; // 0 = keep current
        public int fullscreenMode = -1;    // -1 = keep current, otherwise UnityEngine.FullScreenMode
        public float flashlightBrightness = 1f;
        public int artStyle;               // 0 pixel art (default), 1 smooth
        public int pixelSize = 1;          // pixel art: 0 chunky, 1 medium, 2 fine
        public bool pixelOutlines = true;  // pixel art: dark outlines around objects

        // Audio
        public float masterVolume = 0.8f;
        public float musicVolume = 0.45f;
        public float effectsVolume = 1f;
        public float weaponsVolume = 0.9f;
        public float voiceVolume = 1f;
        public float ambienceVolume = 0.6f; // "Environment"
        public float interfaceVolume = 0.8f;
        public bool muteMusic, muteEffects, muteWeapons, muteVoice, muteAmbience, muteInterface;

        // Camera and aiming
        public float zoomSpeed = 1f;
        public float lookAhead = 0.25f;
        public int zoomPreset = 1;
        public bool autoIndoorZoom = true;
        public float cameraSmoothing = 0.12f;
        public bool edgeScrolling;
        public int cameraShake = 1;        // 0 off, 1 low, 2 medium
        public int cameraView;             // 0 top-down, 1 first person (body cam); V switches it any time
        public float fieldOfView = 90f;    // first person, horizontal degrees
        public bool bodyCamLook = true;    // first person: wide lens, grain and the REC overlay
        public bool realisticAmmo = true;  // HUD shows how full the magazine feels and magazines left, not exact rounds
        public float mouseSensitivity = 1f;
        public float aimSmoothing;         // 0 = off (direct)
        public float controllerSensitivity = 1f;
        public bool controllerAimAssist = true;

        // Gameplay
        public bool planningPauses = true;
        public bool lineOfSight = true;
        public int difficulty = 1;         // 0 Recruit, 1 Regular, 2 Veteran
        public bool autoReload = true;
        public bool autoSwitchWhenEmpty;
        public bool autoFlashlight;
        public bool hitStop = true;        // brief freeze when the player takes someone down
        public bool heavyHandling = true;  // weapons turn at a speed set by their weight; off = the gun snaps to the cursor
        public bool classicCharacters;      // the original blocky officers instead of the imported soldier model
        public bool minimap = true;
        public float minimapScale = 1f;
        public float minimapOpacity = 0.85f;

        // Crosshair
        public float crosshairSize = 1f;
        public float crosshairOpacity = 1f;
        public int crosshairColor;         // index into UITheme.CrosshairColors
        public bool hitMarker = true;

        // Accessibility
        public float uiScale = 1f;
        public float textSize = 1f;
        public bool colorblindMode;
        public bool subtitles = true;
        public float subtitleSize = 1f;
        public bool reduceFlashes;

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
        public VersusOptions versus = new VersusOptions();
    }

    // The last match settings chosen on the Game Modes screen.
    [Serializable]
    public class VersusOptions
    {
        public int mode = 1;               // GameMode: 1 Team Deathmatch, 2 Capture the Flag, 3 Zone Control, 4 Gun Game, 5 Elimination
        public string mapId = "warehouse";
        public int teamSize = 4;           // per side, including you
        public int scoreIndex = 1;
        public int timeIndex = 1;
        public int botSkill = 1;           // 0 easy, 1 normal, 2 hard
        public int timeOfDay;
        public int matchesPlayed, matchesWon;
        public string onlineName = "";      // your name in online games (empty: your officer's callsign)
        public string joinAddress = "";     // the last host address you joined
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
