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
    /// enemy team (Meta/RandomBattle), played out one Step at a time.
    ///
    /// What it shows is the core Step model: both sides' Lead and Support facing each other (Leads
    /// in the middle), each with HP and attack; the dormant mons queued behind them (design doc §7
    /// — only the front two are ever active); a log of the simultaneous attack exchange, faints and
    /// promotions; and step-through, autoplay and skip controls (§10.4). A Step that knocks a mon
    /// out is drawn twice — first with the fallen mon at 0 HP, then with its Support stepped up —
    /// so a promotion never looks like the Lead simply changed species.
    ///
    /// Not here yet, on purpose: abilities (RandomBattle builds every combatant without a passive,
    /// so Speed and charge meters have no visible effect and aren't drawn), catching (§12.1), and
    /// any consequence for the run — a random battle costs no Morale, grants nothing, and never
    /// writes damage back to the roster.
    ///
    /// Uses the on-demand runner rather than the precomputed one even though nothing can interrupt
    /// a random battle: it's the runner a PvE fight needs once catching exists, and it makes step-
    /// through simply "advance once".
    ///
    /// Arriving here without a party is treated as arriving at the start of the game: the screen
    /// sends the player to Character Select instead of fighting with nobody.</summary>
    public sealed class BattleScreenController : MonoBehaviour
    {
        // Card budgets, for the slot sizes BattleSceneBuilder lays out (Lead 240x260, Support
        // 200x230), in the same terms as CharacterSelectController's: 12 padding + 20 role line +
        // sprite + 24 name/attack row + 30 types row + 18 HP bar + 5 one-unit gaps = 109 + sprite.
        // 132 fills the Lead card to 241, 102 the Support card to 211. Grow the slot sizes with any
        // of these.
        private const int LeadSpriteHeight = 132;
        private const int SupportSpriteHeight = 102;
        private const int CardNameFontSize = 17;
        private const int CardRoleFontSize = 14;
        private const int CardTypesRowHeight = 30;
        private const int CardAttackIconSize = 24;
        private const int CardSideInset = 6;
        private const int DamageFontSize = 30;
        private const int ReserveSpriteSize = 44;
        private const int MaxLogLines = 7;

        /// <summary>A mon at 0 HP during a Step's faint reveal.</summary>
        private const float FaintedAlpha = 0.4f;

        /// <summary>A side with no Support left — the frame stays so the formation still reads as
        /// Lead + Support.</summary>
        private const float EmptySlotAlpha = 0.35f;

        /// <summary>Dormant mons are dimmed for the same reason Team dims its Reserve slots.</summary>
        private const float ReserveAlpha = 0.7f;

        [SerializeField] private GameObject board;
        [SerializeField] private RectTransform playerLeadSlot;
        [SerializeField] private RectTransform playerSupportSlot;
        [SerializeField] private RectTransform enemyLeadSlot;
        [SerializeField] private RectTransform enemySupportSlot;
        [SerializeField] private RectTransform playerReserveRow;
        [SerializeField] private RectTransform enemyReserveRow;
        [SerializeField] private Text stepText;
        [SerializeField] private Text resultText;
        [SerializeField] private Text logText;
        [SerializeField] private Button stepButton;
        [SerializeField] private Button autoplayButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button battleAgainButton;
        [SerializeField] private GameObject typeIconPrefab;
        [SerializeField] private GameObject healthBarPrefab;

        /// <summary>Pause between autoplayed Steps.</summary>
        [SerializeField] private float secondsPerStep = 0.7f;

        /// <summary>How long a knocked-out mon stays on screen at 0 HP before its replacement is
        /// drawn.</summary>
        [SerializeField] private float faintRevealSeconds = 0.35f;

        [SerializeField] private bool autoplayOnStart = true;

        private sealed class MonPanel
        {
            public string Role;
            public CanvasGroup Group;
            public Text RoleText;
            public Image Sprite;
            public Text DamageText;
            public PokemonCardBuilder.NameAttackRow NameRow;
            public PokemonCardBuilder.TypeIconRow TypeIcons;
            public HealthBarView HealthBar;
        }

        private OnDemandStepRunner runner;
        private PokemonSpeciesLibrary library;
        private MonPanel playerLead, playerSupport, enemyLead, enemySupport;
        private readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();
        private readonly Dictionary<string, int> damageThisStep = new Dictionary<string, int>();
        private readonly List<string> logLines = new List<string>();
        private int shownPlayerReserve = -1;
        private int shownEnemyReserve = -1;
        private Coroutine autoplayRoutine;

        /// <summary>The fight in progress; null when the screen redirected instead of starting one.</summary>
        public BattleState State => runner?.State;

        public BattleOutcome? Outcome => runner?.Outcome;

        public bool IsAutoplaying { get; private set; }

        /// <summary>True while a Step is being drawn — its faint reveal is timed, so a caller that
        /// wants to see the Step's result has to wait for this to clear.</summary>
        public bool IsAnimating { get; private set; }

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

            // Prefixed by side, since both teams can field the same species.
            foreach (var mon in battle.PlayerLineUp)
            {
                displayNames[mon.InstanceId] = $"Your {SpeciesName(mon)}";
            }
            foreach (var mon in battle.EnemyLineUp)
            {
                displayNames[mon.InstanceId] = $"Foe {SpeciesName(mon)}";
            }

            playerLead = CreatePanel(playerLeadSlot, "Lead", LeadSpriteHeight);
            playerSupport = CreatePanel(playerSupportSlot, "Support", SupportSpriteHeight);
            enemyLead = CreatePanel(enemyLeadSlot, "Lead", LeadSpriteHeight);
            enemySupport = CreatePanel(enemySupportSlot, "Support", SupportSpriteHeight);

            resultText.text = string.Empty;
            battleAgainButton.gameObject.SetActive(false);
            AddLogLine($"A foe team of {battle.EnemyLineUp.Count} appears!");
            RenderCurrent();
            SetAutoplay(autoplayOnStart);
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

        public void OnAutoplayClicked()
        {
            if (runner != null)
            {
                SetAutoplay(!IsAutoplaying);
            }
        }

        /// <summary>Runs the rest of the fight at once and shows where it ended.</summary>
        public void OnSkipClicked()
        {
            if (runner == null || runner.IsBattleOver)
            {
                return;
            }

            // Stops a Step mid-reveal as well as the autoplay loop. Safe: the reveal only draws,
            // and the Step it was drawing has already been applied and logged.
            StopAllCoroutines();
            autoplayRoutine = null;
            IsAutoplaying = false;
            IsAnimating = false;

            List<StepEvent> events = null;
            while (!runner.IsBattleOver)
            {
                events = runner.NextStep();
                // Logged Step by Step, so each promotion line reads the line-up as it was right
                // after its own Step.
                AppendLog(events);
            }
            RecordDamage(events);
            RenderCurrent();
            ShowResult();
        }

        private void RedirectToCharacterSelect()
        {
            board.SetActive(false);
            stepButton.gameObject.SetActive(false);
            autoplayButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            battleAgainButton.gameObject.SetActive(false);

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
            autoplayButton.GetComponent<UiButton>().Text = IsAutoplaying ? "Pause" : "Autoplay";
            // A loop still waiting out its pause picks the flag back up, so only start one when
            // none is running — otherwise pause-then-resume inside one pause would double the pace.
            if (IsAutoplaying && autoplayRoutine == null)
            {
                autoplayRoutine = StartCoroutine(AutoplayLoop());
            }
        }

        private IEnumerator AutoplayLoop()
        {
            while (IsAutoplaying && !runner.IsBattleOver)
            {
                yield return new WaitForSeconds(secondsPerStep);
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
            AppendLog(events);

            if (ChangedLineUp(events))
            {
                // The mons that fought this Step, as they ended it, before the faint check's
                // promotions are drawn.
                Render(shownPlayerLead, shownPlayerSupport, shownEnemyLead, shownEnemySupport);
                yield return new WaitForSeconds(faintRevealSeconds);
            }

            RenderCurrent();
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
                    resultText.text = "Draw";
                    resultText.color = Theme.TextMuted;
                    break;
            }

            // Both sides still standing means the Step cap called it (battle-sim-spec.md §9).
            if (outcome == BattleOutcome.Draw && runner.State.LineUpA.Count > 0 && runner.State.LineUpB.Count > 0)
            {
                AddLogLine($"Step limit of {BattleConfig.StepCap} reached.");
            }

            stepButton.gameObject.SetActive(false);
            autoplayButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            battleAgainButton.gameObject.SetActive(true);
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

        private void RenderCurrent()
        {
            var state = runner.State;
            Render(state.LeadA, state.SupportA, state.LeadB, state.SupportB);
        }

        private void Render(BattleCombatant pLead, BattleCombatant pSupport, BattleCombatant eLead, BattleCombatant eSupport)
        {
            var state = runner.State;
            BindPanel(playerLead, pLead);
            BindPanel(playerSupport, pSupport);
            BindPanel(enemyLead, eLead);
            BindPanel(enemySupport, eSupport);
            RenderReserve(playerReserveRow, state.LineUpA, ref shownPlayerReserve);
            RenderReserve(enemyReserveRow, state.LineUpB, ref shownEnemyReserve);
            stepText.text = $"Step {state.StepNumber}";
        }

        private MonPanel CreatePanel(RectTransform slot, string role, int spriteHeight)
        {
            var card = PokemonCardBuilder.CreateCard(slot, "Card");
            Stretch(card.GetComponent<RectTransform>());

            var panel = new MonPanel
            {
                Role = role,
                Group = card.AddComponent<CanvasGroup>(),
                RoleText = PokemonCardBuilder.AddLine(card.transform, role, CardRoleFontSize, FontStyle.Bold, Theme.TextMuted),
                Sprite = PokemonCardBuilder.AddSprite(card.transform, null, spriteHeight),
                NameRow = PokemonCardBuilder.AddNameAttackRow(card.transform, CardNameFontSize, Theme.TextDark, CardSideInset, CardAttackIconSize),
                TypeIcons = PokemonCardBuilder.AddTypeIconRow(card.transform, typeIconPrefab, CardTypesRowHeight),
                HealthBar = PokemonCardBuilder.AddStatBar<HealthBarView>(card.transform, healthBarPrefab, CardSideInset),
            };
            panel.RoleText.name = "RoleText";
            panel.DamageText = CreateDamageText(panel.Sprite.transform);
            return panel;
        }

        /// <summary>The "-12" over a mon's sprite for the Step just played. A child of the sprite
        /// rather than a line of the card, so it overlays instead of taking layout room.</summary>
        private static Text CreateDamageText(Transform sprite)
        {
            var go = new GameObject("DamageText", typeof(RectTransform));
            go.transform.SetParent(sprite, false);
            var text = go.AddComponent<Text>();
            text.font = Theme.GameFont;
            text.fontSize = DamageFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.color = Theme.Danger;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            go.AddComponent<Outline>().effectColor = Theme.TextLight;
            Stretch(text.rectTransform);
            return text;
        }

        private void BindPanel(MonPanel panel, BattleCombatant mon)
        {
            bool filled = mon != null;
            panel.Sprite.enabled = filled;
            panel.NameRow.Name.transform.parent.gameObject.SetActive(filled);
            panel.HealthBar.transform.parent.gameObject.SetActive(filled);

            if (!filled)
            {
                panel.RoleText.text = $"No {panel.Role}";
                panel.TypeIcons.Row.gameObject.SetActive(false);
                panel.DamageText.text = string.Empty;
                panel.Group.alpha = EmptySlotAlpha;
                return;
            }

            var species = SpeciesOf(mon);
            panel.RoleText.text = panel.Role;
            panel.Sprite.sprite = PokemonSprites.Load(species);
            panel.NameRow.Name.text = SpeciesName(mon);
            panel.NameRow.Attack.text = mon.CurrentStats.Attack.ToString();
            panel.TypeIcons.Row.gameObject.SetActive(species != null);
            if (species != null)
            {
                panel.TypeIcons.SetTypes(species.Type1, species.HasSecondType, species.Type2);
            }
            panel.HealthBar.SetHealth(mon.CurrentHP, mon.CurrentStats.Health);
            panel.DamageText.text = damageThisStep.TryGetValue(mon.InstanceId, out int damage) && damage > 0
                ? $"-{damage}"
                : string.Empty;
            panel.Group.alpha = mon.IsAlive ? 1f : FaintedAlpha;
        }

        /// <summary>Everyone behind the Support, as small dimmed sprites, next-up first. Line-ups
        /// only ever lose mons from the front, so the count alone says whether it changed.</summary>
        private void RenderReserve(RectTransform row, List<BattleCombatant> lineUp, ref int shownCount)
        {
            int reserveCount = Mathf.Max(0, lineUp.Count - 2);
            if (reserveCount == shownCount)
            {
                return;
            }
            shownCount = reserveCount;

            Clear(row);
            for (int i = 2; i < lineUp.Count; i++)
            {
                var go = new GameObject($"Reserve{i - 2}", typeof(RectTransform));
                go.transform.SetParent(row, false);
                go.GetComponent<RectTransform>().sizeDelta = new Vector2(ReserveSpriteSize, ReserveSpriteSize);
                var image = go.AddComponent<Image>();
                image.sprite = PokemonSprites.Load(SpeciesOf(lineUp[i]));
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.color = new Color(1f, 1f, 1f, ReserveAlpha);
                var layoutElement = go.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = ReserveSpriteSize;
                layoutElement.preferredHeight = ReserveSpriteSize;
            }
        }

        private void RecordDamage(List<StepEvent> events)
        {
            damageThisStep.Clear();
            if (events == null)
            {
                return;
            }
            foreach (var evt in events)
            {
                if (evt.Kind == StepEventKind.Damage || evt.Kind == StepEventKind.StatusTick)
                {
                    damageThisStep.TryGetValue(evt.TargetInstanceId, out int total);
                    damageThisStep[evt.TargetInstanceId] = total + evt.Amount;
                }
            }
        }

        private void AppendLog(List<StepEvent> events)
        {
            foreach (var evt in events)
            {
                string line = Describe(evt);
                if (line != null)
                {
                    logLines.Add(line);
                }
            }
            TrimAndShowLog();
        }

        private void AddLogLine(string line)
        {
            logLines.Add(line);
            TrimAndShowLog();
        }

        private void TrimAndShowLog()
        {
            while (logLines.Count > MaxLogLines)
            {
                logLines.RemoveAt(0);
            }
            logText.text = string.Join("\n", logLines);
        }

        /// <summary>Only the events a passive-free fight produces. PassiveTriggered still fires
        /// when a charge meter fills — the sim doesn't know the passive was stripped — but with no
        /// effect behind it there's nothing to tell the player.</summary>
        private string Describe(StepEvent evt)
        {
            switch (evt.Kind)
            {
                case StepEventKind.Damage:
                    return $"{NameOf(evt.SourceInstanceId)} hits {NameOf(evt.TargetInstanceId)} for {evt.Amount}.";
                case StepEventKind.Faint:
                    return $"{NameOf(evt.SourceInstanceId)} fainted!";
                case StepEventKind.Promotion:
                    var lineUp = runner.State.LineUp(evt.SourceSide ?? Side.A);
                    bool isLead = lineUp.Count > 0 && lineUp[0].InstanceId == evt.SourceInstanceId;
                    return $"{NameOf(evt.SourceInstanceId)} {(isLead ? "takes the Lead" : "moves up to Support")}.";
                default:
                    return null;
            }
        }

        private string NameOf(string instanceId) =>
            instanceId != null && displayNames.TryGetValue(instanceId, out var name) ? name : instanceId;

        private PokemonSpeciesDefinitionAsset SpeciesOf(BattleCombatant mon) =>
            mon.Source != null ? library.GetById(mon.Source.SpeciesId) : null;

        private string SpeciesName(BattleCombatant mon)
        {
            var species = SpeciesOf(mon);
            if (mon.Source != null && !string.IsNullOrEmpty(mon.Source.Nickname))
            {
                return mon.Source.Nickname;
            }
            return species != null ? species.DisplayName : mon.InstanceId;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Deactivates as well as destroys, for the reason TeamPanelController.Clear
        /// does: Destroy is deferred to end of frame.</summary>
        private static void Clear(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
