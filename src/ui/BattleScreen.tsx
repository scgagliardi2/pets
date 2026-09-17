/**
 * The battle screen.
 *
 * Opens **paused**, with the synergy opening already applied and drawn. Both come from the Unity
 * build: effects applied inside Step 1 were invisible because a 1-point shield appeared and
 * popped in the same frame, and autoplay-by-default meant the first exchange was over before the
 * player looked at the board.
 *
 * The field is one fixed 1280x720 coordinate space scaled as a whole to fit the window. Nothing
 * inside it is positioned against a viewport edge, and every coordinate comes from `layout.ts`.
 */

import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from 'react';

import {
  activeSynergies,
  type BattleOutcome,
  type Combatant,
  type Side,
  type StepEvent,
} from '../sim/index.js';
import { speciesOf, type Species } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import { statsOf, toCombatant, type PokemonInstance } from '../content/factory.js';
import { BALL_TIERS, ballName, type BallTier } from '../meta/balls.js';
import { percentAgainst } from '../meta/catching.js';
import { useRunStore } from '../state/runStore.js';
import { BattlePlayer } from '../playback/player.js';
import { CHROME, FIELD, slotFor } from './layout.js';
import { MonView } from './MonView.js';
import { acceptDrop, readDragPayload, setDragPayload } from './dragDrop.js';
import { TraitCounters } from './TraitCounters.js';

export interface BattleScreenProps {
  own: PokemonInstance[];
  foe: PokemonInstance[];
  seed: number;
  scenarioNote: string;
  /** Changing this rebuilds the fight from scratch. */
  runKey: string;
  /** Called once, when the fight resolves. */
  onFinished?: (outcome: BattleOutcome) => void;
}

/**
 * Owns the player's lifetime.
 *
 * Deliberately an effect with state rather than a `useMemo` plus a disposing cleanup. StrictMode
 * mounts every effect, tears it down and mounts it again, so a player built during render and
 * disposed on cleanup is dead by the second mount: the clock is cancelled for good, `play()`
 * early-returns on a disposed player, and every control silently does nothing. Building it *in*
 * the effect means the teardown disposes the same instance the teardown belongs to, and the
 * remount gets a fresh one.
 */
export function BattleScreen(props: BattleScreenProps) {
  const { own, foe, seed, runKey } = props;
  const [player, setPlayer] = useState<BattlePlayer | null>(null);

  useEffect(() => {
    const created = new BattlePlayer(own.map(toCombatant), foe.map(toCombatant), seed);
    setPlayer(created);
    return () => created.dispose();
    // runKey is the real dependency: it changes when the fight changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runKey]);

  // One frame with an empty field while the player is built. Rendering the board against a
  // half-made player would mean every child had to cope with nulls.
  if (player === null) return <div className="field-frame" />;
  return <BattleView {...props} player={player} />;
}

function BattleView({
  own,
  foe,
  scenarioNote,
  runKey,
  onFinished,
  player,
}: BattleScreenProps & { player: BattlePlayer }) {
  // A combatant carries no species reference — the sim has no idea what a Pokemon is — so the
  // screen keeps its own map from instance id to species.
  const speciesByInstance = useMemo(() => {
    const map = new Map<string, Species>();
    for (const instance of [...own, ...foe]) {
      const species = speciesOf(instance.speciesId);
      if (species !== null) map.set(instance.instanceId, species);
    }
    return map;
  }, [own, foe]);

  const snapshot = useSyncExternalStore(player.subscribe, player.getSnapshot, player.getSnapshot);
  const { display, history, outcome, isOver, isFinished, clockState, speed } = snapshot;
  const board = display.board;

  const scale = useFieldScale();

  // Reported once, on the transition into a finished state — not on every render while finished.
  const reported = useRef(false);
  useEffect(() => {
    reported.current = false;
  }, [runKey]);
  useEffect(() => {
    // isFinished, not isOver: the latter is true from the moment the killing Step is computed,
    // so reporting on it cuts to the results screen on the same frame as the death blow.
    if (isFinished && outcome !== null && !reported.current) {
      reported.current = true;
      onFinished?.(outcome);
    }
  }, [isFinished, outcome, onFinished]);

  const resolveThrow = useRunStore((s) => s.resolveThrow);
  const balls = useRunStore((s) => s.run.balls);

  /**
   * One throw path, shared by the ball tray and by dropping a ball on the enemy. Two paths would
   * mean two places that could forget to spend the ball or to tell the run about the catch.
   */
  const throwAt = (tier: BallTier): void => {
    if (balls[tier] <= 0) return;
    const target = snapshot.throwTarget;
    if (target === null) return;
    const species = speciesByInstance.get(target.instanceId);
    const wild = foe.find((m) => m.instanceId === target.instanceId);
    if (species === undefined || wild === undefined) return;
    resolveThrow(player.throwBall(tier), species.id, wild.exp);
  };

  const synergiesA = useMemo(() => activeSynergies(own.map(toCombatant)), [own]);
  const synergiesB = useMemo(() => activeSynergies(foe.map(toCombatant)), [foe]);

  const onField = (side: Side) =>
    (side === 'A' ? board.lineUpA : board.lineUpB)
      .slice(0, 2)
      .map((combatant, index) => ({ combatant, index }));

  return (
    <>
      <div className="field-frame">
        <div className="field" style={{ transform: `scale(${scale})` }}>
          <div className="field-art" />

          <div className="trait-slot" style={{ left: CHROME.ownTraits.x, top: CHROME.ownTraits.y }}>
            <TraitCounters synergies={synergiesA} align="left" />
          </div>
          <div
            className="trait-slot right"
            style={{ right: FIELD.width - CHROME.foeTraits.x, top: CHROME.foeTraits.y }}
          >
            <TraitCounters synergies={synergiesB} align="right" />
          </div>

          <FoeRoster instances={foe} board={board} />

          {snapshot.throwTarget !== null && snapshot.canThrow && (
            <FoeDropZone
              target={snapshot.throwTarget}
              onDropBall={(tier) => throwAt(tier)}
            />
          )}

          {(['A', 'B'] as const).flatMap((side) =>
            onField(side).map(({ combatant, index }) => {
              const slot = slotFor(side, index);
              const species = speciesByInstance.get(combatant.instanceId);
              if (slot === null || species === undefined) return null;
              return (
                <MonView
                  key={combatant.instanceId}
                  combatant={combatant}
                  species={species}
                  side={side}
                  slot={slot}
                  flash={display.flashes[combatant.instanceId]}
                  fainting={display.fainting.includes(combatant.instanceId)}
                />
              );
            }),
          )}

          <ControlStack
            player={player}
            clockState={clockState}
            speed={speed}
            isOver={isOver}
            isFinished={isFinished}
          />

          <div className="step-badge" style={{ left: CHROME.stepBadge.x, top: CHROME.stepBadge.y }}>
            STEP {board.stepNumber}
          </div>

          <div
            className="caption"
            style={{ left: CHROME.caption.x, top: CHROME.caption.y, width: CHROME.caption.width }}
          >
            {isOver && outcome !== null ? (
              <span className="verdict">
                {outcome === 'SideAWins' ? 'You win' : outcome === 'SideBWins' ? 'You lose' : 'Draw'}
              </span>
            ) : (
              describeEvent(display.lastEvent, speciesByInstance) ?? scenarioNote
            )}
          </div>
        </div>
      </div>

      <div className="lower">
        <PartyStrip instances={own} board={board} />
        <ThrowPanel
          target={snapshot.throwTarget}
          canThrow={snapshot.canThrow}
          speciesByInstance={speciesByInstance}
          onThrow={throwAt}
        />
        <EventLog history={history} speciesByInstance={speciesByInstance} />
      </div>
    </>
  );
}

/** Scales the fixed field to fit the window, preserving its aspect ratio. */
function useFieldScale(): number {
  const [scale, setScale] = useState(1);

  useLayoutEffect(() => {
    const measure = (): void => {
      const frame = document.querySelector('.field-frame');
      if (frame === null) return;
      const rect = frame.getBoundingClientRect();
      setScale(Math.min(rect.width / FIELD.width, rect.height / FIELD.height, 1));
    };
    measure();
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
  }, []);

  return scale;
}

/**
 * The playback controls, stacked down the right edge of the field.
 *
 * Icons are the 12x12 pixel set from the Unity repo. The stack is, top to bottom: pause/play,
 * autoplay to the end, one Step, and a speed toggle that cycles 1x/2x/4x — the four the mockup
 * shows, mapped onto the four controls the player actually has.
 */
function ControlStack({
  player,
  clockState,
  speed,
  isOver,
  isFinished,
}: {
  player: BattlePlayer;
  clockState: 'paused' | 'running';
  speed: number;
  isOver: boolean;
  isFinished: boolean;
}) {
  const { x, y, gap, size } = CHROME.controlStack;

  const buttons = [
    {
      key: 'play',
      icon: clockState === 'running' ? 'IconPause' : 'IconPlay',
      title: clockState === 'running' ? 'Pause' : 'Play',
      onClick: () => player.togglePlay(),
      disabled: isFinished,
      active: clockState === 'running',
    },
    {
      key: 'step',
      icon: 'IconStep',
      title: 'Play one Step, then pause',
      onClick: () => player.stepOnce(),
      disabled: isFinished || isOver || clockState === 'running',
      active: false,
    },
    {
      key: 'ff',
      icon: 'IconFastForward',
      title: 'Run to the end',
      onClick: () => player.fastForward(),
      disabled: isFinished,
      active: false,
    },
  ];

  return (
    <div className="control-stack" style={{ right: FIELD.width - x, top: y, gap }}>
      {buttons.map((b) => (
        <button
          key={b.key}
          className={`stack-button ${b.active ? 'on' : ''}`}
          style={{ width: size, height: size }}
          onClick={b.onClick}
          disabled={b.disabled}
          title={b.title}
          aria-label={b.title}
        >
          <img src={`/icons/ui/${b.icon}.png`} alt="" />
        </button>
      ))}
      <button
        className="stack-button speed"
        style={{ width: size, height: size }}
        onClick={() => player.setSpeed(speed >= 4 ? 1 : speed * 2)}
        title="Playback speed"
        aria-label={`Playback speed, currently ${speed} times`}
      >
        {speed}x
      </button>
    </div>
  );
}

/** The foe's line-up across the top of the field, with details on hover. */
function FoeRoster({ instances, board }: { instances: PokemonInstance[]; board: { lineUpB: Combatant[] } }) {
  const alive = new Set(board.lineUpB.map((c) => c.instanceId));
  const active = new Set(board.lineUpB.slice(0, 2).map((c) => c.instanceId));

  return (
    <div className="foe-roster" style={{ left: CHROME.foeRoster.x, top: CHROME.foeRoster.y }}>
      {instances.map((instance) => {
        const species = speciesOf(instance.speciesId);
        if (species === null) return null;
        return (
          <RosterCard
            key={instance.instanceId}
            instance={instance}
            species={species}
            fainted={!alive.has(instance.instanceId)}
            active={active.has(instance.instanceId)}
            compact
          />
        );
      })}
    </div>
  );
}

function PartyStrip({ instances, board }: { instances: PokemonInstance[]; board: { lineUpA: Combatant[] } }) {
  const alive = new Set(board.lineUpA.map((c) => c.instanceId));
  const active = new Set(board.lineUpA.slice(0, 2).map((c) => c.instanceId));

  return (
    <div className="panel party">
      <div className="party-list">
        {instances.map((instance) => {
          const species = speciesOf(instance.speciesId);
          if (species === null) return null;
          return (
            <RosterCard
              key={instance.instanceId}
              instance={instance}
              species={species}
              fainted={!alive.has(instance.instanceId)}
              active={active.has(instance.instanceId)}
            />
          );
        })}
      </div>
    </div>
  );
}

/** One mon in a roster strip. Stats, typing and passive are on hover, as the mockup asks. */
function RosterCard({
  instance,
  species,
  fainted,
  active,
  compact,
}: {
  instance: PokemonInstance;
  species: Species;
  fainted: boolean;
  active: boolean;
  compact?: boolean;
}) {
  const stats = statsOf(instance);
  const passive = species.passiveId;

  return (
    <div className={`roster-card ${compact === true ? 'compact' : ''} ${fainted ? 'fainted' : ''} ${active ? 'active' : ''}`}>
      <img src={spriteUrl(species, { facing: 'front' })} alt="" />
      <span className="roster-name">{species.name}</span>
      <div className="roster-tip">
        <strong>{species.name}</strong>
        <span className="roster-tip-types">
          {species.types.map((t) => (
            <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} title={t} />
          ))}
        </span>
        <span>
          {stats.attack} attack &middot; {stats.health} health &middot; speed {stats.speed}
        </span>
        <span>Tier {species.tier}</span>
        {passive !== null && <span className="roster-tip-passive">{passiveLabel(passive)}</span>}
      </div>
    </div>
  );
}

/** Passive ids are `<species>-<passive-name>`; the readable half is what a player wants. */
function passiveLabel(passiveId: string): string {
  const name = passiveId.split('-').slice(1).join(' ');
  return name.replace(/\b\w/g, (c) => c.toUpperCase());
}

/**
 * The patch of field a ball can be dropped on.
 *
 * Sits over the foe's Lead only while a throw would actually resolve, so a ball dragged during a
 * Step's beats has nowhere to land rather than silently doing nothing. Sized generously — a drag
 * target the size of a Pidgey sprite is a test of aim, not a decision.
 */
function FoeDropZone({
  target,
  onDropBall,
}: {
  target: Combatant;
  onDropBall: (tier: BallTier) => void;
}) {
  const [over, setOver] = useState(false);
  const slot = slotFor('B', 0);
  if (slot === null) return null;

  return (
    <div
      className={`foe-drop ${over ? 'over' : ''}`}
      style={{ left: slot.x - 150, top: slot.y - 260, width: 300, height: 300 }}
      onDragEnter={(e) => {
        acceptDrop(e);
        setOver(true);
      }}
      onDragOver={acceptDrop}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        setOver(false);
        const payload = readDragPayload(e);
        if (payload !== null && payload.kind === 'ball') onDropBall(payload.tier as BallTier);
      }}
      aria-hidden="true"
    >
      <span className="foe-drop-hint">Release to throw at {target.instanceId.split('-').pop()}</span>
    </div>
  );
}

/**
 * The Pokeball tray.
 *
 * Odds are shown per ball *before* the throw, against the foe's current Lead, because committing
 * a ball to odds you can't see makes the weaken-then-throw loop guesswork. They move as the fight
 * does: a target at full health is barely worth a ball, a nearly-dead one with a status is.
 *
 * A ball can be clicked or dragged onto the enemy. Dragging is the better verb — you are throwing
 * something at something — but clicking stays because a drag is a poor fit for a trackpad and
 * impossible with a keyboard.
 */
function ThrowPanel({
  target,
  canThrow,
  speciesByInstance,
  onThrow,
}: {
  target: Combatant | null;
  canThrow: boolean;
  speciesByInstance: Map<string, Species>;
  onThrow: (tier: BallTier) => void;
}) {
  const balls = useRunStore((s) => s.run.balls);
  const throwLog = useRunStore((s) => s.throwLog);

  const species = target === null ? undefined : speciesByInstance.get(target.instanceId);
  const last = throwLog[0];

  return (
    <div className="panel throw-panel">
      <div className="throw-heading">
        {target === null ? 'Nothing to catch' : `Throw at ${species?.name ?? 'the foe'}`}
      </div>

      <div className="ball-list">
        {BALL_TIERS.map((tier) => {
          const count = balls[tier];
          const odds = percentAgainst(tier, target);
          const usable = canThrow && count > 0 && target !== null;
          return (
            <button
              key={tier}
              className={`ball-row ${tier.toLowerCase()}`}
              disabled={!usable}
              draggable={usable}
              onDragStart={(e) => setDragPayload(e, { kind: 'ball', tier })}
              onClick={() => onThrow(tier)}
              title={
                count === 0
                  ? `No ${ballName(tier)}s left`
                  : !canThrow
                    ? 'Wait for the Step to finish'
                    : `${ballName(tier)}: ${odds}% — click, or drag onto the enemy`
              }
            >
              <span className={`ball-dot ${tier.toLowerCase()}`} />
              <span className="ball-odds">{odds}%</span>
              <span className="ball-count">x{count}</span>
            </button>
          );
        })}
      </div>

      {last !== undefined ? (
        <span className={`throw-note ${last.outcome === 'Caught' ? 'good' : 'bad'}`}>
          {last.outcome === 'Caught'
            ? `Caught at ${Math.round(last.chance * 100)}%`
            : `Broke free at ${Math.round(last.chance * 100)}%`}
        </span>
      ) : (
        <span className="throw-note">Drag a ball onto the enemy. Odds rise as its health falls.</span>
      )}
    </div>
  );
}

function EventLog({
  history,
  speciesByInstance,
}: {
  history: StepEvent[];
  speciesByInstance: Map<string, Species>;
}) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const el = ref.current;
    if (el !== null) el.scrollTop = el.scrollHeight;
  }, [history.length]);

  const grouped = useMemo(() => {
    const groups: { step: number; events: StepEvent[] }[] = [];
    for (const event of history) {
      const last = groups[groups.length - 1];
      if (last !== undefined && last.step === event.step) last.events.push(event);
      else groups.push({ step: event.step, events: [event] });
    }
    return groups;
  }, [history]);

  return (
    <div className="panel log" ref={ref}>
      {grouped.map((group) => (
        <div key={group.step}>
          <div className="log-step">{group.step === 0 ? 'OPENING' : `STEP ${group.step}`}</div>
          {group.events.map((event, i) => {
            const text = describeEvent(event, speciesByInstance);
            if (text === null) return null;
            const tone =
              event.kind === 'Faint' || event.kind === 'SuddenDeath'
                ? 'faint'
                : event.kind === 'PassiveTriggered' || event.kind === 'BattleEnd'
                  ? 'fire'
                  : event.kind === 'TypeSynergy'
                    ? 'quiet'
                    : '';
            return (
              <div className={`log-line ${tone}`} key={i}>
                {text}
              </div>
            );
          })}
        </div>
      ))}
    </div>
  );
}

/** Plain-language description of an event, using species names rather than instance ids. */
function describeEvent(event: StepEvent | null, names: Map<string, Species>): string | null {
  if (event === null) return null;

  const who = (id: string | undefined): string =>
    id === undefined ? 'someone' : (names.get(id)?.name ?? id);
  const src = who(event.sourceInstanceId);
  const tgt = who(event.targetInstanceId);
  const amt = event.amount ?? 0;

  switch (event.kind) {
    case 'TypeSynergy':
      return `${event.sourceSide === 'A' ? 'Your team' : 'The opponent'}: ${event.synergyType} x${amt}`;
    case 'Damage':
      return amt > 0 ? `${src} hits ${tgt} for ${amt}` : `${src} attacks ${tgt}, no damage`;
    case 'ShieldAbsorbed':
      return `${tgt}'s shield absorbs ${amt}`;
    case 'Heal':
      return `${src} heals ${tgt} for ${amt}`;
    case 'LifestealHeal':
      return `${src} drains back ${amt}`;
    case 'Shield':
      return `${tgt} gains a ${amt} shield`;
    case 'StatusApplied':
      return `${tgt} is ${String(event.status).toLowerCase()}`;
    case 'StatusBlocked':
      return `${tgt} wards off ${String(event.status).toLowerCase()}`;
    case 'StatusCleared':
      return `${tgt} recovers from ${String(event.status).toLowerCase()}`;
    case 'StatusTick':
      return `${tgt} takes ${amt} from ${String(event.status).toLowerCase()}`;
    case 'SuddenDeath':
      return `Sudden death: ${tgt} takes ${amt}`;
    case 'BuffAttack':
      return `${tgt} gains ${amt} attack`;
    case 'BuffSpeed':
      return `${tgt} gains ${amt} speed`;
    case 'ChargeRateModified':
      return `${tgt} charges ${amt > 0 ? 'faster' : 'slower'}`;
    case 'DamageReductionApplied':
      return `${tgt} blocks ${amt} more per hit`;
    case 'Lifesteal':
      return `${tgt} gains ${amt}% lifesteal`;
    case 'PassiveTriggered':
      return `${src} fires its passive`;
    case 'Faint':
      return `${src} faints`;
    case 'BallThrown':
      return `You throw a ball at ${tgt} — ${amt}% odds`;
    case 'Caught':
      return `${src} is caught`;
    case 'Promotion':
      return `${src} steps up`;
    case 'BattleEnd':
      return event.outcome === 'SideAWins'
        ? 'You win'
        : event.outcome === 'SideBWins'
          ? 'You lose'
          : 'Draw';
  }
}
