using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Palimpseste.Game.Library;
using UnityEngine;
using UnityEngine.Networking;

namespace Palimpseste.Game.Service
{
    [Serializable] public sealed class ParchmentDto { public string parchment_id, state, layout_version, job_id, spell_id; }
    [Serializable] public sealed class ParchmentPageDto { public ParchmentDto[] items; public string next_cursor; }
    [Serializable] public sealed class JobDto
    {
        public string job_id, parchment_id, state, resume_stage, spell_id, message, error_code, description_artifact_id;
        public string visual_reference_artifact_id, visual_reference_sha256;
        public int attempt_count, poll_after_ms;
        public long elapsed_ms;
        public string created_at, updated_at, stage_started_at;
        public bool retryable;
    }
    [Serializable] public sealed class CapabilitiesDto
    {
        public string catalog_version, rules_profile, layout_version, reference_artifact_id, minimum_client_version, principal_id;
        public string[] carriers;
    }
    [Serializable] public sealed class InvitationRequestDto { public string invitation_code; }
    [Serializable] public sealed class SessionDto { public string token; }
    [Serializable] public sealed class InterpretationFeedbackRequestDto
    {
        public string description_sha256, verdict = "incorrect", correction;
    }
    [Serializable] public sealed class InterpretationFeedbackDto
    {
        public string feedback_id, job_id, description_sha256;
        public bool recorded;
    }

    [Serializable]
    public sealed class DrawingCaptureDto
    {
        public string schema_version = "sp.capture/1.0";
        public string capture_id, parchment_id;
        public string layout_version = "free_canvas_v2";
        public string reference_sha256, raster_version, drawing_file_sha256, drawing_pixel_sha256, ink_file_sha256, journal_file_sha256;
        public int width = 1024, height = 1024;
        public long used_ink_micro_units;
        public string closed_reason;
        public string[] locked_regions;
        public string created_at;
    }

    public static class ApiErrorText
    {
        private const int DisplayLimit = 300;

        public static bool TryReadProblem(string body, out string code, out string message)
        {
            code = null;
            message = null;
            if (string.IsNullOrWhiteSpace(body) || body.Length > 8192) return false;
            try
            {
                var root = JObject.Parse(body);
                if (root["code"]?.Type != JTokenType.String || root["message"]?.Type != JTokenType.String)
                    return false;
                var parsedCode = root["code"].Value<string>();
                var parsedMessage = root["message"].Value<string>();
                if (string.IsNullOrWhiteSpace(parsedCode) || parsedCode.Length > 80 ||
                    string.IsNullOrWhiteSpace(parsedMessage)) return false;
                code = parsedCode;
                message = Bound(parsedMessage);
                return true;
            }
            catch (JsonException) { return false; }
        }

        public static string Display(string body, string transportError)
        {
            if (TryReadProblem(body, out _, out var message)) return message;
            if (!string.IsNullOrWhiteSpace(body)) return Bound(body);
            return string.IsNullOrWhiteSpace(transportError) ? "Erreur réseau" : Bound(transportError);
        }

        private static string Bound(string value)
        {
            var compact = value.Trim().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return compact.Length > DisplayLimit ? compact.Substring(0, DisplayLimit) : compact;
        }
    }

    public sealed class LabApi
    {
        public string BaseUrl { get; private set; }
        public string Token { get; private set; }
        public bool Configured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

        public void Clear()
        {
            BaseUrl = null;
            Token = null;
        }

        public void Configure(string baseUrl, string token)
        {
            if (!AllowedServiceUrl(baseUrl))
                throw new ArgumentException("HTTPS requis hors 127.0.0.1");
            BaseUrl = baseUrl.TrimEnd('/');
            Token = token.Trim();
        }

        public static bool AllowedServiceUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) return false;
            return string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) &&
                   string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";
        }

        public IEnumerator RedeemInvitation(string serviceUrl, string code, Action<string, string> done)
        {
            if (!AllowedServiceUrl(serviceUrl) || string.IsNullOrEmpty(code))
            {
                done(null, "Invitation ou adresse du laboratoire invalide");
                yield break;
            }
            var body = Json(JsonUtility.ToJson(new InvitationRequestDto { invitation_code = code }));
            using (var req = new UnityWebRequest(serviceUrl.TrimEnd('/') + "/v1/session/redeem", "POST"))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.uploadHandler = new UploadHandlerRaw(body);
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 30;
                yield return req.SendWebRequest();
                var response = req.result == UnityWebRequest.Result.Success
                    ? JsonUtility.FromJson<SessionDto>(req.downloadHandler.text) : null;
                done(string.IsNullOrEmpty(response?.token) ? null : response.token,
                    string.IsNullOrEmpty(response?.token) ? Error(req) ?? "Session non créée" : null);
            }
        }

        private UnityWebRequest Request(string method, string path, byte[] body = null, string contentType = null, string key = null)
        {
            if (!Configured) throw new InvalidOperationException("Connexion au laboratoire non configurée");
            var req = new UnityWebRequest(BaseUrl + path, method);
            req.downloadHandler = new DownloadHandlerBuffer();
            if (body != null) req.uploadHandler = new UploadHandlerRaw(body);
            if (contentType != null) req.SetRequestHeader("Content-Type", contentType);
            req.SetRequestHeader("Authorization", "Bearer " + Token);
            if (!string.IsNullOrEmpty(key)) req.SetRequestHeader("Idempotency-Key", key);
            req.timeout = 30;
            return req;
        }

        private static string PathId(string id) => Uri.EscapeDataString(id ?? "");
        private static byte[] Json(string json) => Encoding.UTF8.GetBytes(json);

        public IEnumerator GetCapabilities(Action<CapabilitiesDto, string, long> done)
        {
            using (var req = Request("GET", "/v1/capabilities"))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? JsonUtility.FromJson<CapabilitiesDto>(req.downloadHandler.text) : null,
                    Error(req), req.responseCode);
            }
        }

        public IEnumerator Allocate(string key, Action<ParchmentDto, string> done)
        {
            using (var req = Request("POST", "/v1/parchments", Json("{\"layout_version\":\"free_canvas_v2\"}"), "application/json", key))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? JsonUtility.FromJson<ParchmentDto>(req.downloadHandler.text) : null, Error(req));
            }
        }

        public IEnumerator List(Action<ParchmentPageDto, string> done)
        {
            using (var req = Request("GET", "/v1/parchments?limit=50"))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? JsonUtility.FromJson<ParchmentPageDto>(req.downloadHandler.text) : null, Error(req));
            }
        }

        public IEnumerator Begin(ParchmentRecord record, string firstBlockHash, Action<string> done)
        {
            var body = Json("{\"first_sequence\":1,\"first_block_sha256\":\"" + firstBlockHash + "\"}");
            using (var req = Request("POST", "/v1/parchments/" + PathId(record.parchment_id) + "/begin", body, "application/json", record.begin_key))
            {
                yield return req.SendWebRequest();
                done(Error(req));
            }
        }

        public IEnumerator GetArtifact(string id, Action<byte[], string, string> done)
        {
            using (var req = Request("GET", "/v1/artifacts/" + PathId(id)))
            {
                yield return req.SendWebRequest();
                var hash = req.GetResponseHeader("X-Content-SHA256");
                var bytes = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
                var error = Error(req);
                if (error == null && !string.IsNullOrEmpty(hash) && ParchmentStore.Hash(bytes) != hash.ToLowerInvariant()) error = "Empreinte d'artefact invalide";
                done(error == null ? bytes : null, hash, error);
            }
        }

        public IEnumerator Upload(ParchmentRecord record, DrawingCaptureDto capture, byte[] drawing, byte[] ink, byte[] journal, Action<JobDto, string, string> done)
        {
            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("capture", JsonUtility.ToJson(capture), "application/json"),
                new MultipartFormFileSection("drawing", drawing, "drawing.png", "image/png"),
                new MultipartFormFileSection("ink", ink, "ink.png", "image/png"),
                new MultipartFormFileSection("journal", journal, "journal.jsonl.gz", "application/gzip")
            };
            using (var req = UnityWebRequest.Post(BaseUrl + "/v1/parchments/" + PathId(record.parchment_id) + "/capture", sections))
            {
                req.method = "PUT";
                req.SetRequestHeader("Authorization", "Bearer " + Token);
                req.SetRequestHeader("Idempotency-Key", record.capture_key);
                req.timeout = 60;
                yield return req.SendWebRequest();
                ApiErrorText.TryReadProblem(req.downloadHandler?.text, out var code, out _);
                done(req.result == UnityWebRequest.Result.Success ? JsonUtility.FromJson<JobDto>(req.downloadHandler.text) : null,
                    Error(req), code);
            }
        }

        public IEnumerator GetJob(string id, Action<JobDto, string> done)
        {
            using (var req = Request("GET", "/v1/jobs/" + PathId(id)))
            {
                yield return req.SendWebRequest();
                done(req.result == UnityWebRequest.Result.Success ? JsonUtility.FromJson<JobDto>(req.downloadHandler.text) : null, Error(req));
            }
        }

        public IEnumerator ResumeJob(string id, string key, Action<JobDto, string> done)
        {
            using (var req = Request("POST", "/v1/jobs/" + PathId(id) + "/resume",
                       Json("{}"), "application/json", key))
            {
                yield return req.SendWebRequest();
                JobDto response = null;
                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 202)
                {
                    try { response = JsonUtility.FromJson<JobDto>(req.downloadHandler.text); }
                    catch (ArgumentException) { }
                }
                done(response, response == null ? Error(req) ?? "Reprise non confirmée par le laboratoire" : null);
            }
        }

        public IEnumerator SendInterpretationFeedback(string jobId, string descriptionHash,
            string correction, string key, Action<InterpretationFeedbackDto, string> done)
        {
            var payload = new InterpretationFeedbackRequestDto
            {
                description_sha256 = descriptionHash, correction = correction
            };
            using (var req = Request("POST", "/v1/jobs/" + PathId(jobId) + "/interpretation-feedback",
                       Json(JsonUtility.ToJson(payload)), "application/json", key))
            {
                yield return req.SendWebRequest();
                InterpretationFeedbackDto response = null;
                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 201)
                {
                    try { response = JsonUtility.FromJson<InterpretationFeedbackDto>(req.downloadHandler.text); }
                    catch (ArgumentException) { }
                }
                done(response, response == null ? Error(req) ?? "Retour non enregistré par le laboratoire" : null);
            }
        }

        public IEnumerator GetSpell(string id, Action<byte[], string> done)
        {
            using (var req = Request("GET", "/v1/spells/" + PathId(id)))
            {
                yield return req.SendWebRequest();
                var bytes = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
                var hash = req.GetResponseHeader("X-Content-SHA256");
                var error = Error(req);
                if (error == null && (string.IsNullOrEmpty(hash) || ParchmentStore.Hash(bytes) != hash.ToLowerInvariant())) error = "Empreinte du sort absente ou invalide";
                done(error == null ? bytes : null, error);
            }
        }

        private static string Error(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.Success) return null;
            return ApiErrorText.Display(req.downloadHandler?.text, req.error);
        }
    }
}
