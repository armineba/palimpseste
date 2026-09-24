using System;
using System.Collections.Generic;
using Palimpseste.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace Palimpseste.Game.SpellRuntime
{
    // Canonical geometry is constructed once. Animation never rebuilds topology.
    // Branched entities are extracted from ONE smooth distance field; the input
    // swept segments are construction mathematics, never separate renderers.
    internal static class CanonicalCoreGeometryV2
    {
        internal sealed class Result
        {
            public Mesh mesh;
            public int[] entityIds;
            public Vector3[] entityCentres;
        }

        public static Vector3 Cm(int[] value) => new Vector3(value[0], value[1], value[2]) * .01f;

        public static Result Build(StructuralCoreV2 core)
        {
            if (core == null || core.control_points == null || core.control_points.Count < 2)
                throw new InvalidOperationException("V2 requires a canonical control path");
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var entities = new List<int>(); var centres = new List<Vector3>();
            switch (core.kind)
            {
                case "swept_tube": case "beam": case "ribbon":
                    Sweep(core.control_points, core.longitudinal_segments, core.radial_segments,
                        core.kind == "ribbon", Vector3.zero, vertices, uv, triangles); break;
                case "radial_volume":
                    // A closed mass follows its declared centreline and section
                    // profiles. It is not a Y-axis ellipsoid inferred from size.
                    Sweep(core.control_points, core.longitudinal_segments, core.radial_segments,
                        false, Vector3.zero, vertices, uv, triangles, true); break;
                case "vortex_surface":
                    Lathe(core, vertices, uv, triangles); break;
                case "planar_field":
                    Planar(core, vertices, uv, triangles); break;
                case "controlled_swarm":
                    for (var entity = 0; entity < core.entity_count; entity++)
                    {
                        var angle = entity * Mathf.PI * 2f / core.entity_count;
                        var centre = new Vector3(Mathf.Cos(angle) * core.size_cm[0] * .005f,
                            Mathf.Sin(angle * 2f) * core.size_cm[1] * .002f,
                            Mathf.Sin(angle) * core.size_cm[2] * .005f);
                        centres.Add(centre); var first = vertices.Count;
                        Sweep(core.control_points, core.longitudinal_segments, core.radial_segments,
                            false, centre, vertices, uv, triangles);
                        for (var i = first; i < vertices.Count; i++) entities.Add(entity);
                    }
                    break;
                case "branched_surface":
                    FusedBranches(core, vertices, uv, triangles); break;
                default: throw new InvalidOperationException("Unsupported V2 structural core: " + core.kind);
            }
            if (vertices.Count == 0 || vertices.Count > 65536 || triangles.Count == 0)
                throw new InvalidOperationException("V2 structural mesh exceeds the bounded geometry budget");
            var mesh = new Mesh { name = "V2 canonical " + core.kind, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.MarkDynamic();
            return new Result { mesh = mesh, entityIds = entities.ToArray(), entityCentres = centres.ToArray() };
        }

        public static Vector3 Path(IList<CoreControlPointV2> path, float t)
        {
            var scaled = Mathf.Clamp01(t) * (path.Count - 1);
            var index = Mathf.Min(path.Count - 2, Mathf.FloorToInt(scaled)); var u = scaled - index;
            var a = Cm(path[Mathf.Max(0, index - 1)].position_cm); var b = Cm(path[index].position_cm);
            var c = Cm(path[index + 1].position_cm); var d = Cm(path[Mathf.Min(path.Count - 1, index + 2)].position_cm);
            return .5f * ((2f * b) + (-a + c) * u + (2f * a - 5f * b + 4f * c - d) * u * u +
                (-a + 3f * b - 3f * c + d) * u * u * u);
        }

        private static float Profile(IList<CoreControlPointV2> points, float t, bool width)
        {
            var scaled = Mathf.Clamp01(t) * (points.Count - 1); var index = Mathf.Min(points.Count - 2, Mathf.FloorToInt(scaled));
            var a = width ? points[index].width_cm : points[index].radius_cm;
            var b = width ? points[index + 1].width_cm : points[index + 1].radius_cm;
            return Mathf.Max(.002f, Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, scaled - index)) * .01f);
        }

        private static void Sweep(IList<CoreControlPointV2> path, int longitudinal, int radial, bool ribbon,
            Vector3 offset, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, bool closedMass = false)
        {
            var start = vertices.Count; var sides = ribbon ? 2 : radial + 1; var previousRight = Vector3.right;
            for (var row = 0; row <= longitudinal; row++)
            {
                var t = row / (float)longitudinal; var centre = Path(path, t);
                var tangent = (Path(path, Mathf.Min(1, t + .001f)) - Path(path, Mathf.Max(0, t - .001f))).normalized;
                if (tangent.sqrMagnitude < .5f) tangent = Vector3.forward;
                var right = Vector3.ProjectOnPlane(previousRight, tangent).normalized;
                if (right.sqrMagnitude < .5f) right = Vector3.Cross(tangent, Vector3.up).normalized;
                if (right.sqrMagnitude < .5f) right = Vector3.Cross(tangent, Vector3.forward).normalized;
                var up = Vector3.Cross(tangent, right).normalized; previousRight = right;
                var radius = Profile(path, t, false); var width = Profile(path, t, true);
                // Only the terminal sections are closed. The interior radii
                // remain the values in the blueprint, including a long narrow
                // tail, asymmetry or an offset centreline. Smooth closure avoids
                // flat cap plates without inventing a spherical cap primitive.
                var closure = closedMass ? Mathf.Sin(Mathf.PI * .5f * Mathf.Clamp01(
                    Mathf.Min(t, 1 - t) * longitudinal * .5f)) : 1f;
                var horizontalRadius = closedMass ? width * .5f : radius;
                for (var side = 0; side < sides; side++)
                {
                    var s = side / (float)(sides - 1); var angle = s * Mathf.PI * 2;
                    vertices.Add(offset + centre + (ribbon ? right * ((s * 2 - 1) * width * .5f) :
                        (right * (Mathf.Cos(angle) * horizontalRadius) + up * (Mathf.Sin(angle) * radius)) * closure));
                    uv.Add(new Vector2(t, s));
                }
            }
            Grid(start, longitudinal, sides - 1, triangles);
            if (!ribbon)
            {
                // Caps share the canonical end rings. No sphere caps, joints or independent pieces.
                var capA = vertices.Count; vertices.Add(Path(path, 0) + offset); uv.Add(new Vector2(0, .5f));
                var capB = vertices.Count; vertices.Add(Path(path, 1) + offset); uv.Add(new Vector2(1, .5f));
                for (var side = 0; side < radial; side++)
                {
                    triangles.Add(capA); triangles.Add(start + side + 1); triangles.Add(start + side);
                    triangles.Add(capB); triangles.Add(start + longitudinal * sides + side);
                    triangles.Add(start + longitudinal * sides + side + 1);
                }
            }
        }

        private static void Lathe(StructuralCoreV2 core, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var rows = core.longitudinal_segments; var sides = core.radial_segments;
            var size = Cm(core.size_cm); var closed = core.kind == "radial_volume";
            for (var row = 0; row <= rows; row++)
            {
                var t = row / (float)rows;
                var profile = Profile(core.control_points, t, false);
                var maxProfile = 0f; foreach (var p in core.control_points) maxProfile = Mathf.Max(maxProfile, p.radius_cm * .01f);
                var radius = closed ? Mathf.Sin(t * Mathf.PI) * size.x * .5f * profile / Mathf.Max(.01f, maxProfile) : profile;
                var y = closed ? Mathf.Cos(t * Mathf.PI) * size.y * .5f : t * size.y;
                var axis = closed ? Vector3.zero : Path(core.control_points, t);
                for (var side = 0; side <= sides; side++)
                {
                    var u = side / (float)sides; var angle = u * Mathf.PI * 2f;
                    // A continuous vortex sheet, never a stack of torus/ring objects.
                    var spiral = closed ? 1f : 1f + .10f * Mathf.Sin(angle * 3f - t * Mathf.PI * 6f);
                    vertices.Add(new Vector3(axis.x + Mathf.Cos(angle) * radius * spiral,
                        y, axis.z + Mathf.Sin(angle) * radius * spiral * size.z / Mathf.Max(.01f, size.x)));
                    uv.Add(new Vector2(t, u));
                }
            }
            Grid(0, rows, sides, triangles);
        }

        private static void Planar(StructuralCoreV2 core, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var size = Cm(core.size_cm); var rows = Mathf.Max(2, core.longitudinal_segments / 4);
            var inner = core.inner_radius_milli / 1000f;
            // Blueprint dimensions select the support plane: a thin Y means
            // ground glyph, thin Z a forward-facing shield/portal, thin X a side plane.
            var plane = size.y <= size.x && size.y <= size.z ? 1 : size.z <= size.x ? 2 : 0;
            for (var row = 0; row <= rows; row++)
            {
                var r = Mathf.Lerp(inner, 1, row / (float)rows);
                for (var side = 0; side <= core.radial_segments; side++)
                {
                    var u = side / (float)core.radial_segments; var angle = u * Mathf.PI * 2f;
                    var cosine = Mathf.Cos(angle) * .5f * r; var sine = Mathf.Sin(angle) * .5f * r;
                    vertices.Add(plane == 1 ? new Vector3(cosine * size.x,0,sine * size.z) :
                        plane == 2 ? new Vector3(cosine * size.x,sine * size.y,0) : new Vector3(0,cosine * size.y,sine * size.z));
                    uv.Add(new Vector2(r, u));
                }
            }
            Grid(0, rows, core.radial_segments, triangles);
        }

        private static void Grid(int start, int rows, int columns, List<int> indices)
        {
            for (var row = 0; row < rows; row++) for (var column = 0; column < columns; column++)
            {
                var a = start + row * (columns + 1) + column; var b = a + columns + 1;
                indices.Add(a); indices.Add(b); indices.Add(a + 1);
                indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
            }
        }

        private struct Segment { public Vector3 a, b; public float ra, rb; }
        private static void FusedBranches(StructuralCoreV2 core, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            var segments = new List<Segment>();
            AddSegments(core.control_points, segments);
            foreach (var branch in core.branches)
            {
                var parent = branch.parent_index < 0 ? core.control_points : core.branches[branch.parent_index].control_points;
                var attachment = Path(parent, branch.parent_t_milli / 1000f);
                var first = branch.control_points[0];
                segments.Add(new Segment { a = attachment, b = Cm(first.position_cm),
                    ra = Profile(parent, branch.parent_t_milli / 1000f, false), rb = first.radius_cm * .01f });
                AddSegments(branch.control_points, segments);
            }
            var bounds = new Bounds(segments[0].a, Vector3.zero);
            foreach (var segment in segments)
            {
                var padding = Vector3.one * (Mathf.Max(segment.ra, segment.rb) * 1.4f + .03f);
                bounds.Encapsulate(segment.a - padding); bounds.Encapsulate(segment.a + padding);
                bounds.Encapsulate(segment.b - padding); bounds.Encapsulate(segment.b + padding);
            }
            const int resolution = 30; var stride = resolution + 1;
            var samples = new float[stride * stride * stride]; var positions = new Vector3[samples.Length];
            var blend = Mathf.Clamp(bounds.size.magnitude * .012f, .012f, .09f);
            for (var z = 0; z <= resolution; z++) for (var y = 0; y <= resolution; y++) for (var x = 0; x <= resolution; x++)
            {
                var index = x + stride * (y + stride * z);
                var point = bounds.min + Vector3.Scale(bounds.size, new Vector3(x, y, z) / resolution);
                positions[index] = point; var distance = 10000f;
                foreach (var segment in segments)
                {
                    var axis = segment.b - segment.a;
                    var t = Mathf.Clamp01(Vector3.Dot(point - segment.a, axis) / Mathf.Max(.000001f, axis.sqrMagnitude));
                    var field = (point - segment.a - t * axis).magnitude - Mathf.Lerp(segment.ra, segment.rb, t);
                    var h = Mathf.Max(blend - Mathf.Abs(distance - field), 0) / blend;
                    distance = Mathf.Min(distance, field) - h * h * blend * .25f;
                }
                samples[index] = distance;
            }
            var lookup = new Dictionary<long, int>();
            var tetra = new int[,] { {0,5,1,6}, {0,1,2,6}, {0,2,3,6}, {0,3,7,6}, {0,7,4,6}, {0,4,5,6} };
            var edges = new int[,] { {0,1}, {0,2}, {0,3}, {1,2}, {1,3}, {2,3} };
            var corners = new int[8]; var nodes = new int[4]; var crossing = new List<int>(4);
            for (var z = 0; z < resolution; z++) for (var y = 0; y < resolution; y++) for (var x = 0; x < resolution; x++)
            {
                var i = x + stride * (y + stride * z);
                corners[0]=i; corners[1]=i+1; corners[2]=i+1+stride; corners[3]=i+stride;
                corners[4]=i+stride*stride; corners[5]=corners[4]+1; corners[6]=corners[5]+stride; corners[7]=corners[4]+stride;
                for (var tet = 0; tet < 6; tet++)
                {
                    for (var n=0;n<4;n++) nodes[n]=corners[tetra[tet,n]];
                    crossing.Clear(); var outside = Vector3.zero; var inside = Vector3.zero; var ni=0;var no=0;
                    for(var n=0;n<4;n++) if(samples[nodes[n]]<0){inside+=positions[nodes[n]];ni++;}else{outside+=positions[nodes[n]];no++;}
                    if(ni==0||no==0)continue;
                    for(var edge=0;edge<6;edge++)
                    {
                        var a=nodes[edges[edge,0]];var b=nodes[edges[edge,1]];
                        if((samples[a]<0)==(samples[b]<0))continue;
                        var key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                        if(!lookup.TryGetValue(key,out var vertex))
                        {
                            var position=Vector3.Lerp(positions[a],positions[b], samples[a]/(samples[a]-samples[b]));
                            vertex=vertices.Count;lookup.Add(key,vertex);vertices.Add(position);
                            uv.Add(new Vector2(Mathf.InverseLerp(bounds.min.z,bounds.max.z,position.z),
                                Mathf.Atan2(position.y-bounds.center.y,position.x-bounds.center.x)/(Mathf.PI*2f)+.5f));
                        }
                        crossing.Add(vertex);
                    }
                    var normal=(outside/no-inside/ni).normalized;
                    var centre=Vector3.zero;foreach(var index in crossing)centre+=vertices[index];centre/=crossing.Count;
                    var right=(vertices[crossing[0]]-centre).normalized;var up=Vector3.Cross(normal,right);
                    crossing.Sort((a,b)=>Mathf.Atan2(Vector3.Dot(vertices[a]-centre,up),Vector3.Dot(vertices[a]-centre,right)).CompareTo(
                        Mathf.Atan2(Vector3.Dot(vertices[b]-centre,up),Vector3.Dot(vertices[b]-centre,right))));
                    for(var j=1;j<crossing.Count-1;j++){triangles.Add(crossing[0]);triangles.Add(crossing[j]);triangles.Add(crossing[j+1]);}
                }
            }
        }

        private static void AddSegments(IList<CoreControlPointV2> path, List<Segment> segments)
        {
            var count = Mathf.Min(48, (path.Count - 1) * 8);
            for (var i = 0; i < count; i++)
            {
                var a = i / (float)count; var b = (i + 1f) / count;
                segments.Add(new Segment { a = Path(path, a), b = Path(path, b), ra = Profile(path, a, false), rb = Profile(path, b, false) });
            }
        }
    }
}
