/**
 * Trait counters: a type icon and how many mons in that line-up carry the type, with the resolved
 * effect on hover.
 *
 * These are the one place a type icon is the right call. §6.8's rule is that anything shaped like
 * a type badge must *mean* a type — the Unity build had a badge row expressing lifesteal with a
 * Grass leaf and damage reduction with a Steel mark, which sitting beside a mon read as that
 * mon's typing and was simply wrong. Here the icon means exactly what it looks like: this side
 * has four Water-types.
 *
 * The icons are the 18 in the Unity repo at Resources/Sprites/Types, copied into public/.
 */

import type { activeSynergies } from '../sim/index.js';

type Synergy = ReturnType<typeof activeSynergies>[number];

export function TraitCounters({
  synergies,
  align,
}: {
  synergies: Synergy[];
  align: 'left' | 'right';
}) {
  if (synergies.length === 0) return null;

  return (
    <div className={`traits traits-${align}`}>
      {synergies.map((s) => (
        <div className="trait" key={s.type}>
          <span className="trait-count">{s.count}</span>
          <img
            className="trait-icon"
            src={`/icons/types/${s.type.toLowerCase()}.png`}
            alt={s.type}
          />
          <div className="trait-tip">
            <strong>
              {s.title}
            </strong>
            <span>{s.effect}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
