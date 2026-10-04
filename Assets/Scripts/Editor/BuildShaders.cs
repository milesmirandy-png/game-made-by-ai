#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swat.EditorTools
{
    // The game makes its materials in code at run time, so no saved material
    // points at the lit shader they use. A build only includes shaders that
    // something saved points at, so without help a built game draws the world
    // bright pink (the editor has every shader, so it looks fine there). This
    // adds those shaders to Project Settings -> Graphics -> Always Included
    // Shaders when the project opens and again before every build.
    [InitializeOnLoad]
    public class BuildShaders : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        static BuildShaders()
        {
            EditorApplication.delayCall += () => Include(false);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Include(true);
        }

        [MenuItem("SWAT/Include Shaders In Builds")]
        static void IncludeMenu()
        {
            int added = Include(true);
            Debug.Log(added > 0 ? "SWAT: added " + added + " shader(s) to Always Included Shaders." : "SWAT: the game's shaders are already included in builds.");
        }

        static int Include(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return 0;
            var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null) return 0;
            var serialized = new SerializedObject(settings);
            var list = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return 0;

            int added = 0;
            foreach (var shader in Needed())
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                if (present) continue;
                int index = list.arraySize;
                list.InsertArrayElementAtIndex(index);
                list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
                added++;
                if (log) Debug.Log("SWAT: including shader '" + shader.name + "' in builds.");
            }
            if (added > 0)
            {
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            return added;
        }

        // The render pipeline's default lit shader (what the game's materials copy), plus the
        // standard fallbacks the game's own shaders and particles can use.
        static List<Shader> Needed()
        {
            var shaders = new List<Shader>();
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.hideFlags = HideFlags.HideAndDontSave;
            var material = probe.GetComponent<Renderer>().sharedMaterial;
            if (material != null && material.shader != null) shaders.Add(material.shader);
            Object.DestroyImmediate(probe);
            string[] names = GraphicsSettings.currentRenderPipeline == null
                ? new[] { "Standard", "Sprites/Default", "Legacy Shaders/VertexLit" }
                : new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Sprites/Default" };
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader != null && !shaders.Contains(shader)) shaders.Add(shader);
            }
            return shaders;
        }
    }
}
#endif
