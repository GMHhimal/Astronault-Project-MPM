using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrionAR
{
    /// <summary>
    /// Builders for every UI primitive in the app, all pulling from UITheme.
    ///
    /// Used by the editor scene generator to construct the screens, and at runtime to
    /// spawn dynamic content such as checklist rows and quiz options. Keeping both
    /// paths on the same builders is what stops the generated screens and the
    /// runtime-spawned rows drifting apart visually.
    /// </summary>
    public static class UIKit
    {
        /// <summary>Large titles (Orbitron). Used for heading text at headline size and above.</summary>
        public static TMP_FontAsset DisplayFont;
        /// <summary>Buttons, labels, overlines (Exo 2 SemiBold).</summary>
        public static TMP_FontAsset HeadingFont;
        /// <summary>Running text (Exo 2 Regular).</summary>
        public static TMP_FontAsset BodyFont;
        /// <summary>Material Icons.</summary>
        public static TMP_FontAsset IconFont;

        /// <summary>
        /// Set by the Editor scene generator. Sprites drawn in memory are not saved
        /// with a scene, so without this every rounded panel and the progress ring
        /// came back as plain squares the next time the scene was opened. The hook
        /// writes each generated sprite to a PNG asset and returns the imported one.
        /// At runtime it is null and sprites stay in memory, which is fine there.
        /// </summary>
        public static System.Func<Sprite, string, Sprite> PersistSprite;

        static Sprite Persist(Sprite s, string key)
        {
            return PersistSprite != null ? PersistSprite(s, key) : s;
        }

        /// <summary>Heading text at or above this size uses the display face.</summary>
        public const float DisplayThreshold = UITheme.FontHeadline;

        // ---------------------------------------------------------------
        // Core objects
        // ---------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, float radius = 0f)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (radius > 0f)
            {
                img.sprite = RoundedSprite(radius);
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            return img;
        }

        public static TMP_Text Text(string name, Transform parent, string content,
                                    float size, Color color,
                                    TextAlignmentOptions align = TextAlignmentOptions.TopLeft,
                                    bool heading = false, float lineSpacingMul = UITheme.LineNormal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();

            var font = heading
                ? ((size >= DisplayThreshold && DisplayFont != null) ? DisplayFont : HeadingFont)
                : BodyFont;
            if (font != null) t.font = font;

            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.lineSpacing = (lineSpacingMul - 1f) * 100f * 0.5f;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static TMP_Text Overline(string name, Transform parent, string content, Color color)
        {
            var t = Text(name, parent, content.ToUpperInvariant(), UITheme.FontOverline, color,
                         TextAlignmentOptions.TopLeft, heading: true, lineSpacingMul: UITheme.LineTight);
            t.characterSpacing = UITheme.TrackOverline * 100f;
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        // ---------------------------------------------------------------
        // Buttons
        // ---------------------------------------------------------------

        public enum ButtonKind { Primary, Secondary, Ghost, Danger }

        /// <summary>
        /// A touch button meeting the minimum target size, with a press animation.
        /// Returns the TouchButton so callers can hook onClick and retitle it.
        /// </summary>
        public static TouchButton Button(string name, Transform parent, string label,
                                         ButtonKind kind = ButtonKind.Primary,
                                         float height = UITheme.ButtonHeight,
                                         float fontSize = UITheme.FontBodyLg)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(0f, Mathf.Max(height, UITheme.TouchMin));

            Color bg, fg, border;
            switch (kind)
            {
                case ButtonKind.Secondary:
                    bg = UITheme.SurfaceHi; fg = UITheme.TextHi; border = UITheme.Outline; break;
                case ButtonKind.Ghost:
                    bg = UITheme.Alpha(UITheme.Surface, 0f); fg = UITheme.TextMid; border = UITheme.Outline; break;
                case ButtonKind.Danger:
                    bg = UITheme.DangerDim; fg = UITheme.TextHi; border = UITheme.Danger; break;
                default:
                    bg = UITheme.Accent; fg = UITheme.TextOnAccent; border = UITheme.AccentGlow; break;
            }

            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            img.sprite = RoundedSprite(UITheme.RadiusMd);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            // Outline drawn as a second, slightly larger image behind the fill.
            var outline = Panel("Border", rt, border, UITheme.RadiusMd);
            Stretch(outline.rectTransform, -1.5f);
            outline.transform.SetAsFirstSibling();

            var txt = Text("Label", rt, label, fontSize, fg, TextAlignmentOptions.Center,
                           heading: true, lineSpacingMul: UITheme.LineTight);
            txt.fontStyle = FontStyles.Bold;
            txt.characterSpacing = 4f;
            txt.enableAutoSizing = true;
            txt.fontSizeMax = fontSize;
            txt.fontSizeMin = Mathf.Max(UITheme.FontCaption, fontSize * 0.7f);
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(txt.rectTransform, 0f);
            txt.rectTransform.offsetMin = new Vector2(UITheme.Space4, 0f);
            txt.rectTransform.offsetMax = new Vector2(-UITheme.Space4, 0f);

            var btn = rt.gameObject.AddComponent<TouchButton>();
            btn.background = img;
            btn.border = outline;
            btn.label = txt;
            btn.normalColor = bg;
            btn.pressedColor = kind == ButtonKind.Primary
                ? UITheme.AccentDim
                : Color.Lerp(bg, Color.white, 0.12f);
            return btn;
        }

        /// <summary>Circular icon button, for close / help / back affordances.</summary>
        public static TouchButton IconButton(string name, Transform parent, string glyph,
                                             float diameter = UITheme.TouchMin)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(diameter, diameter);

            var img = rt.gameObject.AddComponent<Image>();
            img.color = UITheme.Alpha(UITheme.SurfaceHi, 0.92f);
            img.sprite = RoundedSprite(diameter * 0.5f);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;

            var txt = Text("Glyph", rt, glyph, UITheme.FontIcon, UITheme.TextHi,
                           TextAlignmentOptions.Center, lineSpacingMul: UITheme.LineTight);
            if (IconFont != null) txt.font = IconFont;
            Stretch(txt.rectTransform, 0f);

            var btn = rt.gameObject.AddComponent<TouchButton>();
            btn.background = img;
            btn.label = txt;
            btn.normalColor = img.color;
            btn.pressedColor = UITheme.Accent;
            return btn;
        }

        // ---------------------------------------------------------------
        // Layout helpers
        // ---------------------------------------------------------------

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void AnchorTop(RectTransform rt, float height, float topOffset = 0f,
                                     float sideInset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-sideInset * 2f, height);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
        }

        public static void AnchorBottom(RectTransform rt, float height, float bottomOffset = 0f,
                                        float sideInset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-sideInset * 2f, height);
            rt.anchoredPosition = new Vector2(0f, bottomOffset);
        }

        public static void Fill(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing,
                                                 RectOffset padding = null,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing,
                                                   RectOffset padding = null,
                                                   TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static LayoutElement Sized(GameObject go, float height, float width = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f) { le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f; }
            return le;
        }

        public static ContentSizeFitter FitHeight(RectTransform rt)
        {
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return f;
        }

        public static Image Divider(Transform parent, float inset = 0f)
        {
            var img = Panel("Divider", parent, UITheme.OutlineSoft);
            img.rectTransform.sizeDelta = new Vector2(-inset * 2f, 1f);
            Sized(img.gameObject, 1f);
            return img;
        }

        // ---------------------------------------------------------------
        // Rounded-rect sprite cache
        //
        // Generated rather than imported so the project carries no UI image assets.
        // Sliced 9-patch, so one sprite serves every size at a given radius.
        // ---------------------------------------------------------------

        static readonly System.Collections.Generic.Dictionary<int, Sprite> RoundedCache =
            new System.Collections.Generic.Dictionary<int, Sprite>();

        public static Sprite RoundedSprite(float radius)
        {
            // Pill shapes ask for 999: a sliced sprite shrinks oversized corners to fit
            // the rect anyway, so 48 looks identical and saves a 2000px texture.
            int r = Mathf.Clamp(Mathf.RoundToInt(radius), 2, 48);
            Sprite cached;
            if (RoundedCache.TryGetValue(r, out cached) && cached != null) return cached;

            int size = r * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RoundedRect_" + r;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color[size * size];
            float cx = size * 0.5f, cy = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the rounded-rect boundary, computed from the
                    // nearest corner centre. Antialiased over one pixel.
                    float dx = Mathf.Abs(x + 0.5f - cx) - (cx - r);
                    float dy = Mathf.Abs(y + 0.5f - cy) - (cy - r);
                    float d;
                    if (dx > 0f && dy > 0f) d = Mathf.Sqrt(dx * dx + dy * dy);
                    else d = Mathf.Max(dx, dy);

                    float a = Mathf.Clamp01((r - d) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();

            var border = new Vector4(r + 1, r + 1, r + 1, r + 1);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                                       new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect, border);
            sprite.name = "RoundedRect_" + r;
            sprite = Persist(sprite, "RoundedRect_" + r);
            RoundedCache[r] = sprite;
            return sprite;
        }

        /// <summary>Soft radial glow, used behind focal elements and the progress ring.</summary>
        static Sprite _glow;
        static readonly System.Collections.Generic.Dictionary<string, Sprite> RingCache =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite GlowSprite(int size = 128)
        {
            if (_glow != null) return _glow;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "RadialGlow";
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;                      // tighter falloff than linear
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            _glow = Persist(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f)), "RadialGlow");
            return _glow;
        }

        /// <summary>Ring sprite for the radial progress indicator.</summary>
        public static Sprite RingSprite(int size = 256, float thicknessFraction = 0.12f)
        {
            string key = "Ring_" + size + "_" + Mathf.RoundToInt(thicknessFraction * 1000f);
            Sprite cachedRing;
            if (RingCache.TryGetValue(key, out cachedRing) && cachedRing != null) return cachedRing;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "ProgressRing";
            tex.wrapMode = TextureWrapMode.Clamp;

            var px = new Color[size * size];
            float c = size * 0.5f;
            float outer = c - 1f;
            float inner = outer * (1f - thicknessFraction * 2f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            var ringSprite = Persist(Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f)), key);
            RingCache[key] = ringSprite;
            return ringSprite;
        }

        /// <summary>Clears the sprite caches, so a rebuild regenerates and re-saves them.</summary>
        public static void ClearSpriteCaches()
        {
            RoundedCache.Clear();
            RingCache.Clear();
            _glow = null;
        }
    }
}
