using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.Text;
using Object = UnityEngine.Object;

namespace InputPrompts.Editor
{
    /// <summary>
    /// Builds the text sprite asset that UI Toolkit draws an <see cref="InputPromptIcons"/> asset's sprites from, and
    /// rebuilds it whenever the icons or their textures change. Text draws each sprite asset from a single texture, so
    /// the sprites are copied (trimmed of transparent borders, so they all come out the same height) into one atlas
    /// inside the sprite asset. It's kept in a Resources/Sprite Assets folder next to the icons, where UI Toolkit's
    /// text settings look sprite assets up by name.
    /// </summary>
    static class InputPromptSpriteBuilder
    {
        const string SpriteAssetFolder = "Resources/Sprite Assets";
        const string SpriteShader = "Hidden/TextCore/Sprite";
        const int AtlasPadding = 2;
        const int MaxAtlasSize = 4096;
        const byte AlphaThreshold = 8;

        // Text draws each icon about as tall as a capital letter, times its size (see InputPromptIcons.ScaleOf). These
        // centre it on the text whatever its size, with a little room on either side.
        const float CentreAboveBaseline = 0.3f; // Height of the icon's centre above the baseline, relative to an unscaled icon.
        const float SideMargin = 0.06f;         // Space on each side, relative to the icon's height.

        static readonly Dictionary<InputPromptIcons, bool> s_pending = new();

        [InitializeOnLoadMethod]
        static void Initialize() => InputPromptIcons.EditorChanged += icons => Schedule(icons, false);

        /// <summary>Rebuilds the icons' text sprites after this editor update: always if <paramref name="force"/>
        /// (e.g. a texture's pixels changed), otherwise only if the sprites in use changed.</summary>
        internal static void Schedule(InputPromptIcons icons, bool force)
        {
            if (icons == null || !EditorUtility.IsPersistent(icons))
                return;

            if (s_pending.Count == 0)
                EditorApplication.delayCall += BuildPending;
            s_pending[icons] = force || (s_pending.TryGetValue(icons, out bool forced) && forced);
        }

        static void BuildPending()
        {
            var pending = new List<KeyValuePair<InputPromptIcons, bool>>(s_pending);
            s_pending.Clear();
            foreach ((InputPromptIcons icons, bool force) in pending)
            {
                if (icons != null)
                    Build(icons, force);
            }
        }

        [MenuItem("CONTEXT/InputPromptIcons/Rebuild Text Sprites")]
        static void Rebuild(MenuCommand command) => Build((InputPromptIcons)command.context, true);

        static void Build(InputPromptIcons icons, bool force)
        {
            // Each sprite goes into the atlas once, and gets a glyph for every size it's used at.
            var sprites = new List<Sprite>();
            var glyphs = new List<(Sprite sprite, float scale)>();
            foreach (InputPromptIcons.Prompt prompt in icons.Prompts)
            {
                AddIcon(prompt.keyboardMouse);
                AddIcon(prompt.gamepad);
            }

            // Without sprites every icon is a square, so there's nothing to build.
            if (sprites.Count == 0)
                return;

            SpriteAsset spriteAsset = icons.TextSprites;
            if (!force && spriteAsset != null && IsUpToDate(spriteAsset, glyphs))
                return;

            string path = spriteAsset != null ? AssetDatabase.GetAssetPath(spriteAsset) : null;
            if (string.IsNullOrEmpty(path))
            {
                string folder = $"{Path.GetDirectoryName(AssetDatabase.GetAssetPath(icons))?.Replace('\\', '/')}/{SpriteAssetFolder}";
                CreateFolder(folder);
                path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{icons.name.Replace(' ', '_')}_Sprites.asset");
                spriteAsset = ScriptableObject.CreateInstance<SpriteAsset>();
                AssetDatabase.CreateAsset(spriteAsset, path);
            }
            else if (!Path.GetDirectoryName(path).Replace('\\', '/').EndsWith("/" + SpriteAssetFolder))
            {
                Debug.LogWarning($"{path} must sit directly in a {SpriteAssetFolder} folder for text to find it; " +
                    $"the icons of {icons.name} will show as squares.", spriteAsset);
            }

            spriteAsset.name = Path.GetFileNameWithoutExtension(path);

            // Pack the sprites into one texture, reusing the old one so anything holding on to it stays valid.
            var pieces = new Texture2D[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
                pieces[i] = CopyTrimmed(sprites[i]);

            var atlas = spriteAsset.spriteSheet as Texture2D;
            if (atlas == null || !atlas.isReadable || AssetDatabase.GetAssetPath(atlas) != path)
            {
                atlas = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                AssetDatabase.AddObjectToAsset(atlas, spriteAsset);
            }

            atlas.name = $"{spriteAsset.name} Atlas";
            Rect[] uvRects = atlas.PackTextures(pieces, AtlasPadding, MaxAtlasSize, false);
            atlas.filterMode = sprites[0].texture.filterMode;
            atlas.wrapMode = TextureWrapMode.Clamp;
            atlas.Apply(false, false);

            var glyphRects = new Dictionary<Sprite, GlyphRect>();
            for (int i = 0; i < sprites.Count; i++)
            {
                glyphRects[sprites[i]] = new GlyphRect(Mathf.RoundToInt(uvRects[i].x * atlas.width), Mathf.RoundToInt(uvRects[i].y * atlas.height),
                    pieces[i].width, pieces[i].height);
                Object.DestroyImmediate(pieces[i]);
            }

            if (!Fill(spriteAsset, atlas, glyphs, glyphRects))
                return;

            // Drop anything else in the file (e.g. sprite assets and materials left from an older build).
            spriteAsset.fallbackSpriteAssets = new List<SpriteAsset>();
            var keep = new HashSet<Object> { spriteAsset, atlas, spriteAsset.material };
            foreach (Object subAsset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (subAsset != null && !keep.Contains(subAsset))
                    Object.DestroyImmediate(subAsset, true);
            }

            EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssetIfDirty(spriteAsset);

            if (icons.TextSprites != spriteAsset)
            {
                var serializedIcons = new SerializedObject(icons);
                serializedIcons.FindProperty("textSprites").objectReferenceValue = spriteAsset;
                serializedIcons.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(icons);
            }

            icons.ClearSpriteNames();

            void AddIcon(InputPromptIcons.Icon icon)
            {
                Sprite sprite = icon.sprite;
                if (sprite == null || sprite.texture == null)
                    return;

                if (!sprites.Contains(sprite))
                    sprites.Add(sprite);

                float scale = icons.ScaleOf(icon);
                int key = InputPromptIcons.ScaleKey(scale);
                if (!glyphs.Exists(glyph => glyph.sprite == sprite && InputPromptIcons.ScaleKey(glyph.scale) == key))
                    glyphs.Add((sprite, scale));
            }
        }

        static bool Fill(SpriteAsset asset, Texture2D atlas, List<(Sprite sprite, float scale)> glyphs, Dictionary<Sprite, GlyphRect> glyphRects)
        {
            // The texture and version have no public setters.
            var serialized = new SerializedObject(asset);
            SerializedProperty version = serialized.FindProperty("m_Version");
            SerializedProperty sheet = serialized.FindProperty("m_SpriteAtlasTexture");
            if (version == null || sheet == null)
            {
                Debug.LogError("Can't build input prompt sprites: this Unity version stores text sprite assets differently.");
                return false;
            }

            version.stringValue = "1.1.0";
            sheet.objectReferenceValue = atlas;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            asset.spriteGlyphTable.Clear();
            asset.spriteCharacterTable.Clear();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < glyphs.Count; i++)
            {
                (Sprite sprite, float scale) = glyphs[i];
                GlyphRect rect = glyphRects[sprite];
                float margin = rect.height * SideMargin;
                float aboveBaseline = rect.height * (0.5f + CentreAboveBaseline / scale);
                var metrics = new GlyphMetrics(rect.width, rect.height, margin, aboveBaseline, rect.width + margin * 2f);
                var glyph = new SpriteGlyph((uint)i, metrics, rect, scale, 0, sprite);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new SpriteCharacter(0xFFFE, asset, glyph)
                {
                    name = UniqueName(sprite.name, usedNames),
                    glyphIndex = glyph.index,
                    scale = 1f,
                });
            }

            Material material = asset.material;
            if (material == null || AssetDatabase.GetAssetPath(material) != AssetDatabase.GetAssetPath(asset))
            {
                Shader shader = Shader.Find(SpriteShader);
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");

                material = new Material(shader) { hideFlags = HideFlags.HideInHierarchy };
                AssetDatabase.AddObjectToAsset(material, asset);
                asset.material = material;
            }

            material.name = $"{asset.name} Material";
            material.mainTexture = atlas;
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(material);
            return true;
        }

        static bool IsUpToDate(SpriteAsset asset, List<(Sprite sprite, float scale)> glyphs)
        {
            if (asset.spriteSheet == null || asset.material == null || (asset.fallbackSpriteAssets?.Count ?? 0) > 0 ||
                asset.spriteGlyphTable.Count != glyphs.Count || asset.spriteCharacterTable.Count != glyphs.Count)
            {
                return false;
            }

            for (int i = 0; i < glyphs.Count; i++)
            {
                SpriteGlyph glyph = asset.spriteGlyphTable[i];
                if (glyph.sprite != glyphs[i].sprite || InputPromptIcons.ScaleKey(glyph.scale) != InputPromptIcons.ScaleKey(glyphs[i].scale))
                    return false;
            }

            return true;
        }

        /// <summary>A readable copy of the sprite's pixels without its transparent borders (whatever the texture's
        /// import settings).</summary>
        static Texture2D CopyTrimmed(Sprite sprite)
        {
            Texture2D source = sprite.texture;
            Rect rect = sprite.textureRect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));

            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture active = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);

            Color32[] pixels = copy.GetPixels32();
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    if (pixels[row * width + column].a <= AlphaThreshold)
                        continue;

                    minX = Mathf.Min(minX, column);
                    maxX = Mathf.Max(maxX, column);
                    minY = Mathf.Min(minY, row);
                    maxY = Mathf.Max(maxY, row);
                }
            }

            // Fully transparent or nothing to trim: keep it as it is.
            if (maxX < 0 || (maxX - minX + 1 == width && maxY - minY + 1 == height))
            {
                copy.Apply(false, false);
                return copy;
            }

            var trimmed = new Texture2D(maxX - minX + 1, maxY - minY + 1, TextureFormat.RGBA32, false);
            trimmed.SetPixels(copy.GetPixels(minX, minY, trimmed.width, trimmed.height));
            trimmed.Apply(false, false);
            Object.DestroyImmediate(copy);
            return trimmed;
        }

        // Rich text looks sprites up by name (ignoring case), so names must be unique.
        static string UniqueName(string name, HashSet<string> usedNames)
        {
            string unique = name;
            for (int i = 2; !usedNames.Add(unique); i++)
                unique = $"{name}_{i}";
            return unique;
        }

        static void CreateFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    /// <summary>Rebuilds input prompt text sprites when a texture they use is re-imported (edited or re-sliced) or deleted.</summary>
    sealed class InputPromptTexturePostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var textures = new HashSet<string>();
            foreach (string path in importedAssets)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D))
                    textures.Add(path);
            }

            if (textures.Count == 0 && deletedAssets.Length == 0)
                return;

            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(InputPromptIcons)))
            {
                var icons = AssetDatabase.LoadAssetAtPath<InputPromptIcons>(AssetDatabase.GUIDToAssetPath(guid));
                if (icons == null)
                    continue;

                if (UsesAny(icons, textures))
                    InputPromptSpriteBuilder.Schedule(icons, true);
                else if (deletedAssets.Length > 0)
                    InputPromptSpriteBuilder.Schedule(icons, false);
            }
        }

        static bool UsesAny(InputPromptIcons icons, HashSet<string> texturePaths)
        {
            foreach (InputPromptIcons.Prompt prompt in icons.Prompts)
            {
                if (Uses(prompt.keyboardMouse.sprite) || Uses(prompt.gamepad.sprite))
                    return true;
            }

            return false;

            bool Uses(Sprite sprite) => sprite != null && texturePaths.Contains(AssetDatabase.GetAssetPath(sprite));
        }
    }
}
