#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Palimpseste.Game.Editor
{
    public static class BuildPalimpseste
    {
        private static readonly string[] Scenes =
        {
            "Assets/Palimpseste/Scenes/Bootstrap.unity",
            "Assets/Palimpseste/Scenes/SpellLab.unity"
        };

        [MenuItem("Palimpseste/Construire Windows x64 IL2CPP")]
        public static void BuildWindows()
        {
            PrepareMaterials();
            foreach (var scene in Scenes)
                if (!File.Exists(scene)) throw new FileNotFoundException("Scène manquante", scene);
            EditorBuildSettings.scenes = Array.ConvertAll(Scenes, scene => new EditorBuildSettingsScene(scene, true));
            PlayerSettings.companyName = "Palimpseste";
            PlayerSettings.productName = "Palimpseste Spell Lab";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            var directory = Environment.GetEnvironmentVariable("PALIMPSESTE_BUILD_DIR");
            if (string.IsNullOrWhiteSpace(directory)) directory = Path.GetFullPath(Path.Combine("Build", "Windows"));
            Directory.CreateDirectory(directory);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(directory, "Palimpseste.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Build Unity échoué : " + report.summary.result + ", erreurs=" + report.summary.totalErrors);
            UnityEngine.Debug.Log("PALIMPSESTE_BUILD_OK " + report.summary.outputPath + " bytes=" + report.summary.totalSize);
        }

        [MenuItem("Palimpseste/Préparer les matériaux URP")]
        public static void PrepareMaterials()
        {
            const string path = "Assets/Palimpseste/Resources/LabOpaque.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Shader URP Lit introuvable");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "LabOpaque" };
                material.SetFloat("_Smoothness", .28f);
                material.SetFloat("_Metallic", .07f);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("PALIMPSESTE_LIT_MATERIAL_OK " + path);
        }
    }
}
#endif
