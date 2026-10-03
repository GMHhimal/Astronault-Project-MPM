using System.Collections.Generic;
using System.Linq;
using RocketTimeline.Data;
using RocketTimeline.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RocketTimeline.Gameplay
{
    /// <summary>
    /// Central controller for the mini-game: knows about every DropZone and
    /// DraggableRocket on the board, checks answers against RocketDatabase,
    /// and drives the Check Answers / Retry / result-text flow shown in the
    /// original mockups.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Data")]
        public RocketDatabase database;

        [Header("Board")]
        public DropZone[] dropZones;
        public DraggableRocket[] rockets;

        [Header("Controls")]
        public Button checkAnswersButton;
        public Button retryButton;
        public Button mainMenuButton;
        [Tooltip("Shown alongside Retry/Main Menu once Check Answers has been pressed. " +
                 "Its onClick is wired directly to the Trivia Review panel's GameObject " +
                 "at scene-build time, so no extra runtime code is needed here.")]
        public Button seeTriviaButton;

        [Header("Result UI")]
        public GameObject resultPanel;
        public Text resultTitleText;
        public Text resultDetailText;
        public Text flavorText;

        [Header("Trivia")]
        public TriviaPopupUI triviaPopup;

        [Header("Events")]
        [Tooltip("Wire this to your own scene-loading code once you build a Main Menu scene.")]
        // Explicitly constructed rather than left as a bare field: unlike
        // Button.onClick (which Unity's own Button component initializes
        // internally), a plain custom UnityEvent field is NOT auto-created
        // when a component is added via script, so it stays null until
        // something assigns it. UnityEventTools.AddVoidPersistentListener
        // (used in RocketTimelineSceneBuilder.cs to wire this up at
        // scene-build time) does not null-check its UnityEvent argument, so
        // a null onMainMenuRequested here crashed the scene builder with a
        // NullReferenceException inside that call instead of a clear error.
        public UnityEvent onMainMenuRequested = new UnityEvent();

        private void Start()
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
            if (seeTriviaButton != null) seeTriviaButton.gameObject.SetActive(false);

            if (checkAnswersButton != null) checkAnswersButton.onClick.AddListener(CheckAnswers);
            if (retryButton != null) retryButton.onClick.AddListener(ResetBoard);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(() => onMainMenuRequested?.Invoke());

            ShufflePool();

            foreach (var rocket in rockets)
                rocket.SnapshotAsOrigin();

            RefreshCheckButtonState();
        }

        private void Update()
        {
            // Polling is cheap for ~8 items and keeps DropZone/DraggableRocket
            // fully decoupled from GameManager (they don't need to know it exists).
            RefreshCheckButtonState();
        }

        /// <summary>Randomizes the pool order at start so the answer isn't already sorted.</summary>
        private void ShufflePool()
        {
            if (rockets == null || rockets.Length == 0) return;
            var order = rockets.OrderBy(_ => Random.value).ToList();
            // SetParent() to the same parent does NOT reorder siblings in Unity, so
            // sibling index has to be set explicitly for the GridLayoutGroup to
            // actually re-flow the cards into the shuffled order.
            for (int i = 0; i < order.Count; i++)
                order[i].transform.SetSiblingIndex(i);
        }

        private void RefreshCheckButtonState()
        {
            if (checkAnswersButton == null || dropZones == null) return;
            bool allFilled = dropZones.Length > 0 && dropZones.All(z => z.Occupant != null);
            checkAnswersButton.interactable = allFilled;
        }

        public void CheckAnswers()
        {
            int correctCount = 0;
            foreach (var zone in dropZones)
            {
                if (zone.Occupant == null) continue;
                RocketItem data = database.GetById(zone.Occupant.rocketId);
                bool isCorrect = data != null && data.correctZoneId == zone.zoneId;
                zone.ShowResult(isCorrect);
                if (isCorrect) correctCount++;
            }

            bool allCorrect = correctCount == dropZones.Length;

            if (resultPanel != null) resultPanel.SetActive(true);
            if (checkAnswersButton != null) checkAnswersButton.gameObject.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(true);
            if (seeTriviaButton != null) seeTriviaButton.gameObject.SetActive(true);

            if (resultTitleText != null)
                resultTitleText.text = allCorrect
                    ? "Perfect! You put the rockets in the right order"
                    : "Not quite right. Try again!";

            if (resultDetailText != null)
                resultDetailText.text = $"Correct answers: {correctCount}/{dropZones.Length}";

            if (flavorText != null)
                flavorText.text = allCorrect
                    ? "From the Chinese Fire Arrow to Starship, rocket technology has come a long way!"
                    : "Check the earliest and latest rockets by rearranging the order.";
        }

        public void ResetBoard()
        {
            foreach (var zone in dropZones)
                zone.ResetVisual();

            foreach (var rocket in rockets)
            {
                rocket.SetPlacedZone(null);
                rocket.ReturnToOrigin();
            }
            foreach (var zone in dropZones)
                zone.Clear();

            // Reshuffle for a fresh attempt, then re-snapshot so ReturnToOrigin()
            // (used mid-round when a card is picked back up) snaps to the new order.
            ShufflePool();
            foreach (var rocket in rockets)
                rocket.SnapshotAsOrigin();

            if (resultPanel != null) resultPanel.SetActive(false);
            if (retryButton != null) retryButton.gameObject.SetActive(false);
            if (seeTriviaButton != null) seeTriviaButton.gameObject.SetActive(false);
            if (checkAnswersButton != null) checkAnswersButton.gameObject.SetActive(true);

            RefreshCheckButtonState();
        }

        /// <summary>Called by RocketInfoButton so any card can be inspected at any time.</summary>
        public void ShowTriviaFor(string rocketId)
        {
            RocketItem data = database.GetById(rocketId);
            if (data != null && triviaPopup != null) triviaPopup.Show(data);
        }
    }
}
