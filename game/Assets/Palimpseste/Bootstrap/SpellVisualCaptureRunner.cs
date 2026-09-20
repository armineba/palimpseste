using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Game.Library;
using Palimpseste.Game.Service;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Palimpseste.Game.Bootstrap
{
    /// <summary>
    /// Precompiled, offline presentation capture. The operator supplies a cache
    /// directory; spell data never selects paths, commands, shaders or code.
    /// This stage has no receivers, colliders, runtime mechanics or account UI.
    /// </summary>
    public sealed class SpellVisualCaptureRunner : MonoBehaviour
    {
        public const string Argument = "--palimpseste-visual-capture";
        private const int Resolution = 1024;
        private string inputDirectory, outputDirectory, packetHash;
        private CompiledSpell packet;
        private readonly List<GameObject> carriers = new List<GameObject>();
        private readonly List<object> frames = new List<object>();
        private readonly List<object> activeSamples = new List<object>();
        private Camera captureCamera;
        private RenderPipeline.StandardRequest renderRequest;
        private RenderTexture target;
        private Texture2D readback, srgb;
        private float startup;
        private int measuredFrames;
        private int completedCameraRenders;
        private double measuredSeconds;
        private bool finished;
        private Bounds compositionBounds;

        public static bool TryStartFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments,Argument);
            if (index < 0) return false;
            var host = new GameObject("Offline spell visual capture");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<SpellVisualCaptureRunner>();
            runner.inputDirectory = index + 1 < arguments.Length ? arguments[index + 1] : null;
            return true;
        }

        private void Start()
        {
            startup = Time.realtimeSinceStartup;
            StartCoroutine(Guarded(CaptureSequence()));
        }

        private IEnumerator Guarded(IEnumerator sequence)
        {
            while (true)
            {
                var more = false;
                Exception failure = null;
                try
                {
                    if (Time.realtimeSinceStartup - startup > 30)
                        throw new TimeoutException("Visual capture exceeded its presentation deadline");
                    more = sequence.MoveNext();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                if (failure != null) { Fail(failure.GetType().Name + ": " + failure.Message); yield break; }
                if (!more) yield break;
                yield return sequence.Current;
            }
        }

        private IEnumerator CaptureSequence()
        {
            LoadVerifiedPacket();
            CreateStage();
            BuildComposition();
            FitCamera();
            var introDuration = packet.plan.nodes.Max(node => node.appearance.lifecycle.intro.duration_ms) / 1000f;
            var phaseStarted = Time.time;
            while (Time.time - phaseStarted < introDuration * .45f) yield return null;
            yield return null;
            Capture("appearance");
            while (Time.time - phaseStarted < introDuration + .1f) yield return null;

            // A batch player does not automatically render its enabled cameras.
            // Count only explicit URP requests whose complete target has been
            // read back from the GPU. Empty Update ticks are not rendered FPS.
            RenderAndReadback(); // warm the renderer before measuring it
            var measuredStart = System.Diagnostics.Stopwatch.GetTimestamp();
            while (ElapsedSeconds(measuredStart) < .75)
            {
                yield return null;
                RenderAndReadback();
                measuredFrames++;
            }
            measuredSeconds = ElapsedSeconds(measuredStart);
            var activeStarted = Time.time;
            var intervals = new[] { 0f,.073f,.191f,.347f };
            var sheet = new Color32[Resolution * Resolution];
            for (var sample = 0; sample < intervals.Length; sample++)
            {
                if (sample > 0) yield return null; // advance animation even if PNG encoding passed a requested timestamp
                while (Time.time - activeStarted < intervals[sample]) yield return null;
                var sampledAt = Time.time - activeStarted;
                var file = "active_" + sample + ".png";
                var pixels = Capture("active",file,false);
                var offsetX = sample % 2 * (Resolution / 2);
                var offsetY = (1 - sample / 2) * (Resolution / 2);
                for (var y = 0; y < Resolution / 2; y++)
                    for (var x = 0; x < Resolution / 2; x++)
                    {
                        var p = y * 2 * Resolution + x * 2;
                        Color color = ((Color)pixels[p] + pixels[p + 1] + pixels[p + Resolution] + pixels[p + Resolution + 1]) * .25f;
                        sheet[(offsetY + y) * Resolution + offsetX + x] = color;
                    }
                activeSamples.Add(new { time_seconds = sampledAt, requested_time_seconds = intervals[sample],file,
                    sha256 = ParchmentStore.Hash(File.ReadAllBytes(Path.Combine(outputDirectory,file))) });
            }
            srgb.SetPixels32(sheet); srgb.Apply(false,false);
            var activeBytes = srgb.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(outputDirectory,"active.png"),activeBytes);
            frames.Add(new { phase = "active",file = "active.png",sha256 = ParchmentStore.Hash(activeBytes),
                width_px = Resolution,height_px = Resolution });

            var contactDelay = BeginEnding(true);
            phaseStarted = Time.time;
            while (Time.time - phaseStarted < contactDelay) yield return null;
            yield return null;
            Capture("contact");

            ClearComposition();
            yield return null;
            BuildComposition();
            // The camera remains fixed for all four frames. Contact and
            // expiration are independent replays of the same authored subject.
            phaseStarted = Time.time;
            while (Time.time - phaseStarted < introDuration + .1f) yield return null;
            var expirationDelay = BeginEnding(false);
            phaseStarted = Time.time;
            while (Time.time - phaseStarted < expirationDelay) yield return null;
            yield return null;
            Capture("expiration");

            var unchanged = packetHash == ParchmentStore.Hash(File.ReadAllBytes(Path.Combine(inputDirectory,"spell.json")));
            if (!unchanged) throw new InvalidDataException("Input packet changed during visual capture");
            WriteManifest(true,null);
            finished = true;
            Debug.Log("PALIMPSESTE_VISUAL_CAPTURE_OK " + packetHash);
            Application.Quit(0);
        }

        private void LoadVerifiedPacket()
        {
            if (string.IsNullOrWhiteSpace(inputDirectory) || !Path.IsPathRooted(inputDirectory))
                throw new InvalidDataException("Capture requires an absolute operator-supplied cache directory");
            inputDirectory = Path.GetFullPath(inputDirectory);
            EnsureOrdinaryPath(inputDirectory);
            if (!Directory.Exists(inputDirectory)) throw new DirectoryNotFoundException("Capture cache is absent");
            var packetPath = Path.Combine(inputDirectory,"spell.json");
            EnsureOrdinaryPath(packetPath);
            if (new FileInfo(packetPath).Length > ImageSpellPacketValidator.MaximumPacketBytes)
                throw new InvalidDataException("Capture packet exceeds two MiB");
            var bytes = File.ReadAllBytes(packetPath);
            var header = JObject.Parse(new UTF8Encoding(false,true).GetString(bytes));
            var record = new ParchmentRecord {
                spell_id = header["spell_id"]?.Value<string>(),
                parchment_id = header["parchment_id"]?.Value<string>(), state = "ready"
            };
            EnsureOrdinaryPath(Path.Combine(inputDirectory,"spell.json.sha256"));
            EnsureOrdinaryPath(Path.Combine(inputDirectory,"artifacts"));
            foreach (var file in Directory.EnumerateFiles(Path.Combine(inputDirectory,"artifacts"))) EnsureOrdinaryPath(file);
            if (!CachedSpellVerifier.TryLoad(record,inputDirectory,out var json))
                throw new InvalidDataException("Capture cache failed packet, version or artifact verification");
            packet = JsonConvert.DeserializeObject<CompiledSpell>(json,new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore, MaxDepth = 64
            });
            if (packet?.plan?.nodes == null || packet.plan.nodes.Count == 0 || packet.plan.nodes.Any(node =>
                !ImageConstructedSpellVisual.Supports(node) || node.appearance.lifecycle == null))
                throw new InvalidDataException("Capture requires an image construction and lifecycle for every node");
            var descriptionPath = Path.Combine(inputDirectory,"description.json");
            EnsureOrdinaryPath(descriptionPath);
            if (!File.Exists(descriptionPath) || new FileInfo(descriptionPath).Length > 256000)
                throw new InvalidDataException("Capture source description is absent or oversized");
            var descriptionBytes = File.ReadAllBytes(descriptionPath);
            if (ParchmentStore.Hash(descriptionBytes) != packet.description_sha256 ||
                !SpellDescriptionView.TryRead(descriptionBytes,out var description) || description.Lifecycle.Length == 0)
                throw new InvalidDataException("Capture requires the source lifecycle description");
            packetHash = ParchmentStore.Hash(bytes);
            outputDirectory = Path.Combine(inputDirectory,"output");
            EnsureOrdinaryPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                throw new InvalidOperationException("Capture requires an active HDR-capable GPU; omit -nographics");
        }

        private static void EnsureOrdinaryPath(string path)
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Capture paths cannot contain symbolic links or junctions");
                current = Path.GetDirectoryName(current);
            }
        }

        private void CreateStage()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None)) volume.enabled = false;
            Time.captureFramerate = 0;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.23f,.25f,.32f);

            var cameraObject = new GameObject("Independent visual comparison camera");
            cameraObject.transform.SetParent(transform,false);
            captureCamera = cameraObject.AddComponent<Camera>();
            // All rendering is explicit, including in non-batch invocations.
            // This avoids counting an automatic second render of the stage.
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = new Color(.018f,.023f,.034f);
            captureCamera.allowHDR = true;
            captureCamera.fieldOfView = 38;
            captureCamera.aspect = 1;
            captureCamera.nearClipPlane = .03f;
            captureCamera.farClipPlane = 200;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.volumeTrigger = captureCamera.transform;
            target = new RenderTexture(Resolution,Resolution,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            target.Create(); captureCamera.targetTexture = target;
            renderRequest = new RenderPipeline.StandardRequest { destination = target };
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            readback = new Texture2D(Resolution,Resolution,TextureFormat.RGBAFloat,false,true);
            srgb = new Texture2D(Resolution,Resolution,TextureFormat.RGB24,false,false);
            var profile = Resources.Load<VolumeProfile>("LabVfxVolume");
            if (profile == null) throw new InvalidOperationException("Precompiled lab postprocessing profile is missing");
            var volumeObject = new GameObject("Shared lab postprocessing");
            volumeObject.transform.SetParent(transform,false);
            var stageVolume = volumeObject.AddComponent<Volume>();
            stageVolume.isGlobal = true; stageVolume.priority = 100; stageVolume.sharedProfile = profile;
            StageLight("Key",Quaternion.Euler(48,-35,0),new Color(.79f,.86f,1),1.1f);
            StageLight("Rim",Quaternion.Euler(32,145,0),new Color(.45f,.64f,1),.65f);
        }

        private void StageLight(string label, Quaternion rotation, Color color, float intensity)
        {
            var host = new GameObject(label);
            host.transform.SetParent(transform,false); host.transform.rotation = rotation;
            var light = host.AddComponent<Light>();
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void BuildComposition()
        {
            foreach (var node in packet.plan.nodes)
            {
                // Contact/expiration children are shown only in their branch.
                if (node.activation.parent_id != null && node.activation.@event != "spawn" && node.activation.@event != "tick") continue;
                CreateNode(node);
            }
            if (carriers.Count == 0) throw new InvalidDataException("Capture contains no root subject");
        }

        private void CreateNode(SpellNode node)
        {
            var host = new GameObject("Capture subject " + node.node_id);
            host.transform.SetParent(transform,false);
            // Every carrier is held in its local authored coordinate system.
            // Actual launch, travel and collision are deliberately not inferred.
            host.AddComponent<ImageConstructedSpellVisual>().Initialize(node);
            var tint = CarrierVisual.ColorFor(node.appearance.palette,node.appearance.affinity);
            host.AddComponent<SpellVfxComposition>().Initialize(node,tint,Vector3.zero,
                Mathf.Clamp(node.scale_cm / 100f * .25f,.4f,2f));
            carriers.Add(host);
        }

        private void FitCamera()
        {
            var initialized = false;
            foreach (var node in packet.plan.nodes)
                foreach (var part in node.appearance.construction.parts)
                {
                    var center = new Vector3(part.position_cm[0],part.position_cm[1],part.position_cm[2]) / 100f;
                    var extent = new Vector3(part.scale_cm[0],part.scale_cm[1],part.scale_cm[2]) / 200f;
                    var radius = extent.magnitude + (part.motion?.amplitude_cm ?? 0) / 100f;
                    var bounds = new Bounds(center,Vector3.one * Mathf.Max(.1f,radius * 2));
                    if (part.points_cm != null)
                        foreach (var point in part.points_cm)
                            bounds.Encapsulate(center + new Vector3(point[0],point[1],point[2]) / 100f);
                    if (!initialized) { compositionBounds = bounds; initialized = true; }
                    else compositionBounds.Encapsulate(bounds);
                }
            // Reserve moderate space for the declared terminal spread. The
            // same camera/framing is used for entrance, active and both ends.
            var spread = packet.plan.nodes.Max(node => Mathf.Max(node.appearance.lifecycle.contact.spread_cm,
                node.appearance.lifecycle.expiration.spread_cm)) / 100f;
            var radiusFit = Mathf.Max(1.5f,compositionBounds.extents.magnitude + spread * .35f);
            var distance = radiusFit / Mathf.Sin(captureCamera.fieldOfView * Mathf.Deg2Rad * .5f) * 1.1f;
            captureCamera.transform.position = compositionBounds.center + new Vector3(1.25f,.5f,-1.6f).normalized * distance;
            captureCamera.transform.LookAt(compositionBounds.center);
        }

        private float BeginEnding(bool contact)
        {
            var minimumDuration = 3f;
            foreach (var host in carriers)
            {
                var duration = host.GetComponent<ImageConstructedSpellVisual>().RetireForCause(contact);
                host.GetComponent<SpellVfxComposition>()?.DissolveWake(duration);
                minimumDuration = Mathf.Min(minimumDuration,duration);
            }
            foreach (var node in packet.plan.nodes)
            {
                if (node.activation.parent_id == null) continue;
                var trigger = node.activation.@event;
                if (contact ? trigger == "hit" || trigger == "block" || trigger == "trigger" : trigger == "expire")
                    CreateNode(node);
            }
            return Mathf.Max(.025f,minimumDuration * .35f);
        }

        private Color32[] Capture(string phase, string filename = null, bool includePhase = true)
        {
            RenderAndReadback();
            var pixels = readback.GetPixels();
            var minimum = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
            var maximum = new Vector3(float.MinValue,float.MinValue,float.MinValue);
            for (var i = 0; i < pixels.Length; i++)
            {
                var color = pixels[i];
                if (!Finite(color.r) || !Finite(color.g) || !Finite(color.b))
                    throw new InvalidDataException("GPU capture contains non-finite pixels: " + phase);
                var rgb = new Vector3(color.r,color.g,color.b);
                minimum = Vector3.Min(minimum,rgb); maximum = Vector3.Max(maximum,rgb);
                // The render target retains linear HDR through URP's bloom
                // and tone mapping. Encode display sRGB only after readback.
                pixels[i] = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.gamma : color;
            }
            var variation = maximum - minimum;
            if (Mathf.Max(variation.x,Mathf.Max(variation.y,variation.z)) < .0001f)
                throw new InvalidDataException("GPU capture is uniform; no visible spell rendered: " + phase);
            srgb.SetPixels(pixels); srgb.Apply(false,false);
            var bytes = srgb.EncodeToPNG();
            if (bytes == null || bytes.Length < 1000) throw new InvalidDataException("GPU capture produced no image");
            var file = filename ?? phase + ".png";
            var path = Path.Combine(outputDirectory,file);
            EnsureOrdinaryPath(path);
            File.WriteAllBytes(path,bytes);
            if (includePhase) frames.Add(new { phase,file,sha256 = ParchmentStore.Hash(bytes),width_px = Resolution,height_px = Resolution });
            return srgb.GetPixels32();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static double ElapsedSeconds(long started) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - started) / (double)System.Diagnostics.Stopwatch.Frequency;

        private void OnCameraRendered(ScriptableRenderContext context, Camera camera)
        {
            if (camera == captureCamera) completedCameraRenders++;
        }

        private void RenderAndReadback()
        {
            if (captureCamera == null || target == null || !target.IsCreated() || renderRequest == null)
                throw new InvalidOperationException("Explicit GPU capture is not initialized");
            var previous = RenderTexture.active;
            var before = completedCameraRenders;
            try
            {
                // A StandardRequest runs URP's full frame initialization and
                // this single camera's postprocessing in batch mode. The stage
                // has no overlay stack. Unlike SingleCameraRequest this also
                // sets per-frame lighting, shader and renderer globals.
                // SubmitRenderRequest also initializes the active pipeline
                // when there has been no automatic camera frame yet.
                RenderPipeline.SubmitRenderRequest(captureCamera,renderRequest);
                if (completedCameraRenders <= before)
                    throw new InvalidOperationException("URP did not complete the requested capture camera");
                RenderTexture.active = target;
                // Full-frame synchronous readback waits for submitted GPU
                // work. A callback alone would only prove CPU submission.
                readback.ReadPixels(new Rect(0,0,Resolution,Resolution),0,0,false);
            }
            finally { RenderTexture.active = previous; }
        }

        private void WriteManifest(bool completed, string error)
        {
            if (outputDirectory == null) return;
            var path = Path.Combine(outputDirectory,"capture.json");
            EnsureOrdinaryPath(path);
            File.WriteAllText(path,JsonConvert.SerializeObject(new {
                schema_version = "sp.visual-capture/1.0", spell_sha256 = packetHash,
                visual_reference_sha256 = packet?.visual_reference?.sha256,
                completed, presentation_only = true, gameplay_executed = false, provider_calls = 0,
                measured_fps = measuredSeconds > 0 ? measuredFrames / measuredSeconds : 0,
                measured_frames = measuredFrames, measured_seconds = measuredSeconds,
                measurement_scope = "Explicit 1024x1024 URP requests completed with synchronous full-frame GPU readback; includes readback cost, excludes startup and PNG encoding; not gameplay FPS",
                capture_method = "URP StandardRequest for one camera, including full frame initialization; separate precompiled three-quarter stage; carrier positions fixed; real lifecycle animation; independent contact and expiration replay",
                completed_camera_renders = completedCameraRenders,
                active_samples = activeSamples,
                active_layout = "2x2 temporal contact sheet; chronological left-to-right, top-to-bottom; actual times in active_samples",
                color_encoding = QualitySettings.activeColorSpace == ColorSpace.Linear ? "Linear HDR readback converted to display sRGB" : "Gamma project readback retained",
                camera_position = captureCamera == null ? null : new[] { captureCamera.transform.position.x,captureCamera.transform.position.y,captureCamera.transform.position.z },
                camera_field_of_view = captureCamera == null ? 0 : captureCamera.fieldOfView,
                elapsed_seconds = Time.realtimeSinceStartup - startup, graphics_device = SystemInfo.graphicsDeviceName,
                art_acceptance = "not_claimed", frames,error
            },Formatting.Indented),new UTF8Encoding(false));
        }

        private void Fail(string error)
        {
            if (finished) return;
            finished = true;
            try { WriteManifest(false,error); } catch { /* Keep the original failure in the private process log. */ }
            Debug.LogError("PALIMPSESTE_VISUAL_CAPTURE_FAILED " + error);
            Application.Quit(2);
        }

        private void ClearComposition()
        {
            foreach (var host in carriers) if (host != null) Destroy(host);
            carriers.Clear();
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= OnCameraRendered;
            ClearComposition();
            if (target != null) { target.Release(); Destroy(target); }
            if (readback != null) Destroy(readback);
            if (srgb != null) Destroy(srgb);
        }
    }
}
