using System;
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

    // A one-tick beam can be born and expire in successive FixedUpdates before
    // the first rendered frame. This component owns graphics only: the runtime
    // has already removed the carrier and cannot apply another effect or hit.
    internal sealed class BeamAfterimage : MonoBehaviour
    {
        private const float MinimumSeconds = .16f;
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

    internal static class CarrierVisual
    {
        public static void KeepOneTickBeamVisible(GameObject visual)
        {
            if (visual != null) visual.AddComponent<BeamAfterimage>();
        }

        public static GameObject Create(CarrierState state, Texture2D mask)
        {
            var node = state.node;
            var root = new GameObject(node.carrier + " " + state.id);
            root.transform.position = state.position;
            root.transform.rotation = state.rotation;
            var tint = ColorFor(node.appearance?.affinity);
            switch (node.carrier)
            {
                case "projectile": Projectile(root, state, tint); break;
                case "beam": Beam(root, tint, node.options.width_cm ?? 4); break;
                case "field": Footprint(root, mask, node.scale_cm, tint, false); break;
                case "pulse": Pulse(root, mask, node.scale_cm, tint); break;
                case "barrier": Barrier(root, state, tint); break;
                case "trap": Footprint(root, mask, node.scale_cm, tint * .65f, true); break;
            }
            return root;
        }

        private static Color ColorFor(string affinity)
        {
            switch (affinity)
            {
                case "fire": return new Color(1f, .33f, .12f, .68f);
                case "water": return new Color(.16f, .68f, 1f, .62f);
                case "stone": return new Color(.68f, .59f, .41f, .82f);
                default: return new Color(.68f, .85f, 1f, .57f);
            }
        }

        private static void Projectile(GameObject root, CarrierState state, Color tint)
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            core.name = "Noyau filament";
            core.transform.SetParent(root.transform, false);
            core.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var radius = Mathf.Max(.06f, (state.node.options.radius_cm ?? 8) / 100f);
            core.transform.localScale = new Vector3(radius * 1.4f, radius * 2.1f, radius * 1.4f);
            var coreCollider = core.GetComponent<Collider>();
            coreCollider.enabled = false;
            UnityEngine.Object.Destroy(coreCollider);
            core.GetComponent<Renderer>().material = SpellLab.MaterialFor(tint, true);
            var aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aura.name = "Halo du noyau";
            aura.transform.SetParent(root.transform, false);
            aura.transform.localScale = Vector3.one * radius * 2.6f;
            var auraCollider = aura.GetComponent<Collider>();
            auraCollider.enabled = false;
            UnityEngine.Object.Destroy(auraCollider);
            aura.GetComponent<Renderer>().material = SpellLab.MaterialFor(new Color(tint.r, tint.g, tint.b, .22f), true);
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = .22f;
            trail.startWidth = radius * 1.6f;
            trail.endWidth = .01f;
            trail.minVertexDistance = .045f;
            trail.numCornerVertices = 4;
            trail.material = SpellLab.MaterialFor(Color.white, true);
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

        private static void Beam(GameObject root, Color tint, int widthCm)
        {
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = Mathf.Max(.02f, widthCm / 100f);
            line.numCapVertices = 6;
            line.material = SpellLab.MaterialFor(Color.white, true);
            line.startColor = tint;
            line.endColor = new Color(tint.r, tint.g, tint.b, .25f);
            var glow = new GameObject("Lueur du faisceau");
            glow.transform.SetParent(root.transform, false);
            var corona = glow.AddComponent<LineRenderer>();
            corona.useWorldSpace = true;
            corona.positionCount = 2;
            corona.startWidth = corona.endWidth = Mathf.Max(.09f, widthCm / 40f);
            corona.numCapVertices = 6;
            corona.material = SpellLab.MaterialFor(Color.white, true);
            corona.startColor = new Color(tint.r, tint.g, tint.b, .18f);
            corona.endColor = new Color(tint.r, tint.g, tint.b, .06f);
        }

        public static void BeamSegments(CarrierState state, System.Collections.Generic.List<Vector3> points)
        {
            if (state.visual == null || points.Count < 2) return;
            var positions = points.ToArray();
            foreach (var line in state.visual.GetComponentsInChildren<LineRenderer>())
            {
                line.positionCount = positions.Length;
                line.SetPositions(positions);
            }
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

        private static void Barrier(GameObject root, CarrierState state, Color tint)
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
                segment.GetComponent<Renderer>().material = SpellLab.MaterialFor(tint, true);
            }
        }

        public static void TrapArmed(CarrierState state)
        {
            var renderer = state.visual?.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = new Color(1f, .82f, .4f, .75f);
        }
    }
}
