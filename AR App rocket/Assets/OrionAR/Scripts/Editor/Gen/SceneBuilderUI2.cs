using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR.Gen
{
    /// <summary>Quiz, finale and menu screens. Continues SceneBuilderUI.</summary>
    public static partial class SceneBuilderUI
    {
        /// <summary>
        /// A full-screen dimming scrim with a card inside it. The scrim focuses the
        /// eye and swallows taps, so a modal card can never be "clicked through" to
        /// the buttons underneath. Returns the inner card; <paramref name="modal"/> is
        /// the object to show and hide.
        /// </summary>
        static RectTransform Modal(string name, RectTransform parent, float left, float right,
                                   float top, float bottom, out GameObject modal)
        {
            var root = UIKit.Rect(name, parent);
            UIKit.Stretch(root);
            modal = root.gameObject;

            var scrim = UIKit.Panel("Scrim", root, UITheme.Alpha(UITheme.SpaceDeep, 0.72f));
            UIKit.Stretch(scrim.rectTransform, -60f);
            scrim.raycastTarget = true;

            var card = UIKit.Rect("Card", root);
            UIKit.Fill(card, left, right, top, bottom);
            return card;
        }

        // ================================================================
        // Quiz
        // ================================================================

        static QuizDirector BuildQuizPanel(RectTransform parent, QuizData data)
        {
            var root = UIKit.Rect("QuizPanel", parent);
            UIKit.Stretch(root);

            var scrim = UIKit.Panel("Scrim", root, UITheme.Alpha(UITheme.SpaceDeep, 0.93f));
            UIKit.Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;

            // ---- header -----------------------------------------------
            var topic = UIKit.Overline("TopicChip", root, "PROPULSION", UITheme.Accent);
            topic.alignment = TextAlignmentOptions.Center;
            UIKit.AnchorTop(topic.rectTransform, 18f, 40f, G);

            var title = UIKit.Text("Title", root, "MISSION READINESS CHECK",
                                   UITheme.FontHeadline, UITheme.TextHi,
                                   TextAlignmentOptions.Center, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            title.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(title.rectTransform, 30f, 62f, G);

            var counter = UIKit.Text("Counter", root, "QUESTION 1 OF 10",
                                     UITheme.FontCaption, UITheme.TextLow,
                                     TextAlignmentOptions.Center);
            UIKit.AnchorTop(counter.rectTransform, 20f, 98f, G);

            // Progress bar.
            var barBg = UIKit.Panel("ProgressTrack", root, UITheme.SurfaceHi, 4f);
            UIKit.AnchorTop(barBg.rectTransform, 6f, 124f, G);

            var barFillRT = UIKit.Rect("ProgressFill", barBg.rectTransform);
            UIKit.Stretch(barFillRT);
            var barFill = barFillRT.gameObject.AddComponent<Image>();
            barFill.sprite = UIKit.RoundedSprite(4f);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.color = UITheme.Accent;
            barFill.fillAmount = 0f;
            barFill.raycastTarget = false;

            // ---- question ----------------------------------------------
            var questionCard = UIKit.Panel("QuestionCard", root,
                                           UITheme.Alpha(UITheme.Surface, 0.95f), UITheme.RadiusMd);
            UIKit.AnchorTop(questionCard.rectTransform, 156f, 144f, G);

            var question = UIKit.Text("QuestionText", questionCard.rectTransform,
                "Question text",
                UITheme.FontBodyLg, UITheme.TextHi, TextAlignmentOptions.Center,
                lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Stretch(question.rectTransform, UITheme.Space4);
            question.enableAutoSizing = true;
            question.fontSizeMax = UITheme.FontBodyLg;
            question.fontSizeMin = UITheme.FontLabel;

            // ---- answers ------------------------------------------------
            var answers = UIKit.Rect("Answers", root);
            UIKit.AnchorTop(answers, 4f * 64f + 3f * UITheme.Space2, 308f, G);
            UIKit.VStack(answers, UITheme.Space2, null, TextAnchor.UpperCenter);

            var buttons = new List<TouchButton>();
            for (int i = 0; i < 4; i++)
            {
                var b = UIKit.Button("Answer_" + i, answers, "Option " + (i + 1),
                                     UIKit.ButtonKind.Secondary, 64f, UITheme.FontBody);
                UIKit.Sized(b.gameObject, 64f);
                if (b.label != null)
                {
                    // Answers are sentences: body face, wrapping, left aligned.
                    if (UIKit.BodyFont != null) b.label.font = UIKit.BodyFont;
                    b.label.fontStyle = FontStyles.Normal;
                    b.label.characterSpacing = 0f;
                    b.label.textWrappingMode = TextWrappingModes.Normal;
                    b.label.fontSizeMin = UITheme.FontCaption;
                    b.label.alignment = TextAlignmentOptions.MidlineLeft;
                    b.label.lineSpacing = (UITheme.LineTight - 1f) * 50f;
                }
                buttons.Add(b);
            }

            // ---- feedback ------------------------------------------------
            var feedback = UIKit.Rect("FeedbackCard", root);
            UIKit.AnchorBottom(feedback, 270f, 16f, G);

            var fbBg = UIKit.Panel("Background", feedback,
                                   UITheme.Alpha(UITheme.SurfaceAlt, 0.98f), UITheme.RadiusMd);
            UIKit.Stretch(fbBg.rectTransform);
            fbBg.raycastTarget = true;

            var fbAccent = UIKit.Panel("Accent", feedback, UITheme.Success, UITheme.RadiusSm);
            fbAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
            fbAccent.rectTransform.anchorMax = new Vector2(0f, 1f);
            fbAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            fbAccent.rectTransform.sizeDelta = new Vector2(5f, -16f);
            fbAccent.rectTransform.anchoredPosition = new Vector2(8f, 0f);

            var verdict = UIKit.Text("Verdict", feedback, "CORRECT",
                                     UITheme.FontHeadline, UITheme.Success,
                                     TextAlignmentOptions.TopLeft, heading: true,
                                     lineSpacingMul: UITheme.LineTight);
            verdict.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(verdict.rectTransform, 28f, 18f, UITheme.Space5);

            var fbBody = UIKit.Text("Body", feedback, "", UITheme.FontBody, UITheme.TextMid,
                                    TextAlignmentOptions.TopLeft,
                                    lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Fill(fbBody.rectTransform, UITheme.Space5, UITheme.Space4, 54f, 88f);
            fbBody.enableAutoSizing = true;
            fbBody.fontSizeMax = UITheme.FontBody;
            fbBody.fontSizeMin = UITheme.FontCaption;

            var cont = UIKit.Button("ContinueButton", feedback, "NEXT QUESTION",
                                    UIKit.ButtonKind.Primary);
            UIKit.AnchorBottom(cont.GetComponent<RectTransform>(),
                               UITheme.ButtonHeight, 16f, UITheme.Space4);

            feedback.gameObject.SetActive(false);

            // ---- result --------------------------------------------------
            var result = UIKit.Rect("ResultCard", root);
            UIKit.Fill(result, G, G, 110f, 90f);

            var rBg = UIKit.Panel("Background", result,
                                  UITheme.Alpha(UITheme.Surface, 0.98f), UITheme.RadiusLg);
            UIKit.Stretch(rBg.rectTransform);
            rBg.raycastTarget = true;

            var rAccent = UIKit.Panel("Accent", result, UITheme.Success, UITheme.RadiusSm);
            UIKit.AnchorTop(rAccent.rectTransform, 5f, 0f, 0f);

            var rHead = UIKit.Text("Headline", result, "CLEARED FOR TRANS-EARTH INJECTION",
                                   UITheme.FontHeadline, UITheme.Success,
                                   TextAlignmentOptions.Center, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            rHead.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(rHead.rectTransform, 72f, 42f, UITheme.Space5);

            var rScore = UIKit.Text("Score", result, "8 / 10",
                                    UITheme.FontDisplay, UITheme.Success,
                                    TextAlignmentOptions.Center, heading: true,
                                    lineSpacingMul: UITheme.LineTight);
            rScore.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(rScore.rectTransform, 56f, 126f, G);

            var rBody = UIKit.Text("Body", result, "", UITheme.FontBody, UITheme.TextMid,
                                   TextAlignmentOptions.Top,
                                   lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Fill(rBody.rectTransform, UITheme.Space5, UITheme.Space5, 196f, 170f);
            rBody.enableAutoSizing = true;
            rBody.fontSizeMax = UITheme.FontBody;
            rBody.fontSizeMin = UITheme.FontCaption;

            var rPrimary = UIKit.Button("PrimaryButton", result, "BEGIN THE BURN",
                                        UIKit.ButtonKind.Primary, UITheme.ButtonHeightLg);
            UIKit.AnchorBottom(rPrimary.GetComponent<RectTransform>(),
                               UITheme.ButtonHeightLg, 88f, UITheme.Space5);

            var rSecondary = UIKit.Button("SecondaryButton", result, "BACK TO SPACECRAFT",
                                          UIKit.ButtonKind.Ghost);
            UIKit.AnchorBottom(rSecondary.GetComponent<RectTransform>(),
                               UITheme.ButtonHeight, 20f, UITheme.Space5);

            result.gameObject.SetActive(false);

            // ---- wire ----------------------------------------------------
            var quiz = root.gameObject.AddComponent<QuizDirector>();
            quiz.quiz = data;
            quiz.root = root.gameObject;
            quiz.titleText = title;
            quiz.topicChip = topic;
            quiz.counterText = counter;
            quiz.questionText = question;
            quiz.progressBar = barFill;
            quiz.answerButtons = buttons;
            quiz.feedbackCard = feedback.gameObject;
            quiz.feedbackAccent = fbAccent;
            quiz.feedbackVerdict = verdict;
            quiz.feedbackBody = fbBody;
            quiz.continueButton = cont;
            quiz.resultCard = result.gameObject;
            quiz.resultHeadline = rHead;
            quiz.resultScore = rScore;
            quiz.resultBody = rBody;
            quiz.resultAccent = rAccent;
            quiz.resultPrimary = rPrimary;
            quiz.resultSecondary = rSecondary;

            root.gameObject.SetActive(false);
            return quiz;
        }

        // ================================================================
        // Finale HUD
        // ================================================================

        static void BuildFinaleHud(RectTransform parent, ARUIRefs ui)
        {
            var root = UIKit.Rect("FinaleHUD", parent);
            UIKit.Stretch(root);
            ui.finaleHud = root.gameObject;
            ui.finaleGroup = root.gameObject.AddComponent<CanvasGroup>();

            // Phase banner.
            var banner = UIKit.Panel("PhaseBanner", root, UITheme.Alpha(UITheme.Space, 0.85f),
                                     UITheme.RadiusMd);
            UIKit.AnchorTop(banner.rectTransform, 56f, 40f, G);

            ui.finalePhase = UIKit.Text("PhaseText", banner.rectTransform, "LUNAR ORBIT",
                                        UITheme.FontBodyLg, UITheme.Accent,
                                        TextAlignmentOptions.Center, heading: true,
                                        lineSpacingMul: UITheme.LineTight);
            ui.finalePhase.fontStyle = FontStyles.Bold;
            UIKit.Stretch(ui.finalePhase.rectTransform, UITheme.Space3);

            // Countdown, large and central.
            ui.finaleCountdown = UIKit.Text("Countdown", root, "T−10",
                                            UITheme.FontDisplay * 1.5f, UITheme.TextHi,
                                            TextAlignmentOptions.Center, heading: true,
                                            lineSpacingMul: UITheme.LineTight);
            ui.finaleCountdown.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(ui.finaleCountdown.rectTransform, 90f, 128f, G);

            // Telemetry panel. These figures are computed from the rocket equation,
            // so showing them is the educational payload of the whole sequence.
            var telemetry = UIKit.Panel("Telemetry", root, UITheme.Alpha(UITheme.Space, 0.86f),
                                        UITheme.RadiusMd);
            UIKit.AnchorBottom(telemetry.rectTransform, 230f, 40f, G);

            var telHead = UIKit.Overline("TelemetryHeader", telemetry.rectTransform,
                                         "FLIGHT TELEMETRY", UITheme.TextLow);
            UIKit.AnchorTop(telHead.rectTransform, 18f, 14f, UITheme.Space4);

            ui.finaleTimeWarp = UIKit.Text("TimeWarp", telemetry.rectTransform, "TIME ×22",
                                           UITheme.FontCaption, UITheme.Gold,
                                           TextAlignmentOptions.TopRight,
                                           lineSpacingMul: UITheme.LineTight);
            UIKit.AnchorTop(ui.finaleTimeWarp.rectTransform, 18f, 14f, UITheme.Space4);

            ui.finaleDeltaV = TelemetryRow(telemetry.rectTransform, "DeltaV", 44f,
                                           "ΔV  0 / 450 m/s", UITheme.Accent);
            ui.finaleAccel = TelemetryRow(telemetry.rectTransform, "Accel", 76f,
                                          "ACCEL  0.00 m/s²", UITheme.TextMid);
            ui.finaleMass = TelemetryRow(telemetry.rectTransform, "Mass", 108f,
                                         "MASS  20.50 t", UITheme.TextMid);
            ui.finaleBurnTime = TelemetryRow(telemetry.rectTransform, "BurnTime", 140f,
                                             "BURN TIME  00:00", UITheme.TextMid);

            // Burn progress.
            var progTrack = UIKit.Panel("BurnTrack", telemetry.rectTransform, UITheme.SurfaceHi, 4f);
            UIKit.AnchorBottom(progTrack.rectTransform, 8f, 20f, UITheme.Space4);

            var progFillRT = UIKit.Rect("BurnFill", progTrack.rectTransform);
            UIKit.Stretch(progFillRT);
            ui.finaleProgress = progFillRT.gameObject.AddComponent<Image>();
            ui.finaleProgress.sprite = UIKit.RoundedSprite(4f);
            ui.finaleProgress.type = Image.Type.Filled;
            ui.finaleProgress.fillMethod = Image.FillMethod.Horizontal;
            ui.finaleProgress.color = UITheme.Gold;
            ui.finaleProgress.fillAmount = 0f;
            ui.finaleProgress.raycastTarget = false;

            root.gameObject.SetActive(false);

            // ---- completion card ---------------------------------------
            GameObject completeModal;
            var card = Modal("MissionCompleteCard", parent, G, G, 130f, 110f, out completeModal);
            ui.completeCard = completeModal;

            var cBg = UIKit.Panel("Background", card, UITheme.Alpha(UITheme.Surface, 0.97f),
                                  UITheme.RadiusLg);
            UIKit.Stretch(cBg.rectTransform);
            cBg.raycastTarget = true;

            var cAccent = UIKit.Panel("Accent", card, UITheme.Success, UITheme.RadiusSm);
            UIKit.AnchorTop(cAccent.rectTransform, 5f, 0f, 0f);

            ui.completeHeadline = UIKit.Text("Headline", card, "MISSION COMPLETE",
                                             UITheme.FontTitle, UITheme.Success,
                                             TextAlignmentOptions.Center, heading: true,
                                             lineSpacingMul: UITheme.LineTight);
            ui.completeHeadline.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(ui.completeHeadline.rectTransform, 40f, 46f, G);

            var scoreCaption = UIKit.Overline("ScoreCaption", card, "READINESS CHECK", UITheme.TextLow);
            scoreCaption.alignment = TextAlignmentOptions.Center;
            UIKit.AnchorTop(scoreCaption.rectTransform, 18f, 100f, G);

            ui.completeScore = UIKit.Text("Score", card, "9 / 10",
                                          UITheme.FontDisplay, UITheme.Gold,
                                          TextAlignmentOptions.Center, heading: true,
                                          lineSpacingMul: UITheme.LineTight);
            ui.completeScore.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(ui.completeScore.rectTransform, 56f, 124f, G);

            ui.completeBody = UIKit.Text("Body", card, "", UITheme.FontBody, UITheme.TextMid,
                                         TextAlignmentOptions.Top,
                                         lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Fill(ui.completeBody.rectTransform, UITheme.Space5, UITheme.Space5, 196f, 108f);
            ui.completeBody.enableAutoSizing = true;
            ui.completeBody.fontSizeMax = UITheme.FontBody;
            ui.completeBody.fontSizeMin = UITheme.FontCaption;

            ui.completeButton = UIKit.Button("ReturnButton", card, "RETURN TO MENU",
                                             UIKit.ButtonKind.Primary, UITheme.ButtonHeightLg);
            UIKit.AnchorBottom(ui.completeButton.GetComponent<RectTransform>(),
                               UITheme.ButtonHeightLg, 24f, UITheme.Space5);

            completeModal.SetActive(false);
        }

        static TMP_Text TelemetryRow(RectTransform parent, string name, float top,
                                     string value, Color color)
        {
            var t = UIKit.Text(name, parent, value, UITheme.FontLabel, color,
                               TextAlignmentOptions.TopLeft, heading: true,
                               lineSpacingMul: UITheme.LineTight);
            UIKit.AnchorTop(t.rectTransform, 24f, top, UITheme.Space4);
            return t;
        }

        // ================================================================
        // Main menu
        // ================================================================

        public static MainMenuController BuildMenuUI(Transform canvas)
        {
            var safe = UIKit.Rect("SafeArea", canvas);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // Vignette: darkens the edges so the centre of the frame carries the eye.
            var vignette = UIKit.Panel("Vignette", safe, UITheme.Alpha(UITheme.SpaceDeep, 0.55f));
            UIKit.Stretch(vignette.rectTransform);
            vignette.sprite = UIKit.GlowSprite(256);
            vignette.color = UITheme.Alpha(UITheme.SpaceDeep, 0.7f);
            vignette.raycastTarget = false;

            // ---- title block --------------------------------------------
            var kicker = UIKit.Overline("Kicker", safe, "NASA  ·  ARTEMIS PROGRAMME", UITheme.Accent);
            kicker.alignment = TextAlignmentOptions.Center;
            UIKit.AnchorTop(kicker.rectTransform, 20f, 64f, G);

            var title = UIKit.Text("Title", safe, "ORION",
                                   UITheme.FontDisplay * 1.6f, UITheme.TextHi,
                                   TextAlignmentOptions.Center, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 12f;
            UIKit.AnchorTop(title.rectTransform, 78f, 88f, G);

            var subtitle = UIKit.Text("Subtitle", safe, "LUNAR SPACECRAFT EXPLORER",
                                      UITheme.FontBody, UITheme.TextMid,
                                      TextAlignmentOptions.Center, heading: true,
                                      lineSpacingMul: UITheme.LineTight);
            subtitle.characterSpacing = 6f;
            UIKit.AnchorTop(subtitle.rectTransform, 24f, 170f, G);

            // Hairline under the title.
            var rule = UIKit.Panel("Rule", safe, UITheme.Alpha(UITheme.Accent, 0.5f));
            rule.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rule.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rule.rectTransform.pivot = new Vector2(0.5f, 1f);
            rule.rectTransform.sizeDelta = new Vector2(120f, 2f);
            rule.rectTransform.anchoredPosition = new Vector2(0f, -206f);

            // ---- actions --------------------------------------------------
            var start = UIKit.Button("StartButton", safe, "BEGIN MISSION",
                                     UIKit.ButtonKind.Primary, UITheme.ButtonHeightLg,
                                     UITheme.FontHeadline);
            var sr = start.GetComponent<RectTransform>();
            UIKit.AnchorBottom(sr, UITheme.ButtonHeightLg, 176f, G + 16f);

            var about = UIKit.Button("AboutButton", safe, "ABOUT THIS PROJECT",
                                     UIKit.ButtonKind.Ghost);
            UIKit.AnchorBottom(about.GetComponent<RectTransform>(),
                               UITheme.ButtonHeight, 108f, G + 16f);

            var quit = UIKit.Button("QuitButton", safe, "EXIT", UIKit.ButtonKind.Ghost,
                                    UITheme.TouchMin, UITheme.FontLabel);
            UIKit.AnchorBottom(quit.GetComponent<RectTransform>(),
                               UITheme.TouchMin, 48f, G + 110f);

            var version = UIKit.Text("Version", safe, "Artemis programme  ·  augmented reality",
                                     UITheme.FontCaption, UITheme.TextLow,
                                     TextAlignmentOptions.Center);
            UIKit.AnchorBottom(version.rectTransform, 18f, 22f, G);

            // ---- about card -------------------------------------------------
            GameObject aboutModal;
            var aboutCard = Modal("AboutCard", safe, G, G, 90f, 90f, out aboutModal);

            var aBg = UIKit.Panel("Background", aboutCard,
                                  UITheme.Alpha(UITheme.Surface, 0.98f), UITheme.RadiusLg);
            UIKit.Stretch(aBg.rectTransform);
            aBg.raycastTarget = true;

            var aAccent = UIKit.Panel("Accent", aboutCard, UITheme.Accent, UITheme.RadiusSm);
            UIKit.AnchorTop(aAccent.rectTransform, 5f, 0f, 0f);

            var aTitle = UIKit.Text("Title", aboutCard, "ABOUT",
                                    UITheme.FontHeadline, UITheme.TextHi,
                                    TextAlignmentOptions.TopLeft, heading: true,
                                    lineSpacingMul: UITheme.LineTight);
            aTitle.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(aTitle.rectTransform, 30f, 36f, UITheme.Space5);

            var aBody = UIKit.Text("Body", aboutCard, "", UITheme.FontBody, UITheme.TextMid,
                                   TextAlignmentOptions.TopLeft,
                                   lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Fill(aBody.rectTransform, UITheme.Space5, UITheme.Space5, 84f, 104f);
            aBody.enableAutoSizing = true;
            aBody.fontSizeMax = UITheme.FontBody;
            aBody.fontSizeMin = UITheme.FontCaption;

            var aClose = UIKit.Button("CloseButton", aboutCard, "CLOSE", UIKit.ButtonKind.Secondary);
            UIKit.AnchorBottom(aClose.GetComponent<RectTransform>(),
                               UITheme.ButtonHeight, 24f, UITheme.Space5);

            aboutModal.SetActive(false);

            // ---- returning result card ---------------------------------------
            GameObject resultModal;
            var resultCard = Modal("ResultCard", safe, G, G, 150f, 150f, out resultModal);

            var rBg = UIKit.Panel("Background", resultCard,
                                  UITheme.Alpha(UITheme.Surface, 0.98f), UITheme.RadiusLg);
            UIKit.Stretch(rBg.rectTransform);
            rBg.raycastTarget = true;

            var rAccent = UIKit.Panel("Accent", resultCard, UITheme.Success, UITheme.RadiusSm);
            UIKit.AnchorTop(rAccent.rectTransform, 5f, 0f, 0f);

            var rHead = UIKit.Text("Headline", resultCard, "MISSION COMPLETE",
                                   UITheme.FontTitle, UITheme.Success,
                                   TextAlignmentOptions.Center, heading: true,
                                   lineSpacingMul: UITheme.LineTight);
            rHead.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(rHead.rectTransform, 38f, 40f, G);

            var rScore = UIKit.Text("Score", resultCard, "9 / 10",
                                    UITheme.FontDisplay, UITheme.Gold,
                                    TextAlignmentOptions.Center, heading: true,
                                    lineSpacingMul: UITheme.LineTight);
            rScore.fontStyle = FontStyles.Bold;
            UIKit.AnchorTop(rScore.rectTransform, 54f, 92f, G);

            var rBody = UIKit.Text("Body", resultCard, "", UITheme.FontBody, UITheme.TextMid,
                                   TextAlignmentOptions.Top,
                                   lineSpacingMul: UITheme.LineRelaxed);
            UIKit.Fill(rBody.rectTransform, UITheme.Space5, UITheme.Space5, 158f, 100f);

            var rDismiss = UIKit.Button("DismissButton", resultCard, "CONTINUE",
                                        UIKit.ButtonKind.Primary);
            UIKit.AnchorBottom(rDismiss.GetComponent<RectTransform>(),
                               UITheme.ButtonHeight, 24f, UITheme.Space5);

            resultModal.SetActive(false);

            // ---- wire -----------------------------------------------------
            var menu = canvas.gameObject.AddComponent<MainMenuController>();
            menu.mainGroup = safe.gameObject.AddComponent<CanvasGroup>();
            menu.titleText = title;
            menu.subtitleText = subtitle;
            menu.versionText = version;
            menu.startButton = start;
            menu.aboutButton = about;
            menu.quitButton = quit;
            menu.aboutCard = aboutModal;
            menu.aboutBody = aBody;
            menu.aboutClose = aClose;
            menu.resultCard = resultModal;
            menu.resultHeadline = rHead;
            menu.resultScore = rScore;
            menu.resultBody = rBody;
            menu.resultDismiss = rDismiss;

            return menu;
        }
    }
}
