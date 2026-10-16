using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OrionAR.Gen
{
    /// <summary>
    /// Creates the app's TextMeshPro font assets from the TTFs in Assets/OrionAR/Fonts.
    ///
    ///   Orbitron Bold     display titles - geometric, unmistakably "space"
    ///   Exo 2 SemiBold    buttons, labels, overlines
    ///   Exo 2 Regular     running text - Exo 2 keeps the technical feel but stays
    ///                     readable in paragraphs, which Orbitron does not
    ///   Material Icons    every icon in the UI
    ///
    /// All four are SIL OFL / Apache 2.0 and ship with their licence files.
    /// The assets are dynamic, so any character the content uses is added on demand,
    /// and each has fallbacks so a symbol missing from one face still renders.
    /// </summary>
    public static class FontBuilder
    {
        public const string Folder = "Assets/OrionAR/Fonts";

        public class Set
        {
            public TMP_FontAsset display, heading, body, bodyBold, icons;
        }

        const string Common =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`" +
            "abcdefghijklmnopqrstuvwxyz{|}~°±²³·×÷ΔΣπμ–—‘’“”…•−≈≤≥√→←↑↓";

        public static Set BuildAll()
        {
            AssetPaths.EnsureFolder(Folder);

            var set = new Set
            {
                display  = Make("Orbitron-700",  "OrionAR Display SDF", 90),
                heading  = Make("Exo2-600",      "OrionAR Heading SDF", 90),
                body     = Make("Exo2-400",      "OrionAR Body SDF", 90),
                bodyBold = Make("Exo2-700",      "OrionAR Body Bold SDF", 90),
                icons    = Make("MaterialIcons-Regular", "OrionAR Icons SDF", 72, icons: true),
            };

            if (set.body == null || set.heading == null)
            {
                Debug.LogWarning("[FontBuilder] Font files missing from " + Folder +
                                 " - falling back to the TextMeshPro default font.");
                return set;
            }

            var liberation = TMP_Settings.defaultFontAsset != null &&
                             !TMP_Settings.defaultFontAsset.name.StartsWith("OrionAR")
                ? TMP_Settings.defaultFontAsset
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            // Fallback chains: Orbitron has no Greek or maths symbols, Exo 2 has no
            // arrows; Liberation Sans covers both, and the icon font covers the icons.
            SetFallbacks(set.display, set.heading, set.icons, liberation);
            SetFallbacks(set.heading, set.icons, liberation);
            SetFallbacks(set.body, set.icons, liberation);
            SetFallbacks(set.bodyBold, set.icons, liberation);

            // Real bold faces instead of TextMeshPro's faux-bold dilation.
            SetBold(set.body, set.bodyBold);
            SetBold(set.heading, set.bodyBold);
            SetBold(set.display, set.display);

            UIKit.DisplayFont = set.display;
            UIKit.HeadingFont = set.heading;
            UIKit.BodyFont = set.body;
            UIKit.IconFont = set.icons;

            SetProjectDefault(set.body, set.icons, liberation);
            AssetDatabase.SaveAssets();
            return set;
        }

        static TMP_FontAsset Make(string fileBase, string assetName, int samplingSize, bool icons = false)
        {
            string ttf = Folder + "/" + fileBase + ".ttf";
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null)
            {
                Debug.LogWarning("[FontBuilder] Missing " + ttf);
                return null;
            }

            string path = Folder + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var fa = TMP_FontAsset.CreateFontAsset(font, samplingSize, 9, GlyphRenderMode.SDFAA,
                                                   1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Debug.LogWarning("[FontBuilder] Could not create a font asset from " + ttf);
                return null;
            }

            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);

            // The atlas texture and material are created in memory; they must be saved
            // inside the font asset or the font renders as solid blocks after a reload.
            if (fa.atlasTextures != null)
            {
                for (int i = 0; i < fa.atlasTextures.Length; i++)
                {
                    var tex = fa.atlasTextures[i];
                    if (tex == null) continue;
                    tex.name = assetName + " Atlas" + (i > 0 ? " " + i : "");
                    AssetDatabase.AddObjectToAsset(tex, fa);
                }
            }
            if (fa.material != null)
            {
                fa.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            // Pre-populate, so the Editor shows real text immediately and the first
            // frames on device do not stall rasterising glyphs.
            if (icons)
            {
                var chars = new List<uint>();
                foreach (var f in typeof(Icons).GetFields())
                {
                    var v = f.GetRawConstantValue() as string;
                    if (!string.IsNullOrEmpty(v)) chars.Add(v[0]);
                }
                fa.TryAddCharacters(chars.ToArray(), out _);
            }
            else
            {
                fa.TryAddCharacters(Common, out _);
            }

            EditorUtility.SetDirty(fa);
            return fa;
        }

        static void SetFallbacks(TMP_FontAsset fa, params TMP_FontAsset[] fallbacks)
        {
            if (fa == null) return;
            var list = new List<TMP_FontAsset>();
            foreach (var f in fallbacks) if (f != null && f != fa) list.Add(f);
            fa.fallbackFontAssetTable = list;
            EditorUtility.SetDirty(fa);
        }

        static void SetBold(TMP_FontAsset regular, TMP_FontAsset bold)
        {
            if (regular == null || bold == null || regular.fontWeightTable == null) return;
            if (regular.fontWeightTable.Length > 7)
            {
                regular.fontWeightTable[7].regularTypeface = bold;
                EditorUtility.SetDirty(regular);
            }
        }

        /// <summary>
        /// Makes Exo 2 the project default, so any text not built through UIKit (a TMP
        /// object added by hand later, say) still matches the rest of the app.
        /// </summary>
        static void SetProjectDefault(TMP_FontAsset body, TMP_FontAsset icons, TMP_FontAsset liberation)
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;

            var so = new SerializedObject(settings);
            var def = so.FindProperty("m_defaultFontAsset");
            if (def != null) def.objectReferenceValue = body;

            var fb = so.FindProperty("m_fallbackFontAssets");
            if (fb != null && fb.isArray)
            {
                fb.ClearArray();
                int i = 0;
                foreach (var f in new[] { icons, liberation })
                {
                    if (f == null) continue;
                    fb.InsertArrayElementAtIndex(i);
                    fb.GetArrayElementAtIndex(i).objectReferenceValue = f;
                    i++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
