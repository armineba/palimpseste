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
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Palimpseste.Game.Bootstrap
{
    /// <summary>Precompiled Pipeline V2 evidence producer. It never opens a player library,
    /// calls a provider, imports assets or builds code. Execution is not artistic acceptance.</summary>
    public sealed class SpellV2ValidationRunner : MonoBehaviour
    {
        public const string Argument = "--palimpseste-v2-validation";
        private const int Resolution = 512, SheetWidth = 1536, SheetHeight = 1152;
        private string directory, output, mode, packetHash, blueprintsHash, packetJson;
        private CompiledSpell packet;
        private readonly List<CanonicalSpellVisualV2> visuals = new List<CanonicalSpellVisualV2>();
        private readonly List<object> frames = new List<object>();
        private readonly List<object> topologySamples = new List<object>();
        private readonly List<object> phaseSamples = new List<object>();
        private readonly List<object> impactScenarios = new List<object>();
        private readonly List<object> realtimeSamples = new List<object>();
        private readonly List<object> motionSheetSamples = new List<object>();
        private readonly List<object> coreGalleryNodes = new List<object>();
        private readonly List<object> activeLoopSamples = new List<object>();
        private readonly List<object> gates = new List<object>();
        private readonly List<object> visualMetrics = new List<object>();
        private readonly Dictionary<CanonicalSpellVisualV2, V2VisualMetrics> peakMetrics = new Dictionary<CanonicalSpellVisualV2, V2VisualMetrics>();
        private readonly Dictionary<CanonicalSpellVisualV2, string> nodeIds = new Dictionary<CanonicalSpellVisualV2, string>();
        private readonly Dictionary<CanonicalSpellVisualV2, SpellNode> previewNodes = new Dictionary<CanonicalSpellVisualV2, SpellNode>();
        private readonly List<ValidationRulesV2> capturedBudgets = new List<ValidationRulesV2>();
        private readonly List<string> capturedNodeIds = new List<string>();
        private Camera stageCamera;
        private Volume stageVolume;
        private Material validationSurface, validationTarget;
        private RenderTexture target;
        private Texture2D linear, display;
        private RenderPipeline.StandardRequest request;
        private float started;
        private bool finished, continuityPassed, impactsPassed, hierarchyPassed = true;
        private double renderSeconds, sampledCpuSeconds;
        private int renders, requestedRenders;
        private Bounds bounds;
        private float compositionDuration;

        public static bool TryStartFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, Argument);
            if (index < 0) return false;
            var host = new GameObject("Isolated V2 validation");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<SpellV2ValidationRunner>();
            runner.directory = index + 1 < arguments.Length ? arguments[index + 1] : null;
            var modeIndex = Array.IndexOf(arguments, "--v2-mode");
            runner.mode = modeIndex >= 0 && modeIndex + 1 < arguments.Length ? arguments[modeIndex + 1] : null;
            return true;
        }

        private void Start()
        {
            started = Time.realtimeSinceStartup;
            StartCoroutine(Guarded(Run()));
        }

        private IEnumerator Guarded(IEnumerator sequence)
        {
            while (true)
            {
                bool more;
                try
                {
                    if (Time.realtimeSinceStartup - started > 155) throw new TimeoutException("V2 validation deadline");
                    more = sequence.MoveNext();
                }
                catch (Exception e) { Fail(e.GetType().Name + ": " + e.Message); yield break; }
                if (!more) yield break;
                yield return sequence.Current;
            }
        }

        private IEnumerator Run()
        {
            Load(); CreateStage(); BuildSubjects(mode == "core");
            MeasureContinuity();
            foreach (var visual in visuals) { visualMetrics.Add(peakMetrics[visual]); capturedBudgets.Add(visual.Blueprint.validation_rules); capturedNodeIds.Add(visual.NodeId); }
            if (mode == "core")
            {
                var gallery = CaptureCoreGallery(); while (gallery.MoveNext()) yield return gallery.Current;
            }
            var sheet = CaptureSheet(mode == "core" ? "core_sheet" : "normal_sheet");
            while (sheet.MoveNext()) yield return sheet.Current;
            gates.Add(new { gate = "A_structure", status = "requires_visual_review", reason = "Solid core silhouette evidence; readability and blind semantic identity require independent review" });
            gates.Add(new { gate = "B_continuity", status = continuityPassed ? "technical_pass" : "failed", reason = "121 deterministic samples of the same mesh; finite vertices, bounded displacement, invariant topology. Visual temporal review remains required" });
            if (mode == "full")
            {
                var motion = CaptureRealtime(); while (motion.MoveNext()) yield return motion.Current;
                ClearSubjects(); yield return null;
                var physics = CapturePhysics(); while (physics.MoveNext()) yield return physics.Current;
                var camera = CaptureGameCamera(); while (camera.MoveNext()) yield return camera.Current;
                gates.Add(new { gate = "C_rendering", status = "requires_visual_review", reason = "Compare normal_sheet against approved core_sheet; particles must not define the silhouette" });
                gates.Add(new { gate = "D_impact", status = impactsPassed && hierarchyPassed ? "technical_pass" : "failed", reason = "Four unmodified RuntimeEngine casts with real Unity colliders; contact and decorative-physics ownership recorded separately per scenario; contact artistry still reviewed" });
                gates.Add(new { gate = "E_game_camera", status = "requires_visual_review", reason = "game_camera comes from actual SpellLab.BuildScene camera, default gameplay distance and viewport" });
                gates.Add(new { gate = "F_motion", status = "requires_visual_review", reason = "Real SpellLab cast with RuntimeEngine ticks paced at 50 Hz; actual trajectories, contacts and event children; timestamped consecutive frames over declared lifetime and terminal survival, no time compression" });
            }
            gates.Add(new { gate = "semantic_blind", status = "requires_visual_review", reason = "Reviewer must receive core image without intent, title or drawing before comparison" });
            var performancePassed = visualMetrics.Select((metric, index) => WithinBudget((V2VisualMetrics)metric, capturedBudgets[index])).All(value => value);
            gates.Add(new { gate = "performance", status = performancePassed ? "technical_pass" : "failed", reason = "Measured geometry, materials, peak particles and CPU sampling plus declared renderer-submission estimate meet known budgets. GPU cost, transparent overdraw and actual draw calls remain explicitly unmeasured" });
            WriteManifest(true, null);
            finished = true;
            Debug.Log("PALIMPSESTE_V2_VALIDATION_COMPLETED " + mode);
            Application.Quit(0);
        }

        private void Load()
        {
            if (mode != "core" && mode != "full") throw new InvalidDataException("V2 mode must be core or full");
            if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory)) throw new InvalidDataException("Absolute cache path required");
            directory = Path.GetFullPath(directory); Ordinary(directory);
            var packetPath = Path.Combine(directory, "spell.json"); Ordinary(packetPath);
            if (!File.Exists(packetPath) || new FileInfo(packetPath).Length > ImageSpellPacketValidator.MaximumPacketBytes)
                throw new InvalidDataException("V2 packet absent or oversized");
            var bytes = File.ReadAllBytes(packetPath); packetHash = ParchmentStore.Hash(bytes);
            var header = JObject.Parse(new UTF8Encoding(false, true).GetString(bytes));
            Ordinary(Path.Combine(directory, "spell.json.sha256")); Ordinary(Path.Combine(directory, "artifacts"));
            foreach (var file in Directory.EnumerateFiles(Path.Combine(directory, "artifacts"))) Ordinary(file);
            var record = new ParchmentRecord { spell_id = header["spell_id"]?.Value<string>(), parchment_id = header["parchment_id"]?.Value<string>(), state = "ready" };
            if (!CachedSpellVerifier.TryLoad(record, directory, out packetJson)) throw new InvalidDataException("V2 cache verification failed");
            packet = JsonConvert.DeserializeObject<CompiledSpell>(packetJson, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 64 });
            if (packet?.plan?.nodes == null || packet.plan.nodes.Count == 0 || packet.plan.nodes.Any(n => n.blueprint_v2?.schema_version != "sp.blueprint/2.0"))
                throw new InvalidDataException("V2 blueprint required on every node");
            var blueprintPath = Path.Combine(directory, "blueprints.json"); Ordinary(blueprintPath);
            if (!File.Exists(blueprintPath) || new FileInfo(blueprintPath).Length > 1024 * 1024) throw new InvalidDataException("V2 blueprint envelope absent");
            var blueprintBytes = File.ReadAllBytes(blueprintPath); blueprintsHash = ParchmentStore.Hash(blueprintBytes);
            var expected = new JArray(((JArray)header["plan"]["nodes"]).Select(n => n["blueprint_v2"].DeepClone()));
            if (!JToken.DeepEquals(expected, JArray.Parse(new UTF8Encoding(false, true).GetString(blueprintBytes))))
                throw new InvalidDataException("V2 blueprint envelope differs from compiled plan");
            var descriptionPath = Path.Combine(directory, "description.json"); Ordinary(descriptionPath);
            if (!File.Exists(descriptionPath) || new FileInfo(descriptionPath).Length > 256000 || ParchmentStore.Hash(File.ReadAllBytes(descriptionPath)) != packet.description_sha256)
                throw new InvalidDataException("Source description binding failed");
            output = Path.Combine(directory, "output"); Ordinary(output); Directory.CreateDirectory(output);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                throw new InvalidOperationException("V2 capture requires an active HDR GPU");
        }

        private static void Ordinary(string path)
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("V2 evidence paths cannot contain reparse points");
                current = Path.GetDirectoryName(current);
            }
        }

        private void CreateStage()
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)) camera.enabled = false;
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None)) volume.enabled = false;
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            Time.captureDeltaTime = 0; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.25f, .27f, .32f);
            var cameraHost = new GameObject("V2 canonical camera"); cameraHost.transform.SetParent(transform, false);
            stageCamera = cameraHost.AddComponent<Camera>(); stageCamera.enabled = false;
            stageCamera.clearFlags = CameraClearFlags.SolidColor; stageCamera.backgroundColor = new Color(.015f, .019f, .027f);
            stageCamera.allowHDR = true; stageCamera.fieldOfView = 38; stageCamera.aspect = 1; stageCamera.nearClipPlane = .03f; stageCamera.farClipPlane = 200;
            var data = cameraHost.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = mode == "full";
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.volumeTrigger = cameraHost.transform;
            var volumeHost = new GameObject("V2 presentation postprocessing"); volumeHost.transform.SetParent(transform, false);
            stageVolume = volumeHost.AddComponent<Volume>(); stageVolume.isGlobal = true; stageVolume.priority = 100;
            stageVolume.sharedProfile = Resources.Load<VolumeProfile>("LabVfxVolume"); stageVolume.enabled = mode == "full";
            var lightHost = new GameObject("V2 key"); lightHost.transform.SetParent(transform, false);
            lightHost.transform.rotation = Quaternion.Euler(45, -30, 0);
            var key = lightHost.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.2f; key.shadows = LightShadows.None;
            // Runtime CreatePrimitive uses Unity's default material, which is not
            // guaranteed to be an included URP material in a stripped Player.
            // These are private copies; the historical LabOpaque asset is untouched.
            validationSurface = SpellLab.MaterialFor(new Color(.14f, .17f, .22f), false);
            validationSurface.name = "V2 validation surface (runtime copy)";
            validationTarget = SpellLab.MaterialFor(new Color(.27f, .31f, .38f), false);
            validationTarget.name = "V2 validation receiver (runtime copy)";
            target = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear); target.Create();
            linear = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true);
            display = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, false);
            request = new RenderPipeline.StandardRequest { destination = target };
            RenderPipelineManager.endCameraRendering += CameraRendered;
        }

        private void BuildSubjects(bool coreOnly)
        {
            var first = true;
            compositionDuration = packet.plan.nodes.Where(n => n.activation.parent_id == null)
                .Max(n => n.activation.delay_ticks * .02f + n.blueprint_v2.motion.duration_ms * .001f);
            foreach (var node in packet.plan.nodes)
            {
                if (node.activation.parent_id != null) continue;
                var host = new GameObject("Spell_ROOT " + node.node_id); host.transform.SetParent(transform, false);
                var visual = host.AddComponent<CanonicalSpellVisualV2>(); visual.Initialize(node);
                visual.SetManualSampling(true); visual.SetCoreOnly(coreOnly); visual.SampleNormalized(.4f); visuals.Add(visual);
                nodeIds[visual] = node.node_id;
                previewNodes[visual] = node;
                if (first) { bounds = visual.CoreBounds; first = false; } else bounds.Encapsulate(visual.CoreBounds);
            }
            if (visuals.Count == 0) throw new InvalidDataException("V2 capture requires a root");
            var radius = Mathf.Max(1, bounds.extents.magnitude);
            stageCamera.transform.position = bounds.center + new Vector3(1.2f, .6f, -1.8f).normalized * radius / Mathf.Sin(19 * Mathf.Deg2Rad) * 1.18f;
            stageCamera.transform.LookAt(bounds.center);
        }

        private void Sample(float time)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var visual in visuals)
            {
                var node = previewNodes[visual];
                var localSeconds = time * compositionDuration - node.activation.delay_ticks * .02f;
                visual.gameObject.SetActive(localSeconds >= 0);
                visual.SampleNormalized(Mathf.Clamp01(localSeconds / (node.blueprint_v2.motion.duration_ms * .001f)));
                var current = visual.ReadMetrics();
                if (!peakMetrics.TryGetValue(visual, out var peak)) peakMetrics[visual] = current;
                else
                {
                    MergeMetrics(peak, current);
                }
            }
            sampledCpuSeconds += watch.Elapsed.TotalSeconds;
        }

        private static bool WithinBudget(V2VisualMetrics metrics, ValidationRulesV2 rules) =>
            metrics.mesh_vertices <= rules.maximum_vertices && metrics.particle_count <= rules.maximum_particles &&
            metrics.material_count <= rules.maximum_materials && metrics.estimated_draw_calls <= rules.maximum_draw_calls &&
            metrics.transparent_layers <= rules.maximum_transparent_layers && metrics.cpu_update_microseconds <= rules.maximum_cpu_microseconds &&
            metrics.decorative_colliders == 0 && metrics.decorative_rigidbodies == 0;

        private static void MergeMetrics(V2VisualMetrics peak, V2VisualMetrics current)
        {
            peak.particle_count = Math.Max(peak.particle_count, current.particle_count);
            peak.mesh_vertices = Math.Max(peak.mesh_vertices, current.mesh_vertices);
            peak.material_count = Math.Max(peak.material_count, current.material_count);
            peak.estimated_draw_calls = Math.Max(peak.estimated_draw_calls, current.estimated_draw_calls);
            peak.transparent_layers = Math.Max(peak.transparent_layers, current.transparent_layers);
            peak.cpu_update_microseconds = Math.Max(peak.cpu_update_microseconds, current.cpu_update_microseconds);
            peak.decorative_colliders = Math.Max(peak.decorative_colliders, current.decorative_colliders);
            peak.decorative_rigidbodies = Math.Max(peak.decorative_rigidbodies, current.decorative_rigidbodies);
        }

        private void RecordRuntimeMetrics(IEnumerable<CanonicalSpellVisualV2> spawned)
        {
            foreach (var visual in spawned)
            {
                if (visual == null) continue;
                var index = capturedNodeIds.IndexOf(visual.NodeId);
                if (index >= 0) MergeMetrics((V2VisualMetrics)visualMetrics[index], visual.ReadMetrics());
                else
                {
                    // Event children are born only by the actual engine. Their
                    // runtime budget enters the report when genuinely observed.
                    capturedNodeIds.Add(visual.NodeId); visualMetrics.Add(visual.ReadMetrics()); capturedBudgets.Add(visual.Blueprint.validation_rules);
                }
            }
        }

        private void MeasureContinuity()
        {
            continuityPassed = true;
            var previous = new Dictionary<CanonicalSpellVisualV2, Vector3[]>();
            var fingerprints = visuals.ToDictionary(v => v, v => v.CoreTopologyFingerprint);
            for (var sample = 0; sample <= 120; sample++)
            {
                var time = sample / 120f; Sample(time);
                foreach (var visual in visuals)
                {
                    var vertices = visual.GetCoreVertices();
                    var finite = vertices != null && vertices.Length > 0 && vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z));
                    var topologyStable = visual.CoreTopologyFingerprint == fingerprints[visual];
                    topologyStable &= visual.CanonicalConnectedComponents == visual.Blueprint.identity.element_count;
                    var maximumDelta = 0f;
                    if (previous.TryGetValue(visual, out var last))
                    {
                        topologyStable &= last.Length == vertices.Length;
                        if (last.Length == vertices.Length) for (var i = 0; i < vertices.Length; i++) maximumDelta = Mathf.Max(maximumDelta, Vector3.Distance(vertices[i], last[i]));
                    }
                    // Deliberately broad numeric guard: semantic motion continuity is still a visual gate.
                    var bounded = maximumDelta <= Mathf.Max(.5f, bounds.size.magnitude * .8f);
                    continuityPassed &= finite && topologyStable && bounded;
                    topologySamples.Add(new { normalized_milli = Mathf.RoundToInt(time * 1000), node = nodeIds[visual],
                        topology_sha256 = visual.CoreTopologyFingerprint, vertices = vertices.Length, finite, topology_stable = topologyStable,
                        maximum_vertex_displacement_m = maximumDelta, displacement_bounded = bounded });
                    previous[visual] = vertices;
                }
            }
            foreach (var visual in visuals) ValidateLoopSeam(visual, previewNodes[visual], "root_active_loop");
        }

        private void ValidateLoopSeam(CanonicalSpellVisualV2 visual, SpellNode node, string scope)
        {
            if (!node.blueprint_v2.phases.active_loop) return;
            const float tolerance = .001f;
            var phases = node.blueprint_v2.phases;
            visual.SampleNormalized(phases.appearance_end_milli / 1000f);
            var first = visual.GetCoreVertices(); var firstTopology = visual.CoreTopologyFingerprint;
            visual.SampleNormalized(phases.active_end_milli / 1000f);
            var last = visual.GetCoreVertices();
            var finite = first.Length > 0 && first.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z)) &&
                last.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z));
            var topologyStable = first.Length == last.Length && firstTopology == visual.CoreTopologyFingerprint;
            var maximumDelta = 0f;
            if (first.Length == last.Length)
                for (var vertex = 0; vertex < first.Length; vertex++) maximumDelta = Mathf.Max(maximumDelta, Vector3.Distance(first[vertex], last[vertex]));
            var passed = finite && topologyStable && maximumDelta <= tolerance;
            continuityPassed &= passed;
            activeLoopSamples.Add(new { node_id = node.node_id, scope,
                start_normalized_milli = phases.appearance_end_milli, end_normalized_milli = phases.active_end_milli,
                period_seconds = (phases.active_end_milli - phases.appearance_end_milli) * node.blueprint_v2.motion.duration_ms * .000001f,
                first_vertex_count = first.Length, last_vertex_count = last.Length, same_topology = topologyStable,
                maximum_seam_displacement_m = maximumDelta, tolerance_m = tolerance, passed,
                measurement_scope = "Local structural vertices at active-loop boundaries; excludes gameplay root travel. Surface/particle phase quality remains subject to visual review" });
            // Include failure in the common summary consumed by the worker critic.
            topologySamples.Add(new { scope, node = node.node_id, normalized_milli = phases.active_end_milli,
                topology_sha256 = visual.CoreTopologyFingerprint, vertices = last.Length, finite, topology_stable = topologyStable,
                maximum_vertex_displacement_m = maximumDelta, displacement_bounded = maximumDelta <= tolerance });
        }

        private IEnumerator CaptureSheet(string phase)
        {
            var pixels = NewSheet();
            var roots = packet.plan.nodes.Where(n => n.activation.parent_id == null).ToArray();
            var edges = new[] { 0f,
                roots.Max(n => n.activation.delay_ticks * .02f + n.blueprint_v2.motion.duration_ms * n.blueprint_v2.phases.appearance_end_milli * .000001f) / compositionDuration,
                roots.Max(n => n.activation.delay_ticks * .02f + n.blueprint_v2.motion.duration_ms * n.blueprint_v2.phases.active_end_milli * .000001f) / compositionDuration, 1f };
            var names = new[] { "apparition", "stable", "disparition" };
            for (var row = 0; row < 3; row++)
                for (var column = 0; column < 7; column++)
                {
                    var normalized = Mathf.Lerp(edges[row], edges[row + 1], column / 6f);
                    Sample(normalized);
                    var name = phase + "_" + row + "_" + column;
                    var cell = Capture(stageCamera, name);
                    PlaceCell(pixels, cell, row, column);
                    phaseSamples.Add(new { phase = names[row], index = column, normalized_milli = Mathf.RoundToInt(normalized * 1000),
                        composition_seconds = normalized * compositionDuration, file = name + ".png",
                        nodes = visuals.Select(v => new { node_id = v.NodeId, local_normalized_time = Mathf.Clamp01((normalized * compositionDuration - previewNodes[v].activation.delay_ticks * .02f) / (v.Blueprint.motion.duration_ms * .001f)),
                            exists = v.gameObject.activeSelf, topology = v.CoreTopologyFingerprint }).ToArray() });
                    yield return null;
                }
            SaveSheet(phase, pixels);
        }

        private IEnumerator CaptureCoreGallery()
        {
            const int dimension = 1536;
            var cellsPerSide = Mathf.CeilToInt(Mathf.Sqrt(packet.plan.nodes.Count));
            if (cellsPerSide > 4) throw new InvalidDataException("V2 core gallery exceeds sixteen nodes");
            var tileSize = dimension / cellsPerSide;
            var gallery = Enumerable.Repeat(new Color32(8, 11, 16, 255), dimension * dimension).ToArray();
            var oldPosition = stageCamera.transform.position; var oldRotation = stageCamera.transform.rotation;
            foreach (var visual in visuals) visual.gameObject.SetActive(false);
            for (var index = 0; index < packet.plan.nodes.Count; index++)
            {
                var node = packet.plan.nodes[index];
                var host = new GameObject("Independent core gallery item"); host.transform.SetParent(transform, false);
                var visual = host.AddComponent<CanonicalSpellVisualV2>(); visual.Initialize(node); visual.SetManualSampling(true); visual.SetCoreOnly(true);
                var mid = (node.blueprint_v2.phases.appearance_end_milli + node.blueprint_v2.phases.active_end_milli) / 2000f;
                visual.SampleNormalized(mid);
                var localBounds = visual.CoreBounds; var topology = visual.CoreTopologyFingerprint; Vector3[] previous = null;
                for (var sample = 0; sample <= 120; sample++)
                {
                    visual.SampleNormalized(sample / 120f);
                    var vertices = visual.GetCoreVertices(); var finite = vertices.Length > 0 && vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z));
                    var stable = topology == visual.CoreTopologyFingerprint && visual.CanonicalConnectedComponents == node.blueprint_v2.identity.element_count;
                    var maximumDelta = 0f;
                    if (previous != null)
                    {
                        stable &= previous.Length == vertices.Length;
                        if (previous.Length == vertices.Length) for (var vertex = 0; vertex < vertices.Length; vertex++)
                            maximumDelta = Mathf.Max(maximumDelta, Vector3.Distance(vertices[vertex], previous[vertex]));
                    }
                    var bounded = maximumDelta <= Mathf.Max(.5f, localBounds.size.magnitude * .8f);
                    continuityPassed &= finite && stable && bounded;
                    topologySamples.Add(new { scope = "isolated_node_lifecycle", node = node.node_id, normalized_milli = Mathf.RoundToInt(sample / 120f * 1000),
                        topology_sha256 = visual.CoreTopologyFingerprint, vertices = vertices.Length, finite, topology_stable = stable,
                        maximum_vertex_displacement_m = maximumDelta, displacement_bounded = bounded });
                    RecordRuntimeMetrics(new[] { visual }); previous = vertices;
                }
                ValidateLoopSeam(visual, node, "isolated_node_active_loop");
                visual.SampleNormalized(mid);
                var radius = Mathf.Max(.4f, localBounds.extents.magnitude);
                stageCamera.transform.position = localBounds.center + new Vector3(1.2f, .6f, -1.8f).normalized * radius / Mathf.Sin(19 * Mathf.Deg2Rad) * 1.18f;
                stageCamera.transform.LookAt(localBounds.center);
                var name = "core_node_" + index.ToString("D2"); var pixels = Capture(stageCamera, name);
                var imageSize = tileSize - 48; var left = index % cellsPerSide * tileSize + 24;
                var bottom = dimension - (index / cellsPerSide + 1) * tileSize + 36;
                for (var y = 0; y < imageSize; y++) for (var x = 0; x < imageSize; x++)
                    gallery[(bottom + y) * dimension + left + x] = pixels[Mathf.Min(Resolution - 1, y * Resolution / imageSize) * Resolution + Mathf.Min(Resolution - 1, x * Resolution / imageSize)];
                // Opaque tile IDs preserve blind assessment even if a model picked
                // a semantically revealing node_id. The manifest retains the map.
                var label = "N" + (index + 1).ToString();
                DrawText(gallery, label, left, bottom - 24, new Color32(193, 203, 216, 255), 3);
                coreGalleryNodes.Add(new { tile = label, node_id = node.node_id, parent_id = node.activation.parent_id,
                    event_kind = node.activation.@event, local_normalized_time = mid, topology_sha256 = topology,
                    file = name + ".png", scope = "Standalone structural preview; no event occurrence or trigger time is invented" });
                Destroy(host); yield return null;
            }
            var texture = new Texture2D(dimension, dimension, TextureFormat.RGB24, false, false);
            try { texture.SetPixels32(gallery); texture.Apply(false, false); Save("core_gallery", texture.EncodeToPNG(), dimension, dimension); }
            finally { Destroy(texture); }
            stageCamera.transform.SetPositionAndRotation(oldPosition, oldRotation);
        }

        private IEnumerator CaptureRealtime()
        {
            foreach (var visual in visuals) visual.gameObject.SetActive(false);
            var host = new GameObject("V2 real gameplay motion"); host.transform.SetParent(transform, false);
            var lab = host.AddComponent<SpellLab>(); lab.Initialize(packetJson, directory); lab.InputSuppressed = true; lab.enabled = false;
            if (!lab.Ready) throw new InvalidDataException("Real motion SpellLab initialization failed");
            var camera = host.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidDataException("Real motion game camera absent"); camera.enabled = false;
            if (!lab.Cast(new Vector3(0, 1, 3))) throw new InvalidDataException("Real motion spell cast failed");
            var duration = Mathf.Min(60, Mathf.Max(compositionDuration, packet.resource_bounds.max_end_tick * .02f) +
                packet.plan.nodes.Max(n => Mathf.Max(n.blueprint_v2.impact.surviving_core_duration_ms, n.blueprint_v2.disappearance.residual_duration_ms)) * .001f);
            var pixels = NewSheet(); var cells = new List<Color32[]>(); var cellTimes = new List<double>(); var lastOccupied = -1;
            var clock = System.Diagnostics.Stopwatch.StartNew(); var next = 0d; var index = 0; var nextTick = 0d; var tickCount = 0;
            // The exact gameplay runtime owns position, events and contact. Wall
            // time drives ticks and each visual's Update; no handcrafted trajectory
            // or invented time for parent-hit/trigger children is inserted here.
            while (clock.Elapsed.TotalSeconds < duration || index == 0)
            {
                while (nextTick <= clock.Elapsed.TotalSeconds && nextTick < duration)
                { lab.SimulateTicks(1); nextTick += .02; tickCount++; }
                var actors = FindObjectsByType<CanonicalSpellVisualV2>(FindObjectsSortMode.None);
                RecordRuntimeMetrics(actors);
                if (clock.Elapsed.TotalSeconds < next) { yield return null; continue; }
                var elapsed = clock.Elapsed.TotalSeconds; var normalized = Mathf.Clamp01((float)(elapsed / duration));
                var file = "motion_" + index.ToString("D3");
                cells.Add(Capture(camera, file));
                cellTimes.Add(elapsed); if (actors.Length > 0) lastOccupied = index;
                realtimeSamples.Add(new { index, elapsed_seconds = elapsed, normalized_time = normalized, runtime_ticks = tickCount,
                    active_carriers = lab.ActiveCarriers, hits = lab.Hits,
                    actors = actors.Select(v => new { node_id = v.NodeId, world_position = Vec(v.transform.position), topology = v.CoreTopologyFingerprint }).ToArray(), file = file + ".png" });
                index++; next = elapsed + Math.Max(1d / 15, duration / 90d);
                yield return null;
            }
            var finalName = "motion_" + index.ToString("D3"); cells.Add(Capture(camera, finalName));
            cellTimes.Add(clock.Elapsed.TotalSeconds);
            realtimeSamples.Add(new { index, elapsed_seconds = clock.Elapsed.TotalSeconds, normalized_time = 1f,
                runtime_ticks = tickCount, active_carriers = lab.ActiveCarriers, hits = lab.Hits, file = finalName + ".png" });
            // The summary ends after the last genuinely visible actor and one
            // empty frame; full-duration raw evidence remains in realtime_samples.
            var finalCell = Mathf.Min(cells.Count - 1, Math.Max(1, lastOccupied + 1));
            for (var i = 0; i < 21; i++)
            {
                var chosen = Mathf.Clamp(Mathf.RoundToInt(i * finalCell / 20f), 0, cells.Count - 1);
                PlaceCell(pixels, cells[chosen], i / 7, i % 7, true);
                motionSheetSamples.Add(new { row = i / 7, column = i % 7, frame_index = chosen, elapsed_seconds = cellTimes[chosen] });
            }
            SaveSheet("motion_sheet", pixels);
            lab.ResetTargets(); Destroy(host); yield return null;
            stageCamera.gameObject.SetActive(true);
        }

        private IEnumerator CapturePhysics()
        {
            impactsPassed = true;
            var contactSheet = new Color32[1024 * 1024]; var scenarioIndex = 0;
            foreach (var scenario in new[] { "ground", "wall", "target", "oblique" })
            {
                var hasBarrier = packet.plan.nodes.Any(n => n.activation.parent_id == null && n.carrier == "barrier");
                var stationary = packet.plan.nodes.Any(n => n.activation.parent_id == null && (n.carrier == "field" || n.carrier == "trap" || n.carrier == "pulse"));
                var host = new GameObject("V2 physics " + scenario); host.transform.SetParent(transform, false);
                if (scenario != "ground")
                {
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Placement floor";
                    floor.GetComponent<Renderer>().sharedMaterial = validationSurface;
                    floor.transform.SetParent(host.transform, false); floor.transform.position = new Vector3(0, -.2f, 0);
                    floor.transform.localScale = new Vector3(40, .2f, 40);
                }
                var obstacle = GameObject.CreatePrimitive(scenario == "target" ? PrimitiveType.Capsule : PrimitiveType.Cube);
                obstacle.GetComponent<Renderer>().sharedMaterial = scenario == "target" ? validationTarget : validationSurface;
                obstacle.transform.SetParent(host.transform, false);
                obstacle.transform.position = new Vector3(0, 1, 0);
                obstacle.transform.localScale = scenario == "target" ? new Vector3(1, 1, 1) : new Vector3(10, 8, .2f);
                if (scenario == "ground") { obstacle.transform.position = Vector3.zero; obstacle.transform.localScale = new Vector3(15, .2f, 15); }
                if (scenario == "oblique") obstacle.transform.rotation = Quaternion.Euler(0, 35, 0);
                var receivers = new List<LabReceiver>();
                var receiverTeam = "hostile";
                var filter = packet.plan.nodes.First(n => n.activation.parent_id == null).options.trigger_filter ?? packet.plan.nodes.First(n => n.activation.parent_id == null).options.contact_filter;
                if (filter == "ally" || filter == "environment") receiverTeam = filter;
                if (scenario == "target" && !stationary) { var receiver = obstacle.AddComponent<LabReceiver>(); receiver.Initialize(200, receiverTeam); receivers.Add(receiver); }
                var caster = new GameObject("Validation caster").transform; caster.SetParent(host.transform, false);
                caster.position = scenario == "ground" ? new Vector3(0, 3, -3) : new Vector3(0, 1, -3);
                var aim = scenario == "ground" ? Vector3.zero : new Vector3(0, 1, 0);
                if (stationary)
                {
                    // An actual receiver is positioned in the declared interaction area;
                    // terrain remains a separate collider in every scenario.
                    var actor = GameObject.CreatePrimitive(PrimitiveType.Capsule); actor.name = "Interaction receiver";
                    actor.GetComponent<Renderer>().sharedMaterial = validationTarget;
                    actor.transform.SetParent(host.transform, false); actor.transform.position = aim + Vector3.up * .5f;
                    var receiver = actor.AddComponent<LabReceiver>(); receiver.Initialize(201, receiverTeam); receivers.Add(receiver);
                }
                var executionPacket = hasBarrier ? PacketWithBarrierProbe() : packet;
                var runtime = new RuntimeEngine(executionPacket, new Dictionary<string, GeometryAsset>(), new Dictionary<string, Texture2D>(), caster, receivers);
                Physics.SyncTransforms();
                var cast = runtime.TryCast(caster.position, aim, (aim - caster.position).normalized);
                var spawned = new List<CanonicalSpellVisualV2>();
                var fingerprints = new Dictionary<CanonicalSpellVisualV2, string>();
                var maximumTicks = Mathf.Clamp(packet.resource_bounds.max_end_tick + 2, 2, 1000);
                var physicsClock = System.Diagnostics.Stopwatch.StartNew();
                for (var tick = 0; tick < maximumTicks; tick++)
                {
                    runtime.Tick();
                    foreach (var visual in FindObjectsByType<CanonicalSpellVisualV2>(FindObjectsSortMode.None))
                        if (!fingerprints.ContainsKey(visual)) { spawned.Add(visual); fingerprints[visual] = visual.CoreTopologyFingerprint; }
                    RecordRuntimeMetrics(spawned);
                    if (runtime.V2ContactCount > 0 || stationary && runtime.Hits > 0) break;
                    while (physicsClock.Elapsed.TotalSeconds < (tick + 1) * .02) yield return null;
                }
                var contact = runtime.V2ContactCount > 0;
                if (contact)
                {
                    var reactionDelay = Mathf.Min(.1f, packet.plan.nodes.Min(n => n.blueprint_v2.impact.reaction_duration_ms) * .0004f);
                    var until = Time.realtimeSinceStartup + reactionDelay;
                    while (Time.realtimeSinceStartup < until) { RecordRuntimeMetrics(spawned); yield return null; }
                }
                RecordRuntimeMetrics(spawned);
                var rootOnly = spawned.All(v => v == null || v.GetComponentsInChildren<Rigidbody>(true).Length <= 1 &&
                    v.GetComponentsInChildren<Collider>(true).All(c => c.transform == v.transform || IsGameplayChild(c.transform, v.transform)));
                var coherent = spawned.All(v => v == null || fingerprints[v] == v.CoreTopologyFingerprint);
                hierarchyPassed &= rootOnly;
                var physicalContactExpected = packet.plan.nodes.Any(n => n.activation.parent_id == null && (n.carrier == "projectile" || n.carrier == "beam" || n.carrier == "barrier"));
                var expectedResponseObserved = physicalContactExpected ? contact : stationary && runtime.Hits > 0;
                impactsPassed &= cast && expectedResponseObserved && rootOnly && coherent;
                stageCamera.transform.position = new Vector3(5, 3.5f, -6); stageCamera.transform.LookAt(new Vector3(0, 1, 0));
                var contactPixels = Capture(stageCamera, "impact_" + scenario);
                var cellX = scenarioIndex % 2 * Resolution; var cellY = (1 - scenarioIndex / 2) * Resolution;
                for (var y = 0; y < Resolution; y++) for (var x = 0; x < Resolution; x++)
                    contactSheet[(cellY + y) * 1024 + cellX + x] = contactPixels[y * Resolution + x];
                scenarioIndex++;
                impactScenarios.Add(new { scenario, cast, contact_count = runtime.V2ContactCount, ticks = runtime.TickCount,
                    contact_normal = Vec(runtime.V2LastContactNormal), contact_position = Vec(runtime.V2LastContactPosition),
                    expected_response = hasBarrier ? "block_controlled_probe_same_runtime" : physicalContactExpected ? "root_contact" : "receiver_enter_tick_hit_or_trigger",
                    test_apparatus = hasBarrier ? "Original blueprint and nodes unchanged; separate precompiled harmless projectile appended to a private harness copy so RuntimeEngine emits the real barrier block event" : "Original compiled spell unchanged",
                    expected_response_observed = expectedResponseObserved, gameplay_hits = runtime.Hits,
                    root_physics_only = rootOnly, core_topology_preserved = coherent, spawned_visuals = spawned.Count,
                    simulated_physics = "RuntimeEngine.Tick (20 ms) paced by real wall clock; real Unity SphereCast/Raycast/Overlap against scenario colliders; no force on visual children",
                    visual_review = "required" });
                runtime.CancelAll(); Destroy(host); yield return null;
            }
            var contactTexture = new Texture2D(1024, 1024, TextureFormat.RGB24, false, false);
            try { contactTexture.SetPixels32(contactSheet); contactTexture.Apply(false, false); Save("impact_sheet", contactTexture.EncodeToPNG(), 1024, 1024); }
            finally { Destroy(contactTexture); }
        }

        private CompiledSpell PacketWithBarrierProbe()
        {
            var copy = JsonConvert.DeserializeObject<CompiledSpell>(packetJson);
            copy.resource_bounds.max_instances = Math.Min(256, copy.resource_bounds.max_instances + 1);
            copy.plan.nodes.Add(new SpellNode {
                node_id = "validation_probe", subject_id = "validation_probe", clause_ids = new List<string>(),
                carrier = "projectile", anchor = "caster", geometry_id = "validation.probe", scale_cm = 20,
                activation = new SpellActivation { parent_id = null, @event = "cast", delay_ticks = 3, max_activations = 1, copies = 1, spread_mdeg = 0 },
                appearance = new SpellAppearance { affinity = "air", pattern = "solid" }, effects = new List<SpellEffect>(),
                options = new SpellOptions { lifetime_ticks = 100, range_cm = 1200, speed_cm_s = 1000, radius_cm = 5,
                    motion = "straight", contact_filter = "all", bounces = 0, pierces = 0 }
            });
            return copy;
        }

        private static bool IsGameplayChild(Transform current, Transform root)
        {
            while (current != null && current != root) { if (current.name == "Gameplay") return true; current = current.parent; }
            return false;
        }

        private IEnumerator CaptureGameCamera()
        {
            var host = new GameObject("V2 actual gameplay camera validation"); host.transform.SetParent(transform, false);
            var lab = host.AddComponent<SpellLab>(); lab.Initialize(packetJson, directory);
            lab.InputSuppressed = true; lab.enabled = false;
            if (!lab.Ready) throw new InvalidDataException("Actual SpellLab failed to load V2 packet");
            var camera = host.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidDataException("Actual game camera absent");
            camera.enabled = false;
            if (!lab.Cast(new Vector3(0, 1, 3))) throw new InvalidDataException("Actual SpellLab V2 cast failed");
            var previewSeconds = Mathf.Clamp(packet.plan.nodes.Where(n => n.activation.parent_id == null)
                .Max(n => n.blueprint_v2.motion.duration_ms * n.blueprint_v2.phases.appearance_end_milli * .0000008f), .15f, .6f);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (var tick = 0; tick < Mathf.CeilToInt(previewSeconds / .02f); tick++)
            {
                lab.SimulateTicks(1);
                while (clock.Elapsed.TotalSeconds < (tick + 1) * .02) yield return null;
            }
            // This is the real default lab camera, not the fitted canonical comparison camera.
            Capture(camera, "game_camera");
            lab.ResetTargets(); Destroy(host); yield return null;
        }

        private Color32[] Capture(Camera camera, string name)
        {
            var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            var before = renders; var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // The actual game camera reserves part of its viewport for UI.
                // Clear the complete reusable target first, preserving camera.rect
                // while preventing a previous physics frame from leaking outside it.
                RenderTexture.active = target;
                GL.Viewport(new Rect(0, 0, Resolution, Resolution));
                GL.Clear(true, true, Color.black);
                camera.targetTexture = target; RenderPipeline.SubmitRenderRequest(camera, request);
                if (renders <= before) throw new InvalidOperationException("URP did not complete requested V2 frame");
                RenderTexture.active = target; linear.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0, false);
                var pixels = linear.GetPixels();
                for (var i = 0; i < pixels.Length; i++)
                {
                    if (!Finite(pixels[i].r) || !Finite(pixels[i].g) || !Finite(pixels[i].b)) throw new InvalidDataException("Non-finite V2 GPU pixels");
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) pixels[i] = pixels[i].gamma;
                }
                renderSeconds += watch.Elapsed.TotalSeconds; requestedRenders++;
                display.SetPixels(pixels); display.Apply(false, false);
                Save(name, display.EncodeToPNG(), Resolution, Resolution);
                return display.GetPixels32();
            }
            finally { RenderTexture.active = previous; camera.targetTexture = previousTarget; }
        }

        private void CameraRendered(ScriptableRenderContext context, Camera camera) { renders++; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float[] Vec(Vector3 value) => new[] { value.x, value.y, value.z };
        private static Color32[] NewSheet() => Enumerable.Repeat(new Color32(8, 11, 16, 255), SheetWidth * SheetHeight).ToArray();

        private static void PlaceCell(Color32[] sheet, Color32[] pixels, int row, int column, bool motion = false)
        {
            const int cell = 208;
            var left = 20 + column * 214; var bottom = SheetHeight - (row + 1) * 374 + 65;
            for (var y = 0; y < cell; y++) for (var x = 0; x < cell; x++)
                sheet[(bottom + y) * SheetWidth + left + x] = pixels[Mathf.Clamp(y * Resolution / cell, 0, Resolution - 1) * Resolution + Mathf.Clamp(x * Resolution / cell, 0, Resolution - 1)];
            var tint = row == 0 ? new Color32(124, 177, 221, 255) : row == 1 ? new Color32(126, 217, 199, 255) : new Color32(191, 143, 218, 255);
            for (var x = 0; x < cell; x++) { sheet[(bottom - 1) * SheetWidth + left + x] = tint; sheet[(bottom + cell) * SheetWidth + left + x] = tint; }
            for (var y = 0; y < cell; y++) { sheet[(bottom + y) * SheetWidth + left - 1] = tint; sheet[(bottom + y) * SheetWidth + left + cell] = tint; }
            DrawText(sheet, motion ? "TEMPS " + (row + 1) : new[] { "APPARITION", "STABLE", "DISPARITION" }[row], 20, bottom + cell + 32, tint, 3);
            DrawText(sheet, (column + 1).ToString(), left + 98, bottom - 30, tint, 3);
        }

        private static void DrawText(Color32[] image, string text, int left, int bottom, Color32 tint, int scale)
        {
            var glyphs = new Dictionary<char, string> {
                ['A']="010101111101101", ['B']="110101110101110", ['D']="110101101101110", ['E']="111100110100111", ['I']="111010010010111", ['L']="100100100100111",
                ['M']="101111111101101", ['N']="101111111111101", ['O']="010101101101010", ['P']="110101110100100", ['R']="110101110101101", ['S']="111100111001111", ['T']="111010010010010",
                ['0']="111101101101111", ['1']="010110010010111", ['2']="111001111100111", ['3']="111001111001111", ['4']="101101111001001", ['5']="111100111001111", ['6']="111100111101111", ['7']="111001010010010", ['8']="111101111101111", ['9']="111101111001111" };
            foreach (var character in text)
            {
                if (glyphs.TryGetValue(character, out var glyph))
                    for (var y = 0; y < 5; y++) for (var x = 0; x < 3; x++) if (glyph[y * 3 + x] == '1')
                        for (var dy = 0; dy < scale; dy++) for (var dx = 0; dx < scale; dx++)
                            image[(bottom + (4 - y) * scale + dy) * SheetWidth + left + x * scale + dx] = tint;
                left += 4 * scale;
            }
        }

        private void SaveSheet(string name, Color32[] pixels)
        {
            var texture = new Texture2D(SheetWidth, SheetHeight, TextureFormat.RGB24, false, false);
            try { texture.SetPixels32(pixels); texture.Apply(false, false); Save(name, texture.EncodeToPNG(), SheetWidth, SheetHeight); }
            finally { Destroy(texture); }
        }

        private void Save(string name, byte[] bytes, int width, int height)
        {
            if (bytes == null || bytes.Length < 500) throw new InvalidDataException("V2 GPU output absent");
            var file = name + ".png"; var path = Path.Combine(output, file); Ordinary(path); File.WriteAllBytes(path, bytes);
            frames.Add(new { phase = name, file, sha256 = ParchmentStore.Hash(bytes), width_px = width, height_px = height });
        }

        private void WriteManifest(bool completed, string error)
        {
            if (output == null) return;
            var manifest = new {
                schema_version = "sp.v2-validation/1.0", mode, completed, spell_sha256 = packetHash, blueprints_sha256 = blueprintsHash,
                provider_calls = 0, user_library_touched = false, gameplay_executed = mode == "full", art_acceptance = "not_claimed",
                measured_fps = renderSeconds > 0 ? requestedRenders / renderSeconds : 0, measured_frames = requestedRenders,
                measurement_scope = "Explicit 512x512 URP GPU requests plus synchronous full-frame readback; excludes PNG encoding. Not gameplay FPS or isolated GPU duration",
                graphics_device = SystemInfo.graphicsDeviceName, frames, phase_samples = phaseSamples, topology_samples = topologySamples,
                impact_scenarios = impactScenarios, realtime_samples = realtimeSamples, motion_sheet_samples = motionSheetSamples, gates,
                core_gallery_nodes = coreGalleryNodes,
                active_loop_samples = activeLoopSamples,
                canonical_sheet_scope = "Root nodes only, one shared elapsed clock with activation delays and individual lifespans; no invented timeline for event children. Event children are measured during real RuntimeEngine casts in F and D",
                canonical_duration_seconds = compositionDuration,
                impact_sheet_layout = "2x2: ground top-left, wall top-right, target bottom-left, oblique bottom-right. Each cell is an actual unchanged RuntimeEngine scenario",
                metrics = new { per_visual = visualMetrics, canonical_sampling_cpu_ms = sampledCpuSeconds * 1000,
                    actual_draw_calls = (int?)null, gpu_microseconds = (double?)null, transparent_overdraw = (double?)null,
                    unavailable_reason = "Per-effect GPU timing, exact SRP draw calls and overdraw require a separate instrumented GPU profiler capture; no substitute estimated zeros",
                    render_wall_ms = renderSeconds * 1000 },
                pass_stamp = new { mode, spell_sha256 = packetHash, blueprints_sha256 = blueprintsHash,
                    topology = topologySamples.Take(visualMetrics.Count).ToArray(), previous_pass_immutable = true },
                elapsed_seconds = Time.realtimeSinceStartup - started, error
            };
            var path = Path.Combine(output, "validation.json"); Ordinary(path);
            File.WriteAllText(path, JsonConvert.SerializeObject(manifest, Formatting.Indented), new UTF8Encoding(false));
        }

        private void Fail(string error)
        {
            if (finished) return; finished = true;
            try { WriteManifest(false, error); } catch { }
            Debug.LogError("PALIMPSESTE_V2_VALIDATION_FAILED " + error); Application.Quit(2);
        }

        private void ClearSubjects()
        {
            foreach (var visual in visuals) if (visual != null) Destroy(visual.gameObject);
            visuals.Clear();
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endCameraRendering -= CameraRendered; ClearSubjects();
            if (target != null) { target.Release(); Destroy(target); }
            if (linear != null) Destroy(linear); if (display != null) Destroy(display);
            if (validationSurface != null) Destroy(validationSurface);
            if (validationTarget != null) Destroy(validationTarget);
        }
    }
}
