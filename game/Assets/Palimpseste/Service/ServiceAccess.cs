using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Palimpseste.Game.Library;

namespace Palimpseste.Game.Service
{
    public static class ServiceAccess
    {
        public static bool TryLoadServiceUrl(string file, out string url)
        {
            url = null;
            try
            {
                if (!File.Exists(file)) return false;
                var root = JObject.Parse(File.ReadAllText(file));
                if (root.Count != 1 || root["service_url"]?.Type != JTokenType.String) return false;
                var value = root.Value<string>("service_url")?.TrimEnd('/');
                if (!LabApi.AllowedServiceUrl(value)) return false;
                url = value;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            { return false; }
        }

        public static bool TryLoadInvitation(string file, string expectedServiceUrl, out string serviceUrl, out string code)
        {
            serviceUrl = null;
            code = null;
            try
            {
                if (!File.Exists(file)) return false;
                var root = JObject.Parse(File.ReadAllText(file));
                if (root.Count != 2 || root["service_url"]?.Type != JTokenType.String ||
                    root["invitation_code"]?.Type != JTokenType.String) return false;
                var url = root.Value<string>("service_url")?.TrimEnd('/');
                if (!LabApi.AllowedServiceUrl(url) ||
                    (!string.IsNullOrEmpty(expectedServiceUrl) &&
                     !string.Equals(url, expectedServiceUrl, StringComparison.OrdinalIgnoreCase))) return false;
                var value = root.Value<string>("invitation_code");
                if (value == null || value.Length != 64) return false;
                foreach (var c in value)
                    if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') &&
                        !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
                serviceUrl = url;
                code = value;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            { return false; }
        }

        public static void SaveServiceUrl(string file, string serviceUrl)
        {
            if (!LabApi.AllowedServiceUrl(serviceUrl)) throw new ArgumentException("Adresse de service invalide");
            var directory = Path.GetDirectoryName(file);
            Directory.CreateDirectory(directory);
            var temp = file + ".tmp";
            File.WriteAllText(temp, new JObject { ["service_url"] = serviceUrl }.ToString(Newtonsoft.Json.Formatting.None),
                new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(temp, file, null);
            else File.Move(temp, file);
        }

        public static bool TryLoadPrincipalId(string file, string serviceUrl, string token, out string principalId)
        {
            principalId = null;
            if (!LabApi.AllowedServiceUrl(serviceUrl) || string.IsNullOrEmpty(token)) return false;
            try
            {
                if (!File.Exists(file)) return false;
                var root = JObject.Parse(File.ReadAllText(file, Encoding.UTF8));
                if (root.Count != 3 || root["service_url"]?.Type != JTokenType.String ||
                    root["principal_id"]?.Type != JTokenType.String || root["token_sha256"]?.Type != JTokenType.String)
                    return false;
                var value = root.Value<string>("principal_id");
                if (!ValidPrincipalId(value) ||
                    !string.Equals(root.Value<string>("service_url"), serviceUrl, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(root.Value<string>("token_sha256"), TokenHash(token), StringComparison.OrdinalIgnoreCase))
                    return false;
                principalId = value;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            { return false; }
        }

        public static void SavePrincipalId(string file, string serviceUrl, string token, string principalId)
        {
            if (!LabApi.AllowedServiceUrl(serviceUrl) || string.IsNullOrEmpty(token) || !ValidPrincipalId(principalId))
                throw new ArgumentException("Identité de lecteur invalide");
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var temp = file + ".tmp";
            File.WriteAllText(temp, new JObject
            {
                ["service_url"] = serviceUrl,
                ["principal_id"] = principalId,
                ["token_sha256"] = TokenHash(token)
            }.ToString(Newtonsoft.Json.Formatting.None), new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(temp, file, null);
            else File.Move(temp, file);
        }

        private static bool ValidPrincipalId(string value)
        {
            return Guid.TryParseExact(value, "N", out _);
        }

        private static string TokenHash(string token) => ParchmentStore.Hash(Encoding.UTF8.GetBytes(token));
    }
}
