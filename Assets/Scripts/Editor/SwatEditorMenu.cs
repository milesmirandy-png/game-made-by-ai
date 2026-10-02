#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swat.EditorTools
{
    // Editor helpers. The game runs from any scene (GameBootstrap creates it
    // on Play); these save a Boot scene for builds and export the built-in
    // content as editable ScriptableObject assets.
    [InitializeOnLoad]
    public static class SwatEditorMenu
    {
        const string BootScenePath = "Assets/Scenes/Boot.unity";
        const string OfferedKey = "SWAT.BootSceneCreated";

        static SwatEditorMenu()
        {
            // First time the project is opened: create the Boot scene so there's something to press Play in.
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(OfferedKey + "." + Application.dataPath, false)) return;
                EditorPrefs.SetBool(OfferedKey + "." + Application.dataPath, true);
                if (!File.Exists(BootScenePath) && EditorBuildSettings.scenes.Length == 0) CreateBootScene(false);
            };
        }

        [MenuItem("SWAT/Create Boot Scene")]
        static void CreateBootSceneMenu()
        {
            CreateBootScene(true);
        }

        static void CreateBootScene(bool interactive)
        {
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var active = EditorSceneManager.GetActiveScene();
            if (!interactive && active.isDirty) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("SWAT Game").AddComponent<GameManager>();
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, BootScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == BootScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(BootScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("SWAT: saved " + BootScenePath + " and added it to Build Settings. Press Play to start.");
        }

        [MenuItem("SWAT/Play")]
        static void Play()
        {
            if (File.Exists(BootScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(BootScenePath);
            EditorApplication.isPlaying = true;
        }

        // Writes every built-in weapon, attachment, armor, equipment item, officer,
        // suspect type and mission as an asset. GameData prefers these assets
        // over the built-in defaults (matched by id), so they can be edited in
        // the Inspector. Existing assets are never overwritten.
        [MenuItem("SWAT/Create Editable Data Assets")]
        static void CreateDataAssets()
        {
            int created = 0;
            created += Export(DefaultContent.Weapons(), "Weapons", "Weapons", w => w.id);
            created += Export(DefaultContent.Attachments(), "Weapons", "Attachments", a => a.id);
            created += Export(DefaultContent.ArmorList(), "Equipment", "Armor", a => a.id);
            created += Export(DefaultContent.EquipmentList(), "Equipment", "Equipment", e => e.id);
            created += Export(DefaultContent.Officers(), "Officers", "Officers", o => o.id);
            created += Export(DefaultContent.Enemies(), "Enemies", "Enemies", e => e.id);
            created += Export(DefaultContent.Missions(), "Missions", "Missions", m => m.id);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SWAT: created " + created + " data asset(s) under Assets/ScriptableObjects/*/Resources/SWAT/. Edit them in the Inspector; delete one to go back to the built-in default.");
        }

        static int Export<T>(List<T> items, string group, string type, System.Func<T, string> id) where T : ScriptableObject
        {
            string folder = "Assets/ScriptableObjects/" + group + "/Resources/SWAT/" + type;
            Directory.CreateDirectory(folder);
            int created = 0;
            foreach (var item in items)
            {
                string path = folder + "/" + id(item) + ".asset";
                if (File.Exists(path)) continue;
                var copy = Object.Instantiate(item);
                copy.name = id(item);
                AssetDatabase.CreateAsset(copy, path);
                created++;
            }
            return created;
        }

        [MenuItem("SWAT/Open Screenshots Folder")]
        static void OpenScreenshots()
        {
            Directory.CreateDirectory(ScreenshotTool.Folder);
            EditorUtility.RevealInFinder(ScreenshotTool.Folder);
        }

        [MenuItem("SWAT/Delete Save File")]
        static void DeleteSave()
        {
            if (!EditorUtility.DisplayDialog("SWAT", "Delete the save file (settings and campaign progress)?", "Delete", "Cancel")) return;
            if (File.Exists(SaveManager.FilePath)) File.Delete(SaveManager.FilePath);
            Debug.Log("SWAT: deleted " + SaveManager.FilePath);
        }
    }
}
#endif
