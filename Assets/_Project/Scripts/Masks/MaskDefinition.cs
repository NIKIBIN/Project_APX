using UnityEngine;

namespace APX.Masks
{
    /// <summary>Presentation data for a mask (or for the "no mask" option), used by the UI.</summary>
    [CreateAssetMenu(menuName = "APX/Mask Definition", fileName = "Mask_")]
    public sealed class MaskDefinition : ScriptableObject
    {
        [SerializeField] MaskType type = MaskType.NiceAndSlow;
        [SerializeField] string displayName = "Mask";
        [Tooltip("What wearing this option does, shown in the radial menu.")]
        [SerializeField, TextArea(2, 4)] string description;
        [SerializeField] Color color = Color.white;
        [SerializeField] Sprite icon;

        public MaskType Type => type;
        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public Sprite Icon => icon;
    }
}
