using System.Linq;
using RocketTimeline.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// Drives the full-screen Telescopes overlay: one telescope per page, with
    /// Prev/Next buttons to flip through all of them - same "encyclopedia"
    /// pattern as TriviaReviewPagerUI.
    ///
    /// v2 (client feedback: "the telescope and the lens views are too
    /// small... can we try an interactive button that shows the lens
    /// view"): the view image is no longer squeezed into a half-width
    /// column next to the hardware photo. The hardware photo now gets the
    /// full card width (hardwareImage), and a wide "Look Through the
    /// Eyepiece" button - showing a small live preview via viewImage -
    /// opens a separate full-card reveal overlay where the SAME view image
    /// is shown again, much bigger, via viewImageLarge. Both viewImage and
    /// viewImageLarge always show the same sprite (see Render()); only
    /// their on-screen size differs, so there's exactly one real "view"
    /// image per telescope, just displayed in two places/sizes.
    ///
    /// The scene builder constructs all of the UI once (see
    /// RocketTimelineSceneBuilder.BuildTelescopePanel) and wires every field
    /// below; this script only ever swaps the *content* of those existing
    /// Text/Image objects when the page changes, it never builds or destroys
    /// UI at runtime.
    /// </summary>
    public class TelescopePagerUI : MonoBehaviour
    {
        [Header("Root")]
        public GameObject root;

        [Header("Header")]
        public Text nameText;
        public Text eraText;

        [Header("Hardware photo (full width)")]
        public Image hardwareImage;
        public Text hardwareCreditText;

        [Header("Eyepiece preview button (small, opens the overlay below)")]
        public Image viewImage;

        [Header("Eyepiece reveal overlay (big, opened by the preview button)")]
        public GameObject eyepieceOverlayRoot;
        public Image viewImageLarge;
        public Text eyepieceOverlayNameText;
        public Text eyepieceOverlayCreditText;

        [Header("Scrolling fact block")]
        public Text opticalPrincipleValueText;
        public Text whyItLooksValueText;
        public Text triviaValueText;
        public ScrollRect contentScrollRect;

        [Header("Nav")]
        public Text pageIndicatorText;

        [Header("Data")]
        public TelescopeItem[] telescopes;
        public Sprite[] hardwareSprites;
        public Sprite[] viewSprites;

        private int currentIndex;

        /// <summary>Opens the panel and always starts from the first telescope,
        /// so repeated visits are predictable rather than resuming wherever the
        /// player last left off. Also makes sure the eyepiece overlay starts
        /// closed, in case it was left open the last time this panel was shown.</summary>
        public void Show()
        {
            if (root != null) root.SetActive(true);
            if (eyepieceOverlayRoot != null) eyepieceOverlayRoot.SetActive(false);
            currentIndex = 0;
            Render();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>Opens the big eyepiece reveal overlay for whichever telescope
        /// is currently showing. The overlay's own dim background blocks input to
        /// everything beneath it (including Prev/Next), so there's no risk of
        /// paging to a different telescope while it's open.</summary>
        public void ShowEyepieceOverlay()
        {
            if (eyepieceOverlayRoot != null) eyepieceOverlayRoot.SetActive(true);
        }

        public void CloseEyepieceOverlay()
        {
            if (eyepieceOverlayRoot != null) eyepieceOverlayRoot.SetActive(false);
        }

        public void NextPage()
        {
            if (telescopes == null || telescopes.Length == 0) return;
            currentIndex = (currentIndex + 1) % telescopes.Length;
            Render();
        }

        public void PrevPage()
        {
            if (telescopes == null || telescopes.Length == 0) return;
            currentIndex = (currentIndex - 1 + telescopes.Length) % telescopes.Length;
            Render();
        }

        private void Render()
        {
            if (telescopes == null || telescopes.Length == 0) return;
            TelescopeItem data = telescopes[currentIndex];

            if (nameText != null) nameText.text = data.displayName;
            if (eraText != null) eraText.text = data.era;

            if (hardwareImage != null)
            {
                Sprite sprite = (hardwareSprites != null && currentIndex < hardwareSprites.Length) ? hardwareSprites[currentIndex] : null;
                hardwareImage.sprite = sprite;
                hardwareImage.enabled = sprite != null;
            }
            if (hardwareCreditText != null) hardwareCreditText.text = data.hardwareCredit;

            Sprite viewSprite = (viewSprites != null && currentIndex < viewSprites.Length) ? viewSprites[currentIndex] : null;
            if (viewImage != null)
            {
                viewImage.sprite = viewSprite;
                viewImage.enabled = viewSprite != null;
            }
            if (viewImageLarge != null)
            {
                viewImageLarge.sprite = viewSprite;
                viewImageLarge.enabled = viewSprite != null;
            }
            if (eyepieceOverlayNameText != null) eyepieceOverlayNameText.text = data.displayName;
            if (eyepieceOverlayCreditText != null) eyepieceOverlayCreditText.text = data.viewCredit;

            if (opticalPrincipleValueText != null) opticalPrincipleValueText.text = data.opticalPrinciple;
            if (whyItLooksValueText != null) whyItLooksValueText.text = data.whyItLooksThisWay;
            if (triviaValueText != null)
            {
                triviaValueText.text = (data.trivia != null && data.trivia.Length > 0)
                    ? string.Join("\n\n", data.trivia.Select(t => "-  " + t))
                    : "";
            }

            if (pageIndicatorText != null) pageIndicatorText.text = $"{currentIndex + 1} / {telescopes.Length}";

            // Snap the shared scroll position back to the top - without this,
            // paging forward while scrolled down on the previous page would
            // show the new telescope's facts mid-scroll, possibly cut off.
            Canvas.ForceUpdateCanvases();
            if (contentScrollRect != null) contentScrollRect.verticalNormalizedPosition = 1f;
        }
    }
}
