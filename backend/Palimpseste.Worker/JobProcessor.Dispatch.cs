using Palimpseste.Provider;

namespace Palimpseste.Worker;

public sealed partial class JobProcessor
{
    public async Task RunAsync(CancellationToken shutdown)
    {
        if (settings.MaxConcurrentJobs < 0)
            throw new InvalidOperationException("invalid_worker_concurrency");
        var drainFile = ResolveDrainFile();
        var active = new HashSet<Task>();
        var concurrency = settings.MaxConcurrentJobs == 0 ? "unlimited" : settings.MaxConcurrentJobs.ToString();
        Console.WriteLine($"worker dispatcher started: concurrency={concurrency}, drain={(drainFile is null ? "disabled" : "enabled")}");
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                await ReapCompletedAsync(active);
                if (drainFile is not null && File.Exists(drainFile))
                {
                    // Deployment can stop admission without cancelling an already
                    // charged provider call, releasing its lease, or abandoning it.
                    Console.WriteLine($"worker drain requested: active={active.Count}");
                    break;
                }
                if (settings.MaxConcurrentJobs > 0 && active.Count >= settings.MaxConcurrentJobs)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), shutdown);
                    continue;
                }

                ClaimedJob? job;
                try { job = await jobs.ClaimAsync(workerId, shutdown); }
                catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { break; }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"worker claim failed: {e.GetType().Name}");
                    await Task.Delay(TimeSpan.FromSeconds(5), shutdown);
                    continue;
                }
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), shutdown);
                    continue;
                }

                // Each job owns its cancellation, heartbeat, and fenced claim.
                // Calling the async method starts I/O immediately without blocking
                // admission on the completion of another parchment.
                active.Add(ProcessClaimedJobAsync(job, shutdown));
                Console.WriteLine($"worker job admitted: job={job.Id:N}, fence={job.Fence}, active={active.Count}");
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        finally
        {
            // Track only live tasks and observe every outcome, including cleanup
            // failures. Drain preserves their tokens; shutdown cancels them through
            // the linked per-job source and still awaits provider cleanup.
            while (active.Count != 0)
            {
                await Task.WhenAny(active);
                await ReapCompletedAsync(active);
            }
            Console.WriteLine("worker dispatcher stopped: active=0");
        }
    }

    private async Task ProcessClaimedJobAsync(ClaimedJob job, CancellationToken shutdown)
    {
        using var leaseLost = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var heartbeat = HeartbeatAsync(job, leaseLost);
        try { await ProcessAsync(job, leaseLost.Token); }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            Console.WriteLine($"worker job cancelled for shutdown: job={job.Id:N}");
        }
        catch (OperationCanceledException) when (leaseLost.IsCancellationRequested)
        {
            // The persisted fence decides who can resume. Never write an incident
            // or retry through a lease that this process no longer owns.
            Console.Error.WriteLine($"worker job lease lost: job={job.Id:N}, fence={job.Fence}");
        }
        catch (AnimationSheetException e)
        {
            Console.Error.WriteLine($"job {job.Id:N} animation sheet: {e.InnerException?.GetType().Name}");
            try
            {
                await jobs.SetStateAsync(job, "needs_operator", "animation_sheet_failed",
                    "La mise en page de la planche a été interrompue. Les images sont conservées ; réessaie pour continuer.",
                    false, CancellationToken.None);
            }
            catch (Exception) { /* A lost lease may already belong to another worker. */ }
        }
        catch (VisualCaptureException e)
        {
            Console.Error.WriteLine($"job {job.Id:N} visual capture: {e.Reason} ({e.InnerException?.GetType().Name})");
            try
            {
                await jobs.SetStateAsync(job, "needs_operator", "visual_capture_failed",
                    "Finition visuelle interrompue ; image et construction conservées. Réessaie la finition.",
                    false, CancellationToken.None);
            }
            catch (Exception) { /* A lost lease may already belong to another worker. */ }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"job {job.Id:N} incident: {e.GetType().Name}");
            try { await jobs.SetStateAsync(job, "needs_operator", "worker_exception", "Incident technique à examiner", false, CancellationToken.None); }
            catch (Exception) { /* A lost lease may already belong to another worker. */ }
        }
        finally
        {
            leaseLost.Cancel();
            try { await heartbeat; }
            catch (OperationCanceledException) when (leaseLost.IsCancellationRequested) { }
            Console.WriteLine($"worker job finished: job={job.Id:N}, fence={job.Fence}");
        }
    }

    private static async Task ReapCompletedAsync(HashSet<Task> active)
    {
        foreach (var task in active.Where(task => task.IsCompleted).ToArray())
        {
            active.Remove(task);
            try { await task; }
            catch (Exception e)
            {
                // Business failures are handled inside their own job. This covers
                // unexpected heartbeat/cleanup failures without killing siblings.
                Console.Error.WriteLine($"worker task cleanup failed: {e.GetType().Name}");
            }
        }
    }

    private string? ResolveDrainFile()
    {
        var configured = Environment.GetEnvironmentVariable("PALIMPSESTE_WORKER_DRAIN_FILE");
        if (string.IsNullOrWhiteSpace(configured)) return null;
        if (!Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("invalid_worker_drain_path");
        var path = Path.GetFullPath(configured);
        var executableDirectory = Path.GetDirectoryName(Path.GetFullPath(settings.Executable));
        if (!string.Equals(Path.GetDirectoryName(path), executableDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(path), "worker-drain.request", StringComparison.Ordinal) ||
            CodexSettings.HasReparsePoint(path) || Directory.Exists(path))
            throw new InvalidOperationException("invalid_worker_drain_path");
        return path;
    }
}
