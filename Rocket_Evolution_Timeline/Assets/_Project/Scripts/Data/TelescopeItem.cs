using System;

namespace RocketTimeline.Data
{
    /// <summary>
    /// Plain data for one entry in the Telescopes section (added alongside the
    /// Rocket Evolution Timeline, per client request to keep telescopes
    /// separate from the rocket drag-and-drop game for clarity). Unlike
    /// RocketItem, a telescope isn't a drag-and-drop piece - it's a single
    /// "encyclopedia" page browsed via TelescopePagerUI, paired with TWO
    /// images per entry instead of one:
    ///   - hardwareSpriteName: an actual photo of the telescope itself
    ///     (Assets/_Project/Sprites/Telescopes/Hardware/).
    ///   - viewImageSpriteName: a real astrophotography image representing
    ///     what that telescope reveals, stylized as a circular "eyepiece"
    ///     view (Assets/_Project/Sprites/Telescopes/Views/).
    /// Loaded from Resources/TelescopeData.json, same JsonUtility pattern as
    /// RocketItem/TopicItem - NOT a MonoBehaviour or ScriptableObject, so a
    /// new telescope is just a JSON edit (plus two sprites) away, no code
    /// changes needed.
    /// </summary>
    [Serializable]
    public class TelescopeItem
    {
        /// <summary>Unique key, e.g. "hubble".</summary>
        public string id;

        /// <summary>Big heading shown on the telescope's page, e.g. "Hubble Space Telescope".</summary>
        public string displayName;

        /// <summary>Short line under displayName: launch/first-light date and location, e.g.
        /// "Launched April 24, 1990 - Low Earth Orbit, ~540 km up".</summary>
        public string era;

        /// <summary>1-2 sentences on what kind of telescope this is and the hard numbers
        /// (mirror size, dish count, etc.) - the "what it is" block.</summary>
        public string opticalPrinciple;

        /// <summary>The educational core of the page: why the view image looks the way it
        /// does, and - crucially - whether a human eye could ever actually see this
        /// directly, or whether (as with every entry so far) it's built from digital
        /// sensor data and false color mapped afterward. This is what turns the circular
        /// "eyepiece" image from a pretty picture into a teaching moment.</summary>
        public string whyItLooksThisWay;

        /// <summary>Short standalone trivia bullets (NOT full TopicFact cards - these are
        /// one or two sentences each, rendered as a bulleted list on the same page rather
        /// than paged individually).</summary>
        public string[] trivia;

        /// <summary>File name (without extension) under Assets/_Project/Sprites/Telescopes/Hardware/.</summary>
        public string hardwareSpriteName;

        /// <summary>File name (without extension) under Assets/_Project/Sprites/Telescopes/Views/.</summary>
        public string viewImageSpriteName;

        /// <summary>True if viewImageSpriteName was processed as a circular eyepiece crop
        /// (the current house style for every entry). Reserved for a future amateur-telescope
        /// entry that falls back to a plain rectangular "photo-split" image instead, per
        /// client direction, without needing a code change to TelescopePagerUI - the pager
        /// just skips the circular mask/frame treatment when this is false.</summary>
        public bool viewImageIsEyepieceStyle = true;

        /// <summary>Required attribution line for hardwareSpriteName, shown under the image
        /// in small print (NASA/NRAO/ESO imagery - several require a visible credit).</summary>
        public string hardwareCredit;

        /// <summary>Required attribution line for viewImageSpriteName.</summary>
        public string viewCredit;
    }

    /// <summary>Wrapper so JsonUtility (which can't parse a bare top-level array) can read the file.</summary>
    [Serializable]
    public class TelescopeItemList
    {
        public TelescopeItem[] telescopes;
    }
}
