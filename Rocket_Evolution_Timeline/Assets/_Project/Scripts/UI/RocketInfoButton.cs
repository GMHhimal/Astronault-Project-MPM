using RocketTimeline.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace RocketTimeline.UI
{
    /// <summary>
    /// Sits on the small "i" button in the corner of a rocket card. Lets the
    /// player check a rocket's trivia fact at any time, not only after
    /// answering - this is what turns the mechanic into a learning tool
    /// rather than a pure guessing game.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class RocketInfoButton : MonoBehaviour
    {
        public GameManager gameManager;
        public DraggableRocket rocket;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (gameManager != null && rocket != null)
                    gameManager.ShowTriviaFor(rocket.rocketId);
            });
        }
    }
}
