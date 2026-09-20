using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Palimpseste.Game.Library;

namespace Palimpseste.Game.PlayModeTests
{
    // Deliberately invalid authored payloads exercise pre-allocation rejection.
    // These are not provider results, spell recipes or visual evidence.
    public sealed class ImageSpellPacketValidatorTests
    {
        [Test]
        public void RejectsConstructionWithoutItsGeneratedImage()
        {
            var packet = Packet(1,1,1,1);
            packet.Remove("visual_reference");
            var error = Assert.Throws<InvalidDataException>(() => ImageSpellPacketValidator.Validate(packet.ToString(Formatting.None),"unused"));
            StringAssert.Contains("requires reference metadata",error.Message);
        }

        [TestCase(3,43,1,1)]
        [TestCase(1,17,8,8)]
        public void RejectsAggregateBudgetBeforeReadingImageOrCreatingMeshes(int nodes, int parts, int copies, int activations)
        {
            var packet = Packet(nodes,parts,copies,activations);
            var error = Assert.Throws<InvalidDataException>(() => ImageSpellPacketValidator.Validate(packet.ToString(Formatting.None),"unused"));
            StringAssert.Contains("resource budget exceeded",error.Message);
        }

        [Test]
        public void DoesNotCoerceFractionalCoordinatesToIntegers()
        {
            var packet = Packet(1,1,1,1);
            packet["plan"]["nodes"][0]["appearance"]["construction"]["parts"][0]["position_cm"][0] = .5;
            var error = Assert.Throws<InvalidDataException>(() => ImageSpellPacketValidator.Validate(packet.ToString(Formatting.None),"unused"));
            StringAssert.Contains("not an integer",error.Message);
        }

        private static JObject Packet(int nodeCount, int partCount, int copies, int activations)
        {
            var digest = new string('a',64);
            var nodes = new JArray();
            for (var n = 0; n < nodeCount; n++)
            {
                var parts = new JArray();
                for (var i = 0; i < partCount; i++) parts.Add(new JObject {
                    ["kind"] = "ellipsoid", ["material"] = "energy",
                    ["position_cm"] = new JArray(0,0,0), ["scale_cm"] = new JArray(20,20,20),
                    ["rotation_mdeg"] = new JArray(0,0,0), ["color_rgb"] = new JArray(40,120,255),
                    ["opacity_milli"] = 500, ["emission_milli"] = 1000, ["points_cm"] = new JArray(),
                    ["motion"] = new JObject { ["kind"] = "still", ["amplitude_cm"] = 0, ["frequency_mhz"] = 0, ["phase_mdeg"] = 0 }
                });
                nodes.Add(new JObject {
                    ["node_id"] = "n" + n,
                    ["activation"] = new JObject { ["copies"] = copies, ["max_activations"] = activations, ["delay_ticks"] = 0 },
                    ["appearance"] = new JObject { ["construction"] = new JObject { ["parts"] = parts } }
                });
            }
            return new JObject {
                ["schema_version"] = "sp.compiled/1.0", ["description_sha256"] = digest,
                ["versions"] = new JObject { ["min_client"] = "1.3.0" },
                ["plan"] = new JObject { ["schema_version"] = "sp.plan/1.0", ["description_sha256"] = digest,
                    ["visual_reference_sha256"] = digest, ["nodes"] = nodes },
                ["visual_reference"] = new JObject { ["sha256"] = digest, ["description_sha256"] = digest,
                    ["artifact_id"] = "a" + new string('b',32), ["size_bytes"] = 512, ["width_px"] = 512, ["height_px"] = 512,
                    ["prompt_version"] = "sp.prompt.g/1.0" },
                ["resource_bounds"] = new JObject { ["max_instances"] = nodeCount * copies * activations,
                    ["max_effect_applications"] = 0, ["max_end_tick"] = 100, ["geometry_bytes"] = 0, ["max_colliders"] = 0 }
            };
        }
    }
}
