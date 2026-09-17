/**
 * The run: the Location path, your team, the Encounter you walked into, and what a fight just did.
 *
 * Views over one store — the map you choose from, the Center, an Encounter's scene, the result of
 * the fight you just had, and the end of the run. The battle screen itself is unchanged; it is
 * handed a line-up and an opponent and reports an outcome, and knows nothing about badges, Morale
 * or which kind of node sent it there.
 */

import { useLayoutEffect, useRef, useState } from 'react';

import { speciesOf } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import { displayNameOf, statsOf, type PokemonInstance } from '../content/factory.js';
import type { Stats } from '../sim/index.js';
import { EXP_PER_EVOLUTION, expSinceEvolution } from '../meta/experience.js';
import { canCombine, combine as fuse, partnersFor } from '../meta/fusion.js';
import { BADGES_TO_WIN, MAX_PARTY_SIZE } from '../meta/progression.js';
import { describeBounty } from '../meta/roadEvents.js';
import type { MapNode, NodeType } from '../meta/runState.js';
import { useRunStore } from '../state/runStore.js';
import { SHOP_STOCK, canAfford, describeInventory } from '../meta/shop.js';
import { acceptDrop, readDragPayload, setDragPayload } from './dragDrop.js';
import { EvolutionScene } from './EvolutionScene.js';
import { TeamSynergies } from './TeamSynergies.js';

export function RunHeader() {
  const run = useRunStore((s) => s.run);
  const location = useRunStore((s) => s.location);
  const startRun = useRunStore((s) => s.startRun);

  return (
    <div className="panel run-header">
      <span className="run-location">{location.name}</span>
      <span className="run-stat" title={location.blurb}>
        {location.blurb}
      </span>
      <span className="run-stat" title="Badges earned">
        {run.badges}/{BADGES_TO_WIN} badges
      </span>
      <span className="run-stat morale" title="Lose all your morale and the run ends">
        {'\u2665'.repeat(run.morale) || 'none'} morale
      </span>
      <span className="run-stat" title="Spent at the Pokémon Center">
        ${run.money}
      </span>
      <div className="spacer" />
      <button onClick={() => startRun()}>New run</button>
    </div>
  );
}

/**
 * How each node kind presents itself: the icon drawn on the map, and what it is called.
 *
 * The icons are the ones the Location map was designed around — a black exclamation in the grass
 * for a fight, a trainer silhouette with a ball for the one whose team you cannot see, a gold
 * question mark over a ball for an Encounter, the domed Center, and the crowned Gym. A node is
 * read at a glance from its silhouette, which is the whole reason the map is a map rather than a
 * list; the word underneath is the fallback, not the signal.
 */
const NODE_ART: Record<NodeType, { icon: string; kind: string; hint: string }> = {
  Wild: {
    icon: '/icons/nodes/battle.png',
    kind: 'Battle',
    hint: 'Wild Pokémon. EXP, money, and something you can throw a ball at.',
  },
  Trainer: {
    icon: '/icons/nodes/mystery_trainer.png',
    kind: 'Mystery Trainer',
    hint: 'A bigger team than the grass fields, for better money. You see it when you arrive.',
  },
  Encounter: {
    icon: '/icons/nodes/encounter.png',
    kind: 'Encounter',
    hint: 'A scene and a choice. No EXP from the node itself — everything else is on the table.',
  },
  Center: {
    icon: '/icons/nodes/pokemon_center.png',
    kind: 'Pokémon Center',
    hint: 'Restock balls. Pays no EXP and no money.',
  },
  Gym: {
    icon: '/icons/nodes/gym.png',
    kind: 'Gym',
    hint: 'The Leader. Beat them for the badge and the next Location.',
  },
};

/** A node with the edges the generator wired it with. The map hands these out; the type hides it. */
type EdgedNode = MapNode & { next?: readonly string[] };

/** Where every node landed on screen, measured once the columns have laid themselves out. */
interface Geometry {
  readonly points: Record<string, { x: number; y: number }>;
  readonly width: number;
  readonly height: number;
}

/**
 * The Location's branching path, with your traveller on it.
 *
 * Laid out as columns of layers, entry on the left and the Gym on the right, over the Location's
 * own art. Only the nodes the current position leads to are takeable — the decision is which
 * fight you give up, so the ones being passed on have to stay visible rather than disappear.
 *
 * **The edges are drawn, not implied.** A column of buttons tells you what you may take next but
 * not what any of it leads to, and the whole point of a branching map is that a choice two layers
 * out is visible from here. The lines are measured from the DOM rather than computed, because the
 * columns are laid out by flexbox and their positions depend on how the panel wrapped.
 *
 * The token is the thing that makes the map a journey rather than a menu. It sits on the last
 * node taken and slides to the next one, so progress is something you watch happen rather than
 * infer from which buttons went grey.
 */
export function LocationMap() {
  const run = useRunStore((s) => s.run);
  const map = useRunStore((s) => s.map);
  const location = useRunStore((s) => s.location);
  const available = useRunStore((s) => s.available);
  const enter = useRunStore((s) => s.enter);

  const boardRef = useRef<HTMLDivElement>(null);
  const nodeRefs = useRef(new Map<string, HTMLButtonElement>());
  const [geometry, setGeometry] = useState<Geometry | null>(null);

  const here = run.visited.length > 0 ? run.visited[run.visited.length - 1]! : null;

  useLayoutEffect(() => {
    const board = boardRef.current;
    if (board === null) return;

    const measure = (): void => {
      const b = board.getBoundingClientRect();
      const points: Record<string, { x: number; y: number }> = {};
      for (const [id, el] of nodeRefs.current) {
        const n = el.getBoundingClientRect();
        points[id] = { x: n.left - b.left + n.width / 2, y: n.top - b.top + n.height / 2 };
      }
      setGeometry({ points, width: board.scrollWidth, height: board.scrollHeight });
    };

    measure();
    window.addEventListener('resize', measure);
    return () => window.removeEventListener('resize', measure);
  }, [map, run.visited.length]);

  const byLayer = new Map<number, typeof map.nodes>();
  for (const node of map.nodes) {
    byLayer.set(node.layer, [...(byLayer.get(node.layer) ?? []), node]);
  }
  const layers = [...byLayer.keys()].sort((a, b) => a - b);

  const token = here === null ? null : (geometry?.points[here] ?? null);

  return (
    <div
      className={`panel location-map region-${location.slug}`}
      style={{
        // The art sits under a scrim so white node labels stay readable over a bright desert or a
        // pale tundra; the tint is what shows through if the file isn't there at all.
        backgroundColor: location.tint,
        backgroundImage: `linear-gradient(rgb(6 9 14 / 0.62), rgb(6 9 14 / 0.62)), url('${location.art}')`,
      }}
    >
      <div className="layer-row" ref={boardRef}>
        {geometry !== null && (
          <svg
            className="map-edges"
            width={geometry.width}
            height={geometry.height}
            aria-hidden="true"
          >
            {map.nodes.flatMap((node) =>
              ((node as EdgedNode).next ?? []).map((toId) => {
                const from = geometry.points[node.id];
                const to = geometry.points[toId];
                if (from === undefined || to === undefined) return null;
                // An edge is lit when it is one of the moves on offer right now, and dimmed once
                // its source is behind you — the path you walked stays legible without competing
                // with the choice in front of you.
                const open = node.id === here && available.includes(toId);
                const spent = run.visited.includes(node.id);
                return (
                  <line
                    key={`${node.id}->${toId}`}
                    className={`map-edge ${open ? 'open' : spent ? 'spent' : ''}`}
                    x1={from.x}
                    y1={from.y}
                    x2={to.x}
                    y2={to.y}
                  />
                );
              }),
            )}
          </svg>
        )}

        {layers.map((layer) => (
          <div className="layer" key={layer}>
            {byLayer.get(layer)!.map((node) => {
              const done = run.visited.includes(node.id);
              const open = available.includes(node.id);
              const art = NODE_ART[node.type];
              return (
                <button
                  key={node.id}
                  ref={(el) => {
                    if (el === null) nodeRefs.current.delete(node.id);
                    else nodeRefs.current.set(node.id, el);
                  }}
                  className={`map-node ${node.type.toLowerCase()} ${done ? 'done' : ''} ${open ? 'available' : ''} ${node.id === here ? 'here' : ''}`}
                  disabled={!open}
                  onClick={() => enter(node.id)}
                  title={
                    open
                      ? `${art.kind} — ${art.hint}`
                      : done
                        ? `${art.kind}: already taken`
                        : `${art.kind} — not reachable from here`
                  }
                >
                  <img className="node-icon" src={art.icon} alt="" />
                  <span className="node-kind">{art.kind}</span>
                  {/* A Center and a Mystery Trainer are their own label; printing it twice under
                      the icon is noise where the flavour line should be. */}
                  {node.label !== art.kind && <span className="node-label">{node.label}</span>}
                </button>
              );
            })}
          </div>
        ))}

        {token !== null && (
          <div className="traveller" style={{ left: token.x, top: token.y }} aria-hidden="true">
            <span className="traveller-dot" />
          </div>
        )}
      </div>
      {available.length === 0 && <p className="map-note">No way forward from here.</p>}
    </div>
  );
}

/**
 * An Encounter: a scene, a short list of choices, and what the one you took did.
 *
 * Every choice states its trade in full before it is clicked — the cost, the odds where there are
 * any, and what lands in the run. An encounter whose price is a surprise is a trap, and a trap is
 * a node a player learns to walk around, which costs the map a whole kind of node.
 *
 * A branch that starts a fight leaves this screen for the battle and never comes back to it; the
 * result panel behind the fight pays the bounty and narrates the whole node at once.
 */
export function EncounterPanel() {
  const event = useRunStore((s) => s.event);
  const result = useRunStore((s) => s.eventResult);
  const choose = useRunStore((s) => s.chooseEncounter);
  const leave = useRunStore((s) => s.leaveEncounter);
  const [seenEvolutions, setSeenEvolutions] = useState(false);

  if (event === null) return null;

  const evolutions = result?.report.evolutions ?? [];
  if (evolutions.length > 0 && !seenEvolutions) {
    return <EvolutionScene evolutions={evolutions} onDone={() => setSeenEvolutions(true)} />;
  }

  return (
    <div className="panel encounter-panel">
      <img className="encounter-mark" src={NODE_ART.Encounter.icon} alt="" />
      <h2>{event.title}</h2>
      {/* The byline is who you are dealing with. Dropped when it is only the title again. */}
      {event.speaker.toLowerCase() !== event.title.toLowerCase().replace(/^the /, '') && (
        <p className="encounter-speaker">{event.speaker}</p>
      )}
      <p className="encounter-body">{event.body}</p>

      {result === null ? (
        <div className="encounter-choices">
          {event.choices.map((choice, index) => (
            <button
              key={choice.label}
              className="encounter-choice"
              disabled={!choice.available}
              onClick={() => choose(index)}
              title={choice.available ? choice.detail : 'Not something you can do right now'}
            >
              <span className="choice-label">{choice.label}</span>
              <span className="choice-detail">{choice.detail}</span>
            </button>
          ))}
        </div>
      ) : (
        <>
          <p className="encounter-result">{result.message}</p>
          <button className="primary" onClick={leave}>
            Move on
          </button>
        </>
      )}
    </div>
  );
}

/** The Pokemon Center: a shop, since damage already resets after every fight. */
export function ShopPanel() {
  const run = useRunStore((s) => s.run);
  const purchase = useRunStore((s) => s.purchase);
  const leaveShop = useRunStore((s) => s.leaveShop);

  return (
    <div className="panel shop-panel">
      <h2>Pokémon Center</h2>
      <p className="shop-sub">
        Carrying {describeInventory(run.balls)} &middot; ${run.money}
      </p>
      <p className="shop-sub quiet">
        Your team is already healed after every fight. What is for sale is the thing you actually
        run out of.
      </p>

      <div className="shop-stock">
        {SHOP_STOCK.map((item) => {
          const affordable = canAfford(run.money, item);
          return (
            <button
              key={item.id}
              className="shop-item"
              disabled={!affordable}
              onClick={() => purchase(item)}
              title={affordable ? `Buy for $${item.cost}` : 'Not enough money'}
            >
              <span className="shop-item-name">{item.name}</span>
              <span className="shop-item-cost">${item.cost}</span>
              <span className="shop-item-blurb">{item.blurb}</span>
            </button>
          );
        })}
      </div>

      <button className="primary" onClick={leaveShop}>
        Move on
      </button>
    </div>
  );
}

/**
 * Combining, as a two-step choice: press ⊕ on a mon, then pick what it folds into.
 *
 * **Only ⊕ arms a combine.** A plain drag used to arm one too, which meant dragging a Charmander
 * past another Charmander lit that card up as a merge target and dropping on it destroyed a
 * Pokémon when all the player wanted was to change the batting order. One gesture, one verb: a
 * drag moves a mon, ⊕ combines two. Once ⊕ is pressed, either clicking a partner or dropping the
 * armed mon on it commits the merge.
 *
 * The cards light up from the *real* candidate test in `/src/meta/fusion` — a card only glows
 * where a merge would actually land. Highlighting everything and refusing on drop would teach the
 * rule by failure.
 */
interface CombineSelection {
  /** The mon waiting for a partner, once ⊕ has been pressed on it. */
  pendingId: string | null;
  /** Whether this mon has anything at all to merge with, which is what enables its button. */
  hasPartner: (mon: PokemonInstance) => boolean;
  /** Whether the armed mon would merge into this one. False whenever nothing is armed. */
  isCandidate: (mon: PokemonInstance) => boolean;
  /** Whether dropping `draggedId` on this mon should merge rather than move. */
  wouldMerge: (draggedId: string | null, mon: PokemonInstance) => boolean;
  /** The stat line the merge would produce, so the choice is made on numbers, not faith. */
  previewFor: (mon: PokemonInstance) => Stats | null;
  toggle: (instanceId: string) => void;
  commit: (targetId: string) => void;
  cancel: () => void;
}

function useCombineSelection(pool: readonly PokemonInstance[]): CombineSelection {
  const applyCombine = useRunStore((s) => s.combine);
  const [pendingId, setPendingId] = useState<string | null>(null);

  const source = pendingId === null ? null : (pool.find((m) => m.instanceId === pendingId) ?? null);
  const isCandidate = (mon: PokemonInstance): boolean =>
    source !== null && canCombine(source, mon);

  return {
    pendingId,
    hasPartner: (mon) => partnersFor(mon, pool).length > 0,
    isCandidate,
    wouldMerge: (draggedId, mon) => draggedId !== null && draggedId === pendingId && isCandidate(mon),
    previewFor: (mon) => {
      if (source === null) return null;
      // The target goes first, so a tie over which form survives falls to the mon the player
      // aimed at — the same call the store will make when this is committed.
      const fusion = fuse(mon, source);
      return fusion === null ? null : statsOf(fusion.mon);
    },
    toggle: (instanceId) => setPendingId((current) => (current === instanceId ? null : instanceId)),
    commit: (targetId) => {
      if (pendingId === null) return;
      applyCombine(targetId, pendingId);
      setPendingId(null);
    },
    cancel: () => setPendingId(null),
  };
}

/** The one button that both starts a combine and finishes it, depending on what is armed. */
function CombineButton({
  mon,
  selection,
}: {
  mon: PokemonInstance;
  selection: CombineSelection;
}) {
  if (selection.isCandidate(mon)) {
    return (
      <button
        className="combine-go"
        onClick={() => selection.commit(mon.instanceId)}
        title={`Combine into ${displayNameOf(mon)}`}
      >
        ⊕
      </button>
    );
  }

  const selected = selection.pendingId === mon.instanceId;
  const partnered = selection.hasPartner(mon);
  return (
    <button
      className={selected ? 'combine-on' : ''}
      disabled={!partnered}
      onClick={() => selection.toggle(mon.instanceId)}
      title={
        selected
          ? 'Cancel combine'
          : partnered
            ? 'Combine with another of its family'
            : 'Nothing of its family to combine with'
      }
    >
      ⊕
    </button>
  );
}

/** What the last combine produced. Stays until the next fight starts, then clears itself. */
function FusionNote() {
  const fusion = useRunStore((s) => s.lastFusion);
  if (fusion === null) return null;

  const stats = statsOf(fusion.mon);
  return (
    <p className="fusion-note">
      {displayNameOf(fusion.mon)} absorbed its own kind — now {stats.attack} atk &middot;{' '}
      {stats.health} hp
      {fusion.evolutions.map((e) => (
        <span key={e.instanceId + e.to.id}>
          {' '}
          and evolved into {e.to.name}
        </span>
      ))}
      .
    </p>
  );
}

/** Which mon is in hand. Owned by the screen, so every slot on it can show itself as a target. */
function useMonDrag() {
  const [draggingId, setDraggingId] = useState<string | null>(null);
  return {
    draggingId,
    begin: (instanceId: string) => setDraggingId(instanceId),
    end: () => setDraggingId(null),
  };
}

/**
 * One position on a roster: the mon standing in it, or the outline waiting for one.
 *
 * **Every slot is a drop target, filled or empty, and it owns the whole drop.** The card inside it
 * only starts drags. That is what fixed reordering: while the card was also a drop target, a drop
 * either merged (destroying a mon), or missed the card and fell through to the row, which appended
 * it to the end — so the same gesture did three different things depending on a few pixels.
 *
 * The slot says what it will do *before* the drop: the mon that would be displaced is outlined and
 * labelled, and an empty slot lights up with where the mon would land. Nothing about a drag should
 * have to be discovered by doing it and looking at the result.
 */
function RosterSlot({
  index,
  mon,
  draggingId,
  selection,
  onDropMon,
  emptyHint,
  children,
}: {
  /** The slot's position, for the number in its corner. Null in the Box, which has no order. */
  index: number | null;
  mon: PokemonInstance | null;
  draggingId: string | null;
  selection?: CombineSelection;
  onDropMon: (instanceId: string) => void;
  emptyHint?: string;
  children?: React.ReactNode;
}) {
  const [over, setOver] = useState(false);

  const isSelf = mon !== null && mon.instanceId === draggingId;
  const merges = mon !== null && (selection?.wouldMerge(draggingId, mon) ?? false);
  // A drag is in flight and this is not the slot it came from, so something would happen here.
  const accepts = draggingId !== null && !isSelf;
  const verb = merges ? 'Combine' : mon === null ? 'Place here' : 'Swap';

  return (
    <div
      className={`roster-slot ${mon === null ? 'empty' : 'filled'} ${accepts ? 'targetable' : ''} ${over && accepts ? 'over' : ''} ${merges ? 'merging' : ''}`}
      onDragEnter={(event) => {
        if (!accepts) return;
        acceptDrop(event);
        setOver(true);
      }}
      onDragOver={(event) => {
        if (!accepts) return;
        acceptDrop(event);
      }}
      onDragLeave={() => setOver(false)}
      onDrop={(event) => {
        event.preventDefault();
        // Stopped, or the grid underneath treats the same drop as its own and undoes this one.
        event.stopPropagation();
        setOver(false);
        const payload = readDragPayload(event);
        if (payload === null || payload.kind !== 'mon') return;
        if (payload.instanceId === mon?.instanceId) return;
        if (merges && mon !== null) {
          selection?.commit(mon.instanceId);
          return;
        }
        onDropMon(payload.instanceId);
      }}
    >
      {mon === null ? (
        <div className="slot-empty">
          {index !== null && <span className="slot-number">{index + 1}</span>}
          <span className="slot-empty-hint">{emptyHint ?? 'empty'}</span>
        </div>
      ) : (
        children
      )}
      {over && accepts && <span className="slot-verb">{verb}</span>}
    </div>
  );
}

/**
 * The line-up, laid out horizontally under the map.
 *
 * Order is the whole of formation — slot 0 fights, slot 1 is the Support a passive can target,
 * and everything past that waits — so the builder is a row of numbered slots rather than a list.
 * **All six slots are always drawn**, the empty ones included: the line-up's size is a decision
 * the player is making every turn, and a row that only shows what you already have hides both how
 * much room is left and where a mon from the Box would go.
 *
 * Dragging swaps: drop a mon on another and the two trade places. The arrows stay for adjacent
 * moves, because a drag is impossible with a keyboard and awkward on a trackpad.
 */
export function TeamBuilder() {
  const run = useRunStore((s) => s.run);
  const moveToBox = useRunStore((s) => s.moveToBox);
  const reorder = useRunStore((s) => s.reorder);
  const dropOnSlot = useRunStore((s) => s.dropOnSlot);
  const openBox = useRunStore((s) => s.openBox);

  const drag = useMonDrag();
  const selection = useCombineSelection(run.lineUp);
  const slots = Array.from({ length: MAX_PARTY_SIZE }, (_, i) => run.lineUp[i] ?? null);

  return (
    <div className="panel team-builder">
      <div className="builder-head">
        <span className="team-heading">
          Line-up <span className="team-hint">slot 1 leads, slot 2 supports, the rest wait</span>
        </span>
        <div className="spacer" />
        <button onClick={openBox}>Box ({run.box.length})</button>
      </div>

      <TeamSynergies mons={run.lineUp} />

      <div className="slot-row">
        {slots.map((mon, index) => (
          <RosterSlot
            key={mon?.instanceId ?? `empty-${index}`}
            index={index}
            mon={mon}
            draggingId={drag.draggingId}
            selection={selection}
            onDropMon={(instanceId) => dropOnSlot(instanceId, index)}
            emptyHint={index < run.lineUp.length + 1 ? 'drag a mon here' : 'empty'}
          >
            {mon !== null && (
              <TeamCard
                mon={mon}
                index={index}
                selection={selection}
                onDragStart={(e) => {
                  setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'lineUp' });
                  drag.begin(mon.instanceId);
                }}
                onDragEnd={drag.end}
                actions={
                  <>
                    <button
                      disabled={index === 0}
                      onClick={() => reorder(mon.instanceId, index - 1)}
                      title="Move forward"
                    >
                      ‹
                    </button>
                    <button
                      disabled={index === run.lineUp.length - 1}
                      onClick={() => reorder(mon.instanceId, index + 1)}
                      title="Move back"
                    >
                      ›
                    </button>
                    <CombineButton mon={mon} selection={selection} />
                    <button
                      disabled={run.lineUp.length <= 1}
                      onClick={() => moveToBox(mon.instanceId)}
                      title="Send to the Box"
                    >
                      ✕
                    </button>
                  </>
                }
              />
            )}
          </RosterSlot>
        ))}
      </div>

      {selection.pendingId !== null && (
        <p className="combine-hint">
          Pick another of the same family to combine with — or press ⊕ again to cancel.
        </p>
      )}
      <FusionNote />
    </div>
  );
}

/** How many empty slots the Box shows under whatever is in it, so it is always a visible target. */
const MIN_BOX_SLOTS = 6;

/** The Box: everything caught but not carried. */
export function BoxScreen() {
  const run = useRunStore((s) => s.run);
  const closeBox = useRunStore((s) => s.closeBox);
  const moveToLineUp = useRunStore((s) => s.moveToLineUp);
  const moveToBox = useRunStore((s) => s.moveToBox);
  const dropOnSlot = useRunStore((s) => s.dropOnSlot);

  const [overBox, setOverBox] = useState(false);
  const drag = useMonDrag();
  const full = run.lineUp.length >= MAX_PARTY_SIZE;
  // Everything the player owns, because this is the screen where a boxed Charmander can be fed
  // to the Charizard that is fighting.
  const selection = useCombineSelection([...run.lineUp, ...run.box]);

  const lineUpSlots = Array.from({ length: MAX_PARTY_SIZE }, (_, i) => run.lineUp[i] ?? null);
  // Enough outlines that the Box reads as somewhere things go, rather than as blank panel below a
  // heading — and so an empty Box still has something to aim a drag at.
  const boxSlots = [
    ...run.box,
    ...Array.from({ length: Math.max(MIN_BOX_SLOTS - run.box.length, 1) }, () => null),
  ];

  const cardFor = (mon: PokemonInstance, index: number | null, from: 'lineUp' | 'box') => (
    <TeamCard
      mon={mon}
      index={index ?? undefined}
      selection={selection}
      onDragStart={(e) => {
        setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from });
        drag.begin(mon.instanceId);
      }}
      onDragEnd={drag.end}
      actions={
        <>
          <CombineButton mon={mon} selection={selection} />
          {from === 'lineUp' ? (
            <button
              disabled={run.lineUp.length <= 1}
              onClick={() => moveToBox(mon.instanceId)}
              title="Send to the Box"
            >
              ✕
            </button>
          ) : (
            <button
              disabled={full}
              onClick={() => moveToLineUp(mon.instanceId)}
              title={full ? 'Line-up is full — drag it onto a slot to swap' : 'Add to the line-up'}
            >
              +
            </button>
          )}
        </>
      }
    />
  );

  return (
    <div className="panel box-screen">
      <div className="builder-head">
        <h2>Your Pokémon</h2>
        <div className="spacer" />
        <button className="primary" onClick={closeBox}>
          Back to the map
        </button>
      </div>

      <p className="shop-sub quiet">
        Drag a mon onto a slot to put it there — onto an occupied one and the two swap, so a full
        line-up is still something you can trade into. Mons in the Box earn no EXP; one that didn't
        fight doesn't grow. Two of one family can be combined with ⊕, wherever either is kept.
      </p>

      <TeamSynergies mons={run.lineUp} detailed label="Type buffs your line-up carries into the next fight" />

      <div className="box-heading">
        Line-up ({run.lineUp.length}/{MAX_PARTY_SIZE})
      </div>
      <div className="box-grid">
        {lineUpSlots.map((mon, index) => (
          <RosterSlot
            key={mon?.instanceId ?? `line-empty-${index}`}
            index={index}
            mon={mon}
            draggingId={drag.draggingId}
            selection={selection}
            onDropMon={(instanceId) => dropOnSlot(instanceId, index)}
            emptyHint="drag a mon here"
          >
            {mon !== null && cardFor(mon, index, 'lineUp')}
          </RosterSlot>
        ))}
      </div>

      <div className="box-heading">Box ({run.box.length})</div>
      <div
        className={`box-grid box-store ${overBox ? 'over' : ''}`}
        onDragEnter={(e) => {
          acceptDrop(e);
          setOverBox(true);
        }}
        onDragOver={acceptDrop}
        onDragLeave={() => setOverBox(false)}
        onDrop={(e) => {
          e.preventDefault();
          setOverBox(false);
          const payload = readDragPayload(e);
          if (payload?.kind === 'mon' && payload.from === 'lineUp') moveToBox(payload.instanceId);
        }}
      >
        {boxSlots.map((mon, index) => (
          <RosterSlot
            key={mon?.instanceId ?? `box-empty-${index}`}
            index={null}
            mon={mon}
            draggingId={drag.draggingId}
            selection={selection}
            // A slot in the Box is storage, not a position: whatever lands here is simply benched.
            onDropMon={(instanceId) => moveToBox(instanceId)}
            emptyHint="store a mon here"
          >
            {mon !== null && cardFor(mon, null, 'box')}
          </RosterSlot>
        ))}
      </div>

      {selection.pendingId !== null && (
        <p className="combine-hint">
          Pick another of the same family to combine with — or press ⊕ again to cancel.
        </p>
      )}
      <FusionNote />
    </div>
  );
}

/**
 * One mon, as a draggable card. Used by the line-up row and by both grids in the Box.
 *
 * Drags start here and end here; **the slot around it owns the drop.** A card that was also a drop
 * target competed with the slot underneath it for the same pixels, and which one won decided
 * whether a drag reordered the team or merged two mons together.
 */
function TeamCard({
  mon,
  index,
  actions,
  onDragStart,
  onDragEnd,
  selection,
}: {
  mon: PokemonInstance;
  index?: number;
  actions?: React.ReactNode;
  onDragStart?: (event: React.DragEvent) => void;
  onDragEnd?: () => void;
  selection?: CombineSelection;
}) {
  const species = speciesOf(mon.speciesId);
  if (species === null) return null;
  const stats = statsOf(mon);

  const since = expSinceEvolution(mon);
  const canEvolve = species.evolvesIntoId !== null;

  const selected = selection?.pendingId === mon.instanceId;
  const candidate = selection?.isCandidate(mon) ?? false;
  const merged = candidate ? (selection?.previewFor(mon) ?? null) : null;
  const fused = mon.timesFused ?? 0;

  return (
    <div
      className={`team-card ${index === 0 ? 'lead' : index === 1 ? 'support' : ''} ${selected === true ? 'combining' : ''} ${candidate ? 'combine-candidate' : ''}`}
      draggable={onDragStart !== undefined}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
    >
      {index !== undefined && <span className="slot-number">{index + 1}</span>}
      {/* Not draggable in its own right: a native image drag starts from the sprite instead of
          the card, which hands the drop target a picture rather than a mon. */}
      <img src={spriteUrl(species, { facing: 'front' })} alt="" draggable={false} />
      <div className="team-meta">
        <span className="team-name">
          {species.name}
          {fused > 0 && (
            <em title={`${fused} mon${fused === 1 ? '' : 's'} folded into this one`}>⊕{fused}</em>
          )}
        </span>
        <span className="team-figures">
          {stats.attack} atk · {stats.health} hp · spd {stats.speed}
        </span>
        {merged !== null ? (
          <span className="team-preview">
            → {merged.attack} atk · {merged.health} hp if combined
          </span>
        ) : (
          <span className="team-exp" title={`${mon.exp} lifetime EXP`}>
            {canEvolve ? `${since}/${EXP_PER_EVOLUTION} to evolve` : `${mon.exp} exp`}
          </span>
        )}
        <span className="team-types">
          {species.types.map((t) => (
            <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} title={t} />
          ))}
        </span>
      </div>
      {actions !== undefined && <div className="team-actions">{actions}</div>}
    </div>
  );
}

/**
 * Evolutions earned by the fight that just ended, played once before the screen behind them.
 *
 * Keyed by the node and by what evolved, so dismissing the ceremony doesn't re-arm it on the next
 * render and the next fight gets its own.
 */
function useEvolutionCeremony() {
  const result = useRunStore((s) => s.lastResult);
  const [seen, setSeen] = useState<string | null>(null);

  const evolutions = result?.report.evolutions ?? [];
  const key =
    result === null ? null : `${result.nodeId}:${evolutions.map((e) => e.instanceId + e.to.id).join(',')}`;

  return {
    evolutions,
    pending: key !== null && evolutions.length > 0 && seen !== key,
    finish: () => setSeen(key),
  };
}

export function ResultPanel() {
  const result = useRunStore((s) => s.lastResult);
  const dismiss = useRunStore((s) => s.dismissResult);
  const ceremony = useEvolutionCeremony();
  if (result === null) return null;

  // The ceremony comes first and the tally waits behind it: an evolution is the one thing a fight
  // produces that is worth stopping for, and it cannot compete with a list it is an item in.
  if (ceremony.pending) {
    return <EvolutionScene evolutions={ceremony.evolutions} onDone={ceremony.finish} />;
  }

  const won = result.outcome === 'SideAWins';

  return (
    <div className="panel result-panel">
      <h2 className={won ? 'won' : 'lost'}>
        {won ? 'Victory' : result.outcome === 'Draw' ? 'Draw' : 'Defeat'}
      </h2>
      <ul className="result-list">
        {won && <li>+{result.expEach} EXP to everyone who fought</li>}
        {result.money > 0 && <li>+${result.money}</li>}
        {result.bounty !== null && <li className="good">Bounty: {describeBounty(result.bounty)}</li>}
        {result.badge && <li>Badge earned</li>}
        {result.moraleLost > 0 && <li className="bad">-{result.moraleLost} morale</li>}
        {result.report.evolutions.map((e) => (
          <li className="good" key={e.instanceId + e.to.id}>
            {e.from.name} evolved into {e.to.name}
          </li>
        ))}
        <li className="quiet">Everyone is back to full health</li>
      </ul>
      <button className="primary" onClick={dismiss}>
        Continue
      </button>
    </div>
  );
}

export function RunOverPanel() {
  const run = useRunStore((s) => s.run);
  const startRun = useRunStore((s) => s.startRun);
  const ceremony = useEvolutionCeremony();
  const won = run.badges >= BADGES_TO_WIN;

  // The last fight of a run evolves things too, and the run ending is no reason to skip it.
  if (ceremony.pending) {
    return <EvolutionScene evolutions={ceremony.evolutions} onDone={ceremony.finish} />;
  }

  return (
    <div className="panel result-panel">
      <h2 className={won ? 'won' : 'lost'}>{won ? 'Run complete' : 'Run over'}</h2>
      <p>
        {won
          ? `All ${BADGES_TO_WIN} badges.`
          : `Out of morale after ${run.badges} badge${run.badges === 1 ? '' : 's'}.`}
      </p>
      <button className="primary" onClick={() => startRun()}>
        Start another run
      </button>
    </div>
  );
}
