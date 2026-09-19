using System;
using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Wrong landing → the Lunar Module blows up:
    /// flash + fireball + regolith ring, the wreck is thrown and scorched,
    /// physical debris flies out (Rigidbodies under lunar gravity), and the wreck keeps sparking.
    /// </summary>
    [RequireComponent(typeof(LanderController))]
    public class LanderDestruction : MonoBehaviour
    {
        public MoonLandingLibrary library;
        public Renderer[] modelRenderers;
        public SparkEmitter sparks;
        public LanderAudio landerAudio;

        [Header("Explosion")]
        public int debrisCount = 42;
        public Vector2 debrisSpeed = new Vector2(6f, 26f);
        public float debrisLifetime = 35f;
        [Range(0f, 1f)] public float scorchAmount = 0.75f;

        public bool IsExploded { get; private set; }
        public event Action<Vector3> Exploded;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");

        LanderController _lander;
        Collider[] _landerColliders;

        void Awake()
        {
            _lander = GetComponent<LanderController>();
            _landerColliders = GetComponentsInChildren<Collider>();
        }

        public void Explode(Vector3 point)
        {
            if (IsExploded) return;
            IsExploded = true;

            _lander.MarkDestroyed();
            Rigidbody rb = _lander.Body;
            rb.AddForce(Vector3.up * UnityEngine.Random.Range(3f, 6f), ForceMode.VelocityChange);
            rb.AddTorque(UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(1f, 2.5f), ForceMode.VelocityChange);

            if (library != null)
            {
                MoonVFXFactory.SpawnExplosion(library, point + Vector3.up * 0.5f, Vector3.up, 1f);
            }

            Scorch();
            SpawnDebris(point);

            if (sparks != null) sparks.SetDamage(1f);
            if (landerAudio != null) landerAudio.PlayExplosion(point);

            if (Exploded != null) Exploded(point);
        }

        /// <summary>Darkens the model like burnt foil (works for glTFast and URP Lit materials).</summary>
        void Scorch()
        {
            if (modelRenderers == null) return;
            foreach (Renderer r in modelRenderers)
            {
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block, i);
                    if (m.HasProperty(GltfBaseColorId))
                        block.SetColor(GltfBaseColorId, Burnt(m.GetColor(GltfBaseColorId)));
                    if (m.HasProperty(BaseColorId))
                        block.SetColor(BaseColorId, Burnt(m.GetColor(BaseColorId)));
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        Color Burnt(Color c)
        {
            Color soot = new Color(0.07f, 0.06f, 0.05f, c.a);
            return Color.Lerp(c, soot, scorchAmount);
        }

        void SpawnDebris(Vector3 point)
        {
            Material[] palette = CollectMaterials();
            Vector3 inherit = _lander.Body.linearVelocity * 0.4f;

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = "LM_Debris";
                piece.layer = 2; // Ignore Raycast: keeps altitude radar and camera clean
                piece.transform.position = point + Vector3.up * 1.5f + UnityEngine.Random.insideUnitSphere * 2f;
                piece.transform.rotation = UnityEngine.Random.rotation;
                piece.transform.localScale = new Vector3(
                    UnityEngine.Random.Range(0.12f, 1.1f),
                    UnityEngine.Random.Range(0.02f, 0.14f),
                    UnityEngine.Random.Range(0.12f, 0.8f));

                if (palette.Length > 0)
                    piece.GetComponent<MeshRenderer>().sharedMaterial = palette[UnityEngine.Random.Range(0, palette.Length)];

                Collider pieceCollider = piece.GetComponent<Collider>();
                foreach (Collider c in _landerColliders)
                    if (c != null && pieceCollider != null) Physics.IgnoreCollision(pieceCollider, c);

                Rigidbody body = piece.AddComponent<Rigidbody>();
                body.mass = UnityEngine.Random.Range(5f, 60f);
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                Vector3 dir = (UnityEngine.Random.onUnitSphere + Vector3.up * 0.9f).normalized;
                body.linearVelocity = inherit + dir * UnityEngine.Random.Range(debrisSpeed.x, debrisSpeed.y);
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 18f;

                if (library != null)
                {
                    if (i % 4 == 0) MoonVFXFactory.AttachSmokeTrail(library, piece.transform, false);
                    else if (i % 3 == 0) MoonVFXFactory.AttachSmokeTrail(library, piece.transform, true);
                }

                Destroy(piece, debrisLifetime * UnityEngine.Random.Range(0.7f, 1.2f));
            }
        }

        Material[] CollectMaterials()
        {
            var list = new System.Collections.Generic.List<Material>();
            if (modelRenderers != null)
                foreach (Renderer r in modelRenderers)
                    if (r != null)
                        foreach (Material m in r.sharedMaterials)
                            if (m != null && !list.Contains(m)) list.Add(m);
            return list.ToArray();
        }
    }
}
