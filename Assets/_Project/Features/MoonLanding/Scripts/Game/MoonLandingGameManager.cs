using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Astronaut.MoonLanding
{
    public enum MissionState { Briefing, Countdown, Flying, Ended, Paused }

    [Serializable]
    public class DifficultyPreset
    {
        public string name = "NORMAL";
        [TextArea(2, 4)] public string description;

        [Header("Start")]
        public float startAltitude = 220f;
        [Tooltip("Horizontal distance from the landing zone at start (m).")]
        public float startDistance = 300f;
        [Tooltip("Initial speed towards the landing zone (m/s).")]
        public float startHorizontalSpeed = 10f;
        [Tooltip("Initial vertical speed (negative = falling).")]
        public float startVerticalSpeed = -8f;
        public float fuel = 700f;
        [Range(0f, 1f)] public float startThrottle = 0.3f;
        public SasMode sas = SasMode.RateDamp;

        [Header("Landing rules")]
        [Tooltip("Radius of the green landing zone (m).")]
        public float landingZoneRadius = 30f;
        [Tooltip("If true, landing outside the zone FAILS the mission (hard mode). Otherwise it only reduces the score.")]
        public bool requireLandingZone = false;
        public float safeVerticalSpeed = 2f;
        public float crashVerticalSpeed = 4.2f;
        public float safeHorizontalSpeed = 1.2f;
        public float crashHorizontalSpeed = 3.5f;
        public float safeTilt = 12f;
        public float tipOverTilt = 42f;
        [Tooltip("Engine shuts down automatically once resting on the feet.")]
        public bool autoEngineCut = false;

        [Header("Help & score")]
        public GuidanceLevel guidance = GuidanceLevel.Hints;
        public float scoreMultiplier = 1.5f;
    }

    /// <summary>
    /// Mission flow: Briefing → Countdown → Powered descent → Result.
    /// Applies the difficulty rules, runs guidance and scoring, saves the best score, restarts, returns to the hub.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class MoonLandingGameManager : MonoBehaviour
    {
        [Header("Scene references (set by the level builder)")]
        public LanderController lander;
        public LandingEvaluator evaluator;
        public LanderDestruction destruction;
        public LanderAudio landerAudio;
        public SparkEmitter sparks;
        public LanderCameraRig cameraRig;
        public LandingZone landingZone;
        public MoonLandingLibrary library;
        public LandingGuidance guidance;
        public MoonLandingScore score;

        [Header("Flow")]
        [Tooltip("Main HUD / menu scene of the team project (must be in Build Profiles).")]
        public string hubSceneName = "MainMenu";
        [Tooltip("Direction the lander flies towards the landing zone.")]
        public Vector3 approachDirection = Vector3.forward;
        public float resultDelay = 2.5f;

        public DifficultyPreset[] difficulties = DefaultPresets();

        public MissionState State { get; private set; }
        public int DifficultyIndex { get; private set; }
        public DifficultyPreset Difficulty { get { return difficulties[Mathf.Clamp(DifficultyIndex, 0, difficulties.Length - 1)]; } }
        public float MissionTime { get; private set; }
        public float CountdownRemaining { get; private set; }
        public bool ResultVisible { get; private set; }
        public MoonLandingResult LastResult { get; private set; }
        public List<ScoreLine> ScoreBreakdown { get; private set; }

        public event Action<MissionState> StateChanged;
        /// <summary>text, colour — big centre-screen call-outs.</summary>
        public event Action<string, Color> Callout;
        public event Action<MoonLandingResult> ResultReady;

        static bool s_autoStart;
        static int s_difficulty = -1;

        MissionState _stateBeforePause;
        float _resultShownTime;
        bool _hardLandingSparked;

        public static DifficultyPreset[] DefaultPresets()
        {
            return new[]
            {
                new DifficultyPreset
                {
                    name = "EASY", description = "Land anywhere safe. The flight computer tells you what to do, keeps you level and shuts the engine down for you.",
                    startAltitude = 110f, startDistance = 90f, startHorizontalSpeed = 3f, startVerticalSpeed = -4f,
                    fuel = 950f, startThrottle = 0.45f, sas = SasMode.AutoLevel,
                    landingZoneRadius = 45f, requireLandingZone = false,
                    safeVerticalSpeed = 3.0f, crashVerticalSpeed = 5.5f, safeHorizontalSpeed = 2.0f, crashHorizontalSpeed = 4.5f,
                    safeTilt = 18f, tipOverTilt = 48f, autoEngineCut = true,
                    guidance = GuidanceLevel.Full, scoreMultiplier = 1f
                },
                new DifficultyPreset
                {
                    name = "NORMAL", description = "Land anywhere safe. Closer to the target = more points. Only urgent warnings are shown.",
                    startAltitude = 220f, startDistance = 280f, startHorizontalSpeed = 10f, startVerticalSpeed = -8f,
                    fuel = 750f, startThrottle = 0.3f, sas = SasMode.RateDamp,
                    landingZoneRadius = 30f, requireLandingZone = false,
                    safeVerticalSpeed = 2.2f, crashVerticalSpeed = 4.5f, safeHorizontalSpeed = 1.4f, crashHorizontalSpeed = 3.8f,
                    safeTilt = 14f, tipOverTilt = 42f, autoEngineCut = false,
                    guidance = GuidanceLevel.Hints, scoreMultiplier = 1.5f
                },
                new DifficultyPreset
                {
                    name = "HARD", description = "Apollo 11 style: land INSIDE the small target zone. Low fuel, SAS off, no help. Miss the target = mission failed.",
                    startAltitude = 360f, startDistance = 520f, startHorizontalSpeed = 18f, startVerticalSpeed = -12f,
                    fuel = 540f, startThrottle = 0f, sas = SasMode.Off,
                    landingZoneRadius = 15f, requireLandingZone = true,
                    safeVerticalSpeed = 2.0f, crashVerticalSpeed = 4.0f, safeHorizontalSpeed = 1.2f, crashHorizontalSpeed = 3.2f,
                    safeTilt = 12f, tipOverTilt = 40f, autoEngineCut = false,
                    guidance = GuidanceLevel.None, scoreMultiplier = 2.5f
                },
            };
        }

        void Awake()
        {
            ScoreBreakdown = new List<ScoreLine>();
        }

        void Start()
        {
            if (difficulties == null || difficulties.Length == 0) difficulties = DefaultPresets();
            DifficultyIndex = s_difficulty >= 0 ? s_difficulty : PlayerPrefs.GetInt(MoonLandingEvents.DifficultyKey, 0);
            DifficultyIndex = Mathf.Clamp(DifficultyIndex, 0, difficulties.Length - 1);

            if (evaluator != null)
            {
                evaluator.OutcomeDecided += OnOutcome;
                evaluator.Impact += OnImpact;
                evaluator.ContactLightOn += OnContactLight;
            }
            if (lander != null) lander.FuelDepleted += OnFuelDepleted;

            PlaceLanderAtStart();
            SetState(MissionState.Briefing);
            if (cameraRig != null) cameraRig.SetMode(LanderCameraMode.Showcase);

            if (s_autoStart)
            {
                s_autoStart = false;
                BeginCountdown();
            }
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (evaluator != null)
            {
                evaluator.OutcomeDecided -= OnOutcome;
                evaluator.Impact -= OnImpact;
                evaluator.ContactLightOn -= OnContactLight;
            }
            if (lander != null) lander.FuelDepleted -= OnFuelDepleted;
        }

        // ------------------------------------------------------------------ public API (HUD buttons)
        public void SelectDifficulty(int index)
        {
            if (State != MissionState.Briefing) return;
            DifficultyIndex = Mathf.Clamp(index, 0, difficulties.Length - 1);
            PlayerPrefs.SetInt(MoonLandingEvents.DifficultyKey, DifficultyIndex);
            PlaceLanderAtStart();
            Click();
        }

        public void BeginCountdown()
        {
            if (State != MissionState.Briefing) return;
            PlaceLanderAtStart();
            CountdownRemaining = 3.999f;
            SetState(MissionState.Countdown);
            if (cameraRig != null) cameraRig.SetMode(LanderCameraMode.Chase);
            if (landerAudio != null && library != null) { landerAudio.PlayRadio(true); landerAudio.PlayUI(library.countdownBeep, 0.7f); }
        }

        public void Pause()
        {
            if (State != MissionState.Flying && State != MissionState.Countdown) return;
            _stateBeforePause = State;
            SetState(MissionState.Paused);
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public void Resume()
        {
            if (State != MissionState.Paused) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SetState(_stateBeforePause);
        }

        public void Retry()
        {
            s_autoStart = true;
            s_difficulty = DifficultyIndex;
            ReloadScene();
        }

        public void BackToBriefing()
        {
            s_autoStart = false;
            s_difficulty = DifficultyIndex;
            ReloadScene();
        }

        public bool CanExitToHub
        {
            get { return !string.IsNullOrEmpty(hubSceneName) && Application.CanStreamedLevelBeLoaded(hubSceneName); }
        }

        public void ExitToHub()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (CanExitToHub) SceneManager.LoadScene(hubSceneName);
            else RaiseCallout("Add the '" + hubSceneName + "' scene to Build Profiles", new Color(1f, 0.7f, 0.2f));
        }

        // ------------------------------------------------------------------ loop
        void Update()
        {
            if (lander == null) return;
            LanderInput input = lander.Controls;

            switch (State)
            {
                case MissionState.Briefing:
                    if (input.ConsumeConfirm()) BeginCountdown();
                    break;

                case MissionState.Countdown:
                {
                    int before = Mathf.CeilToInt(CountdownRemaining);
                    CountdownRemaining -= Time.deltaTime;
                    int after = Mathf.CeilToInt(CountdownRemaining);
                    if (after != before && after > 0 && landerAudio != null && library != null)
                        landerAudio.PlayUI(library.countdownBeep, 0.7f);
                    if (CountdownRemaining <= 0f) Launch();
                    if (input.ConsumePause()) Pause();
                    break;
                }

                case MissionState.Flying:
                    MissionTime += Time.deltaTime;
                    if (input.ConsumePause()) Pause();
                    if (input.ConsumeCamera() && cameraRig != null) cameraRig.CycleMode();
                    if (input.ConsumeRestart()) Retry();
                    break;

                case MissionState.Paused:
                    if (input.ConsumePause()) Resume();
                    break;

                case MissionState.Ended:
                    if (input.ConsumeCamera() && cameraRig != null) cameraRig.CycleMode();
                    if (ResultVisible && Time.unscaledTime - _resultShownTime > 0.8f && (input.ConsumeRestart() || input.ConsumeConfirm()))
                        Retry();
                    break;
            }
        }

        void Launch()
        {
            DifficultyPreset p = Difficulty;
            Rigidbody rb = lander.Body;
            rb.isKinematic = false;
            Vector3 dir = FlatApproach();
            rb.linearVelocity = dir * p.startHorizontalSpeed + Vector3.up * p.startVerticalSpeed;
            rb.angularVelocity = Vector3.zero;

            lander.ControlsEnabled = true;
            evaluator.ResetEvaluation();
            evaluator.Armed = true;
            MissionTime = 0f;
            if (score != null) { score.ResetScore(p.scoreMultiplier); score.Running = true; }
            SetState(MissionState.Flying);

            if (landerAudio != null && library != null) landerAudio.PlayUI(library.countdownGo, 0.8f);
            RaiseCallout("GO FOR POWERED DESCENT", new Color(0.45f, 1f, 0.6f));
        }

        Vector3 FlatApproach()
        {
            Vector3 dir = approachDirection;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
        }

        void ApplyDifficultyRules(DifficultyPreset p)
        {
            if (evaluator != null)
            {
                evaluator.safeVerticalSpeed = p.safeVerticalSpeed;
                evaluator.crashVerticalSpeed = p.crashVerticalSpeed;
                evaluator.safeHorizontalSpeed = p.safeHorizontalSpeed;
                evaluator.crashHorizontalSpeed = p.crashHorizontalSpeed;
                evaluator.safeTilt = p.safeTilt;
                evaluator.tipOverTilt = p.tipOverTilt;
                evaluator.autoEngineCut = p.autoEngineCut;
            }
            if (landingZone != null) landingZone.SetRadius(p.landingZoneRadius);
            if (guidance != null) guidance.level = p.guidance;
            if (score != null) score.ResetScore(p.scoreMultiplier);
        }

        void PlaceLanderAtStart()
        {
            if (lander == null || landingZone == null) return;
            DifficultyPreset p = Difficulty;
            ApplyDifficultyRules(p);
            Vector3 dir = FlatApproach();

            Vector3 pos = landingZone.transform.position - dir * p.startDistance;
            RaycastHit hit;
            float groundY = landingZone.transform.position.y;
            if (Physics.Raycast(pos + Vector3.up * 3000f, Vector3.down, out hit, 6000f, lander.groundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
            pos.y = groundY + p.startAltitude;

            Rigidbody rb = lander.Body;
            rb.isKinematic = true;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
            lander.transform.SetPositionAndRotation(pos, rot);
            rb.position = pos;
            rb.rotation = rot;

            lander.ResetState(p.fuel, p.startThrottle);
            lander.sasMode = p.sas;
            lander.ControlsEnabled = false;
            if (evaluator != null) { evaluator.Armed = false; evaluator.ResetEvaluation(); }
        }

        // ------------------------------------------------------------------ events
        void OnContactLight()
        {
            RaiseCallout("CONTACT LIGHT", new Color(0.45f, 1f, 0.6f));
            if (landerAudio != null && library != null) landerAudio.PlayVoice(library.voiceContactLight);
        }

        void OnFuelDepleted()
        {
            if (State == MissionState.Flying) RaiseCallout("FUEL DEPLETED — ENGINE FLAME-OUT", new Color(1f, 0.35f, 0.25f));
        }

        void OnImpact(Vector3 point, Vector3 normal, float speed, bool footPad)
        {
            if (landerAudio != null) landerAudio.PlayImpact(point, speed, footPad);
            if (cameraRig != null) cameraRig.AddShake(Mathf.Clamp01(speed / 8f));
            if (sparks != null && speed > 2.2f)
                sparks.Burst(point, normal, Mathf.RoundToInt(Mathf.Lerp(10f, 60f, Mathf.Clamp01(speed / 6f))), 1f);
            if (State == MissionState.Flying && evaluator != null && evaluator.HardImpacts > 0 && !_hardLandingSparked)
            {
                _hardLandingSparked = true;
                if (sparks != null) sparks.SetDamage(0.45f);
                RaiseCallout("HARD CONTACT — LEG DAMAGE", new Color(1f, 0.7f, 0.2f));
            }
        }

        void OnOutcome(LandingOutcome outcome, string reason)
        {
            lander.ControlsEnabled = false;
            if (score != null) score.Running = false;
            DifficultyPreset p = Difficulty;

            // HARD mode: a safe landing outside the target zone does not count.
            if ((outcome == LandingOutcome.Success || outcome == LandingOutcome.HardLanding) && p.requireLandingZone && landingZone != null)
            {
                float d = landingZone.HorizontalDistance(lander.transform.position);
                if (d > landingZone.radius)
                {
                    outcome = LandingOutcome.MissedTarget;
                    reason = string.Format("You landed safely, but {0:0} m from the target. HARD mode needs a landing inside the {1:0} m zone.", d, landingZone.radius);
                }
            }

            bool destroyed = outcome == LandingOutcome.Crashed || outcome == LandingOutcome.TippedOver;
            if (destroyed)
            {
                Vector3 point = evaluator.LastImpactPoint;
                if (point == Vector3.zero) point = lander.transform.position;
                if (destruction != null) destruction.Explode(point);
                if (cameraRig != null) cameraRig.AddShake(1f);
                RaiseCallout(outcome == LandingOutcome.TippedOver ? "LANDER TIPPED OVER" : "CRASH", new Color(1f, 0.3f, 0.25f));
            }
            else if (outcome == LandingOutcome.OutOfBounds || outcome == LandingOutcome.Aborted)
            {
                lander.CutEngine();
                RaiseCallout("MISSION ABORTED", new Color(1f, 0.7f, 0.2f));
            }
            else if (outcome == LandingOutcome.MissedTarget)
            {
                lander.CutEngine();
                RaiseCallout("MISSED THE TARGET", new Color(1f, 0.7f, 0.2f));
            }
            else
            {
                lander.CutEngine();
                RaiseCallout(outcome == LandingOutcome.Success ? "THE EAGLE HAS LANDED" : "LANDED — HARD TOUCHDOWN",
                    outcome == LandingOutcome.Success ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.8f, 0.3f));
                if (landerAudio != null && library != null) landerAudio.PlayVoice(library.voiceLanded);
            }

            LastResult = BuildResult(outcome, reason);
            SaveProgress();
            SetState(MissionState.Ended);
            StartCoroutine(ShowResultAfterDelay());
        }

        IEnumerator ShowResultAfterDelay()
        {
            yield return new WaitForSeconds(resultDelay);
            ResultVisible = true;
            _resultShownTime = Time.unscaledTime;
            if (cameraRig != null && cameraRig.Mode == LanderCameraMode.Chase) cameraRig.SetMode(LanderCameraMode.Showcase);
            if (landerAudio != null && library != null)
            {
                landerAudio.PlayRadio(false);
                landerAudio.PlayUI(LastResult.IsSuccess ? library.missionSuccess : library.missionFailed, 0.8f);
            }
            if (ResultReady != null) ResultReady(LastResult);
            MoonLandingEvents.RaiseFinished(LastResult);
        }

        MoonLandingResult BuildResult(LandingOutcome outcome, string reason)
        {
            DifficultyPreset p = Difficulty;
            var r = new MoonLandingResult
            {
                outcome = outcome,
                reason = reason,
                difficulty = p.name,
                touchdownVerticalSpeed = evaluator.TouchdownVerticalSpeed,
                touchdownHorizontalSpeed = evaluator.TouchdownHorizontalSpeed,
                finalTiltDeg = lander.TiltDegrees,
                distanceToTarget = landingZone != null ? landingZone.HorizontalDistance(lander.transform.position) : 0f,
                fuelRemainingKg = lander.Fuel,
                fuelFraction = lander.FuelFraction,
                missionTime = MissionTime,
            };

            if (score != null)
            {
                r.flightPoints = Mathf.RoundToInt(score.FlightPoints);
                r.score = score.BuildFinal(r, p, ScoreBreakdown);
            }

            // Stars: 3 = soft & inside the zone, 2 = soft landing anywhere, 1 = hard / missed target, 0 = failed
            bool inZone = landingZone == null || r.distanceToTarget <= landingZone.radius;
            if (outcome == LandingOutcome.Success) r.stars = inZone ? 3 : 2;
            else if (outcome == LandingOutcome.HardLanding || outcome == LandingOutcome.MissedTarget) r.stars = 1;
            else r.stars = 0;
            return r;
        }

        void SaveProgress()
        {
            MoonLandingResult r = LastResult;
            if (r.IsSuccess) PlayerPrefs.SetInt(MoonLandingEvents.CompletedKey, 1);
            if (r.score > PlayerPrefs.GetInt(MoonLandingEvents.BestScoreKey, 0))
            {
                PlayerPrefs.SetInt(MoonLandingEvents.BestScoreKey, r.score);
                r.newBest = true;
                LastResult = r;
            }
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ helpers
        void SetState(MissionState s)
        {
            State = s;
            if (StateChanged != null) StateChanged(s);
        }

        void RaiseCallout(string text, Color color)
        {
            if (Callout != null) Callout(text, color);
        }

        void Click()
        {
            if (landerAudio != null && library != null) landerAudio.PlayUI(library.uiClick, 0.6f);
        }

        void ReloadScene()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
