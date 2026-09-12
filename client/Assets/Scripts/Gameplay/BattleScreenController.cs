using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Battle screen (design doc §10, §17): the run's party against a randomly rolled
    /// enemy team (Meta/RandomBattle), played out one Step at a time over a battlefield backdrop.
    ///
    /// Layout, after the battle mockup: the foe's Lead and Support stand on the far clearing with
    /// their stat boxes (BattleStatsBox prefab) top-left; the player's pair stands in the foreground
    /// with its boxes on the right; the whole party runs along a bottom strip (BattlePartySlot
    /// prefab, gold frame = Lead, blue = Support); playback controls — pause, step, play, skip — sit
    /// in a pill at the top (design doc §10.4). The mons' sprites are drawn on the field, not in the
    /// boxes.
    ///
    /// A Step is drawn in three beats so it can be read: both Leads' HP drains over
    /// <see cref="HpDrainSeconds"/> with the damage shown over their sprites; anyone knocked out then
    /// fades; then the Supports step up into the empty places. Autoplay waits for all of that before
    /// the next Step. Whether a battle starts on autoplay is GameSettings.AutoplayBattles.
    ///
    /// Not here yet, on purpose: abilities (RandomBattle builds every combatant without a passive, so
    /// Speed is shown but has no effect, and the mockup's Active Passives panel is left out), catching
    /// (the Throw button is drawn but disabled — design doc §12.1), and any consequence for the run.
    ///
    /// Arriving here without a party is treated as arriving at the start of the game: the screen
    /// sends the player to Character Select instead of fighting with nobody.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        private const float FaintedAlpha = 0.35f;

        [SerializeField] private GameObject board;

        [Header("Field")]
        [SerializeField] private BattleStatsBoxView playerLeadStats;
        [SerializeField] private BattleStatsBoxView playerSupportStats;
        [SerializeField] private BattleStatsBoxView enemyLeadStats;
        [SerializeField] private BattleStatsBoxView enemySupportStats;
        [SerializeField] private Image playerLeadSprite;
        [SerializeField] private Image playerSupportSprite;
        [SerializeField] private Image enemyLeadSprite;
        [SerializeField] private Image enemySupportSprite;
        [SerializeField] private Text playerLeadDamage;
        [SerializeField] private Text playerSupportDamage;
        [SerializeField] private Text enemyLeadDamage;
        [SerializeField] private Text enemySupportDamage;
        [SerializeField] private BattlePartySlotView[] partySlots;
        [SerializeField] private Text stepText;
        [SerializeField] private Text foeCountText;

        [Header("Controls")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stepButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button throwButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;

        [Header("Pacing")]
        [SerializeField] private float hpDrainSeconds = 2f;
        [SerializeField] private float faintRevealSeconds = 0.4f;
        [SerializeField] private float pauseBetweenSteps = 0.4f;

        /// <summary>How a redraw treats HP: drain to the new value (a Step just landed), leave a drain
        /// in progress alone, or jump (the first draw, or a skip to the end).</summary>
        private enum HealthMode { Animate, Keep, Snap }

        private sealed class FieldSlot
        {
            public string Role;
            public BattleStatsBoxView Stats;
            public Image Sprite;
            public Text Damage;
            public BattleCombatant Bound;
        }

        private OnDemandStepRunner runner;
        private PokemonSpeciesLibrary library;
        private FieldSlot playerLead, playerSupport, enemyLead, enemySupport;
        private List<BattleCombatant> party;
        private int enemyTeamSize;
        private readonly Dictionary<string, int> damageThisStep = new Dictionary<string, int>();
        private Coroutine autoplayRoutine;

        /// <summary>The fight in progress; null when the screen redirected instead of starting one.</summary>
        public BattleState State => runner?.State;

        public BattleOutcome? Outcome => runner?.Outcome;

        public bool IsAutoplaying { get; private set; }

        /// <summary>True while a Step is being drawn (its HP drain and faint reveal are timed).</summary>
        public bool IsAnimating { get; private set; }

        public float HpDrainSeconds => hpDrainSeconds;

        private void Start()
        {
            if (!ActiveRun.HasRun || ActiveRun.State.LineUp.Count == 0)
            {
                RedirectToCharacterSelect();
                return;
            }

            library = ActiveRun.Library;
            // UnityEngine.Random only picks the seed; everything downstream of it is the sim's own
            // deterministic PRNG, so a fight is still reproducible from RandomBattle.Seed.
            var battle = RandomBattle.Create(ActiveRun.State.LineUp, library, Random.Range(int.MinValue, int.MaxValue));
            runner = new OnDemandStepRunner(battle.PlayerLineUp, battle.EnemyLineUp, battle.Seed);
            // Copied: the runner removes fainted mons from its own lists, and the strip keeps showing them.
            party = new List<BattleCombatant>(battle.PlayerLineUp);
            enemyTeamSize = battle.EnemyLineUp.Count;

            playerLead = new FieldSlot { Role = "Lead", Stats = playerLeadStats, Sprite = playerLeadSprite, Damage = playerLeadDamage };
            playerSupport = new FieldSlot { Role = "Support", Stats = playerSupportStats, Sprite = playerSupportSprite, Damage = playerSupportDamage };
            enemyLead = new FieldSlot { Role = "Lead", Stats = enemyLeadStats, Sprite = enemyLeadSprite, Damage = enemyLeadDamage };
            enemySupport = new FieldSlot { Role = "Support", Stats = enemySupportStats, Sprite = enemySupportSprite, Damage = enemySupportDamage };

            for (int i = 0; i < partySlots.Length; i++)
            {
                if (i < party.Count)
                {
                    var species = SpeciesOf(party[i]);
                    partySlots[i].SetMon(PokemonSprites.Load(species), DisplayName(party[i]));
                }
            }

            throwButton.interactable = false;
            resultPanel.SetActive(false);
            RenderCurrent(HealthMode.Snap);
            SetAutoplay(GameSettings.AutoplayBattles);
        }

        public void OnStepClicked()
        {
            if (runner == null || IsAnimating || runner.IsBattleOver)
            {
                return;
            }
            // Stepping by hand is dropping out of autoplay (design doc §10.4).
            SetAutoplay(false);
            StartCoroutine(PlayStep());
        }

        public void OnPlayClicked()
        {
            if (runner != null)
            {
                SetAutoplay(true);
            }
        }

        public void OnPauseClicked()
        {
            if (runner != null)
            {
                SetAutoplay(false);
            }
        }

        /// <summary>Runs the rest of the fight at once and shows where it ended.</summary>
        public void OnSkipClicked()
        {
            if (runner == null || runner.IsBattleOver)
            {
                return;
            }

            // Stops a Step mid-drain as well as the autoplay loop. Safe: drawing is all that's
            // interrupted — the Step itself was applied the moment it started.
            StopAllCoroutines();
            autoplayRoutine = null;
            IsAutoplaying = false;
            IsAnimating = false;

            while (!runner.IsBattleOver)
            {
                runner.NextStep();
            }
            damageThisStep.Clear();
            RenderCurrent(HealthMode.Snap);
            ShowResult();
        }

        private void RedirectToCharacterSelect()
        {
            board.SetActive(false);

            // A run with nobody in it can't be fought or continued. Left in ActiveRun, the Map's
            // RunBootstrapper would adopt it over the pair about to be picked.
            if (ActiveRun.HasRun)
            {
                ActiveRun.End();
            }
            ScreenFade.TransitionTo(SceneNames.CharacterSelect);
        }

        private void SetAutoplay(bool on)
        {
            IsAutoplaying = on && !runner.IsBattleOver;
            UpdateControls();
            // A loop still waiting out its pause picks the flag back up, so only start one when none
            // is running — otherwise pause-then-play inside one pause would double the pace.
            if (IsAutoplaying && autoplayRoutine == null)
            {
                autoplayRoutine = StartCoroutine(AutoplayLoop());
            }
        }

        private void UpdateControls()
        {
            bool over = runner == null || runner.IsBattleOver;
            pauseButton.interactable = !over && IsAutoplaying;
            playButton.interactable = !over && !IsAutoplaying;
            stepButton.interactable = !over;
            skipButton.interactable = !over;
        }

        private IEnumerator AutoplayLoop()
        {
            while (IsAutoplaying && !runner.IsBattleOver)
            {
                yield return new WaitForSeconds(pauseBetweenSteps);
                if (IsAutoplaying && !IsAnimating && !runner.IsBattleOver)
                {
                    yield return PlayStep();
                }
            }
            autoplayRoutine = null;
        }

        private IEnumerator PlayStep()
        {
            IsAnimating = true;

            var state = runner.State;
            var shownPlayerLead = state.LeadA;
            var shownPlayerSupport = state.SupportA;
            var shownEnemyLead = state.LeadB;
            var shownEnemySupport = state.SupportB;

            var events = runner.NextStep();
            RecordDamage(events);

            // 1. The mons that fought this Step drain to where it left them.
            Render(shownPlayerLead, shownPlayerSupport, shownEnemyLead, shownEnemySupport, HealthMode.Animate, showFaint: false);
            yield return new WaitForSeconds(hpDrainSeconds);

            if (ChangedLineUp(events))
            {
                // 2. Whoever hit 0 fades before anyone takes their place.
                Render(shownPlayerLead, shownPlayerSupport, shownEnemyLead, shownEnemySupport, HealthMode.Keep, showFaint: true);
                yield return new WaitForSeconds(faintRevealSeconds);
            }

            // 3. Promotions.
            damageThisStep.Clear();
            RenderCurrent(HealthMode.Keep);
            if (runner.IsBattleOver)
            {
                ShowResult();
            }
            IsAnimating = false;
        }

        private void ShowResult()
        {
            IsAutoplaying = false;
            var outcome = runner.Outcome ?? BattleOutcome.Draw;
            bool capped = outcome == BattleOutcome.Draw && runner.State.LineUpA.Count > 0 && runner.State.LineUpB.Count > 0;
            switch (outcome)
            {
                case BattleOutcome.SideAWins:
                    resultText.text = "Victory!";
                    resultText.color = Theme.Positive;
                    break;
                case BattleOutcome.SideBWins:
                    resultText.text = "Defeat...";
                    resultText.color = Theme.Danger;
                    break;
                default:
                    // Both sides still standing means the Step cap called it (battle-sim-spec.md §9).
                    resultText.text = capped ? "Draw - Step limit" : "Draw";
                    resultText.color = Theme.TextMuted;
                    break;
            }
            resultPanel.SetActive(true);
            UpdateControls();
        }

        private static bool ChangedLineUp(List<StepEvent> events)
        {
            foreach (var evt in events)
            {
                if (evt.Kind == StepEventKind.Faint || evt.Kind == StepEventKind.Promotion)
                {
                    return true;
                }
            }
            return false;
        }

        private void RenderCurrent(HealthMode mode)
        {
            var state = runner.State;
            Render(state.LeadA, state.SupportA, state.LeadB, state.SupportB, mode, showFaint: false);
        }

        private void Render(BattleCombatant pLead, BattleCombatant pSupport, BattleCombatant eLead, BattleCombatant eSupport,
            HealthMode mode, bool showFaint)
        {
            var state = runner.State;
            BindSlot(playerLead, pLead, mode, showFaint);
            BindSlot(playerSupport, pSupport, mode, showFaint);
            BindSlot(enemyLead, eLead, mode, showFaint);
            BindSlot(enemySupport, eSupport, mode, showFaint);
            RenderParty(pLead, pSupport, mode, showFaint);
            stepText.text = $"Step {state.StepNumber}";
            foeCountText.text = $"Foe team {state.LineUpB.Count} / {enemyTeamSize}";
        }

        private void BindSlot(FieldSlot slot, BattleCombatant mon, HealthMode mode, bool showFaint)
        {
            if (mon == null)
            {
                slot.Bound = null;
                slot.Stats.SetEmpty($"No {slot.Role}");
                slot.Sprite.enabled = false;
                slot.Damage.text = string.Empty;
                return;
            }

            int maxHp = mon.CurrentStats.Health;
            if (slot.Bound != mon)
            {
                // A different mon in this place (the first draw, or a promotion): nothing to drain from.
                slot.Bound = mon;
                var species = SpeciesOf(mon);
                slot.Stats.Show(DisplayName(mon),
                    species != null ? species.Type1 : PokemonType.Normal,
                    species != null && species.HasSecondType,
                    species != null ? species.Type2 : PokemonType.Normal,
                    mon.CurrentStats.Attack, mon.CurrentStats.Speed, PokemonSpeciesDefinitionAsset.MaxBaseSpeed);
                slot.Stats.HealthBar.SetHealth(mon.CurrentHP, maxHp);
                slot.Sprite.sprite = PokemonSprites.Load(species);
            }
            else
            {
                ApplyHealth(slot.Stats.HealthBar, mon.CurrentHP, maxHp, mode);
            }

            slot.Sprite.enabled = true;
            float alpha = showFaint && !mon.IsAlive ? FaintedAlpha : 1f;
            slot.Sprite.color = new Color(1f, 1f, 1f, alpha);
            slot.Stats.Group.alpha = alpha;
            slot.Damage.text = mode == HealthMode.Animate && damageThisStep.TryGetValue(mon.InstanceId, out int damage) && damage > 0
                ? $"-{damage}"
                : string.Empty;
        }

        private void ApplyHealth(HealthBarView bar, int hp, int maxHp, HealthMode mode)
        {
            switch (mode)
            {
                case HealthMode.Animate:
                    bar.AnimateHealth(hp, maxHp, hpDrainSeconds);
                    break;
                case HealthMode.Keep:
                    if (!bar.IsAnimating)
                    {
                        bar.SetHealth(hp, maxHp);
                    }
                    break;
                default:
                    bar.SetHealth(hp, maxHp);
                    break;
            }
        }

        private void RenderParty(BattleCombatant shownLead, BattleCombatant shownSupport, HealthMode mode, bool showFaint)
        {
            var lineUp = runner.State.LineUpA;
            for (int i = 0; i < partySlots.Length; i++)
            {
                var slot = partySlots[i];
                if (i >= party.Count)
                {
                    slot.SetRole(PartySlotRole.Empty);
                    continue;
                }

                var mon = party[i];
                bool fallen = !mon.IsAlive || !lineUp.Contains(mon);
                var role = mon == shownLead ? PartySlotRole.Lead
                    : mon == shownSupport ? PartySlotRole.Support
                    : PartySlotRole.Reserve;
                // A mon that fell this Step keeps its Lead/Support frame until the faint beat.
                if (fallen && (showFaint || role == PartySlotRole.Reserve))
                {
                    role = PartySlotRole.Fainted;
                }
                slot.SetRole(role);

                float fraction = mon.CurrentStats.Health > 0 ? (float)mon.CurrentHP / mon.CurrentStats.Health : 0f;
                switch (mode)
                {
                    case HealthMode.Animate:
                        if (!Mathf.Approximately(slot.HealthFraction, Mathf.Clamp01(fraction)))
                        {
                            slot.AnimateHealthFraction(fraction, hpDrainSeconds);
                        }
                        break;
                    case HealthMode.Keep:
                        if (!slot.IsAnimating)
                        {
                            slot.SetHealthFraction(fraction);
                        }
                        break;
                    default:
                        slot.SetHealthFraction(fraction);
                        break;
                }
            }
        }

        private void RecordDamage(List<StepEvent> events)
        {
            damageThisStep.Clear();
            foreach (var evt in events)
            {
                if (evt.Kind == StepEventKind.Damage || evt.Kind == StepEventKind.StatusTick)
                {
                    damageThisStep.TryGetValue(evt.TargetInstanceId, out int total);
                    damageThisStep[evt.TargetInstanceId] = total + evt.Amount;
                }
            }
        }

        private PokemonSpeciesDefinitionAsset SpeciesOf(BattleCombatant mon) =>
            mon.Source != null ? library.GetById(mon.Source.SpeciesId) : null;

        private string DisplayName(BattleCombatant mon)
        {
            if (mon.Source != null && !string.IsNullOrEmpty(mon.Source.Nickname))
            {
                return mon.Source.Nickname;
            }
            var species = SpeciesOf(mon);
            return species != null ? species.DisplayName : mon.InstanceId;
        }
    }
}
