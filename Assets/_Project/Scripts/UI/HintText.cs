using System;
using System.Collections.Generic;
using System.Text;
using InputPrompts;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace APX.UI
{
    /// <summary>Animated text effects, written as tags: &lt;wave&gt;, &lt;shake&gt;, &lt;pulse&gt;, &lt;rainbow&gt;.</summary>
    [Flags]
    public enum TextEffects
    {
        None = 0,
        Wave = 1,
        Shake = 2,
        Pulse = 4,
        Rainbow = 8,
    }

    /// <summary>
    /// Hint text for the companion's bubble. Understands, in any combination:
    /// <list type="bullet">
    /// <item><c>{token}</c>: the button icon for an action, for the controller in use (see <see cref="InputPromptIcons"/>).</item>
    /// <item>Rich text tags such as &lt;color&gt;, &lt;b&gt;, &lt;i&gt; and &lt;size&gt;, passed through to UI Toolkit.</item>
    /// <item>Animated effects: &lt;wave&gt;, &lt;shake&gt;, &lt;pulse&gt; and &lt;rainbow&gt; (applied by <see cref="ApplyEffects"/>).</item>
    /// </list>
    /// The text is kept as units (characters, icons and zero-width tags), so it can be revealed one character or
    /// icon at a time without ever cutting through markup; tags take no time to type. A "character" is a whole
    /// user-perceived character, so emoji (surrogate pairs, ❤️ with its variation selector, flags, skin tones,
    /// ZWJ sequences) are typed in one step and never split.
    /// </summary>
    public sealed class HintText
    {
        const string HiddenTag = "<alpha=#00>";
        const int MaxTagLength = 64;

        enum UnitKind
        {
            Character,
            Icon,
            Tag,
        }

        readonly struct Unit
        {
            public readonly UnitKind Kind;
            public readonly string Text; // The character (one or more UTF-16 code units), icon token or tag markup.
            public readonly TextEffects Effects;

            public Unit(UnitKind kind, string text, TextEffects effects)
            {
                Kind = kind;
                Text = text;
                Effects = effects;
            }
        }

        // One per UTF-16 code unit of the text as UI Toolkit lays it out (tags removed, each icon one code unit),
        // used to find which effects a drawn glyph belongs to.
        readonly struct ContentUnit
        {
            public readonly TextEffects Effects;
            public readonly bool IsIcon;
            public readonly bool Skip; // Never starts a glyph: whitespace, joiners, variation selectors.
            public readonly int Ordinal; // Which visible character it belongs to (staggers the animations).

            public ContentUnit(TextEffects effects, bool isIcon, bool skip, int ordinal)
            {
                Effects = effects;
                IsIcon = isIcon;
                Skip = skip;
                Ordinal = ordinal;
            }
        }

        [Serializable]
        public struct EffectSettings
        {
            [Tooltip("Height of the <wave> in pixels.")]
            public float waveHeight;
            public float waveSpeed;
            [Tooltip("How far <shake> jitters, in pixels.")]
            public float shakeAmount;
            [Tooltip("New <shake> positions per second.")]
            public float shakeRate;
            [Tooltip("How much <pulse> grows (0.15 = 15%).")]
            public float pulseAmount;
            public float pulseSpeed;
            [Tooltip("Colour cycles per second for <rainbow>.")]
            public float rainbowSpeed;

            public static EffectSettings Default => new()
            {
                waveHeight = 2.5f,
                waveSpeed = 7f,
                shakeAmount = 1.2f,
                shakeRate = 18f,
                pulseAmount = 0.15f,
                pulseSpeed = 6f,
                rainbowSpeed = 0.4f,
            };
        }

        static readonly HashSet<string> s_warnedTokens = new();

        readonly List<Unit> _units = new();
        readonly List<int> _countable = new(); // Indexes of the units that are typed: characters and icons.
        readonly List<ContentUnit> _content = new();
        readonly int[] _effectDepth = new int[EffectCount];
        readonly StringBuilder _builder = new();

        const int EffectCount = 4;

        /// <summary>Number of characters and icons (what typing counts); tags don't count.</summary>
        public int Length => _countable.Count;

        public bool HasEffects { get; private set; }

        public void SetText(string text)
        {
            _units.Clear();
            _countable.Clear();
            _content.Clear();
            Array.Clear(_effectDepth, 0, EffectCount);
            HasEffects = false;
            if (string.IsNullOrEmpty(text))
                return;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{' && TryReadToken(text, i, out string token, out int tokenEnd))
                {
                    Add(new Unit(UnitKind.Icon, token, CurrentEffects()));
                    i = tokenEnd;
                    continue;
                }

                if (c == '<' && TryReadTag(text, i, out string tag, out int tagEnd))
                {
                    // Effect tags nest by counting, so <wave>a<wave>b</wave>c</wave> keeps "c" waving.
                    if (TryReadEffectTag(tag, out int effect, out bool closing))
                        _effectDepth[effect] = Mathf.Max(0, _effectDepth[effect] + (closing ? -1 : 1));
                    else
                        _units.Add(new Unit(UnitKind.Tag, tag, CurrentEffects()));

                    i = tagEnd;
                    continue;
                }

                int length = ClusterLength(text, i);
                Add(new Unit(UnitKind.Character, text.Substring(i, length), CurrentEffects()));
                i += length - 1;
            }
        }

        TextEffects CurrentEffects()
        {
            TextEffects effects = TextEffects.None;
            for (int i = 0; i < EffectCount; i++)
            {
                if (_effectDepth[i] > 0)
                    effects |= (TextEffects)(1 << i);
            }

            return effects;
        }

        void Add(Unit unit)
        {
            int ordinal = _countable.Count;
            _countable.Add(_units.Count);
            _units.Add(unit);
            HasEffects |= unit.Effects != TextEffects.None;

            if (unit.Kind == UnitKind.Icon)
            {
                _content.Add(new ContentUnit(unit.Effects, true, false, ordinal));
                return;
            }

            foreach (char c in unit.Text)
                _content.Add(new ContentUnit(unit.Effects, false, IsSkippable(c), ordinal));
        }

        /// <summary>The character at typing position <paramref name="index"/>; '\0' for an icon or outside the text.</summary>
        public char CharAt(int index)
        {
            if (index < 0 || index >= _countable.Count)
                return '\0';

            Unit unit = _units[_countable[index]];
            return unit.Kind == UnitKind.Character ? unit.Text[0] : '\0';
        }

        /// <summary>
        /// Rich text showing the first <paramref name="visibleCount"/> characters and icons. The rest stays laid out
        /// but invisible, so a bubble keeps its final size while the text is typed out.
        /// </summary>
        public string Render(int visibleCount, ControlScheme scheme, InputPromptIcons icons)
        {
            _builder.Clear();
            bool hiding = false;
            int typed = 0;
            foreach (Unit unit in _units)
            {
                if (unit.Kind == UnitKind.Tag)
                {
                    _builder.Append(unit.Text);

                    // A colour tag (or its end) would bring the hidden text's alpha back, so hide again after it.
                    if (hiding)
                        _builder.Append(HiddenTag);
                    continue;
                }

                bool visible = typed < visibleCount;
                typed++;
                if (!visible && !hiding)
                {
                    _builder.Append(HiddenTag);
                    hiding = true;
                }

                if (unit.Kind == UnitKind.Character)
                {
                    _builder.Append(unit.Text);
                    continue;
                }

                AppendIcon(unit.Text, scheme, icons, visible);
                if (hiding)
                    _builder.Append(HiddenTag);
            }

            return _builder.ToString();
        }

        /// <summary>
        /// Moves and recolours the glyphs inside effect tags. Pass it the label's PostProcessTextVertices glyphs and
        /// repaint the label every frame while <see cref="HasEffects"/>.
        /// </summary>
        public void ApplyEffects(TextElement.GlyphsEnumerable glyphs, float time, in EffectSettings settings)
        {
            if (!HasEffects)
                return;

            // Walks the laid-out text alongside the glyphs. Each drawn glyph starts at the next code unit that can
            // start one, and covers as many code units as UI Toolkit says it does (2 for most emoji). Whitespace is
            // skipped on both sides, because UI Toolkit collapses runs of it and draws none at line wraps.
            int position = 0;
            foreach (TextElement.Glyph glyph in glyphs)
            {
                NativeSlice<Vertex> vertices = glyph.vertices;
                if (vertices.Length == 0 || IsEmpty(vertices))
                    continue; // Spaces.

                while (position < _content.Count && _content[position].Skip)
                    position++;
                if (position >= _content.Count)
                    break;

                ContentUnit content = _content[position];

                // A sprite that isn't one of our icons (e.g. a <sprite> tag written in the hint) has no content here.
                if (glyph.kind == TextElement.GlyphKind.Sprite && !content.IsIcon)
                    continue;

                if (content.Effects != TextEffects.None)
                    Apply(vertices, content.Effects, content.IsIcon, content.Ordinal, time, settings);
                position += Mathf.Max(1, glyph.textRange.length);
            }
        }

        /// <summary>
        /// Length in UTF-16 code units of the character starting at <paramref name="start"/>: a surrogate pair plus
        /// anything that attaches to it (variation selectors, combining marks, skin tones, emoji tags, a second
        /// flag letter, and further characters joined by a zero-width joiner).
        /// </summary>
        static int ClusterLength(string text, int start)
        {
            int i = start + CodePointLength(text, start);
            bool regionalIndicator = IsRegionalIndicator(CodePoint(text, start));
            while (i < text.Length)
            {
                int codePoint = CodePoint(text, i);
                if (codePoint == 0x200D && i + 1 < text.Length)
                {
                    // Zero-width joiner: takes the next character with it (👨‍👩‍👧).
                    i += 1;
                    i += CodePointLength(text, i);
                    continue;
                }

                bool attaches =
                    (codePoint >= 0xFE00 && codePoint <= 0xFE0F) || // Variation selectors (❤️).
                    (codePoint >= 0x1F3FB && codePoint <= 0x1F3FF) || // Skin tones.
                    (codePoint >= 0xE0020 && codePoint <= 0xE007F) || // Emoji tag sequences.
                    codePoint == 0x20E3 || // Keycap.
                    IsCombiningMark(text[i]) ||
                    (regionalIndicator && IsRegionalIndicator(codePoint)); // Second letter of a flag.
                if (!attaches)
                    break;

                regionalIndicator = false;
                i += CodePointLength(text, i);
            }

            return i - start;
        }

        static int CodePointLength(string text, int index) =>
            char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;

        static int CodePoint(string text, int index) =>
            CodePointLength(text, index) == 2 ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];

        static bool IsRegionalIndicator(int codePoint) => codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF;

        static bool IsCombiningMark(char c)
        {
            System.Globalization.UnicodeCategory category = char.GetUnicodeCategory(c);
            return category is System.Globalization.UnicodeCategory.NonSpacingMark
                or System.Globalization.UnicodeCategory.SpacingCombiningMark
                or System.Globalization.UnicodeCategory.EnclosingMark;
        }

        /// <summary>Code units that never start a drawn glyph: whitespace, joiners and variation selectors.</summary>
        static bool IsSkippable(char c) =>
            char.IsWhiteSpace(c) || c == '‍' || c == '‌' || c == '﻿' || (c >= '︀' && c <= '️');

        static void Apply(NativeSlice<Vertex> vertices, TextEffects effects, bool isIcon, int index, float time, in EffectSettings settings)
        {
            Vector3 offset = Vector3.zero;
            if ((effects & TextEffects.Wave) != 0)
                offset.y += Mathf.Sin(time * settings.waveSpeed + index * 0.55f) * settings.waveHeight;

            if ((effects & TextEffects.Shake) != 0)
            {
                int tick = Mathf.FloorToInt(time * settings.shakeRate);
                offset.x += (Hash(index, tick, 1) * 2f - 1f) * settings.shakeAmount;
                offset.y += (Hash(index, tick, 2) * 2f - 1f) * settings.shakeAmount;
            }

            float scale = 1f;
            if ((effects & TextEffects.Pulse) != 0)
                scale += Mathf.Sin(time * settings.pulseSpeed + index * 0.2f) * settings.pulseAmount;

            // Icons keep their own colour: they stand for specific buttons.
            bool rainbow = (effects & TextEffects.Rainbow) != 0 && !isIcon;
            Color32 rainbowColor = default;
            if (rainbow)
                rainbowColor = Color.HSVToRGB(Mathf.Repeat(time * settings.rainbowSpeed + index * 0.07f, 1f), 0.6f, 1f);

            Vector3 center = Vector3.zero;
            foreach (Vertex vertex in vertices)
                center += vertex.position;
            center /= vertices.Length;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vertex vertex = vertices[i];
                vertex.position = center + (vertex.position - center) * scale + offset;
                if (rainbow)
                {
                    // Keeps the alpha, so text that hasn't been typed yet stays hidden.
                    rainbowColor.a = vertex.tint.a;
                    vertex.tint = rainbowColor;
                }

                vertices[i] = vertex;
            }
        }

        static bool IsEmpty(NativeSlice<Vertex> vertices)
        {
            Vector3 first = vertices[0].position;
            for (int i = 1; i < vertices.Length; i++)
            {
                if ((vertices[i].position - first).sqrMagnitude > 0.0001f)
                    return false;
            }

            return true;
        }

        // Cheap, stable pseudo-random value in 0..1.
        static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        void AppendIcon(string token, ControlScheme scheme, InputPromptIcons icons, bool visible)
        {
            if (icons != null && icons.AppendRichText(_builder, token, scheme, visible))
                return;

            WarnUnknown(token);
            _builder.Append('[').Append(token).Append(']');
        }

        /// <summary>A token is a single word between braces, e.g. {jump}.</summary>
        static bool TryReadToken(string text, int start, out string token, out int end)
        {
            token = null;
            end = text.IndexOf('}', start + 1);
            if (end <= start + 1)
                return false;

            for (int i = start + 1; i < end; i++)
            {
                if (char.IsWhiteSpace(text[i]) || text[i] == '{')
                    return false;
            }

            token = text.Substring(start + 1, end - start - 1);
            return true;
        }

        /// <summary>A tag starts with a letter, '/' or '#' right after '&lt;' (so "3 &lt; 5" stays text).</summary>
        static bool TryReadTag(string text, int start, out string tag, out int end)
        {
            tag = null;
            end = text.IndexOf('>', start + 1);
            if (end <= start + 1 || end - start > MaxTagLength)
                return false;

            char first = text[start + 1];
            if (!char.IsLetter(first) && first != '/' && first != '#')
                return false;

            for (int i = start + 1; i < end; i++)
            {
                if (text[i] == '<' || text[i] == '\n')
                    return false;
            }

            tag = text.Substring(start, end - start + 1);
            return true;
        }

        /// <param name="effect">Bit index of the effect in <see cref="TextEffects"/>.</param>
        static bool TryReadEffectTag(string tag, out int effect, out bool closing)
        {
            closing = tag.Length > 1 && tag[1] == '/';
            string name = tag.Substring(closing ? 2 : 1, tag.Length - (closing ? 3 : 2)).Trim().ToLowerInvariant();
            effect = name switch
            {
                "wave" => 0,
                "shake" => 1,
                "pulse" => 2,
                "rainbow" => 3,
                _ => -1,
            };
            return effect >= 0;
        }

        static void WarnUnknown(string token)
        {
            if (s_warnedTokens.Add(token))
                Debug.LogWarning($"Input prompt {{{token}}} has no icon in the InputPromptIcons asset; showing [{token}] instead.");
        }
    }
}
