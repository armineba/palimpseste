using System.Text;
using System.Text.Json;
using Palimpseste.Provider;

namespace Palimpseste.Worker;

public sealed class AnimationSheetException(Exception inner) : Exception("animation_sheet_failed", inner) { }

public sealed partial class JobProcessor
{
    private async Task<StoredVisualReference?> EnsureVisualReferenceAsync(ClaimedJob job, byte[] description, CancellationToken ct)
    {
        var saved = await jobs.GetVisualReferenceAsync(job, ct);
        if (saved is not null) return saved;
        var sheetRequired = job.VisualPipelineVersion >= 4;
        var descriptionHash = Sha256(description);
        var promptHash = sheetRequired ? provider.PromptGSha256 : provider.LegacyPromptGSha256;
        var promptVersion = sheetRequired ? LunaCodexProvider.PromptGVersion : LunaCodexProvider.LegacyPromptGVersion;
        var sheetJson = sheetRequired ? LunaCodexProvider.AnimationSheetRequest(description) : null;
        var inputHash = Sha256(Encoding.UTF8.GetBytes(descriptionHash + promptHash + sheetJson));
        var atlas = sheetRequired ? await jobs.GetVisualAtlasAsync(job, ct) : null;

        if (atlas is null)
        {
            await jobs.SetStateAsync(job, "generating_visual_reference", null,
                sheetRequired ? "Création de la planche animée du sort" : "Création de l’image du sort", false, ct);
            var attempt = await jobs.BeginAttemptAsync(job, "G", settings.Model, settings.Effort, inputHash, ct);
            var generated = sheetRequired
                ? await provider.GenerateVisualReferenceAsync(job.Id.ToString("N"), attempt.ToString("N"), description, ct)
                : await provider.GenerateLegacyVisualReferenceAsync(job.Id.ToString("N"), attempt.ToString("N"), description, ct);
            if (generated.Transport.Outcome != ProviderOutcome.Success || generated.PngBytes is null)
            {
                var error = "provider_g_" + generated.Transport.Outcome.ToString().ToLowerInvariant();
                await jobs.CompleteAttemptAsync(job, attempt,
                    generated.Transport.Outcome == ProviderOutcome.TransportUncertain ? "transport_uncertain" : "invalid",
                    generated.Sha256, generated.Transport.CliVersion, generated.Transport.SessionId,
                    generated.Transport.UsageJson, error, ct);
                if (!generated.Transport.ProcessStarted && generated.Transport.Outcome == ProviderOutcome.ProcessFailure &&
                    await jobs.CountAttemptsAsync(job, "G", ct) < 3)
                {
                    await jobs.ScheduleRetryAsync(job, "G", error, ct);
                    return null;
                }
                await jobs.SetStateAsync(job, "needs_operator", error,
                    "Référence visuelle indisponible ; dessin et description conservés", false, ct);
                return null;
            }
            var artifact = await files.PutAsync(generated.PngBytes, "png", "image/png", ct);
            if (!sheetRequired)
                await jobs.SaveVisualReferenceAsync(job, attempt, artifact, descriptionHash, inputHash,
                    promptVersion, generated.Width!.Value, generated.Height!.Value, generated.Transport, ct);
            else
            {
                if (generated.AnimationSheetJson != sheetJson || Sha256(generated.PngBytes) != generated.Sha256)
                    throw new InvalidDataException("visual_atlas_provenance_mismatch");
                // Commit the genuine native PNG and complete G BEFORE any fixed composition.
                // A stopped compositor resumes here without another image-generation call.
                await jobs.SaveVisualAtlasAsync(job, attempt, artifact, descriptionHash, inputHash,
                    promptVersion, generated.Width!.Value, generated.Height!.Value, sheetJson!, generated.Transport, ct);
                atlas = await jobs.GetVisualAtlasAsync(job, ct)
                    ?? throw new InvalidDataException("visual_atlas_not_persisted");
            }
        }

        if (sheetRequired)
        {
            if (atlas is null || atlas.DescriptionSha256 != descriptionHash || !SameSheet(atlas.AnimationSheetJson, sheetJson!))
                throw new InvalidDataException("visual_atlas_provenance_mismatch");
            var source = await ReadCheckedAsync(atlas.StorageKey, atlas.Sha256, ct);
            var nativeSize = VisualReferencePng.Validate(source);
            if (source.Length != atlas.SizeBytes || nativeSize.Width != atlas.Width || nativeSize.Height != atlas.Height)
                throw new InvalidDataException("visual_atlas_size_mismatch");
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("animation_sheet_requires_windows");
            using var document = JsonDocument.Parse(description);
            var title = document.RootElement.GetProperty("title").GetString() ?? "Sort";
            try
            {
                var composed = AnimationSheetComposer.Compose(source, title);
                var artifact = await files.PutAsync(composed, "png", "image/png", ct);
                await jobs.SaveVisualReferenceAsync(job, atlas.ProviderAttemptId, artifact, descriptionHash, atlas.InputSha256,
                    atlas.PromptVersion, AnimationSheetComposer.Width, AnimationSheetComposer.Height, null, ct,
                    animationSheetJson: atlas.AnimationSheetJson, sourceAtlasArtifactId: atlas.ArtifactId);
                return await jobs.GetVisualReferenceAsync(job, ct)
                    ?? throw new InvalidDataException("visual_reference_not_persisted");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                // This scope starts only after the native G checkpoint was verified.
                // Retrying this failure reuses that atlas; it never reruns image generation.
                throw new AnimationSheetException(error);
            }
        }
        return await jobs.GetVisualReferenceAsync(job, ct)
            ?? throw new InvalidDataException("visual_reference_not_persisted");
    }

    private static bool SameSheet(string actual, string expected)
    {
        using var left = JsonDocument.Parse(actual);
        using var right = JsonDocument.Parse(expected);
        return JsonElement.DeepEquals(left.RootElement, right.RootElement);
    }
}
