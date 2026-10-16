using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Bottom sheet showing the selected component's description.
    ///
    /// A bottom sheet rather than a centred dialog, because on a phone the bottom
    /// third of the screen is the only region a thumb reaches comfortably, and because
    /// it leaves the model visible above it - the user can still see what they are
    /// reading about. It supports two heights: a peek showing the title and headline
    /// figure, and a full extent with the explanation. Dragging moves between them.
    /// </summary>
    public class PartInfoSheet : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public enum SheetState { Hidden, Peek, Expanded }

        [Header("Structure")]
        public RectTransform sheet;
        public CanvasGroup group;
        public RectTransform grabHandle;
        public ScrollRect scroll;

        [Header("Content")]
        public TMP_Text zoneLabel;
        public TMP_Text titleText;
        public TMP_Text specText;
        public TMP_Text summaryText;
        public TMP_Text whyHeader;
        public TMP_Text whyText;
        public Image zoneAccent;
        public Image studiedBadge;
        public TMP_Text studiedBadgeText;
        public TouchButton closeButton;
        public TouchButton nextButton;

        [Header("Geometry (dp)")]
        public float peekHeight = 190f;
        public float expandedHeight = 560f;
        public float animSpeed = 13f;

        public SheetState State { get; private set; } = SheetState.Hidden;
        public PartInfo CurrentInfo { get; private set; }

        public event System.Action Closed;
        public event System.Action NextRequested;

        float _targetY;
        float _dragStartY;
        float _dragStartTargetY;
        bool _dragging;

        float _titleSize;

        void Awake()
        {
            // Long names ("Rotational and Translational Hand Controllers") wrapped to
            // three lines and ran into the text below. Let the title shrink a little
            // to stay inside its two-line box instead.
            if (titleText != null)
            {
                _titleSize = titleText.fontSize;
                titleText.enableAutoSizing = true;
                titleText.fontSizeMax = _titleSize;
                titleText.fontSizeMin = Mathf.Round(_titleSize * 0.7f);
                titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (sheet == null) sheet = (RectTransform)transform;
            if (group == null) group = GetComponent<CanvasGroup>();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (nextButton != null)
                nextButton.onClick.AddListener(() => { if (NextRequested != null) NextRequested(); });

            _targetY = -expandedHeight;
            ApplyImmediate();
        }

        void Update()
        {
            if (_dragging || sheet == null) return;

            var pos = sheet.anchoredPosition;
            pos.y = Mathf.Lerp(pos.y, _targetY, 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime));
            sheet.anchoredPosition = pos;

            if (group != null)
            {
                float visible = Mathf.InverseLerp(-expandedHeight, 0f, pos.y);
                group.alpha = Mathf.Clamp01(visible * 1.6f);
                group.blocksRaycasts = State != SheetState.Hidden;
                group.interactable = State != SheetState.Hidden;
            }
        }

        void ApplyImmediate()
        {
            if (sheet == null) return;
            var pos = sheet.anchoredPosition;
            pos.y = _targetY;
            sheet.anchoredPosition = pos;
            if (group != null)
            {
                group.alpha = State == SheetState.Hidden ? 0f : 1f;
                group.blocksRaycasts = State != SheetState.Hidden;
                group.interactable = State != SheetState.Hidden;
            }
        }

        // ---------------------------------------------------------------

        public void Show(PartInfo info, bool alreadyStudied)
        {
            if (info == null) { Hide(); return; }

            CurrentInfo = info;

            Color zoneColor = UITheme.ZoneColor(info.zone);
            if (zoneLabel != null)
            {
                zoneLabel.text = UITheme.ZoneLabel(info.zone);
                zoneLabel.color = zoneColor;
            }
            if (zoneAccent != null) zoneAccent.color = zoneColor;

            if (titleText != null) titleText.text = info.displayName;

            if (specText != null)
            {
                bool has = !string.IsNullOrEmpty(info.keySpec);
                specText.text = info.keySpec;
                specText.gameObject.SetActive(has);
            }

            if (summaryText != null) summaryText.text = info.summary;

            bool hasWhy = !string.IsNullOrEmpty(info.whyItMatters);
            if (whyText != null)
            {
                whyText.text = info.whyItMatters;
                whyText.gameObject.SetActive(hasWhy);
            }
            if (whyHeader != null) whyHeader.gameObject.SetActive(hasWhy);

            SetStudiedBadge(alreadyStudied);

            if (scroll != null) scroll.verticalNormalizedPosition = 1f;

            SetState(SheetState.Expanded);
            AudioDirector.Play("ui_open");
        }

        public void SetStudiedBadge(bool studied)
        {
            if (studiedBadge != null) studiedBadge.gameObject.SetActive(studied);
            if (studiedBadgeText != null) studiedBadgeText.text = "STUDIED";
        }

        public void Hide()
        {
            if (State == SheetState.Hidden) return;
            SetState(SheetState.Hidden);
            CurrentInfo = null;
            AudioDirector.Play("ui_close");
            if (Closed != null) Closed();
        }

        public void SetState(SheetState s)
        {
            State = s;
            switch (s)
            {
                case SheetState.Hidden:   _targetY = -expandedHeight; break;
                case SheetState.Peek:     _targetY = -(expandedHeight - peekHeight); break;
                case SheetState.Expanded: _targetY = 0f; break;
            }

            if (scroll != null) scroll.enabled = s == SheetState.Expanded;
        }

        public void SetNextButtonVisible(bool visible, string label = null)
        {
            if (nextButton == null) return;
            nextButton.gameObject.SetActive(visible);
            if (visible && !string.IsNullOrEmpty(label)) nextButton.SetLabel(label);
        }

        // ---------------------------------------------------------------
        // Drag between peek and expanded
        // ---------------------------------------------------------------

        public void OnBeginDrag(PointerEventData e)
        {
            if (State == SheetState.Hidden) return;
            _dragging = true;
            _dragStartY = e.position.y;
            _dragStartTargetY = sheet.anchoredPosition.y;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;

            float delta = e.position.y - _dragStartY;
            float y = Mathf.Clamp(_dragStartTargetY + delta, -expandedHeight, 0f);
            var pos = sheet.anchoredPosition;
            pos.y = y;
            sheet.anchoredPosition = pos;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;

            float y = sheet.anchoredPosition.y;
            float peekY = -(expandedHeight - peekHeight);

            // Flick velocity decides direction; otherwise snap to the nearest stop.
            float velocity = e.delta.y;

            if (velocity < -18f || y < peekY - peekHeight * 0.4f)
            {
                Hide();
            }
            else if (velocity > 18f)
            {
                SetState(SheetState.Expanded);
            }
            else
            {
                float distToExpanded = Mathf.Abs(y - 0f);
                float distToPeek = Mathf.Abs(y - peekY);
                SetState(distToExpanded <= distToPeek ? SheetState.Expanded : SheetState.Peek);
            }
        }
    }
}
