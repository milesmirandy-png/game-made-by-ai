using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Builds the Kill Popups mutator prefab.
// In Unity's top menu: Tools > Kill Popups > Build Mutator Prefab
//
// The prefab is a screen overlay Canvas with the popup texts and sounds that
// KillPopups.txt looks for by name:
//
//   KillPopups          Canvas + ScriptedBehaviour (Source = KillPopups.txt)
//     Headline          the big popup
//     Score             the "+350" points counter
//     Feed/Line1..5     the small lines under it
//     Sounds/Kill, ...  one AudioSource per sound
//
// Feel free to move, resize or recolor these in the prefab afterwards, just
// keep their names.
public static class KillPopupsBuilder
{
    const string ScriptFileName = "KillPopups.txt";
    const int FeedLines = 5;
    static readonly string[] SoundNames = { "Kill", "Headshot", "Medal", "Multikill", "Streak", "SquadKill", "Bad" };

    [MenuItem("Tools/Kill Popups/Build Mutator Prefab")]
    public static void Build()
    {
        TextAsset script = FindScript();
        if (script == null)
        {
            EditorUtility.DisplayDialog("Kill Popups",
                "Couldn't find " + ScriptFileName + ". Make sure the whole KillPopups folder is inside your Assets folder.", "OK");
            return;
        }

        string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(script)).Replace('\\', '/');
        string prefabPath = folder + "/KillPopups.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null &&
            !EditorUtility.DisplayDialog("Kill Popups", "KillPopups.prefab already exists. Build it again from scratch?", "Rebuild", "Cancel"))
        {
            return;
        }

        GameObject root = BuildObjects(folder);
        bool scriptAdded = AddScriptedBehaviour(root, script);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        string message = "Made " + prefabPath + ".\n\n";
        if (scriptAdded)
        {
            message += "Next: add it as a mutator to your Content Mod (drag KillPopups.prefab into the Mutator Prefab slot) and export the mod.";
        }
        else
        {
            message += "One thing to do by hand: select KillPopups.prefab, make sure it has a ScriptedBehaviour " +
                "(Add Component > ScriptedBehaviour if it doesn't), and drag " + ScriptFileName + " into its Source slot.\n\n" +
                "Then add the prefab as a mutator to your Content Mod and export the mod.";
        }
        Debug.Log("Kill Popups: " + message);
        EditorUtility.DisplayDialog("Kill Popups", message, "OK");
    }

    static TextAsset FindScript()
    {
        foreach (string guid in AssetDatabase.FindAssets("KillPopups t:TextAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) == ScriptFileName)
            {
                return AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            }
        }
        return null;
    }

    static GameObject BuildObjects(string folder)
    {
        var root = new GameObject("KillPopups", typeof(RectTransform));

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // The script clears these when the game starts. If you still see them
        // in game, the script isn't running.
        Font font = DefaultFont();
        MakeText(root.transform, "Headline", "DOUBLE KILL", font, 64, -150, new Color(1f, 0.35f, 0.2f));
        MakeText(root.transform, "Score", "+250", font, 36, -205, new Color(1f, 0.9f, 0.4f));

        var feed = new GameObject("Feed", typeof(RectTransform));
        feed.transform.SetParent(root.transform, false);
        var feedRect = (RectTransform)feed.transform;
        feedRect.anchorMin = Vector2.zero;
        feedRect.anchorMax = Vector2.one;
        feedRect.offsetMin = Vector2.zero;
        feedRect.offsetMax = Vector2.zero;

        string[] examples = { "HEADSHOT  +50", "KILLED EAGLE 12  +100", "", "", "" };
        for (int i = 0; i < FeedLines; i++)
        {
            MakeText(feed.transform, "Line" + (i + 1), examples[i], font, 26, -250 - i * 32, Color.white);
        }

        var sounds = new GameObject("Sounds");
        sounds.transform.SetParent(root.transform, false);
        foreach (string name in SoundNames)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "/Sounds/" + name + ".wav");
            if (clip == null)
            {
                Debug.LogWarning("Kill Popups: no sound at " + folder + "/Sounds/" + name + ".wav, that sound will be skipped.");
                continue;
            }

            var soundObject = new GameObject(name);
            soundObject.transform.SetParent(sounds.transform, false);
            var source = soundObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
        }

        return root;
    }

    static Text MakeText(Transform parent, string name, string example, Font font, int size, float y, Color color)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        var rect = (RectTransform)textObject.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400, size * 1.5f);
        rect.anchoredPosition = new Vector2(0, y);

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.color = color;
        text.text = example;

        var outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    static Font DefaultFont()
    {
#if UNITY_2022_2_OR_NEWER
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
    }

    // ScriptedBehaviour comes with the Ravenfield Tools, so it's looked up by
    // name instead of referenced directly. Its script slot is the first
    // TextAsset field.
    static bool AddScriptedBehaviour(GameObject root, TextAsset script)
    {
        Type type = null;
        foreach (Type candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
        {
            if (candidate.Name == "ScriptedBehaviour")
            {
                type = candidate;
                break;
            }
        }
        if (type == null)
        {
            Debug.LogWarning("Kill Popups: couldn't find the ScriptedBehaviour component. Is this the Ravenfield Tools project?");
            return false;
        }

        Component component = root.AddComponent(type);
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.type.Contains("TextAsset"))
            {
                property.objectReferenceValue = script;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
        }

        Debug.LogWarning("Kill Popups: added a ScriptedBehaviour but couldn't find its Source slot. Drag " + ScriptFileName + " into it by hand.");
        return false;
    }
}
