/**
 * Location map generation: a branching path from an entry choice to the Gym.
 *
 * Slay the Spire's shape, and for its reason — the interesting decision is not which fight you
 * take but what you give up to take it. A linear path has no such decision, which is why the
 * hand-authored Location from the previous stage was only ever scaffolding.
 *
 * Generated from the run's seed, so a Location is stable: leaving and returning shows the same
 * map, and a run replays identically.
 */

import { createRandom, type Rng } from '../sim/index.js';
import type { MapNode, NodeType } from './runState.js';

/** How many entry nodes the player picks between. */
export const ENTRY_NODE_COUNT = 3;

/** Layers of choice between the entry and the Gym. */
export const CHOICE_LAYERS = 4;

const MIN_NODES_PER_LAYER = 2;
const MAX_NODES_PER_LAYER = 4;

/** At most one Center per layer — two side by side makes the layer a non-choice. */
const MAX_CENTERS_PER_LAYER = 1;

/** The earliest layer a Center may appear. A rest before any fight is worthless. */
const EARLIEST_CENTER_LAYER = 2;

export interface LocationMap {
  readonly nodes: readonly MapNode[];
  /** Node ids with no incoming edge — the layer the player starts by choosing from. */
  readonly entryIds: readonly string[];
  readonly gymId: string;
}

/** A node plus its outgoing edges. Extends the MapNode the run state already understands. */
export interface MapNodeWithEdges extends MapNode {
  readonly next: readonly string[];
}

/**
 * The same node while the generator is still wiring it.
 *
 * `next` is readonly on the published type so the rest of the app cannot edit a map out from
 * under the traversal; the generator needs it mutable for exactly as long as it takes to build.
 */
type MutableMapNode = MapNode & { next: string[] };

export const nodeById = (map: LocationMap, id: string): MapNodeWithEdges | null =>
  (map.nodes as MapNodeWithEdges[]).find((n) => n.id === id) ?? null;

/**
 * What the player may take next.
 *
 * With nothing visited yet, that is the entry layer. Otherwise it is whatever the last visited
 * node leads to, minus anything already taken — you move forward through the map, never back.
 */
export function reachableFrom(map: LocationMap, visited: readonly string[]): string[] {
  if (visited.length === 0) return [...map.entryIds];

  const last = nodeById(map, visited[visited.length - 1]!);
  if (last === null) return [];
  return last.next.filter((id) => !visited.includes(id));
}

const labelFor = (type: NodeType, rng: Rng): string => {
  if (type === 'Gym') return 'Gym Leader';
  if (type === 'Center') return 'Pokémon Center';
  const flavours = [
    'Tall grass',
    'A narrow track',
    'Rustling undergrowth',
    'Open ground',
    'A shaded hollow',
    'Broken stones',
    'The old road',
  ];
  return flavours[rng.nextInt(flavours.length)]!;
};

/**
 * Builds a Location's map.
 *
 * Layers are generated first and wired afterwards. Wiring guarantees two things that a naive
 * random pass does not: every node in a layer is reachable from the one before it, and every node
 * leads somewhere. Without both, a generated map can contain a node the player can never take, or
 * — worse — a path that dead-ends short of the Gym.
 */
export function generateLocationMap(seed: number, badges: number): LocationMap {
  const rng = createRandom(seed * 7919 + badges * 104729 + 13);
  const layers: MutableMapNode[][] = [];

  const entry: MutableMapNode[] = Array.from({ length: ENTRY_NODE_COUNT }, (_, i) => ({
    id: `L${badges}-0-${i}`,
    type: 'Wild' as NodeType,
    layer: 1,
    label: labelFor('Wild', rng),
    next: [],
  }));
  layers.push(entry);

  for (let depth = 1; depth <= CHOICE_LAYERS; depth++) {
    const size =
      MIN_NODES_PER_LAYER + rng.nextInt(MAX_NODES_PER_LAYER - MIN_NODES_PER_LAYER + 1);
    let centers = 0;

    const layer: MutableMapNode[] = Array.from({ length: size }, (_, i) => {
      let type: NodeType = 'Wild';
      // A Center is a real alternative to a fight: it costs you the EXP and money that node would
      // have paid, and buys restocking instead.
      if (depth >= EARLIEST_CENTER_LAYER && centers < MAX_CENTERS_PER_LAYER && rng.nextInt(100) < 38) {
        type = 'Center';
        centers++;
      }
      return {
        id: `L${badges}-${depth}-${i}`,
        type,
        layer: depth + 1,
        label: labelFor(type, rng),
        next: [],
      };
    });
    layers.push(layer);
  }

  const gym: MutableMapNode = {
    id: `L${badges}-gym`,
    type: 'Gym',
    layer: CHOICE_LAYERS + 2,
    label: 'Gym Leader',
    next: [],
  };
  layers.push([gym]);

  for (let i = 0; i < layers.length - 1; i++) {
    connect(layers[i]!, layers[i + 1]!, rng);
  }

  return {
    nodes: layers.flat(),
    entryIds: entry.map((n) => n.id),
    gymId: gym.id,
  };
}

/**
 * Wires one layer to the next.
 *
 * Two passes, because the two guarantees pull in opposite directions. The first gives every node
 * in `from` at least one way forward; the second gives every node in `to` at least one way in.
 * Doing only the first strands nodes in `to`; doing only the second can dead-end a node in
 * `from`. Extra edges are then sprinkled so the map is a web rather than a set of parallel lines.
 */
function connect(from: MutableMapNode[], to: MutableMapNode[], rng: Rng): void {
  const edges = new Map<string, Set<string>>(from.map((n) => [n.id, new Set<string>()]));

  from.forEach((node, i) => {
    // Bias each node toward the target nearest its own position, so paths read as paths rather
    // than as a scribble across the layer.
    const nearest = Math.min(to.length - 1, Math.round((i / Math.max(1, from.length - 1)) * (to.length - 1)));
    edges.get(node.id)!.add(to[nearest]!.id);
  });

  for (const target of to) {
    const alreadyReached = [...edges.values()].some((set) => set.has(target.id));
    if (!alreadyReached) {
      const source = from[rng.nextInt(from.length)]!;
      edges.get(source.id)!.add(target.id);
    }
  }

  for (const node of from) {
    if (to.length > 1 && rng.nextInt(100) < 45) {
      edges.get(node.id)!.add(to[rng.nextInt(to.length)]!.id);
    }
  }

  for (const node of from) {
    node.next = [...edges.get(node.id)!];
  }
}

/** Every node in the map is reachable from an entry node. A generation invariant, checked by tests. */
export function allReachable(map: LocationMap): boolean {
  const seen = new Set<string>(map.entryIds);
  const queue = [...map.entryIds];

  while (queue.length > 0) {
    const node = nodeById(map, queue.shift()!);
    if (node === null) continue;
    for (const id of node.next) {
      if (!seen.has(id)) {
        seen.add(id);
        queue.push(id);
      }
    }
  }
  return seen.size === map.nodes.length;
}

/** Every path forward eventually arrives at the Gym. The other generation invariant. */
export function allPathsReachGym(map: LocationMap): boolean {
  return map.nodes.every((n) => n.id === map.gymId || (n as MapNodeWithEdges).next.length > 0);
}
