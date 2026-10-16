using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Orchestrates the whole AR experience: place, explore, quiz, burn, return.
    ///
    /// Every subsystem here is independently testable and knows nothing about the
    /// others. This class is the only place that knows the order they run in, which
    /// is what keeps the flow readable and the pieces replaceable.
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        public enum Phase { Placing, Exploring, Quiz, Finale, Done }

        [Header("Systems")]
        public ARPlacementController placement;
        public ModelScaleController scaleController;
        public TouchSelector selector;
        public TutorialDirector tutorial;
        public QuizDirector quiz;
        public TEISequence finale;

        [Header("UI")]
        public PartInfoSheet infoSheet;
        public ChecklistPanel checklist;
        public ProgressRing progressRing;
        public CanvasGroup explorerHud;
        public GameObject placementHud;
        public TMP_Text coachText;
        public TMP_Text scaleLabel;
        public Slider scaleSlider;
        public TouchButton placeButton;
        public TouchButton checklistButton;
        public TouchButton repositionButton;
        public TouchButton continueButton;
        public TouchButton soundButton;
        public GameObject scaleBar;
        public List<TouchButton> presetButtons = new List<TouchButton>();
        public TouchButton cabinButton;
        public TMP_Text cabinButtonLabel;
        public TouchButton ringButton;

        [Header("Scene")]
        public Transform contentRoot;
        public GameObject lunarSurface;
        public GameObject orbitBackdrop;
        public CabinView cabinView;

        public Phase Current { get; private set; } = Phase.Placing;

        void Start()
        {
            WireUI();
            WireSystems();

            SetPhase(Phase.Placing);
            AudioDirector.SetAmbience("amb_orbit", 2f);

            // A device without ARCore still gets the whole experience, just without
            // camera passthrough. Better than a dead end at an exhibition.
            if (AppDirector.Instance != null && AppDirector.Instance.ARUnavailable)
                StartCoroutine(SkipPlacementNextFrame());
        }

        bool _orbitFallbackStarted;

        IEnumerator SkipPlacementNextFrame()
        {
            if (_orbitFallbackStarted) yield break;
            _orbitFallbackStarted = true;
            yield return null;

            if (contentRoot != null)
            {
                contentRoot.gameObject.SetActive(true);

                // Frame the tabletop-scale model in front of the camera, centred
                // vertically, facing the viewer.
                var cam = placement != null && placement.arCamera != null ? placement.arCamera : Camera.main;
                Vector3 origin = cam != null ? cam.transform.position : Vector3.zero;
                Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                forward.Normalize();

                contentRoot.position = origin + forward * 1.05f + Vector3.down * 0.22f;
                contentRoot.rotation = Quaternion.LookRotation(-forward, Vector3.up);
            }
            if (placementHud != null) placementHud.SetActive(false);
            // Re-placing needs a camera feed and a floor; there is neither here.
            if (repositionButton != null) repositionButton.gameObject.SetActive(false);
            BeginExploring();
            ShowCoach("Orbit view — drag to rotate, pinch or scroll to zoom, tap a part to study it", 6f);
        }

        // ---------------------------------------------------------------
        // Wiring
        // ---------------------------------------------------------------

        void WireUI()
        {
            if (placeButton != null)
                placeButton.onClick.AddListener(() =>
                {
                    if (placement != null && placement.TryPlace()) BeginExploring();
                });

            if (checklistButton != null)
                checklistButton.onClick.AddListener(() => { if (checklist != null) checklist.Toggle(); });

            if (repositionButton != null)
                repositionButton.onClick.AddListener(() =>
                {
                    if (placement == null) return;
                    if (infoSheet != null) infoSheet.Hide();
                    if (checklist != null) checklist.Close();
                    placement.Reposition();
                    SetPhase(Phase.Placing);
                });

            if (continueButton != null)
            {
                continueButton.onClick.AddListener(BeginQuiz);
                continueButton.gameObject.SetActive(false);
            }

            if (soundButton != null)
                soundButton.onClick.AddListener(ToggleSound);

            if (scaleSlider != null)
            {
                scaleSlider.minValue = 0f;
                scaleSlider.maxValue = 1f;
                scaleSlider.onValueChanged.AddListener(OnScaleSlider);
            }

            if (infoSheet != null)
            {
                infoSheet.Closed += OnSheetClosed;
                infoSheet.NextRequested += GoToNextSuggested;
            }

            if (checklist != null)
                checklist.RowTapped += OnChecklistRowTapped;

            // Listeners added in the Editor while generating the scene are not
            // serialised, so every button is wired here at runtime instead.
            for (int i = 0; i < presetButtons.Count; i++)
            {
                int preset = i;
                if (presetButtons[i] != null)
                    presetButtons[i].onClick.AddListener(() => ApplyScalePreset(preset));
            }

            if (cabinButton != null)
                cabinButton.onClick.AddListener(() => { if (cabinView != null) cabinView.Toggle(); });
            if (cabinView != null)
                cabinView.Changed += OnCabinChanged;

            if (ringButton != null)
            {
                // Tap the ring: open the systems list. Hold it: presenter shortcut that
                // credits every system so a demo can jump straight to the quiz.
                ringButton.onClick.AddListener(() => { if (checklist != null) checklist.Toggle(); });
                ringButton.AddLongPress(() =>
                {
                    if (Current != Phase.Exploring || tutorial == null) return;
                    tutorial.CompleteAll();
                    ShowCoach("Presenter mode: all 17 systems marked as studied", 4f);
                });
            }
        }

        void OnCabinChanged(bool open)
        {
            if (cabinButtonLabel != null) cabinButtonLabel.text = open ? "HULL" : "CABIN";
            if (open && Current == Phase.Exploring)
            {
                bool small = scaleController != null && scaleController.CurrentScale < 0.1f;
                ShowCoach(small
                    ? "Cabin view — tap DESK or ROOM to see the crew systems up close"
                    : "Cabin view — the hull is hidden so you can tap the systems inside", 5f);
            }
        }

        void WireSystems()
        {
            if (placement != null)
                placement.StateChanged += OnPlacementStateChanged;

            if (selector != null)
                selector.Selected += OnPartSelected;

            if (tutorial != null)
            {
                tutorial.ProgressChanged += OnProgressChanged;
                tutorial.TourCompleted += OnTourCompleted;
            }

            if (quiz != null)
            {
                quiz.Finished += OnQuizFinished;
                quiz.RetryRequested += ReturnToExploring;
            }

            if (finale != null)
                finale.Finished += OnFinaleFinished;

            if (scaleController != null)
                scaleController.ScaleChanged += OnScaleChanged;
        }

        void OnDestroy()
        {
            if (placement != null) placement.StateChanged -= OnPlacementStateChanged;
            if (selector != null) selector.Selected -= OnPartSelected;
            if (tutorial != null)
            {
                tutorial.ProgressChanged -= OnProgressChanged;
                tutorial.TourCompleted -= OnTourCompleted;
            }
            if (quiz != null)
            {
                quiz.Finished -= OnQuizFinished;
                quiz.RetryRequested -= ReturnToExploring;
            }
            if (finale != null) finale.Finished -= OnFinaleFinished;
            if (scaleController != null) scaleController.ScaleChanged -= OnScaleChanged;
            if (infoSheet != null)
            {
                infoSheet.Closed -= OnSheetClosed;
                infoSheet.NextRequested -= GoToNextSuggested;
            }
            if (checklist != null) checklist.RowTapped -= OnChecklistRowTapped;
            if (cabinView != null) cabinView.Changed -= OnCabinChanged;
        }

        // ---------------------------------------------------------------
        // Phases
        // ---------------------------------------------------------------

        void SetPhase(Phase phase)
        {
            Current = phase;
            if (phase != Phase.Exploring && cabinView != null) cabinView.Set(false);

            bool placing = phase == Phase.Placing;
            bool exploring = phase == Phase.Exploring;

            if (placementHud != null) placementHud.SetActive(placing);
            if (scaleBar != null) scaleBar.SetActive(exploring);

            if (explorerHud != null)
            {
                explorerHud.alpha = exploring ? 1f : 0f;
                explorerHud.blocksRaycasts = exploring;
                explorerHud.interactable = exploring;
            }

            if (selector != null) selector.InputEnabled = exploring;
            if (scaleController != null) scaleController.enabled = exploring;
        }

        void BeginExploring()
        {
            SetPhase(Phase.Exploring);

            if (lunarSurface != null) lunarSurface.SetActive(true);
            if (orbitBackdrop != null) orbitBackdrop.SetActive(true);

            if (tutorial != null)
            {
                tutorial.Begin();
                if (checklist != null) checklist.Build(tutorial.parts);
            }

            if (scaleController != null)
            {
                scaleController.ApplyPreset(ModelScaleController.Preset.Tabletop);
                scaleController.ResetRotation();
                if (scaleSlider != null)
                    scaleSlider.SetValueWithoutNotify(scaleController.NormalisedScale);
            }

            ShowCoach("Tap any part of the spacecraft to learn about it");
        }

        void ReturnToExploring()
        {
            SetPhase(Phase.Exploring);
            ShowCoach("Review the systems, then try the readiness check again");
        }

        void BeginQuiz()
        {
            if (quiz == null) return;

            SetPhase(Phase.Quiz);
            HideCoach();
            if (infoSheet != null) infoSheet.Hide();
            if (checklist != null) checklist.Close();

            quiz.Begin();
        }

        void BeginFinale(int score, int total)
        {
            SetPhase(Phase.Finale);

            if (finale == null)
            {
                OnFinaleFinished();
                return;
            }

            // Clear the tour's highlights: the finale shows the real vehicle, not a
            // spacecraft glowing green with "studied" tints.
            HideCoach();
            if (selector != null) selector.ClearSelection();
            if (tutorial != null)
            {
                tutorial.Stop();
                foreach (var p in tutorial.parts) if (p != null) p.ResetState();
            }

            // The Moon stays in view for the burn - Orion is leaving it - and the
            // finale shrinks it away during the coast.
            finale.Begin(score, total);
        }

        // ---------------------------------------------------------------
        // Callbacks
        // ---------------------------------------------------------------

        void OnPlacementStateChanged(ARPlacementController.State state)
        {
            if (Current != Phase.Placing) return;

            // No ARCore (the Editor, or an unsupported phone): skip straight to the
            // orbit view instead of leaving the user on a black scanning screen.
            if (state == ARPlacementController.State.Unavailable)
            {
                StartCoroutine(SkipPlacementNextFrame());
                return;
            }

            ShowCoach(placement != null ? placement.CoachMessage : string.Empty);

            if (placeButton != null)
                placeButton.gameObject.SetActive(state == ARPlacementController.State.ReadyToPlace);
        }

        void OnPartSelected(SelectablePart part)
        {
            if (Current != Phase.Exploring) return;

            if (part == null)
            {
                if (infoSheet != null) infoSheet.Hide();
                return;
            }

            OpenPart(part);
        }

        void OpenPart(SelectablePart part)
        {
            if (part == null || part.info == null) return;

            // Interior systems are only visible with the hull peeled back, and the
            // crew-module exterior only makes sense with it on.
            if (cabinView != null)
            {
                if (part.info.zone == PartZone.Interior) cabinView.Set(true);
                else if (part.info.zone == PartZone.Exterior) cabinView.Set(false);
            }

            if (selector != null) selector.SetCurrent(part);

            if (infoSheet != null)
            {
                bool studied = tutorial != null && tutorial.IsStudied(part);
                infoSheet.Show(part.info, studied);

                var next = tutorial != null ? tutorial.NextSuggested(part) : null;
                if (next != null)
                    infoSheet.SetNextButtonVisible(true, "NEXT SYSTEM");
                else
                    // Last system, or revisiting one after the tour: the sheet's own
                    // primary button becomes the way into the quiz. The HUD's Readiness
                    // Check button sits underneath the open sheet and cannot be tapped.
                    infoSheet.SetNextButtonVisible(tutorial != null && tutorial.IsComplete, "READINESS CHECK");
            }

            if (tutorial != null)
            {
                tutorial.OnPartOpened(part);
                tutorial.PlayNarration(part.info);
            }

            HideCoach();
        }

        void OnSheetClosed()
        {
            if (tutorial != null) { tutorial.OnPartClosed(); tutorial.StopNarration(); }
            if (selector != null) selector.ClearSelection();
        }

        void GoToNextSuggested()
        {
            if (tutorial == null) return;
            var current = selector != null ? selector.Current : null;
            var next = tutorial.NextSuggested(current);
            if (next != null) OpenPart(next);
            else if (tutorial.IsComplete) BeginQuiz();
        }

        void OnChecklistRowTapped(SelectablePart part)
        {
            if (checklist != null) checklist.Close();
            OpenPart(part);
        }

        void OnProgressChanged(int studied, int total)
        {
            if (progressRing != null) progressRing.SetProgress(studied, total);
            if (checklist != null) checklist.Refresh(studied, total, tutorial);

            if (AppDirector.Instance != null) AppDirector.Instance.PartsStudied = studied;

            // Mark studied in the open sheet, so the badge appears without reopening.
            if (infoSheet != null && infoSheet.CurrentInfo != null && tutorial != null)
                infoSheet.SetStudiedBadge(tutorial.IsStudied(infoSheet.CurrentInfo.partId));
        }

        void OnTourCompleted()
        {
            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.SetLabel("READINESS CHECK");
            }

            // The last system's sheet is usually still open at this moment; offer the
            // quiz right there instead of making the user close it to find the button.
            if (infoSheet != null && infoSheet.State != PartInfoSheet.SheetState.Hidden)
                infoSheet.SetNextButtonVisible(true, "READINESS CHECK");
            ShowCoach("All systems studied — you are ready for the readiness check", 6f);
        }

        void OnQuizFinished(int score, int total, bool passed)
        {
            if (AppDirector.Instance != null)
            {
                AppDirector.Instance.LastScore = score;
                AppDirector.Instance.LastTotal = total;
                AppDirector.Instance.LastRunPassed = passed;
            }

            if (passed) BeginFinale(score, total);
            else ReturnToExploring();
        }

        void OnFinaleFinished()
        {
            Current = Phase.Done;
            if (AppDirector.Instance != null)
            {
                AppDirector.Instance.CompleteMission(
                    AppDirector.Instance.LastScore,
                    AppDirector.Instance.LastTotal,
                    true);
            }
        }

        // ---------------------------------------------------------------
        // Scale and sound
        // ---------------------------------------------------------------

        void OnScaleSlider(float value)
        {
            if (scaleController == null) return;
            scaleController.SetNormalisedScale(value);
            UpdateScaleLabel();
        }

        void OnScaleChanged(float scale)
        {
            if (scaleSlider != null) scaleSlider.SetValueWithoutNotify(scaleController.NormalisedScale);
            UpdateScaleLabel();
        }

        void UpdateScaleLabel()
        {
            if (scaleLabel == null || scaleController == null) return;
            scaleLabel.text = scaleController.ScaleLabel();
        }

        public void ApplyScalePreset(int preset)
        {
            if (scaleController == null) return;
            scaleController.ApplyPreset((ModelScaleController.Preset)preset);
            UpdateScaleLabel();
        }

        void ToggleSound()
        {
            if (AppDirector.Instance == null) return;

            AppDirector.Instance.SoundEnabled = !AppDirector.Instance.SoundEnabled;
            AudioDirector.MuteAll(!AppDirector.Instance.SoundEnabled);

            if (soundButton != null)
                soundButton.SetLabel(AppDirector.Instance.SoundEnabled ? Icons.VolumeOn : Icons.VolumeOff);
        }

        // ---------------------------------------------------------------
        // Coach bar
        // ---------------------------------------------------------------

        Coroutine _coachRoutine;

        void ShowCoach(string message, float autoHideAfter = 0f)
        {
            if (coachText == null) return;

            coachText.text = message;
            coachText.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(message));

            if (_coachRoutine != null) StopCoroutine(_coachRoutine);
            if (autoHideAfter > 0f) _coachRoutine = StartCoroutine(HideCoachAfter(autoHideAfter));
        }

        IEnumerator HideCoachAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            HideCoach();
            _coachRoutine = null;
        }

        void HideCoach()
        {
            if (coachText != null && coachText.transform.parent != null)
                coachText.transform.parent.gameObject.SetActive(false);
        }
    }
}
