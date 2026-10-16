using UnityEngine;

namespace OrionAR
{
    /// <summary>
    /// Single source of truth for the app's visual language.
    ///
    /// Everything the UI draws pulls its colour, size and spacing from here, so the
    /// look can be retuned in one file rather than hunted through scenes. The palette
    /// is built for a dark, deep-space feel: near-black backgrounds so the spacecraft
    /// and the Moon carry the image, one luminous cyan for anything interactive, and a
    /// warm star-gold reserved for achievement and data readouts.
    ///
    /// Sizes are expressed in density-independent pixels (dp) against a 1080x1920
    /// reference. UIScale converts to the device's actual pixels.
    /// </summary>
    public static class UITheme
    {
        // ---------------------------------------------------------------
        // Palette
        // ---------------------------------------------------------------

        /// <summary>Deepest background. Near-black with a blue bias, never pure black -
        /// pure black crushes against OLED bezels and looks like a dead screen.</summary>
        public static readonly Color Space      = Hex("#060912");
        public static readonly Color SpaceDeep  = Hex("#030509");

        /// <summary>Raised surfaces: sheets, cards, bars.</summary>
        public static readonly Color Surface    = Hex("#111A2B");
        public static readonly Color SurfaceAlt = Hex("#18243A");
        public static readonly Color SurfaceHi  = Hex("#20304B");

        /// <summary>Hairlines and dividers.</summary>
        public static readonly Color Outline    = Hex("#2A3A56");
        public static readonly Color OutlineSoft= Hex("#1C2840");

        /// <summary>The one interactive colour. If it glows cyan, you can touch it.</summary>
        public static readonly Color Accent     = Hex("#3FD8E8");
        public static readonly Color AccentDim  = Hex("#1E8C99");
        public static readonly Color AccentGlow = Hex("#8BF0FA");

        /// <summary>Reserved for data, scores and achievement. Never for buttons.</summary>
        public static readonly Color Gold       = Hex("#FFC661");
        public static readonly Color GoldDim    = Hex("#B88A3C");

        public static readonly Color Success    = Hex("#4ADE80");
        public static readonly Color SuccessDim = Hex("#1F7A46");
        public static readonly Color Danger     = Hex("#FF6B6B");
        public static readonly Color DangerDim  = Hex("#8C2F2F");
        public static readonly Color Warning    = Hex("#FFB13F");

        /// <summary>Text. Primary is off-white; pure white vibrates against dark UI.</summary>
        public static readonly Color TextHi     = Hex("#EEF4FB");
        public static readonly Color TextMid    = Hex("#A8BACF");
        public static readonly Color TextLow    = Hex("#6B7F99");
        public static readonly Color TextOnAccent = Hex("#04141A");

        /// <summary>Zone tints, used to colour-code the three regions of the vehicle.</summary>
        public static readonly Color ZoneExterior = Hex("#6FA8FF");
        public static readonly Color ZoneInterior = Hex("#3FD8E8");
        public static readonly Color ZoneService  = Hex("#FFC661");

        // ---------------------------------------------------------------
        // Type scale (dp)
        //
        // Deliberately large. This is read at arm's length, often standing, often by
        // someone who has queued and will give it thirty seconds. 15dp is the floor
        // for body text; anything smaller is decoration, not communication.
        // ---------------------------------------------------------------

        public const float FontDisplay  = 40f;
        public const float FontTitle    = 28f;
        public const float FontHeadline = 22f;
        public const float FontBodyLg   = 19f;
        public const float FontBody     = 17f;
        public const float FontLabel    = 15f;
        public const float FontCaption  = 13f;
        public const float FontOverline = 12f;
        public const float FontIcon     = 24f;

        /// <summary>Multiplied into line height. 1.45 is comfortable for long body copy.</summary>
        public const float LineTight    = 1.15f;
        public const float LineNormal   = 1.35f;
        public const float LineRelaxed  = 1.5f;

        /// <summary>Letter spacing for all-caps overlines, which need air to stay legible.</summary>
        public const float TrackOverline = 0.12f;

        // ---------------------------------------------------------------
        // Spacing and metrics (dp)
        // ---------------------------------------------------------------

        public const float Space1 = 4f;
        public const float Space2 = 8f;
        public const float Space3 = 12f;
        public const float Space4 = 16f;
        public const float Space5 = 24f;
        public const float Space6 = 32f;
        public const float Space7 = 48f;
        public const float Space8 = 64f;

        /// <summary>Screen side gutter. Anything closer to the edge gets clipped by
        /// curved glass or hit by the palm.</summary>
        public const float Gutter = 20f;

        /// <summary>Minimum touch target. Below this, miss rates climb sharply -
        /// this is the single most common mobile UI failure.</summary>
        public const float TouchMin = 52f;
        public const float ButtonHeight = 60f;
        public const float ButtonHeightLg = 68f;

        public const float RadiusSm = 10f;
        public const float RadiusMd = 16f;
        public const float RadiusLg = 24f;
        public const float RadiusPill = 999f;

        /// <summary>
        /// Reference resolution in dp. Every size in this file is a dp value, so the
        /// canvas reference must be a dp-sized phone too - a 412 dp wide portrait
        /// screen, matched on width. The earlier build used 1080x1920 here, which made
        /// all type and every control render at roughly 40% of its intended size.
        /// </summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(412f, 915f);

        // ---------------------------------------------------------------
        // Motion
        //
        // Short durations. On a handheld device the user's wrist is already moving;
        // slow UI animation reads as lag, not polish.
        // ---------------------------------------------------------------

        public const float DurFast   = 0.14f;
        public const float DurNormal = 0.22f;
        public const float DurSlow   = 0.36f;

        /// <summary>Ease-out curve. Fast start, gentle settle - the standard for UI
        /// that should feel responsive to touch.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        /// <summary>Slight overshoot, for things that pop into existence.</summary>
        public static float EaseBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        public static Color Hex(string hex)
        {
            Color c;
            if (ColorUtility.TryParseHtmlString(hex, out c)) return c;
            return Color.magenta;      // loudly wrong, so a typo is obvious
        }

        public static Color Alpha(Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        public static Color ZoneColor(PartZone zone)
        {
            switch (zone)
            {
                case PartZone.Exterior:      return ZoneExterior;
                case PartZone.ServiceModule: return ZoneService;
                default:                     return ZoneInterior;
            }
        }

        public static string ZoneLabel(PartZone zone)
        {
            switch (zone)
            {
                case PartZone.Exterior:      return "EXTERIOR";
                case PartZone.ServiceModule: return "SERVICE MODULE";
                default:                     return "CREW CABIN";
            }
        }
    }

    /// <summary>Which region of the vehicle a component belongs to.</summary>
    public enum PartZone
    {
        Exterior = 0,
        Interior = 1,
        ServiceModule = 2,
    }
}
