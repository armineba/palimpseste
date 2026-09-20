using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Palimpseste.Contracts;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Palimpseste.Game.PlayModeTests
{
    public sealed class SemanticVisualPlayModeTests
    {
        private static SpellNode Node(string form, string carrier = "projectile") => new SpellNode
        {
            node_id = "semantic", subject_id = "subject", carrier = carrier,
            scale_cm = 150, geometry_id = "path", anchor = "caster",
            activation = new SpellActivation { @event = "cast", max_activations = 1, copies = 1 },
            appearance = new SpellAppearance { form = form, palette = "stone", signature_geometry_id = "signature" },
            options = new SpellOptions { motion = "straight", radius_cm = 25, speed_cm_s = 300,
                lifetime_ticks = 50, range_cm = 1000, contact_filter = "hostile", pierces = 0,
                bounces = 0, width_cm = 10 },
            effects = new List<SpellEffect>()
        };

        [UnityTest]
        public IEnumerator EveryControlledFormHasBoundedVolumesAndReleasesResources()
        {
            Assert.NotNull(Resources.Load<Shader>("SpellEnergy"));
            foreach (var form in SpellVisualForms.All)
            {
                var root = new GameObject("Semantic fixture " + form);
                var visual = root.AddComponent<SemanticSpellVisual>();
                visual.Initialize(Node(form), new Color(.25f, .65f, 1));
                yield return null;
                Assert.AreEqual(form, visual.Form);
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.Greater(renderers.Length, 0, form + " must contain solid meshes");
                Assert.LessOrEqual(renderers.Length, 32, form + " renderer budget");
                var meshes = root.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).Distinct().ToArray();
                Assert.IsTrue(meshes.Any(mesh => mesh.bounds.size.x > .03f && mesh.bounds.size.y > .03f && mesh.bounds.size.z > .03f),
                    form + " must contain a genuinely volumetric subject");
                foreach (var mesh in meshes) Assert.Less(mesh.vertexCount, 5000, form + " per-mesh budget");
                Assert.IsEmpty(root.GetComponentsInChildren<Collider>(), "Decorative volumes may not add collisions");
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
                    Assert.LessOrEqual(particles.main.maxParticles, 48);
                var materials = root.GetComponentsInChildren<Renderer>().Select(renderer => renderer.sharedMaterial).Distinct().ToArray();
                foreach (var material in materials) Assert.IsFalse(material.shader.name.Contains("InternalError"));
                UnityEngine.Object.Destroy(root);
                yield return null;
                yield return null;
                foreach (var mesh in meshes) Assert.IsTrue(mesh == null, form + " leaked procedural mesh");
                foreach (var material in materials) Assert.IsTrue(material == null, form + " leaked material");
            }
        }

        [UnityTest]
        public IEnumerator RuntimeSemanticProjectileIgnoresAvailableDrawingSignature()
        {
            var node = Node("boulder");
            var mask = new Texture2D(2, 2);
            mask.SetPixels(new[] { Color.magenta, Color.magenta, Color.magenta, Color.magenta }); mask.Apply();
            var assets = new Dictionary<string, GeometryAsset>
            {
                ["path"] = new GeometryAsset { kind = "path", points = new List<GeometryPoint>
                    { new GeometryPoint { x = -10000 }, new GeometryPoint { x = 10000 } } },
                ["signature"] = new GeometryAsset { kind = "silhouette", mask_file = "ink.png" }
            };
            var packet = new CompiledSpell { plan = new SpellPlan { nodes = new List<SpellNode> { node } },
                resource_bounds = new ResourceBounds { max_instances = 4 } };
            var engine = new RuntimeEngine(packet, assets, new Dictionary<string, Texture2D> { ["ink.png"] = mask },
                null, new List<LabReceiver>());
            Assert.IsTrue(engine.TryCast(new Vector3(0, 5, 0), new Vector3(0, 5, 8), Vector3.forward));
            engine.Tick();
            yield return null;
            var visual = UnityEngine.Object.FindFirstObjectByType<SemanticSpellVisual>();
            Assert.NotNull(visual, "Runtime must select the semantic renderer");
            Assert.AreEqual("boulder", visual.Form);
            foreach (var child in visual.GetComponentsInChildren<Transform>())
            {
                Assert.IsFalse(child.name.Contains("dessin"), "A drawing contour or glyph was attached to the interpreted rock");
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial.HasProperty("_BaseMap"))
                    Assert.AreNotSame(mask, renderer.sharedMaterial.GetTexture("_BaseMap"));
            }
            engine.CancelAll();
            UnityEngine.Object.Destroy(mask);
            yield return null;
            Assert.IsTrue(visual == null, "Cancelling the spell releases its semantic visual");
        }

        [UnityTest]
        public IEnumerator SemanticImpactExpiresAndHasNoGameplayComponents()
        {
            SemanticSpellVisual.Impact("meteor", Vector3.zero, Vector3.forward, Color.yellow, .4f);
            var impact = GameObject.Find("Semantic impact meteor");
            Assert.NotNull(impact);
            Assert.IsEmpty(impact.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(impact.GetComponentsInChildren<LabReceiver>());
            var particles = impact.GetComponentInChildren<ParticleSystem>();
            Assert.NotNull(particles);
            Assert.LessOrEqual(particles.main.maxParticles, 48);
            yield return new WaitForSeconds(.85f);
            Assert.IsTrue(impact == null, "Impact afterimages must clean up without another cast");
        }

        [UnityTest]
        public IEnumerator BarrierSurfaceShowsItsCompletePhysicalExtent()
        {
            var root = new GameObject("Barrier extent fixture");
            root.transform.position = new Vector3(3, 2, -1);
            var node = Node("shield", "barrier");
            node.scale_cm = 420; node.options.height_cm = 180; node.options.thickness_cm = 24;
            root.AddComponent<SemanticSpellVisual>().Initialize(node, Color.cyan);
            yield return null;
            var surface = root.transform.Find("Barrier physical extent");
            Assert.NotNull(surface, "A central shield alone cannot communicate a wider blocking wall");
            var bounds = surface.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(bounds.size.x, Is.EqualTo(4.2f).Within(.001f));
            Assert.That(bounds.size.y, Is.EqualTo(1.8f).Within(.001f));
            Assert.That(bounds.size.z, Is.EqualTo(.24f).Within(.001f));
            Assert.That(bounds.min.y, Is.EqualTo(0).Within(.001f));
            Assert.IsTrue(surface.GetComponent<Renderer>().enabled);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(), "The visual extent must not duplicate gameplay colliders");
            UnityEngine.Object.Destroy(root);
            yield return null;
        }
    }
}
