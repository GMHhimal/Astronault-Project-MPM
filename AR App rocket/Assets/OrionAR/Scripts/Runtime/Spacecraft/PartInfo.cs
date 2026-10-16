using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Authored content for one spacecraft component.
    ///
    /// Kept as a ScriptableObject so an instructor or subject expert can revise the
    /// wording in the Inspector without touching code or recompiling - which matters
    /// when the text is being reviewed in the week before an exhibition.
    /// </summary>
    [CreateAssetMenu(menuName = "Orion AR/Part Info", fileName = "Part")]
    public class PartInfo : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id. Quiz questions and progress saving reference parts by this.")]
        public string partId;

        public string displayName = "Unnamed Component";

        [Tooltip("Short label for the checklist row. Keep under about 22 characters.")]
        public string shortName;

        public PartZone zone = PartZone.Interior;

        [Tooltip("Order in the guided tour. Lower numbers come first.")]
        public int order = 100;

        [Header("Content")]
        [Tooltip("One headline figure, e.g. 'Diameter 5.03 m'. Shown under the title.")]
        public string keySpec;

        [TextArea(4, 12)]
        public string summary;

        [Tooltip("The engineering reason this component is the way it is. This is the part students remember.")]
        [TextArea(3, 10)]
        public string whyItMatters;

        [Tooltip("Optional narration. Leave empty to run silently.")]
        public AudioClip narration;

        public string ShortLabel
        {
            get { return string.IsNullOrEmpty(shortName) ? displayName : shortName; }
        }
    }
}
