using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RocketTimeline.Gameplay
{
    /// <summary>
    /// One slot on the timeline (e.g. "1957" or "13th Century"). Accepts a
    /// single DraggableRocket, resizes/centers it to fit the slot, and can
    /// show a green/red ring once the player checks their answers.
    /// </summary>
    public class DropZone : MonoBehaviour, IDropHandler
    {
        [Header("Identity")]
        [Tooltip("Must match a RocketItem.correctZoneId in RocketData.json.")]
        public string zoneId;

        [Header("Visuals")]
        [Tooltip("Border/ring image tinted green/red after Check Answers is pressed. " +
                 "Should use a 9-sliced sprite with a transparent center so the rocket underneath stays visible.")]
        public Image outline;

        [Tooltip("Where the dropped rocket snaps to. Defaults to this transform if left empty.")]
        public RectTransform slot;

        [Tooltip("Size a rocket card is shrunk to once placed in this zone, so it can never spill outside the slot.")]
        public Vector2 placedSize = new Vector2(128, 128);

        public DraggableRocket Occupant { get; private set; }

        public void OnDrop(PointerEventData eventData)
        {
            DraggableRocket dragged = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<DraggableRocket>()
                : null;
            if (dragged == null) return;

            // Swap out whatever was already here (send it back to the pool)
            // so a zone never silently holds two rockets.
            if (Occupant != null && Occupant != dragged)
            {
                Occupant.SetPlacedZone(null);
                Occupant.ReturnToOrigin();
            }

            RectTransform target = slot != null ? slot : (RectTransform)transform;
            RectTransform draggedRect = dragged.GetComponent<RectTransform>();

            dragged.transform.SetParent(target, false);

            // The card arrives here still carrying whatever anchor/size the pool's
            // GridLayoutGroup gave it (which anchors to a corner, not the center).
            // Force it back to a simple centered, fixed-size rect so it can never
            // spill outside this slot or overlap neighbouring zones.
            draggedRect.anchorMin = new Vector2(0.5f, 0.5f);
            draggedRect.anchorMax = new Vector2(0.5f, 0.5f);
            draggedRect.pivot = new Vector2(0.5f, 0.5f);
            draggedRect.sizeDelta = placedSize;
            draggedRect.anchoredPosition = Vector2.zero;

            dragged.SetPlacedZone(this);
            Occupant = dragged;

            // The rocket animates its own landing pop (see DraggableRocket) rather
            // than the zone animating it - that way stopping this zone's own
            // outline-punch coroutine can never accidentally freeze a rocket
            // mid-animation at the wrong scale.
            dragged.PlayPlacementPunch();
        }

        /// <summary>Called by DraggableRocket when it is picked back up.</summary>
        public void Clear()
        {
            Occupant = null;
        }

        public void ShowResult(bool correct)
        {
            if (outline == null) return;
            outline.enabled = true;
            outline.color = correct
                ? new Color(0.30f, 0.90f, 0.40f, 1f)   // green
                : new Color(0.95f, 0.30f, 0.30f, 1f);  // red

            StopAllCoroutines();
            StartCoroutine(PunchScale(outline.transform));
        }

        public void ResetVisual()
        {
            if (outline == null) return;
            outline.enabled = false;
            outline.transform.localScale = Vector3.one;
        }

        /// <summary>Small, dependency-free "pop" animation for this zone's own outline
        /// only - the placed rocket animates itself (see DraggableRocket.PlayPlacementPunch).</summary>
        private static IEnumerator PunchScale(Transform t)
        {
            const float duration = 0.18f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(time / duration);
                float scale = 1f + 0.22f * Mathf.Sin(p * Mathf.PI);
                t.localScale = Vector3.one * scale;
                yield return null;
            }
            t.localScale = Vector3.one;
        }
    }
}
