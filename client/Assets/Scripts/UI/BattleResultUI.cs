using System.Text;
using Pets.Gameplay;
using Pets.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// Battle-result panel: outcome + a plain multiline dump of the battle log (no
    /// animation/replay yet — placeholder-art phase, PLAN.md §8).
    /// </summary>
    public sealed class BattleResultUI : MonoBehaviour
    {
        [SerializeField] private RunController runController;
        [SerializeField] private GameObject panel;
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

        private void Show(BattleLog log, bool won)
        {
            panel.SetActive(true);
            outcomeText.text = won ? "Victory!" : log.Outcome == BattleOutcome.Draw ? "Draw" : "Defeat";

            var sb = new StringBuilder();
            foreach (var e in log.Events)
            {
                sb.AppendLine(e.ToString());
            }
            logText.text = sb.ToString();
        }
    }
}
