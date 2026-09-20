using System;
using System.Collections.Generic;
using UnityEngine;

namespace Palimpseste.Game.SpellRuntime
{
    /// <summary>
    /// Deforms the existing decorative spectral cloth along the actual flight
    /// history. It owns no mesh, collider, material, gameplay state or callback.
    /// The composition continues to own and release its procedural meshes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpectralWakeMotion : MonoBehaviour
    {
        public const int MaximumHistorySamples = 160;
        public const int MaximumVeils = 6;
        private const float MinimumSampleDistance = .04f;

        private struct FlightPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float distance;
        }

        private sealed class VeilState
        {
            public Mesh mesh;
            public Transform transform;
            public Vector3[] restVertices;
            public Vector3[] restNormals;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public int[] vertexRows;
            public float[] rowZ;
            public float[] rowDistance;
            public float[] rowFraction;
            public Vector3[] rowCenters;
            public Quaternion[] rowRotations;
        }

        private readonly FlightPose[] history = new FlightPose[MaximumHistorySamples];
        private VeilState[] veils = Array.Empty<VeilState>();
        private Transform carrier;
        private Transform energyRoot;
        private int newest, historyCount;
        private float physicalScale, maximumDistance, born;
        private bool initialized;

        public void Initialize(Transform carrier, Transform energyRoot,
            IReadOnlyList<Transform> veils, float physicalScale)
        {
            if (carrier == null || energyRoot == null) throw new ArgumentNullException("Spectral wake transforms");
            if (veils == null || veils.Count == 0 || veils.Count > MaximumVeils)
                throw new ArgumentOutOfRangeException(nameof(veils));
            if (!Finite(physicalScale) || physicalScale <= 0 || physicalScale > 4)
                throw new ArgumentOutOfRangeException(nameof(physicalScale));

            this.carrier = carrier;
            this.energyRoot = energyRoot;
            this.physicalScale = physicalScale;
            this.veils = new VeilState[veils.Count];
            maximumDistance = 0;
            for (var i = 0; i < veils.Count; i++)
                this.veils[i] = CacheVeil(veils[i]);

            // Seed the real initial orientation, never Quaternion.identity.
            // The first few frames therefore attach to the hood correctly.
            newest = 0; historyCount = 1; born = Time.time;
            history[0] = new FlightPose {
                position = carrier.TransformPoint(energyRoot.localPosition),
                rotation = carrier.rotation, distance = 0
            };
            initialized = true;
            Deform(history[0], 0);
        }

        private VeilState CacheVeil(Transform veil)
        {
            if (veil == null) throw new ArgumentNullException(nameof(veil));
            var filter = veil.GetComponent<MeshFilter>();
            var mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null) throw new ArgumentException("A spectral veil requires its owned mesh");
            var rest = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            if (rest.Length == 0 || rest.Length > 5000 || normals.Length != rest.Length || uv.Length != rest.Length)
                throw new ArgumentException("A spectral veil requires bounded vertices, normals and UVs");

            // UV.x identifies a longitudinal row, even if the input mesh has
            // a different tessellation. This lookup is built once per cast.
            var rowLookup = new Dictionary<float, int>();
            var rowSums = new List<float>();
            var rowCounts = new List<int>();
            var rowFractions = new List<float>();
            var vertexRows = new int[rest.Length];
            for (var i = 0; i < rest.Length; i++)
            {
                if (!Finite(rest[i]) || !Finite(normals[i]) || !Finite(uv[i].x) || !Finite(uv[i].y))
                    throw new ArgumentException("Spectral veil data must be finite");
                if (!rowLookup.TryGetValue(uv[i].x, out var row))
                {
                    row = rowSums.Count;
                    rowLookup.Add(uv[i].x, row);
                    rowSums.Add(0); rowCounts.Add(0); rowFractions.Add(Mathf.Clamp01(uv[i].x));
                }
                vertexRows[i] = row;
                rowSums[row] += rest[i].z;
                rowCounts[row]++;
            }
            var rowZ = new float[rowSums.Count];
            var rowDistance = new float[rowSums.Count];
            for (var row = 0; row < rowZ.Length; row++)
            {
                rowZ[row] = rowSums[row] / rowCounts[row];
                rowDistance[row] = Mathf.Max(0, -rowZ[row] * physicalScale);
                maximumDistance = Mathf.Max(maximumDistance, rowDistance[row]);
            }
            mesh.MarkDynamic();
            return new VeilState {
                mesh = mesh, transform = veil, restVertices = rest, restNormals = normals,
                vertices = new Vector3[rest.Length], normals = new Vector3[rest.Length],
                uv = uv, vertexRows = vertexRows, rowZ = rowZ, rowDistance = rowDistance,
                rowFraction = rowFractions.ToArray(), rowCenters = new Vector3[rowZ.Length],
                rowRotations = new Quaternion[rowZ.Length]
            };
        }

        private void LateUpdate()
        {
            if (!initialized || carrier == null || energyRoot == null) return;
            var position = carrier.TransformPoint(energyRoot.localPosition);
            var rotation = carrier.rotation;
            if (!Finite(position) || !Finite(rotation)) return;
            var displacement = Vector3.Distance(history[newest].position, position);
            var current = new FlightPose {
                position = position, rotation = rotation,
                distance = history[newest].distance + displacement
            };
            if (displacement >= MinimumSampleDistance)
            {
                newest = (newest + 1) % MaximumHistorySamples;
                history[newest] = current;
                historyCount = Mathf.Min(historyCount + 1, MaximumHistorySamples);
            }
            var oldest = (newest - historyCount + 1 + MaximumHistorySamples) % MaximumHistorySamples;
            var availableLength = Mathf.Max(0, current.distance - history[oldest].distance);
            Deform(current, availableLength);
        }

        private void Deform(FlightPose current, float availableLength)
        {
            var growth = maximumDistance <= .0001f ? 0 : Mathf.Clamp01(availableLength / maximumDistance);
            var age = Time.time - born;
            for (var veilIndex = 0; veilIndex < veils.Length; veilIndex++)
            {
                var veil = veils[veilIndex];
                if (veil.mesh == null || veil.transform == null) continue;
                for (var row = 0; row < veil.rowZ.Length; row++)
                {
                    var distance = Mathf.Min(availableLength, veil.rowDistance[row] * growth);
                    SamplePast(current, distance, out var center, out var basis);
                    var fraction = veil.rowFraction[row];
                    var tail = Mathf.SmoothStep(0, 1, fraction) * growth;
                    // Distinct slow phases travel down each veil. The spine
                    // retains the actual past path instead of rigidly turning
                    // the entire cloak whenever the carrier changes direction.
                    var phase = age * 2.35f - distance * 1.65f + veilIndex * 1.73f;
                    var sway = Mathf.Sin(phase) * (.045f + fraction * .11f) * tail * physicalScale;
                    var lift = Mathf.Sin(phase * .73f + veilIndex * .83f) * (.035f + fraction * .12f) * tail * physicalScale;
                    veil.rowCenters[row] = center + basis * Vector3.right * sway + Vector3.up * lift;
                    veil.rowRotations[row] = basis;
                }

                // All arrays are reused. Veils are normally identity children
                // of energyRoot; using their matrix also handles an explicit
                // decorative offset without changing the carrier transform.
                var worldToLocal = veil.transform.worldToLocalMatrix;
                var localRotation = Quaternion.Inverse(veil.transform.rotation);
                var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                for (var vertex = 0; vertex < veil.restVertices.Length; vertex++)
                {
                    var row = veil.vertexRows[vertex];
                    var rest = veil.restVertices[vertex];
                    var sideGrowth = 1 - veil.rowFraction[row] * .62f * (1 - growth);
                    var offset = new Vector3(rest.x * sideGrowth, rest.y * sideGrowth,
                        (rest.z - veil.rowZ[row]) * growth) * physicalScale;
                    var world = veil.rowCenters[row] + veil.rowRotations[row] * offset;
                    var position = worldToLocal.MultiplyPoint3x4(world);
                    // The cached normal carries the original folded cloth
                    // detail while the historical basis bends its broad shape.
                    var normal = localRotation * (veil.rowRotations[row] * veil.restNormals[vertex]);
                    veil.vertices[vertex] = position;
                    veil.normals[vertex] = normal.sqrMagnitude > .000001f ? normal.normalized : Vector3.up;
                    minimum = Vector3.Min(minimum, position);
                    maximum = Vector3.Max(maximum, position);
                }
                veil.mesh.SetVertices(veil.vertices);
                veil.mesh.SetNormals(veil.normals);
                var bounds = new Bounds((minimum + maximum) * .5f, maximum - minimum);
                // Keep shader-scale flutter within visible culling bounds.
                bounds.Expand(.65f * physicalScale);
                veil.mesh.bounds = bounds;
            }
        }

        private void SamplePast(FlightPose current, float distance,
            out Vector3 position, out Quaternion rotation)
        {
            var target = current.distance - distance;
            var upper = current;
            for (var offset = 0; offset < historyCount; offset++)
            {
                var index = (newest - offset + MaximumHistorySamples) % MaximumHistorySamples;
                var lower = history[index];
                if (target >= lower.distance)
                {
                    var segment = upper.distance - lower.distance;
                    var t = segment <= .000001f ? 1 : Mathf.Clamp01((target - lower.distance) / segment);
                    position = Vector3.LerpUnclamped(lower.position, upper.position, t);
                    rotation = Quaternion.SlerpUnclamped(lower.rotation, upper.rotation, t);
                    return;
                }
                upper = lower;
            }
            position = upper.position;
            rotation = upper.rotation;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);

        private void OnDestroy()
        {
            initialized = false;
            veils = Array.Empty<VeilState>();
            carrier = null; energyRoot = null;
        }
    }
}
