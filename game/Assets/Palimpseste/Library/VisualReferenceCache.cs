using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Palimpseste.Game.Library
{
    public static class VisualReferenceCache
    {
        public static bool TryRead(ParchmentRecord record, string directory, out byte[] bytes)
        {
            bytes = null;
            if (record == null || !ValidId(record.visual_reference_artifact_id)) return false;
            try
            {
                var path = Path.Combine(directory, "artifacts", record.visual_reference_artifact_id);
                if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return false;
                var candidate = File.ReadAllBytes(path);
                if (!ValidPng(candidate, record.visual_reference_sha256)) return false;
                bytes = candidate;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public static bool TrySave(ParchmentRecord record, string directory, string artifactId, string hash, byte[] bytes)
        {
            if (record == null || !ValidId(artifactId) || !ValidPng(bytes, hash)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(bytes, false)) return false;
                var root = Path.Combine(directory, "artifacts");
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, artifactId);
                var temporary = path + ".pending";
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
                record.visual_reference_artifact_id = artifactId;
                record.visual_reference_sha256 = hash;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            finally { UnityEngine.Object.Destroy(texture); }
        }

        public static bool ValidPng(byte[] bytes, string hash)
        {
            if (bytes == null || bytes.Length < 33 || bytes.Length > 8 * 1024 * 1024 ||
                hash == null || !Regex.IsMatch(hash, "^[0-9a-f]{64}$") || ParchmentStore.Hash(bytes) != hash) return false;
            var magic = new byte[] {137,80,78,71,13,10,26,10};
            for (var i=0;i<magic.Length;i++) if (bytes[i] != magic[i]) return false;
            if (bytes[12] != 'I' || bytes[13] != 'H' || bytes[14] != 'D' || bytes[15] != 'R') return false;
            long width=0,height=0;
            for (var i=0;i<4;i++) { width=(width<<8)+bytes[16+i]; height=(height<<8)+bytes[20+i]; }
            return width >= 512 && width <= 2048 && height >= 512 && height <= 2048;
        }

        private static bool ValidId(string id) => id != null && Regex.IsMatch(id, "^a[a-f0-9]{32}$");
    }
}
