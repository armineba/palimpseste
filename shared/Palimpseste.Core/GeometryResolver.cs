using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Palimpseste.Contracts;

namespace Palimpseste.Core
{
    public sealed class GeometryResolution
    {
        public Dictionary<string, GeometryAsset> Assets { get; } = new Dictionary<string, GeometryAsset>(StringComparer.Ordinal);
        public Dictionary<string, byte[]> GeometryJson { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public Dictionary<string, byte[]> MaskPng { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public List<ValidationIssue> Issues { get; } = new List<ValidationIssue>();
        public bool Success => Issues.Count == 0;
    }

    public static class GeometryResolver
    {
        public const string Version = "sp.geometry.resolver/1.0";
        public const string WholeCanvasVersion = "sp.geometry.resolver/1.1.whole_canvas";
        public const string SemanticVersion = "sp.geometry.resolver/2.0.semantic";
        private static readonly int[] Dx4 = { -1, 1, 0, 0 };
        private static readonly int[] Dy4 = { 0, 0, -1, 1 };

        public static GeometryResolution Resolve(byte[] inkPng, SpellDescription description)
        {
            var image = PngCodec.DecodeRgba(inkPng);
            return ResolveRgba(image.Rgba, image.Width, image.Height, description);
        }

        public static GeometryResolution ResolveRgba(byte[] inkRgba, int width, int height, SpellDescription description)
        {
            if (width != 1024 || height != 1024 || inkRgba == null || inkRgba.Length != width * height * 4)
                throw new ArgumentException("Expected canonical 1024² RGBA ink image");
            if (description == null) throw new ArgumentNullException(nameof(description));
            var result = new GeometryResolution();
            var pixelHash = SpellCompiler.Sha256(inkRgba);
            if (UsesSemanticForms(description))
            {
                if (!CanvasPixels(inkRgba, width, height).Any(pixel => pixel))
                {
                    result.Issues.Add(new ValidationIssue("empty_drawing", "$", "A semantic spell still requires a drawing"));
                    return result;
                }
                return ResolveSemantic(description, pixelHash);
            }
            // A can now interpret the complete image without assigning marks to fixed
            // parchment regions. Offer the same bounded pixel geometries to B for any
            // subject it creates. Existing descriptions keep their explicit requests.
            var imageDriven = description.shape_requests == null || description.shape_requests.Count == 0;
            var requests = imageDriven
                ? new List<(string region, string role)> {
                    ("full", "path"), ("full", "footprint"),
                    ("full", "silhouette"), ("full", "distribution") }
                : description.shape_requests.Select(r => (r.region, r.role)).Distinct().ToList();
            if (!requests.Contains(("full", "silhouette"))) requests.Add(("full", "silhouette"));
            bool[] fullImageMask = null;
            foreach (var request in requests)
            {
                if (!new[] { "core", "ring", "outer", "full" }.Contains(request.region) ||
                    !new[] { "path", "footprint", "silhouette", "distribution" }.Contains(request.role))
                {
                    result.Issues.Add(new ValidationIssue("shape_request", request.region + "/" + request.role, "Unknown region or role"));
                    continue;
                }
                var regionMask = imageDriven
                    ? fullImageMask ?? (fullImageMask = CanvasPixels(inkRgba, width, height))
                    : RegionPixels(inkRgba, width, height, request.region);
                if (!regionMask.Any(x => x))
                {
                    result.Issues.Add(new ValidationIssue("empty_region", request.region + "/" + request.role,
                        "No drawn pixels for requested geometry"));
                    continue;
                }
                if (request.role == "path")
                {
                    var thin = (bool[])regionMask.Clone();
                    Thin(thin, width, height);
                    var components = Components(thin, width, height).OrderByDescending(c => c.Count).Take(8).ToList();
                    int index = 0;
                    foreach (var component in components)
                    {
                        var route = LongestRoute(component, thin, width, height);
                        if (route.Count < 2) continue;
                        var simplified = Resample(route, width, 128);
                        var asset = MakeAsset(request.region, request.role, index++, pixelHash,
                            ToNormalizedPoints(simplified, width), null, Bounds(component, width),
                            "Chemin aminci puis échantillonné par longueur d’arc; composante conservée.", imageDriven);
                        AddAsset(result, asset);
                    }
                    // A dot or a group of dots can still become a field, trap, or pulse.
                    // B only receives assets that actually exist, so it cannot select
                    // a fabricated path for a carrier that requires one.
                    if (index == 0 && !imageDriven) result.Issues.Add(new ValidationIssue("path_degenerate", request.region,
                        "The drawing has no usable path with two distinct points"));
                }
                else if (request.role == "distribution")
                {
                    var components = Components(regionMask, width, height).OrderByDescending(c => c.Count).Take(128).ToList();
                    var centers = components.Select(c => (int)c.Average(i => i % width) +
                        width * (int)c.Average(i => i / width)).ToList();
                    var asset = MakeAsset(request.region, request.role, 0, pixelHash,
                        ToNormalizedPoints(centers, width), null, Bounds(regionMask, width),
                        "Centres des composantes de pixels; aucune interprétation tactique.", imageDriven);
                    AddAsset(result, asset);
                }
                else
                {
                    var mask = request.role == "footprint" ? FillClosed(regionMask, width, height, request.region, imageDriven) : regionMask;
                    var bounds = Bounds(mask, width);
                    var file = request.region + "." + request.role + ".0.png";
                    var crop = CropMask(mask, width, bounds);
                    result.MaskPng[file] = PngCodec.EncodeRgba(crop, bounds.width, bounds.height);
                    var asset = MakeAsset(request.region, request.role, 0, pixelHash,
                        new List<GeometryPoint>(), file, bounds,
                        request.role == "footprint" ?
                            "Empreinte issue des pixels; les zones closes alternées restent trouées." :
                            "Silhouette des pixels visibles, sans guides de référence.", imageDriven);
                    AddAsset(result, asset);
                }
            }
            return result;
        }

        public static bool UsesSemanticForms(SpellDescription description) =>
            description?.clauses?.Any(clause => clause.facts?.Any(fact => fact.dimension == "visual_form") == true) == true;

        // This digest describes normalized contract data, independent of whitespace in
        // A's frozen JSON. The plan still binds separately to those exact source bytes.
        public static string SemanticDescriptionSha256(SpellDescription description) =>
            SpellCompiler.Sha256(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(description, Formatting.None)));

        public static string SemanticGeometryId(string subject, string kind) =>
            "semantic." + SpellCompiler.Sha256(Encoding.UTF8.GetBytes(subject)).Substring(0, 16) + "." + kind;

        internal static GeometryResolution ResolveSemantic(SpellDescription description, string pixelHash)
        {
            var result = new GeometryResolution();
            var descriptionHash = SemanticDescriptionSha256(description);
            foreach (var subject in description.clauses.GroupBy(clause => clause.subject_id, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                var allFacts = subject.SelectMany(clause => clause.facts).ToArray();
                var forms = allFacts.Where(fact => fact.dimension == "visual_form").ToArray();
                var mechanics = subject.Where(clause => clause.kind == "mechanical").SelectMany(clause => clause.facts).ToArray();
                var carriers = mechanics.Where(fact => fact.dimension == "carrier").ToArray();
                if (forms.Length != 1 || !SpellVisualForms.All.Contains(forms[0].value) || carriers.Length != 1)
                {
                    result.Issues.Add(new ValidationIssue("semantic_subject", subject.Key,
                        "Semantic geometry requires one controlled visual form and one carrier per subject"));
                    continue;
                }
                var carrier = carriers[0].value;
                bool path = carrier == "projectile" || carrier == "beam" || carrier == "barrier";
                string kind = path ? "path" : "footprint";
                var id = SemanticGeometryId(subject.Key, kind);
                var points = new List<GeometryPoint>();
                string maskFile = null;
                if (path)
                {
                    bool curve = carrier == "projectile" && mechanics.Any(fact => fact.dimension == "motion" && fact.value == "curve");
                    // Barrier paths form a transverse wall; all travel paths advance
                    // on X, matching GeometryRuntime's documented local frame.
                    int count = curve ? 25 : 2;
                    for (int i = 0; i < count; i++)
                    {
                        double t = (double)i / (count - 1);
                        points.Add(new GeometryPoint {
                            x = carrier == "barrier" ? 0 : (int)Math.Round(-10000 + 20000 * t),
                            z = carrier == "barrier" ? (int)Math.Round(-10000 + 20000 * t) :
                                curve ? (int)Math.Round(4000 * 4 * t * (1 - t)) : 0
                        });
                    }
                }
                else
                {
                    // Stable circular collision footprint. The artistic form is a
                    // bounded 3D renderer, never this mask or the player's ink contour.
                    const int size = 128;
                    var rgba = new byte[size * size * 4];
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            double distance = Math.Sqrt(Math.Pow(x + .5 - size / 2d, 2) + Math.Pow(y + .5 - size / 2d, 2));
                            int at = (y * size + x) * 4;
                            rgba[at] = rgba[at + 1] = rgba[at + 2] = 255;
                            rgba[at + 3] = (byte)Math.Round(Math.Max(0, Math.Min(1, size / 2d - distance)) * 255);
                        }
                    maskFile = id + ".png";
                    result.MaskPng[maskFile] = PngCodec.EncodeRgba(rgba, size, size);
                }
                AddAsset(result, new GeometryAsset {
                    schema_version = "sp.geometry/1.0", geometry_id = id, source_region = "full", kind = kind,
                    source_pixel_sha256 = pixelHash, source_subject_id = subject.Key,
                    source_description_sha256 = descriptionHash, algorithm = SemanticVersion,
                    points = points, mask_file = maskFile, width_px = path ? 0 : 128, height_px = path ? 0 : 128,
                    notes = "Clean geometry from interpreted carrier and motion. Pixel hash is input lineage only; no ink contour. " +
                        "Visual form: " + forms[0].value + ". Description hash uses normalized SpellDescription JSON."
                });
            }
            return result;
        }

        private static GeometryAsset MakeAsset(string region, string kind, int index, string pixelHash,
            List<GeometryPoint> points, string maskFile, (int x, int y, int width, int height) bounds, string notes,
            bool wholeCanvas)
        {
            return new GeometryAsset { schema_version = "sp.geometry/1.0", geometry_id = region + "." + kind + "." + index,
                source_region = region, kind = kind, source_pixel_sha256 = pixelHash,
                algorithm = wholeCanvas ? WholeCanvasVersion : Version,
                points = points, mask_file = maskFile, width_px = bounds.width, height_px = bounds.height, notes = notes };
        }

        private static void AddAsset(GeometryResolution result, GeometryAsset asset)
        {
            result.Assets[asset.geometry_id] = asset;
            var token = Newtonsoft.Json.Linq.JObject.FromObject(asset);
            if (asset.source_subject_id == null) token.Remove("source_subject_id");
            if (asset.source_description_sha256 == null) token.Remove("source_description_sha256");
            var json = Encoding.UTF8.GetBytes(token.ToString(Formatting.None));
            var violations = ContractJson.Validate(ContractJson.ParseStrict(json), "geometry");
            foreach (var violation in violations) result.Issues.Add(violation);
            result.GeometryJson[asset.geometry_id] = json;
        }

        private static bool[] CanvasPixels(byte[] rgba, int width, int height)
        {
            var pixels = new bool[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = rgba[i * 4 + 3] >= 32;
            return pixels;
        }

        private static bool[] RegionPixels(byte[] rgba, int width, int height, string region)
        {
            var pixels = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                double v = (y + 0.5) / height;
                for (int x = 0; x < width; x++)
                {
                    double u = (x + 0.5) / width;
                    double rho2 = Math.Pow(2 * u - 1, 2) + Math.Pow(2 * v - 1, 2);
                    bool inside = rho2 <= 1 && (region == "full" ||
                        region == "core" && rho2 < .32 * .32 ||
                        region == "ring" && rho2 >= .32 * .32 && rho2 < .68 * .68 ||
                        region == "outer" && rho2 >= .68 * .68);
                    pixels[y * width + x] = inside && rgba[(y * width + x) * 4 + 3] >= 32;
                }
            }
            return pixels;
        }

        private static bool[] FillClosed(bool[] ink, int width, int height, string region, bool wholeCanvas)
        {
            var output = (bool[])ink.Clone();
            var visited = new bool[ink.Length];
            var queue = new int[ink.Length];
            for (int start = 0; start < ink.Length; start++)
            {
                if (ink[start] || visited[start]) continue;
                int head = 0, tail = 0;
                visited[start] = true; queue[tail++] = start;
                bool touchesBoundary = false;
                while (head < tail)
                {
                    int index = queue[head++], x = index % width, y = index / width;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1 ||
                        !InsideRegion(x, y, width, height, region, wholeCanvas)) touchesBoundary = true;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = x + Dx4[k], ny = y + Dy4[k];
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int next = ny * width + nx;
                        if (!ink[next] && !visited[next]) { visited[next] = true; queue[tail++] = next; }
                    }
                }
                if (touchesBoundary) continue;
                int representative = queue[0], rx = representative % width, ry = representative / width;
                int crossings = 0;
                bool inInk = false;
                for (int x = rx - 1; x >= 0; x--)
                {
                    bool current = ink[ry * width + x];
                    if (current && !inInk) crossings++;
                    inInk = current;
                }
                if ((crossings & 1) == 1)
                    for (int k = 0; k < tail; k++) output[queue[k]] = true;
            }
            return output;
        }

        private static bool InsideRegion(int x, int y, int width, int height, string region, bool wholeCanvas)
        {
            if (wholeCanvas) return true;
            double u = (x + .5) / width, v = (y + .5) / height;
            double r = (2 * u - 1) * (2 * u - 1) + (2 * v - 1) * (2 * v - 1);
            return r <= 1 && (region == "full" || region == "core" && r < .32 * .32 ||
                region == "ring" && r >= .32 * .32 && r < .68 * .68 || region == "outer" && r >= .68 * .68);
        }

        private static void Thin(bool[] pixels, int width, int height)
        {
            var remove = new List<int>();
            for (int iteration = 0; iteration < 64; iteration++)
            {
                bool changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    remove.Clear();
                    for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                    {
                        int i = y * width + x;
                        if (!pixels[i]) continue;
                        bool[] n = { pixels[i - width], pixels[i - width + 1], pixels[i + 1],
                            pixels[i + width + 1], pixels[i + width], pixels[i + width - 1],
                            pixels[i - 1], pixels[i - width - 1] };
                        int count = n.Count(value => value);
                        if (count < 2 || count > 6) continue;
                        int transitions = 0;
                        for (int k = 0; k < 8; k++) if (!n[k] && n[(k + 1) & 7]) transitions++;
                        if (transitions != 1) continue;
                        if (pass == 0 ? (n[0] && n[2] && n[4]) || (n[2] && n[4] && n[6]) :
                            (n[0] && n[2] && n[6]) || (n[0] && n[4] && n[6])) continue;
                        remove.Add(i);
                    }
                    foreach (var index in remove) pixels[index] = false;
                    changed |= remove.Count != 0;
                }
                if (!changed) break;
            }
        }

        private static List<List<int>> Components(bool[] pixels, int width, int height)
        {
            var seen = new bool[pixels.Length];
            var output = new List<List<int>>();
            var queue = new Queue<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (!pixels[start] || seen[start]) continue;
                var component = new List<int>();
                queue.Enqueue(start); seen[start] = true;
                while (queue.Count != 0)
                {
                    int current = queue.Dequeue(), x = current % width, y = current / width;
                    component.Add(current);
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        int next = ny * width + nx;
                        if (pixels[next] && !seen[next]) { seen[next] = true; queue.Enqueue(next); }
                    }
                }
                output.Add(component);
            }
            return output;
        }

        private static List<int> LongestRoute(List<int> component, bool[] pixels, int width, int height)
        {
            if (component.Count < 2) return new List<int>();
            var membership = new HashSet<int>(component);
            int endpoint = Farthest(component[0], membership, width, height, out _);
            int opposite = Farthest(endpoint, membership, width, height, out var parents);
            var route = new List<int>();
            for (int current = opposite; current >= 0; current = parents[current])
            {
                route.Add(current);
                if (current == endpoint) break;
            }
            route.Reverse();
            return route;
        }

        private static int Farthest(int start, HashSet<int> membership, int width, int height, out Dictionary<int, int> parents)
        {
            parents = new Dictionary<int, int> { [start] = -1 };
            var queue = new Queue<int>(); queue.Enqueue(start);
            int farthest = start;
            while (queue.Count != 0)
            {
                int current = queue.Dequeue(), x = current % width, y = current / width;
                farthest = current;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (membership.Contains(next) && !parents.ContainsKey(next))
                    { parents[next] = current; queue.Enqueue(next); }
                }
            }
            return farthest;
        }

        private static List<int> Resample(List<int> route, int width, int maximum)
        {
            if (route.Count <= maximum) return route;
            var cumulative = new double[route.Count];
            for (int i = 1; i < route.Count; i++)
            {
                int dx = route[i] % width - route[i - 1] % width;
                int dy = route[i] / width - route[i - 1] / width;
                cumulative[i] = cumulative[i - 1] + Math.Sqrt(dx * dx + dy * dy);
            }
            var output = new List<int>();
            int cursor = 0;
            for (int i = 0; i < maximum; i++)
            {
                double target = cumulative[cumulative.Length - 1] * i / (maximum - 1);
                while (cursor + 1 < cumulative.Length && cumulative[cursor + 1] < target) cursor++;
                int chosen = cursor + 1 < cumulative.Length &&
                    Math.Abs(cumulative[cursor + 1] - target) < Math.Abs(cumulative[cursor] - target) ? cursor + 1 : cursor;
                if (output.Count == 0 || output[output.Count - 1] != route[chosen]) output.Add(route[chosen]);
            }
            if (output.Last() != route.Last()) output.Add(route.Last());
            return output.Take(maximum).ToList();
        }

        private static List<GeometryPoint> ToNormalizedPoints(List<int> pixels, int width)
        {
            var box = Bounds(pixels, width);
            int side = Math.Max(box.width, box.height);
            double cx = box.x + (box.width - 1) / 2.0, cy = box.y + (box.height - 1) / 2.0;
            return pixels.Select(i => new GeometryPoint {
                x = (int)Math.Round((i % width - cx) * 20000 / Math.Max(1, side - 1)),
                z = (int)Math.Round((cy - i / width) * 20000 / Math.Max(1, side - 1))
            }).ToList();
        }

        private static (int x, int y, int width, int height) Bounds(bool[] pixels, int width)
        {
            var indices = Enumerable.Range(0, pixels.Length).Where(i => pixels[i]);
            return Bounds(indices.ToList(), width);
        }

        private static (int x, int y, int width, int height) Bounds(List<int> pixels, int width)
        {
            if (pixels.Count == 0) return (0, 0, 0, 0);
            int left = pixels.Min(i => i % width), right = pixels.Max(i => i % width);
            int top = pixels.Min(i => i / width), bottom = pixels.Max(i => i / width);
            return (left, top, right - left + 1, bottom - top + 1);
        }

        private static byte[] CropMask(bool[] pixels, int width, (int x, int y, int width, int height) bounds)
        {
            var rgba = new byte[bounds.width * bounds.height * 4];
            for (int y = 0; y < bounds.height; y++) for (int x = 0; x < bounds.width; x++)
            {
                int dest = (y * bounds.width + x) * 4;
                bool set = pixels[(bounds.y + y) * width + bounds.x + x];
                rgba[dest] = rgba[dest + 1] = rgba[dest + 2] = 255;
                rgba[dest + 3] = set ? (byte)255 : (byte)0;
            }
            return rgba;
        }
    }
}
