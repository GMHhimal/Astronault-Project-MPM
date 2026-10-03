using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RocketTimeline.Gameplay
{
    /// <summary>
    /// Put this on every rocket card in the pool. Handles the actual pointer
    /// drag using Unity's standard UI event interfaces - this is the same
    /// pattern Unity's own drag-and-drop examples use, so it works with mouse,
    /// touch, and the new Input System's UI actions without extra setup.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class DraggableRocket : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Identity")]
        [Tooltip("Must match a RocketItem.id in RocketData.json.")]
        public string rocketId;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Canvas rootCanvas;

        // The card's one true "home" in the pool. This is set ONLY by
        // SnapshotAsOrigin() (called at Start and after Retry, always while
        // every card is genuinely sitting in the pool) and is deliberately
        // NEVER overwritten by OnBeginDrag. Earlier versions re-captured this
        // from transform.parent every time a drag started, which meant
        // picking up a card that was already docked in a timeline slot
        // silently changed its "home" to that slot instead of the pool -
        // causing cards to get stuck on the timeline after Retry, or to
        // collide with each other when a new card was dropped on an
        // occupied slot. Leaving these fields alone during drag fixes both.
        private Transform originalParent;
        private Vector2 originalAnchoredPosition;
        private int originalSiblingIndex;

        /// <summary>Null while sitting in the pool; set once dropped on a DropZone.</summary>
        public DropZone CurrentZone { get; private set; }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            rootCanvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // If we were already placed in a zone, free that zone up immediately.
            if (CurrentZone != null)
            {
                CurrentZone.Clear();
                CurrentZone = null;
            }

            // Reparent to the canvas root so the card renders above every other
            // UI element while being dragged (otherwise it can end up hidden
            // behind sibling panels).
            transform.SetParent(rootCanvas.transform, true);
            transform.SetAsLastSibling();

            canvasGroup.blocksRaycasts = false; // let DropZones under the pointer receive OnDrop
            canvasGroup.alpha = 0.85f;
            rectTransform.localScale = Vector3.one * 1.08f; // small "picked up" pop for game feel
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            rectTransform.localScale = Vector3.one;

            // DropZone.OnDrop() sets CurrentZone if the drop was valid. If it's
            // still null here, the card was released over empty space (or on
            // top of another card rather than squarely on a zone), so send it
            // back to the pool instead of leaving it floating mid-canvas.
            if (CurrentZone == null)
            {
                ReturnToOrigin();
            }
        }

        /// <summary>Always returns to the pool - see the field comments above for why.</summary>
        public void ReturnToOrigin()
        {
            transform.SetParent(originalParent, false);
            transform.SetSiblingIndex(originalSiblingIndex);
            rectTransform.anchoredPosition = originalAnchoredPosition;
            rectTransform.localScale = Vector3.one;
        }

        public void SetPlacedZone(DropZone zone)
        {
            CurrentZone = zone;
        }

        /// <summary>
        /// Call ONLY while this card is genuinely sitting in the pool (at
        /// scene start, and after Retry has returned everything) so
        /// ReturnToOrigin() always has a correct, stable home to snap back to.
        /// </summary>
        public void SnapshotAsOrigin()
        {
            originalParent = transform.parent;
            originalAnchoredPosition = rectTransform.anchoredPosition;
            originalSiblingIndex = transform.GetSiblingIndex();
        }

        /// <summary>Small "landed" pop played once this card settles into a slot. Owns its
        /// own coroutine (rather than the DropZone animating it) so stopping one card's
        /// animation can never accidentally cut off a different card's or the zone's own.</summary>
        public void PlayPlacementPunch()
        {
            StopAllCoroutines();
            StartCoroutine(PunchScale());
        }

        private IEnumerator PunchScale()
        {
            const float duration = 0.16f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(time / duration);
                float scale = 1f + 0.2f * Mathf.Sin(p * Mathf.PI);
                rectTransform.localScale = Vector3.one * scale;
                yield return null;
            }
            rectTransform.localScale = Vector3.one;
        }
    }
}
