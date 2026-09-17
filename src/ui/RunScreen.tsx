/**
 * The run: the Location path, your team, and what a fight just did.
 *
 * Three views over one store — the map you choose from, the result of the fight you just had, and
 * the end of the run. The battle screen itself is unchanged; it is handed a line-up and an
 * opponent and reports an outcome, and knows nothing about badges or Morale.
 */

import { Fragment, useLayoutEffect, useRef, useState } from 'react';

import { speciesOf } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import { displayNameOf, statsOf, type PokemonInstance } from '../content/factory.js';
import type { Stats } from '../sim/index.js';
import { EXP_PER_EVOLUTION, expSinceEvolution } from '../meta/experience.js';
import { canCombine, combine as fuse, partnersFor } from '../meta/fusion.js';
import { BADGES_TO_WIN, MAX_PARTY_SIZE } from '../meta/progression.js';
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

const nodeKind = (node: { type: string }): string =>
  node.type === 'Gym' ? 'Gym' : node.type === 'Center' ? 'Shop' : 'Wild';

/**
 * The Location's branching path, with your traveller on it.
 *
 * Laid out as columns of layers, entry on the left and the Gym on the right. Only the nodes the
 * current position leads to are takeable — the decision is which fight you give up, so the ones
 * being passed on have to stay visible rather than disappear.
 *
 * The token is the thing that makes the map a journey rather than a menu. It sits on the last
 * node taken and slides to the next one, so progress is something you watch happen rather than
 * infer from which buttons went grey.
 */
export function LocationMap() {
  const run = useRunStore((s) => s.run);
  const map = useRunStore((s) => s.map);
  const available = useRunStore((s) => s.available);
  const enter = useRunStore((s) => s.enter);

  const boardRef = useRef<HTMLDivElement>(null);
  const nodeRefs = useRef(new Map<string, HTMLButtonElement>());
  const [token, setToken] = useState<{ left: number; top: number } | null>(null);

  const here = run.visited.length > 0 ? run.visited[run.visited.length - 1]! : null;

  // Measured from the DOM rather than computed from the layout, because the layer columns are
  // laid out by flexbox and their positions depend on how the panel wrapped.
  useLayoutEffect(() => {
    const board = boardRef.current;
    if (board === null) return;

    const place = (): void => {
      const target = here === null ? null : nodeRefs.current.get(here);
      if (target === undefined || target === null) {
        setToken(null);
        return;
      }
      const b = board.getBoundingClientRect();
      const n = target.getBoundingClientRect();
      setToken({ left: n.left - b.left + n.width / 2, top: n.top - b.top - 14 });
    };

    place();
    window.addEventListener('resize', place);
    return () => window.removeEventListener('resize', place);
  }, [here, map]);

  const byLayer = new Map<number, typeof map.nodes>();
  for (const node of map.nodes) {
    byLayer.set(node.layer, [...(byLayer.get(node.layer) ?? []), node]);
  }
  const layers = [...byLayer.keys()].sort((a, b) => a - b);

  return (
    <div className="panel location-map">
      <div className="layer-row" ref={boardRef}>
        {layers.map((layer) => (
          <div className="layer" key={layer}>
            {byLayer.get(layer)!.map((node) => {
              const done = run.visited.includes(node.id);
              const open = available.includes(node.id);
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
                      ? `Take this path: ${node.label}`
                      : done
                        ? 'Already taken'
                        : 'Not reachable from here'
                  }
                >
                  <span className="node-kind">{nodeKind(node)}</span>
                  <span className="node-label">{node.label}</span>
                </button>
              );
            })}
          </div>
        ))}

        {token !== null && (
          <div className="traveller" style={{ left: token.left, top: token.top }} aria-hidden="true">
            <span className="traveller-dot" />
          </div>
        )}
      </div>
      {available.length === 0 && <p className="map-note">No way forward from here.</p>}
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
 * Combining, as a two-step choice: pick a mon, then pick what it merges into.
 *
 * Dragging one mon onto another is the better verb — you are folding one thing into another — so
 * that is what the cards highlight for. The click path is here for the same reason the reorder
 * arrows are: a drag is impossible with a keyboard and awkward on a trackpad, and this move
 * destroys a Pokémon, so it must not be drag-only.
 *
 * Whichever path is in flight, the *same* candidate test lights the cards up, and it is the real
 * one from `/src/meta/fusion` — a card only glows when dropping there would actually merge.
 * Highlighting everything and refusing on drop would teach the rule by failure.
 */
interface CombineSelection {
  /** The mon waiting for a partner, if the player picked one by clicking. */
  pendingId: string | null;
  /** Whether this mon has anything at all to merge with, which is what enables its button. */
  hasPartner: (mon: PokemonInstance) => boolean;
  /** Whether what is in flight — dragged or picked — would merge into this mon. */
  isCandidate: (mon: PokemonInstance) => boolean;
  /** The stat line the merge would produce, so the choice is made on numbers, not faith. */
  previewFor: (mon: PokemonInstance) => Stats | null;
  toggle: (instanceId: string) => void;
  commit: (targetId: string) => void;
  beginDrag: (instanceId: string) => void;
  endDrag: () => void;
  dropOnto: (targetId: string, event: React.DragEvent) => void;
}

function useCombineSelection(pool: readonly PokemonInstance[]): CombineSelection {
  const applyCombine = useRunStore((s) => s.combine);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [draggingId, setDraggingId] = useState<string | null>(null);

  const find = (id: string | null): PokemonInstance | null =>
    id === null ? null : (pool.find((m) => m.instanceId === id) ?? null);

  // A drag beats a pending pick: if the player has started dragging, that is the mon they mean.
  const source = find(draggingId) ?? find(pendingId);

  return {
    pendingId,
    hasPartner: (mon) => partnersFor(mon, pool).length > 0,
    isCandidate: (mon) => source !== null && canCombine(source, mon),
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
    beginDrag: (instanceId) => setDraggingId(instanceId),
    endDrag: () => setDraggingId(null),
    dropOnto: (targetId, event) => {
      const payload = readDragPayload(event);
      setDraggingId(null);
      setPendingId(null);
      if (payload === null || payload.kind !== 'mon') return;
      applyCombine(targetId, payload.instanceId);
    },
  };
}

/** The one button that both starts a combine and finishes it, depending on what is in flight. */
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

/**
 * The line-up, laid out horizontally under the map.
 *
 * Order is the whole of formation — slot 0 fights, slot 1 is the Support a passive can target,
 * and everything past that waits — so the builder is a row of numbered slots rather than a list,
 * and reordering is a drag rather than a pair of arrows. The arrows stay anyway: a drag is
 * impossible with a keyboard and awkward on a trackpad.
 *
 * Drop targets are the gaps *between* slots, not the cards themselves. Dropping "onto" a card is
 * ambiguous — before it or after it? — and the ambiguity shows up as mons landing one slot off
 * from where they were aimed.
 */
export function TeamBuilder() {
  const run = useRunStore((s) => s.run);
  const moveToBox = useRunStore((s) => s.moveToBox);
  const reorder = useRunStore((s) => s.reorder);
  const placeInLineUp = useRunStore((s) => s.placeInLineUp);
  const openBox = useRunStore((s) => s.openBox);

  const [dropIndex, setDropIndex] = useState<number | null>(null);
  const selection = useCombineSelection(run.lineUp);

  const onDropAt = (index: number) => (event: React.DragEvent) => {
    event.preventDefault();
    // Stopped, or the drop bubbles from the gap up to the row, whose own handler appends the mon
    // to the end and silently undoes the placement that just happened.
    event.stopPropagation();
    setDropIndex(null);
    selection.endDrag();
    const payload = readDragPayload(event);
    if (payload === null || payload.kind !== 'mon') return;
    placeInLineUp(payload.instanceId, index);
  };

  const gap = (index: number) => (
    <div
      key={`gap-${index}`}
      className={`slot-gap ${dropIndex === index ? 'over' : ''}`}
      onDragEnter={(e) => {
        acceptDrop(e);
        setDropIndex(index);
      }}
      onDragOver={acceptDrop}
      onDragLeave={() => setDropIndex((d) => (d === index ? null : d))}
      onDrop={onDropAt(index)}
    />
  );

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

      <div
        className="slot-row"
        onDragOver={acceptDrop}
        onDrop={onDropAt(run.lineUp.length)}
      >
        {gap(0)}
        {run.lineUp.map((mon, index) => (
          <Fragment key={mon.instanceId}>
            <TeamCard
              mon={mon}
              index={index}
              selection={selection}
              onDragStart={(e) => {
                setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'lineUp' });
                selection.beginDrag(mon.instanceId);
              }}
              onDragEnd={selection.endDrag}
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
            {gap(index + 1)}
          </Fragment>
        ))}

        {run.lineUp.length < MAX_PARTY_SIZE && (
          <div className="slot-empty">
            <span>{MAX_PARTY_SIZE - run.lineUp.length} free</span>
            <span className="team-hint">drag from the Box</span>
          </div>
        )}
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

/** The Box: everything caught but not carried. */
export function BoxScreen() {
  const run = useRunStore((s) => s.run);
  const closeBox = useRunStore((s) => s.closeBox);
  const moveToLineUp = useRunStore((s) => s.moveToLineUp);
  const moveToBox = useRunStore((s) => s.moveToBox);
  const placeInLineUp = useRunStore((s) => s.placeInLineUp);

  const [overLineUp, setOverLineUp] = useState(false);
  const [overBox, setOverBox] = useState(false);
  const full = run.lineUp.length >= MAX_PARTY_SIZE;
  // Everything the player owns, because this is the screen where a boxed Charmander can be fed
  // to the Charizard that is fighting.
  const selection = useCombineSelection([...run.lineUp, ...run.box]);

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
        Mons in the Box earn no EXP — one that didn't fight doesn't grow. Move one into the line-up
        and the next win's catch-up brings it back within a couple of points of the rest. Two of
        one family can be combined into a single stronger mon, wherever either of them is kept.
      </p>

      <TeamSynergies mons={run.lineUp} detailed label="Type buffs your line-up carries into the next fight" />

      <div className="box-heading">
        Line-up ({run.lineUp.length}/{MAX_PARTY_SIZE})
      </div>
      <div
        className={`box-grid ${overLineUp ? 'over' : ''}`}
        onDragEnter={(e) => {
          acceptDrop(e);
          setOverLineUp(true);
        }}
        onDragOver={acceptDrop}
        onDragLeave={() => setOverLineUp(false)}
        onDrop={(e) => {
          e.preventDefault();
          setOverLineUp(false);
          selection.endDrag();
          const payload = readDragPayload(e);
          if (payload?.kind === 'mon') placeInLineUp(payload.instanceId, run.lineUp.length);
        }}
      >
        {run.lineUp.map((mon, index) => (
          <TeamCard
            key={mon.instanceId}
            mon={mon}
            index={index}
            selection={selection}
            onDragStart={(e) => {
              setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'lineUp' });
              selection.beginDrag(mon.instanceId);
            }}
            onDragEnd={selection.endDrag}
            actions={
              <>
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
        ))}
        {run.lineUp.length === 0 && <p className="map-note">Drag a mon here.</p>}
      </div>

      <div className="box-heading">Box ({run.box.length})</div>
      <div
        className={`box-grid ${overBox ? 'over' : ''}`}
        onDragEnter={(e) => {
          acceptDrop(e);
          setOverBox(true);
        }}
        onDragOver={acceptDrop}
        onDragLeave={() => setOverBox(false)}
        onDrop={(e) => {
          e.preventDefault();
          setOverBox(false);
          selection.endDrag();
          const payload = readDragPayload(e);
          if (payload?.kind === 'mon' && payload.from === 'lineUp') moveToBox(payload.instanceId);
        }}
      >
        {run.box.map((mon) => (
          <TeamCard
            key={mon.instanceId}
            mon={mon}
            selection={selection}
            onDragStart={(e) => {
              setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'box' });
              selection.beginDrag(mon.instanceId);
            }}
            onDragEnd={selection.endDrag}
            actions={
              <>
                <CombineButton mon={mon} selection={selection} />
                <button
                  disabled={full}
                  onClick={() => moveToLineUp(mon.instanceId)}
                  title={full ? 'Line-up is full' : 'Add to the line-up'}
                >
                  +
                </button>
              </>
            }
          />
        ))}
        {run.box.length === 0 && <p className="map-note">Nothing stored. Catch something.</p>}
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
 * The card is a drop target only while a merge would actually land — the gaps between slots stay
 * the place a reorder is dropped, so the two drags never compete for the same pixel.
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
      onDragEnter={candidate ? acceptDrop : undefined}
      onDragOver={candidate ? acceptDrop : undefined}
      onDrop={
        candidate
          ? (event) => {
              event.preventDefault();
              // Stopped, or the grid underneath treats the same drop as a move and shuffles the
              // mon that was just merged away.
              event.stopPropagation();
              selection?.dropOnto(mon.instanceId, event);
            }
          : undefined
      }
    >
      {index !== undefined && <span className="slot-number">{index + 1}</span>}
      <img src={spriteUrl(species, { facing: 'front' })} alt="" />
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
