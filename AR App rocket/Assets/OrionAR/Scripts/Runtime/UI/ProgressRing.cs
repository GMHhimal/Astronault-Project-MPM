using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Radial progress indicator for the tour.
    ///
    /// A ring rather than a bar: it reads as a single glanceable gauge, fits in the
    /// corner without stealing width from the camera view, and the centre gives a
    /// natural home for the "7/17" figure the user actually wants.
    /// </summary>
    public class ProgressRing : MonoBehaviour
    {
        [Header("Parts")]
        public Image track;
        public Image fill;
        public TMP_Text countText;
        public TMP_Text captionText;

        [Header("Look")]
        public Color incompleteColor = new Color(0.25f, 0.85f, 0.91f);
        public Color completeColor = new Color(0.29f, 0.87f, 0.50f);
        public float fillSpeed = 7f;

        float _target;
        float _current;
        bool _complete;

        void Awake()
        {
            if (fill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = true;
                fill.fillAmount = 0f;
            }
        }

        void Update()
        {
            if (fill == null) return;
            _current = Mathf.Lerp(_current, _target, 1f - Mathf.Exp(-fillSpeed * Time.unscaledDeltaTime));
            fill.fillAmount = _current;
            fill.color = _complete ? completeColor : incompleteColor;

            if (_complete)
            {
                // Gentle breathing pulse once finished, to draw the eye to the
                // "continue" action that has just become available.
                float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 3f);
                fill.color = new Color(completeColor.r, completeColor.g, completeColor.b, pulse);
            }
        }

        public void SetProgress(int studied, int total)
        {
            _target = total <= 0 ? 0f : Mathf.Clamp01((float)studied / total);
            _complete = total > 0 && studied >= total;

            if (countText != null)
            {
                countText.text = studied + "/" + total;
                countText.color = _complete ? completeColor : UITheme.TextHi;
            }
            if (captionText != null)
                captionText.text = _complete ? "COMPLETE" : "SYSTEMS";
        }
    }
}
