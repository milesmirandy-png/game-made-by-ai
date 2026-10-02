#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swat.EditorTools
{
    // Optional editor helpers. The game runs in any scene without these; they
    // just save a dedicated mission scene and add it to the build list.
    public static class SwatEditorMenu
    {
        const string ScenePath = "Assets/Scenes/Mission01_ClearTheBuilding.unity";

        [MenuItem("SWAT/Create Mission Scene")]
        static void CreateMissionScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("SWAT Game").AddComponent<GameManager>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("SWAT: saved " + ScenePath + " and added it to Build Settings. Press Play to start the mission.");
        }

        [MenuItem("SWAT/Play Mission")]
        static void PlayMission()
        {
            if (System.IO.File.Exists(ScenePath) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
#endif
