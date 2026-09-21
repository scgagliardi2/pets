/**
 * The run: the Location path, your team, and what a fight just did.
 *
 * Three views over one store — the map you choose from, the result of the fight you just had, and
 * the end of the run. The battle screen itself is unchanged; it is handed a line-up and an
 * opponent and reports an outcome, and knows nothing about badges or Morale.
 */

import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';

import { speciesOf } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import { statsOf, typesOf, unspentPoints, type PokemonInstance } from '../content/factory.js';
import { itemById } from '../content/items.js';
import { EXP_PER_EVOLUTION, expSinceEvolution } from '../meta/experience.js';
import { GROWABLE_STATS, MAX_GROWN_SPEED } from '../content/statGrowth.js';
import { BADGES_TO_WIN, MAX_PARTY_SIZE } from '../meta/progression.js';
import { badgeFor } from '../meta/badges.js';
import { useRunStore } from '../state/runStore.js';
import { canCombine, type RunState } from '../meta/runState.js';
import {
  REROLL_COST,
  SHOP_STOCK,
  adoptionCost,
  canAfford,
  canReroll,
  describeInventory,
} from '../meta/shop.js';
import { acceptDrop, readDragPayload, setDragPayload } from './dragDrop.js';

export function RunHeader() {
  const run = useRunStore((s) => s.run);
  const location = useRunStore((s) => s.location);
  const startRun = useRunStore((s) => s.startRun);
  const openBag = useRunStore((s) => s.openBag);

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
      <button onClick={openBag}>Bag</button>
      <button onClick={() => startRun()}>New run</button>
    </div>
  );
}

/** The five node icons, by node type. */
const NODE_ICON: Record<string, string> = {
  Wild: 'battle',
  MysteryTrainer: 'mystery-trainer',
  Gym: 'gym',
  Center: 'shop',
  Encounter: 'encounter',
};

const NODE_KIND: Record<string, string> = {
  Wild: 'Battle',
  MysteryTrainer: 'Trainer',
  Gym: 'Gym',
  Center: 'Shop',
  Encounter: 'Encounter',
};

const nodeKind = (node: { type: string }): string => NODE_KIND[node.type] ?? 'Battle';

/** Three letters, so two of them fit under a node icon. */
const STAT_ABBREV: Record<string, string> = {
  attack: 'ATK',
  health: 'HP',
  special: 'SP',
  speed: 'SPD',
};

/**
 * A card's own drop handler: dropping one mon directly onto another always does something to
 * both of them, never nothing.
 *
 * Same evolution line → combine. Otherwise → swap, wherever each currently is — line-up or Box.
 * That symmetry is what makes a card a predictable target: you don't have to know in advance
 * whether a drop will "count", because it always does. The earlier version only intercepted a
 * combinable drop and silently declined everything else, which read as the feature not working
 * when it was actually just declining.
 *
 * Gaps are the one thing this doesn't touch — they stay the way to insert a mon into a position
 * without displacing whoever is already there.
 */
function cardDropHandler(
  run: RunState,
  actions: {
    combine: (keepId: string, consumeId: string) => void;
    swap: (a: string, b: string) => void;
    equip: (instanceId: string, itemId: string) => void;
  },
  targetId: string,
): (event: React.DragEvent) => void {
  return (event) => {
    const payload = readDragPayload(event);
    if (payload === null) return;

    // An item dropped on a mon is always "give it to this one", whether it came from the bag or
    // off another mon — the store handles returning whatever was displaced.
    if (payload.kind === 'item') {
      event.preventDefault();
      event.stopPropagation();
      actions.equip(targetId, payload.itemId);
      return;
    }

    if (payload.kind !== 'mon' || payload.instanceId === targetId) return;

    event.preventDefault();
    event.stopPropagation();

    if (canCombine(run, payload.instanceId, targetId)) {
      actions.combine(targetId, payload.instanceId);
    } else {
      actions.swap(payload.instanceId, targetId);
    }
  };
}

/**
 * The Location's branching path, drawn over its region art.
 *
 * Nodes sit in layer columns with the edges between them drawn as lines, so the branching is
 * something you can see and plan against rather than infer from which buttons light up. Only the
 * nodes the current position leads to are takeable; the ones being passed on stay visible,
 * because the decision is which fight you give up.
 *
 * The trainer token walks the line to the node you pick *before* that node's event fires. The
 * store holds a `travelling` phase for exactly as long as that takes.
 */
export function LocationMap() {
  const run = useRunStore((s) => s.run);
  const map = useRunStore((s) => s.map);
  const location = useRunStore((s) => s.location);
  const available = useRunStore((s) => s.available);
  const phase = useRunStore((s) => s.phase);
  const travellingTo = useRunStore((s) => s.travellingTo);
  const enter = useRunStore((s) => s.enter);
  const arrive = useRunStore((s) => s.arrive);

  const rowRef = useRef<HTMLDivElement>(null);
  const nodeRefs = useRef(new Map<string, HTMLButtonElement>());
  const [centres, setCentres] = useState<Map<string, { x: number; y: number }>>(new Map());

  const here = run.visited.length > 0 ? run.visited[run.visited.length - 1]! : null;
  // While travelling the token aims at the destination; the previous node is where it starts.
  const previous =
    travellingTo !== null && run.visited.length > 1 ? run.visited[run.visited.length - 2]! : here;
  const tokenAt = travellingTo ?? here;

  // Measured from the DOM rather than computed: the layer columns are laid out by flexbox, so
  // their positions depend on how the panel wrapped and on the region art behind them.
  // Measured against the *content row*, not the scrolling board around it.
  //
  // The board scrolls and its content is wider than its visible box. Measuring node centres
  // against the board's bounding rect gives viewport coordinates that shift the moment anything
  // is scrolled, and an SVG sized to the board covers only the visible width — so the edges drew
  // in the wrong places and ran off the side. The row is the full content box and does not
  // scroll relative to its children, so offsets against it are stable.
  useLayoutEffect(() => {
    const measure = (): void => {
      const row = rowRef.current;
      if (row === null) return;
      const r = row.getBoundingClientRect();
      const next = new Map<string, { x: number; y: number }>();
      for (const [id, el] of nodeRefs.current) {
        const n = el.getBoundingClientRect();
        next.set(id, { x: n.left - r.left + n.width / 2, y: n.top - r.top + n.height / 2 });
      }
      setCentres(next);
    };

    measure();
    // Re-measured on layout changes as well as resize: the columns are laid out by flexbox over
    // a background image, so their positions settle a frame or two after mount.
    const observer = new ResizeObserver(measure);
    if (rowRef.current !== null) observer.observe(rowRef.current);
    window.addEventListener('resize', measure);
    return () => {
      observer.disconnect();
      window.removeEventListener('resize', measure);
    };
  }, [map]);

  // The walk is a fixed beat. Held in an effect rather than driven by a transitionend event,
  // which does not fire if the element never actually moves — the first node of a Location has
  // nowhere to travel from, and the run would hang waiting for it.
  useEffect(() => {
    if (phase !== 'travelling') return;
    const timer = setTimeout(arrive, 620);
    return () => clearTimeout(timer);
  }, [phase, travellingTo, arrive]);

  const byLayer = new Map<number, typeof map.nodes>();
  for (const node of map.nodes) {
    byLayer.set(node.layer, [...(byLayer.get(node.layer) ?? []), node]);
  }
  const layers = [...byLayer.keys()].sort((a, b) => a - b);

  const token = tokenAt === null ? undefined : centres.get(tokenAt);
  const startedAt = previous === null ? undefined : centres.get(previous);

  return (
    <div className="panel location-map">
      <div
        className="map-board"
        style={{ backgroundImage: `url(/art/regions/${location.region}.jpg)` }}
      >
        <div className="map-scrim" />

        <div className="layer-row" ref={rowRef}>
        {/* Edges first, under the nodes, in the row's own coordinate space. */}
        <svg className="map-edges" aria-hidden="true">
          {map.nodes.flatMap((node) =>
            (node as { next?: readonly string[] }).next?.map((toId) => {
              const a = centres.get(node.id);
              const b = centres.get(toId);
              if (a === undefined || b === undefined) return null;
              const walked = run.visited.includes(node.id) && run.visited.includes(toId);
              const open = node.id === here && available.includes(toId);
              return (
                <line
                  key={`${node.id}->${toId}`}
                  x1={a.x}
                  y1={a.y}
                  x2={b.x}
                  y2={b.y}
                  className={`map-edge ${walked ? 'walked' : ''} ${open ? 'open' : ''}`}
                />
              );
            }) ?? [],
          )}
        </svg>

          {layers.map((layer) => (
            <div className="layer" key={layer}>
              {byLayer.get(layer)!.map((node) => {
                const done = run.visited.includes(node.id);
                const open = available.includes(node.id) && phase === 'map';
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
                        ? `${nodeKind(node)}: ${node.label}${
                            node.statRewards !== undefined && node.statRewards.length > 0
                              ? ` — everyone who fights gains ${node.statRewards.join(' and ')}`
                              : ''
                          }`
                        : done
                          ? 'Already taken'
                          : 'Not reachable from here'
                    }
                  >
                    <img
                      className="node-icon"
                      src={`/icons/nodes/${NODE_ICON[node.type] ?? 'battle'}.png`}
                      alt=""
                    />
                    <span className="node-kind">{nodeKind(node)}</span>
                    {node.statRewards !== undefined && node.statRewards.length > 0 && (
                      <span className="node-stats">
                        {node.statRewards.map((stat) => (
                          <span key={stat} className={`node-stat ${stat}`}>
                            {STAT_ABBREV[stat] ?? stat}
                          </span>
                        ))}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          ))}

        {token !== undefined && (
          <div
            className={`traveller ${phase === 'travelling' ? 'walking' : ''}`}
            style={{
              left: token.x,
              top: token.y,
              // Starting point matters only for the very first frame of a walk; after that the
              // CSS transition carries it.
              ...(startedAt !== undefined && phase !== 'travelling' ? {} : {}),
            }}
            aria-hidden="true"
          >
            <span className="traveller-dot" />
          </div>
        )}
        </div>
      </div>

      <div className="map-foot">
        <strong>{location.name}</strong>
        <span>{location.blurb}</span>
      </div>
    </div>
  );
}

/** A road encounter: fiction, two or three choices, and what came of it. */
export function EncounterPanel() {
  const encounter = useRunStore((s) => s.encounter);
  const outcome = useRunStore((s) => s.encounterOutcome);
  const take = useRunStore((s) => s.takeEncounterChoice);
  const leave = useRunStore((s) => s.leaveEncounter);
  if (encounter === null) return null;

  return (
    <div className="panel encounter-panel">
      <h2>{encounter.title}</h2>
      <p className="encounter-body">{encounter.body}</p>

      {outcome === null ? (
        <div className="encounter-choices">
          {encounter.choices.map((choice, i) => (
            <button key={choice.label} className="encounter-choice" onClick={() => take(i)}>
              <span className="choice-label">{choice.label}</span>
              <span className="choice-detail">{choice.detail}</span>
            </button>
          ))}
        </div>
      ) : (
        <>
          <p className="encounter-outcome">{outcome}</p>
          <button className="primary" onClick={leave}>
            Continue
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
  const adoptable = useRunStore((s) => s.adoptable);
  const adopt = useRunStore((s) => s.adopt);
  const rerollAdoptions = useRunStore((s) => s.rerollAdoptions);
  const purchasableItems = useRunStore((s) => s.purchasableItems);
  const buyItem = useRunStore((s) => s.buyItem);

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

      <div className="team-heading">
        Adopt <span className="team-hint">Pokémon that live around here</span>
      </div>
      <div className="adopt-row">
        {adoptable.map((mon) => {
          const species = speciesOf(mon.speciesId);
          if (species === null) return null;
          const cost = adoptionCost(species.tier);
          const stats = statsOf(mon);
          const affordable = run.money >= cost;

          return (
            <button
              key={mon.instanceId}
              className="adopt-card"
              disabled={!affordable}
              onClick={() => adopt(mon.instanceId)}
              title={affordable ? `Adopt ${species.name} for $${cost}` : 'Not enough money'}
            >
              <img src={spriteUrl(species, { facing: 'front' })} alt="" />
              <span className="adopt-name">{species.name}</span>
              <span className="adopt-figures">
                {stats.attack}/{stats.special}/{stats.health} · spd {stats.speed}
              </span>
              <span className="adopt-types">
                {species.types.map((t) => (
                  <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} />
                ))}
              </span>
              <span className="adopt-cost">${cost}</span>
            </button>
          );
        })}
        {adoptable.length === 0 && <p className="map-note">All adopted.</p>}
      </div>

      <div className="team-heading">
        Items <span className="team-hint">a Pokémon holds one at a time</span>
      </div>
      <div className="shop-stock">
        {purchasableItems.map((item) => {
          const affordable = run.money >= item.cost;
          return (
            <button
              key={item.id}
              className="shop-item"
              disabled={!affordable}
              onClick={() => buyItem(item.id)}
              title={affordable ? `Buy ${item.name} for $${item.cost}` : 'Not enough money'}
            >
              <span className="shop-item-name">{item.name}</span>
              <span className="shop-item-cost">${item.cost}</span>
              <span className="shop-item-blurb">{item.blurb}</span>
            </button>
          );
        })}
        {purchasableItems.length === 0 && <p className="map-note">Sold out of items.</p>}
      </div>

      <div className="shop-actions">
        <button disabled={!canReroll(run.money)} onClick={rerollAdoptions}>
          Reroll the shelf (${REROLL_COST})
        </button>
        <button className="primary" onClick={leaveShop}>
          Move on
        </button>
      </div>
    </div>
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
/**
 * A row of mons with insertion gaps between them, and swap-or-combine on the cards themselves.
 *
 * The one interaction model, used everywhere a line-up is shown: drop between two cards to
 * insert without displacing anyone; drop directly onto a card to trade places with it, or to
 * combine if the two share an evolution line. Previously this existed only in the bottom strip —
 * the Box screen's line-up grid had no gaps at all, which is why reordering there didn't work.
 */
function SlottedRow({
  instances,
  onInsertAt,
  onOpenDetail,
  cardActions,
  className,
  emptyHint,
}: {
  instances: readonly PokemonInstance[];
  /** Insert a dragged mon at this index in the line-up, promoting it from the Box if needed. */
  onInsertAt: (instanceId: string, index: number) => void;
  onOpenDetail: (instanceId: string) => void;
  cardActions?: (mon: PokemonInstance, index: number) => React.ReactNode;
  className?: string;
  emptyHint?: React.ReactNode;
}) {
  const run = useRunStore((s) => s.run);
  const combine = useRunStore((s) => s.combine);
  const swap = useRunStore((s) => s.swap);
  const equip = useRunStore((s) => s.equip);
  const [dropIndex, setDropIndex] = useState<number | null>(null);

  const onDropAt = (index: number) => (event: React.DragEvent) => {
    event.preventDefault();
    // Stopped, or the drop bubbles up to the row's own handler, which appends to the end and
    // silently undoes the more specific placement that just happened.
    event.stopPropagation();
    setDropIndex(null);
    const payload = readDragPayload(event);
    if (payload === null || payload.kind !== 'mon') return;
    onInsertAt(payload.instanceId, index);
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
    <div
      className={`slot-row ${className ?? ''}`}
      onDragOver={acceptDrop}
      onDrop={onDropAt(instances.length)}
    >
      {gap(0)}
      {instances.map((mon, index) => (
        <Fragment key={mon.instanceId}>
          <TeamCard
            mon={mon}
            index={index}
            onDragStart={(e) =>
              setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'lineUp' })
            }
            onClick={() => onOpenDetail(mon.instanceId)}
            onDrop={cardDropHandler(run, { combine, swap, equip }, mon.instanceId)}
            actions={cardActions?.(mon, index)}
          />
          {gap(index + 1)}
        </Fragment>
      ))}
      {instances.length === 0 && emptyHint !== undefined && (
        <p className="map-note">{emptyHint}</p>
      )}
    </div>
  );
}

export function TeamBuilder() {
  const run = useRunStore((s) => s.run);
  const placeInLineUp = useRunStore((s) => s.placeInLineUp);
  const openBox = useRunStore((s) => s.openBox);
  const openDetail = useRunStore((s) => s.openDetail);

  return (
    <div className="panel team-builder">
      <div className="builder-head">
        <span className="team-heading">
          Line-up{' '}
          <span className="team-hint">
            drop between two mons to insert · drop onto one to swap or combine
          </span>
        </span>
        <div className="spacer" />
        <button onClick={openBox}>Box ({run.box.length})</button>
      </div>

      <SlottedRow instances={run.lineUp} onInsertAt={placeInLineUp} onOpenDetail={openDetail} />

      {run.lineUp.length < MAX_PARTY_SIZE && (
        <p className="team-hint slot-hint">{MAX_PARTY_SIZE - run.lineUp.length} free · drag from the Box</p>
      )}
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
  const openDetail = useRunStore((s) => s.openDetail);
  const combine = useRunStore((s) => s.combine);
  const swap = useRunStore((s) => s.swap);
  const equip = useRunStore((s) => s.equip);

  const [overBox, setOverBox] = useState(false);
  const full = run.lineUp.length >= MAX_PARTY_SIZE;

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
        and the next win's catch-up brings it back within a couple of points of the rest.
      </p>

      <div className="box-heading">
        Line-up ({run.lineUp.length}/{MAX_PARTY_SIZE})
      </div>
      <SlottedRow
        instances={run.lineUp}
        onInsertAt={placeInLineUp}
        onOpenDetail={openDetail}
        className="wrap"
        emptyHint="Drag a mon here."
        cardActions={(mon) => (
          <button
            disabled={run.lineUp.length <= 1}
            onClick={(e) => {
              e.stopPropagation();
              moveToBox(mon.instanceId);
            }}
            title="Send to the Box"
          >
            ✕
          </button>
        )}
      />

      <div className="box-heading">
        Box ({run.box.length}) <span className="team-hint">drop onto one to swap or combine</span>
      </div>
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
          const payload = readDragPayload(e);
          if (payload?.kind === 'mon' && payload.from === 'lineUp') moveToBox(payload.instanceId);
        }}
      >
        {run.box.map((mon) => (
          <TeamCard
            key={mon.instanceId}
            mon={mon}
            onDragStart={(e) =>
              setDragPayload(e, { kind: 'mon', instanceId: mon.instanceId, from: 'box' })
            }
            onClick={() => openDetail(mon.instanceId)}
            onDrop={cardDropHandler(run, { combine, swap, equip }, mon.instanceId)}
            actions={
              <button
                disabled={full}
                onClick={(e) => {
                  e.stopPropagation();
                  moveToLineUp(mon.instanceId);
                }}
                title={full ? 'Line-up is full' : 'Add to the line-up'}
              >
                +
              </button>
            }
          />
        ))}
        {run.box.length === 0 && <p className="map-note">Nothing stored. Catch something.</p>}
      </div>
    </div>
  );
}

/**
 * A mon's held-item slot: shows what it is carrying, and is both a drag source and a drop target.
 *
 * Dragging the item off and onto another mon moves it directly; dropping it anywhere that isn't a
 * mon does nothing, so an item can't be lost by a misaimed drag. Clicking it returns it to the bag.
 */
function HeldItemSlot({ mon }: { mon: PokemonInstance }) {
  const unequip = useRunStore((s) => s.unequip);
  const item = itemById(mon.heldItemId);
  if (item === null) return null;

  return (
    <span
      className={`held-item kind-${item.kind}`}
      draggable
      onDragStart={(e) => {
        e.stopPropagation();
        setDragPayload(e, { kind: 'item', itemId: item.id, from: mon.instanceId });
      }}
      onClick={(e) => {
        e.stopPropagation();
        unequip(mon.instanceId);
      }}
      title={`${item.name} — ${item.blurb} (click to return to the bag)`}
    >
      {item.name}
    </span>
  );
}

/** One mon, as a draggable card. Used by the line-up row and by both grids in the Box. */
function TeamCard({
  mon,
  index,
  actions,
  onDragStart,
  onClick,
  onDrop,
}: {
  mon: PokemonInstance;
  index?: number;
  actions?: React.ReactNode;
  onDragStart?: (event: React.DragEvent) => void;
  onClick?: () => void;
  onDrop?: (event: React.DragEvent) => void;
}) {
  const species = speciesOf(mon.speciesId);
  if (species === null) return null;
  const stats = statsOf(mon);
  // Typing can be overridden by a Plate, so read it from the mon rather than the species.
  const heldTypes = typesOf(mon);

  const since = expSinceEvolution(mon);
  const canEvolve = species.evolvesIntoId !== null;
  const pending = unspentPoints(mon);

  return (
    <div
      className={`team-card ${index === 0 ? 'lead' : index === 1 ? 'support' : ''} ${onClick !== undefined ? 'clickable' : ''}`}
      draggable={onDragStart !== undefined}
      onDragStart={onDragStart}
      onClick={onClick}
      onDrop={onDrop}
    >
      {index !== undefined && <span className="slot-number">{index + 1}</span>}
      {pending > 0 && (
        <span className="pending-badge" title={`${pending} point${pending === 1 ? '' : 's'} to spend`}>
          {pending}
        </span>
      )}
      <img src={spriteUrl(species, { facing: 'front' })} alt="" />
      <div className="team-meta">
        <span className="team-name">{species.name}</span>
        <div className="team-stat-grid">
          <span>
            <em>ATK</em> {stats.attack}
          </span>
          <span>
            <em>SP</em> {stats.special}
          </span>
          <span>
            <em>HP</em> {stats.health}
          </span>
          <span>
            <em>SPD</em> {stats.speed}
          </span>
        </div>
        {canEvolve ? (
          <div className="evo-bar" title={`${since}/${EXP_PER_EVOLUTION} EXP toward evolving`}>
            <div
              className="evo-bar-fill"
              style={{ width: `${Math.min(100, (since / EXP_PER_EVOLUTION) * 100)}%` }}
            />
          </div>
        ) : (
          <span className="team-exp">final form</span>
        )}
        <span className="team-types">
          {heldTypes.map((t) => (
            <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} title={t} />
          ))}
        </span>
        <HeldItemSlot mon={mon} />
      </div>
      {actions !== undefined && <div className="team-actions">{actions}</div>}
    </div>
  );
}

export function GrowthPanel() {
  const run = useRunStore((s) => s.run);
  const choose = useRunStore((s) => s.choose);
  const waiting = [...run.lineUp, ...run.box].filter((m) => unspentPoints(m) > 0);
  if (waiting.length === 0) return null;

  return (
    <div className="panel growth-panel">
      <div className="team-heading">
        Stat points <span className="team-hint">one point, one stat</span>
      </div>
      {waiting.map((mon) => {
        const species = speciesOf(mon.speciesId);
        if (species === null) return null;
        const stats = statsOf(mon);
        const left = unspentPoints(mon);

        return (
          <div className="growth-row" key={mon.instanceId}>
            <img src={spriteUrl(species, { facing: 'front' })} alt="" />
            <div className="growth-meta">
              <span className="team-name">{species.name}</span>
              <span className="team-figures">
                {stats.attack} atk · {stats.special} sp · {stats.health} hp · spd {stats.speed}
              </span>
            </div>
            <span className="growth-left">{left}</span>
            <div className="growth-buttons">
              {GROWABLE_STATS.map((stat) => {
                const capped = stat === 'speed' && stats.speed >= MAX_GROWN_SPEED;
                return (
                  <button
                    key={stat}
                    className={`growth-button ${stat}`}
                    disabled={capped}
                    onClick={() => choose(mon.instanceId, stat)}
                    title={
                      capped
                        ? `Speed is capped at ${MAX_GROWN_SPEED}`
                        : stat === 'speed'
                          ? 'Speed fills the ability bar faster — the strongest point in the game'
                          : `+1 ${stat}`
                    }
                  >
                    {stat === 'special' ? 'SP' : stat.slice(0, 3).toUpperCase()}
                  </button>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

/**
 * Beating a Gym: the badge, the leader's line, and where to go next.
 *
 * The route choice lives here rather than on the map because it is the moment it belongs to —
 * the Location is finished, and picking the next one is the reward for finishing it. Each option
 * names the types you are likely to meet, so it is a real decision rather than three names.
 */
export function CelebrationPanel() {
  const run = useRunStore((s) => s.run);
  const choices = useRunStore((s) => s.regionChoices);
  const chooseRegion = useRunStore((s) => s.chooseRegion);
  const badge = badgeFor(run.badges - 1);

  return (
    <div className="panel celebration-panel">
      <div className="badge-award">
        <span className="badge-glyph" aria-hidden="true">
          {badge.glyph}
        </span>
        <div>
          <h2>{badge.name}</h2>
          <p className="badge-leader">Defeated {badge.leader}</p>
        </div>
      </div>

      <blockquote className="badge-quote">“{badge.quote}”</blockquote>

      <p className="badge-count">
        {run.badges} of {BADGES_TO_WIN} badges
      </p>

      {choices.length > 0 && (
        <>
          <div className="team-heading">Where next?</div>
          <div className="region-choices">
            {choices.map((loc) => (
              <button
                key={loc.name}
                className="region-choice"
                onClick={() => chooseRegion(loc.name)}
                style={{ backgroundImage: `url(/art/regions/${loc.region}.jpg)` }}
              >
                <span className="region-scrim" />
                <span className="region-body">
                  <strong>{loc.name}</strong>
                  <em>{loc.blurb}</em>
                  <span className="region-types">
                    {loc.typeBias.map((t) => (
                      <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} title={t} />
                    ))}
                  </span>
                </span>
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

/** Arriving somewhere new: one permanent buff, picked once, flavoured to the region. */
export function BuffPanel() {
  const location = useRunStore((s) => s.location);
  const choices = useRunStore((s) => s.buffChoices);
  const chooseBuff = useRunStore((s) => s.chooseBuff);

  return (
    <div className="panel buff-panel">
      <h2>{location.name}</h2>
      <p className="shop-sub">
        You settle in. Something about the place sticks with you — pick one, and it lasts the rest
        of the run.
      </p>
      <div className="buff-choices">
        {choices.map((buff) => (
          <button key={buff.id} className="buff-choice" onClick={() => chooseBuff(buff.id)}>
            <strong>{buff.name}</strong>
            <span>{buff.blurb}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

export function ResultPanel() {
  const result = useRunStore((s) => s.lastResult);
  const dismiss = useRunStore((s) => s.dismissResult);
  if (result === null) return null;

  const won = result.outcome === 'SideAWins';

  return (
    <div className="panel result-panel">
      <h2 className={won ? 'won' : 'lost'}>
        {won ? 'Victory' : result.outcome === 'Draw' ? 'Draw' : 'Defeat'}
      </h2>
      <ul className="result-list">
        {won && <li>+{result.expEach} EXP toward evolving, for everyone who fought</li>}
        {won && result.statRewards.length > 0 && (
          <li className="good">
            +1 {result.statRewards.join(' and +1 ')} to everyone who fought
          </li>
        )}
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
  const won = run.badges >= BADGES_TO_WIN;

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
