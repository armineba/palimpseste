using System;
using System.Collections.Generic;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    internal sealed class PulseVisual : MonoBehaviour
    {
        public Texture2D source;
        public Texture2D live;
        public float scale;
        public Color tint;
    }

    internal sealed class BeamPatternVisual : MonoBehaviour
    {
        public LineRenderer secondStrand;
        public LineRenderer[] dashes;
    }

    // A one-tick beam can be born and expire in successive FixedUpdates before
    // the first rendered frame. This component owns graphics only: the runtime
    // has already removed the carrier and cannot apply another effect or hit.
    internal sealed class BeamAfterimage : MonoBehaviour
    {
        // A one-tick beam is a valid instantaneous mechanic, but its trace
        // needs enough screen time for a player to perceive the drawing.
        private const float MinimumSeconds = .6f;
        private float retireAt;
        private int lateUpdates;

        private void OnEnable() { retireAt = Time.realtimeSinceStartup + MinimumSeconds; }

        private void LateUpdate()
        {
            lateUpdates++;
            // The first LateUpdate precedes at least one render opportunity.
            if (lateUpdates >= 2 && Time.realtimeSinceStartup >= retireAt)
                Destroy(gameObject);
        }
    }

    internal sealed class BeamVisualResources : MonoBehaviour
    {
        private readonly List<Material> owned = new List<Material>();

        public Material Own(Material material)
        {
            owned.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var material in owned)
                if (material != null) Destroy(material);
            owned.Clear();
        }
    }

    // Decorative impact only. The fixed runtime has already applied any hit;
    // this object has no collider, receiver or gameplay callback.
    internal sealed class ProjectileImpactAfterimage : MonoBehaviour
    {
        public LineRenderer ring;
        public Color color;
        public bool impulse;
        private float born;

        private void OnEnable() { born = Time.realtimeSinceStartup; }

        private void Update()
        {
            var duration = impulse ? .48f : .28f;
            var age = (Time.realtimeSinceStartup - born) / duration;
            if (age >= 1f) { Destroy(gameObject); return; }
            transform.localScale = Vector3.one * Mathf.Lerp(impulse ? .22f : .12f,
                impulse ? 1.45f : .65f, age);
            var faded = color;
            faded.a *= 1f - age;
            ring.startColor = ring.endColor = faded;
        }
    }

    internal static class CarrierVisual
    {
        public static void KeepOneTickBeamVisible(GameObject visual)
        {
            if (visual != null) visual.AddComponent<BeamAfterimage>();
        }

        public static GameObject Create(CarrierState state, Texture2D mask, Texture2D signatureMask)
        {
            var node = state.node;
            var root = new GameObject(node.carrier + " " + state.id);
            root.transform.position = state.position;
            root.transform.rotation = state.rotation;
            var tint = ColorFor(node.appearance?.palette, node.appearance?.affinity);
            if (ImageConstructedSpellVisual.Supports(node))
            {
                ImageConstructedSpellVisual.ValidateConstruction(node.appearance.construction);
                if (node.carrier == "barrier") Barrier(root, state, tint, false);
                root.AddComponent<ImageConstructedSpellVisual>().Initialize(node);
                root.AddComponent<SpellVfxComposition>().Initialize(node, tint, Vector3.zero,
                    Mathf.Clamp(node.scale_cm / 100f * .25f, .4f, 2f));
                return root;
            }
            if (SemanticSpellVisual.Supports(node.appearance?.form))
            {
                // Canonical paths and footprints still govern collisions. The
                // rendered subject is a controlled 3D form, never the ink mask.
                if (node.carrier == "barrier") Barrier(root, state, tint, false);
                root.AddComponent<SemanticSpellVisual>().Initialize(node, tint);
                return root;
            }
            switch (node.carrier)
            {
                case "projectile": Projectile(root, state, tint, signatureMask); break;
                case "beam": Beam(root, tint, node.options.width_cm ?? 4,
                    node.appearance?.palette == "lava" ||
                    (string.IsNullOrEmpty(node.appearance?.palette) && node.appearance?.affinity == "fire"),
                    node.appearance?.pattern); break;
                case "field": Footprint(root, mask, node.scale_cm, tint, false); break;
                case "pulse": Pulse(root, mask, node.scale_cm, tint); break;
                case "barrier": Barrier(root, state, tint); break;
                case "trap": Footprint(root, mask, node.scale_cm, tint * .65f, true); break;
            }
            root.AddComponent<SpellVfxComposition>().Initialize(node,tint,Vector3.zero,
                Mathf.Clamp((node.options.radius_cm ?? 25)/50f,.4f,2f));
            return root;
        }

        internal static Color ColorFor(string palette, string affinity)
        {
            // The compiler accepts only this named palette. No model-supplied
            // shader, material path, code or unbounded color value reaches Unity.
            switch (palette)
            {
                case "ember": return new Color(.76f, .20f, .12f, .9f);
                case "lava": return new Color(1f, .30f, .055f, .9f);
                case "ice": return new Color(.63f, .91f, 1f, .78f);
                case "water": return new Color(.17f, .65f, 1f, .7f);
                case "moss": return new Color(.35f, .69f, .28f, .78f);
                case "stone": return new Color(.66f, .53f, .39f, .84f);
                case "storm": return new Color(.66f, .70f, 1f, .8f);
                case "arcane": return new Color(.70f, .36f, .95f, .8f);
                case "shadow": return new Color(.36f, .25f, .55f, .8f);
                case "light": return new Color(1f, .87f, .52f, .85f);
            }
            switch (affinity)
            {
                // Legacy packets carried affinity only. A brick-red ember is
                // the closest visual to a painted red-brown projectile.
                case "fire": return new Color(.76f, .20f, .12f, .9f);
                case "water": return new Color(.16f, .68f, 1f, .62f);
                case "stone": return new Color(.68f, .59f, .41f, .82f);
                default: return new Color(.68f, .85f, 1f, .57f);
            }
        }

        private static void Projectile(GameObject root, CarrierState state, Color tint, Texture2D signatureMask)
        {
            var resources = root.AddComponent<BeamVisualResources>();
            var core = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            core.name = "Pointe du projectile";
            core.transform.SetParent(root.transform, false);
            core.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var radius = Mathf.Max(.06f, (state.node.options.radius_cm ?? 8) / 100f);
            core.transform.localScale = new Vector3(radius * .65f, radius * 1.65f, radius * .65f);
            var coreCollider = core.GetComponent<Collider>();
            coreCollider.enabled = false;
            UnityEngine.Object.Destroy(coreCollider);
            core.GetComponent<Renderer>().sharedMaterial = resources.Own(SpellLab.MaterialFor(tint, true));

            // The sampled source stroke becomes an actual visible contour at
            // the moving tip. The second node therefore has its own bent
            // silhouette, rather than another copy of a generic capsule.
            if (state.path != null && state.path.Count >= 2)
            {
                var last = state.path[state.path.Count - 1];
                var count = Mathf.Min(state.path.Count, 128);
                var positions = new Vector3[count];
                for (var i = 0; i < count; i++)
                {
                    var source = state.path[i * (state.path.Count - 1) / (count - 1)];
                    positions[i] = source - last;
                }
                var pattern = state.node.appearance?.pattern;
                if (pattern == "dotted")
                    DottedProjectileStroke(root, resources, positions, radius, tint);
                else
                {
                    var shadow = ProjectileStroke(root, resources, "Contour du dessin",
                        radius * .95f, new Color(tint.r * .3f, tint.g * .3f, tint.b * .3f, .84f), 0);
                    shadow.positionCount = count;
                    shadow.SetPositions(positions);
                    var stroke = ProjectileStroke(root, resources, "Trait du dessin",
                        radius * .42f, tint, 1);
                    stroke.positionCount = count;
                    stroke.SetPositions(positions);
                    if (pattern == "irregular")
                    {
                        IrregularWidth(shadow);
                        IrregularWidth(stroke);
                    }
                    if (pattern == "double")
                    {
                        var second = ProjectileStroke(root, resources, "Second trait du dessin",
                            radius * .42f, tint, 1);
                        second.positionCount = count;
                        var shifted = new Vector3[count];
                        for (var i = 0; i < count; i++) shifted[i] = positions[i] + Vector3.right * radius * 1.1f;
                        second.SetPositions(shifted);
                    }
                }
            }
            if (signatureMask != null && state.node.activation?.parent_id == null)
            {
                // The whole drawing includes any still-dormant child stroke.
                // Its faint silhouette communicates the motif without spawning
                // a second mechanical projectile before the hit event.
                var glyph = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glyph.name = "Glyphe du dessin complet";
                glyph.transform.SetParent(root.transform, false);
                glyph.transform.localPosition = new Vector3(0, 0, -.6f);
                glyph.transform.localRotation = Quaternion.Euler(0, 180f, 0);
                var size = Mathf.Clamp(state.node.scale_cm / 100f * 1.6f, .8f, 3f);
                glyph.transform.localScale = new Vector3(size, size, 1);
                var collider = glyph.GetComponent<Collider>();
                collider.enabled = false;
                UnityEngine.Object.Destroy(collider);
                var material = resources.Own(SpellLab.MaterialFor(new Color(tint.r, tint.g, tint.b, .42f), true));
                material.mainTexture = signatureMask;
                material.SetTexture("_BaseMap", signatureMask);
                glyph.GetComponent<Renderer>().sharedMaterial = material;
            }
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = state.node.activation?.parent_id == null ? .58f : .40f;
            trail.startWidth = radius * .85f;
            trail.endWidth = .01f;
            trail.minVertexDistance = .025f;
            trail.numCornerVertices = 6;
            trail.sharedMaterial = resources.Own(SpellLab.MaterialFor(Color.white, true));
            trail.startColor = tint;
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0);
            if (state.node.appearance?.pattern == "double")
            {
                var twin = UnityEngine.Object.Instantiate(core, root.transform);
                twin.name = "Second filament";
                core.transform.localPosition = Vector3.left * radius * .55f;
                twin.transform.localPosition = Vector3.right * radius * .55f;
            }
        }

        private static LineRenderer ProjectileStroke(GameObject root, BeamVisualResources resources,
            string name, float width, Color color, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = line.endWidth = Mathf.Max(.018f, width);
            line.numCapVertices = line.numCornerVertices = 6;
            line.sharedMaterial = resources.Own(SpellLab.MaterialFor(Color.white, true));
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            return line;
        }

        private static void DottedProjectileStroke(GameObject root, BeamVisualResources resources,
            Vector3[] positions, float radius, Color tint)
        {
            const int maxDashes = 12;
            for (var i = 0; i < maxDashes; i++)
            {
                var first = i * (positions.Length - 1) / maxDashes;
                var last = Mathf.Min(positions.Length - 1,
                    first + Mathf.Max(1, (positions.Length - 1) / (maxDashes * 2)));
                if (first >= last) continue;
                var dash = ProjectileStroke(root, resources, "Pointillé du dessin " + i,
                    radius * .7f, tint, 1);
                dash.positionCount = 2;
                dash.SetPosition(0, positions[first]);
                dash.SetPosition(1, positions[last]);
            }
        }

        private static void IrregularWidth(LineRenderer line)
        {
            var width = line.startWidth;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, .7f), new Keyframe(.2f, 1.2f),
                new Keyframe(.42f, .55f), new Keyframe(.7f, 1.25f),
                new Keyframe(1f, .75f));
            line.widthMultiplier = width;
        }

        public static GameObject ProjectileHit(CarrierState state, Vector3 point)
        {
            if (state.node.carrier != "projectile") return null;
            if (ImageConstructedSpellVisual.Supports(state.node))
            {
                if (state.node.appearance.lifecycle != null)
                {
                    // A final hit animates the existing carrier in place.
                    // Only a continuing, piercing projectile needs a copy.
                    return state.piercesLeft > 0
                        ? ImageConstructedSpellVisual.SpawnImpact(state.node,point,state.direction)?.gameObject : null;
                }
                ImageConstructedSpellVisual.SpawnImpact(state.node, point, state.direction);
                SpellVfxComposition.SpawnImpact(state.node.appearance, point, state.direction,
                    ColorFor(state.node.appearance.palette, state.node.appearance.affinity),
                    Mathf.Clamp(state.node.scale_cm / 400f, .5f, 2f));
                return null;
            }
            if (SemanticSpellVisual.Supports(state.node.appearance?.form))
            {
                SpellVfxComposition.SpawnImpact(state.node.appearance, point, state.direction,
                    ColorFor(state.node.appearance.palette, state.node.appearance.affinity),
                    (state.node.options.radius_cm ?? 12) / 65f);
                return null;
            }
            var impulse = state.node.effects != null && state.node.effects.Exists(
                effect => effect.@event == "hit" && effect.kind == "impulse");
            var tint = ColorFor(state.node.appearance?.palette, state.node.appearance?.affinity);
            SpellVfxComposition.SpawnImpact(state.node.appearance, point, state.direction, tint,
                (state.node.options.radius_cm ?? 12)/65f);
            var root = new GameObject(impulse ? "Onde de recul" : "Éclat d'impact");
            root.transform.position = point;
            root.transform.rotation = Quaternion.LookRotation(state.direction);
            var resources = root.AddComponent<BeamVisualResources>();
            var ring = ProjectileStroke(root, resources, "Anneau visuel", impulse ? .055f : .035f,
                tint, 4);
            ring.loop = true;
            const int samples = 24;
            ring.positionCount = samples;
            for (var i = 0; i < samples; i++)
            {
                var angle = i * Mathf.PI * 2f / samples;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            var afterimage = root.AddComponent<ProjectileImpactAfterimage>();
            afterimage.ring = ring;
            afterimage.color = tint;
            afterimage.impulse = impulse;
            return root;
        }

        // A bounded visual acknowledgement for every applied mechanic. It is
        // graphics only: no collider, receiver, event callback or spell data.
        public static void EffectCue(string kind, Vector3 point)
        {
            var color = EffectColor(kind);
            var root = new GameObject("Effet " + kind);
            root.transform.position = point + Vector3.up * .35f;
            root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var resources = root.AddComponent<BeamVisualResources>();
            var ring = ProjectileStroke(root, resources, "Signe d'effet", .04f, color, 6);
            ring.loop = true;
            const int samples = 24;
            ring.positionCount = samples;
            for (var i = 0; i < samples; i++)
            {
                var angle = i * Mathf.PI * 2f / samples;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0));
            }
            var afterimage = root.AddComponent<ProjectileImpactAfterimage>();
            afterimage.ring = ring;
            afterimage.color = color;
            afterimage.impulse = kind == "impulse" || kind == "shatter" || kind == "stun";
        }

        private static Color EffectColor(string kind)
        {
            switch (kind)
            {
                case "bleed": case "execute": return new Color(.91f, .08f, .13f, .95f);
                case "poison": return new Color(.32f, .89f, .14f, .9f);
                case "burn": return new Color(1f, .34f, .09f, .95f);
                case "freeze_damage": case "root": return new Color(.46f, .86f, 1f, .9f);
                case "regen": case "heal": case "life_steal": case "cleanse":
                    return new Color(.27f, .98f, .50f, .9f);
                case "barrier_health": case "damage_reduction": return new Color(.3f, .75f, 1f, .95f);
                case "shatter": case "armor_break": return new Color(1f, .64f, .19f, .95f);
                case "stun": return new Color(1f, .91f, .25f, .95f);
                case "dispel": return new Color(.88f, .4f, 1f, .9f);
                case "wet": return new Color(.2f, .7f, 1f, .9f);
                default: return new Color(.84f, .63f, 1f, .9f);
            }
        }

        private static void Beam(GameObject root, Color tint, int widthCm, bool fire, string pattern)
        {
            var resources = root.AddComponent<BeamVisualResources>();
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 6;
            if (fire)
                LavaBeam(root, line, resources, widthCm);
            else
            {
                line.startWidth = line.endWidth = Mathf.Max(.02f, widthCm / 100f);
                line.sharedMaterial = resources.Own(SpellLab.MaterialFor(Color.white, true));
                line.startColor = tint;
                line.endColor = new Color(tint.r, tint.g, tint.b, .25f);
                var glow = new GameObject("Lueur du faisceau");
                glow.transform.SetParent(root.transform, false);
                var corona = glow.AddComponent<LineRenderer>();
                corona.useWorldSpace = true;
                corona.positionCount = 2;
                corona.startWidth = corona.endWidth = Mathf.Max(.09f, widthCm / 40f);
                corona.numCapVertices = 6;
                corona.sharedMaterial = resources.Own(SpellLab.MaterialFor(Color.white, true));
                corona.startColor = new Color(tint.r, tint.g, tint.b, .18f);
                corona.endColor = new Color(tint.r, tint.g, tint.b, .06f);
            }
            BeamPattern(root, resources, line, tint, pattern);
        }

        private static void BeamPattern(GameObject root, BeamVisualResources resources,
            LineRenderer main, Color tint, string pattern)
        {
            if (pattern == "irregular")
            {
                foreach (var part in root.GetComponentsInChildren<LineRenderer>()) IrregularWidth(part);
                return;
            }
            if (pattern != "double" && pattern != "dotted") return;
            var visual = root.AddComponent<BeamPatternVisual>();
            if (pattern == "double")
            {
                visual.secondStrand = BeamLayer(root, resources, "Second brin du faisceau",
                    Mathf.Max(.025f, main.startWidth * .75f), tint, 5);
                return;
            }
            // The subdued continuous trace preserves spatial readability;
            // the bright, fixed-count dash overlay supplies the visual pattern.
            foreach (var part in root.GetComponentsInChildren<LineRenderer>())
            {
                var start = part.startColor;
                var end = part.endColor;
                start.a *= .12f; end.a *= .12f;
                part.startColor = start; part.endColor = end;
            }
            visual.dashes = new LineRenderer[12];
            for (var i = 0; i < visual.dashes.Length; i++)
                visual.dashes[i] = BeamLayer(root, resources, "Pointillé du faisceau " + i,
                    Mathf.Max(.025f, main.startWidth), tint, 5);
        }

        private static void LavaBeam(GameObject root, LineRenderer lava, BeamVisualResources resources, int widthCm)
        {
            // Only the compiled affinity and width select this preauthored visual.
            // Every layer follows the exact points resolved by the fixed beam engine.
            var width = Mathf.Max(.04f, widthCm / 100f);
            var shader = Resources.Load<Shader>("LavaBeam");
            if (shader == null) throw new InvalidOperationException("Shader du faisceau de lave absent du lecteur");

            var halo = BeamLayer(root, resources, "Halo thermique", Mathf.Max(.3f, width * 7f),
                new Color(1f, .19f, .025f, .14f), 0);
            halo.numCapVertices = 8;
            var crust = BeamLayer(root, resources, "Croûte de lave", Mathf.Max(.04f, width),
                new Color(.28f, .075f, .018f, .88f), 1);
            crust.numCapVertices = 8;

            lava.startWidth = lava.endWidth = Mathf.Max(.03f, width * .8f);
            lava.numCapVertices = 8;
            lava.textureMode = LineTextureMode.Stretch;
            lava.sortingOrder = 2;
            lava.sharedMaterial = resources.Own(new Material(shader));
            lava.startColor = new Color(1f, .52f, .15f, .98f);
            lava.endColor = new Color(1f, .38f, .1f, .92f);

            var filament = BeamLayer(root, resources, "Filament incandescent", Mathf.Max(.01f, width * .35f),
                new Color(1f, .82f, .44f, .68f), 3);
            filament.numCapVertices = 8;
        }

        private static LineRenderer BeamLayer(GameObject root, BeamVisualResources resources, string name, float width, Color color, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = resources.Own(SpellLab.MaterialFor(Color.white, true));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = order;
            return line;
        }

        public static void BeamSegments(CarrierState state, System.Collections.Generic.List<Vector3> points)
        {
            if (state.visual == null || points.Count < 2) return;
            var construction = state.visual.GetComponent<ImageConstructedSpellVisual>();
            if (construction != null)
            {
                construction.SetBeamPath(points);
                state.visual.GetComponent<SpellVfxComposition>()?.SetBeamPath(points);
                return;
            }
            var semantic = state.visual.GetComponent<SemanticSpellVisual>();
            if (semantic != null)
            {
                semantic.SetBeamPath(points);
                return;
            }
            state.visual.GetComponent<SpellVfxComposition>()?.SetBeamPath(points);
            var positions = points.ToArray();
            var pattern = state.visual.GetComponent<BeamPatternVisual>();
            foreach (var line in state.visual.GetComponentsInChildren<LineRenderer>())
            {
                if (pattern != null && pattern.dashes != null && Array.IndexOf(pattern.dashes, line) >= 0) continue;
                line.positionCount = positions.Length;
                line.SetPositions(positions);
            }
            if (pattern?.secondStrand != null)
            {
                var axis = (positions[positions.Length - 1] - positions[0]).normalized;
                var lateral = Vector3.Cross(Vector3.up, axis).normalized;
                var offset = lateral * Mathf.Max(.05f, pattern.secondStrand.startWidth * 1.4f);
                for (var i = 0; i < positions.Length; i++) positions[i] += offset;
                pattern.secondStrand.SetPositions(positions);
            }
            if (pattern?.dashes != null)
            {
                var total = 0f;
                for (var i = 1; i < positions.Length; i++) total += Vector3.Distance(positions[i - 1], positions[i]);
                for (var i = 0; i < pattern.dashes.Length; i++)
                {
                    var dash = pattern.dashes[i];
                    dash.positionCount = 2;
                    dash.SetPosition(0, PointAlong(positions, total * i / pattern.dashes.Length));
                    dash.SetPosition(1, PointAlong(positions, total * (i + .45f) / pattern.dashes.Length));
                }
            }
        }

        private static Vector3 PointAlong(Vector3[] points, float distance)
        {
            for (var i = 1; i < points.Length; i++)
            {
                var segment = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= segment || i == points.Length - 1)
                    return Vector3.Lerp(points[i - 1], points[i], segment <= .00001f ? 0 : distance / segment);
                distance -= segment;
            }
            return points[points.Length - 1];
        }

        private static GameObject Footprint(GameObject root, Texture2D mask, int scaleCm, Color tint, bool trap)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = trap ? "Sceau du piège" : "Empreinte du champ";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var ground = .055f - root.transform.position.y;
            quad.transform.localPosition = new Vector3(0, ground, 0);
            quad.transform.localScale = Vector3.one * (scaleCm / 100f);
            var quadCollider = quad.GetComponent<Collider>();
            quadCollider.enabled = false;
            UnityEngine.Object.Destroy(quadCollider);
            var material = SpellLab.MaterialFor(tint, true);
            if (mask != null)
            {
                material.mainTexture = mask;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", mask);
            }
            quad.GetComponent<Renderer>().material = material;
            if (!trap && mask != null)
            {
                var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                glow.name = "Liseré de l'empreinte";
                glow.transform.SetParent(root.transform, false);
                glow.transform.localRotation = Quaternion.Euler(90, 0, 0);
                glow.transform.localPosition = new Vector3(0, ground - .018f, 0);
                glow.transform.localScale = Vector3.one * (scaleCm / 100f) * 1.04f;
                var glowCollider = glow.GetComponent<Collider>();
                glowCollider.enabled = false;
                UnityEngine.Object.Destroy(glowCollider);
                var glowMaterial = SpellLab.MaterialFor(new Color(tint.r, tint.g, tint.b, .2f), true);
                glowMaterial.SetTexture("_BaseMap", mask);
                glow.GetComponent<Renderer>().material = glowMaterial;
            }
            return quad;
        }

        private static void Pulse(GameObject root, Texture2D mask, int scaleCm, Color tint)
        {
            var quad = Footprint(root, mask, scaleCm, tint, false);
            quad.name = "Front d'onde dessiné";
            var visual = root.AddComponent<PulseVisual>();
            visual.source = mask;
            visual.scale = scaleCm / 100f;
            visual.tint = tint;
            visual.live = new Texture2D(128, 128, TextureFormat.RGBA32, false, false);
            visual.live.wrapMode = TextureWrapMode.Clamp;
            quad.GetComponent<Renderer>().material.mainTexture = visual.live;
            if (quad.GetComponent<Renderer>().material.HasProperty("_BaseMap")) quad.GetComponent<Renderer>().material.SetTexture("_BaseMap", visual.live);
        }

        public static void PulseRadius(CarrierState state, float outerRadius)
        {
            state.visual?.GetComponent<ImageConstructedSpellVisual>()?.SetPulseRadius(outerRadius);
            var semantic = state.visual?.GetComponent<SemanticSpellVisual>();
            if (semantic != null)
            {
                semantic.SetPulseRadius(outerRadius);
                return;
            }
            state.visual?.GetComponent<SpellVfxComposition>()?.SetPulseRadius(outerRadius);
            var visual = state.visual?.GetComponent<PulseVisual>();
            if (visual == null || visual.source == null || visual.live == null) return;
            var pixels = new Color32[128 * 128];
            var inner = Mathf.Max(0, outerRadius - (state.node.options.front_width_cm ?? 10) / 100f);
            for (var y = 0; y < 128; y++)
                for (var x = 0; x < 128; x++)
                {
                    var u = (x + .5f) / 128f;
                    var v = (y + .5f) / 128f;
                    var radius = new Vector2((u - .5f) * visual.scale, (v - .5f) * visual.scale).magnitude;
                    var sourceA = visual.source.GetPixelBilinear(u, v).a;
                    var a = radius >= inner && radius <= outerRadius && sourceA >= 16f / 255f ? (byte)Mathf.RoundToInt(sourceA * 175f) : (byte)0;
                    pixels[y * 128 + x] = new Color32(255, 255, 255, a);
                }
            visual.live.SetPixels32(pixels);
            visual.live.Apply(false, false);
        }

        private static void Barrier(GameObject root, CarrierState state, Color tint, bool showSegments = true)
        {
            var receiver = root.AddComponent<BarrierReceiver>();
            receiver.StructureMilli = state.node.options.structure_milli ?? 100000;
            receiver.BlocksLeft = state.node.options.block_limit ?? 1;
            receiver.InstanceId = state.id;
            var path = state.path;
            if (path == null || path.Count < 2) return;
            var count = Mathf.Min(path.Count - 1, 64);
            for (var i = 0; i < count; i++)
            {
                var a = path[i]; var b = path[i + 1];
                var delta = b - a;
                if (delta.magnitude < .02f) continue;
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "Segment solide " + i;
                segment.transform.SetParent(root.transform, false);
                segment.transform.localPosition = (a + b) * .5f + Vector3.up * (state.node.options.height_cm ?? 100) / 200f;
                segment.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                segment.transform.localScale = new Vector3((state.node.options.thickness_cm ?? 10) / 100f, (state.node.options.height_cm ?? 100) / 100f, delta.magnitude);
                if (showSegments) segment.GetComponent<Renderer>().material = SpellLab.MaterialFor(tint, true);
                else segment.GetComponent<Renderer>().enabled = false;
            }
        }

        public static void TrapArmed(CarrierState state)
        {
            if (state.visual?.GetComponent<ImageConstructedSpellVisual>() is ImageConstructedSpellVisual construction)
            {
                construction.Arm();
                state.visual.GetComponent<SpellVfxComposition>()?.Arm();
                return;
            }
            var semantic = state.visual?.GetComponent<SemanticSpellVisual>();
            if (semantic != null)
            {
                semantic.Arm();
                return;
            }
            state.visual?.GetComponent<SpellVfxComposition>()?.Arm();
            var renderer = state.visual?.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = new Color(1f, .82f, .4f, .75f);
        }
    }
}
