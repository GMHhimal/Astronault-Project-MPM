using System.Collections.Generic;
using System.Linq;
using RocketTimeline.App;
using RocketTimeline.Data;
using RocketTimeline.Gameplay;
using RocketTimeline.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RocketTimeline.EditorTools
{
    /// <summary>
    /// Builds the complete "Rocket Evolution Timeline" project from code: a
    /// Main Menu scene and the game scene, fully styled and wired end to end
    /// (Canvas, timeline slots, rocket pool, buttons, result text, trivia
    /// popup, scene navigation, Build Settings). See the class-level comment
    /// in the previous version of this file (or SETUP_GUIDE.md) for why this
    /// is done with editor code instead of a hand-authored .unity file: it
    /// guarantees every reference and every scene-navigation hookup is wired
    /// correctly, because it's just C# calling the same APIs the Editor uses.
    ///
    /// USAGE: Unity menu bar -> Rocket Timeline -> Build Everything.
    /// Re-running it overwrites both scenes with a fresh copy, so it's safe
    /// to use again after tweaking the constants/colors below. Once you've
    /// started customizing a scene by hand in the Editor, stop re-running
    /// the builder for that scene (or duplicate it first).
    /// </summary>
    public static class RocketTimelineSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";
        private const string GameScenePath = ScenesFolder + "/RocketTimelineGame.unity";
        private const string MainMenuScenePath = ScenesFolder + "/MainMenu.unity";

        private const string RocketSpritesFolder = "Assets/_Project/Sprites/Rockets";
        private const string UISpritesFolder = "Assets/_Project/Sprites/UI";
        private const string BackgroundsFolder = "Assets/_Project/Sprites/Backgrounds";
        private const string TopicSpritesFolder = "Assets/_Project/Sprites/Topics";

        // Order here only affects the pool's starting sibling order in the
        // Hierarchy - GameManager shuffles the pool at Play time regardless.
        // v11: grew from 8 to 16 entries - both the timeline strip and the
        // pool grid are now scrollable (see BuildScrollingRow/BuildScrollingGrid)
        // specifically so this list can keep growing without a layout rewrite.
        // v12: briefly grew to 18 entries (Mercury-Redstone, Gemini-Titan II
        // added as both drag-and-drop pieces and trivia).
        // v13: per client feedback, Mercury-Redstone and Gemini-Titan II are
        // trivia-only now - pulled back out of both Zones and the pool below,
        // since their suborbital/early-orbital missions don't have a clean
        // single "this is the one invention that defines this slot" answer
        // the way every other drag-and-drop entry does. They're still fully
        // present in RocketData.json and still appear in the Trivia Review
        // slideshow - see TriviaRocketIdsInOrder further down, which is now
        // the *only* list that includes them.
        private static readonly (string id, string label)[] Zones =
        {
            ("13TH_CENTURY", "13th Century"),
            ("16TH_CENTURY", "16th Century (Legend)"),
            ("LATE_18TH_CENTURY", "Late 1700s"),
            ("1805", "1805"),
            ("1926", "1926"),
            ("1942_1944", "1942 - 1944"),
            ("1957", "1957"),
            ("1958", "1958"),
            ("1959_1968", "1959 - 1968"),
            ("1967_1969", "1967 - 1969"),
            ("1981", "1981"),
            ("2004", "2004"),
            ("2015_2017", "2015 - 2017"),
            ("2021", "2021"),
            ("2022", "2022"),
            ("2023", "2023"),
        };

        private static readonly string[] RocketIdsInPoolOrder =
        {
            "chinese_fire_arrow", "wan_hu_chair", "tipu_sultan_rockets", "congreve_rocket",
            "goddard_nell", "v2_aggregat4", "r7_sputnik", "explorer1_juno",
            "x15", "saturn_v", "space_shuttle", "delta_iv_heavy",
            "falcon_9", "new_shepard", "sls", "starship",
        };

        // Every rocket in RocketData.json, in full chronological order - used
        // only by the Trivia Review slideshow (BuildTriviaReviewPanel), which
        // is meant to be a complete rocket encyclopedia regardless of which
        // entries are playable drag-and-drop pieces. This is the one place
        // Mercury-Redstone and Gemini-Titan II still appear post-v13.
        private static readonly string[] TriviaRocketIdsInOrder =
        {
            "chinese_fire_arrow", "wan_hu_chair", "tipu_sultan_rockets", "congreve_rocket",
            "goddard_nell", "v2_aggregat4", "r7_sputnik", "explorer1_juno",
            "x15", "mercury_redstone", "gemini_titan", "saturn_v", "space_shuttle", "delta_iv_heavy",
            "falcon_9", "new_shepard", "sls", "starship",
        };

        // ---- Palette ----------------------------------------------------------------
        // v11 "Mission Control HUD" pass: shifted from the original soft
        // blue/gold scheme toward a cockpit-display palette - cyan for the
        // primary accent/action color (echoing the chamfered-panel chrome
        // and grid-overlay background from gen_ui_assets.py), amber kept as
        // the secondary/"caution" accent (a real HUD convention), and a
        // cooler, darker navy base throughout.
        // v13: ButtonSlate pushed a little more blue/saturated (was a fairly
        // generic dark grey) now that gen_ui_assets.py's neutral-grey base
        // texture actually reproduces tint saturation correctly - see
        // DESIGN_NOTES.md's v13 section for why the old base texture was
        // quietly muddying every tint regardless of which color was chosen.
        // v14: Blue/Red/Slate brightened again - these colors are now also
        // used directly as ghost-button BORDER and LABEL TEXT color (see
        // BuildStyledButton), not just a fill multiply with a white label on
        // top, so they need to read clearly against a near-black background
        // on their own. Gold was already bright enough to serve both jobs
        // unchanged, which is also why it stayed the one color used for the
        // solid Play button's fill.
        // v15.2: 0.93 -> 0.99 - client feedback was that the Main Menu's
        // background was visibly bleeding through the Trivia/About overlay
        // cards, hurting text readability. The ui_panel texture itself is
        // already fully opaque inside the rounded silhouette (gen_ui_assets.
        // py's make_card does fill.putalpha(mask), not a translucent fill),
        // so that bleed was entirely this tint's own alpha - bumping it
        // closes almost all of the gap without touching the art.
        private static readonly Color PanelTint = new Color(0.12f, 0.15f, 0.23f, 0.99f);
        private static readonly Color CardTint = new Color(0.09f, 0.12f, 0.19f, 0.96f);
        private static readonly Color LineColor = new Color(0.30f, 0.80f, 0.92f, 0.9f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color MutedTextColor = new Color(0.72f, 0.84f, 0.90f);
        private static readonly Color ButtonBlue = new Color(0.22f, 0.74f, 0.87f);
        private static readonly Color ButtonRed = new Color(0.90f, 0.35f, 0.33f);
        private static readonly Color ButtonSlate = new Color(0.55f, 0.63f, 0.76f);
        private static readonly Color ButtonGold = new Color(0.93f, 0.66f, 0.16f);
        // v15.2: Play button moved off gold onto a cosmic blue-violet, per
        // client request ("Blue / cosmic purple"). ButtonGold is kept around
        // for the Game scene's See Trivia button, which wasn't part of this
        // ask.
        private static readonly Color ButtonCosmicPurple = new Color(0.50f, 0.36f, 0.95f);
        private static readonly Vector2 PlacedRocketSize = new Vector2(148, 148);

        // v14: every Main Menu secondary-action button (Rocket Trivia + each
        // About topic) is now built at this one fixed size, in one row
        // (ActionRow - see BuildMainMenuSceneInternal), specifically because
        // v13's mismatched button widths and separately-positioned About row
        // was the client's own "make it symmetric and consistent" feedback.
        // Text wraps to 2 lines within this box for the longest label
        // ("About International Space Station") rather than the box
        // resizing - see BonusButtonLabels just below.
        private static readonly Vector2 ActionButtonSize = new Vector2(280, 76);

        // Bonus-topic Main Menu button text, keyed by TopicData.json's
        // topic.id - kept separate from topic.displayName (the longer title
        // shown on the slideshow panel itself) so the Main Menu reads exactly
        // "About Satellites" / "About Rovers" / "About International Space
        // Station" as asked for, while the panel header can stay more
        // descriptive ("Rovers & Robotic Explorers").
        private static readonly Dictionary<string, string> BonusButtonLabels = new Dictionary<string, string>
        {
            { "satellites", "About Satellites" },
            { "rovers", "About Rovers" },
            { "iss", "About International Space Station" },
        };

        [MenuItem("Rocket Timeline/Build Everything (Menu + Game)")]
        public static void BuildEverything()
        {
            RocketItem[] rocketData = LoadRocketDataForEditor();
            if (rocketData == null) return;
            TopicItem[] topicData = LoadTopicDataForEditor();

            EnsureFolderExists(ScenesFolder);

            BuildMainMenuSceneInternal(rocketData, topicData);
            BuildGameSceneInternal(rocketData);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            EditorSceneManager.OpenScene(MainMenuScenePath);

            EditorUtility.DisplayDialog("Rocket Timeline",
                "Built both scenes and registered them in Build Settings.\n\n" +
                "The Main Menu scene is open now - press Play and click Play in-game " +
                "to jump into the timeline, then try the Main Menu button from there too.",
                "Nice");
        }

        [MenuItem("Rocket Timeline/Build Game Scene Only")]
        public static void BuildGameSceneOnly()
        {
            RocketItem[] rocketData = LoadRocketDataForEditor();
            if (rocketData == null) return;
            EnsureFolderExists(ScenesFolder);
            BuildGameSceneInternal(rocketData);
        }

        [MenuItem("Rocket Timeline/Build Main Menu Scene Only")]
        public static void BuildMainMenuSceneOnly()
        {
            // The Main Menu now also builds its own "Rocket Trivia" panel, which
            // needs the rocket data, so this loads it the same way the game
            // scene builder always has - plus the v13 bonus-topic data for
            // the Satellites/Rovers/ISS buttons.
            RocketItem[] rocketData = LoadRocketDataForEditor();
            if (rocketData == null) return;
            TopicItem[] topicData = LoadTopicDataForEditor();
            EnsureFolderExists(ScenesFolder);
            BuildMainMenuSceneInternal(rocketData, topicData);
        }

        // ================================================================================
        // GAME SCENE
        // ================================================================================

        private static void BuildGameSceneInternal(RocketItem[] rocketData)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();
            CreateCamera();
            RectTransform canvasRoot = CreateCanvas();

            CreateBackground(canvasRoot);

            RectTransform title = CreateText("Title", canvasRoot, "Rocket Evolution Timeline", 46, TextColor, TextAnchor.UpperLeft, FontStyle.Bold);
            Anchor(title, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(-80, 55));

            RectTransform objective = CreateText("Objective", canvasRoot,
                "Objective: Drag & Drop each rocket into the timeline slot where it belongs.",
                24, MutedTextColor, TextAnchor.UpperLeft);
            Anchor(objective, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -85), new Vector2(-80, 35));

            // ---- Timeline area ----------------------------------------------------
            // v11: grew from 8 to 16 rockets, so a fixed 2-row grid no longer
            // fits without either shrinking every card or risking overlap
            // again. Instead this is now a horizontally-scrolling single row
            // (BuildScrollArea) - the viewport stays a fixed 430px-tall box
            // regardless of how many rockets the roster grows to in the
            // future, and the row just gets wider and scrolls. A thin
            // scrollbar along the bottom makes it obvious there's more to
            // see. Scrolling is a LEFT/RIGHT gesture specifically so it can't
            // be confused with the UP drag that carries a card out of the
            // pool below into a slot here - see DraggableRocket's
            // scroll-vs-pickup forwarding logic for the other half of that.
            RectTransform timelineArea = CreateUIObject("TimelineArea", canvasRoot);
            Anchor(timelineArea, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(-80, 430));

            BuildScrollArea("TimelineScroll", timelineArea, horizontal: true, vertical: false,
                out RectTransform timelineContent, out ScrollRect timelineScrollRect);

            HorizontalLayoutGroup timelineLayout = timelineContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            timelineLayout.spacing = 26;
            timelineLayout.padding = new RectOffset(4, 4, 0, 0);
            timelineLayout.childAlignment = TextAnchor.MiddleCenter;
            timelineLayout.childForceExpandWidth = false;
            timelineLayout.childForceExpandHeight = true;
            timelineLayout.childControlWidth = true;
            timelineLayout.childControlHeight = true;
            ContentSizeFitter timelineFitter = timelineContent.gameObject.AddComponent<ContentSizeFitter>();
            timelineFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var dropZoneLookup = new Dictionary<string, DropZone>();
            foreach (var zoneDef in Zones)
            {
                DropZone zone = BuildDropZone(timelineContent, zoneDef.id, zoneDef.label);
                dropZoneLookup[zoneDef.id] = zone;
            }

            BuildScrollbar(timelineArea, timelineScrollRect, horizontal: true);

            // ---- Rocket pool --------------------------------------------------------
            // v11: same reasoning as the timeline above - a fixed 2-row-tall
            // scroll area that scrolls sideways to fit however many rocket
            // cards exist, 2 rows at a time, instead of requiring every card
            // to physically fit on screen at once.
            // v15: client feedback was the cards read too small to clearly
            // make out the rocket, and the tray looked "offset to the left."
            // Cell/card grew 240x145 -> 264x165 (sized against the budget
            // below: 165*2 + 20 spacing = 350, so PoolArea's height grew to
            // match, 310 -> 350; still a clean 15-unit gap under
            // TimelineArea's bottom at 515, see that Anchor call above). The
            // "offset left" part isn't this sizing though - it's that a
            // left-pivoted scroll content (needed so it can grow/scroll
            // rightward once there's more than fits) just sits flush-left
            // whenever the laid-out row is narrower than the viewport.
            // CenterScrollContentIfFits (attached below) re-centers it
            // whenever that's the case and gets out of the way otherwise.
            RectTransform poolArea = CreateUIObject("PoolArea", canvasRoot);
            Anchor(poolArea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(-80, 350));

            BuildScrollArea("PoolScroll", poolArea, horizontal: true, vertical: false,
                out RectTransform poolContent, out ScrollRect poolScrollRect);
            poolContent.gameObject.AddComponent<CenterScrollContentIfFits>();

            GridLayoutGroup grid = poolContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(264, 165);
            grid.spacing = new Vector2(24, 20);
            grid.padding = new RectOffset(4, 4, 0, 0);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 2;
            ContentSizeFitter poolFitter = poolContent.gameObject.AddComponent<ContentSizeFitter>();
            poolFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildScrollbar(poolArea, poolScrollRect, horizontal: true);

            GameObject gameManagerGo = new GameObject("GameManager", typeof(GameManager));
            GameManager gameManager = gameManagerGo.GetComponent<GameManager>();

            GameObject databaseGo = new GameObject("RocketDatabase", typeof(RocketDatabase));
            gameManager.database = databaseGo.GetComponent<RocketDatabase>();

            GameObject sceneLoaderGo = new GameObject("SceneLoader", typeof(SceneLoader));
            SceneLoader sceneLoader = sceneLoaderGo.GetComponent<SceneLoader>();

            var rocketsById = rocketData.ToDictionary(r => r.id, r => r);
            var draggableRockets = new List<DraggableRocket>();
            foreach (string id in RocketIdsInPoolOrder)
            {
                if (!rocketsById.TryGetValue(id, out RocketItem data))
                {
                    Debug.LogWarning($"[RocketTimelineSceneBuilder] No RocketData.json entry found for id '{id}', skipping.");
                    continue;
                }
                draggableRockets.Add(BuildRocketCard(poolContent, data));
            }

            // ---- Controls bar ---------------------------------------------------------
            RectTransform controlsBar = CreateUIObject("ControlsBar", canvasRoot);
            Anchor(controlsBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(-80, 95));
            HorizontalLayoutGroup controlsLayout = controlsBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            controlsLayout.spacing = 24;
            controlsLayout.childAlignment = TextAnchor.MiddleCenter;
            controlsLayout.childForceExpandWidth = false;
            controlsLayout.childForceExpandHeight = true;

            Button checkButton = BuildStyledButton(controlsBar, "CheckAnswersButton", "Check Answers", ButtonBlue, 280, 88);
            Button retryButton = BuildStyledButton(controlsBar, "RetryButton", "Retry", ButtonRed, 200, 88);
            Button seeTriviaButton = BuildStyledButton(controlsBar, "SeeTriviaButton", "See Trivia", ButtonGold, 220, 88);
            Button mainMenuButton = BuildStyledButton(controlsBar, "MainMenuButton", "Main Menu", ButtonSlate, 200, 88);

            // ---- Result panel -----------------------------------------------------------
            RectTransform resultPanel = BuildNineSlicePanel("ResultPanel", canvasRoot, "ui_panel", PanelTint, new Vector2(1100, 300));
            Anchor(resultPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(1100, 300));
            VerticalLayoutGroup resultLayout = resultPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            resultLayout.spacing = 6;
            resultLayout.padding = new RectOffset(30, 30, 24, 24);
            resultLayout.childAlignment = TextAnchor.MiddleCenter;
            resultLayout.childForceExpandWidth = true;
            resultLayout.childForceExpandHeight = false;
            resultLayout.childControlWidth = true;
            resultLayout.childControlHeight = true;

            Text resultTitle = AddParagraph(resultPanel, "ResultTitle", 30, TextColor, FontStyle.Bold, 60, TextAnchor.MiddleCenter);
            Text resultDetail = AddParagraph(resultPanel, "ResultDetail", 24, MutedTextColor, FontStyle.Normal, 40, TextAnchor.MiddleCenter);
            Text flavorText = AddParagraph(resultPanel, "FlavorText", 22, MutedTextColor, FontStyle.Italic, 70, TextAnchor.MiddleCenter);

            resultPanel.gameObject.SetActive(false);

            // ---- Trivia popup ------------------------------------------------------------
            TriviaPopupUI triviaPopup = BuildTriviaPopup(canvasRoot);

            // ---- Trivia review screen (post-game "See Trivia" button) --------------------
            // Built as a full-screen overlay on top of everything else in this canvas,
            // initially inactive. The See Trivia button (shown only once Check Answers
            // has been pressed - see GameManager) opens it; its own Close button closes
            // it. Both are wired here via persistent listeners for the same reason the
            // Main Menu button needs one: a UnityEvent/Button wired only at runtime
            // wouldn't survive being saved as part of this scene.
            TriviaReviewPagerUI triviaReviewPager = BuildTriviaReviewPanel(canvasRoot, rocketData);
            UnityEventTools.AddVoidPersistentListener(seeTriviaButton.onClick, triviaReviewPager.Show);

            // ---- Wire GameManager ---------------------------------------------------------
            gameManager.dropZones = Zones.Select(z => dropZoneLookup[z.id]).ToArray();
            gameManager.rockets = draggableRockets.ToArray();
            gameManager.checkAnswersButton = checkButton;
            gameManager.retryButton = retryButton;
            gameManager.seeTriviaButton = seeTriviaButton;
            gameManager.mainMenuButton = mainMenuButton;
            gameManager.resultPanel = resultPanel.gameObject;
            gameManager.resultTitleText = resultTitle;
            gameManager.resultDetailText = resultDetail;
            gameManager.flavorText = flavorText;
            gameManager.triviaPopup = triviaPopup;

            // Now that gameManager exists, wire the info buttons that reference it.
            foreach (var rocket in draggableRockets)
            {
                var infoButton = rocket.GetComponentInChildren<RocketInfoButton>(true);
                if (infoButton != null) infoButton.gameManager = gameManager;
            }

            // Persistent listener: the button click itself is wired at runtime in
            // GameManager.Start(), but the UnityEvent it raises needs a *persistent*
            // (saved-with-the-scene) listener here, or "Main Menu" would do nothing.
            // Defensive guard: GameManager.onMainMenuRequested is now constructed
            // inline (= new UnityEvent()) so this should never be null, but
            // AddVoidPersistentListener does not null-check its own argument and
            // throws a confusing NullReferenceException deep inside Unity's own
            // code if it ever is - so this is cheap, safe insurance against that
            // exact class of crash, here and for any future UnityEvent field.
            if (gameManager.onMainMenuRequested == null) gameManager.onMainMenuRequested = new UnityEvent();
            UnityEventTools.AddVoidPersistentListener(gameManager.onMainMenuRequested, sceneLoader.LoadMainMenu);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScenePath);
            Debug.Log($"[RocketTimelineSceneBuilder] Game scene saved to {GameScenePath}.");
        }

        private static DropZone BuildDropZone(Transform parent, string zoneId, string label)
        {
            RectTransform box = BuildNineSlicePanel($"Zone_{zoneId}", parent, "ui_panel", PanelTint, new Vector2(300, 200));
            LayoutElement layoutElement = box.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 300;
            layoutElement.preferredHeight = 200;
            box.gameObject.GetComponent<Image>().raycastTarget = true;

            RectTransform outline = BuildNineSliceImage("Outline", box, "ui_ring", Color.green);
            Image outlineImage = outline.GetComponent<Image>();
            outlineImage.raycastTarget = false;
            outlineImage.enabled = false;
            Anchor(outline, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));

            RectTransform labelRect = CreateText("Label", box, label, 22, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(labelRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(-16, 34));

            // Vertically centered in the space *below* the label (box height
            // grew in this pass specifically to give this room), rather than a
            // fixed low fraction that worked for the old shorter boxes but left
            // a bigger placed rocket sprite poking into the label or the box's
            // outline ring.
            RectTransform slotAnchor = CreateUIObject("SlotAnchor", box);
            slotAnchor.anchorMin = slotAnchor.anchorMax = new Vector2(0.5f, 0.396f);
            slotAnchor.pivot = new Vector2(0.5f, 0.5f);
            slotAnchor.sizeDelta = PlacedRocketSize;
            slotAnchor.anchoredPosition = Vector2.zero;

            DropZone dropZone = box.gameObject.AddComponent<DropZone>();
            dropZone.zoneId = zoneId;
            dropZone.outline = outlineImage;
            dropZone.slot = slotAnchor;
            dropZone.placedSize = PlacedRocketSize;

            return dropZone;
        }

        private static DraggableRocket BuildRocketCard(Transform parent, RocketItem data)
        {
            // v15: card grew to match the pool grid's new cellSize (see the
            // comment above the pool's GridLayoutGroup setup) and the icon's
            // anchor widened/grew with it (76%x64% -> 84%x68% of the card)
            // specifically so the rocket itself reads bigger and clearer,
            // not just the card chrome around it.
            RectTransform card = BuildNineSlicePanel($"Card_{data.id}", parent, "ui_panel", CardTint, new Vector2(264, 165));
            card.gameObject.GetComponent<Image>().raycastTarget = true;

            CanvasGroup canvasGroup = card.gameObject.AddComponent<CanvasGroup>();
            DraggableRocket draggable = card.gameObject.AddComponent<DraggableRocket>();
            draggable.rocketId = data.id;

            RectTransform icon = CreateUIObject("Icon", card);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{RocketSpritesFolder}/{data.spriteName}.png");
            if (sprite == null)
                Debug.LogWarning($"[RocketTimelineSceneBuilder] Sprite not found for '{data.id}' at {RocketSpritesFolder}/{data.spriteName}.png");
            iconImage.sprite = sprite;
            Anchor(icon, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform nameLabel = CreateText("NameLabel", card, data.displayName, 17, TextColor, TextAnchor.MiddleCenter);
            Anchor(nameLabel, new Vector2(0.04f, 0), new Vector2(0.96f, 0.26f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);

            RectTransform infoButtonRect = CreateUIObject("InfoButton", card);
            Anchor(infoButtonRect, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(38, 38));
            Image infoImage = infoButtonRect.gameObject.AddComponent<Image>();
            infoImage.color = new Color(1f, 1f, 1f, 0.18f);
            Button infoButton = infoButtonRect.gameObject.AddComponent<Button>();
            infoButton.targetGraphic = infoImage;
            RectTransform infoLabel = CreateText("Label", infoButtonRect, "i", 20, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(infoLabel, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            infoButtonRect.gameObject.AddComponent<RocketInfoButton>().rocket = draggable;
            // gameManager reference is wired by the caller once GameManager exists.

            return draggable;
        }

        private static TriviaPopupUI BuildTriviaPopup(Transform canvasRoot)
        {
            RectTransform root = CreateUIObject("TriviaPopup", canvasRoot);
            Anchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform dim = CreateStretchPanel("DimBackground", root, new Color(0, 0, 0, 0.65f));
            dim.GetComponent<Image>().raycastTarget = true;

            // Card grown 920x580 -> 1000x760 in v12 to make room for the
            // deeper NASA-spec trivia text (see RocketData.json) without
            // squeezing the layout budget to near zero the way a text-box
            // height bump alone would have - wider too, so each line holds
            // more characters and fewer wrapped lines are needed overall.
            RectTransform card = BuildNineSlicePanel("Card", root, "ui_panel", PanelTint, new Vector2(1000, 760));
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760));
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(44, 44, 40, 40);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            // KeyAdvanceText/TriviaText grown again in v12 (100->160, 145->260)
            // alongside the card resize above - comfortable headroom for the
            // longest entries (~280 / ~320 characters) with margin to spare.
            Text nameText = AddParagraph(card, "NameText", 34, TextColor, FontStyle.Bold, 55, TextAnchor.UpperLeft);
            Text countryText = AddParagraph(card, "CountryText", 24, MutedTextColor, FontStyle.Normal, 38, TextAnchor.UpperLeft);
            Text keyAdvanceText = AddParagraph(card, "KeyAdvanceText", 24, MutedTextColor, FontStyle.Normal, 160, TextAnchor.UpperLeft);
            Text triviaText = AddParagraph(card, "TriviaText", 24, TextColor, FontStyle.Italic, 260, TextAnchor.UpperLeft);

            Button closeButton = BuildStyledButton(card, "CloseButton", "Close", ButtonBlue, 200, 72);
            LayoutElement closeLayout = closeButton.GetComponent<LayoutElement>();
            if (closeLayout != null) closeLayout.flexibleWidth = 0;

            TriviaPopupUI popup = root.gameObject.AddComponent<TriviaPopupUI>();
            popup.root = root.gameObject;
            popup.nameText = nameText;
            popup.countryText = countryText;
            popup.keyAdvanceText = keyAdvanceText;
            popup.triviaText = triviaText;
            popup.closeButton = closeButton;

            root.gameObject.SetActive(false);
            return popup;
        }

        /// <summary>
        /// Full-screen "Trivia Review" overlay: a dimmed backdrop and a large
        /// card styled like one slide of your research deck - a big year
        /// heading, the rocket's name, Key Advances / Trivia Fact / Countries,
        /// and an image of the rocket - with Prev/Next buttons to flip through
        /// all 18 rockets in timeline order. Shared between the game scene's
        /// post-Check-Answers "See Trivia" button and the Main Menu's "Browse
        /// Trivia" button - each scene just builds one of these and wires its
        /// own button to open it (via the returned TriviaReviewPagerUI's
        /// Show() method). The panel itself only has one copy of each text/
        /// image field; TriviaReviewPagerUI swaps their *content* at runtime
        /// as the player pages through, rather than this method building one
        /// row per rocket.
        /// </summary>
        private static TriviaReviewPagerUI BuildTriviaReviewPanel(RectTransform canvasRoot, RocketItem[] rocketData)
        {
            RectTransform root = CreateUIObject("TriviaReviewPanel", canvasRoot);
            Anchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform dim = CreateStretchPanel("DimBackground", root, new Color(0, 0, 0, 0.78f));
            dim.GetComponent<Image>().raycastTarget = true;

            RectTransform card = BuildNineSlicePanel("Card", root, "ui_panel", PanelTint, new Vector2(1600, 900));
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 900));

            RectTransform headerTitle = CreateText("HeaderTitle", card, "Rocket Trivia Timeline", 34, TextColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            Anchor(headerTitle, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(-260, 64));

            Button closeButton = BuildStyledButton(card, "CloseButton", "Close", ButtonSlate, 150, 68);
            Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -30), new Vector2(150, 68));

            // ---- Content area: text on the left, rocket image on the right ----------------
            RectTransform contentArea = CreateUIObject("ContentArea", card);
            Anchor(contentArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(-48, -220));

            RectTransform leftColumn = CreateUIObject("LeftColumn", contentArea);
            Anchor(leftColumn, new Vector2(0, 0), new Vector2(0.54f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, 0));

            // A thin accent bar along the left edge, echoing the research
            // deck's own "bullet + vertical line before the heading" look.
            // v15.2: gold -> neon blue (client request), and top-anchored at
            // a fixed height (670 -> 540) instead of stretched to fill
            // LeftColumn's full height, so its bottom sits a clear 80 units
            // above NavBar's top (-220 to -300 in card-local space) rather
            // than a close-but-technically-not-quite-overlapping 60.
            RectTransform accentBar = CreateStretchPanel("AccentBar", leftColumn, ButtonBlue);
            Anchor(accentBar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, 0), new Vector2(6, 540));
            accentBar.GetComponent<Image>().raycastTarget = false;

            RectTransform textStack = CreateUIObject("TextStack", leftColumn);
            Anchor(textStack, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(14, 0), new Vector2(-34, 0));
            VerticalLayoutGroup textLayout = textStack.gameObject.AddComponent<VerticalLayoutGroup>();
            textLayout.spacing = 10;
            textLayout.childAlignment = TextAnchor.UpperLeft;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;

            Text yearText = AddParagraph(textStack, "YearText", 52, TextColor, FontStyle.Bold, 70, TextAnchor.UpperLeft);
            Text nameText = AddParagraph(textStack, "NameText", 28, MutedTextColor, FontStyle.Bold, 42, TextAnchor.UpperLeft);

            // v16: Key Advances / Trivia Fact / Countries now live inside
            // their own scrolling area instead of directly in textStack.
            // Client feedback was that these fonts should match the Topic
            // panels' 28/24 heading/body sizing - but this column's hand-
            // verified layout budget above NavBar is a hard 620 units total
            // (70+42+spacing for the header above, leaving 488 for
            // everything below), and the client separately asked for
            // MEANGINGFULLY MORE technical trivia content to be added per
            // rocket. Bumping fonts AND lengthening content inside a fixed-
            // height block would have overflowed that budget - exactly the
            // "overlapping the Prev button" bug already fixed once for the
            // AccentBar. A vertically-scrolling area (the same BuildScrollArea
            // helper the pool/timeline already use) removes the ceiling
            // entirely instead: the visible slot stays a verified-safe 480
            // units tall (132 header + 480 = 612, an 8-unit margin under the
            // 620 budget - computed by hand the same way as every other
            // layout change in this project), and any amount of text simply
            // scrolls within it, so a future rocket with even more trivia
            // can never reopen this bug.
            RectTransform detailArea = CreateUIObject("DetailArea", textStack);
            LayoutElement detailLayoutElement = detailArea.gameObject.AddComponent<LayoutElement>();
            detailLayoutElement.preferredHeight = 480;
            detailLayoutElement.preferredWidth = 1;
            detailLayoutElement.flexibleWidth = 1;

            BuildScrollArea("DetailScroll", detailArea, horizontal: false, vertical: true, out RectTransform detailContent, out ScrollRect detailScrollRect);
            VerticalLayoutGroup detailLayout = detailContent.gameObject.AddComponent<VerticalLayoutGroup>();
            // v16: tight base spacing (label sits close to its own value) -
            // the BIGGER gap that separates Key Advances / Trivia Fact /
            // Countries as distinct subtopics is now an explicit
            // AddVerticalSpacer between groups (see below), not this
            // uniform value, so the "within a subtopic" and "between
            // subtopics" gaps can finally differ from each other.
            detailLayout.spacing = 6;
            detailLayout.padding = new RectOffset(0, 16, 0, 12); // right clears the scrollbar; bottom gives the last line room before it clips
            detailLayout.childAlignment = TextAnchor.UpperLeft;
            detailLayout.childForceExpandWidth = true;
            detailLayout.childForceExpandHeight = false;
            detailLayout.childControlWidth = true;
            detailLayout.childControlHeight = true;
            ContentSizeFitter detailFitter = detailContent.gameObject.AddComponent<ContentSizeFitter>();
            detailFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BuildScrollbar(detailArea, detailScrollRect, horizontal: false);

            // Fonts matched to the Topic panels' heading/body sizing
            // (28/24) per client request. v16: the three VALUE texts below
            // now pass -1 for preferredHeight instead of a fixed number -
            // see AddParagraph's updated doc comment - so a short trivia
            // fact no longer leaves a large dead gap before the next label,
            // and a long one no longer crowds the label after it. A 26-unit
            // AddVerticalSpacer between each label/value PAIR gives the
            // three subtopics a clearly bigger break than the 6-unit gap
            // within each pair, so they still read as distinct sections
            // regardless of how much text any one of them has.
            AddParagraph(detailContent, "KeyAdvanceLabel", 28, TextColor, FontStyle.Bold, 36, TextAnchor.UpperLeft).text = "Key Advances:";
            Text keyAdvanceValue = AddParagraph(detailContent, "KeyAdvanceValue", 24, MutedTextColor, FontStyle.Normal, -1, TextAnchor.UpperLeft);

            AddVerticalSpacer(detailContent, 26);
            AddParagraph(detailContent, "TriviaLabel", 28, TextColor, FontStyle.Bold, 36, TextAnchor.UpperLeft).text = "Trivia Fact:";
            Text triviaValue = AddParagraph(detailContent, "TriviaValue", 24, MutedTextColor, FontStyle.Normal, -1, TextAnchor.UpperLeft);

            AddVerticalSpacer(detailContent, 26);
            AddParagraph(detailContent, "CountriesLabel", 28, TextColor, FontStyle.Bold, 36, TextAnchor.UpperLeft).text = "Countries:";
            Text countryValue = AddParagraph(detailContent, "CountriesValue", 24, MutedTextColor, FontStyle.Normal, -1, TextAnchor.UpperLeft);

            // ---- Right column: a framed image of the current rocket -----------------------
            RectTransform rightColumn = CreateUIObject("RightColumn", contentArea);
            Anchor(rightColumn, new Vector2(0.54f, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, 0));

            RectTransform imageFrame = BuildNineSlicePanel("ImageFrame", rightColumn, "ui_panel", CardTint, new Vector2(10, 10));
            Anchor(imageFrame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform rocketImageRect = CreateUIObject("RocketImage", imageFrame);
            Anchor(rocketImageRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-60, -60));
            Image rocketImage = rocketImageRect.gameObject.AddComponent<Image>();
            rocketImage.preserveAspect = true;
            rocketImage.raycastTarget = false;

            // ---- Bottom nav bar: Prev / page indicator / Next ------------------------------
            RectTransform navBar = CreateUIObject("NavBar", card);
            Anchor(navBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 54), new Vector2(-48, 96));

            Button prevButton = BuildStyledButton(navBar, "PrevButton", "< Prev", ButtonSlate, 200, 76);
            Anchor(prevButton.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(200, 76));

            Button nextButton = BuildStyledButton(navBar, "NextButton", "Next >", ButtonBlue, 200, 76);
            Anchor(nextButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(200, 76));

            // Placeholder only - TriviaReviewPagerUI.Refresh() overwrites this
            // with the real "{page} / {rockets.Length}" the moment the panel
            // is shown, so the literal count here never actually matters.
            Text pageIndicator = CreateText("PageIndicator", navBar, "1 / 18", 22, MutedTextColor, TextAnchor.MiddleCenter).GetComponent<Text>();
            Anchor(pageIndicator.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 50));

            // ---- Wire the pager, in the same chronological order as the timeline itself ----
            var rocketsById = rocketData.ToDictionary(r => r.id, r => r);
            var orderedRockets = new List<RocketItem>();
            var orderedSprites = new List<Sprite>();
            foreach (string id in TriviaRocketIdsInOrder)
            {
                if (!rocketsById.TryGetValue(id, out RocketItem data)) continue;
                orderedRockets.Add(data);
                orderedSprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>($"{RocketSpritesFolder}/{data.spriteName}.png"));
            }

            TriviaReviewPagerUI pager = root.gameObject.AddComponent<TriviaReviewPagerUI>();
            pager.root = root.gameObject;
            pager.yearText = yearText;
            pager.nameText = nameText;
            pager.keyAdvanceValueText = keyAdvanceValue;
            pager.triviaValueText = triviaValue;
            pager.countryValueText = countryValue;
            pager.rocketImage = rocketImage;
            pager.pageIndicatorText = pageIndicator;
            pager.detailScrollRect = detailScrollRect;
            pager.rockets = orderedRockets.ToArray();
            pager.rocketSprites = orderedSprites.ToArray();

            UnityEventTools.AddVoidPersistentListener(closeButton.onClick, pager.Close);
            UnityEventTools.AddVoidPersistentListener(prevButton.onClick, pager.PrevPage);
            UnityEventTools.AddVoidPersistentListener(nextButton.onClick, pager.NextPage);

            root.gameObject.SetActive(false);
            return pager;
        }

        /// <summary>
        /// Full-screen "About..." overlay for a v13 bonus-content topic
        /// (Satellites, Rovers &amp; Robotic Explorers, International Space
        /// Station) - a dimmed backdrop, one fixed topic icon/title/subtitle,
        /// and a fact card (heading + body) that Prev/Next pages through.
        /// Deliberately simpler than BuildTriviaReviewPanel's two-column
        /// layout: there's one image for the whole topic rather than a
        /// different one per page, so the whole card width is free for text,
        /// which comfortably fits the longest fact bodies (~400 characters)
        /// with a wide margin to spare. One of these is built per topic in
        /// TopicData.json; each gets its own Main Menu button.
        /// </summary>
        private static TopicFactsPagerUI BuildTopicFactsPanel(RectTransform canvasRoot, TopicItem topic)
        {
            RectTransform root = CreateUIObject($"TopicPanel_{topic.id}", canvasRoot);
            Anchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform dim = CreateStretchPanel("DimBackground", root, new Color(0, 0, 0, 0.78f));
            dim.GetComponent<Image>().raycastTarget = true;

            RectTransform card = BuildNineSlicePanel("Card", root, "ui_panel", PanelTint, new Vector2(1200, 820));
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 820));

            // ---- Header: topic icon on the left, title/subtitle to its right, Close top-right ----
            RectTransform iconFrame = BuildNineSlicePanel("IconFrame", card, "ui_panel", CardTint, new Vector2(120, 120));
            Anchor(iconFrame, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(120, 120));
            RectTransform iconRect = CreateUIObject("Icon", iconFrame);
            Anchor(iconRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18, -18));
            Image topicIconImage = iconRect.gameObject.AddComponent<Image>();
            topicIconImage.preserveAspect = true;
            topicIconImage.raycastTarget = false;
            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{TopicSpritesFolder}/{topic.iconName}.png");
            if (iconSprite != null) topicIconImage.sprite = iconSprite;
            else Debug.LogWarning($"[RocketTimelineSceneBuilder] Topic icon not found for '{topic.id}' at {TopicSpritesFolder}/{topic.iconName}.png");

            // v15.2: center-aligned within the band between the icon and the
            // Close button (166 to 1000 of the 1200-wide card, as fractions
            // of card width below) instead of left-aligned flush against the
            // icon - client feedback was that it read as "offset to the
            // left" rather than centered on the card. Font sizes also bumped
            // (32->34, 20->22) to match the richer body-text sizing below.
            RectTransform headerTitle = CreateText("HeaderTitle", card, topic.displayName, 34, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(headerTitle, new Vector2(166f / 1200f, 1), new Vector2(1000f / 1200f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(0, 48));

            RectTransform headerSubtitle = CreateText("HeaderSubtitle", card, topic.subtitle, 22, MutedTextColor, TextAnchor.MiddleCenter);
            Anchor(headerSubtitle, new Vector2(166f / 1200f, 1), new Vector2(1000f / 1200f, 1), new Vector2(0.5f, 1), new Vector2(0, -86), new Vector2(0, 34));

            Button closeButton = BuildStyledButton(card, "CloseButton", "Close", ButtonSlate, 150, 68);
            Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -30), new Vector2(150, 68));

            // ---- Content area: TWO facts per page, stacked vertically ---------------------
            // v16, corrected: client asked to merge two sub-topics into one
            // slide (per the hand-drawn ISS reference and a Photoshopped
            // mockup of an older build) - this halves the page count for
            // Satellites/Rovers/ISS. First attempt at this put the two
            // facts in SIDE-BY-SIDE columns; the client's mockup showed them
            // stacked full-width instead (heading+body, then a clear gap,
            // then the second heading+body) - that's what this builds now.
            // Both fact blocks live in ONE scrolling area (same technique as
            // BuildTriviaReviewPanel's DetailArea) so two long facts
            // together can never overflow into NavBar, and each block's
            // heading/body heights are fully dynamic (see AddParagraph's
            // updated doc comment) so a short fact doesn't leave a dead gap
            // before the next one and a long fact doesn't get cramped.
            RectTransform contentArea = CreateUIObject("ContentArea", card);
            Anchor(contentArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(-96, -340));

            // Hand-verified safe height above NavBar for this card
            // (card=1200x820, same math as every prior pass of this panel):
            // contentArea top to NavBar's top is ~466 units; 460 leaves a
            // small margin, same technique as BuildTriviaReviewPanel's
            // DetailArea.
            RectTransform scrollHost = CreateUIObject("ScrollHost", contentArea);
            Anchor(scrollHost, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 460));

            BuildScrollArea("FactsScroll", scrollHost, horizontal: false, vertical: true, out RectTransform factsContent, out ScrollRect factsScrollRect);
            VerticalLayoutGroup factsLayout = factsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            factsLayout.spacing = 10;
            factsLayout.padding = new RectOffset(0, 16, 0, 12); // right clears the scrollbar; bottom gives the last line room before it clips
            factsLayout.childAlignment = TextAnchor.UpperLeft;
            factsLayout.childForceExpandWidth = true;
            factsLayout.childForceExpandHeight = false;
            factsLayout.childControlWidth = true;
            factsLayout.childControlHeight = true;
            ContentSizeFitter factsFitter = factsContent.gameObject.AddComponent<ContentSizeFitter>();
            factsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            BuildScrollbar(scrollHost, factsScrollRect, horizontal: false);

            var blockA = BuildTopicFactBlock(factsContent);
            // A clearly bigger gap than the 10-unit spacing above/below
            // every other element, so the two stacked facts read as
            // distinct subtopics rather than one continuous ramble - hidden
            // together with blockB on a trailing odd-numbered page.
            RectTransform blockSpacer = AddVerticalSpacer(factsContent, 34);
            var blockB = BuildTopicFactBlock(factsContent);

            // ---- Bottom nav bar: Prev / page indicator / Next --------------------------
            RectTransform navBar = CreateUIObject("NavBar", card);
            Anchor(navBar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 54), new Vector2(-48, 96));

            Button prevButton = BuildStyledButton(navBar, "PrevButton", "< Prev", ButtonSlate, 200, 76);
            Anchor(prevButton.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(200, 76));

            Button nextButton = BuildStyledButton(navBar, "NextButton", "Next >", ButtonBlue, 200, 76);
            Anchor(nextButton.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(200, 76));

            // Placeholder only - TopicFactsPagerUI.Render() overwrites this
            // with the real "{page} / {pageCount}" (pageCount = half the
            // fact count, rounded up) the moment the panel is shown.
            Text pageIndicator = CreateText("PageIndicator", navBar, $"1 / {Mathf.CeilToInt(topic.facts.Length / 2f)}", 22, MutedTextColor, TextAnchor.MiddleCenter).GetComponent<Text>();
            Anchor(pageIndicator.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 50));

            TopicFactsPagerUI pager = root.gameObject.AddComponent<TopicFactsPagerUI>();
            pager.root = root.gameObject;
            pager.titleText = headerTitle.GetComponent<Text>();
            pager.subtitleText = headerSubtitle.GetComponent<Text>();
            pager.topicIcon = topicIconImage;
            pager.headingTextA = blockA.heading;
            pager.bodyTextA = blockA.body;
            pager.blockBRoot = blockB.root.gameObject;
            pager.blockBSpacerRoot = blockSpacer.gameObject;
            pager.headingTextB = blockB.heading;
            pager.bodyTextB = blockB.body;
            pager.contentScrollRect = factsScrollRect;
            pager.pageIndicatorText = pageIndicator;
            pager.facts = topic.facts;

            UnityEventTools.AddVoidPersistentListener(closeButton.onClick, pager.Close);
            UnityEventTools.AddVoidPersistentListener(prevButton.onClick, pager.PrevPage);
            UnityEventTools.AddVoidPersistentListener(nextButton.onClick, pager.NextPage);

            root.gameObject.SetActive(false);
            return pager;
        }

        /// <summary>
        /// One "fact block" within BuildTopicFactsPanel's two-facts-per-page
        /// layout (v16, corrected) - an accent bar running the full height
        /// of a heading + body pair, the two stacked vertically on the page
        /// (NOT side by side - see BuildTopicFactsPanel's comment on why
        /// that first attempt was wrong). The accent bar is the left child
        /// of a HorizontalLayoutGroup with childForceExpandHeight=true, so
        /// it always stretches to match the text column's own natural
        /// height, whatever that ends up being for this fact's heading and
        /// body at the current font sizes - both pass -1 to AddParagraph so
        /// their height tracks their real content instead of a fixed
        /// estimate (see AddParagraph's doc comment).
        /// </summary>
        private static (RectTransform root, Text heading, Text body) BuildTopicFactBlock(Transform parent)
        {
            RectTransform row = CreateUIObject("FactBlock", parent);
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 22;
            rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true; // stretch the accent bar to the text column's own dynamic height
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;

            RectTransform accentBar = CreateUIObject("AccentBar", row);
            Image accentImage = accentBar.gameObject.AddComponent<Image>();
            accentImage.color = ButtonBlue;
            accentImage.raycastTarget = false;
            LayoutElement accentLayoutElement = accentBar.gameObject.AddComponent<LayoutElement>();
            accentLayoutElement.preferredWidth = 6;
            accentLayoutElement.minWidth = 6;
            accentLayoutElement.flexibleWidth = 0;

            RectTransform textColumn = CreateUIObject("TextColumn", row);
            LayoutElement textColumnLayoutElement = textColumn.gameObject.AddComponent<LayoutElement>();
            textColumnLayoutElement.flexibleWidth = 1;
            VerticalLayoutGroup textColumnLayout = textColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            textColumnLayout.spacing = 8;
            textColumnLayout.childAlignment = TextAnchor.UpperLeft;
            textColumnLayout.childForceExpandWidth = true;
            textColumnLayout.childForceExpandHeight = false;
            textColumnLayout.childControlWidth = true;
            textColumnLayout.childControlHeight = true;

            Text headingText = AddParagraph(textColumn, "FactHeading", 28, TextColor, FontStyle.Bold, -1, TextAnchor.UpperLeft);
            Text bodyText = AddParagraph(textColumn, "FactBody", 24, MutedTextColor, FontStyle.Normal, -1, TextAnchor.UpperLeft);

            return (row, headingText, bodyText);
        }

        // ================================================================================
        // MAIN MENU SCENE
        // ================================================================================

        private static void BuildMainMenuSceneInternal(RocketItem[] rocketData, TopicItem[] topicData)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();
            CreateCamera();
            RectTransform canvasRoot = CreateCanvas();
            CreateBackground(canvasRoot);

            // v15: client feedback on v14 was that the title/subtitle/Play
            // button sat too low, leaving a big dead zone above the title
            // (292 units/27% of the 1080-unit canvas) versus a much tighter
            // gap at the bottom. Moved this whole hero block (title+Play+
            // caption) up by a flat 120 units - ActionRow stayed exactly
            // where it was (not part of this request) - see the
            // matching +120 on playButton/playCaption below and the updated
            // gap math in their comments.
            RectTransform titleBlock = CreateUIObject("TitleBlock", canvasRoot);
            Anchor(titleBlock, new Vector2(0.5f, 0.71f), new Vector2(0.5f, 0.71f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 280));

            RectTransform title = CreateText("Title", titleBlock, "Rocket Evolution\nTimeline", 72, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title, new Vector2(0, 0.4f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform subtitle = CreateText("Subtitle", titleBlock,
                "Learn rocket history by dragging each rocket to its place on the timeline!",
                26, MutedTextColor, TextAnchor.MiddleCenter);
            Anchor(subtitle, new Vector2(0, 0), new Vector2(1, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject sceneLoaderGo = new GameObject("SceneLoader", typeof(SceneLoader));
            SceneLoader sceneLoader = sceneLoaderGo.GetComponent<SceneLoader>();

            // ---- Play button + caption + a single unified secondary-action row, stacked bottom-up ----
            // v14: client feedback on the v13 layout was that the About row
            // sat at an inconsistent, oddly-low position with mismatched
            // button sizes, crowding the caption above it. Rebuilt from
            // scratch bottom-up with even, deliberate gaps between every
            // element (see the comment on each Anchor call below for the
            // gap it leaves), and Rocket Trivia + the About buttons are now
            // one identically-sized-button grid (ActionRow) instead of two
            // differently-sized rows - the single biggest thing that makes
            // this read as "symmetric and clean" rather than assembled
            // piecemeal. v15.1: laid out 2x2 (per client request) rather
            // than one row of four.
            Button playButton = BuildStyledButton(canvasRoot, "PlayButton", "Play", ButtonCosmicPurple, 360, 120, solid: true);
            RectTransform playRect = playButton.GetComponent<RectTransform>();
            // v15: +120 to match TitleBlock's move above. Gap from
            // TitleBlock's bottom (626.8) to here (404): 102.8, basically
            // unchanged from v14's 104 since both moved together.
            Anchor(playRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 404), new Vector2(360, 120));
            // Button text a little larger for the primary call-to-action.
            Text playLabel = playButton.GetComponentInChildren<Text>();
            if (playLabel != null) playLabel.fontSize = 36;
            UnityEventTools.AddVoidPersistentListener(playButton.onClick, sceneLoader.LoadGame);

            // v15: +120 to match TitleBlock/Play above - Play's bottom (404)
            // to here (366): 38, same gap as v14.
            // v15.1: ActionRow is now 2 rows tall (its top moved 142 -> 234,
            // see below), so the gap below the caption shrank from 160 back
            // down to 68 - still open breathing room, not packed.
            RectTransform playCaption = CreateText("PlayCaption", canvasRoot,
                "Play the drag & drop timeline challenge and see how much you\nremember of the evolutionary timeline of rockets.",
                22, MutedTextColor, TextAnchor.MiddleCenter);
            Anchor(playCaption, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 302), new Vector2(1000, 64));

            // ---- ActionRow: Rocket Trivia + one "About..." button per TopicData.json
            // entry, all built at one fixed size (ActionButtonSize) so every button -
            // however long its label - lines up identically.
            // v15.1: laid out as a 2-column grid (client asked for 2x2) instead of a
            // single row of four, and the footer text below it was removed entirely
            // (client request) - ActionRow's bottom anchor (66) is unchanged, so with
            // the footer gone that's now simply the bottom margin to the canvas edge.
            // Still data-driven: a 5th topic added later wraps to a 3rd row (2 cols x
            // Ceil(total/2) rows) rather than needing a code change here.
            int actionButtonCount = topicData.Length + 1;
            int actionColumns = 2;
            int actionRows = Mathf.CeilToInt(actionButtonCount / (float)actionColumns);
            float actionRowWidth = actionColumns * ActionButtonSize.x + (actionColumns - 1) * 16f;
            float actionRowHeight = actionRows * ActionButtonSize.y + (actionRows - 1) * 16f;
            RectTransform actionRow = CreateUIObject("ActionRow", canvasRoot);
            Anchor(actionRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 66), new Vector2(actionRowWidth, actionRowHeight));
            GridLayoutGroup actionLayout = actionRow.gameObject.AddComponent<GridLayoutGroup>();
            actionLayout.cellSize = ActionButtonSize;
            actionLayout.spacing = new Vector2(16, 16);
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            actionLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            actionLayout.constraintCount = actionColumns;

            // v15: "Browse Trivia" -> "Rocket Trivia", and rest color pulled
            // back to ButtonSlate so this button looks identical to the
            // three About buttons at rest - the whole row now only
            // differentiates itself on hover/press (see hoverColor below),
            // not by one button permanently wearing a different color than
            // its neighbors.
            Button rocketTriviaButton = BuildStyledButton(actionRow, "RocketTriviaButton", "Rocket Trivia", ButtonSlate, ActionButtonSize.x, ActionButtonSize.y, hoverColor: ButtonBlue);
            Text rocketTriviaLabel = rocketTriviaButton.GetComponentInChildren<Text>();
            if (rocketTriviaLabel != null) rocketTriviaLabel.fontSize = 19;

            // ---- Trivia review screen, reachable straight from the Main Menu too ----------
            TriviaReviewPagerUI triviaReviewPager = BuildTriviaReviewPanel(canvasRoot, rocketData);
            UnityEventTools.AddVoidPersistentListener(rocketTriviaButton.onClick, triviaReviewPager.Show);

            // ---- One bonus-topic panel + one ActionRow button per TopicData.json entry ----
            foreach (TopicItem topic in topicData)
            {
                // Button label is looked up by id (rather than just using
                // topic.displayName, which for "rovers" is the longer
                // "Rovers & Robotic Explorers" panel title) so the Main Menu
                // buttons read exactly as the client asked for: "About
                // Satellites" / "About Rovers" / "About International Space
                // Station". A topic id added later without an entry here
                // just falls back to "About <displayName>" wrapped to fit
                // the same fixed ActionButtonSize as every other button in
                // the row, so nothing ever breaks the row's uniform sizing.
                string label = BonusButtonLabels.TryGetValue(topic.id, out string mapped) ? mapped : $"About {topic.displayName}";

                // v15: same ButtonBlue hover-accent as Rocket Trivia above, so
                // every button in the row pops the same neon-cyan glow on
                // hover/press and only differs while the pointer is on it.
                Button topicButton = BuildStyledButton(actionRow, $"About_{topic.id}", label, ButtonSlate, ActionButtonSize.x, ActionButtonSize.y, hoverColor: ButtonBlue);
                Text topicLabel = topicButton.GetComponentInChildren<Text>();
                if (topicLabel != null) topicLabel.fontSize = 19;

                TopicFactsPagerUI topicPager = BuildTopicFactsPanel(canvasRoot, topic);
                UnityEventTools.AddVoidPersistentListener(topicButton.onClick, topicPager.Show);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log($"[RocketTimelineSceneBuilder] Main Menu scene saved to {MainMenuScenePath}.");
        }

        // ================================================================================
        // Shared scene setup
        // ================================================================================

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            Camera cam = cameraGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            cam.orthographic = true;
            // A camera just needs to exist for the Game view to render normally -
            // the UI itself draws via the Screen Space Overlay canvas regardless.
        }

        private static RectTransform CreateCanvas()
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // Match HEIGHT (not a 0.5 width/height blend): this guarantees the
            // vertical layout always gets the full 1080 units of canvas space
            // it was designed for, no matter how wide or short the actual
            // screen/window is. With the previous 0.5 blend, a wide-but-short
            // window (like a typical Unity Editor Game view, or some phones in
            // landscape) computed a *shorter* effective canvas height than the
            // 1080 reference, which silently ate into the vertical gaps below
            // and caused the timeline/pool/result-panel overlap reported after
            // testing. Matching height removes that dependency entirely; extra
            // width on very wide/narrow screens just adds side margin.
            scaler.matchWidthOrHeight = 1f;
            return canvasGo.GetComponent<RectTransform>();
        }

        private static void CreateBackground(RectTransform canvasRoot)
        {
            RectTransform background = CreateStretchPanel("Background", canvasRoot, Color.white);
            Image bgImage = background.GetComponent<Image>();
            bgImage.raycastTarget = false;
            Sprite bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{BackgroundsFolder}/space_background.png");
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.type = Image.Type.Simple;
                bgImage.preserveAspect = false;
            }
            else
            {
                bgImage.color = new Color(0.04f, 0.05f, 0.09f);
            }
        }

        // ================================================================================
        // Data loading
        // ================================================================================

        private static RocketItem[] LoadRocketDataForEditor()
        {
            const string jsonPath = "Assets/_Project/Resources/RocketData.json";
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null)
            {
                EditorUtility.DisplayDialog("Rocket Timeline",
                    $"Could not find {jsonPath}.\nMake sure the whole _Project folder was imported before running this.",
                    "OK");
                return null;
            }
            RocketItemList list = JsonUtility.FromJson<RocketItemList>(json.text);
            if (list?.rockets == null || list.rockets.Length == 0)
            {
                EditorUtility.DisplayDialog("Rocket Timeline", $"{jsonPath} has no rockets in it.", "OK");
                return null;
            }
            return list.rockets;
        }

        /// <summary>Loads the v13 bonus-content topics (Satellites, Rovers &amp;
        /// Robotic Explorers, International Space Station) the same way
        /// LoadRocketDataForEditor above loads the rocket roster. Returns an
        /// empty array (not null) if the file is missing, so a project built
        /// before v13 still builds successfully with the three new menu
        /// buttons simply left unwired rather than failing the whole build.</summary>
        private static TopicItem[] LoadTopicDataForEditor()
        {
            const string jsonPath = "Assets/_Project/Resources/TopicData.json";
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            if (json == null)
            {
                Debug.LogWarning($"[RocketTimelineSceneBuilder] Could not find {jsonPath} - skipping the Satellites/Rovers/ISS bonus topic buttons.");
                return new TopicItem[0];
            }
            TopicItemList list = JsonUtility.FromJson<TopicItemList>(json.text);
            return list?.topics ?? new TopicItem[0];
        }

        // ================================================================================
        // Small UI-building helpers
        // ================================================================================

        private static RectTransform CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static RectTransform CreateStretchPanel(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateUIObject(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        /// <summary>A panel using the shared rounded 9-slice sprite, tinted to the given color.</summary>
        private static RectTransform BuildNineSlicePanel(string name, Transform parent, string spriteName, Color tint, Vector2 size)
        {
            RectTransform rect = BuildNineSliceImage(name, parent, spriteName, tint);
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform BuildNineSliceImage(string name, Transform parent, string spriteName, Color tint)
        {
            RectTransform rect = CreateUIObject(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UISpritesFolder}/{spriteName}.png");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
            image.color = tint;
            return rect;
        }

        /// <summary>
        /// v14: every button is now one of two styles, matching the client's
        /// reference mission-control dashboard rather than v13's "every
        /// button a loud filled block" look:
        ///   - solid=true -> ui_button.png, a bright filled pill. Reserved
        ///     for the single primary call-to-action on a screen (just Play).
        ///   - solid=false (default) -> ui_button_ghost.png, a near-black
        ///     glass fill with a crisp colored border - used for every other
        ///     button in the app. The label text is colored to match the
        ///     border here (not white), the standard "outline button" look,
        ///     and it's what makes a ButtonRed Retry still clearly read as
        ///     red, a ButtonGold See Trivia still read as gold, etc. without
        ///     the visual weight of a fully filled button.
        /// Both styles share the same hover/press scale punch and the same
        /// (now slightly less extreme, see colors below) ColorBlock tint
        /// reaction.
        ///
        /// v15: an optional <paramref name="hoverColor"/> opts a ghost
        /// button into ButtonHoverPunch's color-accent behavior - it sits at
        /// <paramref name="color"/> normally and eases to
        /// <paramref name="hoverColor"/> on hover/press instead of just
        /// scaling. Left null (the default) every existing call keeps its
        /// single fixed color exactly as before.
        /// </summary>
        private static Button BuildStyledButton(Transform parent, string name, string label, Color color, float width, float height, bool solid = false, Color? hoverColor = null)
        {
            string spriteName = solid ? "ui_button" : "ui_button_ghost";
            RectTransform rect = BuildNineSlicePanel(name, parent, spriteName, color, new Vector2(width, height));
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            bool usesAccent = !solid && hoverColor.HasValue;
            if (usesAccent)
            {
                // v16: set BEFORE `button.colors = colors;` below, not
                // after. A freshly-added Button still has Unity's compiler-
                // default `transition = ColorTint` at this point, and
                // assigning `colors` while that's still the active
                // transition fires an editor-time live-preview
                // DoStateTransition that instantly CrossFades this button's
                // CanvasRenderer to colors.normalColor - a tint that then
                // never gets reconciled again once transition is None later
                // (None's DoStateTransition is a no-op). Harmless on its
                // own (colors.normalColor is opaque white, the multiply
                // identity), but setting None first means it can never
                // happen at all. See ButtonHoverPunch's class comment for
                // the full history here.
                button.transition = Selectable.Transition.None;
            }

            // v14: pulled back a little from v13's 1.25/0.78 - a ghost
            // button's border is already the brightest thing on it, so an
            // overly strong highlight multiply could blow it out to white
            // and lose the color entirely. Still a clearly visible reaction.
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;

            // v13: small hover/press scale punch - see ButtonHoverPunch's own
            // doc comment for why this was added on top of the color tint.
            ButtonHoverPunch punch = rect.gameObject.AddComponent<ButtonHoverPunch>();

            // v14: a small horizontal inset (was 0) so a wrapped 2-line label
            // (the longest, "About International Space Station," wraps at
            // this width) doesn't sit right up against the rounded border.
            Color labelColor = solid ? TextColor : color;
            RectTransform textRect = CreateText("Label", rect, label, 26, labelColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-24, 0));

            // v15: wire the optional hover color-accent - see this method's
            // doc comment and ButtonHoverPunch.SetHoverAccent. (transition
            // was already switched to None above, before `colors` was set.)
            if (usesAccent)
            {
                Text labelText = textRect.GetComponent<Text>();
                punch.SetHoverAccent(image, labelText, color, hoverColor.Value);
            }

            return button;
        }

        /// <summary>
        /// v16 fix: <paramref name="preferredHeight"/> used to be applied
        /// UNCONDITIONALLY as a fixed `LayoutElement.preferredHeight` -
        /// meaning every paragraph built with this method reserved exactly
        /// that much vertical layout space regardless of how much text it
        /// actually held, no matter how dynamic the surrounding comments
        /// claimed it to be. That's the real cause of the "huge gap after a
        /// short trivia fact, cramped after a long one" inconsistency the
        /// client reported - a short fact still reserved the same box as the
        /// longest one ever seen. Passing a non-positive value now (see the
        /// call sites below) leaves the LayoutElement's height at Unity's
        /// own "unset" sentinel (-1), so the layout system falls through to
        /// the Text component's OWN `ILayoutElement.preferredHeight` -
        /// computed fresh from its actual wrapped line count at whatever
        /// width the layout group just gave it - and the box genuinely
        /// shrinks and grows with the real content, short or long.
        /// Positive values are still honored for the handful of callers
        /// that want a firm minimum (e.g. a label that's always one line).
        /// </summary>
        private static Text AddParagraph(Transform parent, string name, int fontSize, Color color, FontStyle style, float preferredHeight, TextAnchor alignment)
        {
            RectTransform rect = CreateText(name, parent, "", fontSize, color, alignment, style);
            Text text = rect.GetComponent<Text>();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight > 0) layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;
            return text;
        }

        /// <summary>An invisible fixed-height gap inserted into a
        /// VerticalLayoutGroup to create a BIGGER break between two groups
        /// of content than the layout group's own uniform `spacing` gives
        /// every pair of children - e.g. separating "Key Advances" from
        /// "Trivia Fact" by more than a label sits from its own value, so
        /// the three Rocket Trivia subtopics (and, per-page, a Topic panel's
        /// two stacked facts) read as clearly distinct sections no matter
        /// how long or short the text directly above/below the gap is.
        /// Returns the spacer's RectTransform so a caller that needs to hide
        /// it alongside an optional following block (see
        /// BuildTopicFactsPanel) can SetActive it.</summary>
        private static RectTransform AddVerticalSpacer(Transform parent, float height)
        {
            RectTransform rect = CreateUIObject("Spacer", parent);
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            layoutElement.flexibleHeight = 0;
            return rect;
        }

        /// <summary>
        /// A standard Unity scroll view: a clipped viewport (RectMask2D) over
        /// a content RectTransform, wired to a ScrollRect that fills
        /// whatever parent rect it's given. v11 introduced this so the
        /// timeline and pool can keep growing (8 -> 16 rockets, and whatever
        /// comes after) without a layout rewrite each time - both scroll
        /// instead of trying to cram every card into a fixed-size grid. The
        /// caller still needs to add its own layout group (Horizontal/Grid)
        /// plus a ContentSizeFitter to the returned content, since the right
        /// layout differs between the timeline row and the pool grid.
        /// </summary>
        private static void BuildScrollArea(string name, Transform parent, bool horizontal, bool vertical,
            out RectTransform content, out ScrollRect scrollRect)
        {
            RectTransform root = CreateUIObject(name, parent);
            Anchor(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            scrollRect = root.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateUIObject("Viewport", root);
            Anchor(viewport, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateUIObject("Content", viewport);
            if (horizontal && !vertical)
            {
                // Left-pivoted, stretched to the viewport's full height -
                // width grows via ContentSizeFitter on whatever layout group
                // the caller adds.
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
            }
            else
            {
                // Top-pivoted, stretched to the viewport's full width -
                // height grows via ContentSizeFitter on whatever layout
                // group the caller adds.
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
            }
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
        }

        /// <summary>
        /// A thin HUD-styled scrollbar overlaid on the edge of a scroll area,
        /// so a scrollable row/grid doesn't just silently cut off with no
        /// visual hint there's more content - important now that no single
        /// screen shows every rocket card at once. Drawn as a thin overlay
        /// rather than reserving dedicated layout space, to keep this a
        /// simple, low-risk addition on top of the scroll area above.
        /// </summary>
        private static void BuildScrollbar(Transform parent, ScrollRect scrollRect, bool horizontal)
        {
            RectTransform bar = CreateUIObject(horizontal ? "HorizontalScrollbar" : "VerticalScrollbar", parent);
            if (horizontal)
                Anchor(bar, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(-16, 6));
            else
                Anchor(bar, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-3, 0), new Vector2(6, -16));

            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.10f);

            Scrollbar scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = horizontal ? Scrollbar.Direction.LeftToRight : Scrollbar.Direction.BottomToTop;

            RectTransform handleArea = CreateUIObject("SlidingArea", bar);
            Anchor(handleArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform handle = CreateUIObject("Handle", handleArea);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.30f, 0.80f, 0.92f, 0.85f);
            handle.sizeDelta = Vector2.zero;

            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;

            if (horizontal) scrollRect.horizontalScrollbar = scrollbar;
            else scrollRect.verticalScrollbar = scrollbar;
        }

        private static RectTransform CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            RectTransform rect = CreateUIObject(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = GetDefaultFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            return rect;
        }

        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private static void EnsureFolderExists(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
