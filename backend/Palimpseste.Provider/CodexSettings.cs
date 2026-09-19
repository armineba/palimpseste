using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace Palimpseste.Provider;

/// <summary>
/// Runtime-only configuration for the Codex worker. Values are supplied by the
/// service operator; no client request can override them.
/// </summary>
public sealed record CodexSettings(
    string Executable,
    string CodexHome,
    string AttemptRoot,
    string ExpectedServiceUser,
    string Model,
    string Effort,
    bool EffortCompatibilityVerified,
    TimeSpan AttemptTimeout,
    int MaxOutputBytes = 2_000_000,
    string TrustedSpecificationRoot = "",
    string TrustedInputRoot = "",
    string DevelopmentRoot = "",
    string CompatibilityEvidencePath = "",
    string CompatibilityEvidenceSha256 = "",
    bool RuntimeFeaturesCompatibilityVerified = false,
    string RuntimeFeaturesEvidencePath = "",
    string RuntimeFeaturesEvidenceSha256 = "")
{
    // These are the capabilities that must be observed false in the CLI
    // feature table before a runtime feature evidence file can be accepted.
    // unified_exec is the CLI's PTY implementation on current Windows builds;
    // it is recorded separately and is not treated as an exposed tool here.
    public static IReadOnlyList<string> RuntimeFeatureGateNames { get; } =
    [
        "shell_tool", "computer_use", "browser_use", "browser_use_external",
        "browser_use_full_cdp_access", "apps", "plugins", "hooks", "multi_agent",
        "skill_mcp_dependency_install", "shell_snapshot", "web_search", "standalone_web_search", "web_search_cached",
        "web_search_request", "remote_plugin", "goals", "memories", "personality",
        "code_mode", "in_app_browser", "in_app_chat", "in_app_dictation",
        "in_app_local_automation", "in_app_updates", "image_generation", "skill_search",
        "tool_suggest", "view_image", "workspace_dependencies", "sleep_tool",
        "tool_call_mcp_elicitation", "auth_elicitation", "code_mode_host"
    ];

    public static CodexSettings FromEnvironment() => new(
        Read("PALIMPSESTE_CODEX_EXE", "PALIMPSESTE_CODEX_EXECUTABLE"),
        Read("PALIMPSESTE_CODEX_HOME"),
        Read("PALIMPSESTE_ATTEMPT_ROOT", "PALIMPSESTE_CODEX_JOB_ROOT"),
        Read("PALIMPSESTE_SERVICE_USER"),
        // The runtime has one deliberately fixed model. A missing value is
        // represented as empty so the doctor reports a configuration error.
        Read("PALIMPSESTE_LUNA_MODEL", "PALIMPSESTE_LUNA_MODEL_A") is { Length: > 0 } model ? model : "gpt-5.6-luna",
        Read("PALIMPSESTE_LUNA_EFFORT", "PALIMPSESTE_LUNA_RESOLVED_EFFORT") is { Length: > 0 } effort ? effort : "max",
        ParseBool("PALIMPSESTE_EFFORT_VERIFIED"),
        TimeSpan.FromSeconds(ParseInt("PALIMPSESTE_ATTEMPT_SECONDS", "PALIMPSESTE_PROVIDER_ATTEMPT_TIMEOUT_SECONDS", 1800)),
        ParseInt("PALIMPSESTE_MAX_OUTPUT_BYTES", defaultValue: 2_000_000),
        Read("PALIMPSESTE_SPEC_ROOT"),
        Read("PALIMPSESTE_INPUT_ROOT", "PALIMPSESTE_ARTIFACT_ROOT", "ARTIFACT_ROOT"),
        Read("PALIMPSESTE_DEVELOPMENT_ROOT"),
        Read("PALIMPSESTE_EFFORT_EVIDENCE_PATH", "PALIMPSESTE_PROVIDER_EVIDENCE_PATH"),
        Read("PALIMPSESTE_EFFORT_EVIDENCE_SHA256"),
        ParseBool("PALIMPSESTE_RUNTIME_FEATURES_VERIFIED"),
        Read("PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_PATH"),
        Read("PALIMPSESTE_RUNTIME_FEATURE_EVIDENCE_SHA256"));

    /// <summary>
    /// Performs the checks that must pass before a process can be started.
    /// The explicit compatibility probe may defer only the effort evidence
    /// check that the probe itself is about to establish; all other production
    /// checks remain active.
    /// </summary>
    public IReadOnlyList<string> Check(bool production, bool compatibilityProbe = false)
    {
        var issues = new List<string>();

        CheckDirectoryOrFile(Executable, file: true, "codex_executable_missing", issues);
        CheckDirectoryOrFile(CodexHome, file: false, "dedicated_codex_home_missing", issues);
        CheckDirectoryOrFile(AttemptRoot, file: false, "attempt_root_missing", issues);
        CheckDirectoryOrFile(TrustedSpecificationRoot, file: false, "spec_root_missing", issues);
        CheckDirectoryOrFile(TrustedInputRoot, file: false, "input_root_missing", issues);
        // This path is an operator audit boundary. The runtime account is
        // intentionally denied traversal into it, so existence is checked by
        // deployment tooling rather than by the worker process itself.
        if (!IsFullyQualified(DevelopmentRoot)) issues.Add("development_root_missing");

        if (string.IsNullOrWhiteSpace(ExpectedServiceUser) ||
            !string.Equals(Environment.UserName, ExpectedServiceUser, StringComparison.OrdinalIgnoreCase))
            issues.Add("service_identity_mismatch");

        if (Model != "gpt-5.6-luna") issues.Add("model_mismatch");
        if (Effort is not ("low" or "medium" or "high" or "xhigh" or "max")) issues.Add("invalid_effort");
        else if (Effort != "max") issues.Add("effort_below_documented_max");
        // The active doctor may establish this one fact. All other production
        // checks remain enabled for its ProbeAsync path.
        if (production && !EffortCompatibilityVerified && !compatibilityProbe) issues.Add("effort_not_verified");
        if (production && !RuntimeFeaturesCompatibilityVerified) issues.Add("runtime_feature_disable_not_verified");
        string? executableHash = null;
        if (production && (RuntimeFeaturesCompatibilityVerified || EffortCompatibilityVerified))
        {
            try { executableHash = ComputeExecutableSha256(Executable); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            { issues.Add("codex_executable_hash_unavailable"); }
        }
        if (production && RuntimeFeaturesCompatibilityVerified && executableHash is not null &&
            !TryVerifyRuntimeFeatureEvidence(executableHash, out var featureEvidenceIssue))
            issues.Add(featureEvidenceIssue ?? "runtime_feature_evidence_invalid");
        if (production && EffortCompatibilityVerified && executableHash is not null &&
            !TryVerifyCompatibilityEvidence(executableHash, out var evidenceIssue))
            issues.Add(evidenceIssue ?? "effort_evidence_invalid");
        if (AttemptTimeout <= TimeSpan.Zero || AttemptTimeout > TimeSpan.FromHours(2)) issues.Add("invalid_timeout");
        if (MaxOutputBytes < 100_000 || MaxOutputBytes > 10_000_000) issues.Add("invalid_output_limit");

        if (ParseBool("PALIMPSESTE_ALLOW_MODEL_FALLBACK")) issues.Add("model_fallback_enabled");
        if (ParseBool("PALIMPSESTE_ALLOW_REASONING_DOWNGRADE")) issues.Add("reasoning_downgrade_enabled");
        if (ParseBool("PALIMPSESTE_RUNTIME_EXECUTION_TOOLS")) issues.Add("runtime_execution_tools_enabled");

        // The process runner deliberately clears the environment and uses a
        // dedicated home. A home nested in the current interactive profile is
        // a configuration error even when its directory happens to exist.
        if (IsFullyQualified(CodexHome))
        {
            var currentProfile = Environment.GetEnvironmentVariable("USERPROFILE") ??
                                  Environment.GetEnvironmentVariable("HOME");
            if (IsPathInside(CodexHome, currentProfile, allowEqual: true))
                issues.Add("codex_home_inside_user_profile");
        }

        if (IsFullyQualified(CodexHome) && IsFullyQualified(AttemptRoot))
            AddOverlap("home_and_attempt_root_overlap", CodexHome, AttemptRoot, issues);
        if (IsFullyQualified(AttemptRoot) && IsFullyQualified(TrustedInputRoot))
            AddOverlap("attempt_root_and_input_root_overlap", AttemptRoot, TrustedInputRoot, issues);
        if (IsFullyQualified(AttemptRoot) && IsFullyQualified(TrustedSpecificationRoot))
            AddOverlap("attempt_root_and_spec_root_overlap", AttemptRoot, TrustedSpecificationRoot, issues);
        if (IsFullyQualified(CodexHome) && IsFullyQualified(TrustedInputRoot))
            AddOverlap("home_and_input_root_overlap", CodexHome, TrustedInputRoot, issues);
        if (IsFullyQualified(CodexHome) && IsFullyQualified(TrustedSpecificationRoot))
            AddOverlap("home_and_spec_root_overlap", CodexHome, TrustedSpecificationRoot, issues);

        // Runtime state and input must not live inside the development tree.
        // The specification tree is intentionally allowed to be the deployed
        // application tree because prompts and schemas are copied per attempt.
        if (IsFullyQualified(DevelopmentRoot))
        {
            if (IsPathInside(CodexHome, DevelopmentRoot, allowEqual: true)) issues.Add("codex_home_inside_development_root");
            if (IsPathInside(AttemptRoot, DevelopmentRoot, allowEqual: true)) issues.Add("attempt_root_inside_development_root");
            if (IsPathInside(TrustedInputRoot, DevelopmentRoot, allowEqual: true)) issues.Add("input_root_inside_development_root");
            if (IsPathInside(TrustedSpecificationRoot, DevelopmentRoot, allowEqual: true)) issues.Add("spec_root_inside_development_root");
        }

        if (production && IsFullyQualified(TrustedSpecificationRoot))
        {
            if (!OperatingSystem.IsWindows()) issues.Add("spec_root_acl_unverifiable");
            else if (!TryVerifySpecificationReadOnly(out var specificationAclIssue))
                issues.Add(specificationAclIssue ?? "spec_root_acl_unverifiable");
        }
        if (production && IsFullyQualified(DevelopmentRoot))
        {
            if (!OperatingSystem.IsWindows()) issues.Add("development_root_acl_unverifiable");
            else if (!TryVerifyDevelopmentDenied(out var developmentAclIssue))
                issues.Add(developmentAclIssue ?? "development_root_acl_unverifiable");
        }

        if (IsFullyQualified(CodexHome) && HasReparsePoint(CodexHome)) issues.Add("codex_home_reparse_point");
        if (IsFullyQualified(AttemptRoot) && HasReparsePoint(AttemptRoot)) issues.Add("attempt_root_reparse_point");
        if (IsFullyQualified(TrustedSpecificationRoot) && HasReparsePoint(TrustedSpecificationRoot)) issues.Add("spec_root_reparse_point");
        if (IsFullyQualified(TrustedInputRoot) && HasReparsePoint(TrustedInputRoot)) issues.Add("input_root_reparse_point");

        return issues;
    }

    public bool IsTrustedSpecificationFile(string path) => IsTrustedFile(path, TrustedSpecificationRoot);

    public static string ComputeExecutableSha256(string path)
    {
        if (!IsFullyQualified(path) || HasReparsePoint(path)) throw new ArgumentException("Untrusted Codex executable path", nameof(path));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    public bool IsTrustedInputFile(string path) => IsTrustedFile(path, TrustedInputRoot);

    public static bool IsTrustedFile(string path, string root)
    {
        if (!IsFullyQualified(path) || !IsFullyQualified(root)) return false;
        if (!File.Exists(path) || HasReparsePoint(path)) return false;
        return IsPathInside(path, root, allowEqual: false);
    }

    public static bool IsFullyQualified(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path);

    public static bool IsPathInside(string? candidate, string? root, bool allowEqual)
    {
        if (!IsFullyQualified(candidate) || !IsFullyQualified(root)) return false;
        var candidateFull = Path.GetFullPath(candidate!);
        var rootFull = Path.GetFullPath(root!);
        if (string.Equals(candidateFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)) return allowEqual;

        var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidateFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasReparsePoint(string path)
    {
        if (!IsFullyQualified(path)) return true;
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (root is null) return true;
        var remainder = full[root.Length..];
        var current = root;
        foreach (var part in remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
        return false;
    }

    [SupportedOSPlatform("windows")]
    private bool TryVerifySpecificationReadOnly(out string? issue)
    {
        issue = null;
        try
        {
            var expectedSid = ResolveExpectedServiceSid();
            var broadWriteSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "S-1-1-0",   // Everyone
                "S-1-5-11",  // Authenticated Users
                "S-1-5-32-545" // Builtin Users
            };
            if (expectedSid is not null) broadWriteSids.Add(expectedSid.Value);

            var pending = new Stack<FileSystemInfo>();
            pending.Push(new DirectoryInfo(TrustedSpecificationRoot));
            while (pending.Count != 0)
            {
                var item = pending.Pop();
                if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    issue = "spec_tree_reparse_point";
                    return false;
                }
                FileSystemSecurity security = item is DirectoryInfo directory
                    ? (FileSystemSecurity)directory.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner)
                    : ((FileInfo)item).GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
                if (!TryCheckSpecificationAcl(security, expectedSid, broadWriteSids, out issue)) return false;
                if (item is DirectoryInfo childDirectory)
                    foreach (var child in childDirectory.GetFileSystemInfos()) pending.Push(child);
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or IdentityNotMappedException or InvalidOperationException)
        {
            issue = "spec_root_acl_unverifiable";
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryCheckSpecificationAcl(FileSystemSecurity security, SecurityIdentifier? expectedSid,
        IReadOnlySet<string> broadWriteSids, out string? issue)
    {
        issue = null;
        var owner = security.GetOwner(typeof(SecurityIdentifier));
        if (owner is SecurityIdentifier ownerSid && expectedSid is not null && ownerSid.Equals(expectedSid))
        {
            issue = "spec_tree_service_is_owner";
            return false;
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference is SecurityIdentifier sid &&
                broadWriteSids.Contains(sid.Value) && HasWritePermission(rule.FileSystemRights))
            {
                issue = "spec_tree_service_write_access";
                return false;
            }
        }
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static bool HasWritePermission(FileSystemRights rights)
    {
        const FileSystemRights writeBits = FileSystemRights.WriteData | FileSystemRights.AppendData |
            FileSystemRights.WriteAttributes | FileSystemRights.WriteExtendedAttributes |
            FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
            FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        return (rights & writeBits) != 0 || (rights & FileSystemRights.Modify) == FileSystemRights.Modify ||
            (rights & FileSystemRights.FullControl) == FileSystemRights.FullControl;
    }

    [SupportedOSPlatform("windows")]
    private bool TryVerifyDevelopmentDenied(out string? issue)
    {
        issue = null;
        try
        {
            var expectedSid = ResolveExpectedServiceSid();
            if (expectedSid is null)
            {
                issue = "development_root_service_sid_unresolved";
                return false;
            }
            var security = new DirectoryInfo(DevelopmentRoot).GetAccessControl(AccessControlSections.Access);
            const FileSystemRights requiredDeny = FileSystemRights.ReadData | FileSystemRights.ReadAttributes |
                FileSystemRights.ReadExtendedAttributes | FileSystemRights.ExecuteFile |
                FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteAttributes |
                FileSystemRights.WriteExtendedAttributes | FileSystemRights.Delete |
                FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions |
                FileSystemRights.TakeOwnership;
            const InheritanceFlags children = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            var inheritedDenyIsComplete = false;
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                if (rule.AccessControlType == AccessControlType.Deny && rule.IdentityReference is SecurityIdentifier sid &&
                    sid.Equals(expectedSid) && !rule.IsInherited && (rule.FileSystemRights & requiredDeny) == requiredDeny &&
                    (rule.InheritanceFlags & children) == children && rule.PropagationFlags == PropagationFlags.None)
                    inheritedDenyIsComplete = true;
            }
            if (!inheritedDenyIsComplete)
            {
                issue = "development_root_deny_incomplete_or_not_inherited";
                return false;
            }

            // The same service identity that will spawn Codex must actually be
            // unable to list the root and open representative source files.
            // No file contents are changed by these probes.
            try
            {
                using var enumerator = Directory.EnumerateFileSystemEntries(DevelopmentRoot).GetEnumerator();
                enumerator.MoveNext();
                issue = "development_root_listable_by_service";
                return false;
            }
            catch (UnauthorizedAccessException) { }

            string[] sentinels =
            [
                "prompts/00_AGENT_BUILD.md",
                "backend/Palimpseste.Worker/Program.cs",
                "game/Assets/Palimpseste/Bootstrap/PalimpsesteApp.cs",
                "ops/provision-runtime.ps1"
            ];
            foreach (var relative in sentinels)
            {
                var path = Path.GetFullPath(Path.Combine(DevelopmentRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathInside(path, DevelopmentRoot, allowEqual: false) || HasReparsePoint(path))
                {
                    issue = "development_probe_path_untrusted";
                    return false;
                }
                if (!OpenIsDenied(path, FileAccess.Read))
                {
                    issue = "development_source_readable_or_missing";
                    return false;
                }
                if (!OpenIsDenied(path, FileAccess.Write))
                {
                    issue = "development_source_writable_or_missing";
                    return false;
                }
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or IdentityNotMappedException or InvalidOperationException or ArgumentException)
        {
            issue = "development_root_acl_unverifiable";
            return false;
        }
    }

    private static bool OpenIsDenied(string path, FileAccess access)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, access, FileShare.ReadWrite | FileShare.Delete);
            return false;
        }
        catch (UnauthorizedAccessException) { return true; }
    }

    private bool TryVerifyCompatibilityEvidence(string executableHash, out string? issue)
    {
        issue = null;
        if (!IsFullyQualified(CompatibilityEvidencePath) || !File.Exists(CompatibilityEvidencePath))
        {
            issue = "effort_evidence_missing";
            return false;
        }
        if (HasReparsePoint(CompatibilityEvidencePath))
        {
            issue = "effort_evidence_reparse_point";
            return false;
        }
        if (!RegexSha256(CompatibilityEvidenceSha256))
        {
            issue = "effort_evidence_hash_missing";
            return false;
        }
        try
        {
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(CompatibilityEvidencePath)));
            if (!string.Equals(actualHash, CompatibilityEvidenceSha256, StringComparison.OrdinalIgnoreCase))
            {
                issue = "effort_evidence_hash_mismatch";
                return false;
            }
            using var json = JsonDocument.Parse(File.ReadAllText(CompatibilityEvidencePath), new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !StringProperty(root, "kind", "provider_doctor") ||
                !StringProperty(root, "mode", "active") ||
                !StringProperty(root, "active_result", "success") ||
                !StringProperty(root, "requested_model", Model) ||
                !StringProperty(root, "requested_effort", Effort) ||
                !StringProperty(root, "cli_executable_sha256", executableHash) ||
                !StringProperty(root, "expected_service_identity", ExpectedServiceUser) ||
                !StringProperty(root, "service_identity", ExpectedServiceUser) ||
                !StageMetadata(root, "stage_a", Model, Effort) ||
                !StageMetadata(root, "stage_b", Model, Effort))
            {
                issue = "effort_evidence_content_invalid";
                return false;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            issue = "effort_evidence_unreadable";
            return false;
        }
    }

    private bool TryVerifyRuntimeFeatureEvidence(string executableHash, out string? issue)
    {
        issue = null;
        if (!IsFullyQualified(RuntimeFeaturesEvidencePath) || !File.Exists(RuntimeFeaturesEvidencePath))
        {
            issue = "runtime_feature_evidence_missing";
            return false;
        }
        if (HasReparsePoint(RuntimeFeaturesEvidencePath) || !RegexSha256(RuntimeFeaturesEvidenceSha256))
        {
            issue = "runtime_feature_evidence_untrusted_path_or_hash";
            return false;
        }
        try
        {
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(RuntimeFeaturesEvidencePath)));
            if (!string.Equals(actualHash, RuntimeFeaturesEvidenceSha256, StringComparison.OrdinalIgnoreCase))
            {
                issue = "runtime_feature_evidence_hash_mismatch";
                return false;
            }
            using var json = JsonDocument.Parse(File.ReadAllText(RuntimeFeaturesEvidencePath), new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !StringProperty(root, "cli_executable_sha256", executableHash) ||
                !RuntimeFeatureEvidenceContentIsValid(root))
            {
                issue = "runtime_feature_evidence_content_invalid";
                return false;
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            issue = "runtime_feature_evidence_unreadable";
            return false;
        }
    }

    private bool RuntimeFeatureEvidenceContentIsValid(JsonElement root)
    {
        if (StringProperty(root, "kind", "provider_feature_doctor"))
        {
            return StringProperty(root, "service_identity", ExpectedServiceUser) &&
                StringProperty(root, "model", Model) && StringProperty(root, "effort", Effort) &&
                StringProperty(root, "disable_flag_parser_status", "ok") &&
                root.TryGetProperty("effective_disable_observed", out var observed) &&
                observed.ValueKind == JsonValueKind.True;
        }

        // The local doctor itself is accepted as evidence after an operator
        // reviews and hashes the complete JSON. This keeps the evidence tied
        // to the service identity and to every observed capability value.
        if (!StringProperty(root, "kind", "provider_doctor") ||
            !StringProperty(root, "mode", "local") ||
            !StringProperty(root, "service_identity", ExpectedServiceUser) ||
            !StringProperty(root, "expected_service_identity", ExpectedServiceUser) ||
            !StringProperty(root, "requested_model", Model) ||
            !StringProperty(root, "requested_effort", Effort) ||
            !StringProperty(root, "disable_flag_parser_status", "ok") ||
            !StringProperty(root, "feature_list_status", "ok") ||
            !root.TryGetProperty("feature_list_observation_complete", out var complete) ||
            complete.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("feature_list_effective_disable_observed", out var observedLocal) ||
            observedLocal.ValueKind != JsonValueKind.True ||
            !root.TryGetProperty("feature_list_observations", out var observations) ||
            observations.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var feature in RuntimeFeatureGateNames)
        {
            if (!observations.TryGetProperty(feature, out var value) || value.ValueKind != JsonValueKind.False)
                return false;
        }
        return true;
    }

    private static bool RegexSha256(string value) =>
        value.Length == 64 && value.All(character => Uri.IsHexDigit(character));

    private static bool StringProperty(JsonElement root, string name, string expected) =>
        root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String &&
        string.Equals(property.GetString(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool StageMetadata(JsonElement root, string name, string expectedModel, string expectedEffort)
    {
        if (!root.TryGetProperty(name, out var stage) || stage.ValueKind != JsonValueKind.Object ||
            !StringProperty(stage, "outcome", "Success") || !StringProperty(stage, "requested_model", expectedModel) ||
            !StringProperty(stage, "requested_effort", expectedEffort) || !StringProperty(stage, "reported_model", expectedModel) ||
            !StringProperty(stage, "reported_effort", expectedEffort)) return false;
        return true;
    }

    [SupportedOSPlatform("windows")]
    private SecurityIdentifier? ResolveExpectedServiceSid()
    {
        if (string.IsNullOrWhiteSpace(ExpectedServiceUser)) return null;
        try
        {
            var account = ExpectedServiceUser.Contains('\\', StringComparison.Ordinal)
                ? ExpectedServiceUser
                : $"{Environment.MachineName}\\{ExpectedServiceUser}";
            return (SecurityIdentifier)new NTAccount(account).Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException) { return null; }
    }

    private static string Read(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return "";
    }

    private static bool ParseBool(string name) =>
        bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;

    private static int ParseInt(string name, string? alias = null, int defaultValue = 0)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw) && alias is not null) raw = Environment.GetEnvironmentVariable(alias);
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static void CheckDirectoryOrFile(string? path, bool file, string issue, ICollection<string> issues)
    {
        if (!IsFullyQualified(path) || (file ? !File.Exists(path!) : !Directory.Exists(path!))) issues.Add(issue);
    }

    private static void AddOverlap(string issue, string first, string second, ICollection<string> issues)
    {
        if (IsPathInside(first, second, allowEqual: true) || IsPathInside(second, first, allowEqual: true)) issues.Add(issue);
    }
}

public enum ProviderOutcome
{
    Success, Refusal, Incomplete, Authentication, ModelUnavailable, EffortUnsupported,
    Quota, TransportUncertain, Timeout, ProcessFailure, InvalidSchema, BusinessViolation, IsolationViolation
}

public sealed record CodexAttempt(
    string AttemptId, string Stage, string Prompt, string SchemaPath, IReadOnlyList<string> Images,
    string JobId);

public sealed record CodexResult(
    ProviderOutcome Outcome, string? FinalJson, int? ExitCode, string? SessionId,
    string? ErrorCode, DateTimeOffset StartedAt, DateTimeOffset EndedAt, string CliVersion,
    string RequestedModel, string RequestedEffort, string? ReportedModel, string? ReportedEffort,
    string? UsageJson, string? AttemptDirectory = null, bool ProcessStarted = false,
    string? DiagnosticStderr = null,
    string? DiagnosticStdoutSha256 = null, int? DiagnosticStdoutLength = null,
    bool DiagnosticStdoutTruncated = false,
    string? DiagnosticEventErrorSha256 = null, int? DiagnosticEventErrorLength = null,
    bool DiagnosticEventErrorTruncated = false,
    string? DiagnosticCategory = null);
