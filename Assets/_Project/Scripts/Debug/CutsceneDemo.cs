using System.Collections;
using APX.Cutscenes;
using UnityEngine;

namespace APX.DebugTools
{
    /// <summary>
    /// Example cutscene trying out the cutscene steps: the companion talks (button icons, text effects, typing that
    /// jump or attack skips), a second character answers, and a quick-time event slows time down until jump is
    /// pressed. Put it on an object in a room with a trigger (e.g. On Room Enter, or On Player Touch with a trigger
    /// collider).
    /// </summary>
    public sealed class CutsceneDemo : Cutscene
    {
        [Tooltip("Optional second character who answers the companion.")]
        [SerializeField] Transform otherSpeaker;
        [SerializeField, Min(1f)] float zoom = 1.8f;
        [SerializeField, Min(0f)] float hopSpeed = 12f;

        protected override IEnumerator Perform(CutsceneContext c)
        {
            c.Companion.Take();
            yield return c.Camera.FocusOn(c.Player.Transform, zoom);

            yield return c.Companion.Say(
                "Ei! Isto é um teste de cutscene.",
                "Aperte {jump} ou {attack} para mostrar o texto todo de uma vez...",
                "...e aperte de novo para <wave>continuar</wave>!");

            if (otherSpeaker != null)
                yield return c.Say(otherSpeaker, "E eu sou outro personagem falando!");

            yield return c.Companion.Say("Agora um evento rápido: aperte {jump} quando o botão aparecer!");
            yield return c.Wait(0.4f);
            yield return c.QuickTimeEvent(CutsceneButton.Jump);
            c.Player.Hop(hopSpeed);
            yield return c.Player.WaitForLanding();

            yield return c.Companion.Say("<pulse>Boa!</pulse> Fim do teste.");
            c.Companion.Release(summon: true);
            yield return c.Camera.Release(0.6f);
        }
    }
}
