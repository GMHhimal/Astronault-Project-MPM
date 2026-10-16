using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// The Trans-Earth Injection burn and the journey home: Orion leaves lunar orbit,
    /// flies to Earth, drops its service module and turns its heat shield forward.
    ///
    /// Version 2. The first version kept the spacecraft almost still and grew an Earth
    /// that had just popped into existence, so Earth appeared to fly at the rocket.
    /// Here the geometry is honest: Earth sits fixed in space and is visible from the
    /// first frame, the spacecraft points its nose at it, fires, and travels. Earth
    /// grows only because the spacecraft is getting closer, and the Moon falls behind.
    ///
    /// The finale runs on its own cinematic camera in a separate "stage" far from the
    /// AR content. In AR the phone camera is owned by tracking and cannot fly anywhere,
    /// and a journey of hundreds of thousands of kilometres does not fit on a table, so
    /// the sequence cuts to a film shot and back. Distances on screen are compressed;
    /// every number in the telemetry panel is real physics.
    ///
    ///   Burn   - Tsiolkovsky: dv = Isp * g0 * ln(m0 / m), mass falls as propellant burns
    ///   Coast  - energy conservation in Earth's gravity:
    ///            v^2 = v0^2 + 2*mu*(1/r - 1/r0), so the spacecraft speeds up as it falls
    ///            from lunar distance (about 1 km/s) to entry interface (about 11 km/s)
    ///   Entry  - the crew module separates from the European Service Module and
    ///            flips so the heat shield leads, as Orion does about 20 minutes before
    ///            entry interface
    /// </summary>
    public class TEISequence : MonoBehaviour
    {
        // ---- Vehicle constants (published Orion / ESM figures) --------
        const float EngineThrustN = 26_600f;    // AJ10-190 orbital manoeuvring engine, 6,000 lbf
        const float SpecificImpulse = 316f;     // seconds, vacuum
        const float G0 = 9.80665f;              // standard gravity, defines Isp
        const float InitialMassKg = 20_500f;    // Orion + ESM at TEI, after lunar operations
        const float TargetDeltaV = 450f;        // m/s, near-rectilinear halo orbit departure

        // The Gateway near-rectilinear halo orbit: about 1,600 km above the lunar north
        // pole at its closest and 70,000 km at its farthest.
        const float MoonRadiusKm = 1737.4f;
        const float MoonMu = 4.9048695e12f;     // GM of the Moon, m^3/s^2
        const float PeriluneAltKm = 1600f;
        const float ApoluneAltKm = 70000f;

        // Earth return.
        const double EarthMu = 3.986004418e14;  // GM of Earth, m^3/s^2
        const double EarthRadiusKm = 6371.0;
        const double EntryInterfaceAltKm = 122.0; // where Orion "meets the atmosphere"
        const double LunarDistanceKm = 384_400.0;
        const double DepartureSpeedMs = 1_000.0;  // Earth-relative speed just after TEI

        [Header("Scene")]
        public Transform spacecraft;
        public Transform moon;                  // the lunar surface used while exploring
        public Transform earth;
        public Transform cameraRig;
        [Tooltip("The AR camera. It is switched off while the cinematic camera runs.")]
        public Camera sequenceCamera;

        [Header("Stage (optional; sensible fallbacks are used if empty)")]
        [Tooltip("Material for the lunar globe seen from orbit, e.g. M_MoonSurface.")]
        public Material moonGlobeMaterial;
        [Tooltip("Rim glow for the lunar globe, e.g. M_MoonRim.")]
        public Material moonRimMaterial;

        [Header("Effects")]
        public ParticleSystem[] enginePlume;
        public Light engineGlow;
        public Transform shakePivot;

        [Header("UI")]
        public GameObject hudRoot;
        public TMP_Text phaseLabel;
        public TMP_Text countdownText;
        public TMP_Text deltaVText;
        public TMP_Text accelText;
        public TMP_Text massText;
        public TMP_Text burnTimeText;
        public TMP_Text timeWarpText;
        public Image burnProgressBar;
        public CanvasGroup hudGroup;

        [Header("Completion")]
        public GameObject completeCard;
        public TMP_Text completeHeadline;
        public TMP_Text completeScore;
        public TMP_Text completeBody;
        public TouchButton completeButton;

        [Header("Motion")]
        [Tooltip("Kept for scene compatibility; the journey now uses real stage distances.")]
        public float riseRate = 0.85f;

        [Header("Timing")]
        [Tooltip("How much faster than real time the burn is shown. The real burn is about five minutes.")]
        public float timeWarp = 22f;
        public float countdownSeconds = 10f;
        [Tooltip("Seconds of screen time for the coast from the Moon to Earth.")]
        public float coastSeconds = 14f;

        public event System.Action Finished;

        // ---- Stage layout, in model metres (Orion is about 10 m long) ----
        static readonly Vector3 StageOrigin = new Vector3(6000f, 6000f, 6000f);
        const float StageMoonRadius = 1000f;
        const float StageOrbitAltitude = 80f;
        const float StageEarthRadius = 380f;
        const float StageEarthDistance = 9000f;
        const float SeparationDistance = 1500f;   // from Earth's centre
        const float EntryDistance = 470f;         // from Earth's centre, entry glow begins

        float _massKg;
        float _deltaV;
        float _burnElapsed;
        bool _running;

        // Stage objects
        Transform _stage;
        Camera _cam;
        Transform _starfield;
        Transform _moonGlobe;
        Transform _crewModule;
        Transform _serviceModule;
        Light _entryGlow;

        // Directions in the stage
        Vector3 _toEarth;        // unit vector, burn direction and flight path
        Vector3 _up;             // perpendicular to _toEarth, roughly "up"
        Vector3 _side;
        Vector3 _orbitDir;       // initial attitude: nose along the orbit
        Vector3 _earthPos;

        // Camera smoothing
        Vector3 _camPos;
        Vector3 _camLook;
        float _shake;
        float _coastEndFraction = 0.9f;

        float MassFlowRate { get { return EngineThrustN / (SpecificImpulse * G0); } }

        void Awake()
        {
            // "BURN COMPLETE" at countdown size wrapped onto three lines. One line,
            // shrinking if it has to.
            if (countdownText != null)
            {
                float size = countdownText.fontSize;
                countdownText.enableAutoSizing = true;
                countdownText.fontSizeMax = size;
                countdownText.fontSizeMin = Mathf.Round(size * 0.4f);
                countdownText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            if (hudRoot != null) hudRoot.SetActive(false);
            if (completeCard != null) completeCard.SetActive(false);
            SetPlume(false);
            if (engineGlow != null) engineGlow.intensity = 0f;
        }

        public void Begin(int score, int total)
        {
            if (_running) return;
            _running = true;
            StartCoroutine(Run(score, total));
        }

        IEnumerator Run(int score, int total)
        {
            _massKg = InitialMassKg;
            _deltaV = 0f;
            _burnElapsed = 0f;

            BuildStage();

            if (hudRoot != null) hudRoot.SetActive(true);
            if (hudGroup != null) hudGroup.alpha = 1f;
            if (timeWarpText != null) timeWarpText.text = "TIME ×" + Mathf.RoundToInt(timeWarp);

            AudioDirector.SetAmbience("amb_orbit", 1.2f);

            yield return Phase_Approach();
            yield return Phase_Countdown();
            yield return Phase_Burn();
            yield return Phase_Coast();
            yield return Phase_Separation();
            yield return Phase_Arrival(score, total);
        }

        // ---------------------------------------------------------------
        // Stage
        // ---------------------------------------------------------------

        void BuildStage()
        {
            _stage = new GameObject("FinaleStage").transform;
            _stage.position = StageOrigin;

            // Directions. Earth is ahead, a little above the orbital plane and to one
            // side, so the camera can keep both the spacecraft and the target in frame.
            _orbitDir = Vector3.right;
            _toEarth = new Vector3(0.80f, 0.30f, 0.52f).normalized;
            _up = (Vector3.up - _toEarth * Vector3.Dot(Vector3.up, _toEarth)).normalized;
            _side = Vector3.Cross(_toEarth, _up).normalized;
            _earthPos = StageOrigin + _toEarth * StageEarthDistance;

            // Hide everything that belongs to the explore view.
            if (moon != null) moon.gameObject.SetActive(false);

            Transform contentRoot = spacecraft != null ? spacecraft.parent : null;
            Transform oldStars = contentRoot != null ? contentRoot.Find("Starfield") : null;

            // ---- Cinematic camera -----------------------------------
            var camGO = new GameObject("FinaleCamera");
            _cam = camGO.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.black;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 40000f;
            _cam.fieldOfView = 55f;
            _cam.depth = sequenceCamera != null ? sequenceCamera.depth + 10f : 10f;

            // The AR camera (and the passthrough feed with it) steps aside. Its audio
            // listener stays on, so sound is unaffected.
            if (sequenceCamera != null) sequenceCamera.enabled = false;

            // ---- Stars, following the camera so they sit at infinity ----
            if (oldStars != null)
            {
                _starfield = Instantiate(oldStars.gameObject).transform;
                _starfield.name = "FinaleStars";
                _starfield.rotation = Quaternion.identity;
                _starfield.localScale = Vector3.one * 45f;     // 400 m radius -> 18 km
                oldStars.gameObject.SetActive(false);
            }

            // ---- Earth: fixed in space, visible from the first frame ----
            if (earth != null)
            {
                earth.SetParent(null, false);
                earth.gameObject.SetActive(true);
                earth.position = _earthPos;
                earth.localScale = Vector3.one * StageEarthRadius;
                earth.rotation = Quaternion.Euler(-23.4f, 160f, 0f);   // axial tilt
            }

            // ---- The Moon as a globe below the spacecraft ----
            if (earth != null)
            {
                _moonGlobe = Instantiate(earth.gameObject, _stage).transform;
                _moonGlobe.name = "FinaleMoon";
                _moonGlobe.position = StageOrigin + Vector3.down * (StageMoonRadius + StageOrbitAltitude);
                _moonGlobe.localScale = Vector3.one * StageMoonRadius;
                // Tip the pole over: the spacecraft sits over the globe's equator, not
                // over the pole where the map pinches into streaks.
                _moonGlobe.rotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.AngleAxis(140f, Vector3.up);

                var mr = _moonGlobe.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = MoonMaterial(mr.sharedMaterial);

                var rim = _moonGlobe.Find("Atmosphere");
                if (rim != null)
                {
                    var rmr = rim.GetComponent<MeshRenderer>();
                    if (moonRimMaterial != null && rmr != null) rmr.sharedMaterial = moonRimMaterial;
                    else rim.gameObject.SetActive(false);     // no air on the Moon
                }
            }

            // ---- The spacecraft ------------------------------------
            if (spacecraft != null)
            {
                spacecraft.SetParent(_stage, false);
                spacecraft.localPosition = Vector3.zero;
                spacecraft.localScale = Vector3.one;
                spacecraft.rotation = NoseAlong(_orbitDir, _up);

                _crewModule = FindDeep(spacecraft, "CrewModule");
                _serviceModule = FindDeep(spacecraft, "EuropeanServiceModule");
            }

            // ---- Sunlight from behind and to the side: a gibbous Earth, a lit hull ----
            var sun = FindSun(contentRoot);
            if (sun != null)
                sun.transform.rotation = Quaternion.LookRotation(
                    (_toEarth + _side * 0.9f - _up * 0.35f).normalized, Vector3.up);

            // Opening shot.
            SideShot(out _camPos, out _camLook);
            ApplyCamera(1f);
        }

        Material MoonMaterial(Material fallbackSource)
        {
            if (moonGlobeMaterial != null) return moonGlobeMaterial;

            // No lunar map wired: borrow the regolith material from the explore view so
            // the globe is at least grey rock, never a grey Earth.
            if (moon != null)
            {
                var r = moon.GetComponentInChildren<Renderer>(true);
                if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
            }

            var m = new Material(fallbackSource);
            m.color = new Color(0.42f, 0.41f, 0.40f);
            return m;
        }

        static Light FindSun(Transform contentRoot)
        {
            if (contentRoot != null)
            {
                var t = contentRoot.Find("Sun");
                var l = t != null ? t.GetComponent<Light>() : null;
                if (l != null) return l;
            }
            if (RenderSettings.sun != null) return RenderSettings.sun;
            var any = FindFirstObjectByType<Light>();
            return any != null && any.type == LightType.Directional ? any : null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Rotation that points the stack axis (+Y, nose) along <paramref name="dir"/>.</summary>
        static Quaternion NoseAlong(Vector3 dir, Vector3 upHint)
        {
            Vector3 fwd = Vector3.Cross(dir, Vector3.Cross(upHint, dir));
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, dir.normalized);
        }

        // ---------------------------------------------------------------
        // Camera
        // ---------------------------------------------------------------

        Vector3 CraftPos { get { return spacecraft != null ? spacecraft.position : StageOrigin; } }

        /// <summary>Side-on: the spacecraft over the lunar horizon.</summary>
        void SideShot(out Vector3 pos, out Vector3 look)
        {
            // Far enough out that the 19 m solar array span fits a portrait screen.
            Vector3 o = CraftPos;
            pos = o + _side * 58f - _orbitDir * 18f + Vector3.up * 7f;
            look = o + Vector3.down * 9f + _orbitDir * 5f;
        }

        /// <summary>
        /// Chase: behind and above, looking past the spacecraft toward Earth. The
        /// spacecraft sits just below centre, clear of the telemetry panel, with Earth
        /// in the upper third ahead of it.
        /// </summary>
        void ChaseShot(Transform target, out Vector3 pos, out Vector3 look, float scale = 1f)
        {
            // Three-quarter view from behind: the full stack with its solar arrays is
            // about 19 m across, so the camera sits about 70 m back. The crew module
            // alone is 5 m, so the separation shot passes a smaller scale.
            Vector3 o = target != null ? target.position : CraftPos;
            pos = o + (-_toEarth * 70f + _up * 14f + _side * 14f) * scale;
            look = o + (_toEarth * 60f - _up * 14f) * scale;
        }

        /// <summary>Exponentially smooths toward the desired pose; 1 = snap.</summary>
        void MoveCamera(Vector3 pos, Vector3 look, float sharpness)
        {
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            _camPos = Vector3.Lerp(_camPos, pos, k);
            _camLook = Vector3.Lerp(_camLook, look, k);
            ApplyCamera(0f);
        }

        void ApplyCamera(float snap)
        {
            if (_cam == null) return;

            Vector3 shake = Vector3.zero;
            if (_shake > 0.00001f)
            {
                float t = Time.time;
                shake = new Vector3(
                    Mathf.PerlinNoise(t * 31f, 0.17f) - 0.5f,
                    Mathf.PerlinNoise(0.43f, t * 27f) - 0.5f,
                    Mathf.PerlinNoise(t * 23f, t * 19f) - 0.5f) * (2f * _shake);
            }

            _cam.transform.position = _camPos + shake;
            _cam.transform.rotation = Quaternion.LookRotation((_camLook - _camPos).normalized, _up);
        }

        void LateUpdate()
        {
            // Stars at infinity: they translate with the camera but never rotate with it.
            if (_starfield != null && _cam != null) _starfield.position = _cam.transform.position;
        }

        // ---------------------------------------------------------------
        // Phases
        // ---------------------------------------------------------------

        /// <summary>Side-on view of Orion over the lunar horizon, Earth on the skyline.</summary>
        IEnumerator Phase_Approach()
        {
            SetPhase("LUNAR HALO ORBIT · PERILUNE");

            // Vis-viva at perilune of the halo orbit: v^2 = mu (2/r - 1/a).
            float rp = (MoonRadiusKm + PeriluneAltKm) * 1000f;
            float ra = (MoonRadiusKm + ApoluneAltKm) * 1000f;
            float a = 0.5f * (rp + ra);
            float vPerilune = Mathf.Sqrt(MoonMu * (2f / rp - 1f / a));

            UpdateReadouts();
            if (deltaVText != null)
                deltaVText.text = "SPEED AT PERILUNE  " + vPerilune.ToString("#,0") + " m/s";

            float t = 0f;
            while (t < 4f)
            {
                t += Time.deltaTime;
                // Orbital motion along the orbit, Moon sliding beneath.
                if (spacecraft != null) spacecraft.position += _orbitDir * (3f * Time.deltaTime);
                SideShot(out var p, out var l);
                MoveCamera(p, l, 3f);
                yield return null;
            }
        }

        /// <summary>
        /// Countdown, during which the reaction control thrusters slew the spacecraft
        /// from its orbital attitude to the burn attitude, nose toward the departure
        /// direction. The main engine is fixed, so the whole vehicle must point.
        /// </summary>
        IEnumerator Phase_Countdown()
        {
            SetPhase("TEI BURN SEQUENCE · ATTITUDE SLEW");

            Quaternion from = spacecraft != null ? spacecraft.rotation : Quaternion.identity;
            Quaternion to = NoseAlong(_toEarth, _up);
            int steps = Mathf.RoundToInt(countdownSeconds);

            for (int i = steps; i > 0; i--)
            {
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(true);
                    countdownText.text = "T−" + i.ToString("00");
                    countdownText.color = i <= 3 ? UITheme.Gold : UITheme.TextHi;
                }

                AudioDirector.Play(i <= 3 ? "beep_high" : "beep_low");
                if (i <= 3) Haptics.Tick();
                if (i == 4) SetPhase("TEI BURN SEQUENCE · ATTITUDE HOLD");

                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime;

                    // Global progress through the countdown, 0..1.
                    float g = ((steps - i) + t) / steps;

                    if (spacecraft != null)
                    {
                        spacecraft.position += _orbitDir * (3f * (1f - g) * Time.deltaTime);
                        // Slew over the first 70% of the countdown, then hold.
                        float slew = UITheme.EaseInOut(Mathf.Clamp01(g / 0.7f));
                        spacecraft.rotation = Quaternion.Slerp(from, to, slew);
                    }

                    // Camera swings from the side shot to the chase position, so by
                    // ignition the viewer is looking where the spacecraft will go.
                    SideShot(out var sp, out var sl);
                    ChaseShot(spacecraft, out var cp, out var cl);
                    float blend = UITheme.EaseInOut(Mathf.Clamp01((g - 0.35f) / 0.6f));
                    MoveCamera(Vector3.Lerp(sp, cp, blend), Vector3.Lerp(sl, cl, blend), 2.5f);

                    yield return null;
                }
            }

            if (countdownText != null) countdownText.text = "IGNITION";
            AudioDirector.Play("engine_ignite");
            Haptics.Impact();
        }

        /// <summary>
        /// The burn. Integrates mass depletion and evaluates the rocket equation each
        /// frame, so every figure shown is derived rather than scripted. The vehicle
        /// accelerates along its nose, away from the plume.
        /// </summary>
        IEnumerator Phase_Burn()
        {
            SetPhase("MAIN ENGINE BURN");

            SetPlume(true);
            AudioDirector.SetEngine("engine_loop", 0f);

            float mdot = MassFlowRate;
            Vector3 start = CraftPos;
            const float burnTravel = 140f;

            while (_deltaV < TargetDeltaV)
            {
                float dt = Time.deltaTime * timeWarp;
                _burnElapsed += dt;

                _massKg = Mathf.Max(1f, _massKg - mdot * dt);
                _deltaV = SpecificImpulse * G0 * Mathf.Log(InitialMassKg / _massKg);

                float progress = Mathf.Clamp01(_deltaV / TargetDeltaV);

                // Thrust is constant, so acceleration climbs as propellant burns off.
                float accel = EngineThrustN / _massKg;

                float ramp = Mathf.Clamp01(_burnElapsed / 6f);
                if (engineGlow != null) engineGlow.intensity = ramp * 7f;
                AudioDirector.SetEngine("engine_loop", ramp * 0.9f, 0.92f + progress * 0.1f);
                _shake = 0.06f * ramp * (1f - progress * 0.4f);

                if (burnProgressBar != null) burnProgressBar.fillAmount = progress;
                UpdateReadouts(accel);

                // Distance grows with the square of time under steady acceleration.
                if (spacecraft != null)
                    spacecraft.position = start + _toEarth * (burnTravel * progress * progress);

                ChaseShot(spacecraft, out var cp, out var cl);
                MoveCamera(cp, cl, 4f);

                yield return null;
            }

            // Cutoff.
            SetPlume(false);
            if (engineGlow != null) engineGlow.intensity = 0f;
            AudioDirector.StopEngine(1.2f);
            _shake = 0f;

            SetPhase("MECO — MAIN ENGINE CUTOFF");
            if (countdownText != null) countdownText.text = "BURN COMPLETE";
            AudioDirector.Play("engine_cutoff");
            Haptics.Impact();

            // Keep drifting at burnout speed while the cutoff reads.
            Vector3 v = _toEarth * (2f * burnTravel / Mathf.Max(1f, _burnElapsed / timeWarp));
            float hold = 0f;
            while (hold < 2.5f)
            {
                hold += Time.deltaTime;
                if (spacecraft != null) spacecraft.position += v * Time.deltaTime;
                ChaseShot(spacecraft, out var cp, out var cl);
                MoveCamera(cp, cl, 4f);
                yield return null;
            }
        }

        /// <summary>
        /// The flight home. The spacecraft travels to Earth; Earth grows because it is
        /// getting closer. Speed and distance come from energy conservation in Earth's
        /// gravity, so the readout speeds up as the spacecraft falls home.
        /// </summary>
        IEnumerator Phase_Coast()
        {
            SetPhase("TRANS-EARTH COAST");
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (timeWarpText != null) timeWarpText.text = "COAST · DAYS IN SECONDS";
            if (burnProgressBar != null) burnProgressBar.fillAmount = 0f;

            Vector3 start = CraftPos;
            Vector3 end = _earthPos - _toEarth * SeparationDistance;
            float pathLength = Vector3.Distance(start, end);
            float fullPath = Vector3.Distance(start, _earthPos - _toEarth * EntryDistance);
            _coastEndFraction = Mathf.Clamp01(pathLength / Mathf.Max(1f, fullPath));

            float t = 0f;
            while (t < coastSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / coastSeconds);
                // Smoothstep: leaves burnout gently and arrives gently, so the hand-off
                // to the separation shot has no jump. The visual distance is compressed
                // and non-linear; the readout carries the true speed profile.
                float f = k * k * (3f - 2f * k);

                if (spacecraft != null)
                {
                    spacecraft.position = Vector3.Lerp(start, end, f);
                    // Passive thermal control: a slow roll that evens out solar heating.
                    spacecraft.Rotate(Vector3.up, 6f * Time.deltaTime, Space.Self);
                }

                float travelled = pathLength * f;
                CoastReadouts(Mathf.Clamp01(travelled / fullPath));
                if (burnProgressBar != null) burnProgressBar.fillAmount = travelled / fullPath;

                ChaseShot(spacecraft, out var cp, out var cl);
                MoveCamera(cp, cl, 5f);

                yield return null;
            }
        }

        /// <summary>
        /// Crew module separates from the service module, which will burn up, then
        /// flips so the heat shield faces the direction of travel.
        /// </summary>
        IEnumerator Phase_Separation()
        {
            SetPhase("CREW MODULE SEPARATION");
            AudioDirector.Play("beep_high");
            Haptics.Impact();

            Vector3 travel = _toEarth * 55f;      // m/s on screen, compressed

            Transform cm = _crewModule != null ? _crewModule : spacecraft;
            Transform esm = _serviceModule;

            if (cm != null && cm != spacecraft) cm.SetParent(_stage, true);
            Quaternion cmFrom = cm != null ? cm.rotation : Quaternion.identity;
            // Heat shield (the crew module's -Y face) forward.
            Quaternion cmTo = NoseAlong(-_toEarth, _up);

            // Entry glow, on the heat shield.
            if (cm != null)
            {
                var glowGO = new GameObject("EntryGlow");
                glowGO.transform.SetParent(cm, false);
                glowGO.transform.localPosition = new Vector3(0f, -1.2f, 0f);
                _entryGlow = glowGO.AddComponent<Light>();
                _entryGlow.type = LightType.Point;
                _entryGlow.color = new Color(1f, 0.55f, 0.22f);
                _entryGlow.range = 16f;
                _entryGlow.intensity = 0f;
            }

            float t = 0f;
            const float duration = 6f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;

                if (cm != null)
                {
                    cm.position += travel * Time.deltaTime;
                    // Separation first, then the flip.
                    float flip = UITheme.EaseInOut(Mathf.Clamp01((k - 0.25f) / 0.6f));
                    cm.rotation = Quaternion.Slerp(cmFrom, cmTo, flip);
                }

                if (esm != null)
                {
                    // The service module trails behind and tumbles slowly.
                    esm.position += (travel * 0.82f - _side * 1.2f) * Time.deltaTime;
                    esm.Rotate(new Vector3(9f, 4f, 3f) * Time.deltaTime, Space.Self);
                }

                if (k > 0.25f) SetPhase("HEAT SHIELD FORWARD");

                // The last stretch: from separation altitude down to entry interface,
                // where the fall is steepest and the speed climbs fastest.
                CoastReadouts(Mathf.Lerp(_coastEndFraction, 1f, k));

                ChaseShot(cm, out var cp, out var cl, 0.45f);
                MoveCamera(cp, cl, 3.5f);

                yield return null;
            }

            // Entry interface: the plasma glow builds on the heat shield.
            SetPhase("ENTRY INTERFACE · 122 KM");
            AudioDirector.Play("engine_cutoff");
            t = 0f;
            while (t < 3f)
            {
                t += Time.deltaTime;
                if (cm != null) cm.position += travel * Time.deltaTime;
                if (_entryGlow != null) _entryGlow.intensity = Mathf.Lerp(0f, 9f, t / 3f);
                _shake = 0.05f * (t / 3f);
                CoastReadouts(1f);
                ChaseShot(cm, out var cp, out var cl, 0.45f);
                MoveCamera(cp, cl, 3.5f);
                yield return null;
            }
            _shake = 0f;
        }

        IEnumerator Phase_Arrival(int score, int total)
        {
            SetPhase("EARTH ARRIVAL");
            AudioDirector.SetAmbience("amb_earth", 1.5f);
            AudioDirector.Play("mission_success");

            yield return new WaitForSeconds(1.2f);

            if (hudGroup != null)
            {
                float t = 0f;
                while (t < 0.6f)
                {
                    t += Time.deltaTime;
                    hudGroup.alpha = 1f - (t / 0.6f);
                    yield return null;
                }
                hudGroup.alpha = 0f;
            }
            if (hudRoot != null) hudRoot.SetActive(false);

            if (completeCard != null) completeCard.SetActive(true);
            if (completeHeadline != null) completeHeadline.text = "MISSION COMPLETE";
            if (completeScore != null)
                completeScore.text = score + " / " + total;
            if (completeBody != null)
                completeBody.text =
                    "The burn put Orion on a trans-Earth trajectory. It fell home, " +
                    "speeding up from about 1 km/s to 11 km/s, dropped its service " +
                    "module and met the atmosphere heat shield first.\n\n" +
                    "Welcome home, astronaut.";

            if (completeButton != null)
            {
                completeButton.SetLabel("RETURN TO MENU");
                completeButton.onClick.RemoveAllListeners();
                completeButton.onClick.AddListener(() =>
                {
                    _running = false;
                    if (Finished != null) Finished();
                });
            }

            Haptics.Impact();
        }

        // ---------------------------------------------------------------
        // Readouts
        // ---------------------------------------------------------------

        void UpdateReadouts(float accel = 0f)
        {
            if (deltaVText != null && _deltaV > 0f)
                deltaVText.text = "ΔV  " + _deltaV.ToString("0") + " / " +
                                  TargetDeltaV.ToString("0") + " m/s";

            if (accelText != null)
                accelText.text = "ACCEL  " + accel.ToString("0.00") + " m/s²   (" +
                                 (accel / G0).ToString("0.000") + " g)";

            if (massText != null)
                massText.text = "MASS  " + (_massKg / 1000f).ToString("0.00") + " t";

            if (burnTimeText != null)
                burnTimeText.text = "BURN TIME  " + FormatTime(_burnElapsed);
        }

        /// <summary>
        /// Coast telemetry from energy conservation. <paramref name="fraction"/> is how
        /// far along the path from lunar distance to entry interface the spacecraft is.
        /// </summary>
        void CoastReadouts(float fraction)
        {
            double r0 = LunarDistanceKm * 1000.0;
            double rEI = (EarthRadiusKm + EntryInterfaceAltKm) * 1000.0;
            double r = r0 - (r0 - rEI) * fraction;
            double v = System.Math.Sqrt(DepartureSpeedMs * DepartureSpeedMs +
                                        2.0 * EarthMu * (1.0 / r - 1.0 / r0));

            if (deltaVText != null)
                deltaVText.text = "SPEED  " + (v / 1000.0).ToString("0.00") + " km/s  (Earth-relative)";
            if (accelText != null)
                accelText.text = "DISTANCE TO EARTH  " + (r / 1000.0).ToString("#,0") + " km";
            if (massText != null)
                massText.text = "MASS  " + (_massKg / 1000f).ToString("0.00") + " t";
            if (burnTimeText != null)
                burnTimeText.text = "ΔV ACHIEVED  " + TargetDeltaV.ToString("0") + " m/s";
        }

        static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }

        void SetPhase(string text)
        {
            if (phaseLabel != null) phaseLabel.text = text;
        }

        void SetPlume(bool on)
        {
            if (enginePlume == null) return;
            foreach (var ps in enginePlume)
            {
                if (ps == null) continue;
                if (on) ps.Play(); else ps.Stop();
            }
        }
    }
}
