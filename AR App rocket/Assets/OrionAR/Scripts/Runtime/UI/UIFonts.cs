using TMPro;
using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Hands the app's font assets to UIKit at runtime.
    ///
    /// UIKit's font fields are static, so they are empty in a fresh player. Anything
    /// built at runtime - checklist rows, for example - would otherwise fall back to
    /// the TextMeshPro default face and look different from the generated screens.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UIFonts : MonoBehaviour
    {
        public TMP_FontAsset display;
        public TMP_FontAsset heading;
        public TMP_FontAsset body;
        public TMP_FontAsset icons;

        void Awake()
        {
            if (display != null) UIKit.DisplayFont = display;
            if (heading != null) UIKit.HeadingFont = heading;
            if (body != null) UIKit.BodyFont = body;
            if (icons != null) UIKit.IconFont = icons;
        }
    }
}
