using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    /// <summary>
    /// Authored visual vocabulary driven only by validated data. It never adds
    /// colliders, gameplay events, scripts, asset paths or provider calls.
    /// Meshes/materials belong to the cast and are released with it.
    /// </summary>
    public sealed class SpellVfxComposition : MonoBehaviour
    {
        public const int MaximumParticlesPerLayer = 48;
        public const float ImpactDuration = 1.25f;
        private const int MaximumVisualLights = 4;
        private static int visualLights;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<float> opacities = new List<float>();
        private readonly List<Transform> ribbons = new List<Transform>();
        private readonly List<Transform> spectralVeils = new List<Transform>();
        private float spectralScale;
        private readonly List<ParticleSystem> particleLayers = new List<ParticleSystem>(4);
        private readonly List<Material> styledMaterials = new List<Material>();
        private Transform energyRoot, groundRoot, seal, corona, skirt, atmosphere, charge, chargeCorona, shockwave, secondWave, pillar, contactFlash;
        private Material contactFlashMaterial;
        private bool dissolving;
        private float dissolveStarted, dissolveDuration = .62f, pendingDissolve = -1;
        private float[] retiringOpacities;
        private SpellVisualLifecycle lifecycle;
        private SpellBehaviorIntent behavior;
        private SpellPhysicsProfile physicsProfile;
        private string resourceId;
        private Material chargeMaterial, chargeCoronaMaterial;
        private Light localLight;
        private Mesh quad;
        private Mesh beamFilaments;
        private Vector3[] beamPoints;
        private readonly List<Vector3> beamVertices = new List<Vector3>(384);
        private readonly List<Vector2> beamUvs = new List<Vector2>(384);
        private readonly List<int> beamIndices = new List<int>(1128);
        private readonly List<Color> beamColors = new List<Color>(384);
        private Color hue, accent;
        private string form, carrier, style, motif, impact;
        private int density;
        private float born, span, chargeSeconds, lifetime, pulseRadius = -1;
        private Vector3 chargePosition;
        private bool ephemeral, armed, ownsLight, groundComposition, restorative, emissionsStopped;

        public string Style => style;
        public string Motif => motif;
        public bool IsImpact => ephemeral;

        public void Initialize(SpellNode node, Color fallback, Vector3 center, float physicalSize)
        {
            Configure(node.appearance, fallback);
            lifecycle = node.appearance?.lifecycle;
            if (SpellBehaviorMotion.Enabled(node))
            {
                behavior = node.behavior; physicsProfile = node.physics; resourceId = node.appearance.resource_id;
                if (behavior.phenomenon == "vortex") motif = "vortex";
            }
            carrier = node.carrier;
            born = Time.time;
            lifetime = Mathf.Max(.1f, (node.options?.lifetime_ticks ?? 150) * Time.fixedDeltaTime);
            if (node.effects != null)
                for (var i = 0; i < node.effects.Count; i++)
                    if (node.effects[i].kind == "heal" || node.effects[i].kind == "regen") restorative = true;
            groundComposition = carrier == "field" || carrier == "trap" || carrier == "pulse";
            var profile = node.appearance?.vfx;
            span = Mathf.Clamp((profile?.aura_cm ?? 200) / 200f, .4f, 2f);
            span = Mathf.Max(span, Mathf.Min(physicalSize * .72f, 2.4f));
            if (groundComposition)
                span = Mathf.Clamp(node.scale_cm / 200f, .45f, 5f);
            chargeSeconds = lifecycle == null ? Mathf.Clamp((profile?.charge_ms ?? 420) / 1000f, .1f, .8f)
                : Mathf.Clamp((lifecycle.intro?.duration_ms ?? 420) / 1000f,.1f,3f);
            energyRoot = Child("Composed energy", transform);
            energyRoot.localPosition = center;
            quad = Own(Quad());

            var spectral = form == "spirit" && node.appearance.construction == null;
            if (groundComposition)
            {
                MakeGroundComposition();
            }
            else if (spectral)
            {
                // Wide translucent fabric with actual folds and torn edges,
                // not a sphere enclosing the interpreted character.
                var cloth = ClothMaterial(true);
                spectralScale = Mathf.Clamp(physicalSize, 1.1f, 1.6f);
                for (var i = 0; i < 6; i++)
                {
                    var veil = Surface("Long spectral veil " + i, energyRoot,
                        Own(Veil(i)), i == 0 ? cloth : ClothMaterial(true));
                    veil.GetComponent<Renderer>().sharedMaterial.SetFloat("_Seed", .37f + i * 1.731f);
                    veil.localScale = Vector3.one;
                    spectralVeils.Add(veil);
                }
                gameObject.AddComponent<SpectralWakeMotion>().Initialize(transform, energyRoot, spectralVeils, spectralScale);
            }
            else
            {
                // Silhouette belongs to the interpreted subject. These narrow
                // wakes accent its motion; no generic sphere encloses it.
                var ribbon = StylizedMaterial(1, .58f, 2.15f, 1.2f);
                var ribbonCount = density == 3 ? 3 : 2;
                for (var i = 0; i < ribbonCount; i++)
                {
                    var band = Surface("Tapered subject wake " + i, energyRoot,
                        Own(SubjectWake(i, motif)), ribbon);
                    band.localScale = Vector3.one * Mathf.Min(span, 1.4f);
                    ribbons.Add(band);
                }
            }
            if (carrier == "projectile")
            {
                if (!spectral) AddTrail(energyRoot, false);
                MakeCharge();
            }
            else if (carrier == "beam") MakeCharge();
            else if (!groundComposition)
            {
                seal = Surface("Inscribed barrier seal", transform, quad, StylizedMaterial(0, .52f, 1.8f, .45f));
                seal.localScale = Vector3.one * span * 2.5f;
                seal.rotation = Quaternion.Euler(90, 0, 0);
            }
            var particleRoot = groundComposition ? groundRoot : energyRoot;
            AddParticles("Rising light lances", particleRoot, 0, false);
            AddParticles("Orbiting luminous motes", particleRoot, 1, false);
            AddParticles("Four point magical glints", particleRoot, 2, false);
            if (density >= 2) AddParticles("Soft atmospheric wisps", particleRoot, 3, false);
            AddLight(energyRoot, span, 1.25f);
            TickVisuals(0);
        }

        private void MakeGroundComposition()
        {
            groundRoot = Child("Ground anchored composition", transform);
            groundRoot.position = new Vector3(transform.position.x, .07f, transform.position.z);
            groundRoot.rotation = Quaternion.identity;
            seal = Surface("Rotating inscribed inner seal", groundRoot, quad, StylizedMaterial(0, .76f, 2.4f, .35f));
            seal.localRotation = Quaternion.Euler(90, 0, 0);
            corona = Surface("Counter rotating segmented corona", groundRoot, quad, StylizedMaterial(4, .8f, 2.5f, -.55f));
            corona.localPosition = Vector3.up * .024f;
            corona.localRotation = Quaternion.Euler(90, 0, 0);
            atmosphere = Surface("Diffuse luminous ground atmosphere", groundRoot, quad, StylizedMaterial(5, .13f, .75f, .23f));
            atmosphere.localPosition = Vector3.up * .009f;
            atmosphere.localRotation = Quaternion.Euler(90, 0, 0);

            var flowing = restorative || style == "nature" || style == "water" || motif == "vortex" || motif == "petal";
            var ribbonCount = density == 3 ? 3 : 2;
            for (var i = 0; i < ribbonCount; i++)
            {
                var band = Surface(flowing ? "Ascending swept spiral " + i : "Sweeping arc of power " + i,
                    groundRoot, Own(SweptSpiral(i, flowing, motif)), StylizedMaterial(1, .74f, 2.65f, i % 2 == 0 ? 1.1f : .78f));
                ribbons.Add(band);
            }
            // A flared, serrated skirt creates a broken energetic silhouette;
            // its brightness lives at the foot and dissolves towards the tips.
            skirt = Surface("Flared crown of ascending light", groundRoot, Own(FlaredSkirt(flowing)),
                StylizedMaterial(2, flowing ? .32f : .62f, flowing ? 1.6f : 2.6f, flowing ? .75f : 1.5f));
        }

        private void Configure(SpellAppearance appearance, Color fallback)
        {
            form = appearance?.form ?? "orb";
            var profile = appearance?.vfx;
            style = profile?.style ?? StyleFor(appearance?.palette, appearance?.affinity);
            motif = profile?.motif ?? (form == "spirit" || form == "vortex" ? "vortex" :
                style == "lightning" ? "storm" : style == "earth" || style == "frost" ? "fracture" :
                style == "nature" ? "petal" : "runic");
            impact = profile?.impact ?? (style == "earth" || style == "frost" ? "shatter" :
                style == "water" ? "ripple" : "nova");
            density = Mathf.Clamp(profile?.density ?? 2, 1, 3);
            hue = StyleColor(style, fallback); hue.a = 1;
            accent = Color.Lerp(hue, Color.white, style == "shadow" ? .38f : .67f);
        }

        private static string StyleFor(string palette, string affinity)
        {
            switch (palette)
            {
                case "ember": case "lava": return "fire";
                case "ice": return "frost";
                case "water": return "water";
                case "stone": return "earth";
                case "storm": return "lightning";
                case "moss": return "nature";
                case "light": return "holy";
                case "shadow": return "shadow";
                case "arcane": return "arcane";
            }
            return affinity == "fire" ? "fire" : affinity == "stone" ? "earth" : affinity == "water" ? "water" : "arcane";
        }

        private static Color StyleColor(string value, Color fallback)
        {
            switch (value)
            {
                case "fire": return new Color(1f, .19f, .025f);
                case "frost": return new Color(.19f, .67f, 1f);
                case "lightning": return new Color(.15f, .42f, 1f);
                case "earth": return new Color(1f, .44f, .095f);
                case "poison": return new Color(.39f, 1f, .045f);
                case "holy": return new Color(1f, .71f, .19f);
                case "shadow": return new Color(.36f, .04f, .82f);
                case "nature": return new Color(.12f, .85f, .31f);
                case "water": return new Color(.035f, .67f, .87f);
                case "arcane": return new Color(.57f, .16f, 1f);
                default: return fallback;
            }
        }

        private T Own<T>(T item) where T : UnityEngine.Object { owned.Add(item); return item; }

        private Material Material(int mode, float opacity, float intensity)
        {
            var shader = Resources.Load<Shader>("SpellComposition");
            if (shader == null) throw new InvalidOperationException("SpellComposition shader missing from player");
            var result = Own(new Material(shader) { name = "Spell composition " + mode });
            result.SetColor("_Color", hue);
            result.SetColor("_Accent", accent);
            result.SetFloat("_Mode", mode);
            result.SetFloat("_Opacity", opacity);
            result.SetFloat("_Intensity", intensity);
            result.SetFloat("_Seed", FormSeed());
            result.SetFloat("_Motif", motif == "orbital" ? 1 : motif == "vortex" ? 2 : motif == "fracture" ? 3 : motif == "storm" ? 4 : motif == "petal" ? 5 : 0);
            SpellResourceLibrary.Bind(result,resourceId);
            materials.Add(result); opacities.Add(opacity);
            return result;
        }

        private Material StylizedMaterial(int mode, float opacity, float intensity, float flow)
        {
            var shader = Resources.Load<Shader>("SpellStylized");
            if (shader == null) throw new InvalidOperationException("SpellStylized shader missing from player");
            var result = Own(new Material(shader) { name = "Stylized spell layer " + mode });
            result.SetColor("_Color", hue);
            result.SetColor("_AccentColor", accent);
            result.SetFloat("_Mode", mode);
            result.SetFloat("_Opacity", opacity);
            result.SetFloat("_Intensity", intensity);
            result.SetFloat("_FlowSpeed", flow);
            result.SetFloat("_Softness", .55f);
            result.SetFloat("_Phase", FormSeed() * 9f + materials.Count * .73f);
            result.SetFloat("_Motif", motif == "orbital" ? 1 : motif == "vortex" ? 2 : motif == "fracture" ? 3 : motif == "storm" ? 4 : motif == "petal" ? 5 : 0);
            SpellResourceLibrary.Bind(result,resourceId);
            materials.Add(result); opacities.Add(opacity); styledMaterials.Add(result);
            return result;
        }

        private float FormSeed()
        {
            var seed = 17;
            foreach (var c in form) seed = (seed * 31 + c) & 65535;
            return seed / 65535f;
        }

        private Material ClothMaterial(bool torn)
        {
            var shader = Resources.Load<Shader>("SpellSpectralCloth");
            if (shader == null) throw new InvalidOperationException("SpellSpectralCloth shader missing from player");
            var result = Own(new Material(shader));
            result.SetColor("_Color", Color.Lerp(hue, new Color(.42f, .30f, .60f), .68f));
            result.SetColor("_Accent", new Color(.78f, .60f, 1f) * 2.15f);
            result.SetFloat("_Opacity", .88f);
            result.SetFloat("_Torn", torn ? 1 : 0);
            result.SetFloat("_Seed", FormSeed());
            materials.Add(result); opacities.Add(.88f);
            return result;
        }

        private static Transform Child(string name, Transform parent)
        {
            var result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        private static Transform Surface(string name, Transform parent, Mesh mesh, Material material)
        {
            var child = Child(name, parent);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return child;
        }

        private void MakeCharge()
        {
            chargeMaterial = StylizedMaterial(0, .9f, 2.7f, .5f);
            charge = Surface("Casting rune seal", transform, quad, chargeMaterial);
            chargePosition = behavior == null ? new Vector3(transform.position.x, .065f, transform.position.z)
                : transform.position + transform.up * .015f;
            charge.rotation = Quaternion.Euler(90, 0, 0);
            charge.localScale = Vector3.one * Mathf.Clamp(span * 2.7f, 1.8f, 4.6f);
            chargeCoronaMaterial = StylizedMaterial(4, .8f, 2.6f, -1.1f);
            chargeCorona = Surface("Casting broken corona", transform, quad, chargeCoronaMaterial);
            chargeCorona.rotation = Quaternion.Euler(90, 0, 0);
        }

        private void AddTrail(Transform parent, bool spectral)
        {
            var trail = Child("Chromatic wake", parent).gameObject.AddComponent<TrailRenderer>();
            trail.time = spectral ? .7f : .48f;
            trail.minVertexDistance = .06f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.startWidth = span * (spectral ? .3f : .48f);
            trail.endWidth = .015f;
            trail.sharedMaterial = StylizedMaterial(1, spectral ? .22f : .45f, 1.8f, 1.1f);
            trail.colorGradient = GradientFor(hue, .75f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private static Gradient GradientFor(Color color, float alpha)
        {
            var result = new Gradient();
            result.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(color,.22f), new GradientColorKey(color*.65f,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(alpha,.10f), new GradientAlphaKey(alpha*.65f,.65f), new GradientAlphaKey(0,1) });
            return result;
        }

        // Four authored layers have different sizes, velocities and rhythms.
        // Particle counts are capped separately; no gameplay events are emitted.
        private void AddParticles(string name, Transform parent, int layer, bool burst)
        {
            var child = Child(name, parent);
            var particles = child.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleLayers.Add(particles);
            var scale = Mathf.Min(span, groundComposition ? 3.5f : 1.3f);
            var main = particles.main;
            main.loop = !burst; main.playOnAwake = false;
            main.duration = burst ? ImpactDuration : 2f;
            var lifeMin = layer == 3 ? .85f : layer == 2 ? .35f : .7f;
            var lifeMax = layer == 3 ? 1.65f : layer == 2 ? .8f : 1.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, burst ? Mathf.Min(lifeMax, ImpactDuration) : lifeMax);
            main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(scale * 1.2f, scale * 4.4f) : new ParticleSystem.MinMaxCurve(0);
            var minimumSize = layer == 0 ? .035f : layer == 1 ? .055f : layer == 2 ? .17f : .65f;
            var maximumSize = layer == 0 ? .072f : layer == 1 ? .12f : layer == 2 ? .42f : 1.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(minimumSize * scale, maximumSize * scale);
            main.startColor = Color.white;
            main.maxParticles = MaximumParticlesPerLayer;
            main.simulationSpace = groundComposition && !burst ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startRotation = layer == 0 ? 0 : new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.gravityModifier = burst && (style == "earth" || style == "fire") ? .18f : 0;
            var emission = particles.emission;
            emission.rateOverTime = burst ? 0 : layer == 0 ? 10 + density * 4 : layer == 1 ? 8 + density * 3 : layer == 2 ? 3 + density : 3.5f;
            var count = layer == 0 ? 18 + density * 5 : layer == 1 ? 12 + density * 3 : layer == 2 ? 5 + density * 2 : 4;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(burst ? count : Mathf.Max(2, count / 3))) });
            var shape = particles.shape;
            shape.shapeType = groundComposition ? ParticleSystemShapeType.Circle : ParticleSystemShapeType.Sphere;
            shape.radius = span * (groundComposition ? .78f : .32f);
            shape.radiusThickness = groundComposition ? .4f : 1;
            if (form == "spirit" && !burst)
            {
                // Dust belongs to the long cloth wake rather than a fountain
                // at the head. World-space particles persist along the flight.
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.position = new Vector3(0, .1f, -1.6f);
                shape.scale = new Vector3(1.1f, .75f, 3.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(layer == 3 ? .6f : .018f,
                    layer == 3 ? 1.2f : layer == 2 ? .10f : .065f);
                emission.rateOverTime = layer == 3 ? 12 : layer == 2 ? 9 : 14;
            }
            if (groundComposition) shape.rotation = new Vector3(90, 0, 0);
            if (!burst)
            {
                var velocity = particles.velocityOverLifetime; velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                // Unity validates the three axes as one curve family: the
                // non-moving axes must also use TwoConstants, not Constant.
                velocity.x = new ParticleSystem.MinMaxCurve(0, 0);
                velocity.y = new ParticleSystem.MinMaxCurve(scale * (layer == 0 ? 1f : .2f), scale * (layer == 0 ? 2.8f : .8f));
                velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
                if (form == "spirit")
                {
                    velocity.y = new ParticleSystem.MinMaxCurve(-.08f, .13f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-.48f, -.18f);
                }
                velocity.orbitalX = new ParticleSystem.MinMaxCurve(0, 0);
                velocity.orbitalY = groundComposition && layer == 1
                    ? new ParticleSystem.MinMaxCurve(.7f, 1.5f)
                    : new ParticleSystem.MinMaxCurve(0, 0);
                velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0, 0);
                var noise = particles.noise; noise.enabled = layer != 0;
                noise.strength = layer == 3 ? .14f : .07f;
                noise.frequency = .65f; noise.scrollSpeed = .35f; noise.quality = ParticleSystemNoiseQuality.Low;
                if (behavior != null)
                {
                    var axis = SpellBehaviorMotion.Axis(behavior);
                    var speed = axis * Mathf.Clamp(physicsProfile.axial_speed_cm_s,-3000,3000) / 100f;
                    var angular = axis * SpellBehaviorMotion.AngularSpeed(behavior,physicsProfile) * Mathf.Deg2Rad;
                    var rotates = behavior.phenomenon == "vortex" || behavior.phenomenon == "orbit" || behavior.phenomenon == "spin";
                    velocity.x = new ParticleSystem.MinMaxCurve(speed.x,speed.x);
                    velocity.y = new ParticleSystem.MinMaxCurve(speed.y,speed.y);
                    velocity.z = new ParticleSystem.MinMaxCurve(speed.z,speed.z);
                    velocity.orbitalX = new ParticleSystem.MinMaxCurve(rotates ? angular.x : 0,rotates ? angular.x : 0);
                    velocity.orbitalY = new ParticleSystem.MinMaxCurve(rotates ? angular.y : 0,rotates ? angular.y : 0);
                    velocity.orbitalZ = new ParticleSystem.MinMaxCurve(rotates ? angular.z : 0,rotates ? angular.z : 0);
                    var radialSpeed = Mathf.Clamp(physicsProfile.radial_speed_cm_s,-3000,3000) / 100f;
                    velocity.radial = new ParticleSystem.MinMaxCurve(radialSpeed,radialSpeed);
                    noise.enabled = physicsProfile.turbulence_cm > 0;
                    noise.strength = Mathf.Clamp(physicsProfile.turbulence_cm,0,300) / 100f;
                    noise.frequency = Mathf.Clamp(physicsProfile.frequency_mhz,0,6000) / 1000f;
                    noise.scrollSpeed = Mathf.Max(.05f,Mathf.Abs(physicsProfile.axial_speed_cm_s) / 100f);
                    if (rotates)
                    {
                        shape.shapeType = ParticleSystemShapeType.Circle;
                        shape.rotation = Quaternion.FromToRotation(Vector3.forward,axis).eulerAngles;
                        if (physicsProfile.radius_cm > 0) shape.radius = Mathf.Clamp(physicsProfile.radius_cm,0,1000) / 100f;
                    }
                    if (behavior.phenomenon == "flow") shape.radius = Mathf.Clamp(physicsProfile.radius_cm,0,1000) / 100f;
                    main.simulationSpace = behavior.attachment == "caster" || groundComposition
                        ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
                    if (behavior.phenomenon == "vortex")
                    {
                        shape.rotation = new Vector3(90,0,0);
                        var spanSeconds = Mathf.Clamp(Mathf.Clamp(span * 3f,.5f,8f) / Mathf.Max(.1f,speed.magnitude),.12f,2.5f);
                        main.startLifetime = new ParticleSystem.MinMaxCurve(spanSeconds * .7f,spanSeconds);
                    }
                }
            }
            var color = particles.colorOverLifetime; color.enabled = true;
            // Color is supplied by the shader. Neutral particle tint avoids
            // multiplying violet/green twice and losing the white hot accents.
            color.color = GradientFor(Color.white, layer == 3 ? .3f : .9f);
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                new Keyframe(0, layer == 3 ? .45f : .12f), new Keyframe(.18f, 1),
                new Keyframe(.62f, layer == 3 ? 1.18f : .8f), new Keyframe(1, layer == 3 ? 1.4f : 0)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = StylizedMaterial(layer == 0 ? 6 : layer == 3 ? 5 : 3,
                layer == 3 ? form == "spirit" ? .18f : .13f : 1,
                layer == 3 ? form == "spirit" ? 1.2f : .65f : layer == 2 ? 3.2f : 2.5f, .6f);
            if (behavior != null) SpellResourceLibrary.Bind(renderer.sharedMaterial,resourceId,true);
            renderer.renderMode = layer == 0 ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (layer == 0) { renderer.lengthScale = burst ? 3.4f : 4.5f; renderer.velocityScale = .22f; }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play();
        }

        private void Sparks(string name, Transform parent, bool burst, float radius, bool streaks)
        {
            var child = Child(name, parent);
            var particles = child.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = !burst; main.playOnAwake = false;
            main.duration = burst ? .72f : 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(burst ? .28f : .4f, burst ? .7f : 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(burst ? radius*2.7f : .05f, burst ? radius*6f : .42f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius*(streaks ? .025f : .04f), radius*(streaks ? .065f : .12f));
            main.startColor = new Color(1,1,1,.92f);
            main.maxParticles = MaximumParticlesPerLayer;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.gravityModifier = burst ? .16f : -.06f;
            var emission = particles.emission;
            emission.rateOverTime = burst ? 0 : (streaks ? 13 : 18) + density*3;
            if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(streaks ? 24 + density*6 : 16 + density*6)) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius*(burst ? .13f : .55f);
            var color = particles.colorOverLifetime; color.enabled = true; color.color = GradientFor(hue,.9f);
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0,1,1,0));
            if (!burst)
            {
                var noise = particles.noise; noise.enabled = true; noise.strength = .13f;
                noise.frequency = .7f; noise.scrollSpeed = .4f; noise.quality = ParticleSystemNoiseQuality.Low;
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material(2, 1, streaks ? 3.2f : 2.3f);
            renderer.renderMode = streaks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (streaks) { renderer.lengthScale = burst ? 4f : 2.5f; renderer.velocityScale = .12f; }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play();
        }

        private void AddLight(Transform parent, float radius, float intensity)
        {
            if (visualLights >= MaximumVisualLights) return;
            localLight = Child("Spell radiance", parent).gameObject.AddComponent<Light>();
            localLight.type = LightType.Point;
            localLight.shadows = LightShadows.None;
            localLight.range = Mathf.Clamp(radius*4,2,7);
            localLight.intensity = intensity;
            localLight.color = hue;
            visualLights++; ownsLight = true;
        }

        public static void SpawnImpact(SpellAppearance appearance, Vector3 point, Vector3 direction, Color color, float radius)
        {
            var root = new GameObject("Semantic impact " + (appearance?.form ?? "orb"));
            root.transform.position = point;
            var visual = root.AddComponent<SpellVfxComposition>();
            visual.Configure(appearance, color);
            visual.ephemeral = true; visual.born = Time.time;
            visual.span = Mathf.Clamp(Mathf.Max(radius, (appearance?.vfx?.aura_cm ?? 160)/160f), .85f, 3.2f);
            visual.quad = visual.Own(Quad());
            visual.energyRoot = Child("Impact energy", root.transform);
            visual.shockwave = Surface("Expanding ground shockwave", root.transform, visual.quad, visual.StylizedMaterial(4,.9f,3.4f,.45f));
            visual.shockwave.rotation = Quaternion.Euler(90,0,0);
            visual.shockwave.position = new Vector3(point.x,.075f,point.z);
            visual.secondWave = Surface("Contact corona", root.transform, visual.quad, visual.StylizedMaterial(4,.72f,3f,-.75f));
            visual.secondWave.rotation = Quaternion.LookRotation(direction.sqrMagnitude>.001f ? direction : Vector3.forward);
            var filaments = Surface("Burst radial filaments", root.transform, visual.Own(BurstMesh(visual.impact=="shatter")), visual.StylizedMaterial(1,.9f,3.6f,1.8f));
            filaments.localScale = Vector3.one * visual.span;
            visual.ribbons.Add(filaments);
            if (visual.form == "spirit")
            {
                visual.energyRoot.rotation = Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : Vector3.forward);
                for (var i = 0; i < 7; i++)
                {
                    var cloth = visual.ClothMaterial(true);
                    cloth.SetFloat("_Seed", i * 1.37f);
                    var shard = Surface("Dispersing spectral cloth " + i, visual.energyRoot,
                        visual.Own(SpectralBurst(i)), cloth);
                    visual.ribbons.Add(shard);
                }
                visual.contactFlash = Child("Pearlescent spectral contact", visual.energyRoot);
                visual.contactFlashMaterial = visual.StylizedMaterial(3, 1f, 7f, 1);
                visual.contactFlashMaterial.SetColor("_Color", new Color(.87f,.74f,1f));
                visual.contactFlashMaterial.SetColor("_AccentColor", Color.white);
                // Crossed planes provide a contact flash visible around the
                // impact; no dependence on the laboratory's camera pose.
                for (var axis = 0; axis < 3; axis++)
                {
                    var flash = Surface("Contact radiance " + axis, visual.contactFlash, visual.quad, visual.contactFlashMaterial);
                    flash.localRotation = axis == 0 ? Quaternion.identity : axis == 1
                        ? Quaternion.Euler(0,90,0) : Quaternion.Euler(90,0,0);
                }
            }
            else if (visual.impact=="pillar" || visual.style=="holy" || visual.style=="fire" || visual.style=="arcane" || visual.style=="shadow")
            {
                visual.pillar = Surface("Flared impact light crown", root.transform, visual.Own(FlaredSkirt(false)), visual.StylizedMaterial(2,.72f,3.2f,1.3f));
                visual.pillar.localScale = new Vector3(visual.span,visual.span*1.7f,visual.span);
            }
            visual.AddParticles("Impact light lances", root.transform, 0, true);
            visual.AddParticles("Impact orbit fragments", root.transform, 1, true);
            visual.AddParticles("Impact four point glints", root.transform, 2, true);
            visual.AddParticles("Dissipating impact wisps", root.transform, 3, true);
            visual.AddLight(root.transform,visual.span,3);
            visual.TickVisuals(0);
        }

        public void SetBeamPath(List<Vector3> points)
        {
            if (points == null || points.Count == 0 || energyRoot == null) return;
            energyRoot.position = points[points.Count-1];
            var count = Mathf.Min(points.Count,64);
            if (beamPoints == null || beamPoints.Length != count) beamPoints = new Vector3[count];
            for(var i=0;i<count;i++) beamPoints[i] = points[i*(points.Count-1)/Mathf.Max(1,count-1)];
            if(beamFilaments==null)
            {
                beamFilaments=Own(new Mesh {name="Animated beam filaments"});
                beamFilaments.MarkDynamic();
                Surface("Helical beam filaments",transform,beamFilaments,Material(3,.65f,2.8f));
            }
            UpdateBeamFilaments();
        }

        private void UpdateBeamFilaments()
        {
            if(beamPoints==null || beamPoints.Length<2 || beamFilaments==null)return;
            beamVertices.Clear(); beamUvs.Clear(); beamIndices.Clear(); beamColors.Clear();
            const int samples=48;
            var total=0f;
            for(var i=1;i<beamPoints.Length;i++)total+=Vector3.Distance(beamPoints[i-1],beamPoints[i]);
            for(var band=0;band<2;band++)
            {
                var walked=0f; var segment=1;
                for(var i=0;i<=samples;i++)
                {
                    var t=i/(float)samples; var distance=t*total;
                    while(segment<beamPoints.Length-1 && walked+Vector3.Distance(beamPoints[segment-1],beamPoints[segment])<distance)
                    { walked+=Vector3.Distance(beamPoints[segment-1],beamPoints[segment]); segment++; }
                    var delta=beamPoints[segment]-beamPoints[segment-1];
                    var center=Vector3.Lerp(beamPoints[segment-1],beamPoints[segment],(distance-walked)/Mathf.Max(.001f,delta.magnitude));
                    var basis=Quaternion.LookRotation(delta.sqrMagnitude<.00001f ? Vector3.forward : delta.normalized);
                    var a=distance*4-Time.time*7+band*Mathf.PI;
                    var radius=span*.19f*Mathf.Sin(t*Mathf.PI);
                    var radial=basis*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                    center+=radial*radius;
                    var width=(style=="lightning" ? .018f : .036f)*span;
                    var n=beamVertices.Count;
                    beamVertices.Add(transform.InverseTransformPoint(center-radial*width));
                    beamVertices.Add(transform.InverseTransformPoint(center+radial*width));
                    beamUvs.Add(new Vector2(t,0));beamUvs.Add(new Vector2(t,1));
                    beamColors.Add(Color.white);beamColors.Add(Color.white);
                    if(i<samples)
                    {
                        beamIndices.Add(n); beamIndices.Add(n+1); beamIndices.Add(n+2);
                        beamIndices.Add(n+2); beamIndices.Add(n+1); beamIndices.Add(n+3);
                    }
                }
            }
            beamFilaments.Clear(); beamFilaments.SetVertices(beamVertices);beamFilaments.SetUVs(0,beamUvs);
            beamFilaments.SetColors(beamColors);beamFilaments.SetTriangles(beamIndices,0);beamFilaments.RecalculateNormals();beamFilaments.RecalculateBounds();
        }

        public void SetPulseRadius(float radius) { pulseRadius = Mathf.Max(.01f,radius); }
        public void Arm() { armed = true; }

        public void DissolveWake(float duration = .62f, float delay = 0)
        {
            if (dissolving) return;
            dissolveDuration = Mathf.Clamp(duration,.1f,3f);
            if (delay > 0)
            {
                pendingDissolve = Time.time + Mathf.Clamp(delay,0,3f);
                return;
            }
            dissolving = true;
            dissolveStarted = Time.time;
            if (lifecycle != null)
            {
                retiringOpacities = new float[materials.Count];
                for (var i = 0; i < materials.Count; i++)
                    retiringOpacities[i] = materials[i].GetFloat("_Opacity");
            }
            if (charge != null) charge.gameObject.SetActive(false);
            if (chargeCorona != null) chargeCorona.gameObject.SetActive(false);
            foreach (var particles in particleLayers)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            if (pendingDissolve >= 0 && Time.time >= pendingDissolve)
            {
                pendingDissolve = -1;
                DissolveWake(dissolveDuration);
            }
            TickVisuals(Time.time-born);
        }

        private void TickVisuals(float age)
        {
            for (var i = 0; i < styledMaterials.Count; i++) styledMaterials[i].SetFloat("_Age", age);
            if (behavior != null)
            {
                var flow = SpellBehaviorMotion.Flow(behavior,physicsProfile);
                foreach (var material in materials)
                {
                    material.SetVector("_BehaviorFlow",new Vector4(flow.x,flow.y,0,0));
                    material.SetFloat("_BehaviorAge",age);
                    material.SetFloat("_BehaviorEnabled",1);
                    material.SetFloat("_BehaviorMotion",behavior.phenomenon == "static" ? 0 : 1);
                }
            }
            if (dissolving)
            {
                // The mechanical carrier has already retired. Its existing
                // cloth keeps billowing while the hit flash and fragments fire.
                var t = Mathf.Clamp01((Time.time - dissolveStarted) / dissolveDuration);
                var envelope = 1 - Mathf.SmoothStep(0, 1, t);
                for (var i = 0; i < materials.Count; i++)
                    materials[i].SetFloat("_Opacity", (retiringOpacities == null ? opacities[i] : retiringOpacities[i]) * envelope);
                if (localLight != null) localLight.intensity = envelope * .7f;
                if (t >= 1) Destroy(gameObject);
                return;
            }
            if (ephemeral)
            {
                if (age>=ImpactDuration) { Destroy(gameObject); return; }
                var t=Mathf.Clamp01(age/ImpactDuration);
                var envelope=Mathf.Pow(1-t,1.45f);
                for (var i=0;i<materials.Count;i++) materials[i].SetFloat("_Opacity",opacities[i]*envelope);
                if (shockwave!=null) shockwave.localScale=Vector3.one*span*Mathf.Lerp(.35f,5f,1-Mathf.Pow(1-t,3));
                if (secondWave!=null) secondWave.localScale=Vector3.one*span*Mathf.Lerp(.28f,3.1f,Mathf.Sqrt(t));
                foreach (var ribbon in ribbons) ribbon.localScale=Vector3.one*span*Mathf.Lerp(.12f,1.8f,Mathf.Sqrt(t));
                if (contactFlash != null)
                {
                    var flashAge = Mathf.Clamp01(age / .24f);
                    contactFlash.localScale = Vector3.one * span * Mathf.Lerp(.5f, 2.4f, Mathf.Sqrt(flashAge));
                    contactFlashMaterial.SetFloat("_Opacity", Mathf.Pow(1-flashAge, 2));
                }
                if (pillar!=null) pillar.localScale=new Vector3(span*Mathf.Lerp(.5f,1.3f,t),span*Mathf.Lerp(.7f,2.7f,1-Mathf.Pow(1-t,3)),span*Mathf.Lerp(.5f,1.3f,t));
                if (localLight!=null) localLight.intensity=(3.4f+3f*Mathf.Exp(-age*24))*envelope;
                return;
            }
            var birth = lifecycle == null ? Mathf.SmoothStep(.08f,1,Mathf.Clamp01(age/.2f))
                : Mathf.Lerp(Mathf.Clamp01((lifecycle.intro?.opacity_start_milli ?? 0) / 1000f),1,
                    Mathf.SmoothStep(0,1,Mathf.Clamp01(age / chargeSeconds)));
            var fade = lifecycle == null && lifetime > .65f ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((lifetime-age)/.35f)) : 1;
            var breathing = 1 + Mathf.Sin(age * (restorative ? 2.4f : 4.1f)) * .045f;
            if (lifecycle != null)
            {
                var live = lifecycle.active;
                var wave = Mathf.Sin(Mathf.Max(0,age - chargeSeconds) * Mathf.PI * 2 /
                    Mathf.Clamp((live?.period_ms ?? 1000) / 1000f,.1f,6f));
                var amount = Mathf.Clamp((live?.amplitude_milli ?? 0) / 1000f,0,.5f);
                breathing = live?.kind == "breathe" ? 1 - amount * .2f + wave * amount * .2f
                    : live?.kind == "pulse" || live?.kind == "surge" ? 1 + Mathf.Max(0,wave) * amount : 1;
            }
            for (var i=0;i<materials.Count;i++)
                if (materials[i]!=chargeMaterial && materials[i]!=chargeCoronaMaterial)
                    materials[i].SetFloat("_Opacity",opacities[i]*birth*fade*breathing*(armed ? 1.1f : 1));
            if (fade < .99f && !emissionsStopped)
            {
                for (var i = 0; i < particleLayers.Count; i++) particleLayers[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
                emissionsStopped = true;
            }
            if (charge!=null)
            {
                charge.position=chargePosition;
                charge.rotation=Quaternion.Euler(90,age*12,0);
                var t=Mathf.Clamp01(age/(chargeSeconds+.3f));
                chargeMaterial.SetFloat("_Opacity",Mathf.Sin(t*Mathf.PI)*.86f);
                charge.gameObject.SetActive(t<1);
                chargeCorona.position=chargePosition+Vector3.up*.02f;
                chargeCorona.rotation=Quaternion.Euler(90,-age*29,0);
                chargeCorona.localScale=Vector3.one*Mathf.Clamp(span*2.7f,1.8f,4.6f)*Mathf.Lerp(.8f,1.35f,Mathf.SmoothStep(0,1,t));
                chargeCoronaMaterial.SetFloat("_Opacity",Mathf.Sin(t*Mathf.PI)*.74f);
                chargeCorona.gameObject.SetActive(t<1);
            }
            if (groundRoot != null)
            {
                groundRoot.position = behavior == null ? new Vector3(transform.position.x, .07f, transform.position.z) : transform.position;
                groundRoot.rotation = behavior == null ? Quaternion.identity : transform.rotation;
            }
            var visualRadius = pulseRadius > 0 ? Mathf.Max(.1f, pulseRadius) : span;
            var expansion = Mathf.Lerp(.3f, 1, 1 - Mathf.Pow(1 - Mathf.Clamp01(age/.32f), 3));
            if (seal!=null)
            {
                seal.position=behavior == null ? new Vector3(transform.position.x,.07f,transform.position.z) : transform.position;
                if (behavior == null) seal.rotation=Quaternion.Euler(90,age*(restorative ? 12 : 21),0);
                else seal.localRotation=Quaternion.Euler(90,age*(restorative ? 12 : 21),0);
                seal.localScale=Vector3.one*visualRadius*2.35f*expansion;
            }
            if (corona != null)
            {
                corona.localRotation=Quaternion.Euler(90,-age*17,0);
                corona.localScale=Vector3.one*visualRadius*2.85f*expansion;
                atmosphere.localScale=Vector3.one*visualRadius*3.4f*expansion;
                skirt.localScale=new Vector3(visualRadius*1.85f*expansion,Mathf.Clamp(span,.6f,3.2f)*(restorative ? .8f : 1.35f)*birth,visualRadius*1.85f*expansion);
                skirt.localRotation=Quaternion.Euler(0,age*8,0);
            }
            for (var i=0;i<ribbons.Count;i++)
            {
                var axis=carrier=="projectile" ? Vector3.forward : Vector3.up;
                ribbons[i].localRotation=Quaternion.AngleAxis(age*(groundComposition ? i%2==0 ? 46 : -34 : i%2==0 ? 24 : -21),axis);
                if (groundComposition)
                {
                    var height=Mathf.Clamp(span,.7f,2.6f)*(restorative ? 1.25f : 1);
                    ribbons[i].localScale=new Vector3(visualRadius*expansion,height*birth,visualRadius*expansion);
                    ribbons[i].localPosition=Vector3.up*(.035f+Mathf.Sin(age*2+i*1.7f)*.025f);
                }
                if (behavior != null && (behavior.phenomenon == "vortex" || behavior.phenomenon == "spin" || behavior.phenomenon == "orbit"))
                    ribbons[i].localRotation = Quaternion.AngleAxis(age * SpellBehaviorMotion.AngularSpeed(behavior,physicsProfile) + i * 27,
                        SpellBehaviorMotion.Axis(behavior));
            }
            if (localLight!=null) localLight.intensity=(1.1f+Mathf.Exp(-age*9)*1.4f+Mathf.Sin(age*3)*.1f)*fade;
            UpdateBeamFilaments();
        }

        private static Mesh Quad()
        {
            var mesh = new Mesh { name="Spell energy quad" };
            mesh.vertices=new[] { new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0) };
            mesh.uv=new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one };
            mesh.triangles=new[] {0,2,1,1,2,3};
            mesh.colors=new[] {Color.white,Color.white,Color.white,Color.white};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh Sphere()
        {
            const int rows=12,columns=24;
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            for (var y=0;y<=rows;y++)
                for (var x=0;x<=columns;x++)
                {
                    var u=x/(float)columns; var v=y/(float)rows;
                    var a=u*Mathf.PI*2; var b=v*Mathf.PI;
                    vertices.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a))*.5f);
                    uvs.Add(new Vector2(u,v));
                    if (x<columns && y<rows) { var i=y*(columns+1)+x; indices.AddRange(new[] {i,i+columns+1,i+1,i+1,i+columns+1,i+columns+2}); }
                }
            return Finish("Spell turbulent membrane",vertices,uvs,indices);
        }

        private static Mesh Cylinder()
        {
            const int segments=64;
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            for (var i=0;i<=segments;i++)
            {
                var t=i/(float)segments; var a=t*Mathf.PI*2;
                vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));
                vertices.Add(new Vector3(Mathf.Cos(a)*.7f,1,Mathf.Sin(a)*.7f));
                uvs.Add(new Vector2(t,0)); uvs.Add(new Vector2(t,1));
                if (i<segments) { var n=i*2; indices.AddRange(new[] {n,n+1,n+2,n+2,n+1,n+3}); }
            }
            return Finish("Spell energy curtain",vertices,uvs,indices);
        }

        private static Mesh SweptSpiral(int seed, bool ascending, string pattern)
        {
            const int segments = 80, across = 3;
            var vertices = new List<Vector3>((segments + 1) * (across + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>(segments * across * 6);
            var phase = seed * Mathf.PI * 1.08f;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var a = phase + t * Mathf.PI * (ascending ? 2.45f : 1.78f);
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var radius = ascending ? Mathf.Lerp(.97f, .38f, t) : .78f + Mathf.Sin(t * Mathf.PI) * .23f;
                var y = ascending ? .10f + t * (seed % 2 == 0 ? 2.25f : 1.9f) : .09f + Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), 1.4f) * (seed % 2 == 0 ? 1.38f : .72f);
                if (pattern == "petal") radius *= 1 + Mathf.Sin(a * 3) * .13f;
                if (pattern == "storm" || pattern == "fracture")
                {
                    var section = t * 9;
                    var lo = Mathf.Floor(section);
                    radius += Mathf.Lerp(Mathf.Sin(lo * 12.3f + seed), Mathf.Sin((lo + 1) * 12.3f + seed), section - lo) * .09f;
                }
                var center = radial * radius + Vector3.up * y;
                var taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .65f);
                var halfWidth = (ascending ? .15f : .21f) * taper;
                var side = (Vector3.up * .94f + radial * .34f).normalized;
                for (var j = 0; j <= across; j++)
                {
                    var v = j / (float)across;
                    vertices.Add(center + side * ((v * 2 - 1) * halfWidth) + radial * (Mathf.Sin(v * Mathf.PI) * .038f * taper));
                    uvs.Add(new Vector2(t, v));
                    if (i < segments && j < across)
                    {
                        var n = i * (across + 1) + j;
                        indices.Add(n); indices.Add(n + 1); indices.Add(n + across + 1);
                        indices.Add(n + 1); indices.Add(n + across + 2); indices.Add(n + across + 1);
                    }
                }
            }
            return Finish(ascending ? "Swept rising spiral ribbon" : "Swept asymmetric power arc", vertices, uvs, indices);
        }

        private static Mesh SubjectWake(int seed, string pattern)
        {
            const int segments = 52;
            var vertices = new List<Vector3>((segments + 1) * 2);
            var uvs = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>(segments * 6);
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var a = seed * Mathf.PI * .84f + t * Mathf.PI * 1.15f;
                var radius = .30f + Mathf.Sin(t * Mathf.PI) * .25f;
                var side = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                var center = side * radius + Vector3.forward * (.35f - t * 2.25f);
                if (pattern == "storm") center += side * Mathf.Sin(t * 42 + seed) * .045f;
                var width = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .7f) * (pattern == "storm" ? .055f : .115f);
                var cross = new Vector3(-side.y, side.x, .25f).normalized;
                vertices.Add(center - cross * width); vertices.Add(center + cross * width);
                uvs.Add(new Vector2(t, 0)); uvs.Add(new Vector2(t, 1));
                if (i < segments)
                {
                    var n = i * 2;
                    indices.Add(n); indices.Add(n + 1); indices.Add(n + 2);
                    indices.Add(n + 2); indices.Add(n + 1); indices.Add(n + 3);
                }
            }
            return Finish("Tapered interpreted subject wake", vertices, uvs, indices);
        }

        private static Mesh FlaredSkirt(bool flowing)
        {
            const int segments = 128, rows = 4;
            var vertices = new List<Vector3>((segments + 1) * (rows + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var indices = new List<int>(segments * rows * 6);
            for (var i = 0; i <= segments; i++)
            {
                var u = i / (float)segments;
                var angle = u * Mathf.PI * 2;
                var serration = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 11)), 2.5f);
                var top = flowing ? .45f + serration * .24f : .46f + serration * .62f;
                for (var row = 0; row <= rows; row++)
                {
                    var v = row / (float)rows;
                    var radius = .47f + Mathf.Pow(v, 2) * (flowing ? .065f : .14f);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, v * top, Mathf.Sin(angle) * radius));
                    uvs.Add(new Vector2(u, v));
                    if (i < segments && row < rows)
                    {
                        var n = i * (rows + 1) + row;
                        indices.Add(n); indices.Add(n + 1); indices.Add(n + rows + 1);
                        indices.Add(n + 1); indices.Add(n + rows + 2); indices.Add(n + rows + 1);
                    }
                }
            }
            return Finish("Flared serrated light crown", vertices, uvs, indices);
        }

        private static Mesh Ribbon(int seed,string pattern)
        {
            const int segments=48;
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            for (var i=0;i<=segments;i++)
            {
                var t=i/(float)segments; var a=t*Mathf.PI*1.7f+seed*Mathf.PI*.6667f;
                var radius=.55f+.24f*Mathf.Sin(t*Mathf.PI);
                var center=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,t*1.6f-.8f);
                var width=.075f+.11f*Mathf.Sin(t*Mathf.PI);
                if (pattern=="vortex") center=new Vector3(Mathf.Cos(a*1.5f)*radius, t*1.5f-.5f,Mathf.Sin(a*1.5f)*radius);
                else if (pattern=="orbital") center=new Vector3(Mathf.Cos(a)*.9f,Mathf.Sin(a)*.55f,Mathf.Sin(a+seed)*.5f);
                else if (pattern=="petal") center=new Vector3(Mathf.Cos(a)*radius,.18f+Mathf.Sin(t*Mathf.PI)*.8f,Mathf.Sin(a)*radius);
                else if (pattern=="fracture" || pattern=="storm")
                {
                    var jag=Mathf.Sin(i*17.3f+seed)*.095f;
                    center+=new Vector3(jag,-jag,Mathf.Cos(i*23.1f)*.035f);
                    width=pattern=="storm" ? .045f : .07f;
                }
                var side=new Vector3(Mathf.Cos(a),Mathf.Sin(a),.3f).normalized*width;
                vertices.Add(center-side); vertices.Add(center+side);
                uvs.Add(new Vector2(t,0)); uvs.Add(new Vector2(t,1));
                if (i<segments) { var n=i*2; indices.AddRange(new[] {n,n+1,n+2,n+2,n+1,n+3}); }
            }
            return Finish("Authored energy ribbon",vertices,uvs,indices);
        }

        private static Mesh Veil(int seed)
        {
            const int segments=80,widthSegments=12;
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            var side=seed<3 ? -1 : 1; var band=seed%3;
            for (var i=0;i<=segments;i++)
            {
                var t=i/(float)segments;
                var length=band == 0 ? 5.8f : band == 1 ? 4.9f : 6.3f;
                var spread = Mathf.Sin(t * Mathf.PI * .82f);
                var center=new Vector3(side*(.24f+spread*(.48f+band*.18f))+Mathf.Sin(t*9+seed*1.31f)*.22f*t,
                    .20f-band*.17f+Mathf.Sin(t*7+seed*1.23f)*(.18f+band*.10f)*spread
                    + (band == 0 ? .46f : band == 1 ? -.14f : .16f)*spread-t*.54f,
                    -.32f-t*length);
                var width=(band == 2 ? .08f+.11f*Mathf.Sin(t*Mathf.PI)
                    : .19f+.24f*Mathf.Sin(t*Mathf.PI))*(1-Mathf.Pow(t,8)*.98f);
                for (var j=0;j<=widthSegments;j++)
                {
                    var v=j/(float)widthSegments; var offset=(v*2-1)*width;
                    var twist = side * (.25f + band * .38f) + Mathf.Sin(t * 5 + seed) * .55f;
                    var cross = new Vector3(Mathf.Cos(twist), Mathf.Sin(twist), .16f * side);
                    var fold = Mathf.Sin(v*Mathf.PI*4+t*8+seed)*.055f
                        + Mathf.Sin(v*Mathf.PI*9-t*5)*.018f;
                    vertices.Add(center + cross * offset + new Vector3(0, fold, fold*.7f));
                    uvs.Add(new Vector2(t,v));
                    if (i<segments && j<widthSegments) { var n=i*(widthSegments+1)+j; indices.AddRange(new[] {n,n+1,n+widthSegments+1,n+1,n+widthSegments+2,n+widthSegments+1}); }
                }
            }
            return Finish("Tattered spectral veil",vertices,uvs,indices);
        }

        private static Mesh SpectralBurst(int seed)
        {
            const int rows = 28, columns = 6;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            var angle = seed * Mathf.PI * 2 / 7;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), (seed % 3 - 1) * .48f - .12f);
            var side = new Vector3(-direction.y, direction.x, 0);
            for (var row = 0; row <= rows; row++)
            {
                var t = row / (float)rows;
                var center = direction * (.12f + t * (1.2f + seed % 3 * .25f))
                    + side * Mathf.Sin(t * 5 + seed) * t * .25f;
                for (var col = 0; col <= columns; col++)
                {
                    var v = col / (float)columns;
                    vertices.Add(center + side * ((v * 2 - 1) * (.08f + Mathf.Sin(t*Mathf.PI)*.17f))
                        + Vector3.forward * Mathf.Sin(v*7+t*8)*.065f);
                    uv.Add(new Vector2(t,v));
                    if (row < rows && col < columns)
                    {
                        var n=row*(columns+1)+col;
                        indices.AddRange(new[]{n,n+1,n+columns+1,n+1,n+columns+2,n+columns+1});
                    }
                }
            }
            return Finish("Torn cloth impact fragment",vertices,uv,indices);
        }

        private static Mesh BurstMesh(bool shattered)
        {
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            for (var r=0;r<18;r++)
            {
                var a=r*Mathf.PI*2/18;
                var direction=new Vector3(Mathf.Cos(a),.2f+(r%4)*.17f,Mathf.Sin(a)).normalized;
                var side=Vector3.Cross(direction,Vector3.up).normalized;
                var length=1.1f+(r%5)*.17f;
                for (var s=0;s<=8;s++)
                {
                    var t=s/8f;
                    var center=direction*(.13f+t*length)+side*(shattered ? Mathf.Sin(s*6.2f+r)*.07f*t : Mathf.Sin(t*3+r)*.12f*t);
                    var width=Mathf.Sin(t*Mathf.PI)*.036f;
                    var n=vertices.Count;
                    vertices.Add(center-side*width); vertices.Add(center+side*width);
                    uvs.Add(new Vector2(t,0)); uvs.Add(new Vector2(t,1));
                    if (s<8) indices.AddRange(new[] {n,n+1,n+2,n+2,n+1,n+3});
                }
            }
            return Finish("Impact radial filaments",vertices,uvs,indices);
        }

        private static Mesh Finish(string name,List<Vector3> vertices,List<Vector2> uvs,List<int> indices)
        {
            var mesh=new Mesh {name=name};
            mesh.SetVertices(vertices); mesh.SetUVs(0,uvs); mesh.SetTriangles(indices,0);
            var colors=new Color[vertices.Count]; for(var i=0;i<colors.Length;i++)colors[i]=Color.white;
            mesh.colors=colors; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // Spectral cloth vertex displacement remains inside this margin.
            var bounds=mesh.bounds; bounds.Expand(.35f); mesh.bounds=bounds;
            return mesh;
        }

        private void OnDestroy()
        {
            if(ownsLight) { visualLights=Mathf.Max(0,visualLights-1); ownsLight=false; }
            foreach(var item in owned) if(item!=null) Destroy(item);
            owned.Clear(); materials.Clear(); opacities.Clear();
        }
    }
}
