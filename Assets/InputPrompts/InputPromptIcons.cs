using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore.Text;

[assembly: InternalsVisibleTo("InputPrompts.Editor")]

namespace InputPrompts
{
    /// <summary>
    /// Button icons for actions mentioned in UI Toolkit text as <c>{token}</c> (e.g. "Press {jump}"), one per kind of
    /// controller (see <see cref="ControlSchemeTracker"/>). An icon with a sprite is drawn inline in the text; one
    /// without shows as a coloured square.
    /// <para>UI Toolkit text can only draw images from a text sprite asset, so the Editor builds one from the sprites
    /// here and keeps it up to date (in a Resources/Sprite Assets folder next to this asset, where rich text finds it).
    /// The label needs rich text enabled.</para>
    /// <para>Self-contained: copy the InputPrompts folder into any Unity 6 project that uses UI Toolkit and the
    /// Input System.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Input Prompts/Input Prompt Icons", fileName = "InputPromptIcons")]
    public sealed class InputPromptIcons : ScriptableObject
    {
        const char Square = '■';

        [Serializable]
        public struct Icon
        {
            [Tooltip("The button's name (e.g. \"Space\", \"A\"). For reference.")]
            public string label;
            [Tooltip("The button's image, drawn inline in the text.")]
            public Sprite sprite;
            [Tooltip("This icon's size, on top of Icon Size (e.g. to make a wide key a little smaller).")]
            [Range(0.5f, 2f)] public float size;
            [Tooltip("Colour of the square shown while there's no sprite.")]
            public Color color;

            /// <summary>The icon's own size; 1 if it was never set.</summary>
            public float Size => size > 0f ? size : 1f;
        }

        [Serializable]
        public struct Prompt
        {
            [Tooltip("Written in text between braces, e.g. jump for {jump}. Not case-sensitive.")]
            public string token;
            public Icon keyboardMouse;
            public Icon gamepad;
        }

        [SerializeField] Prompt[] prompts = Array.Empty<Prompt>();
        [Tooltip("Icon height relative to the capital letters of the text around it.")]
        [SerializeField, Range(0.5f, 3f)] float iconSize = 1.75f;
        [Tooltip("Built automatically from the sprites above; text draws the icons from it.")]
        [SerializeField] SpriteAsset textSprites;

        [NonSerialized] Dictionary<(Sprite, int), string> _spriteNames;
        [NonSerialized] StringBuilder _builder;

        public IReadOnlyList<Prompt> Prompts => prompts;

        internal SpriteAsset TextSprites => textSprites;

        /// <summary>How big text draws the icon, relative to a capital letter.</summary>
        internal float ScaleOf(Icon icon) => iconSize * icon.Size;

        /// <summary>Scales that round to the same key share a text sprite.</summary>
        internal static int ScaleKey(float scale) => Mathf.RoundToInt(scale * 1000f);

        public bool TryGetIcon(string token, ControlScheme scheme, out Icon icon)
        {
            foreach (Prompt prompt in prompts)
            {
                if (string.Equals(prompt.token, token, StringComparison.OrdinalIgnoreCase))
                {
                    icon = scheme == ControlScheme.Gamepad ? prompt.gamepad : prompt.keyboardMouse;
                    return true;
                }
            }

            icon = default;
            return false;
        }

        /// <summary>Appends the rich text that draws the icon for <paramref name="token"/>; false if there's no such prompt.</summary>
        /// <param name="visible">False lays the icon out but leaves it invisible (e.g. for text that is being typed out).</param>
        public bool AppendRichText(StringBuilder builder, string token, ControlScheme scheme, bool visible = true)
        {
            if (!TryGetIcon(token, scheme, out Icon icon))
                return false;

            string alpha = visible ? "FF" : "00";
            float scale = ScaleOf(icon);
            string spriteName = SpriteNameOf(icon.sprite, scale);
            if (spriteName != null)
            {
                // <size> doesn't scale text sprites (their size is built into the sprite asset, see ScaleOf), but
                // it does make the line tall enough for them; sprites alone don't.
                builder.Append("<size=").Append(Mathf.Max(100, Mathf.RoundToInt(scale * 100f))).Append("%><sprite=\"")
                    .Append(textSprites.name).Append("\" name=\"").Append(spriteName)
                    .Append("\" color=#FFFFFF").Append(alpha).Append("></size>");
                return true;
            }

            // The square glyph is smaller than a capital letter, hence the extra size.
            builder.Append("<size=").Append(Mathf.RoundToInt(scale * 130f)).Append("%><color=#")
                .Append(ColorUtility.ToHtmlStringRGB(icon.color)).Append(alpha).Append('>')
                .Append(Square).Append("</color></size>");
            return true;
        }

        /// <summary>Replaces each {token} in <paramref name="text"/> with its icon; unknown tokens are left as written.</summary>
        public string Format(string text, ControlScheme scheme)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
                return text;

            _builder ??= new StringBuilder();
            _builder.Clear();
            int index = 0;
            while (index < text.Length)
            {
                int open = text.IndexOf('{', index);
                int close = open < 0 ? -1 : text.IndexOf('}', open + 1);
                if (close < 0)
                {
                    _builder.Append(text, index, text.Length - index);
                    break;
                }

                _builder.Append(text, index, open - index);
                string token = text.Substring(open + 1, close - open - 1);
                if (!AppendRichText(_builder, token, scheme))
                    _builder.Append(text, open, close - open + 1);
                index = close + 1;
            }

            return _builder.ToString();
        }

        /// <summary>Forgets which text sprite draws which sprite, after the text sprites were rebuilt.</summary>
        internal void ClearSpriteNames() => _spriteNames = null;

        // The text sprite that draws this sprite at this scale; null until the Editor has built it.
        string SpriteNameOf(Sprite sprite, float scale)
        {
            if (sprite == null || textSprites == null)
                return null;

            if (_spriteNames == null)
            {
                _spriteNames = new Dictionary<(Sprite, int), string>();
                foreach (SpriteGlyph glyph in textSprites.spriteGlyphTable)
                {
                    if (glyph.sprite == null)
                        continue;

                    foreach (SpriteCharacter character in textSprites.spriteCharacterTable)
                    {
                        if (character.glyphIndex == glyph.index)
                        {
                            _spriteNames.TryAdd((glyph.sprite, ScaleKey(glyph.scale)), character.name);
                            break;
                        }
                    }
                }
            }

            return _spriteNames.TryGetValue((sprite, ScaleKey(scale)), out string name) ? name : null;
        }

#if UNITY_EDITOR
        /// <summary>Editor only: raised when the icons are edited, so their text sprites can be rebuilt.</summary>
        internal static event Action<InputPromptIcons> EditorChanged;

        void OnValidate()
        {
            // Icons from before the size slider existed load with a size of 0.
            for (int i = 0; i < prompts.Length; i++)
            {
                prompts[i].keyboardMouse.size = prompts[i].keyboardMouse.Size;
                prompts[i].gamepad.size = prompts[i].gamepad.Size;
            }

            _spriteNames = null;
            EditorChanged?.Invoke(this);
        }
#endif
    }
}
