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

        // Order here only affects the pool's starting sibling order in the
        // Hierarchy - GameManager shuffles the pool at Play time regardless.
        private static readonly (string id, string label)[] Zones =
        {
            ("13TH_CENTURY", "13th Century"),
            ("1926", "1926"),
            ("1942_1944", "1942 - 1944"),
            ("1957", "1957"),
            ("1967_1969", "1967 - 1969"),
            ("1981", "1981"),
            ("2015_2017", "2015 - 2017"),
            ("2023", "2023"),
        };

        private static readonly string[] RocketIdsInPoolOrder =
        {
            "chinese_fire_arrow", "goddard_nell", "v2_aggregat4", "r7_sputnik",
            "saturn_v", "space_shuttle", "falcon_9", "starship",
        };

        // ---- Palette --------------------------------------------------------------
        private static readonly Color PanelTint = new Color(0.14f, 0.17f, 0.26f, 0.92f);
        private static readonly Color CardTint = new Color(0.11f, 0.14f, 0.22f, 0.96f);
        private static readonly Color LineColor = new Color(0.55f, 0.62f, 0.78f, 0.9f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color MutedTextColor = new Color(0.78f, 0.81f, 0.90f);
        private static readonly Color ButtonBlue = new Color(0.20f, 0.47f, 0.92f);
        private static readonly Color ButtonRed = new Color(0.80f, 0.24f, 0.24f);
        private static readonly Color ButtonSlate = new Color(0.32f, 0.36f, 0.46f);
        private static readonly Color ButtonGold = new Color(0.93f, 0.66f, 0.16f);
        private static readonly Vector2 PlacedRocketSize = new Vector2(132, 132);

        [MenuItem("Rocket Timeline/Build Everything (Menu + Game)")]
        public static void BuildEverything()
        {
            RocketItem[] rocketData = LoadRocketDataForEditor();
            if (rocketData == null) return;

            EnsureFolderExists(ScenesFolder);

            BuildMainMenuSceneInternal(rocketData);
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
            // The Main Menu now also builds its own "Browse Trivia" panel, which
            // needs the rocket data, so this loads it the same way the game
            // scene builder always has.
            RocketItem[] rocketData = LoadRocketDataForEditor();
            if (rocketData == null) return;
            EnsureFolderExists(ScenesFolder);
            BuildMainMenuSceneInternal(rocketData);
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
            // Height trimmed from 410 to 370 (and the pool area below shrunk to
            // match - see PoolArea) specifically to free up room for a real gap
            // between the timeline's bottom row and the rocket pool, instead of
            // them sitting edge to edge.
            RectTransform timelineArea = CreateUIObject("TimelineArea", canvasRoot);
            Anchor(timelineArea, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(-80, 370));

            RectTransform line = CreateStretchPanel("TimelineLine", timelineArea, LineColor);
            Anchor(line, new Vector2(0, 0.49f), new Vector2(1, 0.49f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 4));
            line.GetComponent<Image>().raycastTarget = false;
            AddTimelineNodes(line);

            RectTransform topRow = CreateRow("TopZonesRow", timelineArea);
            Anchor(topRow, new Vector2(0, 0.52f), new Vector2(1, 1f), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

            RectTransform bottomRow = CreateRow("BottomZonesRow", timelineArea);
            Anchor(bottomRow, new Vector2(0, 0f), new Vector2(1, 0.46f), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);

            var dropZoneLookup = new Dictionary<string, DropZone>();
            for (int i = 0; i < Zones.Length; i++)
            {
                var zoneDef = Zones[i];
                bool topRowZone = i % 2 == 0;
                RectTransform rowParent = topRowZone ? topRow : bottomRow;
                DropZone zone = BuildDropZone(rowParent, zoneDef.id, zoneDef.label);
                dropZoneLookup[zoneDef.id] = zone;
            }

            // ---- Rocket pool --------------------------------------------------------
            // Raised from y=125 to y=155 and shrunk from 380 to 350 tall (grid
            // cell height 180 -> 165, so 2 rows of cards still fit exactly:
            // 165*2 + 20 spacing = 350). Combined with TimelineArea's height
            // trim above, this opens a clear ~70px gap between the timeline's
            // bottom row and the pool cards, instead of them overlapping.
            RectTransform poolArea = CreateUIObject("PoolArea", canvasRoot);
            Anchor(poolArea, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 155), new Vector2(-80, 350));

            GridLayoutGroup grid = poolArea.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(240, 165);
            grid.spacing = new Vector2(24, 20);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

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
                draggableRockets.Add(BuildRocketCard(poolArea, data));
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

        private static void AddTimelineNodes(RectTransform line)
        {
            float[] columnFractions = { 1f / 8f, 3f / 8f, 5f / 8f, 7f / 8f };
            foreach (float fraction in columnFractions)
            {
                RectTransform node = CreateUIObject("Node", line);
                Image nodeImage = node.gameObject.AddComponent<Image>();
                nodeImage.color = LineColor;
                nodeImage.raycastTarget = false;
                node.anchorMin = node.anchorMax = new Vector2(fraction, 0.5f);
                node.pivot = new Vector2(0.5f, 0.5f);
                node.sizeDelta = new Vector2(18, 18);
                node.anchoredPosition = Vector2.zero;
            }
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
            Anchor(labelRect, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-16, 42));

            RectTransform slotAnchor = CreateUIObject("SlotAnchor", box);
            slotAnchor.anchorMin = slotAnchor.anchorMax = new Vector2(0.5f, 0.38f);
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
            RectTransform card = BuildNineSlicePanel($"Card_{data.id}", parent, "ui_panel", CardTint, new Vector2(240, 165));
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
            Anchor(icon, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.94f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

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

            RectTransform card = BuildNineSlicePanel("Card", root, "ui_panel", PanelTint, new Vector2(920, 580));
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920, 580));
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(44, 44, 40, 40);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            Text nameText = AddParagraph(card, "NameText", 34, TextColor, FontStyle.Bold, 55, TextAnchor.UpperLeft);
            Text countryText = AddParagraph(card, "CountryText", 24, MutedTextColor, FontStyle.Normal, 38, TextAnchor.UpperLeft);
            Text keyAdvanceText = AddParagraph(card, "KeyAdvanceText", 24, MutedTextColor, FontStyle.Normal, 95, TextAnchor.UpperLeft);
            Text triviaText = AddParagraph(card, "TriviaText", 24, TextColor, FontStyle.Italic, 120, TextAnchor.UpperLeft);

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
        /// all 8 rockets in timeline order. Shared between the game scene's
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

            Button closeButton = BuildStyledButton(card, "CloseButton", "Close", ButtonSlate, 150, 60);
            Anchor(closeButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -26), new Vector2(150, 60));

            // ---- Content area: text on the left, rocket image on the right ----------------
            RectTransform contentArea = CreateUIObject("ContentArea", card);
            Anchor(contentArea, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(-48, -220));

            RectTransform leftColumn = CreateUIObject("LeftColumn", contentArea);
            Anchor(leftColumn, new Vector2(0, 0), new Vector2(0.54f, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, 0));

            // A thin accent bar along the left edge, echoing the research
            // deck's own "bullet + vertical line before the heading" look.
            RectTransform accentBar = CreateStretchPanel("AccentBar", leftColumn, ButtonGold);
            Anchor(accentBar, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(3, 0), new Vector2(6, -10));
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

            AddParagraph(textStack, "KeyAdvanceLabel", 21, TextColor, FontStyle.Bold, 28, TextAnchor.UpperLeft).text = "Key Advances:";
            Text keyAdvanceValue = AddParagraph(textStack, "KeyAdvanceValue", 21, MutedTextColor, FontStyle.Normal, 64, TextAnchor.UpperLeft);

            AddParagraph(textStack, "TriviaLabel", 21, TextColor, FontStyle.Bold, 28, TextAnchor.UpperLeft).text = "Trivia Fact:";
            Text triviaValue = AddParagraph(textStack, "TriviaValue", 21, MutedTextColor, FontStyle.Normal, 76, TextAnchor.UpperLeft);

            AddParagraph(textStack, "CountriesLabel", 21, TextColor, FontStyle.Bold, 28, TextAnchor.UpperLeft).text = "Countries:";
            Text countryValue = AddParagraph(textStack, "CountriesValue", 21, MutedTextColor, FontStyle.Normal, 34, TextAnchor.UpperLeft);

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

            Text pageIndicator = CreateText("PageIndicator", navBar, "1 / 8", 22, MutedTextColor, TextAnchor.MiddleCenter).GetComponent<Text>();
            Anchor(pageIndicator.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 50));

            // ---- Wire the pager, in the same chronological order as the timeline itself ----
            var rocketsById = rocketData.ToDictionary(r => r.id, r => r);
            var orderedRockets = new List<RocketItem>();
            var orderedSprites = new List<Sprite>();
            foreach (string id in RocketIdsInPoolOrder)
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
            pager.rockets = orderedRockets.ToArray();
            pager.rocketSprites = orderedSprites.ToArray();

            UnityEventTools.AddVoidPersistentListener(closeButton.onClick, pager.Close);
            UnityEventTools.AddVoidPersistentListener(prevButton.onClick, pager.PrevPage);
            UnityEventTools.AddVoidPersistentListener(nextButton.onClick, pager.NextPage);

            root.gameObject.SetActive(false);
            return pager;
        }

        // ================================================================================
        // MAIN MENU SCENE
        // ================================================================================

        private static void BuildMainMenuSceneInternal(RocketItem[] rocketData)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateEventSystem();
            CreateCamera();
            RectTransform canvasRoot = CreateCanvas();
            CreateBackground(canvasRoot);

            RectTransform titleBlock = CreateUIObject("TitleBlock", canvasRoot);
            Anchor(titleBlock, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 280));

            RectTransform title = CreateText("Title", titleBlock, "Rocket Evolution\nTimeline", 72, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(title, new Vector2(0, 0.4f), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            RectTransform subtitle = CreateText("Subtitle", titleBlock,
                "Learn rocket history by dragging each rocket to its place on the timeline!",
                26, MutedTextColor, TextAnchor.MiddleCenter);
            Anchor(subtitle, new Vector2(0, 0), new Vector2(1, 0.4f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject sceneLoaderGo = new GameObject("SceneLoader", typeof(SceneLoader));
            SceneLoader sceneLoader = sceneLoaderGo.GetComponent<SceneLoader>();

            // ---- Play button + caption + secondary Browse Trivia, stacked bottom-up -------
            Button playButton = BuildStyledButton(canvasRoot, "PlayButton", "Play", ButtonGold, 360, 120);
            RectTransform playRect = playButton.GetComponent<RectTransform>();
            Anchor(playRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 340), new Vector2(360, 120));
            // Button text a little larger for the primary call-to-action.
            Text playLabel = playButton.GetComponentInChildren<Text>();
            if (playLabel != null) playLabel.fontSize = 36;
            UnityEventTools.AddVoidPersistentListener(playButton.onClick, sceneLoader.LoadGame);

            RectTransform playCaption = CreateText("PlayCaption", canvasRoot,
                "Play the drag & drop timeline challenge and see how much you\nremember of the evolutionary timeline of rockets.",
                22, MutedTextColor, TextAnchor.MiddleCenter);
            Anchor(playCaption, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(1000, 80));

            Button browseTriviaButton = BuildStyledButton(canvasRoot, "BrowseTriviaButton", "Browse Trivia", ButtonBlue, 300, 76);
            Anchor(browseTriviaButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 120), new Vector2(300, 76));

            RectTransform footer = CreateText("Footer", canvasRoot, "MPM - Group 2  |  Rocket Evolution Timeline",
                18, new Color(1, 1, 1, 0.45f), TextAnchor.LowerCenter);
            Anchor(footer, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 15), new Vector2(0, 30));

            // ---- Trivia review screen, reachable straight from the Main Menu too ----------
            TriviaReviewPagerUI triviaReviewPager = BuildTriviaReviewPanel(canvasRoot, rocketData);
            UnityEventTools.AddVoidPersistentListener(browseTriviaButton.onClick, triviaReviewPager.Show);

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

        private static Button BuildStyledButton(Transform parent, string name, string label, Color color, float width, float height)
        {
            RectTransform rect = BuildNineSlicePanel(name, parent, "ui_button", color, new Vector2(width, height));
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            Image image = rect.GetComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            button.colors = colors;

            RectTransform textRect = CreateText("Label", rect, label, 26, TextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(textRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            return button;
        }

        private static Text AddParagraph(Transform parent, string name, int fontSize, Color color, FontStyle style, float preferredHeight, TextAnchor alignment)
        {
            RectTransform rect = CreateText(name, parent, "", fontSize, color, alignment, style);
            Text text = rect.GetComponent<Text>();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = 1;
            return text;
        }

        private static RectTransform CreateRow(string name, Transform parent)
        {
            RectTransform row = CreateUIObject(name, parent);
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 26;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return row;
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
