using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>One creature card in the fight lineup. Instantiated at runtime by FightLineupUI,
    /// one per creature on each side. No buttons — this is a read-only display.</summary>
    public sealed class FightCardView : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Image background;

        public void Bind(FightCardInfo info)
        {
            nameText.text = $"{info.DisplayName} Lv{info.Level}";
            statsText.text = $"{info.Attack}/{info.Health}";
            background.color = info.Color;
        }
    }
}
