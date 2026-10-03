using RocketTimeline.Data;
using UnityEngine;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// The learning layer on top of the drag-and-drop mechanic: a simple
    /// popup card showing a rocket's country, key advance, and trivia fact.
    /// Opened either from the small "i" button on a pool card, or (optionally)
    /// after Check Answers for a deeper "here's what you just placed" moment.
    /// </summary>
    public class TriviaPopupUI : MonoBehaviour
    {
        [Tooltip("The whole popup, including the dim background - this is what gets shown/hidden.")]
        public GameObject root;

        public Text nameText;
        public Text countryText;
        public Text keyAdvanceText;
        public Text triviaText;
        public Button closeButton;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (root != null) root.SetActive(false);
        }

        public void Show(RocketItem data)
        {
            if (nameText != null) nameText.text = data.displayName;
            if (countryText != null) countryText.text = $"Country: {data.country}";
            if (keyAdvanceText != null) keyAdvanceText.text = $"Key Advance: {data.keyAdvance}";
            if (triviaText != null) triviaText.text = $"Trivia: {data.triviaFact}";
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
