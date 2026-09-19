# Provider doctor and runtime feature gate — 2026-09-19

## Local observation

The published `ProviderDoctor.exe` was executed as `PalRuntimeSvc` with the
runtime `CODEX_HOME` and the copied Codex CLI (`codex-cli 0.154.0-alpha.6.2`).
The local doctor completed with exit code 0. It executed `--version`,
`exec --help`, the complete `--disable` parser check, `features list`, and
`login status`; it did not execute a model request.

The requested model was `gpt-5.6-luna` and the requested reasoning effort was
`max`. Every exposed capability in the runtime gate was observed `false`,
including shell, computer, browser, apps, plugins, agents, image view and the
network search aliases. The CLI reported `unified_exec=true`; this is retained
as an internal PTY implementation observation and is not treated as an exposed
worker tool. The legacy `web_search` key was absent from the CLI table and was
bound only to the explicitly observed `standalone_web_search=false` row; the
mapping is recorded in the hashed doctor JSON.

The reviewed JSON is stored outside the repository at
`E:\PalimpsesteRuntime\evidence\doctor-local-service-final.json` with SHA-256:

`c50647f63efda97ea989374bef7b6b3a6e3bdce6e00d0446758be6db99968b51`

The runtime environment enables `PALIMPSESTE_RUNTIME_FEATURES_VERIFIED` only
with this path and hash. A later no-write doctor check under `PalRuntimeSvc`
reported `runtime_features_compatibility_verified=true` and the only remaining
production issue was `effort_not_verified`.

## Active doctor gate

The final active doctor was invoked under `PalRuntimeSvc` with the prepared
reference, drawing and geometry inputs. It exited 2 before A/B because the
dedicated Codex home had no authenticated account. Its result is stored at
`E:\PalimpsesteRuntime\evidence\doctor-active-service-final.json` with SHA-256:

`5cf4bc017a8a111cdadc4055d736fc6486eb702262b113af9532e65e0912297e`

The result has `active_blocked=dedicated_auth_not_confirmed`,
`model_calls_executed=false`, `runtime_features_compatibility_verified=true`
and `production_issues=[effort_not_verified]`. No personal `auth.json` or
token was copied, and no Luna model call is claimed.
