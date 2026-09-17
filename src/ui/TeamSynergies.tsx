/**
 * The type buffs a line-up is carrying, shown while there is still time to change them.
 *
 * In battle the trait counters sit in the field's corners and the numbers are already locked in;
 * by then knowing you are two Water short is only an explanation of what went wrong. The
 * decisions that set them are all made here — which mon leads, who is benched, what a fusion
 * consumes — so the same readout belongs on every screen where the team can still be edited.
 *
 * It reads `activeSynergies` over the line-up exactly as the battle opening does, so what it
 * promises and what the fight applies cannot drift apart: both are the same function over the
 * same combatants, and a mon counts toward each of its types.
 *
 * `detailed` spells the effect out in full, for the screens with room for it. Everywhere else the
 * chip carries the name and the effect is on hover, because six mons can carry eight types and a
 * wall of sentences under the team is not a readout anyone scans.
 */

import { useMemo } from 'react';

import { activeSynergies } from '../sim/index.js';
import { toLineUp, type PokemonInstance } from '../content/factory.js';

export function TeamSynergies({
  mons,
  detailed = false,
  label = 'Type buffs',
}: {
  mons: readonly PokemonInstance[];
  detailed?: boolean;
  label?: string;
}) {
  const synergies = useMemo(() => activeSynergies(toLineUp(mons)), [mons]);

  return (
    <div className={`team-synergies ${detailed ? 'detailed' : ''}`}>
      <span className="synergy-label">{label}</span>
      {synergies.length === 0 ? (
        <span className="synergy-empty">none — a line-up with no types gets no opening</span>
      ) : (
        synergies.map((synergy) => (
          <div
            className="synergy-chip"
            key={synergy.type}
            title={`${synergy.title} — ${synergy.effect}`}
          >
            <img
              className="synergy-icon"
              src={`/icons/types/${synergy.type.toLowerCase()}.png`}
              alt={synergy.type}
            />
            <span className="synergy-name">{synergy.title}</span>
            {detailed && <span className="synergy-effect">{synergy.effect}</span>}
          </div>
        ))
      )}
    </div>
  );
}
