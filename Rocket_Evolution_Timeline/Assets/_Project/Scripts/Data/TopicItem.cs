using System;

namespace RocketTimeline.Data
{
    /// <summary>
    /// One fact card inside a bonus-content topic (see TopicItem below) - the
    /// same "slideshow of short cards" shape as a rocket's trivia, just not
    /// tied to a single rocket. A topic with 8 facts pages through 8 of these.
    /// </summary>
    [Serializable]
    public class TopicFact
    {
        /// <summary>Short bolded heading for the card, e.g. "Mariner 2".</summary>
        public string heading;

        /// <summary>The fact text itself.</summary>
        public string body;
    }

    /// <summary>
    /// Plain data for one of the bonus "About..." trivia topics added in v13
    /// (Satellites, Rovers & Robotic Explorers, International Space Station) -
    /// the non-rocket research the client sent (satellite/probe book scans,
    /// the ISS deep-dive, the NASA robotic-exploration table, Apollo Lunar
    /// Rover facts) that doesn't belong in the drag-and-drop timeline but is
    /// too good to leave out of the app. Loaded from
    /// Resources/TopicData.json, same JsonUtility pattern as RocketItem/
    /// RocketItemList - NOT a MonoBehaviour or ScriptableObject, so a new
    /// topic or fact is just a JSON edit, no code changes needed.
    /// </summary>
    [Serializable]
    public class TopicItem
    {
        /// <summary>Unique key, e.g. "satellites".</summary>
        public string id;

        /// <summary>Big heading shown on the topic's slideshow panel, e.g. "Satellites".</summary>
        public string displayName;

        /// <summary>Smaller descriptive line under displayName, e.g. "Orbiting Earth".</summary>
        public string subtitle;

        /// <summary>File name (without extension) under Assets/_Project/Sprites/Topics/.</summary>
        public string iconName;

        public TopicFact[] facts;
    }

    /// <summary>Wrapper so JsonUtility (which can't parse a bare top-level array) can read the file.</summary>
    [Serializable]
    public class TopicItemList
    {
        public TopicItem[] topics;
    }
}
