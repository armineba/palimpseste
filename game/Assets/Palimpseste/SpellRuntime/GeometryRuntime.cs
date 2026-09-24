using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    internal sealed class GeometryRuntime
    {
        private readonly Dictionary<string, GeometryAsset> assets;
        private readonly Dictionary<string, Texture2D> masks;

        public GeometryRuntime(Dictionary<string, GeometryAsset> assets, Dictionary<string, Texture2D> masks)
        {
            this.assets = assets; this.masks = masks;
        }

        public List<Vector3> Path(SpellNode node)
        {
            var output = new List<Vector3>();
            if (node.blueprint_v2 != null)
            {
                foreach (var point in node.blueprint_v2.motion.path_cm)
                    output.Add(new Vector3(point[0],point[1],point[2]) * .01f);
                return output;
            }
            if (!assets.TryGetValue(node.geometry_id, out var asset) || asset.points == null) return output;
            // A geometry path advances along its X axis. In the laboratory local frame,
            // forward is Z; the source Z coordinate is the sideways curve offset.
            foreach (var p in asset.points)
                output.Add(new Vector3(p.z * node.scale_cm / 2000000f, 0, p.x * node.scale_cm / 2000000f));
            return output;
        }

        public bool Footprint(SpellNode node, Vector3 origin, Quaternion rotation, Vector3 position, float receiverRadius = 0)
        {
            if (node.blueprint_v2 != null)
            {
                // The admitted V2 field/trap contract declares a root-owned box.
                // Its dimensions come from the blueprint, never the ink mask.
                var localV2 = Quaternion.Inverse(rotation) * (position - origin);
                var extent = node.blueprint_v2.structural_core.size_cm;
                return Mathf.Abs(localV2.x) <= extent[0] * .005f + receiverRadius &&
                    Mathf.Abs(localV2.z) <= extent[2] * .005f + receiverRadius;
            }
            if (!assets.TryGetValue(node.geometry_id, out var asset) || asset.mask_file == null || !masks.TryGetValue(asset.mask_file, out var mask)) return false;
            var local = Quaternion.Inverse(rotation) * (position - origin);
            var span = node.scale_cm / 100f;
            if (span <= 0) return false;
            var offsets = receiverRadius > 0 ? new[] { Vector2.zero, new Vector2(receiverRadius, 0), new Vector2(-receiverRadius, 0), new Vector2(0, receiverRadius), new Vector2(0, -receiverRadius) } : new[] { Vector2.zero };
            foreach (var offset in offsets)
            {
                var u = (local.x + offset.x) / span + .5f;
                var v = (local.z + offset.y) / span + .5f;
                if (u < 0 || u >= 1 || v < 0 || v >= 1) continue;
                var x = Mathf.Clamp(Mathf.FloorToInt(u * mask.width), 0, mask.width - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(v * mask.height), 0, mask.height - 1);
                if (mask.GetPixel(x, y).a >= 16f / 255f) return true;
            }
            return false;
        }

        public Texture2D Mask(SpellNode node)
        {
            if (node.blueprint_v2 != null) return null;
            if (!assets.TryGetValue(node.geometry_id, out var asset) || asset.mask_file == null) return null;
            return masks.TryGetValue(asset.mask_file, out var mask) ? mask : null;
        }

        public Texture2D SignatureMask(SpellNode node)
        {
            if (node.blueprint_v2 != null) return null;
            var id = node.appearance?.signature_geometry_id;
            if (string.IsNullOrEmpty(id) || !assets.TryGetValue(id, out var asset) ||
                asset.kind != "silhouette" || asset.mask_file == null) return null;
            return masks.TryGetValue(asset.mask_file, out var mask) ? mask : null;
        }
    }
}
