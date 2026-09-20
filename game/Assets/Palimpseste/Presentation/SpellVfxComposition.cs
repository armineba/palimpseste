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
        private const int MaximumVisualLights = 4;
        private static int visualLights;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<float> opacities = new List<float>();
        private readonly List<Transform> ribbons = new List<Transform>();
        private Transform energyRoot, seal, charge, shockwave, secondWave, pillar;
        private Material chargeMaterial;
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
        private float born, span, chargeSeconds, pulseRadius = -1;
        private Vector3 chargePosition;
        private bool ephemeral, armed, ownsLight;

        public string Style => style;
        public string Motif => motif;
        public bool IsImpact => ephemeral;

        public void Initialize(SpellNode node, Color fallback, Vector3 center, float physicalSize)
        {
            Configure(node.appearance, fallback);
            carrier = node.carrier;
            born = Time.time;
            var profile = node.appearance?.vfx;
            span = Mathf.Clamp((profile?.aura_cm ?? 200) / 200f, .4f, 2f);
            span = Mathf.Max(span, Mathf.Min(physicalSize * .72f, 2.4f));
            if (carrier == "field" || carrier == "trap" || carrier == "pulse")
                span = Mathf.Clamp(node.scale_cm / 200f, .45f, 5f);
            chargeSeconds = Mathf.Clamp((profile?.charge_ms ?? 420) / 1000f, .1f, .8f);
            energyRoot = Child("Composed energy", transform);
            energyRoot.localPosition = center;
            quad = Own(Quad());

            var spectral = form == "spirit";
            if (spectral)
            {
                // Wide translucent fabric with actual folds and torn edges,
                // not a sphere enclosing the interpreted character.
                var cloth = ClothMaterial(true);
                for (var i = 0; i < 4; i++)
                {
                    var veil = Surface("Long spectral veil " + i, energyRoot,
                        Own(Veil(i)), cloth);
                    veil.localScale = Vector3.one * Mathf.Clamp(span, .85f, 1.45f);
                }
            }
            else
            {
                var solid = form == "boulder" || form == "meteor" || form == "golem" ||
                    form == "hammer" || form == "blade" || form == "spear" || form == "wolf";
                if (!solid)
                {
                    var shell = Surface("Flowing energy membrane", energyRoot, Own(Sphere()),
                        Material(1, solid ? .18f : .38f, 1.8f));
                    shell.localScale = Vector3.one * span * 1.7f;
                }
                var ribbon = Material(3, .62f, 2.1f);
                var ribbonCount = density == 1 ? 2 : 3;
                for (var i = 0; i < ribbonCount; i++)
                {
                    var band = Surface("Energy ribbon " + i, energyRoot,
                        Own(Ribbon(i, motif)), ribbon);
                    band.localScale = Vector3.one * span;
                    ribbons.Add(band);
                }
            }
            if (carrier == "projectile")
            {
                AddTrail(energyRoot, spectral);
                MakeCharge();
            }
            else if (carrier == "beam") MakeCharge();
            else
            {
                seal = Surface("Inscribed spell seal", transform, quad, Material(0, .68f, 2.0f));
                seal.localScale = Vector3.one * span * 2.25f;
                seal.rotation = Quaternion.Euler(90, 0, 0);
                var curtain = Surface("Rising energy curtain", energyRoot, Own(Cylinder()), Material(5, .43f, 1.9f));
                curtain.localScale = new Vector3(span * 1.8f, span * 1.3f, span * 1.8f);
                curtain.localPosition = -center;
            }
            Sparks("Chromatic motes", energyRoot, false, span, false);
            if (density >= 2) Sparks("Fast luminous flecks", energyRoot, false, span * .65f, true);
            AddLight(energyRoot, span, .9f);
            TickVisuals(0);
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
            materials.Add(result); opacities.Add(opacity);
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
            result.SetColor("_Color", Color.Lerp(hue, accent, .18f));
            result.SetColor("_Accent", accent * 1.6f);
            result.SetFloat("_Opacity", .82f);
            result.SetFloat("_Torn", torn ? 1 : 0);
            result.SetFloat("_Seed", FormSeed());
            materials.Add(result); opacities.Add(.82f);
            return result;
        }

        public static Material CreateHoodMaterial(Color tint)
        {
            var shader = Resources.Load<Shader>("SpellSpectralCloth");
            if (shader == null) throw new InvalidOperationException("SpellSpectralCloth shader missing from player");
            var material = new Material(shader);
            material.SetColor("_Color", Color.Lerp(tint, new Color(.07f,.025f,.13f), .75f));
            material.SetColor("_Accent", Color.Lerp(tint, Color.white, .62f) * 1.7f);
            material.SetFloat("_Opacity", .95f);
            material.SetFloat("_Torn", 0);
            return material;
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
            chargeMaterial = Material(0, .9f, 2.35f);
            charge = Surface("Casting rune seal", transform, quad, chargeMaterial);
            chargePosition = new Vector3(transform.position.x, .065f, transform.position.z);
            charge.rotation = Quaternion.Euler(90, 0, 0);
            charge.localScale = Vector3.one * Mathf.Clamp(span * 2.7f, 1.8f, 4.6f);
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
            trail.sharedMaterial = Material(3, spectral ? .24f : .5f, 1.75f);
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
            visual.shockwave = Surface("Expanding ground shockwave", root.transform, visual.quad, visual.Material(4,.9f,3));
            visual.shockwave.rotation = Quaternion.Euler(90,0,0);
            visual.shockwave.position = new Vector3(point.x,.075f,point.z);
            visual.secondWave = Surface("Contact corona", root.transform, visual.quad, visual.Material(4,.8f,2.8f));
            visual.secondWave.rotation = Quaternion.LookRotation(direction.sqrMagnitude>.001f ? direction : Vector3.forward);
            var filaments = Surface("Burst radial filaments", root.transform, visual.Own(BurstMesh(visual.impact=="shatter")), visual.Material(3,.85f,3.3f));
            filaments.localScale = Vector3.one * visual.span;
            visual.ribbons.Add(filaments);
            if (visual.impact=="pillar" || visual.style=="holy" || visual.style=="fire")
            {
                visual.pillar = Surface("Impact pillar", root.transform, visual.Own(Cylinder()), visual.Material(5,.8f,3.2f));
                visual.pillar.localScale = new Vector3(visual.span*.7f,visual.span*3,visual.span*.7f);
            }
            visual.Sparks("Impact shards", root.transform,true,visual.span,true);
            visual.Sparks("Impact motes", root.transform,true,visual.span,false);
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

        private void Update() { TickVisuals(Time.time-born); }

        private void TickVisuals(float age)
        {
            if (ephemeral)
            {
                const float duration=.78f;
                if (age>=duration) { Destroy(gameObject); return; }
                var t=Mathf.Clamp01(age/duration);
                var envelope=Mathf.Pow(1-t,1.6f);
                for (var i=0;i<materials.Count;i++) materials[i].SetFloat("_Opacity",opacities[i]*envelope);
                if (shockwave!=null) shockwave.localScale=Vector3.one*span*Mathf.Lerp(.35f,5f,1-Mathf.Pow(1-t,3));
                if (secondWave!=null) secondWave.localScale=Vector3.one*span*Mathf.Lerp(.28f,3.1f,Mathf.Sqrt(t));
                foreach (var ribbon in ribbons) ribbon.localScale=Vector3.one*span*Mathf.Lerp(.12f,1.8f,Mathf.Sqrt(t));
                if (pillar!=null) pillar.localScale=new Vector3(span*Mathf.Lerp(.7f,1.3f,t),span*Mathf.Lerp(1.7f,3.8f,t),span*Mathf.Lerp(.7f,1.3f,t));
                if (localLight!=null) localLight.intensity=3*envelope;
                return;
            }
            var birth=Mathf.SmoothStep(.3f,1,Mathf.Clamp01(age/.12f));
            for (var i=0;i<materials.Count;i++)
                if (materials[i]!=chargeMaterial) materials[i].SetFloat("_Opacity",opacities[i]*birth*(armed ? 1.15f : 1));
            if (charge!=null)
            {
                charge.position=chargePosition;
                charge.rotation=Quaternion.Euler(90,age*12,0);
                var t=Mathf.Clamp01(age/(chargeSeconds+.3f));
                chargeMaterial.SetFloat("_Opacity",Mathf.Sin(t*Mathf.PI)*.86f);
                charge.gameObject.SetActive(t<1);
            }
            if (seal!=null)
            {
                seal.position=new Vector3(transform.position.x,.07f,transform.position.z);
                seal.rotation=Quaternion.Euler(90,age*9,0);
                if (pulseRadius>0) seal.localScale=Vector3.one*pulseRadius*2.15f;
            }
            for (var i=0;i<ribbons.Count;i++)
            {
                var axis=carrier=="projectile" ? Vector3.forward : Vector3.up;
                ribbons[i].localRotation=Quaternion.AngleAxis(age*(i%2==0 ? 32 : -26),axis);
            }
            if (localLight!=null) localLight.intensity=.8f+Mathf.Sin(age*5)*.12f;
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
            const int segments=54,widthSegments=5;
            var vertices=new List<Vector3>(); var uvs=new List<Vector2>(); var indices=new List<int>();
            var side=seed<2 ? -1 : 1; var band=seed%2;
            for (var i=0;i<=segments;i++)
            {
                var t=i/(float)segments;
                var length=2.5f+band*.65f;
                var center=new Vector3(side*(.19f+t*.36f)+Mathf.Sin(t*8+seed)*.14f*t,
                    .12f-band*.22f+Mathf.Sin(t*6+seed)*.23f*t-t*.33f,.02f-t*length);
                var width=(.15f+.25f*Mathf.Sin(t*Mathf.PI))*(1-Mathf.Pow(t,5)*.95f);
                for (var j=0;j<=widthSegments;j++)
                {
                    var v=j/(float)widthSegments; var offset=(v*2-1)*width;
                    vertices.Add(center+new Vector3(offset,Mathf.Sin(v*Mathf.PI*3+t*7)*.065f,offset*.3f*side));
                    uvs.Add(new Vector2(t,v));
                    if (i<segments && j<widthSegments) { var n=i*(widthSegments+1)+j; indices.AddRange(new[] {n,n+1,n+widthSegments+1,n+1,n+widthSegments+2,n+widthSegments+1}); }
                }
            }
            return Finish("Tattered spectral veil",vertices,uvs,indices);
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
