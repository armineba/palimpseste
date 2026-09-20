using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Contracts;
using Palimpseste.Core;
using Palimpseste.Provider;
using ProviderVisualReference = Palimpseste.Provider.SpellVisualReference;
using ContractVisualReference = Palimpseste.Contracts.SpellVisualReference;

namespace Palimpseste.Worker;

public sealed partial class JobProcessor
{
    private async Task<byte[]?> RefineVisualsAsync(ClaimedJob job, byte[] description, byte[] plan,
        StoredDocument planRecord, string geometryContext, string capabilities,
        ProviderVisualReference reference, ContractVisualReference metadata, CompilationInput input,
        IReadOnlyDictionary<string, byte[]> geometry, IReadOnlyDictionary<string, byte[]> masks, CancellationToken ct,
        SpellReferenceResearch? research = null)
    {
        // The skill's critic loop is translated into fixed application actions.
        // Codex sees only data and images. Neither critic nor planner can run this renderer or a software build.
        const int targetScore = 10000;
        var originalMechanics = Mechanics(plan);
        string? previousVerdict = null;
        for (var round = planRecord.Revision; round < 4; round++)
        {
            ct.ThrowIfCancellationRequested();
            input.PlanJson = plan;
            var compiled = SpellCompiler.Compile(input);
            if (!compiled.Success) throw new InvalidDataException("visual_candidate_compile_failed");
            var known = await jobs.GetVisualReviewAsync(job, Sha256(plan), ct);
            VisualCaptureOutput? capture = null;
            if (known is null)
            {
                if (await jobs.CountTotalAttemptsAsync(job, ct) >= 10) break;
                await jobs.SetStateAsync(job, "refining_visuals", null, "Comparaison du rendu avec l’image du sort", false, ct);
                var assets = MakeCaptureAssets(input, geometry, masks, await File.ReadAllBytesAsync(reference.PngPath, ct), metadata);
                // A rendering failure cannot masquerade as a judged match.
                capture = await new TrustedVisualCapture(settings).CaptureAsync(job.Id, compiled.PayloadUtf8, description, assets, ct);
                var attempt = await jobs.BeginAttemptAsync(job, "J", settings.Model, settings.Effort,
                    Sha256(Encoding.UTF8.GetBytes(Sha256(plan) + reference.Sha256 + Sha256(capture.Manifest) + LunaCodexProvider.PromptJVersion)), ct);
                var judged = await provider.JudgeVisualAsync(job.Id.ToString("N"), attempt.ToString("N"), description, plan,
                    reference, capture.Frames, previousVerdict, false, ct);
                if (judged.Document.Utf8 is null)
                {
                    await jobs.CompleteAttemptAsync(job, attempt,
                        judged.Document.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                        judged.Document.Sha256, judged.Document.Transport.CliVersion, judged.Document.Transport.SessionId,
                        judged.Document.Transport.UsageJson, judged.Document.Transport.ErrorCode ?? "visual_judge_failed", ct);
                    await jobs.SetStateAsync(job, "needs_operator", "visual_judge_failed",
                        "Comparaison visuelle interrompue ; dessin, description, image et construction conservés", false, ct);
                    return null;
                }
                var verdictArtifact = await files.PutAsync(judged.Document.Utf8, "json", "application/json", ct);
                var captureArtifact = await files.PutAsync(capture.Manifest, "json", "application/json", ct);
                previousVerdict = Encoding.UTF8.GetString(judged.Document.Utf8);
                await jobs.SaveVisualReviewAsync(job, attempt, Sha256(plan), judged.Score, judged.LifecycleFaithful,
                    verdictArtifact, captureArtifact, previousVerdict, judged.Document.Transport, ct);
                known = new(judged.Score, judged.LifecycleFaithful, previousVerdict);
            }
            previousVerdict = known.VerdictJson;
            if (known.Score >= targetScore && known.LifecycleFaithful)
            {
                capture ??= await new TrustedVisualCapture(settings).CaptureAsync(job.Id, compiled.PayloadUtf8, description,
                    MakeCaptureAssets(input, geometry, masks, await File.ReadAllBytesAsync(reference.PngPath, ct), metadata), ct);
                if (capture.MeasuredFps >= 30) break;
            }
            if (round == 3 || await jobs.CountTotalAttemptsAsync(job, ct) > 8) break;
            // A resumed job may have a saved verdict but not its in-memory frame paths.
            // Replaying the immutable packet is a bounded renderer action and consumes no model call.
            capture ??= await new TrustedVisualCapture(settings).CaptureAsync(job.Id, compiled.PayloadUtf8, description,
                MakeCaptureAssets(input, geometry, masks, await File.ReadAllBytesAsync(reference.PngPath, ct), metadata), ct);
            await jobs.SetStateAsync(job, "refining_visuals", null, "Ajustement des matières, mouvements et détails", false, ct);
            var revisionAttempt = await jobs.BeginAttemptAsync(job, "B", settings.Model, settings.Effort,
                Sha256(Encoding.UTF8.GetBytes(Sha256(plan) + Sha256(capture.Manifest) + previousVerdict + provider.PromptBSha256)), ct);
            var result = await provider.RefineVisualAsync(job.Id.ToString("N"), revisionAttempt.ToString("N"), description,
                plan, geometryContext, capabilities, reference, capture.Frames, previousVerdict, false, ct, rethink: round >= 2, research: research);
            var issues = result.Utf8 is null ? [] : SpellCompiler.ValidatePlanJson(description, result.Utf8, geometry, masks, metadata, research?.Sha256);
            var mechanicsChanged = result.Utf8 is not null && issues.Count == 0 && Mechanics(result.Utf8) != originalMechanics;
            if (result.Utf8 is null || issues.Count != 0 || mechanicsChanged)
            {
                await jobs.CompleteAttemptAsync(job, revisionAttempt,
                    result.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                    result.Sha256, result.Transport.CliVersion, result.Transport.SessionId, result.Transport.UsageJson,
                    mechanicsChanged ? "visual_refinement_changed_mechanics" : "visual_refinement_invalid", ct);
                // Preserve the best already rendered and judged valid plan, never a broken refinement.
                break;
            }
            var artifact = await files.PutAsync(result.Utf8, "json", "application/json", ct);
            await jobs.SavePlanAsync(job, revisionAttempt, artifact, Encoding.UTF8.GetString(result.Utf8),
                LunaCodexProvider.PromptBVersion, result.Transport, ct, round + 1);
            plan = result.Utf8;
            planRecord = (await jobs.GetPlanAsync(job, ct))!;
        }
        var best = await jobs.GetBestReviewedPlanAsync(job, ct);
        if (best is null)
        {
            await jobs.SetStateAsync(job, "needs_operator", "visual_review_missing", "Comparaison visuelle non terminée ; construction conservée", false, ct);
            return null;
        }
        await jobs.SetStateAsync(job, "validating", null, "Finalisation du sort et de ses animations", false, ct);
        return await ReadCheckedAsync(best.StorageKey, best.Sha256, ct);
    }

    private static Dictionary<string, byte[]> MakeCaptureAssets(CompilationInput input,
        IReadOnlyDictionary<string, byte[]> geometry, IReadOnlyDictionary<string, byte[]> masks,
        byte[] reference, ContractVisualReference metadata)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var pair in input.GeometryArtifactIds) result.Add(pair.Value, geometry[pair.Key]);
        foreach (var pair in input.MaskArtifactIds) result.Add(pair.Value, masks[pair.Key]);
        result.Add(metadata.artifact_id, reference);
        return result;
    }

    // All gameplay, provenance and layout fields remain byte-equivalent as normalized JSON.
    // Only the three bounded decorative profiles may change during a critic iteration.
    public static string Mechanics(byte[] plan)
    {
        var token = ContractJson.ParseStrict(plan);
        foreach (var node in (JArray)token["nodes"]!)
        {
            if (node["appearance"] is JObject appearance)
            {
                appearance.Property("construction")?.Remove();
                appearance.Property("vfx")?.Remove();
                appearance.Property("lifecycle")?.Remove();
                appearance.Property("resource_id")?.Remove();
            }
            // These six fields move only visual layers. Typed intent, cast range, offsets,
            // gravity and launch pitch remain immutable and the compiler rechecks all bounds.
            if (node["physics"] is JObject physics)
                foreach (var key in new[] { "angular_speed_mdeg_s", "axial_speed_cm_s", "radial_speed_cm_s",
                             "radius_cm", "turbulence_cm", "frequency_mhz" }) physics.Property(key)?.Remove();
        }
        return Normalize(token).ToString(Newtonsoft.Json.Formatting.None);
    }

    private static JToken Normalize(JToken token) => token is JObject obj
        ? new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new JProperty(p.Name, Normalize(p.Value))))
        : token is JArray array ? new JArray(array.Select(Normalize)) : token.DeepClone();
}
