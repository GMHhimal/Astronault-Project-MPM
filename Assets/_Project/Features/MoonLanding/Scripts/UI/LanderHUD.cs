using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// Mobile-first sci-fi HUD for the Moon Landing Challenge, built from code at runtime
    /// (nothing to merge-conflict in the scene).
    ///  • Telemetry, fuel, live score with "+points" popups, radar with sweep, sink-rate gauge, attitude ball
    ///  • Guidance banner ("RELEASE THE THRUST NOW") that also makes the right control glow
    ///  • Touch controls: tilt joystick, throttle lever, big THRUST button, CUT, yaw, SAS, camera, pause
    ///  • Briefing with difficulty cards, countdown, animated result screen with score breakdown (win or lose)
    /// </summary>
    public class LanderHUD : MonoBehaviour
    {
        [Header("References")]
        public MoonLandingGameManager game;
        public Camera viewCamera;

        [Header("Options")]
        public bool showTouchControls = true;
        public float radarRange = 600f;
        public int sortingOrder = 50;

        LanderController _lander;
        LandingEvaluator _evaluator;
        LandingZone _zone;
        LanderInput _input;
        LandingGuidance _guidance;
        MoonLandingScore _score;
        bool _mobile;

        Canvas _canvas;
        RectTransform _canvasRect, _safe;
        Rect _lastSafeArea;

        // ---- flight HUD
        GameObject _flightGroup, _touchGroup;
        Text _altValue, _vsValue, _hsValue, _tiltValue, _rangeValue, _hoverValue;
        Image _vsArrow;
        RectTransform _fuelFill;
        Image _fuelFillImg;
        Text _fuelText;
        Text _scoreValue, _timeValue, _difficultyBadge;
        float _displayScore;
        RectTransform _popupRoot;
        readonly List<Popup> _popups = new List<Popup>();

        GameObject _guideRoot;
        Image _guideBg, _guideGlow, _guideIcon;
        Text _guidePrimary, _guideSecondary;

        Text _warning, _callout;
        float _calloutTimer;
        Image[] _alarmEdges;

        RectTransform _horizon;
        Text _pitchRollText;
        RectTransform _sinkMarker, _sinkBand, _sinkSafe;
        Text _sinkValue;
        const float SinkGaugeHeight = 250f, SinkGaugeMax = 12f;

        RectTransform _radarBlip, _radarVelocity, _radarHeading, _radarSweep;
        Text _radarText, _sasText;
        RectTransform _lzMarker;
        Text _lzMarkerText;

        // ---- touch controls
        VirtualJoystick _stick;
        Image _stickArrow;
        ThrottleLever _lever;
        Image _leverGlow, _leverArrow;
        Text _leverText;
        HoldButton _thrust, _yawLeft, _yawRight;
        Button _cutButton;
        Image _cutGlow;

        // ---- panels
        GameObject _briefing, _result, _pause, _countdownGroup;
        Text _countdownText;
        Image _countdownRing;
        Image[] _cardGlows;
        Text _resultTitle, _resultReason, _resultScore, _resultBest;
        Image[] _stars;
        RectTransform _breakdownRoot;
        readonly List<Text[]> _breakdownRows = new List<Text[]>();
        float _resultStartTime;
        MoonLandingResult _shownResult;

        class Popup
        {
            public Text text;
            public float age;
        }

        void Start()
        {
            if (game == null) game = FindFirstObjectByType<MoonLandingGameManager>();
            if (game == null) { Debug.LogError("[MoonLanding] LanderHUD needs a MoonLandingGameManager."); enabled = false; return; }
            _lander = game.lander;
            _evaluator = game.evaluator;
            _zone = game.landingZone;
            _guidance = game.guidance;
            _score = game.score;
            _input = _lander != null ? _lander.Controls : null;
            _mobile = LunarEnvironment.IsMobile || UnityEngine.InputSystem.Touchscreen.current != null;
            if (viewCamera == null) viewCamera = Camera.main;
            if (game.library != null) MoonUI.SetFonts(game.library.displayFont, game.library.bodyFont);

            EnsureEventSystem();
            Build();

            game.Callout += ShowCallout;
            game.StateChanged += OnStateChanged;
            game.ResultReady += OnResult;
            if (_score != null) _score.PointsAwarded += OnPoints;
            OnStateChanged(game.State);
        }

        void OnDestroy()
        {
            if (game == null) return;
            game.Callout -= ShowCallout;
            game.StateChanged -= OnStateChanged;
            game.ResultReady -= OnResult;
            if (_score != null) _score.PointsAwarded -= OnPoints;
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // =================================================================== BUILD
        void Build()
        {
            var canvasGo = new GameObject("MoonLanding_HUD_Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.8f; // phones are wide: keep heights stable
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasGo.transform;

            _safe = MoonUI.Stretch(MoonUI.MakeRect("SafeArea", _canvasRect));
            ApplySafeArea();

            BuildAlarmFrame();

            _flightGroup = MoonUI.Stretch(MoonUI.MakeRect("Flight", _safe)).gameObject;
            BuildTelemetry(_flightGroup.transform);
            BuildScoreBar(_flightGroup.transform);
            BuildGuidance(_flightGroup.transform);
            BuildRadar(_flightGroup.transform);
            BuildInstruments(_flightGroup.transform);
            BuildLzMarker(_flightGroup.transform);
            BuildTouchControls(_flightGroup.transform);

            if (!_mobile)
            {
                Text help = MoonUI.MakeText(_flightGroup.transform, "KeyboardHelp",
                    "W/S A/D tilt  ·  Q/E yaw  ·  SHIFT/CTRL throttle  ·  SPACE full thrust  ·  X cut  ·  F SAS  ·  C camera  ·  RMB orbit  ·  H buttons  ·  ESC pause",
                    18, TextAnchor.MiddleCenter, MoonUI.TextDim);
                MoonUI.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(1500f, 26f));
            }

            _callout = MoonUI.MakeText(_safe, "Callout", "", 56, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(_callout.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1100f, 90f));

            BuildCountdown();
            BuildBriefing();
            BuildResult();
            BuildPause();
        }

        void ApplySafeArea()
        {
            Rect sa = Screen.safeArea;
            _lastSafeArea = sa;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            _safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            _safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            _safe.offsetMin = Vector2.zero;
            _safe.offsetMax = Vector2.zero;
        }

        void BuildAlarmFrame()
        {
            _alarmEdges = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                Image e = MoonUI.MakeImage(_canvasRect, "AlarmEdge" + i, new Color(1f, 0.15f, 0.1f, 0f), MoonUI.GradientSprite);
                RectTransform rt = e.rectTransform;
                switch (i)
                {
                    case 0: rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, 110f); break;
                    case 1: rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, 110f); rt.localRotation = Quaternion.Euler(0f, 0f, 180f); break;
                    case 2: rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(1400f, 110f); rt.localRotation = Quaternion.Euler(0f, 0f, 90f); break;
                    default: rt.anchorMin = new Vector2(1f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(1400f, 110f); rt.localRotation = Quaternion.Euler(0f, 0f, -90f); break;
                }
                rt.anchoredPosition = Vector2.zero;
                _alarmEdges[i] = e;
            }
        }

        Text Label(Transform p, string text, Vector2 pos, float width, TextAnchor align = TextAnchor.MiddleLeft)
        {
            Text t = MoonUI.MakeText(p, text + "_Label", text, 20, align, MoonUI.TextDim, false, FontStyle.Bold);
            MoonUI.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, 26f));
            return t;
        }

        Text Value(Transform p, string name, Vector2 pos, float width, int size)
        {
            Text t = MoonUI.MakeText(p, name, "--", size, TextAnchor.MiddleLeft, Color.white, true, FontStyle.Bold);
            MoonUI.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(width, size + 12f));
            return t;
        }

        void BuildTelemetry(Transform parent)
        {
            Image panel = MoonUI.MakePanel(parent, "Telemetry", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(440f, 262f));
            Transform p = panel.transform.parent;

            Text title = MoonUI.MakeText(p, "Title", "DESCENT DATA", 17, TextAnchor.MiddleLeft, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -10f), new Vector2(300f, 24f));

            Label(p, "ALTITUDE", new Vector2(22f, -38f), 200f);
            _altValue = Value(p, "Altitude", new Vector2(18f, -58f), 400f, 58);

            Label(p, "VERTICAL", new Vector2(22f, -138f), 200f);
            _vsArrow = MoonUI.MakeImage(p, "VsArrow", Color.white, MoonUI.ArrowSprite);
            MoonUI.Place(_vsArrow.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(34f, -183f), new Vector2(24f, 24f));
            _vsValue = Value(p, "VerticalSpeed", new Vector2(50f, -162f), 190f, 30);

            Label(p, "SIDEWAYS", new Vector2(240f, -138f), 180f);
            _hsValue = Value(p, "HorizontalSpeed", new Vector2(240f, -162f), 190f, 30);

            Label(p, "TILT", new Vector2(22f, -204f), 100f);
            _tiltValue = MoonUI.MakeText(p, "Tilt", "--", 24, TextAnchor.MiddleLeft, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_tiltValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -226f), new Vector2(200f, 30f));

            Label(p, "TO TARGET", new Vector2(240f, -204f), 180f);
            _rangeValue = MoonUI.MakeText(p, "Range", "--", 24, TextAnchor.MiddleLeft, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_rangeValue.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(240f, -226f), new Vector2(200f, 30f));

            // fuel strip
            Image fuelPanel = MoonUI.MakePanel(parent, "Fuel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -304f), new Vector2(440f, 84f));
            Transform f = fuelPanel.transform.parent;
            Label(f, "FUEL", new Vector2(22f, -10f), 120f);
            _hoverValue = MoonUI.MakeText(f, "Hover", "", 18, TextAnchor.MiddleRight, MoonUI.TextDim, false, FontStyle.Bold);
            MoonUI.Place(_hoverValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-22f, -10f), new Vector2(260f, 26f));
            Image track = MoonUI.MakeImage(f, "Track", new Color(1f, 1f, 1f, 0.08f), MoonUI.RoundedSprite);
            MoonUI.Place(track.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -44f), new Vector2(300f, 22f));
            _fuelFillImg = MoonUI.MakeImage(track.transform, "Fill", MoonUI.Good, MoonUI.RoundedSprite);
            _fuelFill = _fuelFillImg.rectTransform;
            _fuelFill.anchorMin = Vector2.zero; _fuelFill.anchorMax = Vector2.one; _fuelFill.offsetMin = Vector2.zero; _fuelFill.offsetMax = Vector2.zero;
            _fuelText = MoonUI.MakeText(f, "FuelPct", "", 26, TextAnchor.MiddleRight, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_fuelText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -38f), new Vector2(100f, 34f));
        }

        void BuildScoreBar(Transform parent)
        {
            Image panel = MoonUI.MakePanel(parent, "ScoreBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(560f, 96f), MoonUI.Gold);
            Transform p = panel.transform.parent;

            _timeValue = MoonUI.MakeText(p, "Time", "00:00", 26, TextAnchor.MiddleLeft, MoonUI.TextDim, true, FontStyle.Bold);
            MoonUI.Place(_timeValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(130f, 40f));

            Text label = MoonUI.MakeText(p, "ScoreLabel", "SCORE", 16, TextAnchor.UpperCenter, MoonUI.Gold, true, FontStyle.Bold);
            MoonUI.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(240f, 22f));
            _scoreValue = MoonUI.MakeText(p, "Score", "0", 46, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_scoreValue.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(260f, 56f));

            _difficultyBadge = MoonUI.MakeText(p, "Difficulty", "", 20, TextAnchor.MiddleRight, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(_difficultyBadge.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(140f, 40f));

            _popupRoot = MoonUI.MakeRect("Popups", parent);
            MoonUI.Place(_popupRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(330f, -70f), new Vector2(10f, 10f));
        }

        void BuildGuidance(Transform parent)
        {
            RectTransform root = MoonUI.Place(MoonUI.MakeRect("Guidance", parent), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(900f, 118f));
            _guideRoot = root.gameObject;
            _guideGlow = MoonUI.MakeImage(root, "Glow", MoonUI.Accent, MoonUI.GlowSprite);
            MoonUI.Stretch(_guideGlow.rectTransform);
            _guideGlow.rectTransform.offsetMin = new Vector2(-26f, -26f);
            _guideGlow.rectTransform.offsetMax = new Vector2(26f, 26f);
            _guideBg = MoonUI.MakeImage(root, "Body", MoonUI.PanelColor, MoonUI.RoundedSprite);
            MoonUI.Stretch(_guideBg.rectTransform);
            Image sheen = MoonUI.MakeImage(root, "Sheen", new Color(1f, 1f, 1f, 0.08f), MoonUI.GradientSprite);
            MoonUI.Stretch(sheen.rectTransform);

            _guideIcon = MoonUI.MakeImage(root, "Icon", Color.white, MoonUI.ArrowSprite);
            MoonUI.Place(_guideIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64f, 0f), new Vector2(64f, 64f));

            _guidePrimary = MoonUI.MakeText(root, "Primary", "", 42, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_guidePrimary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -8f), new Vector2(780f, 58f));
            _guideSecondary = MoonUI.MakeText(root, "Secondary", "", 25, TextAnchor.MiddleCenter, MoonUI.TextDim, false, FontStyle.Bold);
            MoonUI.Place(_guideSecondary.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(40f, 10f), new Vector2(780f, 34f));

            _warning = MoonUI.MakeText(parent, "Warning", "", 34, TextAnchor.MiddleCenter, MoonUI.Danger, true, FontStyle.Bold);
            MoonUI.Place(_warning.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(820f, 48f));
        }

        void BuildRadar(Transform parent)
        {
            Image panel = MoonUI.MakePanel(parent, "Radar", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(290f, 318f));
            Transform p = panel.transform.parent;
            Text title = MoonUI.MakeText(p, "Title", "LANDING RADAR", 17, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(270f, 24f));

            Image disc = MoonUI.MakeImage(p, "Disc", new Color(0.05f, 0.25f, 0.35f, 0.45f), MoonUI.CircleSprite);
            MoonUI.Place(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(246f, 246f));
            var mask = disc.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            _radarSweep = MoonUI.MakeImage(disc.transform, "Sweep", new Color(0.3f, 0.9f, 1f, 0.22f), MoonUI.GradientSprite).rectTransform;
            MoonUI.Place(_radarSweep, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), Vector2.zero, new Vector2(123f, 123f));

            Image ring = MoonUI.MakeImage(disc.transform, "Ring", MoonUI.Accent, MoonUI.RingSprite);
            MoonUI.Stretch(ring.rectTransform);
            Image inner = MoonUI.MakeImage(disc.transform, "Ring50", MoonUI.AccentDim, MoonUI.RingSprite);
            MoonUI.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(123f, 123f));
            Image h = MoonUI.MakeImage(disc.transform, "CrossH", new Color(1f, 1f, 1f, 0.12f));
            MoonUI.Place(h.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(246f, 1.5f));
            Image v = MoonUI.MakeImage(disc.transform, "CrossV", new Color(1f, 1f, 1f, 0.12f));
            MoonUI.Place(v.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.5f, 246f));

            _radarVelocity = MoonUI.MakeImage(disc.transform, "Velocity", MoonUI.Warn).rectTransform;
            MoonUI.Place(_radarVelocity, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4f, 0f));
            _radarHeading = MoonUI.MakeImage(disc.transform, "Lander", Color.white, MoonUI.ArrowSprite).rectTransform;
            MoonUI.Place(_radarHeading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            _radarBlip = MoonUI.MakeImage(disc.transform, "LZ", MoonUI.Good, MoonUI.CircleSprite).rectTransform;
            MoonUI.Place(_radarBlip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            _radarText = MoonUI.MakeText(p, "Scale", "", 17, TextAnchor.MiddleCenter, MoonUI.TextDim);
            MoonUI.Place(_radarText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(270f, 24f));

            // small round buttons under the radar
            MoonUI.MakeButton(parent, "PauseButton", "II", new Vector2(1f, 1f), new Vector2(-64f, -392f), new Vector2(80f, 64f), 26,
                () => { if (_input != null) _input.PressPause(); });
            MoonUI.MakeButton(parent, "CameraButton", "CAM", new Vector2(1f, 1f), new Vector2(-164f, -392f), new Vector2(96f, 64f), 20,
                () => { if (_input != null) _input.PressCamera(); });
            MoonUI.MakeButton(parent, "SasButton", "SAS", new Vector2(1f, 1f), new Vector2(-274f, -392f), new Vector2(96f, 64f), 20,
                () => { if (_input != null) _input.PressSas(); });
            _sasText = MoonUI.MakeText(parent, "SasMode", "", 17, TextAnchor.MiddleRight, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(_sasText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -432f), new Vector2(320f, 26f));
        }

        void BuildInstruments(Transform parent)
        {
            // ---- attitude ball (bottom centre)
            Image panel = MoonUI.MakePanel(parent, "Attitude", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(60f, 24f), new Vector2(230f, 250f));
            Transform p = panel.transform.parent;
            Text title = MoonUI.MakeText(p, "Title", "ATTITUDE", 16, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(200f, 22f));

            Image maskImg = MoonUI.MakeImage(p, "Ball", Color.white, MoonUI.CircleSprite);
            MoonUI.Place(maskImg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(184f, 184f));
            var mask = maskImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _horizon = MoonUI.MakeRect("Horizon", maskImg.transform);
            MoonUI.Place(_horizon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 1200f));
            Image sky = MoonUI.MakeImage(_horizon, "Sky", new Color(0.04f, 0.10f, 0.22f, 1f));
            MoonUI.Place(sky.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(600f, 600f));
            Image ground = MoonUI.MakeImage(_horizon, "Ground", new Color(0.34f, 0.31f, 0.27f, 1f));
            MoonUI.Place(ground.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(600f, 600f));
            Image line = MoonUI.MakeImage(_horizon, "Line", Color.white);
            MoonUI.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 2f));
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                Image tick = MoonUI.MakeImage(_horizon, "Pitch" + (i * 10), new Color(1f, 1f, 1f, 0.5f));
                MoonUI.Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, i * 30f), new Vector2(i % 2 == 0 ? 60f : 30f, 1.5f));
            }
            Image ringImg = MoonUI.MakeImage(p, "BallRing", MoonUI.Accent, MoonUI.RingThickSprite);
            MoonUI.Place(ringImg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(188f, 188f));
            Image wing = MoonUI.MakeImage(p, "Aircraft", MoonUI.Warn);
            MoonUI.Place(wing.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(84f, 5f));
            Image dot = MoonUI.MakeImage(p, "Dot", MoonUI.Warn, MoonUI.CircleSprite);
            MoonUI.Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -124f), new Vector2(12f, 12f));
            _pitchRollText = MoonUI.MakeText(p, "Values", "", 17, TextAnchor.MiddleCenter, MoonUI.TextDim, true);
            MoonUI.Place(_pitchRollText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(220f, 24f));

            // ---- sink-rate gauge (left of the attitude ball)
            Image gauge = MoonUI.MakePanel(parent, "SinkGauge", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 24f), new Vector2(110f, 330f));
            Transform g = gauge.transform.parent;
            Text gt = MoonUI.MakeText(g, "Title", "SINK", 16, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(gt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(100f, 22f));
            RectTransform track = MoonUI.MakeImage(g, "Track", new Color(1f, 1f, 1f, 0.07f), MoonUI.RoundedSprite).rectTransform;
            MoonUI.Place(track, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(34f, SinkGaugeHeight));
            _sinkSafe = MoonUI.MakeImage(track, "Safe", new Color(0.35f, 1f, 0.55f, 0.25f)).rectTransform;
            _sinkSafe.anchorMin = new Vector2(0f, 1f); _sinkSafe.anchorMax = new Vector2(1f, 1f); _sinkSafe.pivot = new Vector2(0.5f, 1f);
            _sinkBand = MoonUI.MakeImage(track, "Target", new Color(0.3f, 0.85f, 1f, 0.55f)).rectTransform;
            _sinkBand.anchorMin = new Vector2(0f, 1f); _sinkBand.anchorMax = new Vector2(1f, 1f); _sinkBand.pivot = new Vector2(0.5f, 0.5f);
            _sinkMarker = MoonUI.MakeImage(track, "Marker", Color.white, MoonUI.ArrowSprite).rectTransform;
            MoonUI.Place(_sinkMarker, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-14f, 0f), new Vector2(22f, 22f));
            _sinkMarker.localRotation = Quaternion.Euler(0f, 0f, -90f);
            _sinkValue = MoonUI.MakeText(g, "Value", "", 22, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_sinkValue.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(110f, 30f));
        }

        void BuildLzMarker(Transform parent)
        {
            _lzMarker = MoonUI.MakeRect("LZMarker", parent);
            MoonUI.Place(_lzMarker, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            Image ring = MoonUI.MakeImage(_lzMarker, "Ring", MoonUI.Good, MoonUI.RingThickSprite);
            MoonUI.Stretch(ring.rectTransform);
            Image dot = MoonUI.MakeImage(_lzMarker, "Dot", MoonUI.Good, MoonUI.CircleSprite);
            MoonUI.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            _lzMarkerText = MoonUI.MakeText(_lzMarker, "Text", "TARGET", 20, TextAnchor.UpperCenter, MoonUI.Good, true, FontStyle.Bold);
            MoonUI.Place(_lzMarkerText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(220f, 28f));
        }

        void BuildTouchControls(Transform parent)
        {
            _touchGroup = MoonUI.Stretch(MoonUI.MakeRect("TouchControls", parent)).gameObject;
            Transform t = _touchGroup.transform;

            // ---- tilt joystick (bottom left)
            RectTransform stickRoot = MoonUI.Place(MoonUI.MakeRect("TiltStick", t), Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(230f, 210f), new Vector2(300f, 300f));
            Image stickGlow = MoonUI.MakeImage(stickRoot, "Glow", new Color(0.3f, 0.85f, 1f, 0.18f), MoonUI.SoftDotSprite);
            MoonUI.Stretch(stickGlow.rectTransform);
            stickGlow.rectTransform.offsetMin = new Vector2(-40f, -40f);
            stickGlow.rectTransform.offsetMax = new Vector2(40f, 40f);
            Image stickBase = stickRoot.gameObject.AddComponent<Image>();
            stickBase.sprite = MoonUI.CircleSprite;
            stickBase.color = new Color(0.02f, 0.08f, 0.12f, 0.55f);
            stickBase.raycastTarget = true;
            Image stickRing = MoonUI.MakeImage(stickRoot, "Ring", MoonUI.AccentDim, MoonUI.RingThickSprite);
            MoonUI.Stretch(stickRing.rectTransform);
            for (int i = 0; i < 4; i++)
            {
                Image chevron = MoonUI.MakeImage(stickRoot, "Chevron" + i, new Color(1f, 1f, 1f, 0.25f), MoonUI.ArrowSprite);
                Vector2 dir = Quaternion.Euler(0f, 0f, -90f * i) * Vector3.up;
                MoonUI.Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 120f, new Vector2(26f, 22f));
                chevron.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
            }
            _stickArrow = MoonUI.MakeImage(stickRoot, "GuideArrow", MoonUI.Good, MoonUI.ArrowSprite);
            MoonUI.Place(_stickArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            RectTransform knob = MoonUI.MakeImage(stickRoot, "Knob", MoonUI.Accent, MoonUI.CircleSprite).rectTransform;
            MoonUI.Place(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            Image knobRing = MoonUI.MakeImage(knob, "KnobRing", Color.white, MoonUI.RingThickSprite);
            MoonUI.Stretch(knobRing.rectTransform);
            _stick = stickRoot.gameObject.AddComponent<VirtualJoystick>();
            _stick.knob = knob;
            _stick.knobImage = knob.GetComponent<Image>();
            _stick.ringImage = stickRing;
            Text stickLabel = MoonUI.MakeText(stickRoot, "Label", "TILT", 20, TextAnchor.MiddleCenter, MoonUI.TextDim, true, FontStyle.Bold);
            MoonUI.Place(stickLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(200f, 26f));

            _yawLeft = MoonUI.MakeHoldButton(t, "YawLeft", "YAW", Vector2.zero, new Vector2(80f, 420f), new Vector2(100f, 64f), 18);
            _yawRight = MoonUI.MakeHoldButton(t, "YawRight", "YAW", Vector2.zero, new Vector2(380f, 420f), new Vector2(100f, 64f), 18);
            AddArrow(_yawLeft.transform, 90f, new Vector2(-34f, 0f));
            AddArrow(_yawRight.transform, -90f, new Vector2(34f, 0f));

            // ---- throttle lever (bottom right edge)
            RectTransform leverRoot = MoonUI.Place(MoonUI.MakeRect("ThrottleLever", t), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-92f, 40f), new Vector2(128f, 430f));
            _leverGlow = MoonUI.MakeImage(leverRoot, "Glow", new Color(0.3f, 0.85f, 1f, 0.15f), MoonUI.GlowSprite);
            MoonUI.Stretch(_leverGlow.rectTransform);
            _leverGlow.rectTransform.offsetMin = new Vector2(-26f, -26f);
            _leverGlow.rectTransform.offsetMax = new Vector2(26f, 26f);
            Image leverBody = leverRoot.gameObject.AddComponent<Image>();
            leverBody.sprite = MoonUI.RoundedSprite;
            leverBody.type = Image.Type.Sliced;
            leverBody.color = MoonUI.PanelColor;
            leverBody.raycastTarget = true;
            Text leverTitle = MoonUI.MakeText(leverRoot, "Title", "THROTTLE", 15, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(leverTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(128f, 22f));
            RectTransform leverTrack = MoonUI.MakeImage(leverRoot, "Track", new Color(1f, 1f, 1f, 0.08f), MoonUI.RoundedSprite).rectTransform;
            MoonUI.Place(leverTrack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(40f, 320f));
            Image leverFill = MoonUI.MakeImage(leverTrack, "Fill", MoonUI.Warn, MoonUI.RoundedSprite);
            RectTransform fillRt = leverFill.rectTransform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = new Vector2(1f, 0f); fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            RectTransform hoverMark = MoonUI.MakeImage(leverTrack, "HoverMark", MoonUI.Good).rectTransform;
            MoonUI.Place(hoverMark, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 4f));
            Text hoverLabel = MoonUI.MakeText(hoverMark, "HoverLabel", "HOVER", 13, TextAnchor.MiddleRight, MoonUI.Good, true, FontStyle.Bold);
            MoonUI.Place(hoverLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(70f, 18f));
            RectTransform handle = MoonUI.MakeImage(leverTrack, "Handle", Color.white, MoonUI.RoundedSprite).rectTransform;
            MoonUI.Place(handle, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 34f));
            Image handleLine = MoonUI.MakeImage(handle, "Line", MoonUI.Accent);
            MoonUI.Place(handleLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 4f));
            _leverArrow = MoonUI.MakeImage(leverRoot, "GuideArrow", MoonUI.Good, MoonUI.ArrowSprite);
            MoonUI.Place(_leverArrow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(54f, 54f));
            _leverText = MoonUI.MakeText(leverRoot, "Value", "0%", 24, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_leverText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(128f, 32f));
            _lever = leverRoot.gameObject.AddComponent<ThrottleLever>();
            _lever.track = leverTrack;
            _lever.handle = handle;
            _lever.fill = fillRt;
            _lever.hoverMarker = hoverMark;

            // ---- THRUST (hold) + CUT
            _thrust = MoonUI.MakeHoldButton(t, "Thrust", "THRUST", new Vector2(1f, 0f), new Vector2(-330f, 170f), new Vector2(230f, 230f), 34, MoonUI.Warn, true);
            Text hold = MoonUI.MakeText(_thrust.transform, "Hold", "HOLD", 17, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), true, FontStyle.Bold);
            MoonUI.Place(hold.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(160f, 24f));
            _cutButton = MoonUI.MakeButton(t, "Cut", "CUT", new Vector2(1f, 0f), new Vector2(-330f, 350f), new Vector2(150f, 70f), 28,
                () => { if (_input != null) _input.PressCut(); }, MoonUI.Danger);
            _cutGlow = _cutButton.transform.Find("Glow").GetComponent<Image>();
        }

        static void AddArrow(Transform parent, float rotation, Vector2 pos)
        {
            Image a = MoonUI.MakeImage(parent, "Arrow", Color.white, MoonUI.ArrowSprite);
            MoonUI.Place(a.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(18f, 18f));
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        void BuildCountdown()
        {
            _countdownGroup = MoonUI.Stretch(MoonUI.MakeRect("Countdown", _safe)).gameObject;
            _countdownRing = MoonUI.MakeImage(_countdownGroup.transform, "Ring", MoonUI.Accent, MoonUI.RingThickSprite);
            MoonUI.Place(_countdownRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(300f, 300f));
            _countdownText = MoonUI.MakeText(_countdownGroup.transform, "Number", "3", 170, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(400f, 240f));
            Text sub = MoonUI.MakeText(_countdownGroup.transform, "Sub", "POWERED DESCENT IN", 34, TextAnchor.MiddleCenter, MoonUI.Accent, true, FontStyle.Bold);
            MoonUI.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(900f, 50f));
        }

        void BuildBriefing()
        {
            Image dim = MoonUI.MakeImage(_safe, "Briefing", new Color(0f, 0.01f, 0.03f, 0.55f));
            dim.raycastTarget = true;
            MoonUI.Stretch(dim.rectTransform);
            _briefing = dim.gameObject;
            Transform b = dim.transform;

            Text title = MoonUI.MakeText(b, "Title", "MOON LANDING CHALLENGE", 70, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1800f, 90f));
            Text sub = MoonUI.MakeText(b, "Subtitle", "APOLLO LUNAR MODULE  ·  POWERED DESCENT  ·  SEA OF TRANQUILITY", 24, TextAnchor.MiddleCenter, MoonUI.Accent, true);
            MoonUI.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1800f, 34f));

            Text how = MoonUI.MakeText(b, "HowTo",
                "Slow your fall with <b>THRUST</b>, tilt with the <b>stick</b> to stop drifting, and touch down gently (under ~2 m/s). No air on the Moon: only your engine slows you down.",
                24, TextAnchor.MiddleCenter, new Color(0.88f, 0.92f, 0.96f));
            MoonUI.Place(how.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(1500f, 70f));

            int n = game.difficulties.Length;
            _cardGlows = new Image[n];
            for (int i = 0; i < n; i++)
            {
                int index = i;
                DifficultyPreset p = game.difficulties[i];
                Color tint = i == 0 ? MoonUI.Good : (i == 1 ? MoonUI.Accent : MoonUI.Danger);
                float x = (i - (n - 1) * 0.5f) * 500f;
                Button card = MoonUI.MakeButton(b, "Card_" + p.name, "", new Vector2(0.5f, 0.5f), new Vector2(x, -10f), new Vector2(460f, 360f), 10,
                    () => game.SelectDifficulty(index), tint);
                _cardGlows[i] = card.transform.Find("Glow").GetComponent<Image>();
                Transform c = card.transform;

                Text name = MoonUI.MakeText(c, "Name", p.name, 46, TextAnchor.MiddleCenter, tint, true, FontStyle.Bold);
                MoonUI.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(440f, 60f));

                string target = p.requireLandingZone ? "MUST HIT THE TARGET" : "LAND ANYWHERE SAFE";
                string help = p.guidance == GuidanceLevel.Full ? "FULL GUIDANCE" : (p.guidance == GuidanceLevel.Hints ? "WARNINGS ONLY" : "NO HELP");
                string sas = p.sas == SasMode.AutoLevel ? "AUTO-LEVEL" : (p.sas == SasMode.RateDamp ? "STABILISER" : "MANUAL");
                string badges = string.Format("{0}\n{1}  ·  {2}\nFUEL {3:0} kg  ·  SCORE ×{4:0.0}", target, help, sas, p.fuel, p.scoreMultiplier);
                Text badge = MoonUI.MakeText(c, "Badges", badges, 22, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
                MoonUI.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(430f, 100f));

                Text desc = MoonUI.MakeText(c, "Description", p.description, 22, TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 0.95f), false);
                MoonUI.Place(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(410f, 130f));
            }

            MoonUI.MakeButton(b, "Start", "START MISSION", new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 104f), 40, () => game.BeginCountdown(), MoonUI.Good);
            Text best = MoonUI.MakeText(b, "Best", "", 24, TextAnchor.MiddleCenter, MoonUI.Gold, true, FontStyle.Bold);
            MoonUI.Place(best.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 34f));
            best.text = MoonLandingEvents.BestScore > 0 ? "BEST SCORE  " + MoonLandingEvents.BestScore.ToString("N0") : "";
        }

        void BuildResult()
        {
            Image dim = MoonUI.MakeImage(_safe, "Result", new Color(0f, 0.01f, 0.03f, 0.5f));
            dim.raycastTarget = true;
            MoonUI.Stretch(dim.rectTransform);
            _result = dim.gameObject;

            Image panel = MoonUI.MakePanel(dim.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 960f));
            Transform p = panel.transform.parent;

            _resultTitle = MoonUI.MakeText(p, "Title", "", 64, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(_resultTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1020f, 80f));
            _resultReason = MoonUI.MakeText(p, "Reason", "", 25, TextAnchor.MiddleCenter, new Color(0.88f, 0.92f, 0.96f));
            MoonUI.Place(_resultReason.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -106f), new Vector2(960f, 66f));

            _stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                _stars[i] = MoonUI.MakeImage(p, "Star" + i, Color.white, MoonUI.StarSprite);
                MoonUI.Place(_stars[i].rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 110f, -222f + (i == 1 ? 14f : 0f)), new Vector2(i == 1 ? 108f : 88f, i == 1 ? 108f : 88f));
            }

            _resultScore = MoonUI.MakeText(p, "Score", "0", 84, TextAnchor.MiddleCenter, MoonUI.Gold, true, FontStyle.Bold);
            MoonUI.Place(_resultScore.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(900f, 100f));
            _resultBest = MoonUI.MakeText(p, "Best", "", 24, TextAnchor.MiddleCenter, MoonUI.TextDim, true, FontStyle.Bold);
            MoonUI.Place(_resultBest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -384f), new Vector2(900f, 32f));

            _breakdownRoot = MoonUI.MakeRect("Breakdown", p);
            MoonUI.Place(_breakdownRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -430f), new Vector2(760f, 360f));
            for (int i = 0; i < 10; i++)
            {
                Text l = MoonUI.MakeText(_breakdownRoot, "L" + i, "", 25, TextAnchor.MiddleLeft, new Color(0.88f, 0.92f, 0.96f), false, FontStyle.Bold);
                MoonUI.Place(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * 36f), new Vector2(560f, 34f));
                Text v = MoonUI.MakeText(_breakdownRoot, "V" + i, "", 25, TextAnchor.MiddleRight, Color.white, true, FontStyle.Bold);
                MoonUI.Place(v.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -i * 36f), new Vector2(200f, 34f));
                _breakdownRows.Add(new[] { l, v });
            }

            MoonUI.MakeButton(p, "Retry", "RETRY", new Vector2(0.5f, 0f), new Vector2(-330f, 70f), new Vector2(290f, 90f), 32, () => game.Retry(), MoonUI.Good);
            MoonUI.MakeButton(p, "Briefing", "DIFFICULTY", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(290f, 90f), 28, () => game.BackToBriefing());
            MoonUI.MakeButton(p, "Hub", "MAIN MENU", new Vector2(0.5f, 0f), new Vector2(330f, 70f), new Vector2(290f, 90f), 28, () => game.ExitToHub());
        }

        void BuildPause()
        {
            Image dim = MoonUI.MakeImage(_safe, "Pause", new Color(0f, 0.01f, 0.03f, 0.7f));
            dim.raycastTarget = true;
            MoonUI.Stretch(dim.rectTransform);
            _pause = dim.gameObject;
            Text t = MoonUI.MakeText(dim.transform, "Title", "PAUSED", 90, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            MoonUI.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 240f), new Vector2(800f, 120f));
            MoonUI.MakeButton(dim.transform, "Resume", "RESUME", new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(440f, 96f), 36, () => game.Resume(), MoonUI.Good);
            MoonUI.MakeButton(dim.transform, "Restart", "RESTART", new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(440f, 96f), 32, () => game.Retry());
            MoonUI.MakeButton(dim.transform, "Difficulty", "DIFFICULTY", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(440f, 96f), 32, () => game.BackToBriefing());
            MoonUI.MakeButton(dim.transform, "Hub", "MAIN MENU", new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(440f, 96f), 32, () => game.ExitToHub());
        }

        // =================================================================== STATE
        void OnStateChanged(MissionState state)
        {
            if (_safe == null) return;
            _briefing.SetActive(state == MissionState.Briefing);
            _pause.SetActive(state == MissionState.Paused);
            _countdownGroup.SetActive(state == MissionState.Countdown);
            _result.SetActive(false);
            _flightGroup.SetActive(state != MissionState.Briefing);
            if (state == MissionState.Countdown || state == MissionState.Briefing) _displayScore = 0f;
            if (_difficultyBadge != null) _difficultyBadge.text = game.Difficulty.name;
        }

        void OnResult(MoonLandingResult r)
        {
            _shownResult = r;
            _result.SetActive(true);
            _resultStartTime = Time.unscaledTime;

            Color titleColor;
            switch (r.outcome)
            {
                case LandingOutcome.Success: _resultTitle.text = "MISSION SUCCESS"; titleColor = MoonUI.Good; break;
                case LandingOutcome.HardLanding: _resultTitle.text = "HARD LANDING"; titleColor = MoonUI.Warn; break;
                case LandingOutcome.MissedTarget: _resultTitle.text = "MISSED THE TARGET"; titleColor = MoonUI.Warn; break;
                case LandingOutcome.TippedOver: _resultTitle.text = "LANDER TIPPED OVER"; titleColor = MoonUI.Danger; break;
                case LandingOutcome.OutOfBounds: _resultTitle.text = "MISSION ABORTED"; titleColor = MoonUI.Warn; break;
                default: _resultTitle.text = "MISSION FAILED"; titleColor = MoonUI.Danger; break;
            }
            _resultTitle.color = titleColor;
            _resultReason.text = r.reason;
            _resultBest.text = r.newBest ? "NEW BEST SCORE!" : "BEST  " + MoonLandingEvents.BestScore.ToString("N0");
            _resultBest.color = r.newBest ? MoonUI.Gold : MoonUI.TextDim;

            List<ScoreLine> lines = game.ScoreBreakdown;
            for (int i = 0; i < _breakdownRows.Count; i++)
            {
                bool has = lines != null && i < lines.Count;
                _breakdownRows[i][0].text = has ? lines[i].label : "";
                _breakdownRows[i][1].text = has ? (lines[i].points >= 0 ? "+" : "") + lines[i].points.ToString("N0") : "";
                _breakdownRows[i][1].color = has && lines[i].points < 0 ? MoonUI.Danger : Color.white;
                _breakdownRows[i][0].color = new Color(1f, 1f, 1f, 0f);
                _breakdownRows[i][1].color = new Color(_breakdownRows[i][1].color.r, _breakdownRows[i][1].color.g, _breakdownRows[i][1].color.b, 0f);
            }
        }

        void ShowCallout(string text, Color color)
        {
            if (_callout == null) return;
            _callout.text = text;
            _callout.color = color;
            _calloutTimer = 2.6f;
        }

        void OnPoints(int points, string label)
        {
            if (_popupRoot == null) return;
            Popup p = null;
            foreach (Popup existing in _popups) if (existing.age >= 1.6f) { p = existing; break; }
            if (p == null)
            {
                Text t = MoonUI.MakeText(_popupRoot, "Popup", "", 26, TextAnchor.MiddleLeft, MoonUI.Gold, true, FontStyle.Bold);
                MoonUI.Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(520f, 34f));
                p = new Popup { text = t };
                _popups.Add(p);
            }
            p.age = 0f;
            p.text.text = "+" + points + "  " + label;
            p.text.gameObject.SetActive(true);
        }

        // =================================================================== UPDATE
        void Update()
        {
            if (_safe == null || _lander == null) return;
            float dt = Time.unscaledDeltaTime;
            if (Screen.safeArea != _lastSafeArea) ApplySafeArea();

            bool flying = game.State == MissionState.Flying;
            if (_input != null)
            {
                if (_input.ConsumeToggleButtons()) showTouchControls = !showTouchControls;
                _touchGroup.SetActive(showTouchControls && (flying || game.State == MissionState.Countdown));
                _input.uiThrust = _thrust.IsHeld;
                _input.uiYawLeft = _yawLeft.IsHeld;
                _input.uiYawRight = _yawRight.IsHeld;
                _input.uiStick = _stick.Value;
                _input.uiThrottleLever = _lever.IsDragging ? _lever.Value : -1f;
            }

            // callout
            if (_calloutTimer > 0f)
            {
                _calloutTimer -= dt;
                Color c = _callout.color;
                c.a = Mathf.Clamp01(_calloutTimer / 0.5f);
                _callout.color = c;
                float pop = Mathf.Clamp01((2.6f - _calloutTimer) * 6f);
                _callout.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, pop);
            }

            // popups
            for (int i = 0; i < _popups.Count; i++)
            {
                Popup p = _popups[i];
                if (p.age >= 1.6f) { if (p.text.gameObject.activeSelf) p.text.gameObject.SetActive(false); continue; }
                p.age += dt;
                p.text.rectTransform.anchoredPosition = new Vector2(0f, p.age * 40f);
                Color c = p.text.color;
                c.a = Mathf.Clamp01(1.6f - p.age);
                p.text.color = c;
            }

            if (_briefing.activeSelf) UpdateBriefing();
            if (_countdownGroup.activeSelf)
            {
                float remain = game.CountdownRemaining;
                _countdownText.text = Mathf.Max(1, Mathf.CeilToInt(remain)).ToString();
                float frac = remain - Mathf.Floor(remain);
                _countdownRing.rectTransform.localScale = Vector3.one * (0.8f + frac * 0.5f);
                _countdownRing.color = new Color(MoonUI.Accent.r, MoonUI.Accent.g, MoonUI.Accent.b, frac);
            }
            if (_flightGroup.activeSelf) UpdateFlight(dt);
            if (_result.activeSelf) UpdateResult();
        }

        void UpdateBriefing()
        {
            for (int i = 0; i < _cardGlows.Length; i++)
            {
                bool selected = i == game.DifficultyIndex;
                Color tint = i == 0 ? MoonUI.Good : (i == 1 ? MoonUI.Accent : MoonUI.Danger);
                float a = selected ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f) : 0.08f;
                _cardGlows[i].color = new Color(tint.r, tint.g, tint.b, a);
                _cardGlows[i].transform.parent.localScale = Vector3.one * (selected ? 1.04f : 0.96f);
            }
        }

        void UpdateFlight(float dt)
        {
            LanderController l = _lander;
            float alt = l.Altitude;
            float vs = l.VerticalSpeed;
            bool flying = game.State == MissionState.Flying;

            // ---- telemetry
            _altValue.text = alt < 100f ? string.Format("{0:0.0} <size=30>m</size>", alt) : string.Format("{0:0} <size=30>m</size>", alt);
            float sink = -vs;
            Color vsColor = sink <= _evaluator.safeVerticalSpeed ? MoonUI.Good : (sink <= _evaluator.crashVerticalSpeed ? MoonUI.Warn : (alt < 60f ? MoonUI.Danger : Color.white));
            _vsValue.text = string.Format("{0:0.0} <size=18>m/s</size>", Mathf.Abs(vs));
            _vsValue.color = vsColor;
            _vsArrow.color = vsColor;
            _vsArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, vs >= 0f ? 0f : 180f);
            _hsValue.text = string.Format("{0:0.0} <size=18>m/s</size>", l.HorizontalSpeed);
            _hsValue.color = l.HorizontalSpeed <= _evaluator.safeHorizontalSpeed ? MoonUI.Good : (l.HorizontalSpeed <= _evaluator.crashHorizontalSpeed ? MoonUI.Warn : Color.white);
            _tiltValue.text = string.Format("{0:0}°", l.TiltDegrees);
            _tiltValue.color = l.TiltDegrees <= _evaluator.safeTilt ? MoonUI.Good : (l.TiltDegrees <= _evaluator.tipOverTilt ? MoonUI.Warn : MoonUI.Danger);
            float range = _zone != null ? _zone.HorizontalDistance(l.transform.position) : 0f;
            _rangeValue.text = string.Format("{0:0} m", range);
            _rangeValue.color = _zone != null && range <= _zone.radius ? MoonUI.Good : Color.white;

            _fuelFill.anchorMax = new Vector2(Mathf.Clamp01(l.FuelFraction), 1f);
            _fuelFillImg.color = l.FuelFraction > 0.25f ? MoonUI.Good : (l.FuelFraction > 0.1f ? MoonUI.Warn : MoonUI.Danger);
            _fuelText.text = string.Format("{0:0}%", l.FuelFraction * 100f);
            _hoverValue.text = string.Format("HOVER TIME {0:0} s", l.HoverSecondsLeft);

            // ---- score & time
            int target = _score != null ? (game.State == MissionState.Ended && game.ResultVisible ? game.LastResult.score : _score.LiveScore) : 0;
            _displayScore = Mathf.MoveTowards(_displayScore, target, Mathf.Max(40f, Mathf.Abs(target - _displayScore) * 4f) * dt);
            _scoreValue.text = Mathf.RoundToInt(_displayScore).ToString("N0");
            _timeValue.text = FormatTime(game.MissionTime);

            // ---- guidance + warnings
            GuidanceAdvice advice = _guidance != null ? _guidance.Current : new GuidanceAdvice();
            UpdateGuidance(advice, flying);

            string warning = _evaluator.ActiveWarning;
            bool alarm = flying && _evaluator.MasterAlarm;
            bool flash = Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.6f;
            bool showWarning = flying && !string.IsNullOrEmpty(warning) && (!advice.HasAdvice || alarm);
            _warning.text = showWarning && (flash || !alarm) ? (alarm ? "MASTER ALARM  ·  " + warning : warning) : "";
            _warning.color = alarm ? MoonUI.Danger : MoonUI.Warn;
            float edge = alarm ? 0.25f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) : 0f;
            foreach (Image e in _alarmEdges) e.color = new Color(1f, 0.12f, 0.08f, Mathf.MoveTowards(e.color.a, edge, dt * 3f));

            if (_sasText != null) _sasText.text = "SAS  " + (l.sasMode == SasMode.Off ? "OFF" : (l.sasMode == SasMode.RateDamp ? "STABILISER" : "AUTO-LEVEL"));

            // ---- touch control feedback
            if (_touchGroup.activeSelf)
            {
                _lever.Show(l.ThrottleSetting, l.Throttle, l.HoverThrottle);
                _leverText.text = string.Format("{0:0}%", (_thrust.IsHeld ? 1f : l.Throttle) * 100f);
                bool guideOn = flying && advice.HasAdvice;
                MoonUI.Pulse(_thrust.glow, guideOn && advice.highlightThrust, MoonUI.Warn, 0.25f);
                MoonUI.Pulse(_leverGlow, guideOn && (advice.highlightRelease || advice.highlightThrust), advice.highlightRelease ? MoonUI.Good : MoonUI.Warn, 0.12f);
                _leverArrow.enabled = guideOn && (advice.highlightRelease || advice.highlightThrust);
                _leverArrow.color = advice.highlightRelease ? MoonUI.Good : MoonUI.Warn;
                _leverArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, advice.highlightRelease ? 180f : 0f);
                _leverArrow.rectTransform.anchoredPosition = new Vector2(0f, 40f + 8f * Mathf.Sin(Time.unscaledTime * 8f));
                MoonUI.Pulse(_cutGlow, guideOn && advice.highlightCut, MoonUI.Danger, 0.25f);

                bool stickHint = guideOn && advice.stickDirection.sqrMagnitude > 0.01f && !_stick.IsHeld;
                _stickArrow.enabled = stickHint;
                if (stickHint)
                {
                    float angle = Mathf.Atan2(advice.stickDirection.x, advice.stickDirection.y) * Mathf.Rad2Deg;
                    _stickArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
                    _stickArrow.rectTransform.anchoredPosition = advice.stickDirection.normalized * (70f + 10f * Mathf.Sin(Time.unscaledTime * 8f));
                    _stickArrow.color = new Color(MoonUI.Good.r, MoonUI.Good.g, MoonUI.Good.b, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f));
                }
            }

            UpdateSinkGauge(sink, advice);
            UpdateAttitude(l);
            UpdateRadar(l);
            UpdateLzMarker(l);
        }

        void UpdateGuidance(GuidanceAdvice advice, bool flying)
        {
            bool show = flying && advice.HasAdvice;
            if (_guideRoot.activeSelf != show) _guideRoot.SetActive(show);
            if (!show) return;

            Color c;
            switch (advice.severity)
            {
                case GuidanceSeverity.Good: c = MoonUI.Good; break;
                case GuidanceSeverity.Caution: c = MoonUI.Warn; break;
                case GuidanceSeverity.Urgent: c = MoonUI.Danger; break;
                default: c = MoonUI.Accent; break;
            }
            float pulse = advice.severity == GuidanceSeverity.Urgent ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 0.6f;
            _guideGlow.color = new Color(c.r, c.g, c.b, 0.45f * pulse);
            _guideBg.color = new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f, 0.88f);
            _guidePrimary.color = Color.Lerp(Color.white, c, 0.35f);
            _guidePrimary.text = advice.primary;

            string secondary = advice.secondary ?? "";
            if (!_mobile || !showTouchControls)
            {
                if (advice.highlightCut) secondary += "   [X]";
                else if (advice.highlightThrust) secondary += "   [hold SPACE / SHIFT]";
                else if (advice.highlightRelease) secondary += "   [release SPACE / CTRL]";
                else if (advice.stickDirection.sqrMagnitude > 0.01f) secondary += "   [W A S D]";
            }
            _guideSecondary.text = secondary;

            // icon: arrow up = more thrust, down = release, sideways = tilt, circle = good
            bool arrow = advice.highlightThrust || advice.highlightRelease || advice.stickDirection.sqrMagnitude > 0.01f;
            _guideIcon.sprite = arrow ? MoonUI.ArrowSprite : MoonUI.RingThickSprite;
            _guideIcon.color = c;
            float rot = 0f;
            if (advice.highlightRelease) rot = 180f;
            else if (!advice.highlightThrust && advice.stickDirection.sqrMagnitude > 0.01f)
                rot = -Mathf.Atan2(advice.stickDirection.x, advice.stickDirection.y) * Mathf.Rad2Deg;
            _guideIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot);
            _guideIcon.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 7f));
        }

        static float SinkY(float sink)
        {
            return -Mathf.Clamp(sink, -1f, SinkGaugeMax) / SinkGaugeMax * (SinkGaugeHeight - 12f) - 6f;
        }

        void UpdateSinkGauge(float sink, GuidanceAdvice advice)
        {
            float safe = _evaluator.safeVerticalSpeed;
            _sinkSafe.anchoredPosition = new Vector2(0f, SinkY(0f));
            _sinkSafe.sizeDelta = new Vector2(0f, Mathf.Abs(SinkY(safe) - SinkY(0f)));
            float targetSink = advice.targetSinkRate > 0f ? advice.targetSinkRate : (_guidance != null ? _guidance.TargetSinkRate(_lander.Altitude) : safe);
            bool showBand = _guidance != null && _guidance.level == GuidanceLevel.Full;
            _sinkBand.gameObject.SetActive(showBand);
            if (showBand)
            {
                _sinkBand.anchoredPosition = new Vector2(0f, SinkY(targetSink));
                _sinkBand.sizeDelta = new Vector2(10f, Mathf.Abs(SinkY(targetSink + 1.1f) - SinkY(targetSink - 1.1f)));
            }
            _sinkMarker.anchoredPosition = new Vector2(-16f, SinkY(sink));
            Color c = sink <= safe ? MoonUI.Good : (sink <= _evaluator.crashVerticalSpeed ? MoonUI.Warn : MoonUI.Danger);
            _sinkMarker.GetComponent<Image>().color = c;
            _sinkValue.text = string.Format("{0:0.0}", sink);
            _sinkValue.color = c;
        }

        void UpdateAttitude(LanderController l)
        {
            Transform t = l.transform;
            float pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(t.forward, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
            float roll = Mathf.Asin(Mathf.Clamp(Vector3.Dot(t.right, Vector3.up), -1f, 1f)) * Mathf.Rad2Deg;
            _horizon.localRotation = Quaternion.Euler(0f, 0f, -roll);
            _horizon.anchoredPosition = (Vector2)(Quaternion.Euler(0f, 0f, -roll) * new Vector3(0f, -pitch * 3f, 0f));
            _pitchRollText.text = string.Format("P {0:+0;-0}°  R {1:+0;-0}°", -pitch, -roll);
        }

        void UpdateRadar(LanderController l)
        {
            if (viewCamera == null) return;
            _radarSweep.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 120f);
            Vector3 fwd = viewCamera.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = viewCamera.transform.up;
            fwd.y = 0f;
            fwd.Normalize();
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            const float radius = 123f;

            if (_zone != null)
            {
                Vector3 off = _zone.transform.position - l.transform.position;
                Vector2 p = new Vector2(Vector3.Dot(off, right), Vector3.Dot(off, fwd)) / radarRange * radius;
                if (p.magnitude > radius - 8f) p = p.normalized * (radius - 8f);
                _radarBlip.anchoredPosition = p;
                _radarBlip.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f));
            }

            Vector3 v = l.HorizontalVelocity;
            Vector2 vp = new Vector2(Vector3.Dot(v, right), Vector3.Dot(v, fwd));
            _radarVelocity.sizeDelta = new Vector2(4f, Mathf.Min(radius, vp.magnitude * 6f));
            _radarVelocity.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(vp.x, vp.y) * Mathf.Rad2Deg);

            Vector3 lf = l.transform.forward; lf.y = 0f;
            _radarHeading.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(Vector3.Dot(lf, right), Vector3.Dot(lf, fwd)) * Mathf.Rad2Deg);
            _radarText.text = string.Format("ring {0:0} m  ·  target {1:0} m", radarRange * 0.5f, _zone != null ? _zone.radius : 0f);
        }

        void UpdateLzMarker(LanderController l)
        {
            if (_zone == null || viewCamera == null) { _lzMarker.gameObject.SetActive(false); return; }
            Vector3 sp = viewCamera.WorldToScreenPoint(_zone.transform.position + Vector3.up * 1.5f);
            if (sp.z < 0f) { sp.x = Screen.width - sp.x; sp.y = 0f; }
            const float margin = 60f;
            sp.x = Mathf.Clamp(sp.x, margin, Screen.width - margin);
            sp.y = Mathf.Clamp(sp.y, margin, Screen.height - margin);
            _lzMarker.gameObject.SetActive(game.State == MissionState.Flying);
            _lzMarker.position = new Vector3(sp.x, sp.y, 0f);
            _lzMarker.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f));
            float d = _zone.HorizontalDistance(l.transform.position);
            _lzMarkerText.text = d <= _zone.radius ? "ON TARGET" : string.Format("TARGET {0:0} m", d);
        }

        void UpdateResult()
        {
            float t = Time.unscaledTime - _resultStartTime;

            // stars pop in one by one
            for (int i = 0; i < _stars.Length; i++)
            {
                bool earned = i < _shownResult.stars;
                float k = Mathf.Clamp01((t - 0.3f - i * 0.25f) * 5f);
                float overshoot = k < 1f ? Mathf.Sin(k * Mathf.PI) * 0.35f : 0f;
                _stars[i].rectTransform.localScale = Vector3.one * (earned ? k + overshoot : 1f);
                _stars[i].color = earned ? MoonUI.Gold : new Color(1f, 1f, 1f, 0.12f);
            }

            // breakdown lines fade in, score counts up
            List<ScoreLine> lines = game.ScoreBreakdown;
            int count = lines != null ? Mathf.Min(lines.Count, _breakdownRows.Count) : 0;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Clamp01((t - 0.6f - i * 0.18f) * 4f);
                Text l = _breakdownRows[i][0], v = _breakdownRows[i][1];
                l.color = new Color(0.88f, 0.92f, 0.96f, a);
                Color vc = lines[i].points < 0 ? MoonUI.Danger : Color.white;
                v.color = new Color(vc.r, vc.g, vc.b, a);
            }
            float countUp = Mathf.Clamp01((t - 0.6f) / Mathf.Max(0.8f, count * 0.18f + 0.6f));
            countUp = 1f - Mathf.Pow(1f - countUp, 3f);
            _resultScore.text = Mathf.RoundToInt(_shownResult.score * countUp).ToString("N0");
            _resultScore.rectTransform.localScale = Vector3.one * (countUp >= 1f ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 4f) : 1f);
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return string.Format("{0:00}:{1:00}", s / 60, s % 60);
        }
    }
}
