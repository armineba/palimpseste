#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            PlayerSettings.bundleVersion = "1.2.0";
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
            // A real Resources material keeps the _EMISSION shader_feature
            // variant in the Player. Enabling it only on a runtime clone lets
            // Unity strip the variant even when Editor previews look correct.
            const string emissivePath = "Assets/Palimpseste/Resources/LabEmissive.mat";
            var emissive = AssetDatabase.LoadAssetAtPath<Material>(emissivePath);
            if (emissive == null)
            {
                emissive = new Material(material) { name = "LabEmissive" };
                AssetDatabase.CreateAsset(emissive, emissivePath);
            }
            emissive.shader = shader;
            emissive.EnableKeyword("_EMISSION");
            emissive.SetColor("_EmissionColor", Color.white * 2f);
            emissive.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(emissive);
            PrepareVfxProfile();
            AssetDatabase.SaveAssets();
            UnityEngine.Debug.Log("PALIMPSESTE_LIT_MATERIAL_OK " + path);
        }

        private static void PrepareVfxProfile()
        {
            const string path = "Assets/Palimpseste/Resources/LabVfxVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "LabVfxVolume";
                AssetDatabase.CreateAsset(profile, path);
            }
            var bloom = Component<Bloom>(profile);
            bloom.intensity.Override(.7f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(.65f);
            bloom.clamp.Override(8f);
            var tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);
            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(7f);
            color.saturation.Override(3f);
            EditorUtility.SetDirty(profile);
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(component);
            return component;
        }
    }
}
#endif
