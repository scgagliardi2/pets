using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Pets.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Pets.UI
{
    /// <summary>
    /// Run history/stats content, nested under HomeScreenUI's HomePanel. Reads
    /// SaveSystem.LoadHistory() directly — this is a read-only view, no RunController
    /// involvement needed.
    /// </summary>
    public sealed class StatsScreenUI : MonoBehaviour
    {
        private const int MaxEntriesShown = 15;

        [SerializeField] private GameObject homeContent;
        [SerializeField] private Text summaryText;
        [SerializeField] private Text historyListText;
        [SerializeField] private Button backButton;

        private void Awake()
        {
            backButton.onClick.AddListener(() =>
            {
                gameObject.SetActive(false);
                homeContent.SetActive(true);
            });
        }

        public void Refresh()
        {
            var history = SaveSystem.LoadHistory();

            if (history.Count == 0)
            {
                summaryText.text = "No runs yet.";
                historyListText.text = "";
                return;
            }

            int wins = history.Count(e => e.Victory);
            int bestRound = history.Max(e => e.RoundReached);
            float winRate = 100f * wins / history.Count;
            summaryText.text = $"Runs: {history.Count}   Wins: {wins} ({winRate:F0}%)   Best round: {bestRound}";

            var sb = new StringBuilder();
            foreach (var entry in Enumerable.Reverse(history).Take(MaxEntriesShown))
            {
                string when = DateTime.TryParse(entry.CompletedAtUtc, null, DateTimeStyles.RoundtripKind, out var dt)
                    ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                    : entry.CompletedAtUtc;
                sb.AppendLine($"{when} — {(entry.Victory ? "Victory" : "Defeat")}, round {entry.RoundReached}");
            }
            historyListText.text = sb.ToString();
        }
    }
}
