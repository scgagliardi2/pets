using System.Collections.Generic;
using System.Text;
using Pets.Gameplay;
using Pets.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// Battle-result panel: a static lineup of both teams going into the fight, the outcome, and
    /// a plain multiline dump of the battle log (no animation/replay yet — placeholder-art
    /// phase, PLAN.md §8).
    /// </summary>
    public sealed class BattleResultUI : MonoBehaviour
    {
        // A single UI Text component can't hold arbitrarily long content — its mesh generator
        // hits Unity's 65,000-vertex-per-mesh limit and throws around ~16k characters. The
        // simulator allows battles up to 10,000 events (battle-sim-spec.md §6.4's safety cap),
        // so a long/chaotic battle's full log can exceed that. This is a rough debug view, not a
        // replay, so truncating is the right trade-off rather than a scroll view for this phase.
        private const int MaxLogLines = 150;

        [SerializeField] private RunController runController;
        [SerializeField] private GameObject panel;
        [SerializeField] private FightLineupUI lineupUI;
        [SerializeField] private Text outcomeText;
        [SerializeField] private Text logText;
        [SerializeField] private Button continueButton;

        private void Awake()
        {
            panel.SetActive(false);
            continueButton.onClick.AddListener(() => panel.SetActive(false));
        }

        private void OnEnable()
        {
            runController.OnBattleResolved += Show;
        }

        private void OnDisable()
        {
            runController.OnBattleResolved -= Show;
        }

        private void Show(List<FightCardInfo> playerTeam, List<FightCardInfo> enemyTeam, BattleLog log, bool won)
        {
            panel.SetActive(true);
            lineupUI.Show(playerTeam, enemyTeam);
            outcomeText.text = won ? "Victory!" : log.Outcome == BattleOutcome.Draw ? "Draw" : "Defeat";

            var sb = new StringBuilder();
            int shown = Mathf.Min(log.Events.Count, MaxLogLines);
            for (int i = 0; i < shown; i++)
            {
                sb.AppendLine(log.Events[i].ToString());
            }
            if (log.Events.Count > shown)
            {
                sb.AppendLine($"... ({log.Events.Count - shown} more events not shown)");
            }
            logText.text = sb.ToString();
        }
    }
}
