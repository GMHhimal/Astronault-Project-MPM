using UnityEngine;

namespace RocketTimeline.UI
{
    /// <summary>
    /// Classic 2-panel "treadmill" infinite vertical scroll (client: a moving
    /// starfield background, "stars moving downwards like we are going up").
    ///
    /// Two copies of the same seamlessly-tileable star Image (panelA/panelB),
    /// each exactly <see cref="panelHeight"/> tall, start stacked directly on
    /// top of one another. Both move down together every frame; whenever one
    /// has scrolled completely past the bottom of the screen, it jumps back
    /// up by exactly TWO panel-heights (not reset to a fixed position) -
    /// that's what keeps the pair always exactly one panel-height apart and
    /// the screen always fully covered with no gap, at every single frame,
    /// not just most of them. (Worked through the full math for this before
    /// writing it - a naive "one tall image, wrap its position" version
    /// looks like it should also work but actually leaves the screen
    /// uncovered for part of every cycle; this two-panel version is the
    /// standard, provably-gapless way to do it.)
    ///
    /// panelHeight must be an exact whole multiple of the star sprite's
    /// rendered tile size (RocketSpriteImporter.ScrollStarTileSize, via that
    /// sprite's Pixels Per Unit import value) - otherwise each panel ends on
    /// a partial, cut-off tile, and the seam between panels becomes visible
    /// as a sudden jump in the star pattern.
    /// </summary>
    public class ScrollingStarfieldLayer : MonoBehaviour
    {
        public RectTransform panelA;
        public RectTransform panelB;
        public float panelHeight = 1080f;

        // Positive = stars drift DOWN the screen (the look the client asked
        // for). This is a plain RectTransform.anchoredPosition move (+Y is up,
        // -Y is down, Unity's standard UI convention - unambiguous), not a
        // texture/UV offset, specifically so the scroll direction doesn't
        // depend on anything that would need visually checking in the Editor
        // first. If a future change ever needs it to run the other way,
        // flipping the sign here is the only change required.
        public float scrollSpeed = 40f;

        private void Update()
        {
            float delta = scrollSpeed * Time.deltaTime;
            MovePanel(panelA, delta);
            MovePanel(panelB, delta);
        }

        private void MovePanel(RectTransform panel, float delta)
        {
            if (panel == null) return;
            Vector2 pos = panel.anchoredPosition;
            pos.y -= delta;
            if (pos.y <= -panelHeight)
            {
                pos.y += 2f * panelHeight;
            }
            panel.anchoredPosition = pos;
        }
    }
}
