using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Procedural star background for the menu and the orbital backdrop.
    ///
    /// Built as a single mesh of camera-facing quads rather than a particle system:
    /// stars never move relative to each other, so simulating them every frame is
    /// wasted work on a phone. One static mesh, one draw call.
    ///
    /// Star colour follows stellar classification rather than being random white -
    /// hot blue-white through to cool orange - which is what makes a synthetic sky
    /// look plausible instead of like scattered dots.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Starfield : MonoBehaviour
    {
        [Header("Generation")]
        public int starCount = 1400;
        public float radius = 420f;
        public float minSize = 0.5f;
        public float maxSize = 2.6f;
        public int seed = 20260401;

        [Header("Look")]
        [Range(0f, 1f)] public float colourVariation = 0.75f;
        public float brightnessPower = 2.4f;

        void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) mf.sharedMesh = Generate();
        }

        public Mesh Generate()
        {
            var rng = new System.Random(seed);

            var verts = new Vector3[starCount * 4];
            var uvs = new Vector2[starCount * 4];
            var colors = new Color[starCount * 4];
            var tris = new int[starCount * 6];

            for (int i = 0; i < starCount; i++)
            {
                // Uniform distribution on a sphere. Naive angle pairs cluster at the
                // poles; inverting the cosine fixes that.
                double u = rng.NextDouble();
                double v = rng.NextDouble();
                float theta = (float)(u * System.Math.PI * 2.0);
                float phi = Mathf.Acos((float)(2.0 * v - 1.0));

                var dir = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta));

                Vector3 centre = dir * radius;

                // Brightness: a power curve so most stars are faint and a few are not.
                float brightness = Mathf.Pow((float)rng.NextDouble(), brightnessPower);
                float size = Mathf.Lerp(minSize, maxSize, brightness);

                Color tint = StarColour((float)rng.NextDouble(), brightness);

                // Build the quad in a plane perpendicular to the view direction.
                Vector3 right = Vector3.Cross(dir, Vector3.up);
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
                right.Normalize();
                Vector3 up = Vector3.Cross(right, dir);

                int b = i * 4;
                verts[b + 0] = centre + (-right - up) * size;
                verts[b + 1] = centre + (right - up) * size;
                verts[b + 2] = centre + (right + up) * size;
                verts[b + 3] = centre + (-right + up) * size;

                uvs[b + 0] = new Vector2(0f, 0f);
                uvs[b + 1] = new Vector2(1f, 0f);
                uvs[b + 2] = new Vector2(1f, 1f);
                uvs[b + 3] = new Vector2(0f, 1f);

                for (int k = 0; k < 4; k++) colors[b + k] = tint;

                int t = i * 6;
                tris[t + 0] = b + 0; tris[t + 1] = b + 2; tris[t + 2] = b + 1;
                tris[t + 3] = b + 0; tris[t + 4] = b + 3; tris[t + 5] = b + 2;
            }

            var mesh = new Mesh { name = "Starfield" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 3f);
            return mesh;
        }

        /// <summary>Approximate stellar colours across the main sequence.</summary>
        static Color StarColour(float t, float brightness)
        {
            Color c;
            if (t < 0.10f) c = new Color(0.72f, 0.80f, 1.00f);       // O/B blue-white
            else if (t < 0.28f) c = new Color(0.86f, 0.90f, 1.00f);  // A white
            else if (t < 0.58f) c = new Color(1.00f, 0.98f, 0.94f);  // F/G sun-like
            else if (t < 0.84f) c = new Color(1.00f, 0.90f, 0.74f);  // K warm
            else c = new Color(1.00f, 0.78f, 0.62f);                 // M orange

            float a = Mathf.Lerp(0.18f, 1f, brightness);
            return new Color(c.r, c.g, c.b, a);
        }
    }
}
