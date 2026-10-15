using UnityEngine;

namespace RocketTimeline.UI
{
    /// <summary>
    /// v15: RocketTimelineSceneBuilder.BuildScrollArea pivots a horizontal
    /// scroll's Content rect to the left (anchorMin/Max.x = 0, pivot.x = 0)
    /// so it can grow rightward and scroll once it holds more than fits -
    /// necessary for the rocket pool, whose GridLayoutGroup lays out however
    /// many rocket cards the roster has. But that same left pivot means that
    /// whenever the laid-out content ends up narrower than the viewport (a
    /// wide enough window, or a roster short enough to fit in one screen),
    /// the row just sits flush against the left edge instead of looking
    /// centered - this was reported as the drag-and-drop tray looking
    /// "offset to the left."
    ///
    /// Attached directly to that Content rect (its parent is always the
    /// scroll Viewport, per BuildScrollArea), this re-centers it horizontally
    /// whenever it fits within the viewport, and leaves it at its natural
    /// flush-left/scrollable position whenever it doesn't. Re-checked every
    /// frame rather than once, since both the viewport width (window resize)
    /// and the content's preferred width (ContentSizeFitter, after the roster
    /// loads) are only known after layout has actually run.
    /// </summary>
    // Runs its LateUpdate after ScrollRect's own (ScrollRect repositions
    // Content in LateUpdate too, e.g. for inertia/clamping) so this is the
    // last word on Content's position each frame instead of racing it.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(RectTransform))]
    public class CenterScrollContentIfFits : MonoBehaviour
    {
        private RectTransform content;
        private RectTransform viewport;

        private void Awake()
        {
            content = (RectTransform)transform;
            viewport = transform.parent as RectTransform;
        }

        private void LateUpdate()
        {
            if (viewport == null) return;

            float viewportWidth = viewport.rect.width;
            float contentWidth = content.rect.width;
            float targetX = contentWidth <= viewportWidth ? (viewportWidth - contentWidth) * 0.5f : 0f;

            Vector2 pos = content.anchoredPosition;
            if (!Mathf.Approximately(pos.x, targetX))
            {
                pos.x = targetX;
                content.anchoredPosition = pos;
            }
        }
    }
}
