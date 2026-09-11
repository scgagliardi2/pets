using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;

namespace Pets.Gameplay
{
    /// <summary>Team Management tab (design doc §5.2). Phase 0 is read-only — reordering the
    /// Lead/Support line-up and Box management are Phase 1 (design doc §7).</summary>
    public sealed class TeamPanelController : MonoBehaviour
    {
        [SerializeField] private Text lineUpText;
        [SerializeField] private Text boxText;

        public void Refresh(RunState state, PokemonSpeciesLibrary library)
        {
            lineUpText.text = "Line-up:\n" + string.Join("\n", state.LineUp.Select((mon, i) =>
                $"{(i == 0 ? "Lead" : "Support")}: {library.GetById(mon.SpeciesId)?.DisplayName ?? mon.SpeciesId.ToString()} " +
                $"(Lv.{mon.Level}, {mon.CurrentStats.Attack}/{mon.CurrentStats.Health}/{mon.CurrentStats.Speed})"));

            boxText.text = state.Box.Count == 0
                ? "Box: (empty)"
                : "Box:\n" + string.Join("\n", state.Box.Select(mon =>
                    library.GetById(mon.SpeciesId)?.DisplayName ?? mon.SpeciesId.ToString()));
        }
    }
}
