using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Astronaut.MoonLanding.EditorTools
{
    /// <summary>Generates lumpy rock meshes and scatters pebbles, rocks and a boulder field.</summary>
    public static class MoonRockBuilder
    {
        const int Variants = 8;

        public static void Build(Transform parent, Terrain terrain, Material material, MoonTerrainBuilder.Crater westCrater, int seed)
        {
            var rnd = new System.Random(seed);
            Mesh[] meshes = CreateMeshes(rnd);

            var root = new GameObject("Rocks").transform;
            root.SetParent(parent, false);

            // Boulder field around the West-like crater (the one Armstrong had to fly over)
            Scatter(root, "Boulder", terrain, material, meshes, rnd, 90,
                () => AroundCrater(rnd, westCrater, 0.85f, 2.3f), 1.2f, 5.5f, 2.2f);
            // Medium rocks everywhere
            Scatter(root, "Rock", terrain, material, meshes, rnd, 750,
                () => Disc(rnd, 30f, 900f), 0.5f, 1.6f, 3f);
            // Pebbles near the landing zone (visible detail during touchdown)
            Scatter(root, "Pebble", terrain, material, meshes, rnd, 3200,
                () => Disc(rnd, 4f, 420f), 0.1f, 0.45f, 3.5f);
        }

        delegate Vector2 PointSampler();

        static Vector2 Disc(System.Random rnd, float minR, float maxR)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Mathf.Lerp(minR * minR, maxR * maxR, (float)rnd.NextDouble()));
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        static Vector2 AroundCrater(System.Random rnd, MoonTerrainBuilder.Crater c, float minK, float maxK)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float k = Mathf.Lerp(minK, maxK, Mathf.Pow((float)rnd.NextDouble(), 1.8f));
            return new Vector2(c.x + Mathf.Cos(a) * c.radius * k, c.z + Mathf.Sin(a) * c.radius * k);
        }

        static void Scatter(Transform root, string prefix, Terrain terrain, Material material, Mesh[] meshes,
            System.Random rnd, int count, PointSampler sampler, float minSize, float maxSize, float sizeSkew)
        {
            var group = new GameObject(prefix + "s").transform;
            group.SetParent(root, false);

            for (int i = 0; i < count; i++)
            {
                Vector2 p = sampler();
                float lzDistance = p.magnitude;
                float size = Mathf.Lerp(minSize, maxSize, Mathf.Pow((float)rnd.NextDouble(), sizeSkew));
                if (lzDistance < 28f && size > 0.3f) continue; // keep the landing zone safe
                if (lzDistance < 8f) continue;

                Vector3 world = new Vector3(p.x, 0f, p.y);
                world.y = MoonTerrainBuilder.GroundHeight(terrain, world);
                Vector3 normal = MoonTerrainBuilder.GroundNormal(terrain, world);

                var go = new GameObject(prefix + "_" + i);
                go.transform.SetParent(group, false);
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, 0.7f));
                go.transform.rotation = tilt * Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                go.transform.localScale = new Vector3(
                    size * Mathf.Lerp(0.8f, 1.35f, (float)rnd.NextDouble()),
                    size * Mathf.Lerp(0.45f, 0.9f, (float)rnd.NextDouble()),
                    size * Mathf.Lerp(0.8f, 1.35f, (float)rnd.NextDouble()));
                go.transform.position = world - normal * (go.transform.localScale.y * 0.22f);

                Mesh mesh = meshes[rnd.Next(meshes.Length)];
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = size > 0.35f ? ShadowCastingMode.On : ShadowCastingMode.Off;

                if (size > 0.6f)
                {
                    var col = go.AddComponent<MeshCollider>();
                    col.sharedMesh = mesh;
                    col.convex = true;
                }
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static Mesh[] CreateMeshes(System.Random rnd)
        {
            MoonAssetFactory.EnsureFolder(MoonPaths.Meshes);
            var meshes = new Mesh[Variants];
            for (int v = 0; v < Variants; v++)
            {
                Mesh mesh = Icosphere(2);
                Vector3[] verts = mesh.vertices;
                float p1 = (float)rnd.NextDouble() * 10f, p2 = (float)rnd.NextDouble() * 10f, p3 = (float)rnd.NextDouble() * 10f;
                float f1 = Mathf.Lerp(1.5f, 2.6f, (float)rnd.NextDouble());
                float f2 = Mathf.Lerp(3.5f, 5.5f, (float)rnd.NextDouble());
                float flat = Mathf.Lerp(-0.35f, -0.15f, (float)rnd.NextDouble());

                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 n = verts[i].normalized;
                    float noise =
                        0.22f * Mathf.Sin(n.x * f1 + p1) * Mathf.Sin(n.y * f1 + p2) * Mathf.Sin(n.z * f1 + p3) +
                        0.10f * Mathf.Sin(n.x * f2 + p2) * Mathf.Sin(n.y * f2 + p3) * Mathf.Sin(n.z * f2 + p1);
                    // a few "facets" make it look fractured rather than blobby
                    float facet = Mathf.Abs(Vector3.Dot(n, new Vector3(Mathf.Sin(p1), Mathf.Cos(p2), Mathf.Sin(p3)).normalized));
                    Vector3 pos = n * (0.5f + noise - Mathf.Max(0f, facet - 0.85f) * 0.8f);
                    if (pos.y < flat) pos.y = flat + (pos.y - flat) * 0.2f; // flat-ish bottom
                    verts[i] = pos;
                }
                mesh.vertices = verts;
                var uv = new Vector2[verts.Length];
                for (int i = 0; i < verts.Length; i++) uv[i] = new Vector2(verts[i].x * 0.7f + verts[i].z * 0.7f, verts[i].y + verts[i].z * 0.3f) * 1.5f;
                mesh.uv = uv;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                mesh.name = "M_Rock_" + v;

                string path = MoonPaths.Meshes + "/M_Rock_" + v + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                meshes[v] = mesh;
            }
            return meshes;
        }

        static Mesh Icosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            var cache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Midpoint(a, b, verts, cache);
                    int bc = Midpoint(b, c, verts, cache);
                    int ca = Midpoint(c, a, verts, cache);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = next;
            }

            // Winding: cross(b-a, c-a) points outwards = front face in Unity (verified).

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        static int Midpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            int index;
            if (cache.TryGetValue(key, out index)) return index;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            index = verts.Count - 1;
            cache[key] = index;
            return index;
        }
    }
}
