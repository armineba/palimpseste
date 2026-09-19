using System;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    // Opt-in reader measurement of Unity frame-loop throughput, not display presents,
    // fixed simulation ticks or an Editor profiler estimate.
    public sealed class LabPerformanceProbe : MonoBehaviour
    {
        private SpellLab lab;
        private double started, lastSample;
        private int lastFrame;

        public static void AttachIfRequested(SpellLab lab)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-palimpseste-b28") >= 0)
                lab.gameObject.AddComponent<LabPerformanceProbe>();
        }

        private void Start()
        {
            lab = GetComponent<SpellLab>();
            started = lastSample = Time.realtimeSinceStartupAsDouble;
            lastFrame = Time.frameCount;
            Debug.Log("PALIMPSESTE_B28 begin resolution=" + Screen.width + "x" + Screen.height);
        }

        private void Update()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            var seconds = now - lastSample;
            if (seconds < 10) return;
            var frames = Time.frameCount - lastFrame;
            Debug.Log("PALIMPSESTE_B28 elapsed_s=" + (now - started).ToString("F1") +
                " frames=" + frames + " window_s=" + seconds.ToString("F2") +
                " fps=" + (frames / seconds).ToString("F1") +
                " active=" + lab.ActiveCarriers + " hits=" + lab.Hits +
                " managed_bytes=" + GC.GetTotalMemory(false));
            lastSample = now;
            lastFrame = Time.frameCount;
        }
    }
}
