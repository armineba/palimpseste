using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    /// <summary>
    /// Renders the bounded composition inferred from a spell's visual reference.
    /// Every part is declarative data. Meshes, shaders and motion are authored
    /// here; no reference image, player text or model output executes code.
    /// Coordinates are absolute local centimetres, independent of colliders.
    /// </summary>
    public sealed class ImageConstructedSpellVisual : MonoBehaviour
    {
        public const int MaximumParts = 64;
        private const float ImpactLifetime = .85f;
        private const int MaximumBeamVerticesPerPart = 8192;
        private const int MaximumBeamVertices = 65536;
        private static readonly int EnvelopeId = Shader.PropertyToID("_Envelope");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int ArmedId = Shader.PropertyToID("_Armed");
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<PartState> parts = new List<PartState>(MaximumParts);
        private MaterialPropertyBlock properties;
        private float born, lifetime, birthDuration, retirementStarted = -1, retirementDuration;
        private bool initialized, impact, armed;
        private Bounds authoredBounds;
        private Vector3 presentationScale = Vector3.one, presentationOffset;
        private Quaternion presentationRotation = Quaternion.identity;
        private readonly Vector3[] beamPoints = new Vector3[64];
        private readonly Vector3[] incomingBeamPoints = new Vector3[64];
        private readonly Vector3[] beamRight = new Vector3[64];
        private readonly Vector3[] beamUp = new Vector3[64];
        private readonly Quaternion[] beamFrames = new Quaternion[64];
        private readonly Vector3[] beamAngular = new Vector3[64];
        private readonly float[] beamDistances = new float[64];
        private readonly float[] beamCuts = new float[64];
        private readonly float[] cachedBeamCuts = new float[64];
        private readonly BeamVertex[] clipA = new BeamVertex[8], clipB = new BeamVertex[8];
        private int beamPointCount, cachedBeamPointCount;
        private float beamLength, nextBeamTessellation;
        private bool beamActive, beamNeedsUpload;

        private struct BeamVertex : IEquatable<BeamVertex>
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public bool Equals(BeamVertex other) => position.Equals(other.position) && normal.Equals(other.normal) && uv.Equals(other.uv);
            public override bool Equals(object other) => other is BeamVertex vertex && Equals(vertex);
            public override int GetHashCode() => unchecked((position.GetHashCode() * 397 ^ normal.GetHashCode()) * 397 ^ uv.GetHashCode());
            public static BeamVertex Lerp(BeamVertex a, BeamVertex b, float t) => new BeamVertex {
                position = Vector3.LerpUnclamped(a.position,b.position,t),
                normal = Vector3.LerpUnclamped(a.normal,b.normal,t), uv = Vector2.LerpUnclamped(a.uv,b.uv,t)
            };
        }

        private sealed class PartState
        {
            public Transform transform;
            public MeshRenderer renderer;
            public Vector3 position, scale, velocity;
            public Quaternion rotation;
            public string motion;
            public float amplitude, frequency, phase, seed;
            public Mesh mesh;
            public BeamVertex[] beamSource;
            public int[] beamSourceTriangles;
            public List<BeamVertex> beamSamples;
            public List<Vector3> beamVertices, beamNormals;
            public List<Vector2> beamUvs;
            public List<int> beamTriangles;
            public Dictionary<BeamVertex,int> beamLookup;
        }

        public int PartCount => parts.Count;
        public bool IsImpact => impact;
        public bool BeamTessellationLimited { get; private set; }

        public static bool Supports(SpellNode node)
            => node?.appearance?.construction?.parts != null && node.appearance.construction.parts.Count > 0;

        public void Initialize(SpellNode node)
        {
            if (initialized) throw new InvalidOperationException("Image construction already initialized");
            ValidateConstruction(node?.appearance?.construction);
            var shader = Resources.Load<Shader>("SpellImageConstruction");
            if (shader == null) throw new InvalidOperationException("SpellImageConstruction shader missing from player");
            // Native Unity allocations must run here on the main thread,
            // never in MonoBehaviour field initializers during construction.
            properties = new MaterialPropertyBlock();
            born = Time.time;
            lifetime = Mathf.Max(.1f,(node.options?.lifetime_ticks ?? 150) * Time.fixedDeltaTime);
            birthDuration = Mathf.Clamp((node.appearance.vfx?.charge_ms ?? 280) / 1000f,.12f,.65f);
            // A fast projectile can hit before the long decorative charge
            // finishes. Reveal its silhouette promptly while the aura unfolds.
            if (node.carrier == "projectile") birthDuration = Mathf.Min(birthDuration, .14f);
            var definition = node.appearance.construction.parts;
            for (var i = 0; i < definition.Count; i++)
            {
                var part = definition[i];
                var child = new GameObject("Image part " + i + " " + part.kind).transform;
                child.SetParent(transform,false);
                var mesh = Own(BuildMesh(part));
                child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Own(MakeMaterial(shader,part));
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var pathPart = part.kind == "ribbon" || part.kind == "arc";
                var state = new PartState {
                    transform = child, renderer = renderer, mesh = mesh,
                    position = Centimetres(part.position_cm,-1000,1000),
                    scale = pathPart ? Vector3.one : Centimetres(part.scale_cm,1,1000),
                    rotation = Quaternion.Euler(Vector(part.rotation_mdeg,-360000,360000) / 1000f),
                    motion = part.motion?.kind ?? "still",
                    amplitude = Mathf.Clamp(part.motion?.amplitude_cm ?? 0,0,150) / 100f,
                    frequency = Mathf.Clamp(part.motion?.frequency_mhz ?? 0,0,6000) / 1000f,
                    phase = (part.motion?.phase_mdeg ?? 0) / 1000f * Mathf.Deg2Rad,
                    seed = (i * .61803399f) % 1f
                };
                state.velocity = (state.position.normalized * .6f + new Vector3(
                    Mathf.Sin(i * 2.39f),.4f + (i % 5) * .16f,Mathf.Cos(i * 2.39f))) * (1.2f + i % 4 * .24f);
                for (var corner = 0; corner < 8; corner++)
                {
                    var local = mesh.bounds.center + Vector3.Scale(mesh.bounds.extents,new Vector3(
                        (corner & 1) == 0 ? -1 : 1,(corner & 2) == 0 ? -1 : 1,(corner & 4) == 0 ? -1 : 1));
                    var point = state.position + state.rotation * Vector3.Scale(local,state.scale);
                    if (i == 0 && corner == 0) authoredBounds = new Bounds(point,Vector3.zero);
                    else authoredBounds.Encapsulate(point);
                }
                parts.Add(state);
            }
            initialized = true;
            Animate(0);
        }

        /// <summary>Reuse the actual image composition as impact fragments.</summary>
        public static ImageConstructedSpellVisual SpawnImpact(SpellNode node, Vector3 position, Vector3 direction)
        {
            if (!Supports(node)) return null;
            ValidateConstruction(node.appearance.construction);
            var root = new GameObject("Image construction impact");
            root.transform.position = position;
            if (direction.sqrMagnitude > .0001f) root.transform.rotation = Quaternion.LookRotation(direction.normalized);
            var visual = root.AddComponent<ImageConstructedSpellVisual>();
            visual.Initialize(node);
            visual.impact = true;
            visual.Animate(0);
            return visual;
        }

        public void Retire(float duration = .2f)
        {
            if (retirementStarted >= 0) return;
            retirementStarted = Time.time;
            retirementDuration = Mathf.Clamp(duration,.03f,.85f);
        }

        public void Arm() { armed = true; }

        public void SetPulseRadius(float radius)
        {
            if (!initialized || float.IsNaN(radius) || float.IsInfinity(radius)) return;
            var extent = Mathf.Max(.01f,Mathf.Max(Mathf.Abs(authoredBounds.min.x),Mathf.Abs(authoredBounds.max.x)),
                Mathf.Max(Mathf.Abs(authoredBounds.min.z),Mathf.Abs(authoredBounds.max.z)));
            var expansion = Mathf.Clamp(radius / extent,.005f,100f);
            presentationScale = new Vector3(expansion,1,expansion);
        }

        public void SetBeamPath(List<Vector3> points)
        {
            if (!initialized || points == null || points.Count < 2 || points.Count > 64) return;
            for (var i = 0; i < points.Count; i++) if (!Finite(points[i])) return;
            var count = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var point = transform.InverseTransformPoint(points[i]);
                if (!Finite(point)) return;
                if (count > 0 && (point - incomingBeamPoints[count - 1]).sqrMagnitude < .00000001f) continue;
                // Straight samples add no bend. Keep all actual direction
                // changes, including a chained beam turning back on itself.
                if (count > 1)
                {
                    var a = incomingBeamPoints[count - 1] - incomingBeamPoints[count - 2];
                    var b = point - incomingBeamPoints[count - 1];
                    if (Vector3.Dot(a.normalized,b.normalized) > .999999f) count--;
                }
                incomingBeamPoints[count++] = point;
            }
            if (count < 2) return;
            var pathChanged = !beamActive || count != beamPointCount;
            for (var i = 0; i < count && !pathChanged; i++)
                pathChanged = (incomingBeamPoints[i] - beamPoints[i]).sqrMagnitude > .000000000001f;
            Array.Copy(incomingBeamPoints,beamPoints,count);
            beamPointCount = count; beamDistances[0] = 0;
            for (var i = 1; i < count; i++) beamDistances[i] = beamDistances[i - 1] + Vector3.Distance(beamPoints[i - 1],beamPoints[i]);
            beamLength = beamDistances[count - 1];
            if (beamLength < .0001f) return;
            var previousRight = Vector3.right;
            for (var i = 0; i < count; i++)
            {
                var incoming = i == 0 ? (beamPoints[1] - beamPoints[0]).normalized : (beamPoints[i] - beamPoints[i - 1]).normalized;
                var outgoing = i == count - 1 ? incoming : (beamPoints[i + 1] - beamPoints[i]).normalized;
                var tangent = incoming + outgoing;
                if (tangent.sqrMagnitude < .00001f) tangent = outgoing;
                tangent.Normalize();
                var right = previousRight - tangent * Vector3.Dot(previousRight,tangent);
                if (right.sqrMagnitude < .00001f) right = Vector3.Cross(Mathf.Abs(tangent.y) > .95f ? Vector3.forward : Vector3.up,tangent);
                right.Normalize();
                if (i > 0 && Vector3.Dot(right,previousRight) < 0) right = -right;
                beamRight[i] = right; beamUp[i] = Vector3.Cross(tangent,right).normalized;
                beamFrames[i] = Quaternion.LookRotation(tangent,beamUp[i]);
                previousRight = right;
                beamCuts[i] = authoredBounds.min.z + beamDistances[i] / beamLength * Mathf.Max(.0001f,authoredBounds.size.z);
            }
            for (var i = 0; i < count - 1; i++)
            {
                var delta = beamFrames[i + 1] * Quaternion.Inverse(beamFrames[i]);
                delta.ToAngleAxis(out var angle,out var axis);
                if (angle > 180) angle -= 360;
                beamAngular[i] = Mathf.Abs(angle) < .0001f || !Finite(axis) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad);
            }
            beamActive = true; beamNeedsUpload |= pathChanged;
            var topologyChanged = cachedBeamPointCount != count;
            var tolerance = Mathf.Max(.00005f,authoredBounds.size.z * .001f);
            for (var i = 1; i < count - 1 && !topologyChanged; i++)
                topologyChanged = Mathf.Abs(cachedBeamCuts[i] - beamCuts[i]) > tolerance;
            if (cachedBeamPointCount == 0 || topologyChanged && Time.time >= nextBeamTessellation)
            {
                PrepareBeamMeshes();
                beamNeedsUpload = true;
                cachedBeamPointCount = count; Array.Copy(beamCuts,cachedBeamCuts,count);
                nextBeamTessellation = Time.time + .08f;
            }
        }

        private void PrepareBeamMeshes()
        {
            foreach (var part in parts)
            {
                if (part.beamSource == null)
                {
                    var source = part.mesh.vertices; var normals = part.mesh.normals; var uvs = part.mesh.uv;
                    part.beamSourceTriangles = part.mesh.triangles;
                    part.beamSource = new BeamVertex[source.Length];
                    for (var i = 0; i < source.Length; i++) part.beamSource[i] = new BeamVertex {
                        position = part.position + part.rotation * Vector3.Scale(source[i],part.scale),
                        normal = (part.rotation * new Vector3(normals[i].x / part.scale.x,normals[i].y / part.scale.y,normals[i].z / part.scale.z)).normalized,
                        uv = uvs[i]
                    };
                    part.beamSamples = new List<BeamVertex>(source.Length);
                    part.beamVertices = new List<Vector3>(source.Length); part.beamNormals = new List<Vector3>(source.Length);
                    part.beamUvs = new List<Vector2>(source.Length); part.beamTriangles = new List<int>(part.beamSourceTriangles.Length);
                    part.beamLookup = new Dictionary<BeamVertex,int>(source.Length);
                    part.mesh.MarkDynamic();
                }
            }
            BeamTessellationLimited = false;
            foreach (var part in parts)
            {
                // Fixed quotas bound retained scratch capacities as well as
                // uploaded vertices, including after a failed subdivision.
                var limit = Mathf.Min(MaximumBeamVerticesPerPart,MaximumBeamVertices / parts.Count);
                part.beamSamples.Clear(); part.beamUvs.Clear(); part.beamTriangles.Clear(); part.beamLookup.Clear();
                var complete = TessellateBeamPart(part,limit);
                if (!complete)
                {
                    // Exceptional dense zigzags retain every original part
                    // and follow every path segment, with coarser faces at
                    // sharp corners, rather than growing unbounded meshes.
                    BeamTessellationLimited = true;
                    part.beamSamples.Clear(); part.beamUvs.Clear(); part.beamTriangles.Clear();
                    part.beamSamples.AddRange(part.beamSource);
                    foreach (var vertex in part.beamSource) part.beamUvs.Add(vertex.uv);
                    part.beamTriangles.AddRange(part.beamSourceTriangles);
                }
                part.beamVertices.Clear(); part.beamNormals.Clear();
                foreach (var vertex in part.beamSamples) { part.beamVertices.Add(vertex.position); part.beamNormals.Add(vertex.normal); }
                part.mesh.Clear(); part.mesh.SetVertices(part.beamVertices); part.mesh.SetNormals(part.beamNormals);
                part.mesh.SetUVs(0,part.beamUvs); part.mesh.SetTriangles(part.beamTriangles,0,false);
            }
        }

        private bool TessellateBeamPart(PartState part, int limit)
        {
            var source = part.beamSource; var triangles = part.beamSourceTriangles;
            for (var triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                var a = source[triangles[triangle]]; var b = source[triangles[triangle + 1]]; var c = source[triangles[triangle + 2]];
                var minimum = Mathf.Min(a.position.z,Mathf.Min(b.position.z,c.position.z));
                var maximum = Mathf.Max(a.position.z,Mathf.Max(b.position.z,c.position.z));
                for (var segment = 0; segment < beamPointCount - 1; segment++)
                {
                    if (maximum < beamCuts[segment] || minimum > beamCuts[segment + 1]) continue;
                    if (maximum - minimum < .000001f && segment != BeamSegment(minimum)) continue;
                    clipA[0] = a; clipA[1] = b; clipA[2] = c;
                    var count = ClipPlane(clipA,3,clipB,beamCuts[segment],true);
                    count = ClipPlane(clipB,count,clipA,beamCuts[segment + 1],false);
                    if (count < 3) continue;
                    var first = AddBeamVertex(part,clipA[0],limit);
                    if (first < 0) return false;
                    for (var i = 1; i < count - 1; i++)
                    {
                        var second = AddBeamVertex(part,clipA[i],limit); var third = AddBeamVertex(part,clipA[i + 1],limit);
                        if (second < 0 || third < 0) return false;
                        if (first == second || second == third || third == first) continue;
                        if (part.beamTriangles.Count + 3 > limit * 9) return false;
                        part.beamTriangles.Add(first); part.beamTriangles.Add(second); part.beamTriangles.Add(third);
                    }
                }
            }
            return true;
        }

        private static int ClipPlane(BeamVertex[] source, int count, BeamVertex[] destination, float plane, bool keepAbove)
        {
            if (count == 0) return 0;
            var written = 0; var previous = source[count - 1];
            var previousInside = keepAbove ? previous.position.z >= plane : previous.position.z <= plane;
            for (var i = 0; i < count; i++)
            {
                var current = source[i]; var inside = keepAbove ? current.position.z >= plane : current.position.z <= plane;
                if (inside != previousInside)
                {
                    var denominator = current.position.z - previous.position.z;
                    var crossing = BeamVertex.Lerp(previous,current,(plane - previous.position.z) / denominator);
                    crossing.position.z = plane;
                    destination[written++] = crossing;
                }
                if (inside) destination[written++] = current;
                previous = current; previousInside = inside;
            }
            return written;
        }

        private static int AddBeamVertex(PartState part, BeamVertex vertex, int limit)
        {
            if (part.beamLookup.TryGetValue(vertex,out var existing)) return existing;
            if (part.beamSamples.Count >= limit) return -1;
            var index = part.beamSamples.Count;
            part.beamLookup.Add(vertex,index); part.beamSamples.Add(vertex); part.beamUvs.Add(vertex.uv);
            return index;
        }

        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        // Check the complete cached payload before creating even the first
        // child or allocating meshes. Contracts is shared with this assembly;
        // the client does not load the server compiler or trust cache contents.
        public static void ValidateConstruction(SpellVisualConstruction construction)
        {
            if (construction?.parts == null || construction.parts.Count < 1 || construction.parts.Count > MaximumParts)
                throw new ArgumentException("Image construction requires 1 to 64 validated parts");
            foreach (var part in construction.parts)
            {
                if (part == null || Array.IndexOf(SpellVisualConstructionLimits.Kinds,part.kind) < 0 ||
                    Array.IndexOf(SpellVisualConstructionLimits.Materials,part.material) < 0)
                    throw new ArgumentException("Unsupported image construction part");
                ValidateVector(part.position_cm,-1000,1000); ValidateVector(part.scale_cm,1,1000);
                ValidateVector(part.rotation_mdeg,-360000,360000); ValidateVector(part.color_rgb,0,255);
                if (part.opacity_milli < 0 || part.opacity_milli > 1000 || part.emission_milli < 0 || part.emission_milli > 6000)
                    throw new ArgumentException("Image construction radiance exceeds bounds");
                if (part.points_cm == null || part.points_cm.Count > 16 ||
                    ((part.kind == "ribbon" || part.kind == "arc") && part.points_cm.Count < 2))
                    throw new ArgumentException("Image construction path exceeds bounds");
                foreach (var point in part.points_cm) ValidateVector(point,-1000,1000);
                if (part.motion == null || Array.IndexOf(SpellVisualConstructionLimits.Motions,part.motion.kind) < 0 ||
                    part.motion.amplitude_cm < 0 || part.motion.amplitude_cm > 150 ||
                    part.motion.frequency_mhz < 0 || part.motion.frequency_mhz > 6000 ||
                    part.motion.phase_mdeg < 0 || part.motion.phase_mdeg > 360000)
                    throw new ArgumentException("Image construction motion exceeds bounds");
            }
        }

        private static void ValidateVector(int[] values, int minimum, int maximum)
        {
            if (values == null || values.Length != 3) throw new ArgumentException("Visual vectors require three coordinates");
            for (var i = 0; i < values.Length; i++)
                if (values[i] < minimum || values[i] > maximum) throw new ArgumentException("Visual coordinate exceeds bounds");
        }

        private static Vector3 Vector(int[] values, int minimum, int maximum)
        {
            if (values == null || values.Length != 3) throw new ArgumentException("Visual vectors require three coordinates");
            return new Vector3(Mathf.Clamp(values[0],minimum,maximum),Mathf.Clamp(values[1],minimum,maximum),
                Mathf.Clamp(values[2],minimum,maximum));
        }

        private static Vector3 Centimetres(int[] values, int minimum, int maximum) => Vector(values,minimum,maximum) / 100f;

        private T Own<T>(T item) where T : UnityEngine.Object { owned.Add(item); return item; }

        private static Material MakeMaterial(Shader shader, SpellVisualPart part)
        {
            float mode;
            switch (part.material)
            {
                case "glass": mode = 0; break;
                case "energy": mode = 1; break;
                case "mist": mode = 2; break;
                case "stone": mode = 3; break;
                case "metal": mode = 4; break;
                default: throw new ArgumentException("Unsupported image construction material");
            }
            var rgb = Vector(part.color_rgb,0,255) / 255f;
            var material = new Material(shader) { name = "Image construction " + part.material };
            material.SetColor("_Color",new Color(rgb.x,rgb.y,rgb.z,1));
            material.SetFloat("_Material",mode);
            material.SetFloat("_Opacity",Mathf.Clamp01(part.opacity_milli / 1000f));
            material.SetFloat("_Emission",Mathf.Clamp(part.emission_milli / 1000f,0,6));
            material.SetFloat("_Shape",part.kind == "feather" ? 1 : part.kind == "ribbon" ? 2 : part.kind == "arc" ? 3 : 0);
            material.SetFloat("_ZWrite",mode >= 3 ? 1 : 0);
            material.SetFloat("_Cull",part.kind == "ribbon" ? 0 : 2);
            material.renderQueue = mode >= 3 ? 2500 : part.material == "energy" ? 3022 : 3018;
            return material;
        }

        private void Update()
        {
            if (!initialized) return;
            var age = Time.time - born;
            if (impact && age >= ImpactLifetime) { Destroy(gameObject); return; }
            if (retirementStarted >= 0 && Time.time - retirementStarted >= retirementDuration)
            {
                // This component may live on a gameplay carrier. Retiring
                // decoration must never destroy its host or its colliders.
                foreach (var part in parts) if (part.transform != null) Destroy(part.transform.gameObject);
                Destroy(this); return;
            }
            Animate(age);
        }

        private void Animate(float age)
        {
            var fade = impact ? 1f - Mathf.SmoothStep(0,1,Mathf.Clamp01((age / ImpactLifetime - .12f) / .88f))
                : 1f - Mathf.SmoothStep(0,1,Mathf.Clamp01((age - lifetime + .16f) / .16f));
            if (retirementStarted >= 0) fade *= 1f - Mathf.SmoothStep(0,1,(Time.time - retirementStarted) / retirementDuration);
            for (var i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var emergence = impact ? 1f : Mathf.SmoothStep(0,1,Mathf.Clamp01((age - (i % 7) * .009f) / birthDuration));
                var position = part.position * Mathf.Lerp(.82f,1,emergence);
                var rotation = part.rotation;
                var phase = age * part.frequency * Mathf.PI * 2 + part.phase;
                if (impact)
                {
                    position += part.velocity * age + Vector3.down * age * age * .6f;
                    rotation *= Quaternion.Euler(age * (35 + i % 5 * 17),age * (i % 7 * 11),age * 37);
                }
                else switch (part.motion)
                {
                    case "flutter":
                        position += Vector3.up * (Mathf.Sin(phase) * part.amplitude * .22f);
                        var flutter = Mathf.Min(28,part.amplitude * 22f);
                        rotation *= Quaternion.Euler(Mathf.Sin(phase) * flutter,0,Mathf.Sin(phase + .8f) * flutter * .28f);
                        break;
                    case "orbit":
                        position += new Vector3(Mathf.Cos(phase),Mathf.Sin(phase * .5f) * .18f,Mathf.Sin(phase)) * part.amplitude;
                        rotation *= Quaternion.Euler(0,age * part.frequency * 45,0);
                        break;
                    case "drift":
                        position += new Vector3(Mathf.Sin(phase * .73f) * .35f,Mathf.Sin(phase),Mathf.Cos(phase) * .45f) * part.amplitude;
                        break;
                }
                part.transform.localPosition = presentationOffset + presentationRotation * Vector3.Scale(position,presentationScale);
                part.transform.localRotation = presentationRotation * rotation;
                var growth = Mathf.Lerp(.04f,1,emergence) * (impact ? Mathf.Lerp(1,.34f,age / ImpactLifetime) : 1);
                part.transform.localScale = Vector3.Scale(part.scale,presentationScale) * growth;
                if (beamActive && part.beamSamples != null)
                {
                    if (beamNeedsUpload || emergence < 1 || part.motion != "still" && part.frequency > 0 && part.amplitude > 0)
                        DeformBeamPart(part,position,rotation,growth);
                    part.transform.localPosition = Vector3.zero;
                    part.transform.localRotation = Quaternion.identity;
                    part.transform.localScale = Vector3.one;
                }
                properties.Clear(); properties.SetFloat(EnvelopeId,fade * emergence); properties.SetFloat(SeedId,part.seed);
                properties.SetFloat(ArmedId,armed ? 1 : 0);
                part.renderer.SetPropertyBlock(properties);
            }
            beamNeedsUpload = false;
        }

        private int BeamSegment(float z)
        {
            var low = 0; var high = beamPointCount - 2;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (z > beamCuts[middle + 1]) low = middle + 1;
                else high = middle;
            }
            return low;
        }

        private void DeformBeamPart(PartState part, Vector3 position, Quaternion rotation, float growth)
        {
            var motion = rotation * Quaternion.Inverse(part.rotation);
            var motionMatrix = Matrix4x4.Rotate(motion);
            for (var row = 0; row < 2; row++)
                for (var column = 0; column < 3; column++) motionMatrix[row,column] *= growth;
            motionMatrix.SetRow(2,new Vector4(0,0,1,0));
            var normalMatrix = motionMatrix.inverse.transpose;
            var stretch = beamLength / Mathf.Max(.0001f,authoredBounds.size.z);
            var bounds = new Bounds();
            for (var i = 0; i < part.beamSamples.Count; i++)
            {
                var sample = part.beamSamples[i];
                var point = position + motion * (sample.position - part.position) * growth;
                // The physical path owns the longitudinal coordinate. Birth
                // and motion animate the transverse section, so they cannot
                // move a bend past the clipping plane or past the last hit.
                point.z = sample.position.z;
                var normal = normalMatrix.MultiplyVector(sample.normal);
                var segment = BeamSegment(point.z);
                var distance = Mathf.Clamp01((point.z - authoredBounds.min.z) / Mathf.Max(.0001f,authoredBounds.size.z)) * beamLength;
                var segmentLength = Mathf.Max(.0001f,beamDistances[segment + 1] - beamDistances[segment]);
                var t = Mathf.Clamp01((distance - beamDistances[segment]) / segmentLength);
                var frame = Quaternion.Slerp(beamFrames[segment],beamFrames[segment + 1],t);
                var right = frame * Vector3.right; var up = frame * Vector3.up;
                var tangent = (beamPoints[segment + 1] - beamPoints[segment]) / segmentLength;
                var result = Vector3.LerpUnclamped(beamPoints[segment],beamPoints[segment + 1],t) + right * point.x + up * point.y;
                // Inverse transpose via Jacobian cofactors also accounts for
                // the transported frame turning along the current segment.
                var longitudinal = stretch * (tangent + (Vector3.Cross(beamAngular[segment],right) * point.x +
                    Vector3.Cross(beamAngular[segment],up) * point.y) / segmentLength);
                var transformedNormal = normal.x * Vector3.Cross(up,longitudinal) +
                    normal.y * Vector3.Cross(longitudinal,right) + normal.z * Vector3.Cross(right,up);
                if (transformedNormal.sqrMagnitude < .000000001f) transformedNormal = frame * normal;
                part.beamVertices[i] = result; part.beamNormals[i] = transformedNormal.normalized;
                if (i == 0) bounds = new Bounds(result,Vector3.zero); else bounds.Encapsulate(result);
            }
            part.mesh.SetVertices(part.beamVertices); part.mesh.SetNormals(part.beamNormals);
            bounds.Expand(.01f); part.mesh.bounds = bounds;
        }

        private static Mesh BuildMesh(SpellVisualPart part)
        {
            switch (part.kind)
            {
                case "ellipsoid": return Ellipsoid();
                case "shard": return Shard();
                case "feather": return Feather();
                case "ribbon": return Path(part,false);
                case "arc": return Path(part,true);
                case "ring": return Ring();
                default: throw new ArgumentException("Unsupported image construction primitive");
            }
        }

        private static Mesh Ellipsoid()
        {
            const int longitude = 24, latitude = 16;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var row = 0; row <= latitude; row++)
            {
                var v = row / (float)latitude; var theta = v * Mathf.PI;
                for (var column = 0; column <= longitude; column++)
                {
                    var u = column / (float)longitude; var a = u * Mathf.PI * 2;
                    vertices.Add(new Vector3(Mathf.Sin(theta) * Mathf.Cos(a),Mathf.Cos(theta),Mathf.Sin(theta) * Mathf.Sin(a)) * .5f);
                    uv.Add(new Vector2(u,v));
                    if (row < latitude && column < longitude) Grid(triangles,row * (longitude + 1) + column,longitude + 1,false);
                }
            }
            return Finish("Image ellipsoid",vertices,uv,triangles);
        }

        private static Mesh Shard()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            const int sides = 7;
            for (var ring = 0; ring < 4; ring++)
                for (var side = 0; side < sides; side++)
                {
                    var a = side * Mathf.PI * 2 / sides;
                    var radius = ring == 0 ? .012f : ring == 1 ? .5f : ring == 2 ? .34f : .002f;
                    var z = ring == 0 ? -.5f : ring == 1 ? -.22f : ring == 2 ? .16f : .5f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius,Mathf.Sin(a) * radius,z));
                    uv.Add(new Vector2((z + .5f),side / (float)sides));
                    if (ring < 3)
                    {
                        var n = ring * sides + side; var next = ring * sides + (side + 1) % sides;
                        triangles.AddRange(new[] { n,next,n+sides,next,next+sides,n+sides });
                    }
                }
            return Finish("Image faceted shard",vertices,uv,triangles,true);
        }

        private static Mesh Feather()
        {
            const int segments = 28, widthSegments = 6;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var face = 0; face < 2; face++)
                for (var row = 0; row <= segments; row++)
                {
                    var t = row / (float)segments;
                    var breadth = Mathf.Pow(Mathf.Max(0,Mathf.Sin(t * Mathf.PI)),.72f) * (.5f - t * .12f) + .002f;
                    for (var column = 0; column <= widthSegments; column++)
                    {
                        var v = column / (float)widthSegments; var across = v * 2 - 1;
                        var notch = 1f - Mathf.Pow(Mathf.Abs(across),4) * .07f * Mathf.Pow(Mathf.Sin(t * 35),2);
                        vertices.Add(new Vector3(across * breadth * notch + Mathf.Sin(t * Mathf.PI) * t * .075f,
                            (face == 0 ? 1 : -1) * (.025f + (1 - Mathf.Abs(across)) * .39f) * Mathf.Sin(t * Mathf.PI)
                                + Mathf.Sin(t * Mathf.PI) * .055f,t));
                        uv.Add(new Vector2(t,v));
                        if (row < segments && column < widthSegments)
                            Grid(triangles,face * (segments + 1) * (widthSegments + 1) + row * (widthSegments + 1) + column,widthSegments + 1,face == 0);
                    }
                }
            return Finish("Image tapered feather",vertices,uv,triangles);
        }

        private static Mesh Ring()
        {
            const int segments = 64, sides = 6;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var row = 0; row <= segments; row++)
                for (var side = 0; side <= sides; side++)
                {
                    var t = row / (float)segments; var a = t * Mathf.PI * 2;
                    var v = side / (float)sides; var b = v * Mathf.PI * 2;
                    var radius = .475f + Mathf.Cos(b) * .025f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius,Mathf.Sin(b) * .5f,Mathf.Sin(a) * radius));
                    uv.Add(new Vector2(t,v));
                    if (row < segments && side < sides) Grid(triangles,row * (sides + 1) + side,sides + 1,false);
                }
            return Finish("Image ring",vertices,uv,triangles);
        }

        private static Mesh Path(SpellVisualPart part, bool tube)
        {
            if (part.points_cm == null || part.points_cm.Count < 2 || part.points_cm.Count > 16)
                throw new ArgumentException("Visual paths require 2 to 16 local control points");
            var points = new Vector3[part.points_cm.Count];
            for (var i = 0; i < points.Length; i++) points[i] = Centimetres(part.points_cm[i],-1000,1000);
            var width = Centimetres(part.scale_cm,1,1000).x;
            var steps = Mathf.Clamp((points.Length - 1) * 6,12,90);
            var crossSegments = tube ? 6 : 4;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var row = 0; row <= steps; row++)
            {
                var t = row / (float)steps; var center = Curve(points,t);
                var tangent = Curve(points,Mathf.Min(1,t + .004f)) - Curve(points,Mathf.Max(0,t - .004f));
                if (tangent.sqrMagnitude < .0000001f) tangent = Vector3.forward;
                tangent.Normalize();
                var side = Vector3.Cross(Mathf.Abs(tangent.y) > .9f ? Vector3.forward : Vector3.up,tangent).normalized;
                var up = Vector3.Cross(tangent,side).normalized;
                var taper = tube ? .34f + Mathf.Sin(t * Mathf.PI) * .66f : .12f + Mathf.Pow(Mathf.Max(0,Mathf.Sin(t * Mathf.PI)),.55f) * .88f;
                for (var column = 0; column <= crossSegments; column++)
                {
                    var v = column / (float)crossSegments; var a = v * Mathf.PI * 2;
                    var point = tube ? center + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * width * .5f * taper
                        : center + side * ((v * 2 - 1) * width * .5f * taper) + up * (Mathf.Sin(v * Mathf.PI * 2 + t * 5) * width * .08f * taper);
                    vertices.Add(point); uv.Add(new Vector2(t,v));
                    if (row < steps && column < crossSegments) Grid(triangles,row * (crossSegments + 1) + column,crossSegments + 1,false);
                }
            }
            return Finish(tube ? "Image curved energy filament" : "Image swept energy sheet",vertices,uv,triangles);
        }

        private static Vector3 Curve(Vector3[] points, float t)
        {
            var along = Mathf.Clamp01(t) * (points.Length - 1);
            var index = Mathf.Min(points.Length - 2,Mathf.FloorToInt(along)); var fraction = along - index;
            var a = points[Mathf.Max(0,index - 1)]; var b = points[index];
            var c = points[index + 1]; var d = points[Mathf.Min(points.Length - 1,index + 2)];
            return .5f * ((2 * b) + (-a + c) * fraction + (2 * a - 5 * b + 4 * c - d) * fraction * fraction
                + (-a + 3 * b - 3 * c + d) * fraction * fraction * fraction);
        }

        private static void Grid(List<int> triangles, int index, int stride, bool reverse)
        {
            if (reverse) triangles.AddRange(new[] { index,index+stride,index+1,index+1,index+stride,index+stride+1 });
            else triangles.AddRange(new[] { index,index+1,index+stride,index+1,index+stride+1,index+stride });
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, bool faceted = false)
        {
            if (faceted)
            {
                var flatVertices = new List<Vector3>(triangles.Count); var flatUv = new List<Vector2>(triangles.Count);
                for (var i = 0; i < triangles.Count; i++)
                {
                    flatVertices.Add(vertices[triangles[i]]); flatUv.Add(uv[triangles[i]]); triangles[i] = i;
                }
                vertices = flatVertices; uv = flatUv;
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (var item in owned) if (item != null) Destroy(item);
            owned.Clear(); parts.Clear();
        }
    }
}
