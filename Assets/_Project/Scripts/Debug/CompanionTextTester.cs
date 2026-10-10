using APX.Core;
using APX.Hints;
using APX.Masks;
using APX.Rooms;
using APX.UI;
using InputPrompts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace APX.DebugTools
{
    /// <summary>
    /// Helper for the companion text test scene: summons the companion right away and adds shortcuts to
    /// re-trigger the hint bubble and force the button icons, with an on-screen cheat sheet.
    /// F1 dismisses or summons the companion, F2 types the current hint again, F3 cycles the forced controller.
    /// </summary>
    public sealed class CompanionTextTester : MonoBehaviour
    {
        [Tooltip("Defaults to the MaskManager in the scene.")]
        [SerializeField] MaskManager maskManager;
        [Tooltip("Defaults to the HintBubbleView in the scene.")]
        [SerializeField] HintBubbleView bubble;
        [Tooltip("Defaults to the HintCompanion in the scene.")]
        [SerializeField] HintCompanion companion;
        [Tooltip("Mask put on to send the companion away (F1).")]
        [SerializeField] MaskType dismissMask = MaskType.SecondChannel;
        [SerializeField, Min(0f)] float summonDelay = 0.5f;
        [SerializeField] bool showHelp = true;

        GUIStyle _style;

        void Awake()
        {
            if (maskManager == null)
                maskManager = FindAnyObjectByType<MaskManager>();
            if (bubble == null)
                bubble = FindAnyObjectByType<HintBubbleView>();
            if (companion == null)
                companion = FindAnyObjectByType<HintCompanion>();
        }

        void Start()
        {
            if (maskManager != null)
                Invoke(nameof(Summon), summonDelay);
        }

        void OnDisable() => ControlSchemeTracker.Override = null;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                ToggleCompanion();
            if (keyboard.f2Key.wasPressedThisFrame && bubble != null)
                bubble.ReplayHint();
            if (keyboard.f3Key.wasPressedThisFrame)
                CycleControllerOverride();
        }

        void Summon() => maskManager.SummonCompanion();

        void ToggleCompanion()
        {
            if (maskManager == null || maskManager.IsTransitioning)
                return;

            if (MaskManager.CurrentMask == MaskType.None && MaskManager.IsCompanionSummoned)
                maskManager.Equip(dismissMask);
            else
                maskManager.UnequipAll();
        }

        static void CycleControllerOverride()
        {
            ControlSchemeTracker.Override = ControlSchemeTracker.Override switch
            {
                null => ControlScheme.Gamepad,
                ControlScheme.Gamepad => ControlScheme.KeyboardMouse,
                _ => null,
            };
        }

        void OnGUI()
        {
            if (!showHelp)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                richText = true,
                padding = new RectOffset(12, 12, 10, 10),
            };

            Room room = RoomManager.CurrentRoom;
            string forced = ControlSchemeTracker.Override.HasValue ? " <color=#FFCC55>(forçado)</color>" : " (automático)";
            string text =
                "<b>Teste de texto do companheiro</b>\n" +
                "Ande para a direita: cada sala testa uma coisa.\n" +
                "<b>F1</b>  dispensar / chamar o companheiro\n" +
                "<b>F2</b>  digitar a dica desta sala de novo\n" +
                "<b>F3</b>  forçar ícones: gamepad → teclado → automático\n" +
                "<b>Tab</b>  menu de máscaras (\"sem máscara\" chama o companheiro)\n\n" +
                $"Sala: {(room != null ? room.name : "-")}\n" +
                $"Controle: {ControlSchemeTracker.Current}{forced}\n" +
                $"Companheiro: {(companion != null && companion.IsSummoned ? "presente" : "ausente")}";

            GUI.Box(new Rect(Screen.width - 470, 16, 454, 230), text, _style);
        }
    }
}
