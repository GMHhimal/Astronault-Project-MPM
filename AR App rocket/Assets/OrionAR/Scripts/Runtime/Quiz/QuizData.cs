using System.Collections.Generic;
using UnityEngine;

namespace OrionAR
{
    [System.Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 6)] public string prompt;

        [Tooltip("Four options. Keep each under about 90 characters so they fit a phone without scrolling.")]
        public string[] options = new string[4];

        [Tooltip("Index into options of the single correct answer.")]
        public int correctIndex;

        [TextArea(2, 6)] public string explanation;

        [Tooltip("Optional partId, so a wrong answer can point back at the right component.")]
        public string relatedPartId;

        [Tooltip("Shown as a chip above the question, e.g. 'PROPULSION'.")]
        public string topic = "SPACECRAFT";
    }

    [CreateAssetMenu(menuName = "Orion AR/Quiz", fileName = "Quiz")]
    public class QuizData : ScriptableObject
    {
        public string title = "MISSION READINESS CHECK";

        [TextArea(2, 5)]
        public string intro = "Ten questions on the spacecraft and the physics of spaceflight.";

        [Tooltip("Fraction of correct answers needed to pass. 0.8 means 8 of 10.")]
        [Range(0f, 1f)] public float passFraction = 0.8f;

        public bool shuffleQuestions = true;
        public bool shuffleOptions = true;

        public List<QuizQuestion> questions = new List<QuizQuestion>();

        public int PassMark
        {
            get { return Mathf.CeilToInt(questions.Count * passFraction); }
        }
    }
}
