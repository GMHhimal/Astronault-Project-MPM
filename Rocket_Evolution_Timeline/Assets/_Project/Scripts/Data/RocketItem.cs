using System;

namespace RocketTimeline.Data
{
    /// <summary>
    /// Plain data describing a single rocket card in the timeline mini-game.
    /// Loaded from Resources/RocketData.json - NOT a MonoBehaviour or
    /// ScriptableObject on purpose, so there is nothing to re-wire in the
    /// Inspector if you edit the JSON. Add a new rocket by adding a new
    /// entry to the JSON array and a matching sprite; no code changes needed.
    /// </summary>
    [Serializable]
    public class RocketItem
    {
        /// <summary>Unique key used everywhere in code, e.g. "falcon_9".</summary>
        public string id;

        /// <summary>Name shown under the rocket card, e.g. "Falcon 9".</summary>
        public string displayName;

        /// <summary>Must match a DropZone.zoneId - this is the "correct answer".</summary>
        public string correctZoneId;

        /// <summary>Label shown on the timeline slot itself, e.g. "2015 - 2017".</summary>
        public string zoneLabel;

        public string country;
        public string keyAdvance;
        public string triviaFact;

        /// <summary>File name (without extension) under Assets/_Project/Sprites/Rockets/.</summary>
        public string spriteName;
    }

    /// <summary>Wrapper so JsonUtility (which can't parse a bare top-level array) can read the file.</summary>
    [Serializable]
    public class RocketItemList
    {
        public RocketItem[] rockets;
    }
}
