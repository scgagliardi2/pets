using System;
using Pets.Data;
using Pets.Simulation;
using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>
    /// The one MonoBehaviour in Gameplay — orchestrates shop/battle/run-over transitions, delegates
    /// actual logic to ShopEconomy/BattleSimulator/SaveSystem, and fires events for UI to react to.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        [SerializeField] private CreatureLibrary creatureLibrary;
        [SerializeField] private BotRosterLibrary botRosterLibrary;
        [SerializeField] private ShopConfig shopConfig;

        public RunState State { get; private set; }
        public ShopConfig Config => shopConfig;

        public event Action OnStateChanged;
        public event Action<BattleLog, bool> OnBattleResolved;
        public event Action<bool> OnRunEnded;

        private void Start()
        {
            if (SaveSystem.TryLoad(creatureLibrary, out var loaded))
            {
                State = loaded;
            }
            else
            {
                State = new RunState();
                ShopEconomy.StartRun(State, shopConfig, creatureLibrary);
                SaveSystem.Save(State);
            }
            OnStateChanged?.Invoke();
        }

        public void Buy(int shopIndex)
        {
            if (ShopEconomy.Buy(State, shopConfig, shopIndex))
            {
                SaveSystem.Save(State);
                OnStateChanged?.Invoke();
            }
        }

        public void Sell(int boardIndex)
        {
            if (ShopEconomy.Sell(State, shopConfig, boardIndex))
            {
                SaveSystem.Save(State);
                OnStateChanged?.Invoke();
            }
        }

        public void Reroll()
        {
            if (ShopEconomy.Reroll(State, shopConfig, creatureLibrary))
            {
                SaveSystem.Save(State);
                OnStateChanged?.Invoke();
            }
        }

        public void ToggleFreeze(int shopIndex)
        {
            if (ShopEconomy.ToggleFreeze(State, shopIndex))
            {
                SaveSystem.Save(State);
                OnStateChanged?.Invoke();
            }
        }

        public void MoveBoardCreature(int index, int direction)
        {
            if (ShopEconomy.MoveBoardCreature(State, index, direction))
            {
                SaveSystem.Save(State);
                OnStateChanged?.Invoke();
            }
        }

        public void Fight()
        {
            if (State.Phase != GamePhase.Shop || State.Board.Count == 0)
            {
                return;
            }

            var botTeam = botRosterLibrary.GetByRound(State.Round);
            if (botTeam == null)
            {
                // No more scripted opponents left — the player survived the whole roster.
                EndRun(victory: true);
                return;
            }

            State.Phase = GamePhase.Battle;

            var playerSlots = State.Board.ConvertAll(c => (c.Definition, c.Level, c.BonusAttack, c.BonusHealth));
            var teamA = TeamStateConverter.ToTeamState(playerSlots, "player");
            var teamB = TeamStateConverter.ToTeamState(botTeam, "bot");

            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            var log = BattleSimulator.Run(teamA, teamB, seed);
            bool won = log.Outcome == BattleOutcome.TeamAWins;

            if (!won)
            {
                State.Lives -= 1;
            }

            OnBattleResolved?.Invoke(log, won);

            if (State.Lives <= 0)
            {
                EndRun(victory: false);
                return;
            }

            State.Round += 1;
            ShopEconomy.StartShopPhase(State, shopConfig, creatureLibrary);
            SaveSystem.Save(State);
            OnStateChanged?.Invoke();
        }

        public void StartNewRun()
        {
            SaveSystem.DeleteSave();
            State = new RunState();
            ShopEconomy.StartRun(State, shopConfig, creatureLibrary);
            SaveSystem.Save(State);
            OnStateChanged?.Invoke();
        }

        private void EndRun(bool victory)
        {
            State.Phase = GamePhase.RunOver;
            State.Victory = victory;
            SaveSystem.Save(State);
            OnRunEnded?.Invoke(victory);
        }
    }
}
