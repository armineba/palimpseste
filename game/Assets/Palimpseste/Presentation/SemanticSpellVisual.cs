using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    // The model chooses a bounded form identifier. Every mesh, material and
    // animation below is authored game code; no drawing texture is consumed.
    public sealed class SemanticSpellVisual : MonoBehaviour
    {
        public string Form { get; private set; }
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Transform> orbiters = new List<Transform>();
        private readonly List<Transform> wings = new List<Transform>();
        private readonly List<Transform> debris = new List<Transform>();
        private readonly List<Vector3> debrisVelocity = new List<Vector3>();
        private Transform body;
        private Transform pulseRing;
        private Material stone, metal, mineral, dark, energy, bright, particleMaterial, ribbonMaterial;
        private Mesh rockMesh, roundMesh, crystalMesh;
        private Color tint;
        private float size, born;
        private string carrier;
        private LineRenderer[] beamLayers;
        private Vector3[] beamPath;
        private float beamWidth;
        private bool armed;
        private bool ephemeral;
        private SpellVfxComposition composition;

        public static bool Supports(string form)
        {
            switch (form)
            {
                case "boulder": case "meteor": case "crystal": case "blade": case "spear":
                case "hammer": case "orb": case "fireball": case "lightning": case "wave":
                case "vortex": case "chain": case "vine": case "tentacle": case "shield":
                case "skull": case "spirit": case "wolf": case "bird": case "serpent": case "golem": return true;
                default: return false;
            }
        }

        public void Initialize(SpellNode node, Color color)
        {
            if (!Supports(node.appearance?.form)) throw new ArgumentException("Unsupported semantic visual form");
            Form = node.appearance.form;
            carrier = node.carrier;
            tint = color;
            tint.a = 1f;
            born = Time.time;
            MakeMaterials();
            rockMesh = Own(Lathe(new[] { -.5f, -.31f, .04f, .34f, .5f },
                new[] { .11f, .44f, .53f, .37f, .09f }, 9, .23f, .11f, true));
            rockMesh.name = "Semantic faceted stone";
            roundMesh = Own(Lathe(new[] { -.5f, -.433f, -.25f, 0f, .25f, .433f, .5f },
                new[] { .001f, .25f, .433f, .5f, .433f, .25f, .001f }, 16, 0, 0, false));
            roundMesh.name = "Semantic rounded volume";
            crystalMesh = Own(Lathe(new[] { -.5f, -.32f, .22f, .62f },
                new[] { .03f, .27f, .27f, .001f }, 6, 0, 0, true));
            crystalMesh.name = "Semantic hexagonal crystal";
            body = Child("Form " + Form, transform);
            size = carrier == "projectile" ? Mathf.Clamp((node.options.radius_cm ?? 12) * .022f, .22f, 2.6f)
                : carrier == "beam" ? Mathf.Clamp((node.options.width_cm ?? 8) * .025f, .18f, .8f)
                : Mathf.Clamp(node.scale_cm / 100f * .72f, .45f, 4.5f);
            // The spectral robe is a decorative wake. Its visible head remains
            // distinct from the energy envelope, even in archived small spells.
            if (Form == "spirit") size = Mathf.Max(size, 1.3f);
            body.localScale = Vector3.one * size;
            if (carrier == "field" || carrier == "trap" || carrier == "barrier")
                body.localPosition = Vector3.up * (.08f - transform.position.y + size * .52f);
            if (carrier == "barrier")
            {
                var height = (node.options.height_cm ?? 100) / 100f;
                size = Mathf.Max(.1f, Mathf.Min(node.scale_cm / 100f * .6f, height * .82f));
                body.localPosition = Vector3.up * (height * .5f);
                body.localScale = Vector3.one * size;
            }
            BuildForm(body);
            if (carrier == "barrier") BuildBarrierVolume(node);
            if (carrier == "beam") BuildBeam(node.options.width_cm ?? 8);
            if (carrier == "pulse") body.localPosition = Vector3.up * (.09f - transform.position.y);
            composition = gameObject.AddComponent<SpellVfxComposition>();
            composition.Initialize(node, tint, body.localPosition, size);
        }

        private T Own<T>(T item) where T : UnityEngine.Object { owned.Add(item); return item; }

        private void MakeMaterials()
        {
            stone = Surface(Color.Lerp(new Color(.17f, .18f, .20f), tint, .26f), .25f, .04f);
            metal = Surface(Color.Lerp(new Color(.42f, .47f, .54f), tint, .24f), .73f, .5f);
            mineral = Surface(Color.Lerp(new Color(.25f, .33f, .39f), tint, .7f), .82f, .18f, tint * .24f);
            dark = Surface(new Color(.024f, .03f, .045f), .5f, .3f);
            energy = Surface(tint * .55f, .62f, .25f, tint * 1.9f);
            bright = Surface(Color.Lerp(tint, Color.white, .65f), .64f, .2f, Color.Lerp(tint, Color.white, .45f) * 2.5f);
            var shader = Resources.Load<Shader>("SpellEnergy");
            if (shader == null) throw new InvalidOperationException("SpellEnergy shader missing from player");
            particleMaterial = Own(new Material(shader));
            particleMaterial.SetColor("_BaseColor", Color.white);
            particleMaterial.SetFloat("_Radial", 1f);
            ribbonMaterial = Own(new Material(shader));
            ribbonMaterial.SetColor("_BaseColor", Color.white);
            ribbonMaterial.SetFloat("_Radial", 0f);
        }

        private Material Surface(Color color, float smoothness, float metallic, Color emission = default)
        {
            color.a = 1f;
            Material material;
            if (emission.maxColorComponent > 0)
            {
                var template = Resources.Load<Material>("LabEmissive");
                if (template == null) throw new InvalidOperationException("Emissive URP material missing from player");
                material = Own(new Material(template));
                material.color = color;
                material.SetColor("_BaseColor", color);
            }
            else material = Own(SpellLab.MaterialFor(color, false));
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private Transform Part(string name, Transform parent, Mesh mesh, Vector3 position, Vector3 scale,
            Material material, Vector3 angles = default)
        {
            var part = Child(name, parent);
            part.localPosition = position;
            part.localScale = scale;
            part.localRotation = Quaternion.Euler(angles);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return part;
        }

        private Transform Rock(Transform parent, Vector3 position, Vector3 scale, Vector3 angles = default)
            => Part("Faceted volume", parent, rockMesh, position, scale, stone, angles);

        private Transform Round(Transform parent, Vector3 position, Vector3 scale, Material material)
            => Part("Rounded volume", parent, roundMesh, position, scale, material);

        private void BuildForm(Transform parent)
        {
            switch (Form)
            {
                case "boulder": case "meteor":
                    Rock(parent, Vector3.zero, new Vector3(1.15f, .91f, .98f), new Vector3(18, 24, 7));
                    Rock(parent, new Vector3(-.24f, .12f, .1f), new Vector3(.62f, .74f, .67f), new Vector3(30, 60, 20));
                    Rock(parent, new Vector3(.3f, -.16f, -.06f), new Vector3(.56f, .58f, .7f), new Vector3(0, 30, 10));
                    var satellites = Child("Orbiting fragments", parent); orbiters.Add(satellites);
                    for (var i = 0; i < 5; i++)
                    {
                        var angle = i * Mathf.PI * .4f;
                        Rock(satellites, new Vector3(Mathf.Cos(angle) * .63f, Mathf.Sin(angle * 2) * .17f,
                            Mathf.Sin(angle) * .63f), Vector3.one * (.12f + i * .014f), new Vector3(i * 37, i * 24, 0));
                    }
                    // Recessed molten seams sit between solid slabs, never on a drawn decal.
                    for (var i = 0; i < 4; i++)
                        Tube(parent, "Mineral seam", new[] { new Vector3(-.35f + i * .2f, -.28f, .37f),
                            new Vector3(-.29f + i * .2f, -.02f, .46f), new Vector3(-.36f + i * .2f, .28f, .32f) },
                            .017f, .009f, Form == "meteor" ? bright : energy);
                    if (Form == "meteor") Round(parent, new Vector3(0, 0, -.22f), Vector3.one * .82f, energy);
                    break;
                case "crystal":
                    for (var i = 0; i < 7; i++)
                    {
                        var angle = i * Mathf.PI / 3;
                        Part("Crystal prism", parent, crystalMesh,
                            i == 0 ? Vector3.zero : new Vector3(Mathf.Cos(angle) * .26f, -.21f, Mathf.Sin(angle) * .26f),
                            Vector3.one * (i == 0 ? 1f : .58f), mineral,
                            new Vector3(i == 0 ? 0 : Mathf.Sin(angle) * 24, i * 35, i == 0 ? 0 : -Mathf.Cos(angle) * 24));
                    }
                    Rock(parent, new Vector3(0, -.4f, 0), new Vector3(.68f, .27f, .58f));
                    Round(parent, new Vector3(0, -.08f, .255f), new Vector3(.09f, .22f, .065f), bright);
                    // Narrow luminous edges leave broad lit faces available
                    // for shading instead of washing every prism in emission.
                    for (var i = 0; i < 6; i++)
                    {
                        var angle = i * Mathf.PI / 3;
                        var x = Mathf.Cos(angle) * .273f;
                        var z = Mathf.Sin(angle) * .273f;
                        Tube(parent, "Crystal edge", new[] { new Vector3(x, -.3f, z),
                            new Vector3(x, .22f, z), new Vector3(0, .622f, 0) }, .006f, .003f, energy);
                    }
                    break;
                case "blade": case "spear":
                    Weapon(parent, Form == "spear"); break;
                case "hammer":
                    Tube(parent, "Hammer handle", new[] { new Vector3(0, -.64f, 0), new Vector3(0, .27f, 0) }, .065f, .065f, dark);
                    Rock(parent, new Vector3(0, .28f, 0), new Vector3(1.03f, .5f, .48f));
                    Part("Hammer striking face", parent, rockMesh, new Vector3(-.42f, .28f, 0), new Vector3(.22f, .5f, .47f), metal);
                    Part("Hammer striking face", parent, rockMesh, new Vector3(.42f, .28f, 0), new Vector3(.22f, .5f, .47f), metal);
                    Ring(parent, new Vector3(0, .26f, 0), .17f, .033f, bright, 24, Quaternion.Euler(90, 0, 0));
                    break;
                case "orb": case "fireball":
                    Round(parent, Vector3.zero, Vector3.one * .75f, energy);
                    Round(parent, Vector3.zero, Vector3.one * .49f, bright);
                    var gyroscope = Child("Orbiting plasma", parent); orbiters.Add(gyroscope);
                    for (var i = 0; i < 3; i++)
                        Ring(gyroscope, Vector3.zero, .49f + i * .025f, .014f, i == 1 ? bright : energy, 48,
                            Quaternion.Euler(28 + i * 53, i * 48, i * 28));
                    if (Form == "fireball")
                        for (var i = 0; i < 7; i++)
                        {
                            var angle = i * Mathf.PI * 2 / 7;
                            Tube(parent, "Flame tongue", new[] { new Vector3(Mathf.Cos(angle) * .32f, Mathf.Sin(angle) * .32f, .12f),
                                new Vector3(Mathf.Cos(angle) * .25f, Mathf.Sin(angle) * .25f, -.4f),
                                new Vector3(Mathf.Cos(angle + .4f) * .12f, Mathf.Sin(angle + .4f) * .12f, -.82f) }, .08f, .002f, energy);
                        }
                    break;
                case "lightning":
                    Tube(parent, "Lightning core", new[] { new Vector3(0, 0, -.7f), new Vector3(.18f, .04f, -.2f),
                        new Vector3(-.13f, 0, -.08f), new Vector3(.04f, -.03f, .2f), new Vector3(0, 0, .7f) }, .055f, .016f, bright);
                    for (var i = 0; i < 3; i++)
                        Tube(parent, "Electrical fork", new[] { Vector3.zero, new Vector3((i - 1) * .25f, .13f, .15f),
                            new Vector3((i - 1) * .31f, .25f, .4f) }, .021f, .001f, energy);
                    break;
                case "wave":
                    for (var i = 0; i < 3; i++)
                    {
                        var points = new Vector3[25];
                        for (var n = 0; n < points.Length; n++)
                        {
                            var a = (n / 24f * 1.4f - .7f) * Mathf.PI;
                            points[n] = new Vector3(Mathf.Sin(a) * (.43f + i * .05f), .025f + Mathf.Cos(a) * .09f + i * .06f,
                                Mathf.Cos(a) * (.43f + i * .05f));
                        }
                        Tube(parent, "Crest of wave", points, .045f, .025f, i == 2 ? bright : energy);
                    }
                    break;
                case "vortex":
                    var swirl = Child("Vortex rotation", parent); orbiters.Add(swirl);
                    for (var i = 0; i < 3; i++)
                    {
                        var points = new Vector3[36];
                        for (var n = 0; n < points.Length; n++)
                        {
                            var t = n / 35f;
                            var a = t * Mathf.PI * 3 + i * Mathf.PI * 2 / 3;
                            var r = .14f + t * .35f;
                            points[n] = new Vector3(Mathf.Cos(a) * r, t * .9f - .45f, Mathf.Sin(a) * r);
                        }
                        Tube(swirl, "Helical current", points, .047f, .01f, i == 0 ? bright : energy);
                    }
                    break;
                case "chain":
                    for (var i = 0; i < 9; i++)
                        Ring(parent, new Vector3(Mathf.Sin(i * .55f) * .16f, 0, i * .13f - .52f), .105f, .027f,
                            i % 3 == 0 ? energy : metal, 18, Quaternion.Euler(90, 0, i % 2 * 90), new Vector3(.7f, 1, 1.2f));
                    break;
                case "vine": case "tentacle":
                    Tendril(parent, Form == "vine"); break;
                case "shield":
                    Shield(parent); break;
                case "skull":
                    Skull(parent); break;
                case "spirit":
                    Spirit(parent);
                    break;
                case "wolf": Wolf(parent); break;
                case "bird": Bird(parent); break;
                case "serpent": Serpent(parent); break;
                case "golem": Golem(parent); break;
            }
        }

        private void Spirit(Transform parent)
        {
            var cloak = Own(SpellVfxComposition.CreateHoodMaterial(tint));
            const int segments = 32, rings = 12;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (var row = 0; row <= rings; row++)
            {
                var t = row / (float)rings;
                var radius = Mathf.Sin(t * Mathf.PI * .52f) * .33f + .006f;
                for (var col = 0; col <= segments; col++)
                {
                    var a = col / (float)segments * Mathf.PI * 2;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius,
                        .32f + Mathf.Sin(a) * radius * 1.18f, -.34f + t * .76f));
                    uv.Add(new Vector2(t,col / (float)segments));
                    if (row < rings && col < segments)
                    {
                        var n = row * (segments + 1) + col;
                        triangles.AddRange(new[] { n,n+1,n+segments+1,n+1,n+segments+2,n+segments+1 });
                    }
                }
            }
            var hood = Own(Finish(vertices,triangles,false));
            hood.name = "Open spectral hood"; hood.SetUVs(0,uv);
            Part("Translucent spectral hood",parent,hood,Vector3.zero,Vector3.one,cloak);
            Round(parent,new Vector3(0,.30f,.34f),new Vector3(.49f,.58f,.09f),dark);
            Round(parent,new Vector3(0,.30f,.411f),new Vector3(.12f,.17f,.065f),bright);
            Round(parent,new Vector3(0,-.025f,-.14f),new Vector3(.38f,.39f,.63f),cloak);
            var rim = new Vector3[33];
            for (var i=0;i<rim.Length;i++)
            {
                var a=i/(float)(rim.Length-1)*Mathf.PI*2;
                rim[i]=new Vector3(Mathf.Cos(a)*.338f,.32f+Mathf.Sin(a)*.398f,.419f);
            }
            Tube(parent,"Pearlescent hood seam",rim,.009f,.009f,energy);
        }

        private void Weapon(Transform parent, bool spear)
        {
            var mesh = Own(Lathe(new[] { -.5f, -.24f, .23f, .66f }, new[] { .025f, .22f, .14f, .001f }, 4, 0, 0, true));
            Part(spear ? "Spear head" : "Blade", parent, mesh, new Vector3(0, 0, spear ? .43f : .16f),
                new Vector3(spear ? .66f : 1f, spear ? .59f : .92f, .21f), metal, new Vector3(90, 45, 0));
            Tube(parent, "Weapon spine", new[] { new Vector3(0, .022f, -.18f), new Vector3(0, .022f, .74f) }, .013f, .001f, bright);
            Tube(parent, "Grip", new[] { new Vector3(0, 0, spear ? -.8f : -.57f), new Vector3(0, 0, -.18f) }, .052f, .052f, dark);
            if (!spear)
                Tube(parent, "Crossguard", new[] { new Vector3(-.26f, 0, -.19f), new Vector3(0, .02f, -.13f), new Vector3(.26f, 0, -.19f) }, .046f, .046f, metal);
            Round(parent, new Vector3(0, 0, spear ? -.79f : -.57f), Vector3.one * .12f, energy);
        }

        private void BuildBarrierVolume(SpellNode node)
        {
            // Semantic barrier geometry is a canonical transverse segment.
            // Its complete physical extent remains visible around the subject.
            var halfWidth = node.scale_cm / 200f;
            var height = (node.options.height_cm ?? 100) / 100f;
            var halfDepth = (node.options.thickness_cm ?? 10) / 200f;
            var vertices = new List<Vector3>
            {
                new Vector3(-halfWidth, 0, -halfDepth), new Vector3(halfWidth, 0, -halfDepth),
                new Vector3(halfWidth, height, -halfDepth), new Vector3(-halfWidth, height, -halfDepth),
                new Vector3(-halfWidth, 0, halfDepth), new Vector3(halfWidth, 0, halfDepth),
                new Vector3(halfWidth, height, halfDepth), new Vector3(-halfWidth, height, halfDepth)
            };
            var triangles = new List<int> { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2 };
            var surfaceColor = tint; surfaceColor.a = .075f;
            var surface = Own(SpellLab.MaterialFor(surfaceColor, true));
            var volume = Part("Barrier physical extent", transform, Own(Finish(vertices, triangles, true)),
                Vector3.zero, Vector3.one, surface);
            volume.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var borderRadius = Mathf.Clamp(Mathf.Min(height, halfWidth * 2) * .012f, .006f, .022f);
            for (var face = 0; face <= 1; face++)
            {
                var start = face * 4;
                Tube(transform, "Barrier perimeter", new[] { vertices[start], vertices[start + 1],
                    vertices[start + 2], vertices[start + 3], vertices[start] }, borderRadius, borderRadius, energy);
            }
            for (var corner = 0; corner < 4; corner++)
                Tube(transform, "Barrier depth edge", new[] { vertices[corner], vertices[corner + 4] },
                    borderRadius, borderRadius, energy);
        }

        private void Tendril(Transform parent, bool vine)
        {
            var points = new Vector3[18];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / 17f;
                points[i] = new Vector3(Mathf.Sin(t * 5) * .23f, -.5f + t, Mathf.Cos(t * 5) * .16f);
            }
            Tube(parent, vine ? "Living vine" : "Muscular tentacle", points, .15f, .012f, vine ? stone : energy);
            for (var i = 2; i < 16; i += 2)
            {
                var p = points[i];
                if (vine)
                    Part("Leaf", parent, crystalMesh, p + Vector3.right * (i % 4 == 0 ? .13f : -.13f),
                        new Vector3(.52f, .29f, .09f), energy, new Vector3(0, 0, i % 4 == 0 ? -45 : 45));
                else
                    Ring(parent, p + Vector3.forward * .065f, .048f * (1f - i / 22f), .013f, bright, 12, Quaternion.Euler(90, 0, 0));
            }
        }

        private void Shield(Transform parent)
        {
            var outline = new[] { new Vector2(-.38f, .43f), new Vector2(.38f, .43f), new Vector2(.43f, .05f),
                new Vector2(.28f, -.31f), new Vector2(0, -.57f), new Vector2(-.28f, -.31f), new Vector2(-.43f, .05f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var side = 0; side < 2; side++)
            {
                var center = vertices.Count; vertices.Add(new Vector3(0, 0, side == 0 ? .14f : -.075f));
                foreach (var p in outline) vertices.Add(new Vector3(p.x, p.y, side == 0 ? .015f : -.06f));
                for (var i = 0; i < outline.Length; i++)
                {
                    var next = (i + 1) % outline.Length;
                    if (side == 0) { triangles.Add(center); triangles.Add(center + 1 + next); triangles.Add(center + 1 + i); }
                    else { triangles.Add(center); triangles.Add(center + 1 + i); triangles.Add(center + 1 + next); }
                }
            }
            var stride = outline.Length + 1;
            for (var i = 0; i < outline.Length; i++)
            {
                var a = i + 1; var b = (i + 1) % outline.Length + 1;
                triangles.AddRange(new[] { a, b, a + stride, b, b + stride, a + stride });
            }
            Part("Convex shield", parent, Own(Finish(vertices, triangles, true)), Vector3.zero, Vector3.one, metal);
            var rim = new Vector3[outline.Length + 1];
            for (var i = 0; i < rim.Length; i++) rim[i] = new Vector3(outline[i % outline.Length].x, outline[i % outline.Length].y, .022f);
            Tube(parent, "Shield luminous rim", rim, .024f, .024f, energy);
            Part("Shield gemstone", parent, crystalMesh, new Vector3(0, .05f, .16f), new Vector3(.28f, .33f, .18f), bright);
        }

        private void Skull(Transform parent)
        {
            Round(parent, new Vector3(0, .12f, 0), new Vector3(.77f, .76f, .67f), metal);
            Rock(parent, new Vector3(0, -.25f, .08f), new Vector3(.56f, .23f, .45f));
            for (var side = -1; side <= 1; side += 2)
            {
                Round(parent, new Vector3(side * .18f, .1f, .278f), new Vector3(.23f, .23f, .15f), dark);
                Round(parent, new Vector3(side * .18f, .1f, .35f), new Vector3(.075f, .072f, .04f), bright);
            }
            Part("Nasal cavity", parent, crystalMesh, new Vector3(0, -.035f, .31f), new Vector3(.22f, .16f, .11f), dark, new Vector3(0, 0, 180));
            for (var i = 0; i < 6; i++)
                Part("Tooth", parent, crystalMesh, new Vector3((i - 2.5f) * .064f, -.19f, .255f), new Vector3(.09f, .13f, .1f), metal, new Vector3(0, 0, 180));
        }

        private void Eyes(Transform parent, float spread, float y, float z, float radius)
        {
            for (var sign = -1; sign <= 1; sign += 2)
                Round(parent, new Vector3(sign * spread, y, z), new Vector3(radius, radius * .75f, radius * .55f), bright);
        }

        private void Wolf(Transform parent)
        {
            Rock(parent, new Vector3(0, .05f, -.1f), new Vector3(.43f, .46f, .83f));
            Rock(parent, new Vector3(0, .17f, .24f), new Vector3(.39f, .52f, .39f), new Vector3(-25, 0, 0));
            Part("Wolf head", parent, rockMesh, new Vector3(0, .38f, .4f), new Vector3(.37f, .35f, .36f), metal);
            Part("Wolf muzzle", parent, rockMesh, new Vector3(0, .3f, .6f), new Vector3(.22f, .16f, .33f), metal);
            Round(parent, new Vector3(0, .31f, .745f), new Vector3(.15f, .1f, .08f), dark);
            for (var side = -1; side <= 1; side += 2)
            {
                Part("Wolf pointed ear", parent, crystalMesh, new Vector3(side * .13f, .58f, .31f), new Vector3(.27f, .3f, .27f), metal);
                for (var end = -1; end <= 1; end += 2)
                {
                    var hip = new Vector3(side * .16f, -.06f, end * .32f);
                    var knee = new Vector3(side * .19f, -.28f, end * .26f);
                    var foot = new Vector3(side * .18f, -.48f, end * .32f + .055f);
                    Tube(parent, "Wolf bent leg", new[] { hip, knee, foot }, .085f, .044f, metal);
                    Rock(parent, foot + Vector3.forward * .035f, new Vector3(.16f, .1f, .24f));
                }
            }
            Tube(parent, "Wolf tail", new[] { new Vector3(0, .08f, -.48f), new Vector3(.12f, .16f, -.66f),
                new Vector3(.21f, -.05f, -.9f) }, .12f, .025f, metal);
            Eyes(parent, .105f, .42f, .552f, .065f);
        }

        private void Bird(Transform parent)
        {
            Round(parent, new Vector3(0, 0, -.08f), new Vector3(.3f, .32f, .57f), metal);
            Round(parent, new Vector3(0, .13f, .26f), new Vector3(.25f, .24f, .25f), metal);
            Part("Bird beak", parent, crystalMesh, new Vector3(0, .1f, .45f), new Vector3(.2f, .27f, .16f), bright, new Vector3(90, 0, 0));
            for (var side = -1; side <= 1; side += 2)
            {
                var wing = Child("Articulated wing", parent); wings.Add(wing);
                wing.localPosition = new Vector3(side * .1f, .06f, .04f);
                for (var i = 0; i < 6; i++)
                {
                    var tip = new Vector3(side * (.62f + i * .055f), -.04f, .09f - i * .135f);
                    Tube(wing, "Flight feather", new[] { Vector3.zero, tip * .55f + new Vector3(0, .1f, 0), tip }, .075f, .002f, i % 2 == 0 ? metal : energy);
                }
            }
            for (var i = -1; i <= 1; i++)
                Tube(parent, "Tail feather", new[] { new Vector3(0, 0, -.24f), new Vector3(i * .12f, -.04f, -.58f) }, .065f, .004f, energy);
            Eyes(parent, .095f, .17f, .34f, .042f);
        }

        private void Serpent(Transform parent)
        {
            var points = new Vector3[24];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / 23f;
                points[i] = new Vector3(Mathf.Sin(t * 7) * .3f * (1f - t * .4f), -.32f + t * .56f, -.66f + t * 1.12f);
            }
            Tube(parent, "Serpent coils", points, .025f, .14f, metal);
            var head = Child("Serpent head", parent); head.localPosition = points[23];
            Part("Serpent skull", head, rockMesh, new Vector3(0, .04f, .12f), new Vector3(.34f, .25f, .4f), metal);
            Eyes(head, .112f, .102f, .22f, .059f);
            for (var side = -1; side <= 1; side += 2)
                Part("Serpent fang", head, crystalMesh, new Vector3(side * .084f, -.075f, .22f), new Vector3(.09f, .19f, .08f), bright, new Vector3(0, 0, 180));
            for (var i = 5; i < 22; i += 3)
                Part("Dorsal scale", parent, crystalMesh, points[i] + Vector3.up * .06f, new Vector3(.16f, .19f, .15f), energy);
        }

        private void Golem(Transform parent)
        {
            Rock(parent, new Vector3(0, .1f, 0), new Vector3(.68f, .68f, .37f));
            Rock(parent, new Vector3(0, .53f, 0), new Vector3(.33f, .33f, .29f));
            for (var side = -1; side <= 1; side += 2)
            {
                Rock(parent, new Vector3(side * .43f, .27f, 0), new Vector3(.32f, .37f, .35f));
                Rock(parent, new Vector3(side * .51f, -.01f, .03f), new Vector3(.25f, .39f, .29f), new Vector3(0, 0, side * 17));
                Rock(parent, new Vector3(side * .55f, -.28f, .1f), new Vector3(.31f, .28f, .36f));
                Rock(parent, new Vector3(side * .19f, -.39f, 0), new Vector3(.27f, .46f, .31f));
                Rock(parent, new Vector3(side * .19f, -.65f, .12f), new Vector3(.31f, .19f, .41f));
            }
            Part("Golem heart", parent, crystalMesh, new Vector3(0, .11f, .19f), new Vector3(.36f, .37f, .2f), bright);
            Eyes(parent, .074f, .55f, .14f, .067f);
        }

        private void Ring(Transform parent, Vector3 position, float radius, float width, Material material, int segments,
            Quaternion rotation = default, Vector3 scale = default)
        {
            var points = new Vector3[segments + 1];
            if (rotation == default) rotation = Quaternion.identity;
            if (scale == default) scale = Vector3.one;
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                points[i] = position + rotation * Vector3.Scale(new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), scale);
            }
            Tube(parent, "Energy ring", points, width, width, material);
        }

        private void Tube(Transform parent, string name, Vector3[] points, float startRadius, float endRadius, Material material)
        {
            const int sides = 8;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var i = 0; i < points.Length; i++)
            {
                var forward = i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i];
                var rotation = Quaternion.LookRotation(forward.sqrMagnitude < .000001f ? Vector3.forward : forward.normalized);
                var radius = Mathf.Lerp(startRadius, endRadius, i / (float)(points.Length - 1));
                for (var s = 0; s < sides; s++)
                {
                    var a = s * Mathf.PI * 2 / sides;
                    vertices.Add(points[i] + rotation * new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0));
                }
            }
            for (var ring = 0; ring < points.Length - 1; ring++)
                for (var s = 0; s < sides; s++)
                {
                    var a = ring * sides + s; var b = ring * sides + (s + 1) % sides;
                    triangles.AddRange(new[] { a, b, a + sides, b, b + sides, a + sides });
                }
            // Caps keep solids closed when seen head-on.
            var first = vertices.Count; vertices.Add(points[0]);
            var last = vertices.Count; vertices.Add(points[points.Length - 1]);
            for (var s = 0; s < sides; s++)
            {
                triangles.AddRange(new[] { first, (s + 1) % sides, s });
                var offset = (points.Length - 1) * sides;
                triangles.AddRange(new[] { last, offset + s, offset + (s + 1) % sides });
            }
            Part(name, parent, Own(Finish(vertices, triangles, false)), Vector3.zero, Vector3.one, material);
        }

        private static Mesh Lathe(float[] heights, float[] radii, int sides, float twist, float roughness, bool faceted)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (var y = 0; y < heights.Length; y++)
                for (var x = 0; x < sides; x++)
                {
                    var angle = x * Mathf.PI * 2 / sides + y * twist;
                    var perturb = 1f + Mathf.Sin(x * 13.17f + y * 5.7f) * roughness;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radii[y] * perturb, heights[y], Mathf.Sin(angle) * radii[y] * perturb));
                }
            for (var y = 0; y < heights.Length - 1; y++)
                for (var x = 0; x < sides; x++)
                {
                    var a = y * sides + x; var b = y * sides + (x + 1) % sides;
                    triangles.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
                }
            var bottom = vertices.Count; vertices.Add(new Vector3(0, heights[0], 0));
            var top = vertices.Count; vertices.Add(new Vector3(0, heights[heights.Length - 1], 0));
            for (var x = 0; x < sides; x++)
            {
                triangles.AddRange(new[] { bottom, x, (x + 1) % sides });
                var offset = (heights.Length - 1) * sides;
                triangles.AddRange(new[] { top, offset + (x + 1) % sides, offset + x });
            }
            return Finish(vertices, triangles, faceted);
        }

        private static Mesh Finish(List<Vector3> vertices, List<int> triangles, bool faceted)
        {
            if (faceted)
            {
                var flat = new List<Vector3>(triangles.Count);
                for (var i = 0; i < triangles.Count; i++) { flat.Add(vertices[triangles[i]]); triangles[i] = i; }
                vertices = flat;
            }
            var mesh = new Mesh { name = "Authored semantic volume" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        private void Trail(SpellNode node)
        {
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = Form == "meteor" || Form == "fireball" ? .48f : .24f;
            trail.minVertexDistance = .035f;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 4;
            trail.widthCurve = AnimationCurve.EaseInOut(0, size * .62f, 1, .005f);
            trail.sharedMaterial = ribbonMaterial;
            trail.colorGradient = FadeGradient(tint, .72f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private ParticleSystem Particles(string name, Transform parent, bool burst, float span, int limit)
        {
            var child = Child(name, parent);
            var system = child.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = !burst; main.playOnAwake = false;
            main.duration = burst ? .6f : 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, burst ? .6f : .46f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(burst ? span * 1.8f : .03f, burst ? span * 4 : .17f);
            main.startSize = new ParticleSystem.MinMaxCurve(span * .026f, span * .11f);
            main.startColor = Color.Lerp(tint, Color.white, .3f);
            main.maxParticles = limit;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.gravityModifier = burst ? .28f : -.035f;
            var emission = system.emission;
            emission.rateOverTime = burst ? 0 : 18f;
            if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)limit) });
            var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = Mathf.Max(.02f, span * (burst ? .09f : .48f));
            var color = system.colorOverLifetime; color.enabled = true; color.color = FadeGradient(tint, .8f);
            var sizes = system.sizeOverLifetime; sizes.enabled = true;
            sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            system.Play();
            return system;
        }

        private static Gradient FadeGradient(Color color, float alpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, .55f), 0), new GradientColorKey(color, .35f), new GradientColorKey(color * .7f, 1) },
                new[] { new GradientAlphaKey(alpha, 0), new GradientAlphaKey(alpha * .55f, .45f), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        private void BuildBeam(int widthCm)
        {
            beamWidth = Mathf.Clamp(widthCm / 100f, .035f, 1.1f);
            beamLayers = new LineRenderer[3];
            for (var i = 0; i < 3; i++)
            {
                var line = Child(i == 0 ? "Beam atmosphere" : i == 1 ? "Beam plasma" : "Beam hot core", transform).gameObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true; line.positionCount = 0;
                line.sharedMaterial = ribbonMaterial;
                line.startWidth = line.endWidth = beamWidth * (i == 0 ? 4.5f : i == 1 ? 1.6f : .42f);
                var color = i == 2 ? Color.Lerp(tint, Color.white, .75f) : tint;
                color.a = i == 0 ? .16f : i == 1 ? .55f : .92f;
                line.startColor = line.endColor = color;
                line.numCapVertices = 5; line.numCornerVertices = 4;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                beamLayers[i] = line;
            }
        }

        public void SetBeamPath(List<Vector3> points)
        {
            beamPath = points.ToArray();
            if (body != null && beamPath.Length > 1) body.position = beamPath[beamPath.Length - 1];
            AnimateBeam();
            composition?.SetBeamPath(points);
        }

        private void AnimateBeam()
        {
            if (beamPath == null || beamPath.Length < 2 || beamLayers == null) return;
            var positions = new List<Vector3>(64);
            positions.Add(beamPath[0]);
            var electrical = Form == "lightning";
            for (var n = 1; n < beamPath.Length && positions.Count < 64; n++)
            {
                var a = beamPath[n - 1]; var b = beamPath[n];
                var segments = electrical ? Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) * 4f), 1, 24) : 1;
                for (var s = 1; s <= segments && positions.Count < 64; s++)
                {
                    var t = s / (float)segments;
                    var point = Vector3.Lerp(a, b, t);
                    if (electrical && s != segments)
                    {
                        var phase = Mathf.Floor(Time.time * 18f);
                        var amplitude = Mathf.Sin(t * Mathf.PI) * Mathf.Min(.14f, beamWidth * 1.9f);
                        point += new Vector3(Mathf.Sin(s * 34.1f + phase), Mathf.Cos(s * 17.6f + phase), 0) * amplitude;
                    }
                    positions.Add(point);
                }
            }
            foreach (var line in beamLayers) { line.positionCount = positions.Count; line.SetPositions(positions.ToArray()); }
        }

        public void SetPulseRadius(float radius)
        {
            if (pulseRing != null) pulseRing.localScale = Vector3.one * Mathf.Max(.02f, radius * 2);
            if (body != null && (Form == "wave" || Form == "vortex")) body.localScale = Vector3.one * Mathf.Max(.02f, radius * 2);
            composition?.SetPulseRadius(radius);
        }

        public void Arm()
        {
            if (armed) return;
            armed = true;
            if (pulseRing != null) pulseRing.localPosition += Vector3.up * .018f;
            if (energy != null) energy.SetColor("_EmissionColor", tint * 2.8f);
            composition?.Arm();
        }

        public static void Impact(string form, Vector3 point, Vector3 direction, Color color, float radius)
        {
            SpellVfxComposition.SpawnImpact(new SpellAppearance { form = form }, point, direction, color, radius);
        }

        private void Update()
        {
            var age = Time.time - born;
            if (ephemeral)
            {
                if (age >= .7f) { Destroy(gameObject); return; }
                pulseRing.localScale = Vector3.one * size * Mathf.Lerp(.12f, 1.8f, Mathf.Clamp01(age / .45f));
                pulseRing.gameObject.SetActive(age < .33f);
                for (var i = 0; i < debris.Count; i++)
                {
                    debris[i].localPosition = debrisVelocity[i] * age + Vector3.down * (age * age * 2.3f);
                    debris[i].localRotation = Quaternion.Euler(age * (i * 29 + 75), age * (i * 17 + 35), age * 57);
                    if (age > .45f) debris[i].localScale *= Mathf.Max(0f, 1f - Time.deltaTime * 12f);
                }
                return;
            }
            foreach (var orbiter in orbiters) orbiter.Rotate(0, (Form == "vortex" ? 155f : 52f) * Time.deltaTime, 0, Space.Self);
            for (var i = 0; i < wings.Count; i++) wings[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 8f) * 19f * (i == 0 ? 1 : -1));
            if (body != null && carrier == "projectile" && (Form == "boulder" || Form == "meteor"))
                body.Rotate(new Vector3(29, 43, 17) * Time.deltaTime, Space.Self);
            AnimateBeam();
        }

        private void OnDestroy()
        {
            foreach (var item in owned) if (item != null) Destroy(item);
            owned.Clear();
        }
    }
}
