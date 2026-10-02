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
            GameInput.Load(data.settings.bindings);
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
