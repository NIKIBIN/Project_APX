using System.Collections.Generic;
using System.Text;
using APX.Core;
using UnityEngine;

namespace APX.UI
{
    /// <summary>
    /// Text with input prompts: each <c>{token}</c> becomes the button icon for the current controller (see
    /// <see cref="InputPromptIcons"/>). The text is kept as units, one per character and one per icon, so it can be
    /// revealed a unit at a time without ever cutting through an icon's markup. Write plain text only: other rich
    /// text tags would count as characters.
    /// </summary>
    public sealed class InputPromptText
    {
        const string HiddenTag = "<alpha=#00>";
        const char Square = '■';

        readonly struct Unit
        {
            public readonly char Character;
            public readonly string Token;

            public Unit(char character)
            {
                Character = character;
                Token = null;
            }

            public Unit(string token)
            {
                Character = '\0';
                Token = token;
            }

            public bool IsIcon => Token != null;
        }

        static readonly HashSet<string> s_warnedTokens = new();

        readonly List<Unit> _units = new();
        readonly StringBuilder _builder = new();

        /// <summary>Number of units: characters plus icons.</summary>
        public int Length => _units.Count;

        public void SetText(string text)
        {
            _units.Clear();
            if (string.IsNullOrEmpty(text))
                return;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{' && TryReadToken(text, i, out string token, out int end))
                {
                    _units.Add(new Unit(token));
                    i = end;
                    continue;
                }

                _units.Add(new Unit(text[i]));
            }
        }

        /// <summary>The character at <paramref name="index"/>; '\0' for an icon or outside the text.</summary>
        public char CharAt(int index) => index >= 0 && index < _units.Count ? _units[index].Character : '\0';

        /// <summary>
        /// Rich text showing the first <paramref name="visibleCount"/> units. The rest stays laid out but invisible,
        /// so a bubble keeps its final size while the text is typed out.
        /// </summary>
        public string Render(int visibleCount, ControlScheme scheme, InputPromptIcons icons)
        {
            _builder.Clear();
            bool hiding = false;
            for (int i = 0; i < _units.Count; i++)
            {
                bool visible = i < visibleCount;
                if (!visible && !hiding)
                {
                    _builder.Append(HiddenTag);
                    hiding = true;
                }

                Unit unit = _units[i];
                if (!unit.IsIcon)
                {
                    _builder.Append(unit.Character);
                    continue;
                }

                AppendIcon(unit.Token, scheme, icons, visible);

                // The icon's own colour tag ends the hidden alpha, so hide again after it.
                if (hiding)
                    _builder.Append(HiddenTag);
            }

            return _builder.ToString();
        }

        void AppendIcon(string token, ControlScheme scheme, InputPromptIcons icons, bool visible)
        {
            if (icons == null || !icons.TryGetIcon(token, scheme, out InputPromptIcons.Icon icon))
            {
                WarnUnknown(token);
                _builder.Append('[').Append(token).Append(']');
                return;
            }

            string alpha = visible ? "FF" : "00";
            if (!string.IsNullOrEmpty(icon.spriteName))
            {
                _builder.Append("<sprite name=\"").Append(icon.spriteName).Append("\" color=#FFFFFF").Append(alpha).Append('>');
                return;
            }

            _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(icon.color)).Append(alpha)
                .Append("><size=125%>").Append(Square).Append("</size></color>");
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

        static void WarnUnknown(string token)
        {
            if (s_warnedTokens.Add(token))
                Debug.LogWarning($"Input prompt {{{token}}} has no icon in the InputPromptIcons asset; showing [{token}] instead.");
        }
    }
}
