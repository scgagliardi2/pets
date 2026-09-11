using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Gameplay
{
    /// <summary>PvE Clash screen (design doc §5.1): runs the current line-up against a Forest
    /// wild encounter, autoplays the Step log, then offers the stubbed "pick 1 from defeated"
    /// catch (design doc §12.1 is the real drag-a-Pokéball version, Phase 1).</summary>
    public sealed class PvEClashController : MonoBehaviour
    {
        [SerializeField] private Text logText;
        [SerializeField] private Text outcomeText;
        [SerializeField] private Transform catchButtonsContainer;
        [SerializeField] private Button continueButton;
        [SerializeField] private LocationFlowController flow;
        [SerializeField] private float secondsPerStep = 0.15f;
        [SerializeField] private int maxLogLines = 40;

        private RunState state;
        private PokemonSpeciesLibrary library;
        private List<PokemonInstance> wildSnapshot;
        private bool lastOutcomeWon;

        public void Begin(RunState runState, PokemonSpeciesLibrary speciesLibrary)
        {
            state = runState;
            library = speciesLibrary;

            logText.text = string.Empty;
            outcomeText.text = string.Empty;
            continueButton.gameObject.SetActive(false);
            ClearCatchButtons();

            var playerLineUp = state.LineUp.Select(PokemonInstanceFactory.ResetForBattle).ToList();
            if (state.NextBattleAttackBonusPercent > 0f)
            {
                foreach (var mon in playerLineUp)
                {
                    mon.CurrentStats.Attack = Mathf.RoundToInt(mon.CurrentStats.Attack * (1f + state.NextBattleAttackBonusPercent));
                }
                state.NextBattleAttackBonusPercent = 0f;
            }

            int seed = state.RunSeed + state.CurrentNodeIndex;
            var wildLineUp = EncounterGenerator.GenerateWildLineUp(
                library, ForestLocationFactory.TypeBias, seed, $"wild-{state.CurrentNodeIndex}");
            wildSnapshot = new List<PokemonInstance>(wildLineUp);

            var log = PrecomputedStepLogRunner.Run(playerLineUp, wildLineUp, seed);
            StartCoroutine(PlayLog(log));
        }

        private IEnumerator PlayLog(StepLog log)
        {
            var lines = new List<string>();
            int step = -1;
            foreach (var evt in log.Events)
            {
                if (evt.Kind == StepEventKind.BattleEnd)
                {
                    continue;
                }
                if (evt.Step != step)
                {
                    step = evt.Step;
                    yield return new WaitForSeconds(secondsPerStep);
                }
                lines.Add(Describe(evt));
                if (lines.Count > maxLogLines)
                {
                    lines.RemoveAt(0);
                }
                logText.text = string.Join("\n", lines);
            }

            Resolve(log.Outcome);
        }

        private static string Describe(StepEvent evt)
        {
            switch (evt.Kind)
            {
                case StepEventKind.Damage: return $"{evt.SourceInstanceId} hits {evt.TargetInstanceId} for {evt.Amount}";
                case StepEventKind.Faint: return $"{evt.TargetInstanceId} fainted!";
                case StepEventKind.PassiveTriggered: return $"{evt.SourceInstanceId}'s passive triggers!";
                case StepEventKind.Promotion: return $"{evt.TargetInstanceId} steps up to Lead";
                case StepEventKind.StatusApplied: return $"{evt.TargetInstanceId} is {evt.Status}!";
                default: return evt.ToString();
            }
        }

        private void Resolve(BattleOutcome outcome)
        {
            lastOutcomeWon = outcome == BattleOutcome.SideAWins;
            outcomeText.text = lastOutcomeWon ? "Victory!" : outcome == BattleOutcome.Draw ? "Draw." : "Defeat...";

            if (lastOutcomeWon)
            {
                foreach (var defeated in CatchResolver.GetDefeated(wildSnapshot))
                {
                    CreateCatchButton(defeated);
                }
            }
            else
            {
                state.Morale--;
            }

            continueButton.gameObject.SetActive(true);
        }

        private void CreateCatchButton(PokemonInstance defeated)
        {
            var species = library.GetById(defeated.SpeciesId);
            var go = new GameObject($"Catch_{species?.DisplayName}", typeof(RectTransform));
            go.transform.SetParent(catchButtonsContainer, false);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.black;
            text.text = $"Catch {species?.DisplayName}";
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 32);

            var button = go.AddComponent<Button>();
            button.targetGraphic = text;
            button.onClick.AddListener(() =>
            {
                CatchResolver.Catch(state, defeated, library);
                Destroy(go);
            });
        }

        private void ClearCatchButtons()
        {
            for (int i = catchButtonsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(catchButtonsContainer.GetChild(i).gameObject);
            }
        }

        public void OnContinueClicked()
        {
            flow.OnPvEResolved(lastOutcomeWon);
        }
    }
}
