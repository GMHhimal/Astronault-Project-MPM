using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Astronaut.MoonLanding
{
    /// <summary>
    /// UI toolkit for the Moon Landing HUD, built from code (no prefabs → no merge conflicts).
    /// Sci-fi style: glowing glass panels, corner brackets, Orbitron numbers, procedural sprites.
    /// </summary>
    public static class MoonUI
    {
        public static readonly Color Accent = new Color(0.30f, 0.85f, 1f, 1f);
        public static readonly Color AccentDim = new Color(0.30f, 0.85f, 1f, 0.35f);
        public static readonly Color Good = new Color(0.35f, 1f, 0.55f, 1f);
        public static readonly Color Warn = new Color(1f, 0.72f, 0.18f, 1f);
        public static readonly Color Danger = new Color(1f, 0.28f, 0.24f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.28f, 1f);
        public static readonly Color PanelColor = new Color(0.02f, 0.06f, 0.10f, 0.78f);
        public static readonly Color TextDim = new Color(0.62f, 0.74f, 0.82f, 1f);

        static Font _display, _body;
        static Sprite _circle, _ring, _ringThick, _star, _rounded, _glow, _arrow, _gradient, _corner, _softDot;

        /// <summary>Assign the fonts from the MoonLandingLibrary (falls back to the built-in font).</summary>
        public static void SetFonts(Font display, Font body)
        {
            _display = display;
            _body = body;
        }

        static Font Builtin { get { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } }
        public static Font DisplayFont { get { return _display != null ? _display : Builtin; } }
        public static Font BodyFont { get { return _body != null ? _body : Builtin; } }

        // ------------------------------------------------------------------ layout
        public static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image MakeImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            RectTransform rt = MakeRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            if (sprite == RoundedSprite || sprite == GlowSprite) img.type = Image.Type.Sliced;
            return img;
        }

        public static Text MakeText(Transform parent, string name, string text, int size, TextAnchor align, Color color,
            bool display = false, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = MakeRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = display ? DisplayFont : BodyFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.lineSpacing = 0.95f;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>Glass panel with an outer glow, thin border and corner brackets.</summary>
        public static Image MakePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color? tint = null)
        {
            Color accent = tint ?? Accent;
            RectTransform root = Place(MakeRect(name, parent), anchor, pivot, pos, size);

            Image glow = MakeImage(root, "Glow", new Color(accent.r, accent.g, accent.b, 0.18f), GlowSprite);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-18f, -18f);
            glow.rectTransform.offsetMax = new Vector2(18f, 18f);

            Image body = MakeImage(root, "Body", PanelColor, RoundedSprite);
            Stretch(body.rectTransform);

            Image sheen = MakeImage(root, "Sheen", new Color(accent.r, accent.g, accent.b, 0.10f), GradientSprite);
            Stretch(sheen.rectTransform);
            sheen.rectTransform.offsetMin = new Vector2(3f, 3f);
            sheen.rectTransform.offsetMax = new Vector2(-3f, -3f);

            var border = body.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(accent.r, accent.g, accent.b, 0.45f);
            border.effectDistance = new Vector2(1.5f, -1.5f);

            for (int i = 0; i < 4; i++)
            {
                Image c = MakeImage(root, "Corner" + i, accent, CornerSprite);
                bool right = i == 1 || i == 2, top = i < 2;
                Place(c.rectTransform, new Vector2(right ? 1f : 0f, top ? 1f : 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f));
                c.rectTransform.localRotation = Quaternion.Euler(0f, 0f, right ? (top ? -90f : 180f) : (top ? 0f : 90f));
            }
            return body;
        }

        public static Button MakeButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize,
            UnityAction onClick, Color? tint = null)
        {
            Color accent = tint ?? Accent;
            RectTransform root = Place(MakeRect(name, parent), anchor, new Vector2(0.5f, 0.5f), pos, size);

            Image glow = MakeImage(root, "Glow", new Color(accent.r, accent.g, accent.b, 0.25f), GlowSprite);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-14f, -14f);
            glow.rectTransform.offsetMax = new Vector2(14f, 14f);

            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = RoundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 0.92f);
            bg.raycastTarget = true;

            Image sheen = MakeImage(root, "Sheen", new Color(accent.r, accent.g, accent.b, 0.35f), GradientSprite);
            Stretch(sheen.rectTransform);
            sheen.rectTransform.offsetMin = new Vector2(2f, 2f);
            sheen.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var outline = root.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock cb = button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier = 1.6f;
            cb.fadeDuration = 0.06f;
            button.colors = cb;
            if (onClick != null) button.onClick.AddListener(onClick);

            Text t = MakeText(root, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            Stretch(t.rectTransform);
            return button;
        }

        public static HoldButton MakeHoldButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color? tint = null, bool round = false)
        {
            Color accent = tint ?? Accent;
            RectTransform root = Place(MakeRect(name, parent), anchor, new Vector2(0.5f, 0.5f), pos, size);

            Image glow = MakeImage(root, "Glow", new Color(accent.r, accent.g, accent.b, 0.3f), round ? SoftDotSprite : GlowSprite);
            Stretch(glow.rectTransform);
            glow.rectTransform.offsetMin = new Vector2(-24f, -24f);
            glow.rectTransform.offsetMax = new Vector2(24f, 24f);

            Image bg = root.gameObject.AddComponent<Image>();
            bg.sprite = round ? CircleSprite : RoundedSprite;
            if (!round) bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            Image ring = MakeImage(root, "Ring", accent, round ? RingThickSprite : null);
            if (round) Stretch(ring.rectTransform);
            else
            {
                Object.Destroy(ring.gameObject);
                var o = root.gameObject.AddComponent<Outline>();
                o.effectColor = accent;
                o.effectDistance = new Vector2(2f, -2f);
            }

            var hold = root.gameObject.AddComponent<HoldButton>();
            hold.background = bg;
            hold.glow = glow;
            hold.normalColor = new Color(accent.r * 0.22f, accent.g * 0.22f, accent.b * 0.22f, 0.88f);
            hold.heldColor = new Color(accent.r * 0.8f, accent.g * 0.8f, accent.b * 0.8f, 0.95f);
            bg.color = hold.normalColor;

            Text t = MakeText(root, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white, true, FontStyle.Bold);
            Stretch(t.rectTransform);
            return hold;
        }

        /// <summary>Pulses a glow image (used by guidance to point at the right control).</summary>
        public static void Pulse(Image glow, bool on, Color color, float baseAlpha)
        {
            if (glow == null) return;
            float a = on ? 0.45f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f) : baseAlpha;
            glow.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(a));
            glow.rectTransform.localScale = Vector3.one * (on ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 9f) : 1f);
        }

        // ------------------------------------------------------------------ procedural sprites
        public static Sprite CircleSprite { get { if (_circle == null) _circle = BuildSprite(128, (x, y) => Disc(x, y, 1f)); return _circle; } }
        public static Sprite RingSprite { get { if (_ring == null) _ring = BuildSprite(256, (x, y) => Disc(x, y, 1f) - Disc(x, y, 0.95f)); return _ring; } }
        public static Sprite RingThickSprite { get { if (_ringThick == null) _ringThick = BuildSprite(256, (x, y) => Disc(x, y, 1f) - Disc(x, y, 0.88f)); return _ringThick; } }
        public static Sprite StarSprite { get { if (_star == null) _star = BuildSprite(128, Star); return _star; } }
        public static Sprite SoftDotSprite { get { if (_softDot == null) _softDot = BuildSprite(128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 2f)); return _softDot; } }
        /// <summary>Triangle pointing up.</summary>
        public static Sprite ArrowSprite { get { if (_arrow == null) _arrow = BuildSprite(128, Arrow); return _arrow; } }
        /// <summary>L-shaped bracket for the top-left corner.</summary>
        public static Sprite CornerSprite { get { if (_corner == null) _corner = BuildSprite(64, Corner); return _corner; } }

        public static Sprite RoundedSprite
        {
            get
            {
                if (_rounded == null) _rounded = BuildSliced(48, 12f, 0f);
                return _rounded;
            }
        }

        public static Sprite GlowSprite
        {
            get
            {
                if (_glow == null) _glow = BuildSliced(96, 30f, 26f);
                return _glow;
            }
        }

        /// <summary>Vertical gradient: bright at the top, transparent at the bottom (glass sheen).</summary>
        public static Sprite GradientSprite
        {
            get
            {
                if (_gradient == null)
                {
                    var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 4; x++)
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(y / 63f, 2.2f)));
                    tex.Apply();
                    _gradient = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
                }
                return _gradient;
            }
        }

        delegate float Shape(float x, float y);

        static float Disc(float x, float y, float radius)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((radius - d) * 60f);
        }

        static float Star(float x, float y)
        {
            float angle = Mathf.Atan2(y, x) + Mathf.PI / 2f;
            float d = Mathf.Sqrt(x * x + y * y);
            float k = Mathf.Repeat(angle / (Mathf.PI * 2f / 5f), 1f);
            float edge = Mathf.Lerp(0.42f, 0.95f, Mathf.Abs(k - 0.5f) * 2f);
            return Mathf.Clamp01((edge - d) * 40f);
        }

        static float Arrow(float x, float y)
        {
            // triangle: apex (0, 0.85), base y = -0.6, half width 0.8
            if (y < -0.6f || y > 0.85f) return 0f;
            float halfWidth = 0.8f * (0.85f - y) / 1.45f;
            return Mathf.Clamp01((halfWidth - Mathf.Abs(x)) * 40f) * Mathf.Clamp01((y + 0.6f) * 40f);
        }

        static float Corner(float x, float y)
        {
            // map to 0..1 with origin at the top-left
            float u = (x + 1f) * 0.5f, v = (1f - y) * 0.5f;
            const float t = 0.16f;
            bool horizontal = v < t && u < 0.9f;
            bool vertical = u < t && v < 0.9f;
            return horizontal || vertical ? 1f : 0f;
        }

        static Sprite BuildSprite(int size, Shape shape)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(shape(u, v))));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Rounded rectangle (feather = 0) or soft glow (feather > 0), 9-sliced.</summary>
        static Sprite BuildSliced(int s, float radius, float feather)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float inset = feather;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float r = radius - inset * 0.5f;
                    float lo = inset * 0.5f + r, hi = s - inset * 0.5f - r;
                    float dx = Mathf.Max(0f, Mathf.Max(lo - x - 0.5f, x + 0.5f - hi));
                    float dy = Mathf.Max(0f, Mathf.Max(lo - y - 0.5f, y + 0.5f - hi));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) - r;
                    float a = feather > 0f ? Mathf.Pow(Mathf.Clamp01(1f - (dist + feather * 0.5f) / feather), 2f)
                                           : Mathf.Clamp01(0.5f - dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            float b = radius + 2f;
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
