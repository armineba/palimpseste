using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Game.SpellRuntime;

namespace Palimpseste.Game.Library
{
    /// <summary>
    /// Client preflight for image-bound construction, before a lab or carrier
    /// allocates any decorative mesh. This adds no dependency on server Core.
    /// Legacy packets still use their existing artifact and version verifier.
    /// </summary>
    public static class ImageSpellPacketValidator
    {
        public const int MaximumPacketBytes = 2 * 1024 * 1024;
        public const int MaximumNodes = 16;

        public static void Validate(string json, string directory)
        {
            try { ValidateCore(json,directory); }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException ||
                exception is OverflowException || exception is InvalidCastException || exception is FormatException ||
                exception is IOException || exception is UnauthorizedAccessException)
            { throw new InvalidDataException("Image spell packet is invalid",exception); }
        }

        private static void ValidateCore(string json, string directory)
        {
            Require(!string.IsNullOrWhiteSpace(json) && json.Length <= MaximumPacketBytes &&
                Encoding.UTF8.GetByteCount(json) <= MaximumPacketBytes,"Spell packet exceeds two MiB");
            JObject packet;
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = 64, DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal })
            {
                packet = JObject.Load(reader,new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                Require(!reader.Read(),"Trailing content in spell packet");
            }
            var plan = packet["plan"] as JObject;
            var nodes = plan?["nodes"] as JArray;
            var reference = packet["visual_reference"];
            var hasReference = reference != null && reference.Type != JTokenType.Null;
            var hasConstruction = nodes != null && nodes.Any(node => node?["appearance"]?["construction"] != null &&
                node["appearance"]["construction"].Type != JTokenType.Null);
            var imageHash = plan?["visual_reference_sha256"];
            var citesImage = imageHash != null && imageHash.Type != JTokenType.Null;
            var minimumClient = packet["versions"]?["min_client"]?.Value<string>();
            var newClient = Version.TryParse(minimumClient,out var minimumVersion) && minimumVersion >= new Version(1,3,0);
            if (!hasReference && !hasConstruction && !citesImage && !newClient) return;

            Require(packet["schema_version"]?.Value<string>() == "sp.compiled/1.0" &&
                plan?["schema_version"]?.Value<string>() == "sp.plan/1.0","Unsupported image spell schema");
            Require(newClient,"Image construction requires client 1.3.0 or later");
            Require(reference is JObject,"Image construction requires reference metadata");
            Require(nodes != null && nodes.Count >= 1 && nodes.Count <= MaximumNodes,"Image spell node count exceeds 16");
            var descriptionHash = Text(packet,"description_sha256");
            Require(Digest(descriptionHash) && Text(plan,"description_sha256") == descriptionHash,"Description provenance mismatch");
            var visual = (JObject)reference;
            var referenceHash = Text(visual,"sha256");
            Require(Digest(referenceHash) && Text(plan,"visual_reference_sha256") == referenceHash &&
                Text(visual,"description_sha256") == descriptionHash,"Image provenance mismatch");
            var artifactId = Text(visual,"artifact_id");
            Require(artifactId != null && artifactId.Length == 33 && artifactId[0] == 'a' &&
                artifactId.Skip(1).All(IsLowerHex),"Invalid image artifact identifier");
            var promptVersion = Text(visual,"prompt_version");
            const string promptPrefix = "sp.prompt.g/";
            Require(promptVersion != null && promptVersion.Length <= 80 && promptVersion.StartsWith(promptPrefix,StringComparison.Ordinal)
                && Version.TryParse(promptVersion.Substring(promptPrefix.Length),out _),"Invalid image prompt provenance");
            var referenceSize = Integer(visual,"size_bytes",33,SpellVisualConstructionLimits.MaximumReferenceBytes);
            var width = Integer(visual,"width_px",512,SpellVisualConstructionLimits.MaximumReferenceDimension);
            var height = Integer(visual,"height_px",512,SpellVisualConstructionLimits.MaximumReferenceDimension);

            // Count the full plan, not just one renderer about to be spawned.
            // Integer token checks precede deserialization to avoid coercing a
            // fractional or string coordinate into an apparently valid int.
            long authoredParts = 0, expandedParts = 0, instances = 0;
            var parentIds = new Dictionary<string,string>(StringComparer.Ordinal);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 64
            });
            foreach (var raw in nodes)
            {
                Require(raw is JObject,"Missing image spell node");
                var node = (JObject)raw;
                var id = Text(node,"node_id");
                Require(!string.IsNullOrEmpty(id) && id.Length <= 64 && !parentIds.ContainsKey(id),"Duplicate or invalid node identifier");
                Require(node["activation"] is JObject,"Missing activation bounds");
                var activation = (JObject)node["activation"];
                var copies = Integer(activation,"copies",1,8);
                var activations = Integer(activation,"max_activations",1,8);
                Integer(activation,"delay_ticks",0,1500);
                var parent = activation["parent_id"];
                Require(parent == null || parent.Type == JTokenType.Null || parent.Type == JTokenType.String,"Invalid parent identifier");
                parentIds.Add(id,parent?.Type == JTokenType.String ? parent.Value<string>() : null);
                var appearance = node["appearance"] as JObject;
                Require(appearance?["construction"] is JObject,"Every image-bound node requires a construction");
                var lifecycle = appearance["lifecycle"];
                if (lifecycle != null && lifecycle.Type != JTokenType.Null)
                {
                    Require(minimumVersion != null && minimumVersion >= new Version(1,4,0),"Lifecycle requires client 1.4.0 or later");
                    ValidateLifecycle(lifecycle);
                }
                else Require(minimumVersion == null || minimumVersion < new Version(1,4,0),"Client 1.4.0 requires a lifecycle per node");
                var constructionJson = (JObject)appearance["construction"];
                Require(constructionJson["parts"] is JArray,"Missing construction parts");
                var parts = (JArray)constructionJson["parts"];
                Require(parts.Count >= 1 && parts.Count <= SpellVisualConstructionLimits.MaximumPartsPerNode,"Construction exceeds 64 parts");
                checked {
                    authoredParts += parts.Count;
                    instances += (long)copies * activations;
                    expandedParts += (long)parts.Count * copies * activations;
                }
                Require(authoredParts <= SpellVisualConstructionLimits.MaximumPartsPerPlan &&
                    expandedParts <= SpellVisualConstructionLimits.MaximumExpandedParts && instances <= 128,"Image construction resource budget exceeded");
                foreach (var rawPart in parts)
                {
                    Require(rawPart is JObject,"Invalid construction part");
                    var part = (JObject)rawPart;
                    IntegerVector(part["position_cm"],-1000,1000); IntegerVector(part["scale_cm"],1,1000);
                    IntegerVector(part["rotation_mdeg"],-360000,360000); IntegerVector(part["color_rgb"],0,255);
                    Integer(part,"opacity_milli",0,1000); Integer(part,"emission_milli",0,6000);
                    Require(part["points_cm"] is JArray,"Missing visual path");
                    var points = (JArray)part["points_cm"];
                    Require(points.Count <= SpellVisualConstructionLimits.MaximumPoints,"Visual path exceeds 16 points");
                    var pathPart = Text(part,"kind") == "ribbon" || Text(part,"kind") == "arc";
                    Require(pathPart ? points.Count >= 2 : points.Count == 0,"Invalid visual path cardinality");
                    foreach (var point in points) IntegerVector(point,-1000,1000);
                    if (pathPart) Require(points.Skip(1).Any(point => !JToken.DeepEquals(point,points[0])),"Degenerate visual path");
                    Require(part["motion"] is JObject,"Missing visual motion bounds");
                    var motion = (JObject)part["motion"];
                    Integer(motion,"amplitude_cm",0,150); Integer(motion,"frequency_mhz",0,6000); Integer(motion,"phase_mdeg",0,360000);
                }
                ImageConstructedSpellVisual.ValidateConstruction(constructionJson.ToObject<SpellVisualConstruction>(serializer));
            }
            foreach (var id in parentIds.Keys)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal); var current = id;
                while (current != null)
                {
                    Require(seen.Add(current) && parentIds.ContainsKey(current),"Cyclic or unresolved image node parent");
                    current = parentIds[current];
                }
            }
            Require(packet["resource_bounds"] is JObject,"Missing compiled resource bounds");
            var bounds = (JObject)packet["resource_bounds"];
            Require(Integer(bounds,"max_instances",1,128) == instances,"Compiled instance budget mismatch");
            Integer(bounds,"max_effect_applications",0,8192); Integer(bounds,"max_end_tick",0,1500);
            Integer(bounds,"geometry_bytes",0,33554432); Integer(bounds,"max_colliders",0,512);

            Require(!string.IsNullOrWhiteSpace(directory),"Image cache directory missing");
            var artifactRoot = Path.GetFullPath(Path.Combine(directory,"artifacts"));
            var path = Path.GetFullPath(Path.Combine(artifactRoot,artifactId));
            Require(path.StartsWith(artifactRoot + Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Image escaped artifact directory");
            var info = new FileInfo(path);
            Require(info.Exists && info.Length == referenceSize && info.Length <= SpellVisualConstructionLimits.MaximumReferenceBytes,
                "Reference image is absent or has the wrong size");
            var bytes = File.ReadAllBytes(path);
            Require(VisualReferenceCache.ValidPng(bytes,referenceHash),"Reference image hash or PNG profile is invalid");
            Require(PngInteger(bytes,16) == width && PngInteger(bytes,20) == height,"Reference image dimensions differ from metadata");
        }

        private static string Text(JObject value, string key) => value?[key]?.Type == JTokenType.String ? value[key].Value<string>() : null;
        private static void ValidateLifecycle(JToken token)
        {
            Require(token is JObject,"Lifecycle must be an object");
            var lifecycle = (JObject)token;
            Require(lifecycle["intro"] is JObject && lifecycle["active"] is JObject &&
                lifecycle["contact"] is JObject && lifecycle["expiration"] is JObject,"Lifecycle requires four phases");
            var intro = (JObject)lifecycle["intro"];
            Require(SpellVisualLifecycleLimits.IntroKinds.Contains(Text(intro,"kind")),"Unknown entrance animation");
            Integer(intro,"duration_ms",100,3000); Integer(intro,"scale_start_milli",0,1000);
            Integer(intro,"opacity_start_milli",0,1000); Integer(intro,"emission_start_milli",0,6000);
            var active = (JObject)lifecycle["active"];
            Require(SpellVisualLifecycleLimits.ActiveKinds.Contains(Text(active,"kind")),"Unknown active animation");
            Integer(active,"period_ms",100,6000); Integer(active,"amplitude_milli",0,500);
            foreach (var name in new[] { "contact","expiration" })
            {
                var end = (JObject)lifecycle[name];
                Require(SpellVisualLifecycleLimits.EndingKinds.Contains(Text(end,"kind")),"Unknown ending animation");
                Integer(end,"duration_ms",100,3000); Integer(end,"spread_cm",0,600); Integer(end,"scale_end_milli",0,3000);
            }
            lifecycle.ToObject<SpellVisualLifecycle>(JsonSerializer.Create(new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None, MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error, MaxDepth = 64
            }));
        }
        private static bool IsLowerHex(char value) => value >= '0' && value <= '9' || value >= 'a' && value <= 'f';
        private static bool Digest(string value) => value != null && value.Length == 64 && value.All(IsLowerHex);
        private static int PngInteger(byte[] bytes, int offset) => bytes[offset] << 24 | bytes[offset+1] << 16 | bytes[offset+2] << 8 | bytes[offset+3];
        private static int Integer(JObject owner, string name, int minimum, int maximum)
        {
            var token = owner?[name];
            Require(token?.Type == JTokenType.Integer,"Missing integer: " + name);
            var value = token.Value<long>();
            Require(value >= minimum && value <= maximum,"Integer exceeds bounds: " + name);
            return (int)value;
        }
        private static void IntegerVector(JToken token, int minimum, int maximum)
        {
            Require(token is JArray && ((JArray)token).Count == 3,"Visual coordinate requires three integers");
            foreach (var coordinate in (JArray)token)
            {
                Require(coordinate.Type == JTokenType.Integer,"Visual coordinate is not an integer");
                var value = coordinate.Value<long>();
                Require(value >= minimum && value <= maximum,"Visual coordinate exceeds bounds");
            }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
