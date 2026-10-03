using System.Collections.Generic;
using UnityEngine;

namespace RocketTimeline.Data
{
    /// <summary>
    /// Loads RocketData.json (Assets/_Project/Resources/RocketData.json) once at
    /// startup and hands out RocketItem lookups by id. Keeping data loading in
    /// one place means GameManager, the scene builder, and any future screens
    /// (a menu, a "rocket encyclopedia" list, etc.) all read from the same source.
    /// </summary>
    public class RocketDatabase : MonoBehaviour
    {
        [Tooltip("Name of the JSON file inside a Resources folder, without the .json extension.")]
        public string resourceFileName = "RocketData";

        public IReadOnlyList<RocketItem> Rockets => rockets;

        private List<RocketItem> rockets = new List<RocketItem>();

        private void Awake()
        {
            Load();
        }

        private void Load()
        {
            TextAsset json = Resources.Load<TextAsset>(resourceFileName);
            if (json == null)
            {
                Debug.LogError($"[RocketDatabase] Could not find Resources/{resourceFileName}.json. " +
                                "Make sure the file lives directly inside a folder named 'Resources'.");
                rockets = new List<RocketItem>();
                return;
            }

            RocketItemList list = JsonUtility.FromJson<RocketItemList>(json.text);
            rockets = list != null && list.rockets != null
                ? new List<RocketItem>(list.rockets)
                : new List<RocketItem>();
        }

        public RocketItem GetById(string id)
        {
            return rockets.Find(r => r.id == id);
        }
    }
}
