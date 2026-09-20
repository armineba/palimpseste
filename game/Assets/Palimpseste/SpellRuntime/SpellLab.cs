using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Palimpseste.Contracts;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    public sealed class LabReceiver : MonoBehaviour
    {
        public int StableId { get; private set; }
        public string Team { get; private set; }
        public int HealthMilli { get; private set; } = 100000;
        public int MaxHealthMilli { get; private set; } = 100000;
        public int BurnAmount { get; private set; }
        public int BurnUntil { get; private set; }
        public int BurnNextTick { get; private set; }
        public int WetUntil { get; private set; }
        public int SlowAmount { get; private set; }
        public int SlowUntil { get; private set; }
        public float SpeedFactor => 1f - (SlowUntil > 0 ? SlowAmount / 1000f : 0f);
        private Rigidbody body;
        private LineRenderer statusHalo;
        private Transform healthFill;

        public void AttachVisuals(LineRenderer halo, Transform fill)
        {
            statusHalo = halo;
            healthFill = fill;
        }

        private void Update()
        {
            if (healthFill != null)
            {
                var fraction = MaxHealthMilli <= 0 ? 0f : Mathf.Clamp01(HealthMilli / (float)MaxHealthMilli);
                var width = .96f * fraction;
                healthFill.localScale = new Vector3(width, healthFill.localScale.y, healthFill.localScale.z);
                healthFill.localPosition = new Vector3(-.48f + width * .5f, healthFill.localPosition.y, healthFill.localPosition.z);
            }
            if (statusHalo == null) return;
            var burning = BurnUntil > 0;
            var wet = WetUntil > 0;
            var slowed = SlowUntil > 0;
            statusHalo.gameObject.SetActive(burning || wet || slowed);
            if (!statusHalo.gameObject.activeSelf) return;
            var color = burning ? new Color(1f, .36f, .17f, .9f) : wet ?
                new Color(.23f, .78f, 1f, .85f) : new Color(.75f, .52f, 1f, .8f);
            statusHalo.startColor = statusHalo.endColor = color;
            statusHalo.widthMultiplier = .045f + .017f * (1f + Mathf.Sin(Time.time * 7f)) * .5f;
        }

        public void Initialize(int id, string team, int healthMilli = 100000)
        {
            StableId = id; Team = team; HealthMilli = MaxHealthMilli = healthMilli;
            body = GetComponent<Rigidbody>();
        }

        public void Restore()
        {
            HealthMilli = MaxHealthMilli;
            BurnAmount = BurnUntil = BurnNextTick = WetUntil = SlowAmount = SlowUntil = 0;
            if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }

        public int ApplyDamage(int amount)
        {
            var applied = Mathf.Min(Mathf.Max(amount, 0), HealthMilli);
            HealthMilli -= applied;
            return applied;
        }

        public int ApplyHeal(int amount)
        {
            if (HealthMilli <= 0) return 0;
            var applied = Mathf.Min(Mathf.Max(amount, 0), MaxHealthMilli - HealthMilli);
            HealthMilli += applied;
            return applied;
        }

        public void Impulse(Vector3 vector, int amount)
        {
            if (body != null && !body.isKinematic) body.AddForce(vector.normalized * (amount / 1000f), ForceMode.Impulse);
        }

        public string ApplyStatus(string kind, int amount, int duration, int tick)
        {
            if (kind == "wet")
            {
                var reaction = BurnUntil > tick ? "vapeur" : null;
                BurnAmount = BurnUntil = BurnNextTick = 0;
                WetUntil = Mathf.Max(WetUntil, tick + duration);
                return reaction;
            }
            if (kind == "burn")
            {
                if (WetUntil > tick) { WetUntil = 0; return "vapeur"; }
                if (BurnUntil <= tick) BurnNextTick = tick + 50;
                BurnAmount = Mathf.Max(BurnAmount, amount);
                BurnUntil = Mathf.Max(BurnUntil, tick + duration);
            }
            if (kind == "slow")
            {
                SlowAmount = Mathf.Max(SlowAmount, Mathf.Min(amount, 750));
                SlowUntil = Mathf.Max(SlowUntil, tick + duration);
            }
            return null;
        }

        public int TickStatus(int tick)
        {
            var damage = 0;
            if (BurnUntil >= tick && BurnNextTick > 0 && tick >= BurnNextTick)
            {
                damage = ApplyDamage(BurnAmount);
                BurnNextTick += 50;
            }
            if (BurnUntil <= tick) { BurnUntil = 0; BurnAmount = 0; BurnNextTick = 0; }
            if (WetUntil <= tick) WetUntil = 0;
            if (SlowUntil <= tick) { SlowUntil = 0; SlowAmount = 0; }
            return damage;
        }
    }

    public sealed class BarrierReceiver : MonoBehaviour
    {
        public int StructureMilli;
        public int BlocksLeft;
        public int InstanceId;
        public int Damage(int amount) { var taken = Mathf.Min(amount, StructureMilli); StructureMilli -= taken; return taken; }
    }

    public sealed class SpellLab : MonoBehaviour
    {
        private CompiledSpell spell;
        private readonly Dictionary<string, GeometryAsset> geometry = new Dictionary<string, GeometryAsset>();
        private readonly Dictionary<string, Texture2D> masks = new Dictionary<string, Texture2D>();
        private readonly List<LabReceiver> targets = new List<LabReceiver>();
        private RuntimeEngine runtime;
        private Camera camera3d;
        private Transform caster;
        private Vector3 aim = new Vector3(0, 0, 5);
        private float yaw = 20f, pitch = 31f, distance = 18f;
        private int shots;
        private Vector3[] initialPositions;
        private string loadError;
        public string Metrics => loadError != null ? loadError : "Lancers : " + shots + "\nTouches : " + runtime.Hits + " · Dégâts : " + (runtime.DamageMilli / 1000f).ToString("F1") + " · Soins : " + (runtime.HealMilli / 1000f).ToString("F1") + "\nImpulsions : " + runtime.Impulses + " · Statuts : " + runtime.Statuses + "\nInstances : " + runtime.ActiveCount + " · Tick : " + runtime.TickCount;
        public int Hits => runtime?.Hits ?? 0;
        public int ActiveCarriers => runtime?.ActiveCount ?? 0;
        public long DamageMilli => runtime?.DamageMilli ?? 0;
        public int Impulses => runtime?.Impulses ?? 0;
        public bool Ready => runtime != null;
        public bool InputSuppressed { get; set; }
        public string SpellTitle => spell?.display?.title ?? "Sort";

        public bool Cast(Vector3 aimPoint)
        {
            if (runtime == null || caster == null) return false;
            var direction = aimPoint - caster.position;
            direction.y = 0;
            return runtime.TryCast(caster.position, aimPoint, direction.sqrMagnitude < .01f ? Vector3.forward : direction.normalized);
        }

        public void SimulateTicks(int count)
        {
            for (var i = 0; i < count; i++) runtime?.Tick();
        }

        public void Initialize(string json, string parchmentDirectory)
        {
            try
            {
                spell = JsonConvert.DeserializeObject<CompiledSpell>(json);
                if (spell?.schema_version != "sp.compiled/1.0" || spell.plan?.nodes == null) throw new InvalidDataException("Paquet compilé invalide");
                foreach (var entry in spell.geometry_manifest)
                {
                    var path = Path.Combine(parchmentDirectory, "artifacts", entry.artifact_id);
                    var bytes = File.ReadAllBytes(path);
                    if (Library.ParchmentStore.Hash(bytes) != entry.sha256) throw new InvalidDataException("Géométrie modifiée : " + entry.id);
                    var asset = JsonConvert.DeserializeObject<GeometryAsset>(System.Text.Encoding.UTF8.GetString(bytes));
                    if (asset.geometry_id != entry.id) throw new InvalidDataException("Identité de géométrie invalide");
                    geometry.Add(entry.id, asset);
                }
                foreach (var entry in spell.binary_assets)
                {
                    var bytes = File.ReadAllBytes(Path.Combine(parchmentDirectory, "artifacts", entry.artifact_id));
                    if (Library.ParchmentStore.Hash(bytes) != entry.sha256) throw new InvalidDataException("Masque modifié");
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    if (!tex.LoadImage(bytes, false) || tex.width > 1024 || tex.height > 1024) throw new InvalidDataException("Masque invalide");
                    NormalizeMask(tex);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    masks.Add(entry.file_name, tex);
                }
                BuildScene();
                runtime = new RuntimeEngine(spell, geometry, masks, caster, targets);
                LabPerformanceProbe.AttachIfRequested(this);
            }
            catch (Exception ex) { loadError = "Sort indisponible : " + ex.Message; Debug.LogException(ex); }
        }

        private void BuildScene()
        {
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            var cameraObject = new GameObject("Caméra orbitale");
            cameraObject.transform.SetParent(transform, false);
            camera3d = cameraObject.AddComponent<Camera>();
            camera3d.clearFlags = CameraClearFlags.SolidColor;
            camera3d.backgroundColor = new Color(.045f, .078f, .1f);
            camera3d.fieldOfView = 55f;
            camera3d.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            LabSetDressing.BuildEnvironment(transform);
            caster = new GameObject("Lanceur").transform;
            caster.SetParent(transform, false);
            caster.position = new Vector3(0, 1.1f, -5);
            var self = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            self.name = "Avatar neutre";
            self.transform.SetParent(caster);
            self.transform.localPosition = Vector3.zero;
            self.transform.localScale = new Vector3(.6f, .8f, .6f);
            LabSetDressing.DecorateCaster(caster, self);
            self.AddComponent<LabReceiver>().Initialize(1, "ally");
            targets.Add(self.GetComponent<LabReceiver>());
            AddTarget(2, new Vector3(-3, 1, 3), "hostile", true);
            AddTarget(3, new Vector3(0, 1, 6), "hostile", true);
            AddTarget(4, new Vector3(3, 1, 4), "hostile", true);
            AddTarget(5, new Vector3(-5, 1, 7), "ally", false);
            AddTarget(6, new Vector3(5, .55f, 8), "environment", true);
            initialPositions = new Vector3[targets.Count];
            for (var i = 0; i < targets.Count; i++) initialPositions[i] = targets[i].transform.position;
            Physics.SyncTransforms();
            UpdateCamera();
        }

        private static void NormalizeMask(Texture2D texture)
        {
            // The current resolver writes alpha masks. Older illustrative PNG fixtures
            // are opaque grayscale; convert those after verifying their original bytes.
            var pixels = texture.GetPixels32();
            var hasTransparency = false;
            var hasDark = false;
            foreach (var pixel in pixels)
            {
                if (pixel.a < 255) hasTransparency = true;
                if (pixel.r < 250 || pixel.g < 250 || pixel.b < 250) hasDark = true;
                if (hasTransparency && hasDark) break;
            }
            if (hasTransparency || !hasDark) return;
            for (var i = 0; i < pixels.Length; i++)
            {
                var luminance = (byte)((pixels[i].r + pixels[i].g + pixels[i].b) / 3);
                pixels[i] = new Color32(255, 255, 255, luminance);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        private void AddTarget(int id, Vector3 position, string team, bool dynamic)
        {
            var type = team == "environment" ? PrimitiveType.Cube : PrimitiveType.Capsule;
            var obj = GameObject.CreatePrimitive(type);
            obj.transform.SetParent(transform, false);
            obj.name = team == "environment" ? "Caisse dynamique" : team == "ally" ? "Allié" : "Cible hostile " + id;
            obj.transform.position = position;
            if (team == "environment") obj.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
            var receiver = obj.AddComponent<LabReceiver>();
            if (dynamic)
            {
                var body = obj.AddComponent<Rigidbody>();
                body.mass = team == "environment" ? 3f : 80f;
                body.constraints = team == "environment" ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeRotation;
            }
            receiver.Initialize(id, team, team == "ally" ? 50000 : 100000);
            targets.Add(receiver);
            LabSetDressing.DecorateReceiver(obj, team, receiver);
        }

        private void Update()
        {
            if (runtime == null) return;
            if (InputSuppressed) return;
            if (Input.GetMouseButton(1)) { yaw += Input.GetAxis("Mouse X") * 3f; pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 3f, 12f, 78f); }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y, 9, 30);
            UpdateCamera();
            if (Input.GetKeyDown(KeyCode.Escape)) return;
            if (Input.mousePosition.x > 400 && Input.GetMouseButtonDown(0))
            {
                var ray = camera3d.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100)) aim = hit.point;
                else aim = ray.origin + ray.direction * 15;
                var direction = aim - caster.position;
                direction.y = 0;
                if (direction.sqrMagnitude < .01f) direction = caster.forward;
                if (runtime.TryCast(caster.position, aim, direction.normalized)) shots++;
            }
        }

        private void FixedUpdate() { runtime?.Tick(); }

        private void OnDestroy()
        {
            runtime?.CancelAll();
            foreach (var mask in masks.Values) if (mask != null) Destroy(mask);
        }

        private void UpdateCamera()
        {
            if (camera3d == null) return;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var focus = new Vector3(0, 1.2f, 3);
            camera3d.transform.position = focus + rot * new Vector3(0, 0, -distance);
            camera3d.transform.LookAt(focus);
        }

        public void ResetTargets()
        {
            runtime?.CancelAll();
            for (var i = 0; i < targets.Count; i++)
            {
                targets[i].transform.position = initialPositions[i];
                targets[i].Restore();
            }
            shots = 0;
        }

        public static Material MaterialFor(Color color, bool transparent)
        {
            Material material;
            if (transparent)
            {
                var shader = Resources.Load<Shader>("LabUnlit");
                if (shader == null) throw new InvalidOperationException("Shader VFX du laboratoire absent du lecteur");
                material = new Material(shader);
            }
            else
            {
                var template = Resources.Load<Material>("LabOpaque");
                if (template == null) throw new InvalidOperationException("Matériau URP du laboratoire absent du lecteur");
                material = new Material(template);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_SrcBlend", 5);
                material.SetFloat("_DstBlend", 10);
                material.SetFloat("_ZWrite", 0);
                material.renderQueue = 3000;
            }
            return material;
        }
    }
}
