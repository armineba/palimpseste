using System;
using System.IO;
using Palimpseste.Game.Service;

namespace Palimpseste.Game.Library
{
    public static class DescriptionCache
    {
        private const string FileName = "description.json";

        public static bool TryLoad(ParchmentRecord record, string directory, out SpellDescriptionView view)
        {
            view = null;
            if (record == null || !ValidArtifactId(record.description_artifact_id) ||
                !ValidHash(record.description_sha256)) return false;
            try
            {
                var path = Path.Combine(directory, FileName);
                if (!File.Exists(path)) return false;
                var bytes = File.ReadAllBytes(path);
                return string.Equals(ParchmentStore.Hash(bytes), record.description_sha256,
                           StringComparison.OrdinalIgnoreCase) && SpellDescriptionView.TryRead(bytes, out view);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        }

        public static bool TrySave(ParchmentRecord record, string directory, string artifactId,
            string responseHash, byte[] utf8, out SpellDescriptionView view)
        {
            view = null;
            if (record == null || utf8 == null || !ValidArtifactId(artifactId) || !ValidHash(responseHash) ||
                !string.Equals(ParchmentStore.Hash(utf8), responseHash, StringComparison.OrdinalIgnoreCase) ||
                !SpellDescriptionView.TryRead(utf8, out view)) return false;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(utf8, 0, utf8.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                record.description_artifact_id = artifactId;
                record.description_sha256 = responseHash.ToLowerInvariant();
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                view = null;
                return false;
            }
        }

        private static bool ValidArtifactId(string value) =>
            value != null && value.Length == 33 && value[0] == 'a' &&
            Guid.TryParseExact(value.Substring(1), "N", out _);

        private static bool ValidHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var character in value) if (!Uri.IsHexDigit(character)) return false;
            return true;
        }
    }
}
