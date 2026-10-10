using System;
using APX.Core;
using UnityEngine;

namespace APX.UI
{
    /// <summary>
    /// Button icons for actions mentioned in text as <c>{token}</c> (e.g. "Press {jump}"), one per kind of
    /// controller. Until the real icons exist each one is a coloured square; give an icon a sprite name (from the
    /// panel's text sprite asset) to show that sprite instead.
    /// </summary>
    [CreateAssetMenu(menuName = "APX/Input Prompt Icons", fileName = "InputPromptIcons")]
    public sealed class InputPromptIcons : ScriptableObject
    {
        [Serializable]
        public struct Icon
        {
            [Tooltip("The button's name (e.g. \"Space\", \"A\"). For reference, and shown when the icon is missing.")]
            public string label;
            [Tooltip("Colour of the placeholder square.")]
            public Color color;
            [Tooltip("Optional: sprite name in the panel's text sprite asset, used instead of the square.")]
            public string spriteName;
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
    }
}
