using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class SemanticVisualPresentationTests
    {
        // Explicit graphical inspection fixture. It is never a player spell,
        // provider result, or a substitute for the creator's visual verdict.
        [UnityTest]
        public IEnumerator RenderInterpretedObjectsForReview()
        {
            var output = Environment.GetEnvironmentVariable("PALIMPSESTE_SEMANTIC_PREVIEW_PATH");
            if (string.IsNullOrWhiteSpace(output)) Assert.Ignore("Graphical preview output was not requested");
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "A visual evidence capture requires a real graphics device");
            var host = new GameObject("Semantic renderer review fixture");
            var renderTexture = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previousAmbient = RenderSettings.ambientLight;
            var previousAmbientMode = RenderSettings.ambientMode;
            var ownedMaterials = new System.Collections.Generic.List<Material>();
            Texture2D image = null;
            try
            {
                const int layer = 30;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.30f, .34f, .40f);
                var cameraObject = new GameObject("Review camera");
                cameraObject.transform.SetParent(host.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.021f, .033f, .054f);
                camera.allowHDR = true;
                camera.fieldOfView = 38;
                camera.targetTexture = renderTexture;
                camera.transform.position = new Vector3(0, 8.6f, -13.7f);
                camera.transform.LookAt(new Vector3(0, .2f, 1.6f));
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.volumeLayerMask = 1 << layer;
                var profile = Resources.Load<VolumeProfile>("LabVfxVolume");
                Assert.NotNull(profile, "The shipping postprocessing profile must be included");
                var volume = host.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 100;
                volume.sharedProfile = profile;
                var key = new GameObject("Review key");
                key.transform.SetParent(host.transform, false);
                key.transform.rotation = Quaternion.Euler(42, -35, 0);
                var light = key.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, .9f, .78f);
                light.intensity = 2.1f;
                light.shadows = LightShadows.Soft;
                light.cullingMask = 1 << layer;
                var fill = new GameObject("Review rim");
                fill.transform.SetParent(host.transform, false);
                fill.transform.rotation = Quaternion.Euler(25, 155, 0);
                var rim = fill.AddComponent<Light>();
                rim.type = LightType.Directional;
                rim.color = new Color(.35f, .6f, 1f);
                rim.intensity = .85f;
                rim.cullingMask = 1 << layer;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.SetParent(host.transform, false);
                floor.transform.position = new Vector3(0, -.27f, 1.6f);
                floor.transform.localScale = new Vector3(13, .35f, 10);
                var floorMaterial = SpellLab.MaterialFor(new Color(.064f, .077f, .10f), false);
                ownedMaterials.Add(floorMaterial);
                floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                var forms = new[] { "boulder", "crystal", "meteor", "wolf", "golem", "vortex" };
                var colors = new[] { new Color(.58f,.43f,.28f), new Color(.28f,.77f,1f),
                    new Color(1f,.22f,.04f), new Color(.33f,.73f,1f), new Color(.42f,.86f,.54f),
                    new Color(.66f,.25f,1f) };
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                for (var i = 0; i < forms.Length; i++)
                {
                    var position = new Vector3((i % 3 - 1) * 3.6f, 1.15f, i < 3 ? -.6f : 3.8f);
                    var subject = new GameObject("Review " + forms[i]);
                    subject.transform.SetParent(host.transform, false);
                    subject.transform.position = position;
                    subject.transform.rotation = Quaternion.Euler(0, 155, 0);
                    subject.AddComponent<SemanticSpellVisual>().Initialize(new SpellNode {
                        carrier = "projectile", scale_cm = 150,
                        appearance = new SpellAppearance { form = forms[i], palette = "arcane", pattern = "solid" },
                        options = new SpellOptions { radius_cm = 65 },
                        activation = new SpellActivation()
                    }, colors[i]);
                    var captionObject = new GameObject("Caption " + forms[i]);
                    captionObject.transform.SetParent(host.transform, false);
                    captionObject.transform.position = position + new Vector3(0, -1.02f, -1.25f);
                    captionObject.transform.rotation = camera.transform.rotation;
                    var caption = captionObject.AddComponent<TextMesh>();
                    caption.text = forms[i].ToUpperInvariant();
                    caption.font = font;
                    caption.fontSize = 52;
                    caption.characterSize = .066f;
                    caption.anchor = TextAnchor.MiddleCenter;
                    caption.color = new Color(.76f, .81f, .86f);
                    captionObject.GetComponent<Renderer>().sharedMaterial = font.material;
                }
                foreach (var item in host.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = layer;
                yield return new WaitForSeconds(.7f);
                // Read the preceding rendered frame; WaitForEndOfFrame does
                // not resume in every batch editor configuration.
                yield return null;
                var oldTarget = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;
                    image = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                    image.Apply();
                }
                finally { RenderTexture.active = oldTarget; }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllBytes(output, image.EncodeToPNG());
                Assert.Greater(new FileInfo(output).Length, 10000, "The graphics capture must contain rendered scene data");
                Debug.Log("PALIMPSESTE_SEMANTIC_PREVIEW " + output);
            }
            finally
            {
                Object.Destroy(host);
                if (image != null) Object.Destroy(image);
                foreach (var material in ownedMaterials) Object.Destroy(material);
                renderTexture.Release();
                Object.Destroy(renderTexture);
                RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientMode = previousAmbientMode;
            }
            yield return null;
        }
    }
}
