using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;

namespace Palimpseste.Worker;

public sealed partial class JobProcessor
{
    private async Task ProcessBlueprintV2Async(ClaimedJob job, CaptureFiles drawing, byte[] description,
        string capabilities, CancellationToken ct)
    {
        var researchRecord = await jobs.GetV2PassAsync(job, 0, "research", ct);
        SpellReferenceResearch research;
        if (researchRecord is null)
        {
            await jobs.SetStateAsync(job, "resolving_geometry", null, "V2 · recherche des structures et ressources", false, ct);
            research = await new SpellReferenceResearchResolver(specRoot).ResolveAsync(description,
                new(files.PathForKey(drawing.DrawingKey), drawing.DrawingSha), ct);
            await SaveV2BytesAsync(job, 0, "research", Encoding.UTF8.GetBytes(research.Json),
                Sha256(description), null, true, ct);
        }
        else research = SpellReferenceResearch.Read(await ReadCheckedAsync(researchRecord.StorageKey, researchRecord.Sha256, ct),
            Sha256(description), drawing.DrawingSha);

        var current = await jobs.GetPlanAsync(job, ct);
        byte[]? plan = current is null ? null : await ReadCheckedAsync(current.StorageKey, current.Sha256, ct);
        string? feedback = null;
        var returnStage = "structural_core";
        var renderer = new TrustedVisualCapture(settings);
        var descriptionRecord = await jobs.GetDescriptionAsync(job, ct);
        for (var revision = current?.Revision ?? 0; revision <= 3; revision++)
        {
            if (current is null || current.Revision != revision)
            {
                var rejected = await jobs.GetV2PassAsync(job, revision, "blueprint", ct);
                if (rejected is { Accepted: false })
                {
                    feedback = Encoding.UTF8.GetString(await ReadCheckedAsync(rejected.StorageKey, rejected.Sha256, ct));
                    continue;
                }
                await jobs.SetStateAsync(job, "planning", null,
                    revision == 0 ? "V2 · intention, structure et mouvement" : "V2 · correction de " + returnStage, false, ct);
                var inputHash = Sha256(Encoding.UTF8.GetBytes(Sha256(description) + research.Sha256 +
                    LunaCodexProvider.BlueprintV2PromptVersion + (plan is null ? "" : Sha256(plan)) + feedback));
                var attempt = await jobs.BeginAttemptAsync(job, "B", settings.Model, settings.Effort, inputHash, ct);
                var candidate = await provider.PlanBlueprintV2Async(job.Id.ToString("N"), attempt.ToString("N"),
                    description, capabilities, research, plan is null ? null : Encoding.UTF8.GetString(plan), feedback, returnStage, ct);
                var issues = candidate.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, candidate.Utf8,
                    new Dictionary<string, byte[]>(), new Dictionary<string, byte[]>(), null, research.Sha256);
                var changedLock = plan is not null && candidate.Utf8 is not null && !V2RevisionAllowed(plan, candidate.Utf8, returnStage);
                if (candidate.Transport.Outcome != ProviderOutcome.Success || candidate.Utf8 is null || issues.Count != 0 || changedLock)
                {
                    await jobs.CompleteAttemptAsync(job, attempt,
                        candidate.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                        candidate.Sha256, candidate.Transport.CliVersion, candidate.Transport.SessionId,
                        candidate.Transport.UsageJson, changedLock ? "v2_locked_stage_changed" : "v2_blueprint_invalid", ct);
                    if (candidate.Transport.Outcome is ProviderOutcome.TransportUncertain or ProviderOutcome.IsolationViolation or ProviderOutcome.Refusal)
                    {
                        await StopV2Async(job, "v2_provider_unavailable", "Construction V2 interrompue ; dessin conservé", ct);
                        return;
                    }
                    feedback = JsonSerializer.Serialize(new { stage = returnStage,
                        issues = issues.Select(i => i.ToString()).ToArray(), locked_stage_changed = changedLock,
                        provider_error = candidate.Transport.ErrorCode });
                    await SaveV2BytesAsync(job, revision, "blueprint", Encoding.UTF8.GetBytes(feedback), inputHash, null, false, ct);
                    continue;
                }
                var artifact = await files.PutAsync(candidate.Utf8, "json", "application/json", ct);
                await jobs.SavePlanAsync(job, attempt, artifact, Encoding.UTF8.GetString(candidate.Utf8),
                    LunaCodexProvider.BlueprintV2PromptVersion, candidate.Transport, ct, revision);
                plan = candidate.Utf8;
                current = await jobs.GetPlanAsync(job, ct);
            }
            if (plan is null) throw new InvalidDataException("v2_missing_plan");
            var planHash = Sha256(plan);
            var coreHash = V2CoreHash(plan);
            await SaveV2BytesAsync(job, revision, "blueprint", plan, planHash, coreHash, true, ct);
            var input = new CompilationInput
            {
                DescriptionJson = description, PlanJson = plan,
                GeometryJson = new Dictionary<string, byte[]>(), MaskPng = new Dictionary<string, byte[]>(),
                GeometryArtifactIds = new Dictionary<string, string>(), MaskArtifactIds = new Dictionary<string, string>(),
                ReferenceResearchSha256 = research.Sha256, SpellId = job.Id.ToString("N"),
                ParchmentId = job.ParchmentId!.Value.ToString("N"), SignatureSeedHex = drawing.SignatureSeedHex,
                CreatedAt = await jobs.GetV2CreationTimeAsync(job, ct), MinimumClientVersion = "1.8.0",
                Provenance = new SpellProvenance { mode = "drawing", capture_sha256 = drawing.ManifestSha,
                    reference_sha256 = drawing.ReferenceSha, model_a = await jobs.GetSuccessfulRequestedModelAsync(job, "A", ct),
                    model_b = await jobs.GetSuccessfulRequestedModelAsync(job, "B", ct),
                    prompt_a_version = descriptionRecord?.PromptVersion, prompt_b_version = LunaCodexProvider.BlueprintV2PromptVersion }
            };
            var compiled = SpellCompiler.Compile(input);
            if (!compiled.Success) throw new InvalidDataException("v2_compile_rejected");
            await jobs.SetStateAsync(job, "refining_visuals", null, "V2 · contrôle de la silhouette et du mouvement", false, ct);
            var core = await CaptureV2StoredAsync(job, revision, "core", "core", compiled.PayloadUtf8, description, planHash, coreHash, renderer, ct);
            var blind = await JudgeV2StoredAsync(job, revision, "blind", "blind", description, plan,
                core.Frames.Take(1).ToArray(), null, null, ct);
            if (blind is null) return;
            var structure = await JudgeV2StoredAsync(job, revision, "structure", "structure", description, plan,
                core.Frames.Take(5).ToArray(), blind, V2TelemetrySummary(core.Manifest), ct);
            if (structure is null) return;
            if (!V2JudgementPasses(structure, "A_structure", "B_continuity", "semantic_blind") ||
                !V2MeasuredGatePasses(core.Manifest, "B_continuity"))
            {
                feedback = structure + "\nMEASUREMENTS\n" + V2TelemetrySummary(core.Manifest);
                returnStage = V2ReturnStage(structure, "structural_core");
                continue;
            }
            await jobs.MarkV2StructureAcceptedAsync(job, revision, planHash, ct);
            await SaveV2BytesAsync(job, revision, "motion", core.Manifest, planHash, coreHash, true, ct);
            await jobs.SetStateAsync(job, "refining_visuals", null, "V2 · rendu, impacts et caméra de jeu", false, ct);
            var full = await CaptureV2StoredAsync(job, revision, "secondary", "full", compiled.PayloadUtf8, description, planHash, coreHash, renderer, ct);
            var fullFrames = new[] { core.Frames[0] }.Concat(full.Frames.Take(4)).ToArray();
            var review = await JudgeV2StoredAsync(job, revision, "validation", "full", description, plan,
                fullFrames, blind, V2TelemetrySummary(full.Manifest), ct);
            if (review is null) return;
            var impactPassed = V2MeasuredGatePasses(full.Manifest, "D_impact");
            var performancePassed = V2MeasuredGatePasses(full.Manifest, "performance") && full.MeasuredFps >= 30;
            await SaveV2BytesAsync(job, revision, "impact", full.Manifest, planHash, coreHash, impactPassed, ct);
            if (!V2JudgementPasses(review, "C_rendering", "E_game_camera", "F_motion") ||
                !impactPassed || !performancePassed || !V2MeasuredGatePasses(full.Manifest, "B_continuity"))
            {
                feedback = review + "\nMEASUREMENTS\n" + V2TelemetrySummary(full.Manifest);
                returnStage = !impactPassed ? "physics" : !performancePassed ? "optimization" : V2ReturnStage(review, "rendering");
                continue;
            }
            // No hidden geometry edits during polish/optimization: both snapshot the exact accepted plan.
            await SaveV2BytesAsync(job, revision, "polish", plan, planHash, coreHash, true, ct);
            await SaveV2BytesAsync(job, revision, "optimization", full.Manifest, planHash, coreHash, true, ct);
            var sheetFrame = full.Frames.First(f => f.Phase == "normal_sheet");
            var sheetBytes = await File.ReadAllBytesAsync(sheetFrame.PngPath, ct);
            if (Sha256(sheetBytes) != sheetFrame.Sha256) throw new InvalidDataException("v2_sheet_hash");
            var sheet = await files.PutAsync(sheetBytes, "png", "image/png", ct);
            if (await jobs.GetV2PassAsync(job, revision, "sheet", ct) is null)
                await jobs.SaveV2PassAsync(job, revision, "sheet", sheet, planHash, coreHash, true, ct);
            else
            {
                var saved = (await jobs.GetV2PassAsync(job, revision, "sheet", ct))!;
                var frozenSheet = await ReadCheckedAsync(saved.StorageKey, saved.Sha256, ct);
                sheet = new(saved.ArtifactId, saved.StorageKey, saved.Sha256, frozenSheet.Length, "image/png");
            }
            input.BlueprintAnimationSheet = new BinaryAssetEntry { file_name = "v2-animation-sheet.png", artifact_id = "a" + sheet.Id.ToString("N"),
                sha256 = sheet.Sha256, size_bytes = checked((int)sheet.ByteLength), media_type = "image/png" };
            var final = SpellCompiler.Compile(input);
            if (!final.Success) throw new InvalidDataException("v2_final_compile_rejected");
            await jobs.MarkV2ValidationAcceptedAsync(job, revision, planHash, ct);
            var payload = await files.PutAsync(final.PayloadUtf8, "json", "application/json", ct);
            await jobs.PublishAsync(job, job.Id, payload, ct, "Sort V2 disponible · structure, mouvement et impacts vérifiés");
            return;
        }
        await StopV2Async(job, "v2_validation_rejected", "Le sort ne satisfait pas encore les contrôles V2 ; dessin et étapes conservés", ct);
    }

    private async Task SaveV2BytesAsync(ClaimedJob job, int revision, string pass, byte[] bytes,
        string inputHash, string? coreHash, bool accepted, CancellationToken ct)
    {
        var existing = await jobs.GetV2PassAsync(job, revision, pass, ct);
        if (existing is not null)
        {
            if (existing.InputSha256 != inputHash || existing.LockedCoreSha256 != coreHash || existing.Sha256 != Sha256(bytes) || existing.Accepted != accepted)
                throw new InvalidDataException("v2_pass_input_changed");
            return;
        }
        var artifact = await files.PutAsync(bytes, "json", "application/json", ct);
        await jobs.SaveV2PassAsync(job, revision, pass, artifact, inputHash, coreHash, accepted, ct);
    }

    private async Task<V2CaptureOutput> CaptureV2StoredAsync(ClaimedJob job, int revision, string pass, string mode,
        byte[] packet, byte[] description, string planHash, string coreHash, TrustedVisualCapture renderer, CancellationToken ct)
    {
        var saved = await jobs.GetV2PassAsync(job, revision, pass, ct);
        if (saved is not null)
        {
            if (saved.InputSha256 != planHash || saved.LockedCoreSha256 != coreHash) throw new InvalidDataException("v2_capture_input_changed");
            var output = JsonSerializer.Deserialize<V2CaptureOutput>(await ReadCheckedAsync(saved.StorageKey, saved.Sha256, ct))
                ?? throw new InvalidDataException("v2_capture_cache");
            foreach (var frame in output.Frames)
                if (!settings.IsTrustedInputFile(frame.PngPath) || Sha256(await File.ReadAllBytesAsync(frame.PngPath, ct)) != frame.Sha256)
                    throw new InvalidDataException("v2_capture_cache_hash");
            return output;
        }
        var result = await renderer.CaptureV2Async(job.Id, packet, description, new Dictionary<string, byte[]>(), mode, ct);
        await SaveV2BytesAsync(job, revision, pass, JsonSerializer.SerializeToUtf8Bytes(result), planHash, coreHash, true, ct);
        return result;
    }

    private async Task<string?> JudgeV2StoredAsync(ClaimedJob job, int revision, string pass, string mode,
        byte[] description, byte[] plan, IReadOnlyList<RenderedSpellFrame> frames, string? blind, string? telemetry, CancellationToken ct)
    {
        var saved = await jobs.GetV2PassAsync(job, revision, pass, ct);
        if (saved is not null)
        {
            if (saved.InputSha256 != Sha256(plan)) throw new InvalidDataException("v2_review_input_changed");
            return Encoding.UTF8.GetString(await ReadCheckedAsync(saved.StorageKey, saved.Sha256, ct));
        }
        var attempt = await jobs.BeginAttemptAsync(job, "J", settings.Model, settings.Effort,
            Sha256(Encoding.UTF8.GetBytes(Sha256(plan) + mode + string.Concat(frames.Select(f => f.Sha256)))), ct);
        var result = await provider.JudgeBlueprintV2Async(job.Id.ToString("N"), attempt.ToString("N"), mode,
            description, plan, frames, blind, telemetry, ct);
        if (result.Transport.Outcome != ProviderOutcome.Success || result.Utf8 is null)
        {
            await jobs.CompleteAttemptAsync(job, attempt,
                result.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                result.Sha256, result.Transport.CliVersion, result.Transport.SessionId, result.Transport.UsageJson,
                "v2_review_unavailable", ct);
            await StopV2Async(job, "v2_review_unavailable", "Contrôle visuel V2 interrompu ; construction conservée", ct);
            return null;
        }
        var artifact = await files.PutAsync(result.Utf8, "json", "application/json", ct);
        // Validation is accepted separately after combining independent judgement and engine measurements.
        await jobs.SaveV2PassAsync(job, revision, pass, artifact, Sha256(plan), V2CoreHash(plan), false, ct, attempt, result.Transport);
        return Encoding.UTF8.GetString(result.Utf8);
    }

    private Task<bool> StopV2Async(ClaimedJob job, string code, string message, CancellationToken ct)
        => jobs.SetStateAsync(job, "needs_operator", code, message, false, ct);

    internal static bool V2JudgementPasses(string json, params string[] gates)
    {
        using var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        return root.GetProperty("score_milli").GetInt32() >= 8000 &&
            (!gates.Contains("semantic_blind") || root.GetProperty("semantic_match").GetBoolean()) &&
            gates.All(g => root.GetProperty("gates").GetProperty(g).GetString() == "pass");
    }

    private static string V2TelemetrySummary(byte[] manifest)
    {
        var source = JObject.Parse(Encoding.UTF8.GetString(manifest));
        var summary = new JObject { ["manifest_sha256"] = Sha256(manifest), ["full_manifest_retained"] = true };
        foreach (var name in new[] { "schema_version", "mode", "completed", "spell_sha256", "blueprints_sha256", "measured_fps", "gates", "metrics", "impact_scenarios" })
            if (source[name] is { } value) summary[name] = value.DeepClone();
        foreach (var name in new[] { "topology_samples", "realtime_samples" })
        {
            if (source[name] is not JArray samples) continue;
            summary[name] = new JObject { ["count"] = samples.Count,
                ["samples"] = new JArray(samples.Where((_, i) => i == 0 || i == samples.Count - 1 || i % Math.Max(1, samples.Count / 12) == 0)
                    .Take(16).Select(s => s.DeepClone())) };
        }
        return summary.ToString(Newtonsoft.Json.Formatting.None);
    }

    private static bool V2MeasuredGatePasses(byte[] manifest, string name)
    {
        using var json = JsonDocument.Parse(manifest);
        return json.RootElement.GetProperty("gates").EnumerateArray()
            .Any(g => g.GetProperty("gate").GetString() == name && g.GetProperty("status").GetString() is "pass" or "technical_pass");
    }

    private static string V2ReturnStage(string judgement, string fallback)
    {
        using var json = JsonDocument.Parse(judgement);
        return json.RootElement.GetProperty("return_stage").GetString() is { } s && s != "none" ? s : fallback;
    }

    public static string V2CoreHash(byte[] plan)
    {
        var parsed = JObject.Parse(Encoding.UTF8.GetString(plan));
        var parts = new JArray(((JArray)parsed["nodes"]!).Select(n => new JObject {
            ["node_id"] = n["node_id"]!.DeepClone(), ["identity"] = n["blueprint_v2"]!["identity"]!.DeepClone(),
            ["structural_core"] = n["blueprint_v2"]!["structural_core"]!.DeepClone() }));
        return Sha256(Encoding.UTF8.GetBytes(parts.ToString(Newtonsoft.Json.Formatting.None)));
    }

    public static bool V2RevisionAllowed(byte[] before, byte[] after, string stage)
    {
        var a = JObject.Parse(Encoding.UTF8.GetString(before)); var b = JObject.Parse(Encoding.UTF8.GetString(after));
        var aa = (JArray)a["nodes"]!; var bb = (JArray)b["nodes"]!;
        if (aa.Count != bb.Count) return false;
        for (var i = 0; i < aa.Count; i++)
        {
            var old = (JObject)aa[i].DeepClone(); var next = (JObject)bb[i].DeepClone();
            var ab = (JObject)old["blueprint_v2"]!; var cb = (JObject)next["blueprint_v2"]!;
            string[] mutable = stage switch {
                "structural_core" => ["archetype", "identity", "structural_core", "motion", "phases", "rendering_layers", "unity_implementation"],
                "motion" => ["motion", "phases"],
                "physics" => ["physics", "impact", "disappearance"],
                "rendering" or "optimization" => ["rendering_layers", "unity_implementation"], _ => [] };
            foreach (var field in mutable) { ab.Remove(field); cb.Remove(field); }
            // The blueprint is bound to the controlled runtime's numeric fields.
            // Permit the matching fields in the responsible pass; otherwise a
            // legitimate change of angular speed or collision policy can never
            // satisfy both the revision lock and the compiler's traceability rules.
            // Identity/Core stay immutable outside the structural pass; effects,
            // carrier, provenance, activation and semantic intent always stay locked.
            static void Unlock(JObject left, JObject right, string parent, params string[] names)
            {
                foreach (var name in names) { (left[parent] as JObject)?.Remove(name); (right[parent] as JObject)?.Remove(name); }
            }
            if (stage == "structural_core")
            {
                old.Remove("scale_cm"); next.Remove("scale_cm");
                old.Remove("rotation_mdeg"); next.Remove("rotation_mdeg");
            }
            if (stage is "structural_core" or "motion")
            {
                Unlock(old, next, "options", "lifetime_ticks", "speed_cm_s", "motion", "turn_mdeg_s");
                Unlock(old, next, "physics", "angular_speed_mdeg_s", "axial_speed_cm_s", "radial_speed_cm_s", "frequency_mhz", "turbulence_cm", "gravity_cm_s2", "launch_pitch_mdeg");
                Unlock(old, next, "behavior", "travel", "phenomenon", "axis", "sense", "intensity");
            }
            if (stage == "physics")
            {
                Unlock(old, next, "options", "radius_cm", "width_cm", "height_cm", "thickness_cm", "range_cm", "bounces", "pierces", "block_limit", "structure_milli", "arm_ticks", "trigger_limit", "rearm_ticks");
                Unlock(old, next, "physics", "cast_range_cm", "offset_cm", "gravity_cm_s2", "launch_pitch_mdeg");
                Unlock(old, next, "behavior", "origin", "orientation", "attachment");
            }
            if (stage is "structural_core" or "rendering" or "optimization")
                Unlock(old, next, "appearance", "resource_id");
            if (!JToken.DeepEquals(old, next)) return false;
        }
        a.Remove("nodes"); b.Remove("nodes");
        return JToken.DeepEquals(a, b);
    }
}
