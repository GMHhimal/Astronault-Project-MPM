using UnityEngine;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Builds every particle system of the challenge from code, so the look is consistent and
    /// nobody has to hand-edit hundreds of Shuriken settings. Used by the editor Level Builder
    /// (saved into the scene, editable in the Inspector) and at runtime for explosions.
    /// </summary>
    public static class MoonVFXFactory
    {
        // ------------------------------------------------------------------ helpers
        public static ParticleSystem CreateSystem(string name, Transform parent, Material material, bool worldSpace, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 5f;
            main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 4f;
            return ps;
        }

        static Gradient MakeGradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colorTimes[i]);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);
            g.SetKeys(ck, ak);
            return g;
        }

        static void SetColorOverLifetime(ParticleSystem ps, Gradient g)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        static void SetSizeOverLifetime(ParticleSystem ps, float start, float end)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            float max = Mathf.Max(start, end);
            var curve = AnimationCurve.EaseInOut(0f, start / max, 1f, end / max);
            sol.size = new ParticleSystem.MinMaxCurve(max, curve);
        }

        static void Burst(ParticleSystem ps, float count)
        {
            var main = ps.main;
            main.loop = false;
            main.duration = 1f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(count)) });
        }

        /// <summary>Changes emission without allocating (call every frame).</summary>
        public static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTimeMultiplier = rate;
        }

        // ------------------------------------------------------------------ engine
        /// <summary>Descent engine exhaust. Parent should point its +Z down the nozzle.</summary>
        public static ParticleSystem CreatePlume(MoonLandingLibrary lib, Transform nozzle)
        {
            var ps = CreateSystem("VFX_EnginePlume", nozzle, lib.fireMaterial, true, 600);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startColor = new Color(1f, 0.9f, 0.8f, 0.55f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 7f;
            shape.radius = 0.35f;

            var inherit = ps.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new ParticleSystem.MinMaxCurve(1f);

            SetColorOverLifetime(ps, MakeGradient(
                new[] { new Color(0.75f, 0.85f, 1f), new Color(1f, 0.62f, 0.3f), new Color(0.6f, 0.25f, 0.1f) },
                new[] { 0f, 0.35f, 1f },
                new[] { 0.7f, 0.35f, 0f }, new[] { 0f, 0.5f, 1f }));
            SetSizeOverLifetime(ps, 0.6f, 2.4f);
            return ps;
        }

        public static ParticleSystem CreatePlumeCore(MoonLandingLibrary lib, Transform nozzle)
        {
            var ps = CreateSystem("VFX_EngineCore", nozzle, lib.flashMaterial, false, 120);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
            main.startColor = new Color(0.85f, 0.9f, 1f, 0.9f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 2f;
            shape.radius = 0.15f;
            SetColorOverLifetime(ps, MakeGradient(
                new[] { Color.white, new Color(1f, 0.7f, 0.4f) }, new[] { 0f, 1f },
                new[] { 0.9f, 0f }, new[] { 0f, 1f }));
            return ps;
        }

        // ------------------------------------------------------------------ lunar dust
        /// <summary>
        /// In vacuum there is no air to make billowing clouds: regolith is blasted out in fast, flat,
        /// ballistic sheets (seen in Apollo footage). The "haze" layer is a small artistic addition.
        /// Parent's +Z must point along the ground normal.
        /// </summary>
        public static void CreateDust(MoonLandingLibrary lib, Transform emitterRoot,
            out ParticleSystem sheet, out ParticleSystem haze, out ParticleSystem streaks)
        {
            sheet = CreateSystem("VFX_DustSheet", emitterRoot, lib.dustMaterial, true, 1500);
            var main = sheet.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
            main.startColor = new Color(0.62f, 0.6f, 0.57f, 0.42f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
            var shape = sheet.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 84f;
            shape.radius = 2.5f;
            SetColorOverLifetime(sheet, MakeGradient(
                new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 0.9f, 0.4f, 0f }, new[] { 0f, 0.08f, 0.5f, 1f }));
            SetSizeOverLifetime(sheet, 0.6f, 3.0f);

            haze = CreateSystem("VFX_DustHaze", emitterRoot, lib.dustMaterial, true, 250);
            main = haze.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startColor = new Color(0.55f, 0.53f, 0.5f, 0.22f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.05f);
            shape = haze.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70f;
            shape.radius = 4f;
            var limit = haze.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(1.5f);
            limit.dampen = 0.08f;
            var rot = haze.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            SetColorOverLifetime(haze, MakeGradient(
                new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 1f, 0f }, new[] { 0f, 0.2f, 1f }));
            SetSizeOverLifetime(haze, 0.5f, 2.2f);

            streaks = CreateSystem("VFX_DustStreaks", emitterRoot, lib.dustMaterial, true, 600);
            main = streaks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(28f, 48f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = new Color(0.75f, 0.72f, 0.68f, 0.6f);
            shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 86f;
            shape.radius = 1.5f;
            var r = streaks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.08f;
            r.lengthScale = 2f;
            SetColorOverLifetime(streaks, MakeGradient(
                new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 1f, 0f }, new[] { 0f, 1f }));
        }

        // ------------------------------------------------------------------ RCS
        public static ParticleSystem CreateRcsQuad(MoonLandingLibrary lib, Transform parent, string name, Vector3 localPos)
        {
            var ps = CreateSystem(name, parent, lib.smokeMaterial, true, 200);
            ps.transform.localPosition = localPos;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(0.95f, 0.95f, 1f, 0.55f);
            main.startSpeed = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            SetColorOverLifetime(ps, MakeGradient(
                new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0.8f, 0f }, new[] { 0f, 1f }));
            SetSizeOverLifetime(ps, 0.4f, 2.5f);
            return ps;
        }

        // ------------------------------------------------------------------ sparks
        /// <summary>World-space spark pool. Use ParticleSystem.Emit with EmitParams to spawn sparks anywhere.</summary>
        public static ParticleSystem CreateSparkPool(MoonLandingLibrary lib, Transform parent)
        {
            var ps = CreateSystem("VFX_SparkPool", parent, lib.sparkMaterial, true, 1200);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new Color(0.85f, 0.92f, 1f, 1f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
            var shape = ps.shape;
            shape.enabled = false;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1f;
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.bounce = new ParticleSystem.MinMaxCurve(0.35f);
            collision.dampen = new ParticleSystem.MinMaxCurve(0.45f);
            collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0.15f);
            SetColorOverLifetime(ps, MakeGradient(
                new[] { new Color(0.8f, 0.9f, 1f), new Color(1f, 0.75f, 0.35f), new Color(1f, 0.35f, 0.1f) },
                new[] { 0f, 0.3f, 1f },
                new[] { 1f, 1f, 0f }, new[] { 0f, 0.6f, 1f }));
            return ps;
        }

        // ------------------------------------------------------------------ explosion
        /// <summary>Spawns a complete explosion (flash, fireball, sparks, regolith ring, smoke, light).</summary>
        public static GameObject SpawnExplosion(MoonLandingLibrary lib, Vector3 position, Vector3 up, float scale)
        {
            var root = new GameObject("FX_Explosion");
            root.transform.position = position;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.forward, up.sqrMagnitude > 0f ? up : Vector3.up);

            // 1. Flash
            var flash = CreateSystem("Flash", root.transform, lib.flashMaterial, false, 4);
            var main = flash.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0f;
            main.startSize = 45f * scale;
            main.startColor = new Color(1f, 0.92f, 0.75f, 1f);
            var fs = flash.shape; fs.enabled = false;
            Burst(flash, 1);
            SetColorOverLifetime(flash, MakeGradient(new[] { Color.white, new Color(1f, 0.6f, 0.3f) }, new[] { 0f, 1f },
                new[] { 1f, 0f }, new[] { 0f, 1f }));
            SetSizeOverLifetime(flash, 0.5f, 1.3f);
            flash.GetComponent<ParticleSystemRenderer>().maxParticleSize = 50f;

            // 2. Fireball (hypergolic propellants carry their own oxidiser, so a brief fireball is plausible)
            var fire = CreateSystem("Fireball", root.transform, lib.fireMaterial, true, 120);
            main = fire.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 16f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(3f * scale, 8f * scale);
            main.startColor = Color.white;
            var shape = fire.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f * scale;
            Burst(fire, 60);
            var limit = fire.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(2f);
            limit.dampen = 0.12f;
            SetColorOverLifetime(fire, MakeGradient(
                new[] { new Color(1f, 0.95f, 0.8f), new Color(1f, 0.55f, 0.15f), new Color(0.35f, 0.1f, 0.05f) },
                new[] { 0f, 0.3f, 1f }, new[] { 1f, 0.8f, 0f }, new[] { 0f, 0.4f, 1f }));
            SetSizeOverLifetime(fire, 0.6f, 1.8f);
            fire.GetComponent<ParticleSystemRenderer>().maxParticleSize = 20f;

            // 3. Sparks / burning debris embers
            var sparks = CreateSystem("Embers", root.transform, lib.sparkMaterial, true, 500);
            main = sparks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f * scale, 45f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = new Color(1f, 0.8f, 0.5f, 1f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
            shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1f;
            Burst(sparks, 320);
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.03f;
            var col = sparks.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.bounce = new ParticleSystem.MinMaxCurve(0.35f);
            col.dampen = new ParticleSystem.MinMaxCurve(0.4f);
            SetColorOverLifetime(sparks, MakeGradient(
                new[] { new Color(1f, 0.95f, 0.8f), new Color(1f, 0.5f, 0.15f) }, new[] { 0f, 1f },
                new[] { 1f, 1f, 0f }, new[] { 0f, 0.7f, 1f }));

            // 4. Regolith shock ring (flat, fast — vacuum ballistic)
            var ring = CreateSystem("RegolithRing", root.transform, lib.dustMaterial, true, 300);
            main = ring.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(14f * scale, 34f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(2f * scale, 6f * scale);
            main.startColor = new Color(0.6f, 0.58f, 0.55f, 0.45f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(1f);
            shape = ring.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 80f;
            shape.radius = 2f * scale;
            Burst(ring, 160);
            SetColorOverLifetime(ring, MakeGradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 1f, 0f }, new[] { 0f, 0.1f, 1f }));
            SetSizeOverLifetime(ring, 0.5f, 2f);

            // 5. Lingering dark smoke
            var smoke = CreateSystem("Smoke", root.transform, lib.smokeMaterial, true, 120);
            main = smoke.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(5f * scale, 12f * scale);
            main.startColor = new Color(0.12f, 0.11f, 0.1f, 0.6f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.02f);
            main.startDelay = new ParticleSystem.MinMaxCurve(0.15f);
            shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 2.5f * scale;
            Burst(smoke, 45);
            var rot = smoke.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            SetColorOverLifetime(smoke, MakeGradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 0f, 1f, 0f }, new[] { 0f, 0.15f, 1f }));
            SetSizeOverLifetime(smoke, 0.5f, 2f);
            smoke.GetComponent<ParticleSystemRenderer>().maxParticleSize = 20f;

            // 6. Light
            var lightGo = new GameObject("ExplosionLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 3f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 90f * scale;
            light.intensity = 40f;
            light.shadows = LightShadows.None;
            var fade = lightGo.AddComponent<LightFlashFade>();
            fade.duration = 1.2f;

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
            Object.Destroy(root, 14f);
            return root;
        }

        /// <summary>Smoke trail for a flying debris piece.</summary>
        public static ParticleSystem AttachSmokeTrail(MoonLandingLibrary lib, Transform target, bool embers)
        {
            var ps = CreateSystem(embers ? "EmberTrail" : "SmokeTrail", target, embers ? lib.sparkMaterial : lib.smokeMaterial, true, 150);
            var main = ps.main;
            main.startLifetime = embers ? new ParticleSystem.MinMaxCurve(0.2f, 0.5f) : new ParticleSystem.MinMaxCurve(1.2f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.6f);
            main.startSize = embers ? new ParticleSystem.MinMaxCurve(0.05f, 0.12f) : new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startColor = embers ? new Color(1f, 0.7f, 0.3f, 1f) : new Color(0.15f, 0.14f, 0.13f, 0.5f);
            main.duration = 4f;
            main.loop = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            var emission = ps.emission;
            emission.rateOverTime = embers ? 30f : 10f;
            emission.rateOverDistance = embers ? 4f : 2f;
            SetColorOverLifetime(ps, MakeGradient(new[] { Color.white, Color.white }, new[] { 0f, 1f },
                new[] { 1f, 0f }, new[] { 0f, 1f }));
            if (!embers) SetSizeOverLifetime(ps, 0.5f, 2.5f);
            ps.Play();
            return ps;
        }
    }
}
