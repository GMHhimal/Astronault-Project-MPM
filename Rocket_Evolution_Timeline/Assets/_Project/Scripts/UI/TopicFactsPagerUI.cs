using RocketTimeline.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// Drives a single bonus-content "About..." slideshow overlay (Satellites,
    /// Rovers &amp; Robotic Explorers, International Space Station) - same
    /// visual language as TriviaReviewPagerUI's rocket slideshow but simpler:
    /// a single fixed icon for the whole topic rather than a different image
    /// per page, since each topic's facts are various probes/stats/events
    /// rather than one physical object per page.
    ///
    /// v16: each page now shows TWO facts, stacked vertically in one shared
    /// scrolling area (block A above block B) instead of one - client
    /// feedback was that a single fact per page left a lot of empty space
    /// below shorter entries, and asked for two sub-topics to share one
    /// slide instead (halving the page count). An earlier pass of this put
    /// the two facts in side-by-side columns; the client corrected that
    /// with a mockup showing them stacked full-width instead - see
    /// RocketTimelineSceneBuilder.BuildTopicFactsPanel. Block B (and the
    /// spacer above it) is hidden via SetActive(false) on a topic's final
    /// page whenever its fact count is odd, so this works for any topic
    /// regardless of how many facts it has.
    ///
    /// The scene builder constructs one of these per topic (see
    /// RocketTimelineSceneBuilder.BuildTopicFactsPanel) and wires every field
    /// below; this script only ever swaps the *content* of those existing
    /// Text objects when the page changes, it never builds or destroys UI at
    /// runtime.
    /// </summary>
    public class TopicFactsPagerUI : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Fixed for the whole topic (set once, never changes per-page)")]
        public Text titleText;
        public Text subtitleText;
        public Image topicIcon;

        [Header("Block A (always shown)")]
        public Text headingTextA;
        public Text bodyTextA;

        [Header("Block B (hidden, along with its spacer, on a trailing odd-numbered page)")]
        public GameObject blockBRoot;
        public GameObject blockBSpacerRoot;
        public Text headingTextB;
        public Text bodyTextB;

        [Header("Shared")]
        public ScrollRect contentScrollRect;
        public Text pageIndicatorText;

        [Header("Data")]
        public TopicFact[] facts;

        private int currentIndex;

        private int PageCount => (facts == null || facts.Length == 0) ? 0 : Mathf.CeilToInt(facts.Length / 2f);

        /// <summary>Opens the panel and always starts from the first page, so
        /// repeated visits are predictable rather than resuming wherever the
        /// player last left off.</summary>
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
            int pageCount = PageCount;
            if (pageCount == 0) return;
            currentIndex = (currentIndex + 1) % pageCount;
            Render();
        }

        public void PrevPage()
        {
            int pageCount = PageCount;
            if (pageCount == 0) return;
            currentIndex = (currentIndex - 1 + pageCount) % pageCount;
            Render();
        }

        private void Render()
        {
            int pageCount = PageCount;
            if (pageCount == 0) return;

            int firstIndex = currentIndex * 2;
            int secondIndex = firstIndex + 1;
            bool hasSecond = secondIndex < facts.Length;

            TopicFact first = facts[firstIndex];
            if (headingTextA != null) headingTextA.text = first.heading;
            if (bodyTextA != null) bodyTextA.text = first.body;

            if (blockBRoot != null) blockBRoot.SetActive(hasSecond);
            if (blockBSpacerRoot != null) blockBSpacerRoot.SetActive(hasSecond);
            if (hasSecond)
            {
                TopicFact second = facts[secondIndex];
                if (headingTextB != null) headingTextB.text = second.heading;
                if (bodyTextB != null) bodyTextB.text = second.body;
            }

            if (pageIndicatorText != null) pageIndicatorText.text = $"{currentIndex + 1} / {pageCount}";

            // Snap the shared scroll position back to the top - without
            // this, paging forward while scrolled down on the previous page
            // would show the new facts mid-scroll, possibly cut off.
            Canvas.ForceUpdateCanvases();
            if (contentScrollRect != null) contentScrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
