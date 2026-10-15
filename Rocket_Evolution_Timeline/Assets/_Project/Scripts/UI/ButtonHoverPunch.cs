using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// v13: small interaction "juice" for buttons - a quick scale-up on
    /// hover and a quick scale-down on press, eased every frame rather than
    /// snapped instantly. Unity's built-in Button only ever offers a flat
    /// Image.color tint transition, which (combined with v11's pale,
    /// barely-shaded base textures) was a real contributor to the "feels
    /// washed off/static" feedback - buttons didn't visibly react to being
    /// touched at all beyond a faint color shift. This is a cheap, purely
    /// visual add-on (no layout changes, no effect on click handling) wired
    /// automatically by RocketTimelineSceneBuilder.BuildStyledButton, so
    /// every styled button in the app picks it up for free.
    ///
    /// v15: optional hover "accent" color swap, opted into per-button via
    /// SetHoverAccent (currently just the Main Menu's Rocket Trivia + About
    /// buttons). At rest the border/label sit at one tint (e.g. the same
    /// neutral slate every About button uses); on hover/press they switch to
    /// a second, brighter accent tint (e.g. the cyan Rocket Trivia used to
    /// wear all the time) instead of just scaling up. Both border Image and
    /// label Text are driven from the same ghost-button sprite's
    /// multiply-tint trick already used app-wide, so this is just setting
    /// Graphic.color/Text.color to one of two fixed Colors - no extra art
    /// needed.
    ///
    /// v16: this is the THIRD attempt at this feature, after v15's original
    /// per-frame Color.Lerp and v15.2's "disable Selectable.Transition +
    /// speed up the lerp" (both reported by the client as still not
    /// visible). Root-caused this time: BuildStyledButton calls
    /// `button.colors = colors;` - and because a freshly-AddComponent'd
    /// Button starts with `transition = ColorTint` (Unity's compiler
    /// default) until BuildStyledButton later switches it to None, that one
    /// assignment fires Unity's *editor-time* live-preview code path and
    /// instantly CrossFades the button's CanvasRenderer to
    /// colors.normalColor (opaque white) - a value that then never gets
    /// reconciled with Graphic.color again once transition is None, because
    /// None's DoStateTransition is a no-op. On its own that stray white
    /// multiply is harmless (white is the identity color), but it shows the
    /// accent mechanism was fighting Selectable's internals in a way that
    /// was hard to reason about with certainty without running the editor.
    /// Rather than risk a fourth blind tweak to the same eased-lerp
    /// mechanism, this version is a clean rewrite: no per-frame color lerp
    /// at all (removes any dependency on Update() cadence/timing being "fast
    /// enough to notice"), an immediate, explicit color SNAP on every
    /// relevant pointer event, and an explicit CrossFadeColor(0) call the
    /// moment the accent is wired up to force the CanvasRenderer in sync
    /// with Graphic.color from a known-good baseline (belt-and-suspenders
    /// against the stray-tint issue above, even though it was provably
    /// harmless). It also now reacts to press (OnPointerDown/Up), not just
    /// hover - important because this is a mobile build, and a touchscreen
    /// never generates a sustained "hover" the way a desktop mouse does
    /// (Enter/Exit arrive back-to-back with Down/Up on touch); pressing a
    /// button is the one gesture every input method can reliably produce,
    /// so it's the fallback that guarantees SOME visible accent feedback
    /// regardless of how this is being tested or played.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonHoverPunch : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float HoverScale = 1.045f;
        private const float PressScale = 0.96f;
        private const float EaseSpeed = 14f;

        private RectTransform rect;
        private Vector3 baseScale;
        private float targetScale = 1f;
        private bool hovering;
        private bool pressed;

        private Graphic accentGraphic;
        private Text accentLabel;
        private Color restColor;
        private Color hoverColor;
        private bool hasAccent;

        private void Awake()
        {
            rect = (RectTransform)transform;
            baseScale = rect.localScale;
        }

        /// <summary>
        /// Opts this button into the hover/press color-accent behavior
        /// described above. <paramref name="rest"/> should match whatever
        /// color the button's border/label were already built with, so
        /// there's no pop the first frame.
        /// </summary>
        public void SetHoverAccent(Graphic graphic, Text label, Color rest, Color hover)
        {
            accentGraphic = graphic;
            accentLabel = label;
            restColor = rest;
            hoverColor = hover;
            hasAccent = true;
            ApplyAccentColor(restColor);
        }

        /// <summary>Immediately (no fade) sets both the border and label to
        /// <paramref name="target"/>, and - for the border - also forces the
        /// CanvasRenderer in sync via an instant CrossFadeColor, so any
        /// stray tint left over from Selectable's own internals (see the
        /// class comment above) can never linger on top of this.</summary>
        private void ApplyAccentColor(Color target)
        {
            if (accentGraphic != null)
            {
                accentGraphic.color = target;
                accentGraphic.CrossFadeColor(target, 0f, true, true);
            }
            if (accentLabel != null)
                accentLabel.color = target;
        }

        private void Update()
        {
            // Only the scale "punch" still animates per-frame - the color
            // accent is snapped instantly from the pointer callbacks below.
            float current = rect.localScale.x / Mathf.Max(0.0001f, baseScale.x);
            float next = Mathf.Lerp(current, targetScale, Time.unscaledDeltaTime * EaseSpeed);
            rect.localScale = baseScale * next;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            RecomputeTarget();
            if (hasAccent) ApplyAccentColor(hoverColor);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            pressed = false;
            RecomputeTarget();
            if (hasAccent) ApplyAccentColor(restColor);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            RecomputeTarget();
            if (hasAccent) ApplyAccentColor(hoverColor);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressed = false;
            RecomputeTarget();
            // Touch input fires Up without ever having fired Enter, so fall
            // back to restColor rather than assuming `hovering` is set.
            if (hasAccent) ApplyAccentColor(hovering ? hoverColor : restColor);
        }

        private void RecomputeTarget()
        {
            targetScale = pressed ? PressScale : (hovering ? HoverScale : 1f);
        }
    }
}
