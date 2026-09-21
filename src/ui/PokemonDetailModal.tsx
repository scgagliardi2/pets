/**
 * A single Pokémon's detail panel: full stats, its ability, spending any pending EXP, and
 * combining it with a duplicate.
 *
 * Opened by clicking a card in the team builder or the Box, independent of `phase` so it can sit
 * over either. This is the one place all three of those things live together — the growth panel
 * after a fight is fast and disposable, but a player revisiting their team between fights wants
 * to see what a mon *is*, not just spend a point and move on.
 */

import { resolvePassive, speciesOf } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import { statsOf, unspentPoints, type PokemonInstance } from '../content/factory.js';
import { EXP_PER_COMBINE, EXP_PER_EVOLUTION, expSinceEvolution } from '../meta/experience.js';
import { GROWABLE_STATS, MAX_GROWN_SPEED } from '../content/statGrowth.js';
import { duplicatesOf } from '../meta/runState.js';
import { useRunStore } from '../state/runStore.js';

export function PokemonDetailModal({ instanceId }: { instanceId: string }) {
  const run = useRunStore((s) => s.run);
  const close = useRunStore((s) => s.closeDetail);
  const choose = useRunStore((s) => s.choose);
  const combine = useRunStore((s) => s.combine);

  const mon: PokemonInstance | undefined =
    run.lineUp.find((m) => m.instanceId === instanceId) ??
    run.box.find((m) => m.instanceId === instanceId);

  // The mon being viewed can vanish mid-view — combined away, most likely, since that action is
  // reachable from this same panel. Close cleanly rather than render against nothing.
  if (mon === undefined) {
    close();
    return null;
  }

  const species = speciesOf(mon.speciesId);
  if (species === null) return null;

  const stats = statsOf(mon);
  const ability = resolvePassive(species, species.evolutionStage);
  const canEvolve = species.evolvesIntoId !== null;
  const since = expSinceEvolution(mon);
  const pending = unspentPoints(mon);
  const duplicates = duplicatesOf(run, instanceId);

  return (
    <div className="modal-scrim" onClick={close}>
      <div className="panel detail-modal" onClick={(e) => e.stopPropagation()}>
        <button className="modal-close" onClick={close} aria-label="Close">
          ✕
        </button>

        <div className="detail-head">
          <img src={spriteUrl(species, { facing: 'front' })} alt="" />
          <div>
            <h2>{species.name}</h2>
            <div className="detail-types">
              {species.types.map((t) => (
                <img key={t} src={`/icons/types/${t.toLowerCase()}.png`} alt={t} title={t} />
              ))}
            </div>
          </div>
        </div>

        <div className="detail-stats">
          <div className="detail-stat">
            <span className="detail-stat-label">ATK</span>
            <span className="detail-stat-value">{stats.attack}</span>
          </div>
          <div className="detail-stat">
            <span className="detail-stat-label">SP</span>
            <span className="detail-stat-value">{stats.special}</span>
          </div>
          <div className="detail-stat">
            <span className="detail-stat-label">HP</span>
            <span className="detail-stat-value">{stats.health}</span>
          </div>
          <div className="detail-stat">
            <span className="detail-stat-label">SPD</span>
            <span className="detail-stat-value">{stats.speed}</span>
          </div>
        </div>

        {ability !== null && (
          <div className="detail-ability">
            <span className="detail-ability-name">{ability.displayName}</span>
            <p className="detail-ability-desc">{ability.description}</p>
          </div>
        )}

        <div className="detail-evo">
          {canEvolve ? (
            <>
              <div className="evo-bar large">
                <div
                  className="evo-bar-fill"
                  style={{ width: `${Math.min(100, (since / EXP_PER_EVOLUTION) * 100)}%` }}
                />
              </div>
              <span className="detail-evo-label">
                {since}/{EXP_PER_EVOLUTION} EXP toward evolving
              </span>
            </>
          ) : (
            <span className="detail-evo-label">Fully evolved</span>
          )}
        </div>

        {pending > 0 && (
          <div className="detail-points">
            <span className="team-heading">
              {pending} stat point{pending === 1 ? '' : 's'} to spend
            </span>
            <div className="growth-buttons large">
              {GROWABLE_STATS.map((stat) => {
                const capped = stat === 'speed' && stats.speed >= MAX_GROWN_SPEED;
                return (
                  <button
                    key={stat}
                    className={`growth-button ${stat}`}
                    disabled={capped}
                    onClick={() => choose(mon.instanceId, stat)}
                    title={capped ? `Speed is capped at ${MAX_GROWN_SPEED}` : `+1 ${stat}`}
                  >
                    {stat === 'special' ? 'SP' : stat.slice(0, 3).toUpperCase()}
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {duplicates.length > 0 && (
          <div className="detail-combine">
            <span className="team-heading">
              Combine <span className="team-hint">+{EXP_PER_COMBINE} EXP toward evolving</span>
            </span>
            <div className="combine-list">
              {duplicates.map((dup) => {
                const dupSpecies = speciesOf(dup.speciesId);
                if (dupSpecies === null) return null;
                return (
                  <button
                    key={dup.instanceId}
                    className="combine-row"
                    onClick={() => combine(mon.instanceId, dup.instanceId)}
                    title={`Combine with this ${dupSpecies.name}`}
                  >
                    <img src={spriteUrl(dupSpecies, { facing: 'front' })} alt="" />
                    <span>{dupSpecies.name}</span>
                    <span className="combine-plus">+</span>
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
