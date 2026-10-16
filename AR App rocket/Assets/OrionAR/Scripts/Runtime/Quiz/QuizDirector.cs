using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Runs the ten-question readiness check.
    ///
    /// Passing at 80% rather than 100% is a deliberate change from the earlier build.
    /// A perfect-score gate in front of a queue at an exhibition means most people
    /// never see the ending, and one careless tap erases a correct run. Eight out of
    /// ten still demonstrates understanding while leaving room to be human.
    ///
    /// Every answer shows its explanation whether right or wrong, because the
    /// explanation is the teaching - a quiz that only says "incorrect" has wasted the
    /// moment the user was most willing to learn.
    /// </summary>
    public class QuizDirector : MonoBehaviour
    {
        [Header("Data")]
        public QuizData quiz;

        [Header("Panel")]
        public GameObject root;
        public CanvasGroup group;
        public TMP_Text titleText;
        public TMP_Text topicChip;
        public TMP_Text counterText;
        public TMP_Text questionText;
        public Image progressBar;

        [Header("Answers")]
        public List<TouchButton> answerButtons = new List<TouchButton>();

        [Header("Feedback")]
        public GameObject feedbackCard;
        public Image feedbackAccent;
        public TMP_Text feedbackVerdict;
        public TMP_Text feedbackBody;
        public TouchButton continueButton;

        [Header("Result")]
        public GameObject resultCard;
        public TMP_Text resultHeadline;
        public TMP_Text resultScore;
        public TMP_Text resultBody;
        public Image resultAccent;
        public TouchButton resultPrimary;
        public TouchButton resultSecondary;

        /// <summary>(score, total, passed)</summary>
        public event System.Action<int, int, bool> Finished;
        public event System.Action RetryRequested;

        readonly List<QuizQuestion> _order = new List<QuizQuestion>();
        readonly List<int> _optionMap = new List<int>();
        int _index;
        int _score;
        bool _awaitingAnswer;
        bool _active;
        readonly List<string> _missedParts = new List<string>();

        public bool IsActive { get { return _active; } }
        public int Score { get { return _score; } }

        bool _wired;

        void Awake()
        {
            // The panel is generated inactive, so this Awake first runs at the moment
            // Begin() switches it on. Hiding it here - as the earlier build did -
            // switched the quiz straight back off, and the readiness check never
            // appeared. Only hide when nothing has started the quiz yet.
            if (!_active && root != null && root != gameObject) root.SetActive(false);
            Wire();
        }

        void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < answerButtons.Count; i++)
            {
                int slot = i;
                var btn = answerButtons[i];
                if (btn == null) continue;
                btn.onClick.AddListener(() => Answer(slot));

                // The marked answers are disabled while the explanation shows; dimming
                // their labels made white-on-green text half transparent.
                btn.dimLabelWhenDisabled = false;

                // Three-line answers ran out of the bottom of their button into the
                // next one. Shrink slightly to fit rather than overflow.
                if (btn.label != null)
                {
                    float size = btn.label.fontSize;
                    btn.label.enableAutoSizing = true;
                    btn.label.fontSizeMax = size;
                    btn.label.fontSizeMin = Mathf.Round(size * 0.78f);
                    btn.label.textWrappingMode = TextWrappingModes.Normal;
                }
            }

            if (continueButton != null) continueButton.onClick.AddListener(Continue);
        }

        // ---------------------------------------------------------------

        public void Begin()
        {
            if (quiz == null || quiz.questions == null || quiz.questions.Count == 0)
            {
                Debug.LogError("[Quiz] No QuizData assigned.");
                return;
            }

            _active = true;
            Wire();
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = quiz.title;

            StartRun();
        }

        void StartRun()
        {
            _order.Clear();
            _order.AddRange(quiz.questions);
            if (quiz.shuffleQuestions) Shuffle(_order);

            _index = 0;
            _score = 0;
            _missedParts.Clear();

            if (resultCard != null) resultCard.SetActive(false);
            ShowQuestion();
        }

        void ShowQuestion()
        {
            if (feedbackCard != null) feedbackCard.SetActive(false);
            if (resultCard != null) resultCard.SetActive(false);
            SetQuestionVisible(true);

            var q = _order[_index];

            if (topicChip != null) topicChip.text = (q.topic ?? "SPACECRAFT").ToUpperInvariant();
            if (questionText != null) questionText.text = q.prompt;
            if (counterText != null)
            {
                counterText.gameObject.SetActive(true);
                counterText.text = "QUESTION " + (_index + 1) + " OF " + _order.Count;
            }
            if (progressBar != null) progressBar.fillAmount = (float)_index / _order.Count;

            _optionMap.Clear();
            int optionCount = Mathf.Min(q.options.Length, answerButtons.Count);
            for (int i = 0; i < optionCount; i++) _optionMap.Add(i);
            if (quiz.shuffleOptions) Shuffle(_optionMap);

            for (int slot = 0; slot < answerButtons.Count; slot++)
            {
                var btn = answerButtons[slot];
                if (btn == null) continue;

                bool used = slot < _optionMap.Count;
                btn.gameObject.SetActive(used);
                if (!used) continue;

                btn.Interactable = true;
                btn.normalColor = UITheme.SurfaceAlt;
                btn.pressedColor = UITheme.SurfaceHi;
                if (btn.background != null) btn.background.color = UITheme.SurfaceAlt;
                if (btn.label != null) btn.label.color = UITheme.TextHi;
                btn.SetLabel(q.options[_optionMap[slot]]);
            }

            _awaitingAnswer = true;
            AudioDirector.Play("quiz_next");
        }

        void Answer(int slot)
        {
            if (!_awaitingAnswer || slot >= _optionMap.Count) return;
            _awaitingAnswer = false;

            var q = _order[_index];
            int chosen = _optionMap[slot];
            bool correct = chosen == q.correctIndex;

            for (int i = 0; i < answerButtons.Count; i++)
            {
                var btn = answerButtons[i];
                if (btn == null || !btn.gameObject.activeSelf) continue;

                btn.Interactable = false;

                bool isCorrectOption = _optionMap[i] == q.correctIndex;
                Color c;
                if (isCorrectOption) c = UITheme.SuccessDim;
                else if (i == slot) c = UITheme.DangerDim;
                else c = UITheme.Alpha(UITheme.Surface, 0.55f);

                if (btn.background != null) btn.background.color = c;
                btn.normalColor = c;
                btn.disabledColor = c;     // the press release re-applies the disabled colour
                if (btn.label != null)
                    btn.label.color = (isCorrectOption || i == slot) ? UITheme.TextHi : UITheme.TextLow;

                // Keep only the chosen and the correct answer on screen while the
                // explanation is shown, so the feedback card never covers the one
                // answer the user needs to see - on a 16:9 phone there is no room for both.
                if (!isCorrectOption && i != slot) btn.gameObject.SetActive(false);
            }

            if (correct)
            {
                _score++;
                AudioDirector.Play("quiz_correct");
                Haptics.Confirm();
            }
            else
            {
                if (!string.IsNullOrEmpty(q.relatedPartId)) _missedParts.Add(q.relatedPartId);
                AudioDirector.Play("quiz_wrong");
                Haptics.Reject();
            }

            ShowFeedback(correct, q);
        }

        void ShowFeedback(bool correct, QuizQuestion q)
        {
            if (feedbackCard != null) feedbackCard.SetActive(true);

            Color accent = correct ? UITheme.Success : UITheme.Danger;
            if (feedbackAccent != null) feedbackAccent.color = accent;

            if (feedbackVerdict != null)
            {
                feedbackVerdict.text = correct ? "CORRECT" : "NOT QUITE";
                feedbackVerdict.color = accent;
            }

            if (feedbackBody != null) feedbackBody.text = q.explanation;

            bool last = _index >= _order.Count - 1;
            if (continueButton != null)
                continueButton.SetLabel(last ? "SEE RESULT" : "NEXT QUESTION");
        }

        void Continue()
        {
            if (_index >= _order.Count - 1) ShowResult();
            else { _index++; ShowQuestion(); }
        }

        /// <summary>The question card and answers sit behind the result card; hide them so
        /// the result reads cleanly.</summary>
        void SetQuestionVisible(bool on)
        {
            if (questionText != null && questionText.transform.parent != null)
                questionText.transform.parent.gameObject.SetActive(on);
            if (answerButtons.Count > 0 && answerButtons[0] != null && answerButtons[0].transform.parent != null)
                answerButtons[0].transform.parent.gameObject.SetActive(on);
        }

        void ShowResult()
        {
            SetQuestionVisible(false);
            if (feedbackCard != null) feedbackCard.SetActive(false);
            if (resultCard != null) resultCard.SetActive(true);
            if (progressBar != null) progressBar.fillAmount = 1f;
            // The result card's accent line sat on top of "QUESTION 10 OF 10".
            if (counterText != null) counterText.gameObject.SetActive(false);

            int total = _order.Count;
            int passMark = Mathf.CeilToInt(total * quiz.passFraction);
            bool passed = _score >= passMark;
            int percent = Mathf.RoundToInt(100f * _score / Mathf.Max(1, total));

            if (resultScore != null)
            {
                resultScore.text = _score + " / " + total;
                resultScore.color = passed ? UITheme.Success : UITheme.Warning;
            }
            if (resultAccent != null) resultAccent.color = passed ? UITheme.Success : UITheme.Warning;

            if (passed)
            {
                if (resultHeadline != null)
                {
                    resultHeadline.text = "CLEARED FOR TRANS-EARTH INJECTION";
                    resultHeadline.color = UITheme.Success;
                }
                if (resultBody != null)
                    resultBody.text = "You scored " + percent + "%. Flight control has cleared Orion to " +
                                      "leave lunar orbit and begin the coast home.";

                if (resultPrimary != null)
                {
                    resultPrimary.gameObject.SetActive(true);
                    resultPrimary.SetLabel("BEGIN THE BURN");
                    resultPrimary.onClick.RemoveAllListeners();
                    resultPrimary.onClick.AddListener(() => Complete(true));
                }
                if (resultSecondary != null) resultSecondary.gameObject.SetActive(false);

                AudioDirector.Play("quiz_pass");
                Haptics.Impact();
            }
            else
            {
                if (resultHeadline != null)
                {
                    resultHeadline.text = "NOT YET CLEARED";
                    resultHeadline.color = UITheme.Warning;
                }

                string hint = _missedParts.Count > 0
                    ? " Review the " + PrettyId(_missedParts[0]) + " and try again."
                    : " Review the spacecraft systems and try again.";

                if (resultBody != null)
                    resultBody.text = "You scored " + percent + "%. You need " + passMark +
                                      " of " + total + " to be cleared for the burn." + hint;

                if (resultPrimary != null)
                {
                    resultPrimary.gameObject.SetActive(true);
                    resultPrimary.SetLabel("TRY AGAIN");
                    resultPrimary.onClick.RemoveAllListeners();
                    resultPrimary.onClick.AddListener(StartRun);
                }
                if (resultSecondary != null)
                {
                    resultSecondary.gameObject.SetActive(true);
                    resultSecondary.SetLabel("BACK TO SPACECRAFT");
                    resultSecondary.onClick.RemoveAllListeners();
                    resultSecondary.onClick.AddListener(() =>
                    {
                        Hide();
                        if (RetryRequested != null) RetryRequested();
                    });
                }

                AudioDirector.Play("quiz_fail");
            }
        }

        void Complete(bool passed)
        {
            _active = false;
            Hide();
            if (Finished != null) Finished(_score, _order.Count, passed);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        static string PrettyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "spacecraft";
            return id.Replace('_', ' ');
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
