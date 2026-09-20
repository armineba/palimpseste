using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Palimpseste.Game.SpellRuntime
{
    public sealed class LabReceiver : MonoBehaviour
    {
        private sealed class TimedEffect
        {
            public int Amount;
            public int Until;
            public int NextTick;
        }

        private readonly Dictionary<string, TimedEffect> timed = new Dictionary<string, TimedEffect>(StringComparer.Ordinal);
        private int currentTick;
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
        public float SpeedFactor => CanMove ? (1f - (SlowUntil > currentTick ? SlowAmount / 1000f : 0f)) *
            (1f + StatusAmount("haste") / 1000f) : 0f;
        public bool CanMove => !HasStatus("root") && !HasStatus("stun");
        public bool CanCast => HealthMilli > 0 && !HasStatus("stun");
        public int ShieldMilli => StatusAmount("barrier_health");
        public int LastTickHealMilli { get; private set; }
        public int StructureMilli => GetComponent<LabBreakable>()?.StructureMilli ?? 0;
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
            var burning = BurnUntil > currentTick;
            var wet = WetUntil > currentTick;
            var slowed = SlowUntil > currentTick;
            var poisoned = HasStatus("poison");
            var bleeding = HasStatus("bleed");
            var frozen = HasStatus("freeze_damage") || HasStatus("root");
            var shielded = HasStatus("barrier_health");
            var regenerative = HasStatus("regen");
            var stunned = HasStatus("stun");
            var modified = timed.Count > 0;
            statusHalo.gameObject.SetActive(burning || wet || slowed || modified);
            if (!statusHalo.gameObject.activeSelf) return;
            var color = burning ? new Color(1f, .36f, .17f, .9f) : bleeding ?
                new Color(.8f, .08f, .13f, .9f) : poisoned ? new Color(.35f, .9f, .16f, .9f) :
                frozen ? new Color(.35f, .85f, 1f, .9f) : stunned ? new Color(1f, .82f, .2f, .9f) :
                shielded ? new Color(.4f, .75f, 1f, .9f) : regenerative ?
                new Color(.2f, 1f, .45f, .9f) : wet ?
                new Color(.23f, .78f, 1f, .85f) : slowed ? new Color(.75f, .52f, 1f, .8f) :
                new Color(.88f, .54f, 1f, .8f);
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
            gameObject.SetActive(true);
            HealthMilli = MaxHealthMilli;
            BurnAmount = BurnUntil = BurnNextTick = WetUntil = SlowAmount = SlowUntil = 0;
            timed.Clear(); currentTick = 0; LastTickHealMilli = 0;
            GetComponent<LabBreakable>()?.Restore();
            if (body != null) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }

        public bool HasStatus(string kind) => timed.TryGetValue(kind, out var status) && status.Until > currentTick;

        public void AdvanceTick(int tick) { currentTick = tick; }

        public int StatusAmount(string kind) => HasStatus(kind) ? timed[kind].Amount : 0;

        public int OutgoingDamagePerMille => 1000 - StatusAmount("weakness");

        public int ApplyDamage(int amount, int outgoingPerMille = 1000)
        {
            if (amount <= 0 || HealthMilli <= 0) return 0;
            var protection = Mathf.Max(0, StatusAmount("damage_reduction") - StatusAmount("armor_break"));
            var scaled = (long)amount * Mathf.Clamp(outgoingPerMille, 0, 1000) / 1000;
            scaled = scaled * (1000 + StatusAmount("vulnerability")) / 1000;
            scaled = scaled * (1000 - protection) / 1000;
            var shield = timed.TryGetValue("barrier_health", out var barrier) && barrier.Until > currentTick ? barrier : null;
            if (shield != null)
            {
                var absorbed = (int)Math.Min(scaled, shield.Amount);
                shield.Amount -= absorbed; scaled -= absorbed;
                if (shield.Amount <= 0) timed.Remove("barrier_health");
            }
            var applied = (int)Math.Min(Math.Max(scaled, 0), HealthMilli);
            HealthMilli -= applied;
            return applied;
        }

        public int ApplyHeal(int amount)
        {
            if (HealthMilli <= 0) return 0;
            var scaled = (long)Mathf.Max(amount, 0) * (1000 - StatusAmount("healing_reduction")) / 1000;
            var applied = (int)Math.Min(scaled, MaxHealthMilli - HealthMilli);
            HealthMilli += applied;
            return applied;
        }

        public void Impulse(Vector3 vector, int amount)
        {
            if (CanMove && body != null && !body.isKinematic) body.AddForce(vector.normalized * (amount / 1000f), ForceMode.Impulse);
        }

        public int Shatter(int amount)
        {
            var breakable = GetComponent<LabBreakable>();
            return breakable == null ? 0 : breakable.ApplyShatter(amount);
        }

        public string ApplyStatus(string kind, int amount, int duration, int tick)
        {
            currentTick = tick;
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
            if (kind == "cleanse")
            {
                BurnAmount = BurnUntil = BurnNextTick = SlowAmount = SlowUntil = WetUntil = 0;
                foreach (var negative in new[] { "bleed", "poison", "freeze_damage", "root", "stun",
                    "weakness", "vulnerability", "armor_break", "healing_reduction" }) timed.Remove(negative);
            }
            if (kind == "dispel")
                foreach (var positive in new[] { "regen", "barrier_health", "haste", "damage_reduction" })
                    timed.Remove(positive);
            if (duration > 0 && kind != "burn" && kind != "wet" && kind != "slow")
            {
                if (!timed.TryGetValue(kind, out var status) || status.Until <= tick)
                {
                    status = new TimedEffect { NextTick = tick + 50 };
                    timed[kind] = status;
                }
                status.Amount = Mathf.Max(status.Amount, amount);
                status.Until = Mathf.Max(status.Until, tick + duration);
                if (kind == "root" || kind == "stun")
                    if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
            }
            return null;
        }

        public int TickStatus(int tick)
        {
            currentTick = tick;
            LastTickHealMilli = 0;
            var damage = 0;
            if (BurnUntil >= tick && BurnNextTick > 0 && tick >= BurnNextTick)
            {
                damage = ApplyDamage(BurnAmount);
                BurnNextTick += 50;
            }
            if (BurnUntil <= tick) { BurnUntil = 0; BurnAmount = 0; BurnNextTick = 0; }
            if (WetUntil <= tick) WetUntil = 0;
            if (SlowUntil <= tick) { SlowUntil = 0; SlowAmount = 0; }
            foreach (var kind in new[] { "bleed", "poison", "freeze_damage", "regen" })
            {
                if (!timed.TryGetValue(kind, out var status) || status.Until < tick || status.NextTick > tick) continue;
                if (kind == "regen") LastTickHealMilli += ApplyHeal(status.Amount);
                else damage += ApplyDamage(status.Amount);
                status.NextTick += 50;
            }
            foreach (var kind in new List<string>(timed.Keys))
                if (timed[kind].Until <= tick) timed.Remove(kind);
            return damage;
        }
    }

    // Only this authored lab component admits structural destruction.
    public sealed class LabBreakable : MonoBehaviour
    {
        public int StructureMilli { get; private set; } = 100000;

        public int ApplyShatter(int amount)
        {
            var applied = Mathf.Min(Mathf.Max(amount, 0), StructureMilli);
            StructureMilli -= applied;
            if (StructureMilli <= 0) gameObject.SetActive(false);
            return applied;
        }

        public void Restore()
        {
            StructureMilli = 100000;
            gameObject.SetActive(true);
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
        private float yaw = -22f, pitch = 25f, distance = 17f;
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
                Library.ImageSpellPacketValidator.Validate(json, parchmentDirectory);
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
            camera3d.backgroundColor = new Color(.035f, .042f, .064f);
            camera3d.fieldOfView = 44f;
            camera3d.allowHDR = true;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            var profile = Resources.Load<VolumeProfile>("LabVfxVolume");
            if (profile != null)
            {
                var volumeObject = new GameObject("Lumière des sorts");
                volumeObject.transform.SetParent(transform, false);
                var volume = volumeObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10;
                volume.sharedProfile = profile;
            }
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
            if (team == "environment") obj.AddComponent<LabBreakable>();
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
            // Keep the spell arena centered in the actual visible viewport,
            // rather than hiding its left third behind the parchment panel.
            var panelFraction = Mathf.Clamp(390f / Mathf.Max(1, Screen.width), 0, .45f);
            camera3d.rect = new Rect(panelFraction, 0, 1 - panelFraction, 1);
            var rot = Quaternion.Euler(pitch, yaw, 0);
            var focus = new Vector3(0, 1.1f, 1.2f);
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
