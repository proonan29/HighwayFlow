#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HighwayFlow.EditorTools
{
    /// <summary>
    /// MeshUtil.Lit() finds shaders by name at runtime. In a player build a shader that no asset references is
    /// stripped, Shader.Find returns null and the 3D world turns invisible (only the UI Toolkit panel is left).
    /// This adds the shaders to Graphics Settings > Always Included Shaders, on editor load and before every build.
    /// </summary>
    [InitializeOnLoad]
    public class HighwayFlowShaderSetup : IPreprocessBuildWithReport
    {
        static readonly string[] Shaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit",
            "GUI/Text Shader",
            "Sprites/Default",
        };

        static HighwayFlowShaderSetup() { EditorApplication.delayCall += () => Ensure(false); }

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { Ensure(true); }

        [MenuItem("HighwayFlow/Include Runtime Shaders In Build")]
        static void Menu() { Ensure(true); }

        static void Ensure(bool log)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            bool changed = false;
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { if (log) Debug.LogWarning("[HighwayFlow] shader not found: " + name); continue; }
                bool have = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) { have = true; break; }
                if (have) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
                changed = true;
                if (log) Debug.Log("[HighwayFlow] added to Always Included Shaders: " + name);
            }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
        }
    }
}
#endif
