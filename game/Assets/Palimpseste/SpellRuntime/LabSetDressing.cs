using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    // Decorative geometry has no active collider. The fixed runtime owns all spell physics.
    internal static class LabSetDressing
    {
        private static readonly Color Brass = new Color(.68f, .45f, .21f);
        private static readonly Color GoldGlow = new Color(1f, .72f, .36f, .9f);
        private static readonly Color TealGlow = new Color(.3f, .87f, .89f, .8f);

        public static void BuildEnvironment(Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.24f, .29f, .31f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.055f, .09f, .12f);
            RenderSettings.fogStartDistance = 19f;
            RenderSettings.fogEndDistance = 37f;

            var sun = new GameObject("Lumière de l'atelier");
            sun.transform.SetParent(root, false);
            sun.transform.rotation = Quaternion.Euler(52, -32, 0);
            var main = sun.AddComponent<Light>();
            main.type = LightType.Directional;
            main.color = new Color(1f, .82f, .64f);
            main.intensity = 1.45f;
            main.shadows = LightShadows.Soft;
            main.shadowStrength = .78f;

            PointLight(root, "Braise latérale", new Vector3(7, 4, -1), new Color(1f, .43f, .2f), 13f, 2.2f);
            PointLight(root, "Reflet de cuivre", new Vector3(-8, 5, 8), new Color(.22f, .72f, .83f), 14f, 1.8f);

            var floor = Primitive(PrimitiveType.Cube, root, "Dalle d'épreuve",
                new Vector3(0, -.18f, 4), new Vector3(26, .36f, 26), new Color(.105f, .16f, .17f), true);
            floor.GetComponent<Renderer>().receiveShadows = true;
            Primitive(PrimitiveType.Cube, root, "Bord nord", new Vector3(0, .11f, 17),
                new Vector3(26.5f, .22f, .22f), Brass);
            Primitive(PrimitiveType.Cube, root, "Bord sud", new Vector3(0, .11f, -9),
                new Vector3(26.5f, .22f, .22f), Brass);
            Primitive(PrimitiveType.Cube, root, "Bord ouest", new Vector3(-13, .11f, 4),
                new Vector3(.22f, .22f, 26.5f), Brass);
            Primitive(PrimitiveType.Cube, root, "Bord est", new Vector3(13, .11f, 4),
                new Vector3(.22f, .22f, 26.5f), Brass);

            var grid = new Color(.48f, .65f, .62f, .24f);
            for (var step = -12; step <= 12; step += 2)
            {
                Line(root, "Graduation est-ouest", new Vector3(-12.8f, .017f, 4 + step),
                    new Vector3(12.8f, .017f, 4 + step), grid, .012f);
                Line(root, "Graduation nord-sud", new Vector3(step, .018f, -8.8f),
                    new Vector3(step, .018f, 16.8f), grid, .012f);
            }
            Circle(root, "Cercle de l'arène", 9.4f, .023f, new Color(.76f, .54f, .29f, .43f), .037f,
                new Vector3(0, 0, 4));
            Circle(root, "Cercle intérieur", 4.4f, .025f, new Color(.31f, .78f, .8f, .39f), .026f,
                new Vector3(0, 0, 4));

            var plinth = Primitive(PrimitiveType.Cylinder, root, "Socle lanceur",
                new Vector3(0, .16f, -5), new Vector3(1.9f, .16f, 1.9f), new Color(.33f, .29f, .23f));
            Circle(plinth.transform, "Inscription du socle", .76f, 1.08f, GoldGlow, .05f, Vector3.zero);
            foreach (var site in new[] { new Vector3(-11, 0, -7), new Vector3(11, 0, -7),
                new Vector3(-11, 0, 15), new Vector3(11, 0, 15) })
            {
                Primitive(PrimitiveType.Cube, root, "Pilier de pierre", site + Vector3.up * 1.1f,
                    new Vector3(.68f, 2.2f, .68f), new Color(.22f, .27f, .27f));
                Primitive(PrimitiveType.Cube, root, "Chapiteau de cuivre", site + Vector3.up * 2.24f,
                    new Vector3(.88f, .18f, .88f), Brass);
                Primitive(PrimitiveType.Sphere, root, "Lueur de l'arène", site + Vector3.up * 2.54f,
                    Vector3.one * .23f, new Color(.29f, .8f, .78f, .72f), false, true);
            }
        }

        public static void DecorateReceiver(GameObject actor, string team, LabReceiver receiver)
        {
            var hostile = team == "hostile";
            var ally = team == "ally";
            var body = hostile ? new Color(.43f, .16f, .15f) : ally ? new Color(.15f, .43f, .46f) : new Color(.38f, .34f, .28f);
            actor.GetComponent<Renderer>().material = SpellLab.MaterialFor(body, false);
            if (team != "environment")
            {
                var trim = hostile ? new Color(.72f, .38f, .29f) : new Color(.43f, .75f, .75f);
                Primitive(PrimitiveType.Sphere, actor.transform, "Masque sculpté", new Vector3(0, .84f, 0),
                    new Vector3(.61f, .43f, .58f), new Color(.22f, .25f, .27f));
                Primitive(PrimitiveType.Cube, actor.transform, "Épaulette gauche", new Vector3(-.52f, .47f, 0),
                    new Vector3(.44f, .2f, .52f), trim);
                Primitive(PrimitiveType.Cube, actor.transform, "Épaulette droite", new Vector3(.52f, .47f, 0),
                    new Vector3(.44f, .2f, .52f), trim);
                Primitive(PrimitiveType.Cube, actor.transform, "Viseur lumineux", new Vector3(0, .87f, -.31f),
                    new Vector3(.39f, .075f, .04f), hostile ? GoldGlow : TealGlow, false, true);
                Circle(actor.transform, "Socle du receveur", .68f, -.96f,
                    hostile ? new Color(1f, .46f, .3f, .8f) : TealGlow, .045f, Vector3.zero);
            }
            else
            {
                Primitive(PrimitiveType.Cube, actor.transform, "Cerclage du coffre", new Vector3(0, .33f, 0),
                    new Vector3(1.04f, .12f, 1.04f), Brass);
                Primitive(PrimitiveType.Cube, actor.transform, "Noyau de cuivre", new Vector3(0, .51f, -.51f),
                    new Vector3(.47f, .33f, .05f), Brass);
            }

            var baseBar = Primitive(PrimitiveType.Cube, actor.transform, "Fond santé",
                new Vector3(0, 1.35f, -.18f), new Vector3(1.04f, .095f, .065f),
                new Color(.08f, .12f, .14f), false, true);
            var fill = Primitive(PrimitiveType.Cube, actor.transform, "Santé restante",
                new Vector3(0, 1.35f, -.23f), new Vector3(.96f, .075f, .065f),
                hostile ? new Color(1f, .42f, .28f, 1f) : ally ? TealGlow : GoldGlow, false, true);
            baseBar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            fill.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var halo = Circle(actor.transform, "Signe d'état", .61f, 1.1f,
                GoldGlow, .065f, Vector3.zero);
            halo.gameObject.SetActive(false);
            receiver.AttachVisuals(halo, fill.transform);
        }

        public static void DecorateCaster(Transform caster, GameObject body)
        {
            body.GetComponent<Renderer>().material = SpellLab.MaterialFor(new Color(.68f, .55f, .37f), false);
            Primitive(PrimitiveType.Sphere, caster, "Masque du lanceur", new Vector3(0, .58f, 0),
                new Vector3(.47f, .34f, .47f), new Color(.76f, .64f, .43f));
            Primitive(PrimitiveType.Cube, caster, "Attache de cuivre", new Vector3(0, .14f, -.36f),
                new Vector3(.55f, .17f, .09f), Brass);
            Circle(caster, "Halo du lanceur", .71f, -1.07f, GoldGlow, .05f, Vector3.zero);
        }

        private static void PointLight(Transform root, string name, Vector3 position, Color color, float range, float intensity)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root, false);
            obj.transform.localPosition = position;
            var light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
        }

        private static GameObject Primitive(PrimitiveType shape, Transform parent, string name,
            Vector3 position, Vector3 scale, Color color, bool collider = false, bool emissive = false)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            var physical = obj.GetComponent<Collider>();
            if (physical != null) physical.enabled = collider;
            obj.GetComponent<Renderer>().material = SpellLab.MaterialFor(color, emissive);
            return obj;
        }

        private static LineRenderer Circle(Transform parent, string name, float radius, float height,
            Color color, float width, Vector3 center)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.widthMultiplier = width;
            line.numCornerVertices = 4;
            line.material = SpellLab.MaterialFor(Color.white, true);
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (var i = 0; i < 64; i++)
            {
                var angle = i * Mathf.PI * 2f / 64f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
            return line;
        }

        private static void Line(Transform parent, string name, Vector3 from, Vector3 to, Color color, float width)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.material = SpellLab.MaterialFor(Color.white, true);
            line.startColor = line.endColor = color;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }
}
