using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Slide-over list of every component and whether it has been studied.
    ///
    /// This exists because of a specific failure in the previous build: the user could
    /// not tell what was left, or whether anything was being recorded at all. A
    /// checklist answers both questions at a glance, and tapping a row flies the
    /// camera to that component so it also works as navigation.
    /// </summary>
    public class ChecklistPanel : MonoBehaviour
    {
        [Header("Structure")]
        public RectTransform panel;
        public CanvasGroup group;
        public RectTransform content;
        public TMP_Text headerText;
        public TMP_Text subheaderText;
        public TouchButton closeButton;

        [Header("Animation")]
        public float hiddenX = 760f;
        public float animSpeed = 14f;

        public bool IsOpen { get; private set; }
        public event System.Action<SelectablePart> RowTapped;

        readonly List<Row> _rows = new List<Row>();
        float _targetX;

        class Row
        {
            public SelectablePart part;
            public RectTransform root;
            public Image check;
            public TMP_Text label;
            public TMP_Text zone;
            public Image zoneDot;
            public TouchButton button;
        }

        void Awake()
        {
            if (panel == null) panel = (RectTransform)transform;
            if (group == null) group = GetComponent<CanvasGroup>();
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            _targetX = hiddenX;
            var p = panel.anchoredPosition;
            p.x = hiddenX;
            panel.anchoredPosition = p;
            SetInteractable(false);
        }

        void Update()
        {
            if (panel == null) return;
            var p = panel.anchoredPosition;
            p.x = Mathf.Lerp(p.x, _targetX, 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime));
            panel.anchoredPosition = p;

            if (group != null)
                group.alpha = Mathf.Clamp01(1f - (p.x / Mathf.Max(1f, hiddenX)));
        }

        void SetInteractable(bool on)
        {
            if (group == null) return;
            group.blocksRaycasts = on;
            group.interactable = on;
        }

        public void Open()
        {
            IsOpen = true;
            _targetX = 0f;
            SetInteractable(true);
            AudioDirector.Play("ui_open");
        }

        public void Close()
        {
            IsOpen = false;
            _targetX = hiddenX;
            SetInteractable(false);
            AudioDirector.Play("ui_close");
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        /// <summary>Builds one row per component. Called once when the tour starts.</summary>
        public void Build(List<SelectablePart> parts)
        {
            foreach (var r in _rows) if (r.root != null) Destroy(r.root.gameObject);
            _rows.Clear();

            if (content == null) return;

            foreach (var part in parts)
            {
                if (part == null || part.info == null) continue;
                _rows.Add(BuildRow(part));
            }

            if (headerText != null) headerText.text = "SPACECRAFT SYSTEMS";
            Refresh(0, _rows.Count);
        }

        Row BuildRow(SelectablePart part)
        {
            var row = new Row { part = part };

            row.root = UIKit.Rect("Row_" + part.PartId, content);
            // The list's layout group does not control child height, so the rect itself
            // must be sized - the LayoutElement alone left every row at 100 dp.
            UIKit.Sized(row.root.gameObject, UITheme.TouchMin + 12f);
            row.root.sizeDelta = new Vector2(0f, UITheme.TouchMin + 12f);

            var bg = row.root.gameObject.AddComponent<Image>();
            bg.color = UITheme.Alpha(UITheme.SurfaceAlt, 0.65f);
            bg.sprite = UIKit.RoundedSprite(UITheme.RadiusSm);
            bg.type = Image.Type.Sliced;

            // Zone colour stripe down the left edge.
            row.zoneDot = UIKit.Panel("ZoneStripe", row.root, UITheme.ZoneColor(part.info.zone),
                                      UITheme.RadiusSm);
            row.zoneDot.rectTransform.anchorMin = new Vector2(0f, 0f);
            row.zoneDot.rectTransform.anchorMax = new Vector2(0f, 1f);
            row.zoneDot.rectTransform.pivot = new Vector2(0f, 0.5f);
            row.zoneDot.rectTransform.sizeDelta = new Vector2(4f, -12f);
            row.zoneDot.rectTransform.anchoredPosition = new Vector2(6f, 0f);

            // Checkbox.
            var checkBg = UIKit.Panel("CheckBg", row.root, UITheme.Alpha(UITheme.Space, 0.8f),
                                      UITheme.RadiusSm);
            checkBg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            checkBg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            checkBg.rectTransform.pivot = new Vector2(0f, 0.5f);
            checkBg.rectTransform.sizeDelta = new Vector2(30f, 30f);
            checkBg.rectTransform.anchoredPosition = new Vector2(18f, 0f);

            row.check = UIKit.Panel("Check", checkBg.rectTransform, UITheme.Success, UITheme.RadiusSm);
            UIKit.Stretch(row.check.rectTransform, 0f);
            var tick = UIKit.Text("Tick", row.check.rectTransform, Icons.Check, 22f, UITheme.TextOnAccent,
                                  TextAlignmentOptions.Center, lineSpacingMul: UITheme.LineTight);
            if (UIKit.IconFont != null) tick.font = UIKit.IconFont;
            UIKit.Stretch(tick.rectTransform, 0f);
            row.check.gameObject.SetActive(false);

            // Zone caption under the name, so the list doubles as a map of the vehicle.
            row.zone = UIKit.Text("Zone", row.root, UITheme.ZoneLabel(part.info.zone),
                                  UITheme.FontOverline - 1f, UITheme.ZoneColor(part.info.zone),
                                  TextAlignmentOptions.BottomLeft, heading: true,
                                  lineSpacingMul: UITheme.LineTight);
            row.zone.characterSpacing = 8f;
            row.zone.rectTransform.anchorMin = new Vector2(0f, 0f);
            row.zone.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            row.zone.rectTransform.offsetMin = new Vector2(56f, 7f);
            row.zone.rectTransform.offsetMax = new Vector2(-12f, 0f);

            row.label = UIKit.Text("Label", row.root, part.info.ShortLabel,
                                   UITheme.FontLabel, UITheme.TextHi,
                                   TextAlignmentOptions.MidlineLeft);
            row.label.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            row.label.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.label.rectTransform.offsetMin = new Vector2(56f, 0f);
            row.label.rectTransform.offsetMax = new Vector2(-12f, -4f);
            row.label.fontSize = UITheme.FontBody;
            row.label.textWrappingMode = TextWrappingModes.NoWrap;
            row.label.overflowMode = TextOverflowModes.Ellipsis;

            row.button = row.root.gameObject.AddComponent<TouchButton>();
            row.button.background = bg;
            row.button.normalColor = bg.color;
            row.button.pressedColor = UITheme.Alpha(UITheme.Accent, 0.25f);
            row.button.pressScale = 0.985f;

            var captured = part;
            row.button.onClick.AddListener(() =>
            {
                if (RowTapped != null) RowTapped(captured);
            });

            return row;
        }

        /// <summary>Updates every checkbox. Cheap enough to call on any progress change.</summary>
        public void Refresh(int studied, int total, TutorialDirector director = null)
        {
            foreach (var row in _rows)
            {
                if (row.part == null || row.part.info == null) continue;

                bool done = director != null
                    ? director.IsStudied(row.part)
                    : row.part.Studied;

                if (row.check != null) row.check.gameObject.SetActive(done);
                if (row.label != null)
                    row.label.color = done ? UITheme.TextMid : UITheme.TextHi;
            }

            if (subheaderText != null)
            {
                subheaderText.text = studied + " of " + total + " studied";
                subheaderText.color = studied >= total ? UITheme.Success : UITheme.TextMid;
            }
        }
    }
}
