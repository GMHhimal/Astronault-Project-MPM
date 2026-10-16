using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR.Gen
{
    /// <summary>References the scene builder wires up after constructing the AR screens.</summary>
    public class ARUIRefs
    {
        public GameObject placementHud;
        public TMP_Text coachText;
        public TouchButton placeButton;

        public CanvasGroup explorerGroup;
        public ProgressRing ring;
        public TouchButton checklistButton;
        public TouchButton repositionButton;
        public TouchButton continueButton;
        public TouchButton soundButton;
        public TouchButton cabinButton;
        public TMP_Text cabinButtonLabel;
        public TouchButton ringButton;

        public GameObject scaleBar;
        public Slider scaleSlider;
        public TMP_Text scaleLabel;
        public List<TouchButton> presetButtons = new List<TouchButton>();

        public PartInfoSheet sheet;
        public ChecklistPanel checklist;
        public QuizDirector quiz;

        public GameObject finaleHud;
        public CanvasGroup finaleGroup;
        public TMP_Text finalePhase, finaleCountdown, finaleDeltaV, finaleAccel,
                        finaleMass, finaleBurnTime, finaleTimeWarp;
        public Image finaleProgress;
        public GameObject completeCard;
        public TMP_Text completeHeadline, completeScore, completeBody;
        public TouchButton completeButton;
    }

    /// <summary>
    /// Builds every screen in the AR experience and the main menu.
    ///
    /// Separated from SceneBuilder purely for readability; UI construction is verbose
    /// and would otherwise bury the scene logic. Every measurement comes from UITheme,
    /// so the look is retuned in one place rather than here.
    /// </summary>
    public static partial class SceneBuilderUI
    {
        const float G = UITheme.Gutter;

        // ================================================================
        // AR experience
        // ================================================================

        public static ARUIRefs BuildARUI(Transform canvas, QuizData quizData)
        {
            var ui = new ARUIRefs();

            // Safe area wrapper: everything interactive lives inside this.
            var safe = UIKit.Rect("SafeArea", canvas);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildCoachBar(safe, ui);      // shared by placement and exploring
            BuildPlacementHud(safe, ui);
            BuildExplorerHud(safe, ui);
            BuildScaleBar(safe, ui);
            ui.sheet = BuildInfoSheet(safe);
            ui.checklist = BuildChecklist(safe);
            ui.quiz = BuildQuizPanel(safe, quizData);
            BuildFinaleHud(safe, ui);

            return ui;
        }

        // ---------------------------------------------------------------
        // Coach bar
        //
        // Deliberately outside both phase HUDs. It is the only element that must stay
        // visible across placement and exploring, and parenting it to either one
        // meant it vanished the moment that phase ended.
        // ---------------------------------------------------------------

        static void BuildCoachBar(RectTransform parent, ARUIRefs ui)
        {
            var bar = UIKit.Panel("CoachBar", parent, UITheme.Alpha(UITheme.Space, 0.86f),
                                  UITheme.RadiusMd);
            UIKit.AnchorBottom(bar.rectTransform, 84f, 246f, G);

            ui.coachText = UIKit.Text("CoachText", bar.rectTransform,
                "Point your phone at the floor and move slowly",
                UITheme.FontBodyLg, UITheme.TextHi, TextAlignmentOptions.Center,
                lineSpacingMul: UITheme.LineNormal);
            UIKit.Stretch(ui.coachText.rectTransform, UITheme.Space4);
        }

        // ---------------------------------------------------------------
        // Placement
        // ---------------------------------------------------------------

        static void BuildPlacementHud(RectTransform parent, ARUIRefs ui)
        {
            var root = UIKit.Rect("PlacementHUD", parent);
            UIKit.Stretch(root);
            ui.placementHud = root.gameObject;

            // Scanning hint above the shared coach bar.
            var hint = UIKit.Overline("ScanHint", root, "AR  ·  SURFACE DETECTION", UITheme.Accent);
            UIKit.AnchorBottom(hint.rectTransform, 24f, 342f, G);
            hint.alignment = TextAlignmentOptions.Center;

            ui.placeButton = UIKit.Button("PlaceButton", root, "TAP TO PLACE ORION",
                                          UIKit.ButtonKind.Primary, UITheme.ButtonHeightLg);
            UIKit.AnchorBottom(ui.placeButton.GetComponent<RectTransform>(),
                               UITheme.ButtonHeightLg, 72f, G + 24f);
            ui.placeButton.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Explorer HUD
        // ---------------------------------------------------------------

        static void BuildExplorerHud(RectTransform parent, ARUIRefs ui)
        {
            var root = UIKit.Rect("ExplorerHUD", parent);
            UIKit.Stretch(root);
            ui.explorerGroup = root.gameObject.AddComponent<CanvasGroup>();

            // ---- top bar -----------------------------------------------
            var top = UIKit.Rect("TopBar", root);
            UIKit.AnchorTop(top, 96f, 8f, G);

            // Progress ring, top left.
            ui.ring = BuildProgressRing(top);
            ui.ringButton = ui.ring.gameObject.AddComponent<TouchButton>();
            ui.ringButton.pressScale = 0.92f;

            // Title block beside it.
            var titleCol = UIKit.Rect("TitleBlock", top);
            titleCol.anchorMin = new Vector2(0f, 0.5f);
            titleCol.anchorMax = new Vector2(1f, 0.5f);
            titleCol.pivot = new Vector2(0f, 0.5f);
            titleCol.offsetMin = new Vector2(84f, -35f);
            titleCol.offsetMax = new Vector2(-(3f * UITheme.TouchMin + 3f * UITheme.Space2), 35f);

            var kicker = UIKit.Overline("Kicker", titleCol, "LUNAR ORBIT", UITheme.Accent);
            kicker.textWrappingMode = TextWrappingModes.NoWrap;
            kicker.characterSpacing = 6f;
            UIKit.AnchorTop(kicker.rectTransform, 18f, 6f);

            var title = UIKit.Text("Title", titleCol, "ORION",
                                   UITheme.FontHeadline, UITheme.TextHi,
                                   TextAlignmentOptions.TopLeft, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            title.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(title.rectTransform, 32f, 26f);

            // Top-right buttons.
            ui.soundButton = UIKit.IconButton("SoundButton", top, Icons.VolumeOn);
            var sr = ui.soundButton.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(1f, 0.5f);
            sr.anchorMax = new Vector2(1f, 0.5f);
            sr.pivot = new Vector2(1f, 0.5f);
            sr.anchoredPosition = new Vector2(0f, 0f);

            ui.repositionButton = UIKit.IconButton("RepositionButton", top, Icons.Replace);
            var rr = ui.repositionButton.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(1f, 0.5f);
            rr.anchorMax = new Vector2(1f, 0.5f);
            rr.pivot = new Vector2(1f, 0.5f);
            rr.anchoredPosition = new Vector2(-(UITheme.TouchMin + UITheme.Space2), 0f);

            // Cabin view toggle: the way into the nine interior systems.
            ui.cabinButton = UIKit.IconButton("CabinButton", top, Icons.Layers);
            var cr2 = ui.cabinButton.GetComponent<RectTransform>();
            cr2.anchorMin = new Vector2(1f, 0.5f);
            cr2.anchorMax = new Vector2(1f, 0.5f);
            cr2.pivot = new Vector2(1f, 0.5f);
            cr2.anchoredPosition = new Vector2(-2f * (UITheme.TouchMin + UITheme.Space2), 0f);
            ui.cabinButton.normalColor = UITheme.Alpha(UITheme.AccentDim, 0.95f);
            if (ui.cabinButton.background != null) ui.cabinButton.background.color = ui.cabinButton.normalColor;

            ui.cabinButtonLabel = UIKit.Text("CabinCaption", cr2, "CABIN", UITheme.FontOverline - 2f,
                                             UITheme.Accent, TextAlignmentOptions.Top, heading: true,
                                             lineSpacingMul: UITheme.LineTight);
            ui.cabinButtonLabel.fontStyle = FontStyles.Bold;
            var cc = ui.cabinButtonLabel.rectTransform;
            cc.anchorMin = new Vector2(0f, 0f); cc.anchorMax = new Vector2(1f, 0f);
            cc.pivot = new Vector2(0.5f, 1f);
            cc.sizeDelta = new Vector2(20f, 14f);
            cc.anchoredPosition = new Vector2(0f, -3f);

            // ---- bottom actions -----------------------------------------
            ui.checklistButton = UIKit.Button("ChecklistButton", root, "SYSTEMS LIST",
                                              UIKit.ButtonKind.Secondary);
            var cb = ui.checklistButton.GetComponent<RectTransform>();
            UIKit.AnchorBottom(cb, UITheme.ButtonHeight, 24f, G);
            cb.anchorMin = new Vector2(0f, 0f);
            cb.anchorMax = new Vector2(0.48f, 0f);
            cb.offsetMin = new Vector2(G, 24f);
            cb.offsetMax = new Vector2(-UITheme.Space2, 24f + UITheme.ButtonHeight);

            ui.continueButton = UIKit.Button("ContinueButton", root, "READINESS CHECK",
                                             UIKit.ButtonKind.Primary);
            var qb = ui.continueButton.GetComponent<RectTransform>();
            qb.anchorMin = new Vector2(0.52f, 0f);
            qb.anchorMax = new Vector2(1f, 0f);
            qb.offsetMin = new Vector2(UITheme.Space2, 24f);
            qb.offsetMax = new Vector2(-G, 24f + UITheme.ButtonHeight);
            ui.continueButton.gameObject.SetActive(false);
        }

        static ProgressRing BuildProgressRing(RectTransform parent)
        {
            var root = UIKit.Rect("ProgressRing", parent);
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(0f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(72f, 72f);
            root.anchoredPosition = new Vector2(0f, 0f);

            var bg = UIKit.Panel("Backdrop", root, UITheme.Alpha(UITheme.Space, 0.75f), 36f);
            UIKit.Stretch(bg.rectTransform);

            var ringSprite = UIKit.RingSprite(256, 0.10f);

            var track = root.gameObject.AddComponent<Image>();
            track.sprite = ringSprite;
            track.color = UITheme.Alpha(UITheme.Outline, 0.75f);
            track.raycastTarget = true;     // the ring is a button: tap for the list, hold for presenter mode

            var fillRT = UIKit.Rect("Fill", root);
            UIKit.Stretch(fillRT);
            var fill = fillRT.gameObject.AddComponent<Image>();
            fill.sprite = ringSprite;
            fill.color = UITheme.Accent;
            fill.raycastTarget = false;

            var count = UIKit.Text("Count", root, "0/17", UITheme.FontBody, UITheme.TextHi,
                                   TextAlignmentOptions.Center, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            count.fontStyle = FontStyles.Bold;
            UIKit.Stretch(count.rectTransform);
            count.rectTransform.anchoredPosition = new Vector2(0f, 5f);

            var caption = UIKit.Text("Caption", root, "SYSTEMS", UITheme.FontOverline,
                                     UITheme.TextLow, TextAlignmentOptions.Center,
                                     lineSpacingMul: UITheme.LineTight);
            caption.characterSpacing = 4f;
            caption.fontSize = UITheme.FontOverline - 3f;
            UIKit.Stretch(caption.rectTransform);
            caption.rectTransform.anchoredPosition = new Vector2(0f, -14f);

            var ring = root.gameObject.AddComponent<ProgressRing>();
            ring.track = track;
            ring.fill = fill;
            ring.countText = count;
            ring.captionText = caption;
            return ring;
        }

        // ---------------------------------------------------------------
        // Scale bar
        // ---------------------------------------------------------------

        static void BuildScaleBar(RectTransform parent, ARUIRefs ui)
        {
            var root = UIKit.Panel("ScaleBar", parent, UITheme.Alpha(UITheme.Space, 0.80f),
                                   UITheme.RadiusMd);
            UIKit.AnchorBottom(root.rectTransform, 132f, 100f, G);
            ui.scaleBar = root.gameObject;

            var label = UIKit.Overline("ScaleCaption", root.rectTransform, "MODEL SCALE", UITheme.TextLow);
            UIKit.AnchorTop(label.rectTransform, 18f, 12f, UITheme.Space4);

            ui.scaleLabel = UIKit.Text("ScaleValue", root.rectTransform, "55 cm tall",
                                       UITheme.FontLabel, UITheme.Gold,
                                       TextAlignmentOptions.TopRight, heading: true,
                                       lineSpacingMul: UITheme.LineTight);
            UIKit.AnchorTop(ui.scaleLabel.rectTransform, 20f, 10f, UITheme.Space4);

            // Slider.
            var sliderRT = UIKit.Rect("ScaleSlider", root.rectTransform);
            UIKit.AnchorTop(sliderRT, 32f, 36f, UITheme.Space4);

            var slider = sliderRT.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;

            var sliderBg = UIKit.Panel("Background", sliderRT, UITheme.SurfaceHi, 6f);
            sliderBg.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            sliderBg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            sliderBg.rectTransform.sizeDelta = new Vector2(0f, 8f);

            var fillArea = UIKit.Rect("Fill Area", sliderRT);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-20f, 8f);
            var fill = UIKit.Panel("Fill", fillArea, UITheme.Accent, 6f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 8f);

            var handleArea = UIKit.Rect("Handle Slide Area", sliderRT);
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.sizeDelta = new Vector2(-28f, 0f);
            var handle = UIKit.Panel("Handle", handleArea, UITheme.AccentGlow, 14f);
            handle.rectTransform.sizeDelta = new Vector2(28f, 28f);
            handle.raycastTarget = true;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            ui.scaleSlider = slider;

            // Preset chips. Four named sizes cover the useful range without anyone
            // having to understand what "0.32" means.
            var chips = UIKit.Rect("Presets", root.rectTransform);
            UIKit.AnchorBottom(chips, 44f, 12f, UITheme.Space4);
            var chipRow = UIKit.HStack(chips, UITheme.Space2, null, TextAnchor.MiddleCenter);
            chipRow.childForceExpandWidth = true;

            string[] names = { "TABLETOP", "DESK", "ROOM", "LIFE SIZE" };
            for (int i = 0; i < names.Length; i++)
            {
                var chip = UIKit.Button("Preset_" + i, chips, names[i],
                                        UIKit.ButtonKind.Ghost, 42f, UITheme.FontCaption);
                var le = UIKit.Sized(chip.gameObject, 42f);
                le.flexibleWidth = 1f;
                if (chip.label != null) { chip.label.characterSpacing = 1f; chip.label.rectTransform.offsetMin = new Vector2(4f, 0f); chip.label.rectTransform.offsetMax = new Vector2(-4f, 0f); }
                ui.presetButtons.Add(chip);
            }
        }

        // ---------------------------------------------------------------
        // Part info sheet
        // ---------------------------------------------------------------

        static PartInfoSheet BuildInfoSheet(RectTransform parent)
        {
            const float expanded = 500f;
            const float peek = 200f;

            var root = UIKit.Rect("PartInfoSheet", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, expanded);
            root.anchoredPosition = new Vector2(0f, -expanded);

            var group = root.gameObject.AddComponent<CanvasGroup>();

            var bg = UIKit.Panel("Background", root, UITheme.Alpha(UITheme.Surface, 0.97f),
                                 UITheme.RadiusLg);
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            // Zone accent along the top edge.
            var accent = UIKit.Panel("ZoneAccent", root, UITheme.Accent, UITheme.RadiusSm);
            UIKit.AnchorTop(accent.rectTransform, 4f, 0f, 0f);

            // Grab handle.
            var handle = UIKit.Panel("GrabHandle", root, UITheme.Outline, 3f);
            handle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            handle.rectTransform.pivot = new Vector2(0.5f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(56f, 5f);
            handle.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            var zone = UIKit.Overline("ZoneLabel", root, "CREW CABIN", UITheme.Accent);
            UIKit.AnchorTop(zone.rectTransform, 18f, 28f, G);

            var studiedBadge = UIKit.Panel("StudiedBadge", root,
                                           UITheme.Alpha(UITheme.SuccessDim, 0.9f), UITheme.RadiusPill);
            studiedBadge.rectTransform.anchorMin = new Vector2(1f, 1f);
            studiedBadge.rectTransform.anchorMax = new Vector2(1f, 1f);
            studiedBadge.rectTransform.pivot = new Vector2(1f, 1f);
            studiedBadge.rectTransform.sizeDelta = new Vector2(118f, 30f);
            studiedBadge.rectTransform.anchoredPosition = new Vector2(-G, -24f);

            var badgeText = UIKit.Text("BadgeText", studiedBadge.rectTransform, "STUDIED",
                                       UITheme.FontOverline, UITheme.Success,
                                       TextAlignmentOptions.Center, heading: true,
                                       lineSpacingMul: UITheme.LineTight);
            badgeText.fontStyle = FontStyles.Bold;
            UIKit.Stretch(badgeText.rectTransform);
            studiedBadge.gameObject.SetActive(false);

            var title = UIKit.Text("Title", root, "Component",
                                   UITheme.FontTitle, UITheme.TextHi,
                                   TextAlignmentOptions.TopLeft, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            title.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(title.rectTransform, 68f, 50f, G);

            var spec = UIKit.Text("Spec", root, "", UITheme.FontLabel, UITheme.Gold,
                                  TextAlignmentOptions.TopLeft);
            UIKit.AnchorTop(spec.rectTransform, 24f, 120f, G);

            // Scrollable body.
            var scrollRT = UIKit.Rect("Scroll", root);
            UIKit.Fill(scrollRT, G, G, 154f, 96f);

            var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;

            var viewport = UIKit.Rect("Viewport", scrollRT);
            UIKit.Stretch(viewport);
            var mask = viewport.gameObject.AddComponent<Image>();
            mask.color = new Color(1f, 1f, 1f, 0.003f);     // must be present for masking
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;      // a new rect is 100x100: without this the
            content.anchoredPosition = Vector2.zero; // content overhangs the viewport by 50 dp each side
            // The stack sizes each paragraph from TextMeshPro's preferred height.
            // Previously it did not control child height and each text carried its own
            // size fitter, which fought the layout and stacked the paragraphs on top
            // of each other.
            var stack = UIKit.VStack(content, UITheme.Space3, new RectOffset(0, 0, 4, 24), TextAnchor.UpperLeft);
            stack.childControlHeight = true;
            stack.childForceExpandHeight = false;
            UIKit.FitHeight(content);
            scroll.content = content;

            var summary = UIKit.Text("Summary", content, "", UITheme.FontBody, UITheme.TextHi,
                                     TextAlignmentOptions.TopLeft,
                                     lineSpacingMul: UITheme.LineRelaxed);
            summary.textWrappingMode = TextWrappingModes.Normal;

            var whyHeader = UIKit.Overline("WhyHeader", content, "WHY IT MATTERS", UITheme.Accent);
            UIKit.Sized(whyHeader.gameObject, 22f);

            var why = UIKit.Text("WhyText", content, "", UITheme.FontBody, UITheme.TextMid,
                                 TextAlignmentOptions.TopLeft,
                                 lineSpacingMul: UITheme.LineRelaxed);
            why.textWrappingMode = TextWrappingModes.Normal;

            // Footer actions.
            var close = UIKit.Button("CloseButton", root, "CLOSE", UIKit.ButtonKind.Secondary);
            var closeRT = close.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0f, 0f);
            closeRT.anchorMax = new Vector2(0.46f, 0f);
            closeRT.offsetMin = new Vector2(G, 20f);
            closeRT.offsetMax = new Vector2(-UITheme.Space2, 20f + UITheme.ButtonHeight);

            var next = UIKit.Button("NextButton", root, "NEXT SYSTEM", UIKit.ButtonKind.Primary);
            var nextRT = next.GetComponent<RectTransform>();
            nextRT.anchorMin = new Vector2(0.54f, 0f);
            nextRT.anchorMax = new Vector2(1f, 0f);
            nextRT.offsetMin = new Vector2(UITheme.Space2, 20f);
            nextRT.offsetMax = new Vector2(-G, 20f + UITheme.ButtonHeight);

            var sheet = root.gameObject.AddComponent<PartInfoSheet>();
            sheet.sheet = root;
            sheet.group = group;
            sheet.grabHandle = handle.rectTransform;
            sheet.scroll = scroll;
            sheet.zoneLabel = zone;
            sheet.titleText = title;
            sheet.specText = spec;
            sheet.summaryText = summary;
            sheet.whyHeader = whyHeader;
            sheet.whyText = why;
            sheet.zoneAccent = accent;
            sheet.studiedBadge = studiedBadge;
            sheet.studiedBadgeText = badgeText;
            sheet.closeButton = close;
            sheet.nextButton = next;
            sheet.peekHeight = peek;
            sheet.expandedHeight = expanded;

            return sheet;
        }

        // ---------------------------------------------------------------
        // Checklist
        // ---------------------------------------------------------------

        static ChecklistPanel BuildChecklist(RectTransform parent)
        {
            // Full width on a phone: a 412 dp screen has no room for a side drawer.
            const float width = 460f;      // off-screen distance when hidden

            var root = UIKit.Rect("ChecklistPanel", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 0.5f);
            root.offsetMin = new Vector2(8f, 8f);
            root.offsetMax = new Vector2(-8f, -8f);
            root.anchoredPosition = new Vector2(width, 0f);

            var group = root.gameObject.AddComponent<CanvasGroup>();

            var bg = UIKit.Panel("Background", root, UITheme.Alpha(UITheme.Surface, 0.98f),
                                 UITheme.RadiusLg);
            UIKit.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            var header = UIKit.Text("Header", root, "SPACECRAFT SYSTEMS",
                                    UITheme.FontHeadline, UITheme.TextHi,
                                    TextAlignmentOptions.TopLeft, heading: true,
                                    lineSpacingMul: UITheme.LineTight);
            header.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(header.rectTransform, 32f, 26f, G);

            var sub = UIKit.Text("Subheader", root, "0 of 17 studied",
                                 UITheme.FontLabel, UITheme.TextMid, TextAlignmentOptions.TopLeft);
            UIKit.AnchorTop(sub.rectTransform, 22f, 62f, G);

            var close = UIKit.IconButton("CloseButton", root, Icons.Close);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(1f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-12f, -16f);

            // Scrollable rows.
            var scrollRT = UIKit.Rect("Scroll", root);
            UIKit.Fill(scrollRT, 12f, 12f, 98f, 16f);

            var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 34f;

            var viewport = UIKit.Rect("Viewport", scrollRT);
            UIKit.Stretch(viewport);
            var maskImg = viewport.gameObject.AddComponent<Image>();
            maskImg.color = new Color(1f, 1f, 1f, 0.003f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;      // a new rect is 100x100: without this the
            content.anchoredPosition = Vector2.zero; // content overhangs the viewport by 50 dp each side
            UIKit.VStack(content, UITheme.Space2, new RectOffset(0, 0, 0, 20), TextAnchor.UpperCenter);
            UIKit.FitHeight(content);
            scroll.content = content;

            var panel = root.gameObject.AddComponent<ChecklistPanel>();
            panel.panel = root;
            panel.group = group;
            panel.content = content;
            panel.headerText = header;
            panel.subheaderText = sub;
            panel.closeButton = close;
            panel.hiddenX = width;

            return panel;
        }
    }
}
