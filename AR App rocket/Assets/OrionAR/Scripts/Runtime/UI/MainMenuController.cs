using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Main menu: title, start, and the result card when returning from a mission.
    ///
    /// Also home to the starfield and the slowly rotating spacecraft, which do the
    /// work of establishing tone before a word is read.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scene")]
        public Transform heroModel;
        public float heroSpinSpeed = 6f;
        public Transform heroBob;
        public float bobAmplitude = 0.04f;
        public float bobSpeed = 0.7f;

        [Header("UI")]
        public CanvasGroup mainGroup;
        public TMP_Text titleText;
        public TMP_Text subtitleText;
        public TMP_Text versionText;
        public TouchButton startButton;
        public TouchButton aboutButton;
        public TouchButton quitButton;

        [Header("About")]
        public GameObject aboutCard;
        public TMP_Text aboutBody;
        public TouchButton aboutClose;

        [Header("Return result")]
        public GameObject resultCard;
        public TMP_Text resultHeadline;
        public TMP_Text resultScore;
        public TMP_Text resultBody;
        public TouchButton resultDismiss;

        Vector3 _heroHome;

        void Start()
        {
            if (heroBob != null) _heroHome = heroBob.localPosition;

            if (titleText != null) titleText.text = "ORION";
            if (subtitleText != null)
                subtitleText.text = "LUNAR SPACECRAFT EXPLORER";
            if (versionText != null)
                versionText.text = "Artemis programme  ·  augmented reality";

            if (startButton != null) startButton.onClick.AddListener(OnStart);
            if (aboutButton != null) aboutButton.onClick.AddListener(ShowAbout);
            if (aboutClose != null) aboutClose.onClick.AddListener(HideAbout);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (resultDismiss != null) resultDismiss.onClick.AddListener(HideResult);

            if (aboutCard != null) aboutCard.SetActive(false);
            if (resultCard != null) resultCard.SetActive(false);

            if (aboutBody != null)
                aboutBody.text =
                    "Explore NASA's Orion spacecraft in augmented reality.\n\n" +
                    "Place the vehicle on any surface, scale it from a desktop model to " +
                    "its true eleven metres, and study all seventeen systems. Pass the " +
                    "readiness check and you fly the Trans-Earth Injection burn home.\n\n" +
                    "Orion carries four crew for up to twenty-one days beyond low Earth " +
                    "orbit. It flew crewed for the first time on Artemis II in April 2026.\n\n" +
                    "Built in Unity with AR Foundation. Spacecraft dimensions and engine " +
                    "figures follow published NASA and ESA data.\n\n" +
                    "<size=85%>Typefaces: Orbitron and Exo 2 (SIL Open Font Licence), " +
                    "Material Icons (Apache 2.0). Earth and Moon maps derived from NASA " +
                    "imagery.</size>";

            AudioDirector.SetAmbience("amb_menu", 2f);

            if (AppDirector.Instance != null && AppDirector.Instance.ReturnedFromMission)
            {
                ShowResult();
                AppDirector.Instance.ReturnedFromMission = false;
            }
        }

        void Update()
        {
            if (heroModel != null)
                heroModel.Rotate(Vector3.up, heroSpinSpeed * Time.deltaTime, Space.World);

            if (heroBob != null)
                heroBob.localPosition = _heroHome +
                    Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        }

        // ---------------------------------------------------------------

        void OnStart()
        {
            AudioDirector.Play("ui_launch");
            Haptics.Impact();
            if (AppDirector.Instance != null) AppDirector.Instance.StartMission();
        }

        /// <summary>
        /// While a card is open the menu buttons are hidden and the card made opaque:
        /// the cards are translucent glass, and BEGIN MISSION / EXIT showed through them.
        /// </summary>
        void SetMenuButtonsVisible(bool visible)
        {
            if (startButton != null) startButton.gameObject.SetActive(visible);
            if (aboutButton != null) aboutButton.gameObject.SetActive(visible);
            if (quitButton != null) quitButton.gameObject.SetActive(visible);
        }

        static void MakeOpaque(GameObject card)
        {
            if (card == null) return;
            var img = card.GetComponent<Image>();
            if (img == null) return;
            var c = img.color;
            if (c.a < 0.96f) { c.a = 0.96f; img.color = c; }
        }

        void ShowAbout()
        {
            SetMenuButtonsVisible(false);
            MakeOpaque(aboutCard);
            if (aboutCard != null) aboutCard.SetActive(true);
            AudioDirector.Play("ui_open");
        }

        void HideAbout()
        {
            SetMenuButtonsVisible(true);
            if (aboutCard != null) aboutCard.SetActive(false);
            AudioDirector.Play("ui_close");
        }

        void ShowResult()
        {
            if (resultCard == null || AppDirector.Instance == null) return;

            var app = AppDirector.Instance;
            SetMenuButtonsVisible(false);
            MakeOpaque(resultCard);
            resultCard.SetActive(true);

            if (resultHeadline != null)
            {
                resultHeadline.text = "MISSION COMPLETE";
                resultHeadline.color = UITheme.Success;
            }
            if (resultScore != null)
                resultScore.text = app.LastScore + " / " + app.LastTotal;

            int percent = app.LastTotal > 0
                ? Mathf.RoundToInt(100f * app.LastScore / app.LastTotal) : 0;

            if (resultBody != null)
                resultBody.text = "Readiness check: " + percent + "%.\n" +
                                  app.PartsStudied + " spacecraft systems studied.\n\n" +
                                  "Orion is home.";

            AudioDirector.Play("mission_success");
        }

        void HideResult()
        {
            SetMenuButtonsVisible(true);
            if (resultCard != null) resultCard.SetActive(false);
            AudioDirector.Play("ui_close");
        }

        void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
