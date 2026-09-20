using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Palimpseste.Game.Drawing;
using UnityEngine;

namespace Palimpseste.Game.Library
{
    [Serializable]
    public sealed class ParchmentRecord
    {
        public string local_id;
        public string parchment_id;
        public string owner_id;
        public string state = "blank";
        public string job_id;
        public string spell_id;
        public string description_artifact_id;
        public string description_sha256;
        public string feedback_key;
        public string feedback_description_sha256;
        public string feedback_correction;
        public string feedback_id;
        public bool feedback_sent;
        public string resume_stage;
        public string last_job_message;
        public bool server_issued;
        public bool requires_description_before_lab;
        public string closed_reason;
        public string allocation_key;
        public string begin_key;
        public string capture_key;
        public string reference_sha256;
        public string reference_artifact_id;
        public string capture_id;
        public string layout_version;
        public string raster_version;
        public string created_at;
        public int sequence;
        public string last_hash = new string('0', 64);
        public bool needs_begin;
        public bool needs_capture;
    }

    [Serializable]
    internal sealed class JournalPoint
    {
        public int sequence;
        public string op;
        public int x;
        public int y;
        public int brush;
        public int r;
        public int g;
        public int b;
        public int a;
        public int diameter_milli;
        public int pressure_milli;
        public string previous_sha256;
        public string sha256;
    }

    public sealed class ParchmentStore
    {
        private readonly string root;

        public ParchmentStore()
        {
            root = Path.Combine(Application.persistentDataPath, "Palimpseste", "parchments");
            Directory.CreateDirectory(root);
        }

        public string DirectoryFor(ParchmentRecord record) => Path.Combine(root, record.local_id);

        public ParchmentRecord Create(string parchmentId)
        {
            var record = new ParchmentRecord
            {
                local_id = Guid.NewGuid().ToString("N"),
                parchment_id = parchmentId,
                allocation_key = Guid.NewGuid().ToString("N"),
                begin_key = Guid.NewGuid().ToString("N"),
                capture_key = Guid.NewGuid().ToString("N"),
                capture_id = Guid.NewGuid().ToString("N"),
                layout_version = "free_canvas_v2",
                raster_version = DrawingCanvas.RasterVersion,
                created_at = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            };
            Save(record);
            return record;
        }

        public List<ParchmentRecord> LoadAll()
        {
            var result = new List<ParchmentRecord>();
            foreach (var dir in Directory.GetDirectories(root))
            {
                var path = Path.Combine(dir, "state.json");
                if (!File.Exists(path)) continue;
                try
                {
                    var item = JsonUtility.FromJson<ParchmentRecord>(File.ReadAllText(path, Encoding.UTF8));
                    if (item != null && item.local_id == Path.GetFileName(dir)) result.Add(item);
                }
                catch (Exception ex) { Debug.LogError("Parchemin local illisible : " + ex.Message); }
            }
            result.Sort((a, b) => string.CompareOrdinal(b.created_at, a.created_at));
            return result;
        }

        public void Save(ParchmentRecord record)
        {
            var dir = DirectoryFor(record);
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, "state.json");
            var temp = target + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(record, true));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
        }

        public void Append(ParchmentRecord record, string op, Vector2 point, BrushStyle brush, Color32 color, float diameter, float pressure)
        {
            if (record.state == "capture_corrupted") throw new InvalidOperationException("Journal endommagé");
            var entry = new JournalPoint
            {
                sequence = record.sequence + 1,
                op = op,
                x = Mathf.Clamp(Mathf.RoundToInt(point.x * 65535f / 1023f), 0, 65535),
                y = Mathf.Clamp(Mathf.RoundToInt(point.y * 65535f / 1023f), 0, 65535),
                brush = (int)brush,
                r = color.r, g = color.g, b = color.b, a = color.a,
                diameter_milli = Mathf.RoundToInt(diameter * 1000f),
                pressure_milli = Mathf.RoundToInt(pressure * 1000f),
                previous_sha256 = record.last_hash
            };
            entry.sha256 = Hash(Encoding.UTF8.GetBytes(Canonical(entry)));
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(entry) + "\n");
            var path = Path.Combine(DirectoryFor(record), "journal.jsonl");
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            record.sequence = entry.sequence;
            record.last_hash = entry.sha256;
            if (op == "down" || op == "move") record.state = "writing";
            Save(record);
        }

        public DrawingCanvas Replay(ParchmentRecord record)
        {
            // Records created before free_canvas_v2 have no raster_version field.
            // They must keep the historical circular clipping and region locks when replayed.
            var raster = string.IsNullOrEmpty(record.raster_version)
                ? DrawingCanvas.LegacyRasterVersion : record.raster_version;
            if (raster != DrawingCanvas.RasterVersion && raster != DrawingCanvas.LegacyRasterVersion)
            {
                record.state = "capture_corrupted";
                Save(record);
                Debug.LogError("Version de journal de parchemin inconnue : " + raster);
                return new DrawingCanvas();
            }
            var canvas = new DrawingCanvas(raster == DrawingCanvas.LegacyRasterVersion);
            var path = Path.Combine(DirectoryFor(record), "journal.jsonl");
            if (!File.Exists(path)) return canvas;
            var expected = new string('0', 64);
            var sequence = 0;
            try
            {
                var bytes = File.ReadAllBytes(path);
                var completeLength = Array.LastIndexOf(bytes, (byte)'\n') + 1;
                if (bytes.Length > 0 && completeLength == 0) throw new InvalidDataException("Aucune entrée durable du journal");
                if (bytes.Length != completeLength && record.state != "blank" && record.state != "writing")
                    throw new InvalidDataException("Journal clos tronqué");
                var checkpointHash = record.sequence == 0 ? expected : null;
                using (var reader = new StringReader(Encoding.UTF8.GetString(bytes, 0, completeLength)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var point = JsonUtility.FromJson<JournalPoint>(line);
                        if (point == null || point.sequence != ++sequence || point.previous_sha256 != expected ||
                            point.sha256 != Hash(Encoding.UTF8.GetBytes(Canonical(point))))
                            throw new InvalidDataException("Séquence ou empreinte du journal invalide");
                        var p = new Vector2(point.x * 1023f / 65535f, point.y * 1023f / 65535f);
                        var color = new Color32((byte)point.r, (byte)point.g, (byte)point.b, (byte)point.a);
                        var style = (BrushStyle)point.brush;
                        var d = point.diameter_milli / 1000f;
                        var pressure = point.pressure_milli / 1000f;
                        switch (point.op)
                        {
                            case "down": canvas.Begin(p, style, color, d, pressure); break;
                            case "move": canvas.Move(p, style, color, d, pressure); break;
                            case "up": canvas.End(); break;
                            case "close": canvas.Close(); break;
                            default: throw new InvalidDataException("Opération inconnue");
                        }
                        expected = point.sha256;
                        if (sequence == record.sequence) checkpointHash = expected;
                    }
                }
                if (record.sequence > sequence || checkpointHash != record.last_hash)
                    throw new InvalidDataException("Checkpoint incohérent");
                if (bytes.Length != completeLength)
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        stream.SetLength(completeLength);
                        stream.Flush(true);
                    }
                }
                var checkpointAdvanced = record.sequence != sequence;
                record.sequence = sequence;
                record.last_hash = expected;
                if (record.server_issued && sequence > 0 && string.IsNullOrEmpty(record.job_id) &&
                    (record.state == "blank" || record.state == "writing" || record.needs_capture))
                    record.needs_begin = true; // Idempotent if Begin already reached the server.
                if ((record.state == "blank" || record.state == "writing") && canvas.Engaged)
                {
                    if (canvas.UsesLegacyRegions)
                    {
                        if (!canvas.Closed)
                        {
                            canvas.Close();
                            Append(record, "close", Vector2.zero, BrushStyle.Solid, new Color32(0, 0, 0, 0), 0, 0);
                        }
                        record.closed_reason = "crash_recovered";
                        record.state = "capture_pending";
                        record.needs_capture = true;
                    }
                    else
                    {
                        // A free-canvas drawing is committed only by the explicit finish button.
                        // A crashed gesture is ended durably so the player can add more strokes.
                        if (canvas.Closed)
                        {
                            record.state = "capture_pending";
                            record.closed_reason = "user_finished";
                            record.needs_capture = true;
                        }
                        else
                        {
                            record.state = "writing";
                            if (canvas.IsDrawing)
                            {
                                canvas.End();
                                Append(record, "up", Vector2.zero, BrushStyle.Solid, new Color32(0, 0, 0, 0), 0, 0);
                            }
                        }
                    }
                    Save(record);
                }
                else
                {
                    if (checkpointAdvanced) Save(record);
                    if (record.state != "blank" && canvas.Engaged) canvas.Close();
                }
            }
            catch (Exception ex)
            {
                record.state = "capture_corrupted";
                Save(record);
                Debug.LogError("Journal de parchemin corrompu : " + ex);
            }
            return canvas;
        }

        public byte[] JournalGzip(ParchmentRecord record)
        {
            var path = Path.Combine(DirectoryFor(record), "journal.jsonl");
            var bytes = File.ReadAllBytes(path);
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) gzip.Write(bytes, 0, bytes.Length);
                return output.ToArray();
            }
        }

        private static string Canonical(JournalPoint p) => string.Join("|", p.sequence, p.op, p.x, p.y, p.brush, p.r, p.g, p.b, p.a, p.diameter_milli, p.pressure_milli, p.previous_sha256);

        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var chars = new char[hash.Length * 2];
                const string hex = "0123456789abcdef";
                for (var i = 0; i < hash.Length; i++) { chars[i * 2] = hex[hash[i] >> 4]; chars[i * 2 + 1] = hex[hash[i] & 15]; }
                return new string(chars);
            }
        }
    }
}
