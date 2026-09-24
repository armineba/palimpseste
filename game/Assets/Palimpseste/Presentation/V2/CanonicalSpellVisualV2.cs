using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    [Serializable]
    public sealed class V2VisualMetrics
    {
        public int particle_count, vfx_systems, mesh_vertices, mesh_triangles, estimated_draw_calls, material_count;
        public int transparent_layers, decorative_colliders, decorative_rigidbodies;
        public long cpu_update_microseconds;
        public string draw_calls_measurement = "renderer submission estimate; GPU batching not measured";
        public string transparent_overdraw = "unavailable: requires GPU pixel instrumentation";
        public string gpu_cost = "unavailable: per-spell GPU timestamps not exposed by this player";
    }

    /// <summary>
    /// Independent V2 renderer. One canonical structure + continuous motion(t).
    /// No blueprint can provide code, shader paths or physical child objects.
    /// The existing gameplay runtime retains exclusive authority over contacts.
    /// </summary>
    public sealed class CanonicalSpellVisualV2 : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private SpellBlueprintV2 blueprint;
        private CanonicalCoreGeometryV2.Result geometry;
        private Mesh coreMesh;
        private Vector3[] bindVertices, vertices;
        private Vector2[] coordinates;
        private MeshRenderer coreRenderer, energyRenderer;
        private Material coreMaterial, energyMaterial, particleMaterial;
        private Transform coreRoot, secondaryRoot, atmosphereRoot, impactRoot;
        private ParticleSystem atmosphere;
        private ParticleSystem.Particle[] particles;
        private ParticleSystem contactParticles;
        private ParticleSystem.Particle[] contactParticleBuffer;
        private MaterialPropertyBlock properties;
        private Bounds bindBounds;
        private float born, retirementStart = -1, retirementDuration, retirementBaseSeconds, lastSeconds;
        private float pulseRadius = -1;
        private float contactTime = -10000;
        private bool initialized, manualSampling, coreOnly, retirementContact, destroyWhenFinished;
        private Vector3 contactNormal = Vector3.back;
        private Vector3[] beamPath;
        private long updateMicroseconds;
        public string CoreTopologyFingerprint { get; private set; }
        public string NodeId { get; private set; }
        public int CanonicalConnectedComponents { get; private set; }
        public Bounds CoreBounds => coreMesh == null ? new Bounds() : coreMesh.bounds;
        public bool IsRetiring => retirementStart >= 0;
        public string CoreKind => blueprint?.structural_core?.kind;
        public SpellBlueprintV2 Blueprint => blueprint;
        public bool ActiveLoopRequested => blueprint?.phases?.active_loop == true;
        public float CurrentLoopPhase { get; private set; }

        public static bool Supports(SpellNode node) => node?.blueprint_v2 != null;

        public void Initialize(SpellNode node)
        {
            if (initialized) throw new InvalidOperationException("V2 renderer already initialized");
            blueprint = node?.blueprint_v2 ?? throw new InvalidOperationException("Missing V2 blueprint");
            NodeId = node.node_id;
            var preflight = SpellBlueprintV2Safety.Validate(blueprint);
            if (preflight.Count != 0) throw new InvalidOperationException("Invalid V2 blueprint: " + string.Join("; ", preflight));
            if (blueprint.schema_version != SpellBlueprintV2Limits.SchemaVersion)
                throw new InvalidOperationException("Unsupported V2 blueprint schema");
            if (blueprint.physics.decorative_physics || blueprint.physics.rigidbody_count > 1)
                throw new InvalidOperationException("V2 forbids independent decorative physics");
            transform.name = "Spell_ROOT";
            Child("Gameplay"); coreRoot = Child("Core"); secondaryRoot = Child("SecondaryVFX");
            atmosphereRoot = Child("AtmosphericVFX"); impactRoot = Child("Impact");
            geometry = CanonicalCoreGeometryV2.Build(blueprint.structural_core);
            coreMesh = Own(geometry.mesh); bindVertices = coreMesh.vertices; vertices = new Vector3[bindVertices.Length];
            coordinates = coreMesh.uv; bindBounds = coreMesh.bounds;
            if (bindVertices.Length > blueprint.validation_rules.maximum_vertices)
                throw new InvalidOperationException("Canonical V2 core exceeds its declared vertex budget");
            CoreTopologyFingerprint = HashTopology(coreMesh.triangles, bindVertices.Length);
            CanonicalConnectedComponents = ConnectedComponents(bindVertices, coreMesh.triangles);
            if (CanonicalConnectedComponents != blueprint.identity.element_count)
                throw new InvalidOperationException("V2 structure has " + CanonicalConnectedComponents +
                    " disconnected components; identity declares " + blueprint.identity.element_count);
            var shader = Resources.Load<Shader>("CanonicalSpellCoreV2");
            if (shader == null) throw new InvalidOperationException("CanonicalSpellCoreV2 shader missing");
            coreMaterial = Own(MakeMaterial(shader, false));
            coreRenderer = MakeRenderer(coreRoot, coreMesh, coreMaterial);
            if (blueprint.rendering_layers.secondary_kind != "none" && blueprint.rendering_layers.secondary_intensity_milli > 0)
            {
                energyMaterial = Own(MakeMaterial(shader, true));
                energyRenderer = MakeRenderer(Child("Energy", secondaryRoot), coreMesh, energyMaterial);
                energyRenderer.transform.localScale = Vector3.one * 1.003f;
            }
            if (blueprint.rendering_layers.atmosphere_kind != "none" && blueprint.rendering_layers.atmosphere_particles > 0)
                CreateAtmosphere(shader);
            if (blueprint.impact.secondary_emission > 0) CreateContactParticles(shader);
            properties = new MaterialPropertyBlock();
            born = Time.time; initialized = true;
            SetCoreOnly(false); Animate(0, 0, false);
        }

        private Transform Child(string name, Transform parent = null)
        {
            var child = new GameObject(name).transform; child.SetParent(parent == null ? transform : parent, false); return child;
        }

        private static MeshRenderer MakeRenderer(Transform host, Mesh mesh, Material material)
        {
            host.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = host.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private Material MakeMaterial(Shader shader, bool secondary)
        {
            var material = new Material(shader) { name = secondary ? "V2 secondary energy" : "V2 canonical surface" };
            var rgb = blueprint.structural_core.color_rgb;
            material.SetColor("_BaseColor", new Color(rgb[0] / 255f, rgb[1] / 255f, rgb[2] / 255f, 1));
            material.SetFloat("_Opacity", secondary ? blueprint.rendering_layers.secondary_intensity_milli * .00018f :
                blueprint.rendering_layers.core_opacity_milli / 1000f);
            material.SetFloat("_Emission", secondary ? 1.8f : blueprint.rendering_layers.core_emission_milli / 1000f);
            var surface = blueprint.rendering_layers.core_surface;
            material.SetFloat("_Surface", surface == "plasma" ? 1 : surface == "forcefield" ? 2 : surface == "toxic" ? 3 : surface == "unlit" ? 0 : 4);
            material.SetFloat("_Secondary", secondary ? 1 : 0);
            material.SetFloat("_Flow", blueprint.motion.frequency_mhz / 1000f);
            material.SetTexture("_SurfaceTex", Resources.Load<Texture2D>("SourcedVfx/tinyplay_plasma"));
            material.SetTexture("_NoiseTex", Resources.Load<Texture2D>("SourcedVfx/tinyplay_noise"));
            material.SetTexture("_ResourceTex", SpellResourceLibrary.Load(blueprint.rendering_layers.resource_id));
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0); material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private void CreateAtmosphere(Shader shader)
        {
            var host = Child(blueprint.rendering_layers.atmosphere_kind, atmosphereRoot);
            atmosphere = host.gameObject.AddComponent<ParticleSystem>(); atmosphere.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = atmosphere.main; main.playOnAwake = false; main.loop = false; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = blueprint.rendering_layers.atmosphere_particles; main.startLifetime = 1000; main.startSpeed = 0;
            var emission = atmosphere.emission; emission.enabled = false;
            var shape = atmosphere.shape; shape.enabled = false;
            var collision = atmosphere.collision; collision.enabled = false;
            var renderer = atmosphere.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleMaterial = Own(MakeMaterial(shader, true)); particleMaterial.SetFloat("_Particle", 1);
            renderer.sharedMaterial = particleMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles = new ParticleSystem.Particle[main.maxParticles];
        }

        private void CreateContactParticles(Shader shader)
        {
            var host = Child("Contact emission", impactRoot);
            contactParticles = host.gameObject.AddComponent<ParticleSystem>();
            contactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = contactParticles.main; main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startSpeed = 0; main.startLifetime = 1000;
            main.maxParticles = blueprint.impact.secondary_emission;
            var emission = contactParticles.emission; emission.enabled = false;
            var shape = contactParticles.shape; shape.enabled = false;
            var collision = contactParticles.collision; collision.enabled = false;
            var renderer = contactParticles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (particleMaterial == null) { particleMaterial = Own(MakeMaterial(shader, true)); particleMaterial.SetFloat("_Particle", 1); }
            renderer.sharedMaterial = particleMaterial;
            contactParticleBuffer = new ParticleSystem.Particle[main.maxParticles];
        }

        public void SetManualSampling(bool value)
        {
            if (manualSampling && !value) born = Time.time - lastSeconds;
            manualSampling = value;
        }

        public void SetCoreOnly(bool value)
        {
            coreOnly = value;
            if (coreMaterial != null)
            {
                coreMaterial.SetFloat("_CoreOnly", value ? 1 : 0);
                // CORE_ONLY is opaque during its active phase. Its uniform
                // lifetime fade still needs alpha blending; replacing the fade
                // with a shape contraction would contradict the blueprint.
                coreMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                coreMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                coreMaterial.SetInt("_ZWrite", value ? 1 : 0);
                coreMaterial.renderQueue = value ? (int)RenderQueue.Geometry : (int)RenderQueue.Transparent;
            }
            if (secondaryRoot != null) secondaryRoot.gameObject.SetActive(!value);
            if (atmosphereRoot != null) atmosphereRoot.gameObject.SetActive(!value);
            if (impactRoot != null) impactRoot.gameObject.SetActive(!value);
        }

        // Captures sample the same function as Update. No random seed or geometry
        // generation occurs here, and setting a prior time restores the same frame.
        public void SampleNormalized(float normalizedTime)
        {
            if (!initialized) throw new InvalidOperationException("V2 renderer is not initialized");
            manualSampling = true;
            var t = Mathf.Clamp01(normalizedTime); var activeEnd = blueprint.phases.active_end_milli / 1000f;
            var retirement = t <= activeEnd ? 0 : Mathf.InverseLerp(activeEnd, 1, t);
            Animate(t * blueprint.motion.duration_ms / 1000f, retirement, false);
        }

        public void SetContactNormal(Vector3 normal)
        {
            if (normal.sqrMagnitude > .00001f) contactNormal = transform.InverseTransformDirection(normal.normalized);
        }

        public void NotifyContact(Vector3 normal)
        {
            SetContactNormal(normal); contactTime = Time.time;
        }

        public float BeginImpact(Vector3 normal)
        {
            SetContactNormal(normal); return RetireForCause(true);
        }

        public float RetireForCause(bool contact)
        {
            if (IsRetiring) return Mathf.Max(0, retirementDuration - (Time.time - retirementStart));
            retirementStart = Time.time; retirementBaseSeconds = lastSeconds;
            retirementContact = contact;
            retirementDuration = contact ? Mathf.Max(blueprint.impact.reaction_duration_ms,
                blueprint.impact.surviving_core_duration_ms) / 1000f :
                (1f - blueprint.phases.active_end_milli / 1000f) * blueprint.motion.duration_ms / 1000f;
            retirementDuration = Mathf.Max(.06f, retirementDuration + blueprint.disappearance.residual_duration_ms * .001f);
            return retirementDuration;
        }

        public void Arm() { if (coreMaterial != null) coreMaterial.SetFloat("_Armed", 1); }
        public void SetPulseRadius(float radius) { pulseRadius = Mathf.Max(.01f, radius); }
        public void SetBeamPath(IList<Vector3> worldPoints)
        {
            if (worldPoints == null || worldPoints.Count < 2) return;
            beamPath = new Vector3[Mathf.Min(64, worldPoints.Count)];
            for (var i = 0; i < beamPath.Length; i++) beamPath[i] = transform.InverseTransformPoint(worldPoints[i]);
        }

        public static GameObject SpawnContactVisual(SpellNode node, Vector3 point, Vector3 forward, Vector3 normal)
        {
            var host = new GameObject("V2 continuing contact"); host.transform.position = point;
            if (forward.sqrMagnitude > .001f) host.transform.rotation = Quaternion.LookRotation(forward);
            var visual = host.AddComponent<CanonicalSpellVisualV2>(); visual.Initialize(node);
            visual.destroyWhenFinished = true; visual.BeginImpact(normal); return host;
        }

        private void Update()
        {
            if (!initialized || manualSampling) return;
            var seconds = Time.time - born;
            var ending = IsRetiring ? Mathf.Clamp01((Time.time - retirementStart) / retirementDuration) : 0;
            Animate(IsRetiring ? retirementBaseSeconds + Time.time - retirementStart : seconds, ending, retirementContact);
            if (destroyWhenFinished && ending >= 1) Destroy(gameObject);
        }

        private void Animate(float seconds, float ending, bool contact)
        {
            var start = Stopwatch.GetTimestamp(); lastSeconds = seconds;
            var motion = blueprint.motion; var duration = motion.duration_ms * .001f;
            var appearanceDuration = blueprint.phases.appearance_end_milli * .001f * duration;
            var activeDuration = (blueprint.phases.active_end_milli - blueprint.phases.appearance_end_milli) * .001f * duration;
            // A requested stable loop has its phase origin at the end of the
            // appearance. Shared validation requires whole declared rotation /
            // oscillation cycles in this interval, so rates are never silently
            // rounded and no sawtooth time reset can teleport the core.
            var animationSeconds = blueprint.phases.active_loop ? seconds - appearanceDuration : seconds;
            var loopPhase = animationSeconds / Mathf.Max(.001f, activeDuration);
            CurrentLoopPhase = Mathf.Repeat(loopPhase, 1);
            var reveal = Mathf.SmoothStep(0, 1, seconds / Mathf.Max(.001f, appearanceDuration));
            var angular = motion.angular_speed_mdeg_s * .001f * animationSeconds * Mathf.Deg2Rad;
            var wave = animationSeconds * motion.frequency_mhz * .001f * Mathf.PI * 2f;
            var amplitude = motion.amplitude_cm * .01f;
            var centre = bindBounds.center;
            var endingMode = contact ? blueprint.impact.destruction_mode : blueprint.disappearance.mode;
            var contactProgress = contact ? ending : Mathf.Clamp01((Time.time-contactTime) /
                Mathf.Max(.02f,blueprint.impact.reaction_duration_ms*.001f));
            var hasContact = contact || (!manualSampling && contactProgress < 1);
            var collapse = endingMode == "contract" ? Mathf.Lerp(1, .001f, ending * ending) : 1;
            var introScale = .08f + .92f * reveal;
            var anticipation = Mathf.Sin(Mathf.PI * reveal) * motion.anticipation_milli * .001f;
            var impulse = hasContact ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(contactProgress * 1.75f)) : 0;
            for (var i = 0; i < vertices.Length; i++)
            {
                var p = bindVertices[i]; var u = coordinates[i].x;
                var phase = wave - u * Mathf.PI * (3f + motion.follow_through_milli * .002f);
                if (geometry.entityIds.Length != 0)
                {
                    var id = geometry.entityIds[i]; var anchor = geometry.entityCentres[id];
                    var rotation = Quaternion.AngleAxis(angular * Mathf.Rad2Deg, Vector3.up);
                    p = rotation * anchor + (p - anchor) + Vector3.up * Mathf.Sin(wave + id * 1.7f) * amplitude;
                }
                switch (motion.deformation)
                {
                    case "undulate": p.x += Mathf.Sin(phase) * amplitude * Mathf.Sin(u * Mathf.PI); break;
                    case "flutter": p.y += Mathf.Sin(phase) * amplitude * Mathf.Sin(u * Mathf.PI); break;
                    case "twist":
                        p = Quaternion.AngleAxis(Mathf.Sin(phase) * amplitude * 70f, Vector3.forward) * p; break;
                    case "pulse": p = centre + (p - centre) * (1 + Mathf.Sin(wave) * Mathf.Min(.35f, amplitude)); break;
                    case "expand": p = centre + (p - centre) * (1 + Mathf.Min(2, seconds / Mathf.Max(.1f, duration)) * amplitude); break;
                    case "vortex":
                        var height = Mathf.InverseLerp(bindBounds.min.y, bindBounds.max.y, p.y);
                        p = Quaternion.AngleAxis((angular + height * .3f * Mathf.Min(1,amplitude) * Mathf.Sin(wave)) * Mathf.Rad2Deg, Vector3.up) * p;
                        p.x += Mathf.Sin(phase) * amplitude * height * .15f;
                        p.z += Mathf.Cos(phase) * amplitude * height * .15f; break;
                }
                if (motion.deformation != "vortex" && geometry.entityIds.Length == 0 && motion.angular_speed_mdeg_s != 0)
                    p = Quaternion.AngleAxis(angular * Mathf.Rad2Deg, Vector3.up) * p;
                if (hasContact)
                {
                    var projected = Vector3.Dot(p - centre, contactNormal);
                    if (blueprint.impact.contact_pose == "align_normal")
                        p = centre + Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.forward, -contactNormal), impulse * .55f) * (p - centre);
                    if (blueprint.impact.contact_pose == "flatten") p -= contactNormal * projected * impulse * .65f;
                    switch (blueprint.impact.deformation)
                    {
                        case "compress":
                            p -= contactNormal * projected * impulse * .28f;
                            p += Vector3.ProjectOnPlane(p - centre, contactNormal) * impulse * .08f; break;
                        case "ripple": p += contactNormal * Mathf.Sin(u * 16 - contactProgress * 16) * impulse * .06f; break;
                        case "expand": p = centre + (p - centre) * (1 + impulse * .2f); break;
                    }
                }
                if (ending > 0 && endingMode == "disperse")
                    p += DissipationDirection(p - centre) * (ending * ending * amplitude);
                p = centre + (p - centre) * introScale * collapse;
                p.z = centre.z + (p.z - centre.z) * (1 - anticipation * .25f);
                if (pulseRadius > 0)
                {
                    var ratio = pulseRadius / Mathf.Max(.01f, Mathf.Max(bindBounds.extents.x, bindBounds.extents.z));
                    p.x *= ratio; p.z *= ratio;
                }
                if (beamPath != null) p = BeamPoint(u, p);
                vertices[i] = p;
            }
            coreMesh.vertices = vertices; coreMesh.RecalculateNormals(); coreMesh.RecalculateBounds();
            var dissolve = endingMode == "dissolve" ? ending : 0;
            var fadeProgress = endingMode == "persist" ? Mathf.InverseLerp(.82f, 1, ending) : ending;
            var opacity = (1 - Mathf.SmoothStep(0, 1, fadeProgress));
            properties.Clear(); properties.SetFloat("_Age", seconds); properties.SetFloat("_Envelope", reveal * opacity);
            properties.SetFloat("_Reveal", reveal); properties.SetFloat("_Dissolve", dissolve);
            properties.SetFloat("_SecondaryMotion", motion.secondary_motion_milli * .001f);
            properties.SetFloat("_LoopEnabled", blueprint.phases.active_loop ? 1 : 0);
            properties.SetFloat("_LoopPhase", loopPhase);
            properties.SetFloat("_HitFlash", hasContact ? Mathf.Pow(1 - contactProgress, 8) * blueprint.impact.hit_flash_milli * .001f : 0);
            coreRenderer.SetPropertyBlock(properties); if (energyRenderer != null) energyRenderer.SetPropertyBlock(properties);
            if (particleMaterial != null)
            {
                particleMaterial.SetFloat("_LoopEnabled", blueprint.phases.active_loop ? 1 : 0);
                particleMaterial.SetFloat("_LoopPhase", loopPhase);
            }
            if (atmosphere != null && !coreOnly) SampleAtmosphere(animationSeconds, reveal * opacity, ending, loopPhase);
            if (contactParticles != null && !coreOnly) SampleContactEmission(seconds, hasContact ? contactProgress : -1);
            updateMicroseconds = (Stopwatch.GetTimestamp() - start) * 1000000 / Stopwatch.Frequency;
        }

        private Vector3 DissipationDirection(Vector3 radial)
        {
            switch (blueprint.disappearance.direction)
            {
                case "upward": return Vector3.up;
                case "longitudinal": return Vector3.forward;
                case "uniform": return Vector3.zero;
                default: return radial.normalized;
            }
        }

        private Vector3 BeamPoint(float t, Vector3 authored)
        {
            var length = 0f; for (var i = 1; i < beamPath.Length; i++) length += Vector3.Distance(beamPath[i - 1], beamPath[i]);
            var remaining = t * length;
            for (var i = 1; i < beamPath.Length; i++)
            {
                var segment = beamPath[i] - beamPath[i - 1]; var distance = segment.magnitude;
                if (remaining <= distance || i == beamPath.Length - 1)
                {
                    var tangent = distance < .0001f ? Vector3.forward : segment / distance;
                    var rotation = Quaternion.FromToRotation(Vector3.forward, tangent);
                    return beamPath[i - 1] + tangent * Mathf.Min(distance, remaining) + rotation * new Vector3(authored.x, authored.y, 0);
                }
                remaining -= distance;
            }
            return beamPath[beamPath.Length - 1];
        }

        private void SampleAtmosphere(float seconds, float envelope, float ending, float loopPhase)
        {
            var rgb = blueprint.structural_core.color_rgb; var color = new Color(rgb[0] / 255f, rgb[1] / 255f, rgb[2] / 255f, .32f * envelope);
            var kind = blueprint.rendering_layers.atmosphere_kind;
            for (var i = 0; i < particles.Length; i++)
            {
                var phase = Mathf.Repeat((blueprint.phases.active_loop ? loopPhase : seconds * .4f) + i * .61803399f, 1);
                // Stable point identity: no independent random shape interpretation.
                var index = (i * 7919) % vertices.Length; var origin = vertices[index];
                var angle = i * 2.39996323f + seconds * blueprint.motion.angular_speed_mdeg_s * .001f * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(angle), .7f, Mathf.Sin(angle));
                particles[i].position = origin + radial * phase * (kind == "smoke" ? .35f : .16f);
                particles[i].startSize = (kind == "smoke" ? .09f : .018f) * Mathf.Sin(phase * Mathf.PI) * envelope;
                particles[i].startColor = color; particles[i].remainingLifetime = 100; particles[i].startLifetime = 100;
                particles[i].velocity = Vector3.zero; particles[i].randomSeed = (uint)(i + 1);
            }
            atmosphere.SetParticles(particles, particles.Length);
            particleMaterial.SetFloat("_Age", seconds); particleMaterial.SetFloat("_Envelope", envelope);
        }

        private void SampleContactEmission(float seconds, float progress)
        {
            if (progress < 0) { contactParticles.SetParticles(contactParticleBuffer, 0); return; }
            var rgb = blueprint.structural_core.color_rgb;
            var rotation = Quaternion.FromToRotation(Vector3.up, contactNormal);
            for (var i = 0; i < contactParticleBuffer.Length; i++)
            {
                var angle = i * 2.39996323f;
                var radial = new Vector3(Mathf.Cos(angle), .25f + .08f * (i % 4), Mathf.Sin(angle));
                var direction = blueprint.impact.dissipation_direction == "normal" ? rotation * radial :
                    blueprint.impact.dissipation_direction == "forward" ? Quaternion.Euler(90,0,0) * radial : radial;
                if (blueprint.impact.dissipation_direction == "upward") direction = (radial * .25f + Vector3.up).normalized;
                contactParticleBuffer[i].position = direction * progress * (1 + (i % 5) * .13f);
                contactParticleBuffer[i].startSize = .045f * (1 - progress);
                contactParticleBuffer[i].startColor = new Color(rgb[0]/255f,rgb[1]/255f,rgb[2]/255f,.6f*(1-progress));
                contactParticleBuffer[i].remainingLifetime = contactParticleBuffer[i].startLifetime = 100;
                contactParticleBuffer[i].velocity = Vector3.zero;
            }
            contactParticles.SetParticles(contactParticleBuffer, contactParticleBuffer.Length);
            particleMaterial.SetFloat("_Age",seconds); particleMaterial.SetFloat("_Envelope",1-progress);
        }

        public Vector3[] GetCoreVertices() => vertices == null ? new Vector3[0] : (Vector3[])vertices.Clone();

        public V2VisualMetrics ReadMetrics()
        {
            return new V2VisualMetrics {
                particle_count = coreOnly ? 0 : (atmosphere == null ? 0 : atmosphere.particleCount) + (contactParticles == null ? 0 : contactParticles.particleCount),
                vfx_systems = 0, mesh_vertices = bindVertices?.Length ?? 0,
                mesh_triangles = coreMesh == null ? 0 : coreMesh.triangles.Length / 3,
                estimated_draw_calls = 1 + (!coreOnly && energyRenderer != null ? 1 : 0) + (!coreOnly && atmosphere != null ? 1 : 0) + (!coreOnly && contactParticles != null && contactParticles.particleCount > 0 ? 1 : 0),
                material_count = 1 + (energyRenderer != null ? 1 : 0) + (particleMaterial != null ? 1 : 0),
                transparent_layers = coreOnly ? 0 : 1 + (energyRenderer != null ? 1 : 0) + (atmosphere != null ? 1 : 0) + (contactParticles != null ? 1 : 0),
                decorative_colliders = coreRoot.GetComponentsInChildren<Collider>().Length + secondaryRoot.GetComponentsInChildren<Collider>().Length + atmosphereRoot.GetComponentsInChildren<Collider>().Length,
                decorative_rigidbodies = GetComponentsInChildren<Rigidbody>().Length,
                cpu_update_microseconds = updateMicroseconds
            };
        }

        private static string HashTopology(int[] triangles, int vertexCount)
        {
            using (var hash = SHA256.Create())
            {
                var bytes = new byte[(triangles.Length + 1) * sizeof(int)];
                Buffer.BlockCopy(triangles, 0, bytes, 0, triangles.Length * sizeof(int));
                Buffer.BlockCopy(BitConverter.GetBytes(vertexCount), 0, bytes, triangles.Length * sizeof(int), sizeof(int));
                var digest = hash.ComputeHash(bytes); var text = new StringBuilder(64);
                foreach (var value in digest) text.Append(value.ToString("x2")); return text.ToString();
            }
        }

        private static int ConnectedComponents(Vector3[] positions, int[] triangles)
        {
            // Weld only equal canonical coordinates for topology analysis. UV
            // seams and poles are logically continuous, despite duplicate UVs.
            var lookup = new Dictionary<Vector3Int,int>(); var parent = new int[positions.Length];
            for (var i=0;i<positions.Length;i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(positions[i].x*100000),
                    Mathf.RoundToInt(positions[i].y*100000),Mathf.RoundToInt(positions[i].z*100000));
                if (!lookup.TryGetValue(key,out var first)) { first=i; lookup.Add(key,i); }
                parent[i]=first;
            }
            for(var i=0;i<triangles.Length;i+=3)
            {
                var a=Find(parent,triangles[i]); var b=Find(parent,triangles[i+1]); var c=Find(parent,triangles[i+2]);
                parent[b]=a;parent[c]=a;
            }
            var roots=new HashSet<int>();for(var i=0;i<positions.Length;i++)roots.Add(Find(parent,i));
            return roots.Count;
        }

        private static int Find(int[] parent,int value)
        {
            while(parent[value]!=value){parent[value]=parent[parent[value]];value=parent[value];}return value;
        }

        private T Own<T>(T resource) where T : UnityEngine.Object { owned.Add(resource); return resource; }
        private void OnDestroy() { foreach (var resource in owned) if (resource != null) Destroy(resource); }
    }
}
