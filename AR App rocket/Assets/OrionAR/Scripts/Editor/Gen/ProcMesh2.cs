using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrionAR.Gen
{
    /// <summary>
    /// Procedural mesh library, rebuilt for visual quality.
    ///
    /// The previous version produced the faceted, obviously-generated look that gets
    /// described as "low poly". Three things fix that, and all three are here:
    ///
    ///   1. Angle-based normal smoothing. Faceting is a shading problem before it is
    ///      a triangle-count problem. Averaging normals across faces that meet below a
    ///      threshold angle makes a 48-segment cylinder read as a smooth machined
    ///      surface, while edges sharper than the threshold stay crisp.
    ///
    ///   2. Lathed profiles with explicit bevels. Real hardware has no zero-radius
    ///      edges; every corner is chamfered or filleted, and those chamfers catch a
    ///      highlight that tells the eye the surface is solid. A profile-driven lathe
    ///      makes adding them trivial.
    ///
    ///   3. Correct tangents. Without them normal maps are silently ignored, which is
    ///      where most of the surface detail now lives.
    /// </summary>
    public static class ProcMesh2
    {
        public const float DefaultSmoothAngle = 55f;

        // ================================================================
        // Lathe - the workhorse
        // ================================================================

        /// <summary>One point on a lathe profile, in (radius, height) space.</summary>
        public struct ProfilePoint
        {
            public float r;
            public float y;
            /// <summary>Force a hard edge here regardless of the smoothing angle.</summary>
            public bool sharp;
            /// <summary>Explicit UV v. Negative means derive it from arc length.</summary>
            public float v;

            public ProfilePoint(float radius, float height, bool sharpEdge = false, float vCoord = -1f)
            {
                r = radius; y = height; sharp = sharpEdge; v = vCoord;
            }
        }

        public static ProfilePoint P(float r, float y, bool sharp = false)
        {
            return new ProfilePoint(r, y, sharp);
        }

        /// <summary>
        /// Revolves a 2D profile around the Y axis.
        ///
        /// This single function replaces the old frustum / cylinder / ring / cap set,
        /// and because the profile is data it can describe flanges, steps, chamfers
        /// and fillets that those primitives could not.
        /// </summary>
        public static Mesh Lathe(string name, IList<ProfilePoint> profile, int segments = 72,
                                 float uTile = 1f, bool flipNormals = false,
                                 float smoothAngle = DefaultSmoothAngle)
        {
            segments = Mathf.Max(3, segments);
            int rings = profile.Count;
            if (rings < 2) return new Mesh { name = name };

            // Arc length along the profile drives V, so texture density stays even
            // across sections of very different lengths.
            var vCoord = new float[rings];
            float total = 0f;
            vCoord[0] = 0f;
            for (int i = 1; i < rings; i++)
            {
                total += Vector2.Distance(new Vector2(profile[i - 1].r, profile[i - 1].y),
                                          new Vector2(profile[i].r, profile[i].y));
                vCoord[i] = total;
            }
            if (total > 0.0001f)
                for (int i = 0; i < rings; i++)
                    vCoord[i] = profile[i].v >= 0f ? profile[i].v : vCoord[i] / total;

            var verts = new List<Vector3>(rings * (segments + 1));
            var uvs = new List<Vector2>(rings * (segments + 1));
            var tris = new List<int>();

            int stride = segments + 1;      // duplicate seam column for clean UVs

            for (int ring = 0; ring < rings; ring++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float t = (float)s / segments;
                    float a = t * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * profile[ring].r,
                                          profile[ring].y,
                                          Mathf.Sin(a) * profile[ring].r));
                    uvs.Add(new Vector2(t * uTile, vCoord[ring]));
                }
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                // Degenerate band (two identical rings) - used to insert a hard edge.
                if (Mathf.Approximately(profile[ring].r, profile[ring + 1].r) &&
                    Mathf.Approximately(profile[ring].y, profile[ring + 1].y))
                    continue;

                for (int s = 0; s < segments; s++)
                {
                    int a = ring * stride + s;
                    int b = a + 1;
                    int c = (ring + 1) * stride + s;
                    int d = c + 1;

                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            if (flipNormals) ReverseWinding(tris);

            var mesh = Build(name, verts, uvs, tris);
            ApplySmoothing(mesh, smoothAngle, MarkSharpRings(profile, stride, rings));
            return mesh;
        }

        /// <summary>Vertex indices that must keep hard normals.</summary>
        static HashSet<int> MarkSharpRings(IList<ProfilePoint> profile, int stride, int rings)
        {
            var sharp = new HashSet<int>();
            for (int ring = 0; ring < rings; ring++)
            {
                if (!profile[ring].sharp) continue;
                for (int s = 0; s < stride; s++) sharp.Add(ring * stride + s);
            }
            return sharp;
        }

        // ================================================================
        // Profile helpers - build the point lists that Lathe consumes
        // ================================================================

        /// <summary>Appends a chamfered corner: the detail that makes hardware read as machined.</summary>
        public static void AddBevelCorner(List<ProfilePoint> p, float r, float y,
                                          float bevel, Vector2 inDir, Vector2 outDir)
        {
            Vector2 corner = new Vector2(r, y);
            Vector2 a = corner - inDir.normalized * bevel;
            Vector2 b = corner + outDir.normalized * bevel;
            p.Add(new ProfilePoint(a.x, a.y));
            p.Add(new ProfilePoint(b.x, b.y));
        }

        /// <summary>A cylinder section with chamfers at both ends.</summary>
        public static List<ProfilePoint> CylinderProfile(float radius, float yBottom, float yTop,
                                                          float bevel = 0.03f, bool capBottom = true,
                                                          bool capTop = true)
        {
            var p = new List<ProfilePoint>();
            if (capBottom)
            {
                p.Add(P(0f, yBottom, true));
                p.Add(P(radius - bevel, yBottom, true));
                p.Add(P(radius, yBottom + bevel));
            }
            else
            {
                p.Add(P(radius, yBottom, true));
            }

            if (capTop)
            {
                p.Add(P(radius, yTop - bevel));
                p.Add(P(radius - bevel, yTop, true));
                p.Add(P(0f, yTop, true));
            }
            else
            {
                p.Add(P(radius, yTop, true));
            }
            return p;
        }

        /// <summary>
        /// Spherical cap - Orion's heat shield is a section of a 6.05 m sphere, not a
        /// cone or a disc, and the shallow curve is visible in silhouette.
        /// </summary>
        public static List<ProfilePoint> SphericalCapProfile(float discRadius, float sphereRadius,
                                                              int rings = 16, bool pointDown = true,
                                                              float rimBevel = 0.04f)
        {
            sphereRadius = Mathf.Max(sphereRadius, discRadius * 1.02f);
            float edgeDepth = sphereRadius - Mathf.Sqrt(sphereRadius * sphereRadius - discRadius * discRadius);

            var p = new List<ProfilePoint>();
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                float r = discRadius * t;
                float depth = sphereRadius - Mathf.Sqrt(Mathf.Max(0f, sphereRadius * sphereRadius - r * r));
                float y = pointDown ? -(edgeDepth - depth) : (edgeDepth - depth);
                if (i == rings) r -= rimBevel;        // pull the last ring in for the chamfer
                p.Add(P(r, y));
            }

            // Chamfered rim.
            float rimY = p[p.Count - 1].y;
            p.Add(P(discRadius, rimY + (pointDown ? -rimBevel : rimBevel) * 0.5f));
            return p;
        }

        /// <summary>
        /// Bell nozzle with a parabolic contour.
        ///
        /// A straight cone is the giveaway of a generated rocket. Real nozzles follow a
        /// bell contour computed to keep the exhaust flow axial - steep at the throat,
        /// flaring quickly, then nearly straight at the exit.
        /// </summary>
        public static List<ProfilePoint> BellNozzleProfile(float throatRadius, float exitRadius,
                                                            float length, int rings = 20)
        {
            var p = new List<ProfilePoint>();
            p.Add(P(throatRadius * 0.82f, -length * 0.08f, true));   // convergent lip
            for (int i = 0; i <= rings; i++)
            {
                float t = (float)i / rings;
                // Parabolic flare: fast expansion near the throat, easing to the exit.
                float r = Mathf.Lerp(throatRadius, exitRadius, Mathf.Pow(t, 0.62f));
                p.Add(P(r, t * length));
            }
            p.Add(P(exitRadius * 1.015f, length, true));             // thickened exit lip
            return p;
        }

        // ================================================================
        // Beveled box - panels, racks, avionics boxes
        // ================================================================

        /// <summary>
        /// A box with chamfered edges and correctly split normals.
        ///
        /// Unity's cube primitive has zero-radius edges, which under any light reads as
        /// a hard geometric shape rather than a manufactured object. A 1-2 cm chamfer
        /// catches a specular line along every edge and transforms how solid it looks.
        /// </summary>
        public static Mesh BeveledBox(string name, Vector3 size, float bevel = 0.015f,
                                      float uvScale = 1f)
        {
            bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.32f);
            Vector3 h = size * 0.5f;
            Vector3 i = h - Vector3.one * bevel;      // inset corner

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Six faces, each inset by the bevel, then the chamfer strips between them.
            AddQuad(verts, uvs, tris,
                new Vector3(-i.x, -i.y, h.z), new Vector3(i.x, -i.y, h.z),
                new Vector3(i.x, i.y, h.z), new Vector3(-i.x, i.y, h.z), uvScale);
            AddQuad(verts, uvs, tris,
                new Vector3(i.x, -i.y, -h.z), new Vector3(-i.x, -i.y, -h.z),
                new Vector3(-i.x, i.y, -h.z), new Vector3(i.x, i.y, -h.z), uvScale);
            AddQuad(verts, uvs, tris,
                new Vector3(h.x, -i.y, -i.z), new Vector3(h.x, -i.y, i.z),
                new Vector3(h.x, i.y, i.z), new Vector3(h.x, i.y, -i.z), uvScale);
            AddQuad(verts, uvs, tris,
                new Vector3(-h.x, -i.y, i.z), new Vector3(-h.x, -i.y, -i.z),
                new Vector3(-h.x, i.y, -i.z), new Vector3(-h.x, i.y, i.z), uvScale);
            AddQuad(verts, uvs, tris,
                new Vector3(-i.x, h.y, i.z), new Vector3(i.x, h.y, i.z),
                new Vector3(i.x, h.y, -i.z), new Vector3(-i.x, h.y, -i.z), uvScale);
            AddQuad(verts, uvs, tris,
                new Vector3(-i.x, -h.y, -i.z), new Vector3(i.x, -h.y, -i.z),
                new Vector3(i.x, -h.y, i.z), new Vector3(-i.x, -h.y, i.z), uvScale);

            // Edge chamfers (12) and corner patches (8), built as a rounded shell.
            AddBevelShell(verts, uvs, tris, h, i, uvScale);

            var mesh = Build(name, verts, uvs, tris);
            ApplySmoothing(mesh, 32f, null);     // low angle: faces stay flat, chamfers smooth
            return mesh;
        }

        static void AddBevelShell(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                                  Vector3 h, Vector3 i, float uvScale)
        {
            // Twelve edge strips. Each connects two face borders across the chamfer.
            // Signs: for each axis pair, four edges.
            int[,] edges = {
                {0,1,1}, {0,1,-1}, {0,-1,1}, {0,-1,-1},   // along X, varying Y,Z
                {1,0,1}, {1,0,-1}, {-1,0,1}, {-1,0,-1},   // along Y, varying X,Z
                {1,1,0}, {1,-1,0}, {-1,1,0}, {-1,-1,0},   // along Z, varying X,Y
            };

            for (int e = 0; e < edges.GetLength(0); e++)
            {
                int sx = edges[e, 0], sy = edges[e, 1], sz = edges[e, 2];

                Vector3 a, b, c, d;
                if (sx == 0)
                {
                    a = new Vector3(-i.x, sy * h.y, sz * i.z);
                    b = new Vector3(i.x, sy * h.y, sz * i.z);
                    c = new Vector3(i.x, sy * i.y, sz * h.z);
                    d = new Vector3(-i.x, sy * i.y, sz * h.z);
                    if (sy * sz < 0) { var t1 = a; a = d; d = t1; var t2 = b; b = c; c = t2; }
                }
                else if (sy == 0)
                {
                    a = new Vector3(sx * h.x, -i.y, sz * i.z);
                    b = new Vector3(sx * i.x, -i.y, sz * h.z);
                    c = new Vector3(sx * i.x, i.y, sz * h.z);
                    d = new Vector3(sx * h.x, i.y, sz * i.z);
                    if (sx * sz > 0) { var t1 = a; a = d; d = t1; var t2 = b; b = c; c = t2; }
                }
                else
                {
                    a = new Vector3(sx * h.x, sy * i.y, -i.z);
                    b = new Vector3(sx * i.x, sy * h.y, -i.z);
                    c = new Vector3(sx * i.x, sy * h.y, i.z);
                    d = new Vector3(sx * h.x, sy * i.y, i.z);
                    if (sx * sy < 0) { var t1 = a; a = d; d = t1; var t2 = b; b = c; c = t2; }
                }

                AddQuad(verts, uvs, tris, a, b, c, d, uvScale);
            }

            // Corner patches: a single triangle each, closing the shell.
            for (int cx = -1; cx <= 1; cx += 2)
                for (int cy = -1; cy <= 1; cy += 2)
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        Vector3 p1 = new Vector3(cx * h.x, cy * i.y, cz * i.z);
                        Vector3 p2 = new Vector3(cx * i.x, cy * h.y, cz * i.z);
                        Vector3 p3 = new Vector3(cx * i.x, cy * i.y, cz * h.z);

                        bool flip = (cx * cy * cz) > 0;
                        AddTri(verts, uvs, tris, flip ? p1 : p3, p2, flip ? p3 : p1, uvScale);
                    }
        }

        // ================================================================
        // Tube along a path - piping, handrails, cable runs, trusses
        // ================================================================

        public static Mesh Tube(string name, IList<Vector3> path, float radius,
                                int sides = 12, bool capEnds = true)
        {
            if (path.Count < 2) return new Mesh { name = name };

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int stride = sides + 1;

            // Parallel-transport frame: avoids the twisting that a naive up-vector
            // frame produces when the path turns.
            Vector3 prevNormal = Vector3.zero;

            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent;
                if (i == 0) tangent = (path[1] - path[0]).normalized;
                else if (i == path.Count - 1) tangent = (path[i] - path[i - 1]).normalized;
                else tangent = (path[i + 1] - path[i - 1]).normalized;

                Vector3 normal;
                if (i == 0)
                {
                    Vector3 seed = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.95f
                        ? Vector3.right : Vector3.up;
                    normal = Vector3.Cross(tangent, seed).normalized;
                }
                else
                {
                    normal = Vector3.ProjectOnPlane(prevNormal, tangent).normalized;
                    if (normal.sqrMagnitude < 0.001f)
                        normal = Vector3.Cross(tangent, Vector3.up).normalized;
                }
                prevNormal = normal;
                Vector3 binormal = Vector3.Cross(tangent, normal);

                float vCoord = i / (float)(path.Count - 1);
                for (int s = 0; s <= sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    Vector3 offset = (normal * Mathf.Cos(a) + binormal * Mathf.Sin(a)) * radius;
                    verts.Add(path[i] + offset);
                    uvs.Add(new Vector2((float)s / sides, vCoord * path.Count * radius * 2f));
                }
            }

            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * stride + s, b = a + 1;
                    int c = (i + 1) * stride + s, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }

            if (capEnds)
            {
                AddFan(verts, uvs, tris, path[0], 0, stride, sides, true);
                AddFan(verts, uvs, tris, path[path.Count - 1],
                       (path.Count - 1) * stride, stride, sides, false);
            }

            var mesh = Build(name, verts, uvs, tris);
            ApplySmoothing(mesh, 70f, null);     // high angle: pipes should look round
            return mesh;
        }

        static void AddFan(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                           Vector3 centre, int ringStart, int stride, int sides, bool flip)
        {
            int c = verts.Count;
            verts.Add(centre);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int baseIdx = verts.Count;
            for (int s = 0; s <= sides; s++)
            {
                verts.Add(verts[ringStart + s]);
                uvs.Add(new Vector2(0.5f, 0.5f));
            }

            for (int s = 0; s < sides; s++)
            {
                int a = baseIdx + s, b = baseIdx + s + 1;
                if (flip) { tris.Add(c); tris.Add(a); tris.Add(b); }
                else { tris.Add(c); tris.Add(b); tris.Add(a); }
            }
        }

        // ================================================================
        // Sphere - planets, tanks, domes
        // ================================================================

        /// <summary>
        /// UV sphere with even latitude spacing and a proper seam.
        /// Used for the Moon and Earth, where the silhouette must be perfectly round
        /// at any viewing distance.
        /// </summary>
        public static Mesh Sphere(string name, float radius, int segments = 64, int rings = 32,
                                  bool invert = false)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int stride = segments + 1;

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi = (float)ring / rings * Mathf.PI;
                float y = Mathf.Cos(phi) * radius;
                float r = Mathf.Sin(phi) * radius;

                for (int s = 0; s <= segments; s++)
                {
                    float theta = (float)s / segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(theta) * r, y, Mathf.Sin(theta) * r));
                    uvs.Add(new Vector2((float)s / segments, 1f - (float)ring / rings));
                }
            }

            for (int ring = 0; ring < rings; ring++)
                for (int s = 0; s < segments; s++)
                {
                    int a = ring * stride + s, b = a + 1;
                    int c = (ring + 1) * stride + s, d = c + 1;
                    // Clockwise seen from outside, which is Unity's front face. The
                    // earlier order (a, c, b) built every sphere inside out: with back
                    // faces culled, the planets showed the inside of their far half, lit
                    // from within, as dark blotches and spikes.
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }

            if (invert) ReverseWinding(tris);

            var mesh = Build(name, verts, uvs, tris);
            ApplySmoothing(mesh, 180f, null);    // fully smooth
            return mesh;
        }

        // ================================================================
        // Terrain
        // ================================================================

        public static Mesh HeightGrid(string name, int resolution, float size,
                                      System.Func<float, float, float> height, float uvTile = 20f)
        {
            resolution = Mathf.Clamp(resolution, 2, 400);
            int stride = resolution + 1;

            var verts = new Vector3[stride * stride];
            var uvs = new Vector2[stride * stride];
            var tris = new int[resolution * resolution * 6];

            for (int z = 0; z <= resolution; z++)
                for (int x = 0; x <= resolution; x++)
                {
                    float u = (float)x / resolution, v = (float)z / resolution;
                    float wx = (u - 0.5f) * size, wz = (v - 0.5f) * size;
                    int idx = z * stride + x;
                    verts[idx] = new Vector3(wx, height(wx, wz), wz);
                    uvs[idx] = new Vector2(u * uvTile, v * uvTile);
                }

            int t = 0;
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    int a = z * stride + x, b = a + 1, c = a + stride, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ================================================================
        // Normal smoothing - the single biggest visual improvement
        // ================================================================

        /// <summary>
        /// Recomputes normals, averaging across faces that meet below the given angle
        /// and keeping a hard edge where they do not.
        ///
        /// Unity's RecalculateNormals averages everything that shares a vertex index,
        /// so generated meshes come out either entirely faceted (no shared vertices) or
        /// smeared across real edges (all shared). Neither looks like hardware. This
        /// groups vertices by position, then decides per face-pair.
        /// </summary>
        /// <summary>Exact quantised vertex position, used as a dictionary key.</summary>
        readonly struct PosKey : System.IEquatable<PosKey>
        {
            readonly long _x, _y, _z;
            public PosKey(long x, long y, long z) { _x = x; _y = y; _z = z; }
            public bool Equals(PosKey o) { return _x == o._x && _y == o._y && _z == o._z; }
            public override bool Equals(object o) { return o is PosKey k && Equals(k); }
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 486187739 + _x.GetHashCode();
                    h = h * 486187739 + _y.GetHashCode();
                    h = h * 486187739 + _z.GetHashCode();
                    return h;
                }
            }
        }

        public static void ApplySmoothing(Mesh mesh, float smoothAngleDegrees,
                                          HashSet<int> forceSharp)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            int vcount = verts.Length;
            if (vcount == 0 || tris.Length == 0) return;

            // Area-weighted face normals: larger faces should dominate the average,
            // which matters where a big flat panel meets a narrow chamfer.
            int faceCount = tris.Length / 3;
            var faceNormals = new Vector3[faceCount];
            for (int f = 0; f < faceCount; f++)
            {
                Vector3 a = verts[tris[f * 3]];
                Vector3 b = verts[tris[f * 3 + 1]];
                Vector3 c = verts[tris[f * 3 + 2]];
                faceNormals[f] = Vector3.Cross(b - a, c - a);      // unnormalised = area-weighted
            }

            // Group vertex indices by quantised position so coincident vertices from
            // separate triangles can share a smoothed normal.
            //
            // The key is the exact quantised (x, y, z) triple. The earlier version
            // XOR-hashed the three into one long and trusted the hash: on a sphere two
            // thirds of all vertices collided with unrelated vertices elsewhere on the
            // mesh, and their normals were averaged together. That is what made the
            // planets look like camouflage and the hull shading look dirty.
            var buckets = new Dictionary<PosKey, List<int>>(vcount);
            var keyOf = new PosKey[vcount];
            const float quant = 10000f;
            for (int v = 0; v < vcount; v++)
            {
                var key = new PosKey(
                    (long)Mathf.Round(verts[v].x * quant),
                    (long)Mathf.Round(verts[v].y * quant),
                    (long)Mathf.Round(verts[v].z * quant));
                keyOf[v] = key;

                List<int> list;
                if (!buckets.TryGetValue(key, out list)) { list = new List<int>(4); buckets[key] = list; }
                list.Add(v);
            }

            // Faces touching each vertex.
            var vertFaces = new List<int>[vcount];
            for (int f = 0; f < faceCount; f++)
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[f * 3 + k];
                    if (vertFaces[v] == null) vertFaces[v] = new List<int>(4);
                    vertFaces[v].Add(f);
                }

            float cosThreshold = Mathf.Cos(Mathf.Clamp(smoothAngleDegrees, 0f, 180f) * Mathf.Deg2Rad);
            var normals = new Vector3[vcount];

            for (int v = 0; v < vcount; v++)
            {
                if (vertFaces[v] == null) { normals[v] = Vector3.up; continue; }

                bool sharpVertex = forceSharp != null && forceSharp.Contains(v);

                // Reference direction: this vertex's own faces.
                Vector3 reference = Vector3.zero;
                foreach (int f in vertFaces[v]) reference += faceNormals[f];
                if (reference.sqrMagnitude < 1e-12f) reference = Vector3.up;
                reference.Normalize();

                Vector3 sum = Vector3.zero;

                if (sharpVertex)
                {
                    foreach (int f in vertFaces[v]) sum += faceNormals[f];
                }
                else
                {
                    // Average across every coincident vertex whose faces fall inside
                    // the smoothing cone.
                    var group = buckets[keyOf[v]];
                    foreach (int other in group)
                    {
                        if (vertFaces[other] == null) continue;
                        foreach (int f in vertFaces[other])
                        {
                            Vector3 fn = faceNormals[f];
                            if (fn.sqrMagnitude < 1e-12f) continue;
                            if (Vector3.Dot(fn.normalized, reference) >= cosThreshold)
                                sum += fn;
                        }
                    }
                }

                normals[v] = sum.sqrMagnitude > 1e-12f ? sum.normalized : reference;
            }

            mesh.normals = normals;
            mesh.RecalculateTangents();      // normal maps need these
            mesh.RecalculateBounds();
        }

        // ================================================================
        // Primitive assembly helpers
        // ================================================================

        static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);

            // Planar UVs from the quad's own extents, so texture density is uniform
            // across faces of different sizes.
            float w = Vector3.Distance(a, b) * uvScale;
            float h = Vector3.Distance(b, c) * uvScale;
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(w, 0f));
            uvs.Add(new Vector2(w, h));
            uvs.Add(new Vector2(0f, h));

            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
        }

        static void AddTri(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
                           Vector3 a, Vector3 b, Vector3 c, float uvScale)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(Vector3.Distance(a, b) * uvScale, 0f));
            uvs.Add(new Vector2(0f, Vector3.Distance(a, c) * uvScale));
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
        }

        static void ReverseWinding(List<int> tris)
        {
            for (int i = 0; i < tris.Count; i += 3)
            {
                int t = tris[i];
                tris[i] = tris[i + 2];
                tris[i + 2] = t;
            }
        }

        static Mesh Build(string name, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Merges meshes so a cluster of greebles becomes one draw call.</summary>
        public static Mesh Combine(string name, IList<Mesh> meshes, IList<Matrix4x4> transforms,
                                   float smoothAngle = 40f)
        {
            var combine = new CombineInstance[meshes.Count];
            for (int i = 0; i < meshes.Count; i++)
            {
                combine[i].mesh = meshes[i];
                combine[i].transform = transforms[i];
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(combine, true, true);
            ApplySmoothing(mesh, smoothAngle, null);
            return mesh;
        }
    }
}
