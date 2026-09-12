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
    /// <summary>The Battle screen (design doc §10, §17): the run's party against an enemy team,
    /// played out one Step at a time over a battlefield backdrop.
    ///
    /// It runs two kinds of fight, told apart by whether a map node left an encounter in
    /// PendingBattle:
    /// - A **node fight** (PendingBattle set, by NodeResolutionController): the real thing. Both
    ///   sides keep their passives, the Camp buff is spent on the player's line-up, and the result
    ///   is written back to the run — Morale on a defeat, EXP to the survivors, the stubbed catch
    ///   offered on a PvE win, and the Location completed by beating the Gym.
    /// - A **dev random battle** (nothing pending): Team's "Dev: Random Battle" button. A throwaway
    ///   fight against a rolled team with passives stripped on both sides (Meta/RandomBattle) that
    ///   costs the run nothing. This is what the screen did before node resolution existed.
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
    /// Not here yet, on purpose: the mockup's Active Passives panel (a node fight does run passives,
    /// it just doesn't show them firing yet), the real drag-a-Pokéball catch at a Step boundary
    /// (design doc §12.1 — the Throw button is drawn but disabled; a won PvE node instead offers the
    /// "pick 1 from defeated" stub on the result panel), and HP carrying over between node fights
    /// (every fight starts at full health).
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
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultText;
        [SerializeField] private UiButton battleAgainButton;
        [SerializeField] private UiButton resultBackButton;
        [SerializeField] private UiButton resultActionButton;
        [SerializeField] private Transform catchButtonsContainer;
        [SerializeField] private UiButton catchButtonPrefab;

        [Header("Pacing")]
        [SerializeField] private float hpDrainSeconds = 2f;
        [SerializeField] private float faintRevealSeconds = 0.4f;
        [SerializeField] private float pauseBetweenSteps = 0.4f;

        /// <summary>How a redraw treats HP: drain to the new value (a Step just landed), leave a drain
        /// in progress alone, or jump (the first draw, or a skip to the end).</summary>
        private enum HealthMode { Animate, Keep, Snap }

        /// <summary>Which fight this is, and therefore what the end of it means — see the class
        /// doc. Decided once, in Start, by whether a map node left an encounter pending.</summary>
        private enum BattleContext { DevRandom, MapPvE, MapGym }

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

        private BattleContext context;

        /// <summary>The node fight's opposing line-up as the *run* holds it (not the battle's
        /// copies), which is what a catch has to add to the Box. Null for a dev random battle.</summary>
        private List<PokemonInstance> nodeEnemyLineUp;

        /// <summary>The node this fight belongs to, kept so a lost Gym can be re-rolled and
        /// refought (it's the one node with nowhere to walk on to).</summary>
        private string nodeId;

        /// <summary>Every event of the whole fight, Step by Step — the record a catch reads to find
        /// what fainted on the wild side (Meta/CatchResolver). Bounded by BattleConfig's Step
        /// cap.</summary>
        private readonly List<StepEvent> fightEvents = new List<StepEvent>();

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
            var playerLineUp = PendingBattle.HasPending ? StartNodeFight() : StartDevRandomFight();
            // Copied: the runner removes fainted mons from its own lists, and the strip keeps showing them.
            party = new List<BattleCombatant>(playerLineUp);

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
            // A node fight is committed the moment the player walks onto the node, so there's no
            // leaving it half-fought — the result panel is the way out. The dev battle keeps its
            // Back button, since it costs the run nothing either way.
            backButton.gameObject.SetActive(context == BattleContext.DevRandom);
            resultPanel.SetActive(false);
            RenderCurrent(HealthMode.Snap);
            SetAutoplay(GameSettings.AutoplayBattles);
        }

        /// <summary>The real thing: the encounter a map node handed over. Combatants are built here
        /// rather than by the runner because the Camp buff has to be applied at line-up assembly
        /// (battle-sim-spec.md §8) and must land on the battle's copies — multiplying the roster's
        /// own CurrentStats would buff those mons permanently, every fight, compounding. Passives
        /// are left exactly as the content resolved them on both sides.</summary>
        private List<BattleCombatant> StartNodeFight()
        {
            var state = ActiveRun.State;
            context = PendingBattle.IsGym ? BattleContext.MapGym : BattleContext.MapPvE;
            nodeEnemyLineUp = PendingBattle.EnemyLineUp;
            nodeId = PendingBattle.NodeId;
            int seed = PendingBattle.Seed;
            PendingBattle.Clear();

            var playerLineUp = BattleCombatant.FromLineUp(state.LineUp);
            if (state.NextBattleAttackBonusPercent > 0f)
            {
                foreach (var mon in playerLineUp)
                {
                    mon.CurrentStats.Attack = Mathf.RoundToInt(mon.CurrentStats.Attack * (1f + state.NextBattleAttackBonusPercent));
                }
                state.NextBattleAttackBonusPercent = 0f;
            }

            var enemyLineUp = BattleCombatant.FromLineUp(nodeEnemyLineUp);
            enemyTeamSize = enemyLineUp.Count;
            // The node's own seed, the one its encounter was rolled from (see PendingBattle.Seed).
            runner = new OnDemandStepRunner(playerLineUp, enemyLineUp, seed);
            return playerLineUp;
        }

        /// <summary>Team's dev button: a throwaway fight, passives stripped, no consequences.</summary>
        private List<BattleCombatant> StartDevRandomFight()
        {
            context = BattleContext.DevRandom;
            // UnityEngine.Random only picks the seed; everything downstream of it is the sim's own
            // deterministic PRNG, so a fight is still reproducible from RandomBattle.Seed.
            var battle = RandomBattle.Create(ActiveRun.State.LineUp, library, Random.Range(int.MinValue, int.MaxValue));
            runner = new OnDemandStepRunner(battle.PlayerLineUp, battle.EnemyLineUp, battle.Seed);
            enemyTeamSize = battle.EnemyLineUp.Count;
            return battle.PlayerLineUp;
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
                fightEvents.AddRange(runner.NextStep());
            }
            damageThisStep.Clear();
            RenderCurrent(HealthMode.Snap);
            ShowResult();
        }

        private void RedirectToCharacterSelect()
        {
            board.SetActive(false);
            // Whatever node queued this fight, there's no run left to fight it for.
            PendingBattle.Clear();

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
            fightEvents.AddRange(events);
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
                    resultText.text = context == BattleContext.MapGym
                        ? "Badge earned!\nLocation complete."
                        : "Victory!";
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

            if (context != BattleContext.DevRandom)
            {
                ApplyNodeFightResult(outcome);
            }
            UpdateResultButtons();

            resultPanel.SetActive(true);
            UpdateControls();
        }

        /// <summary>Writes a node fight's outcome back to the run — the part that makes a fight
        /// matter. Called exactly once per fight, since the fight can only end once.
        ///
        /// A draw (the Step cap, battle-sim-spec.md §9) is treated as neither: no Morale lost, no
        /// EXP gained. It's rare enough not to deserve a rule of its own, and taking Morale for a
        /// fight nobody lost would read as a bug.</summary>
        private void ApplyNodeFightResult(BattleOutcome outcome)
        {
            var state = ActiveRun.State;
            if (outcome == BattleOutcome.SideBWins)
            {
                // Morale is the run's life total (design doc §4) and the only cost of losing: on a
                // branching map there's no standing still to refight a node you've already walked
                // onto, so a loss is paid for and the run moves on. The Gym is the exception, and
                // it's handled by offering a retry rather than by blocking the map.
                state.Morale--;
                resultText.text += state.IsRunOver
                    ? "\nThe team's morale is broken. The run ends here."
                    : $"\nMorale {state.Morale} left.";
                return;
            }

            if (outcome != BattleOutcome.SideAWins)
            {
                return;
            }

            BattleRewardResolver.GrantWinRewards(runner.State.LineUpA, context == BattleContext.MapGym);
            if (context == BattleContext.MapPvE)
            {
                OfferCatches();
            }
        }

        /// <summary>The stubbed catch (design doc §12.1 is the real, Step-boundary version): one
        /// button per wild mon that fainted, each adding it to the Box. Whatever the player doesn't
        /// press, they don't keep.</summary>
        private void OfferCatches()
        {
            foreach (var defeated in CatchResolver.GetDefeated(nodeEnemyLineUp, fightEvents, Side.B))
            {
                CreateCatchButton(defeated);
            }
        }

        private void CreateCatchButton(PokemonInstance defeated)
        {
            var species = library.GetById(defeated.SpeciesId);
            // The shared Button prefab rather than a hand-built Image+Text, so a catch button is the
            // same widget as every other button in the game (see UI/UiButton).
            var button = Instantiate(catchButtonPrefab, catchButtonsContainer);
            button.name = $"Catch_{species?.DisplayName}";
            button.Style = Theme.ButtonStyle.Confirm;
            button.Text = $"Catch {species?.DisplayName}";
            button.Button.onClick.AddListener(() =>
            {
                CatchResolver.Catch(ActiveRun.State, defeated, library);
                Destroy(button.gameObject);
            });
        }

        /// <summary>The two kinds of fight end on different buttons: a dev battle keeps its pair
        /// (roll another / back to Team), and a node fight gets the single centred action, since
        /// there's only ever one thing to do with a run — carry on with it.</summary>
        private void UpdateResultButtons()
        {
            bool nodeFight = context != BattleContext.DevRandom;
            battleAgainButton.gameObject.SetActive(!nodeFight);
            resultBackButton.gameObject.SetActive(!nodeFight);
            resultActionButton.gameObject.SetActive(nodeFight);

            if (nodeFight)
            {
                resultActionButton.Text = GymNeedsAnotherAttempt ? "Try Again" : "Continue";
            }
        }

        /// <summary>A node fight's one way on from the result panel — the whole win-loss loop. A
        /// broken run and a completed Location both end at Home (there's no Region Hub to pick the
        /// next Location from yet — see ADR 0003), a Gym still standing is fought again, and
        /// anything else returns to the map to keep walking.
        ///
        /// The dev battle's own two buttons go straight to the navigator and never reach here.</summary>
        public void OnResultActionClicked()
        {
            if (GymNeedsAnotherAttempt)
            {
                // A fresh seed, not the node's: a retry is meant to be a new fight, not the same one
                // played out again.
                int seed = Random.Range(int.MinValue, int.MaxValue);
                PendingBattle.Set(
                    GymTeamGenerator.Generate(library, ActiveRun.State.LineUp.Count, seed), nodeId, isGym: true, seed);
                ScreenFade.TransitionTo(SceneNames.Battle);
                return;
            }

            bool locationComplete = context == BattleContext.MapGym && runner.Outcome == BattleOutcome.SideAWins;
            if (ActiveRun.State.IsRunOver || locationComplete)
            {
                ActiveRun.End();
                ScreenFade.TransitionTo(SceneNames.Home);
                return;
            }

            ScreenFade.TransitionTo(SceneNames.Map);
        }

        /// <summary>The Gym is every path's terminus (design doc §14), so a run that's still alive
        /// but hasn't beaten it has nowhere to walk on to — it gets another attempt instead.</summary>
        private bool GymNeedsAnotherAttempt =>
            context == BattleContext.MapGym
            && runner.Outcome != BattleOutcome.SideAWins
            && !ActiveRun.State.IsRunOver;

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
