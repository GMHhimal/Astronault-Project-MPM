using RocketTimeline.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// Drives the full-screen "Trivia Review" overlay: one rocket per page,
    /// with Prev/Next buttons to flip through all of them in timeline order -
    /// styled after the research deck's own slides (big year, name, Key
    /// Advances / Trivia Fact / Countries, and an image of the rocket).
    ///
    /// The scene builder constructs all of the UI once (see
    /// RocketTimelineSceneBuilder.BuildTriviaReviewPanel) and wires every
    /// field below; this script only ever swaps the *content* of those
    /// existing Text/Image objects when the page changes; it never builds or
    /// destroys UI at runtime.
    /// </summary>
    public class TriviaReviewPagerUI : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Per-page content")]
        public Text yearText;
        public Text nameText;
        public Text keyAdvanceValueText;
        public Text triviaValueText;
        public Text countryValueText;
        public Image rocketImage;
        public Text pageIndicatorText;

        [Header("v16: the Key Advances / Trivia Fact / Countries block now\n" +
                 "scrolls (see RocketTimelineSceneBuilder.BuildTriviaReviewPanel)\n" +
                 "so a page change needs to snap it back to the top - otherwise\n" +
                 "paging to a new rocket while scrolled down would show that\n" +
                 "rocket's panel mid-scroll, possibly cut off at the top.")]
        public ScrollRect detailScrollRect;

        [Header("Data (ordered chronologically - same order as the timeline itself)")]
        public RocketItem[] rockets;
        public Sprite[] rocketSprites;

        private int currentIndex;

        /// <summary>Opens the panel and always starts from the first rocket,
        /// so repeated visits are predictable rather than resuming wherever
        /// the player last left off.</summary>
        public void Show()
        {
            if (root != null) root.SetActive(true);
            currentIndex = 0;
            Render();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        public void NextPage()
        {
            if (rockets == null || rockets.Length == 0) return;
            currentIndex = (currentIndex + 1) % rockets.Length;
            Render();
        }

        public void PrevPage()
        {
            if (rockets == null || rockets.Length == 0) return;
            currentIndex = (currentIndex - 1 + rockets.Length) % rockets.Length;
            Render();
        }

        private void Render()
        {
            if (rockets == null || rockets.Length == 0) return;
            RocketItem data = rockets[currentIndex];

            if (yearText != null) yearText.text = data.zoneLabel;
            if (nameText != null) nameText.text = data.displayName;
            if (keyAdvanceValueText != null) keyAdvanceValueText.text = data.keyAdvance;
            if (triviaValueText != null) triviaValueText.text = data.triviaFact;
            if (countryValueText != null) countryValueText.text = data.country;

            if (rocketImage != null)
            {
                Sprite sprite = (rocketSprites != null && currentIndex < rocketSprites.Length) ? rocketSprites[currentIndex] : null;
                rocketImage.sprite = sprite;
                rocketImage.enabled = sprite != null;
            }

            if (pageIndicatorText != null) pageIndicatorText.text = $"{currentIndex + 1} / {rockets.Length}";

            if (detailScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                detailScrollRect.verticalNormalizedPosition = 1f;
            }
        }
    }
}
