using System;
using System.IO;
using UnityEngine;

namespace Swat
{
    // Loads and saves SaveData as JSON in the persistent data folder. A
    // missing file starts fresh; a corrupted one is set aside (.bak) and a new
    // save is started, so a bad file never stops the game from launching.
    public static class SaveManager
    {
        const string FileName = "swat_tactical_response.json";
        static SaveData data;

        public static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        public static SettingsData Settings { get { return Data.settings; } }
        public static ProgressData Progress { get { return Data.progress; } }
        public static string FilePath { get { return Path.Combine(Application.persistentDataPath, FileName); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            data = null;
        }

        public static void Load()
        {
            data = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (data != null && data.version > SaveData.CurrentVersion)
                        Debug.LogWarning("SWAT: save file is from a newer version; loading what we can.");
                    // Keep a copy of an older-format file before it is rewritten in the new format.
                    if (data != null && data.version < SaveData.CurrentVersion)
                    {
                        try { File.Copy(FilePath, FilePath + ".v" + data.version + ".bak", true); } catch (Exception) { }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("SWAT: could not read save file, starting a new one. " + e.Message);
                try { File.Copy(FilePath, FilePath + ".bak", true); } catch (Exception) { }
                data = null;
            }

            if (data == null) data = new SaveData();
            if (data.settings == null) data.settings = new SettingsData();
            if (data.progress == null) data.progress = new ProgressData();
            data.version = SaveData.CurrentVersion;
            Sanitize(data.settings);
            GameInput.Load(data.settings.bindings);
        }

        // Clamps values a hand-edited or damaged file could put out of range.
        static void Sanitize(SettingsData s)
        {
            s.masterVolume = Mathf.Clamp01(s.masterVolume);
            s.musicVolume = Mathf.Clamp01(s.musicVolume);
            s.effectsVolume = Mathf.Clamp01(s.effectsVolume);
            s.weaponsVolume = Mathf.Clamp01(s.weaponsVolume);
            s.voiceVolume = Mathf.Clamp01(s.voiceVolume);
            s.ambienceVolume = Mathf.Clamp01(s.ambienceVolume);
            s.interfaceVolume = Mathf.Clamp01(s.interfaceVolume);
            s.zoomSpeed = Mathf.Clamp(s.zoomSpeed, 0.2f, 3f);
            s.lookAhead = Mathf.Clamp(s.lookAhead, 0f, 0.5f);
            s.cameraSmoothing = Mathf.Clamp(s.cameraSmoothing, 0f, 0.4f);
            s.cameraShake = Mathf.Clamp(s.cameraShake, 0, 2);
            s.mouseSensitivity = Mathf.Clamp(s.mouseSensitivity, 0.2f, 3f);
            s.aimSmoothing = Mathf.Clamp(s.aimSmoothing, 0f, 0.9f);
            s.controllerSensitivity = Mathf.Clamp(s.controllerSensitivity, 0.2f, 3f);
            s.crosshairSize = Mathf.Clamp(s.crosshairSize, 0.5f, 2f);
            s.crosshairOpacity = Mathf.Clamp(s.crosshairOpacity, 0.2f, 1f);
            s.uiScale = Mathf.Clamp(s.uiScale, 0.75f, 1.5f);
            s.textSize = Mathf.Clamp(s.textSize, 0.85f, 1.4f);
            s.subtitleSize = Mathf.Clamp(s.subtitleSize, 0.8f, 1.6f);
            s.minimapScale = Mathf.Clamp(s.minimapScale, 0.6f, 1.8f);
            s.minimapOpacity = Mathf.Clamp(s.minimapOpacity, 0.3f, 1f);
            s.viewDistance = Mathf.Clamp(s.viewDistance, 0.5f, 1.5f);
            s.flashlightBrightness = Mathf.Clamp(s.flashlightBrightness, 0.4f, 1.6f);
            s.textureQuality = Mathf.Clamp(s.textureQuality, 0, 2);
            s.difficulty = Mathf.Clamp(s.difficulty, 0, 2);
            s.zoomPreset = Mathf.Clamp(s.zoomPreset, 0, 2);
            if (s.zoomSpeed <= 0f) s.zoomSpeed = 1f;
            if (s.bindings == null) s.bindings = new System.Collections.Generic.List<KeyBinding>();
        }

        public static void Save()
        {
            if (data == null) return;
            try
            {
                data.settings.bindings = GameInput.Save();
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("SWAT: could not write save file. " + e.Message);
            }
        }

        public static MissionRecord Record(string missionId)
        {
            foreach (var record in Progress.missions)
                if (record.missionId == missionId) return record;
            var created = new MissionRecord { missionId = missionId };
            Progress.missions.Add(created);
            return created;
        }

        public static bool IsUnlocked(string id)
        {
            return string.IsNullOrEmpty(id) || Progress.unlocked.Contains(id);
        }

        public static void ResetProgress()
        {
            Data.progress = new ProgressData();
            Save();
        }
    }
}
