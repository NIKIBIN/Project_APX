using UnityEngine;

namespace APX.Cutscenes
{
    /// <summary>
    /// Where cutscene speech bubbles point for a character: put it on (or under) the speaker, e.g. at the top of their
    /// head. Speakers without one get the bubble's default offset from their own position.
    /// </summary>
    public sealed class SpeechAnchor : MonoBehaviour
    {
        [Tooltip("World-space offset from this point to the tip of the bubble.")]
        [SerializeField] Vector2 offset = new(0f, 0.3f);

        /// <summary>The point (and offset, if any) bubbles over <paramref name="speaker"/> should use.</summary>
        internal static Transform Find(Component speaker, out Vector2? offset)
        {
            SpeechAnchor anchor = speaker.GetComponentInChildren<SpeechAnchor>();
            if (anchor != null)
            {
                offset = anchor.offset;
                return anchor.transform;
            }

            offset = null;
            return speaker.transform;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.75f);
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)offset);
            Gizmos.DrawWireSphere(transform.position + (Vector3)offset, 0.08f);
        }
    }
}
