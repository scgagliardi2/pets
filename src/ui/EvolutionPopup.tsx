/**
 * The evolution popup: the flicker-between-forms sequence from the handheld games.
 *
 * Four phases, driven by one timer rather than by CSS alone, because the middle phase needs to
 * *accelerate* — the whole character of the original animation is the swap getting faster and
 * faster until you can't tell the two forms apart. A fixed CSS keyframe loop can't speed itself
 * up, so the swap interval is recomputed on each tick.
 *
 * - `charging` — the old form whites out and starts to glow.
 * - `flickering` — the two forms alternate, the interval shrinking from 220ms to 40ms.
 * - `bursting` — a white flash at the moment the new form locks in.
 * - `revealed` — the new form, named, with a button to move on.
 *
 * Both sprites are the ones already in use, so this needs no new art.
 */

import { useEffect, useRef, useState } from 'react';

import { spriteUrl } from '../content/sprites.js';
import type { Evolution } from '../meta/experience.js';
import { STAT_POINTS_PER_EVOLUTION } from '../meta/experience.js';
import { useRunStore } from '../state/runStore.js';

type Phase = 'charging' | 'flickering' | 'bursting' | 'revealed';

const CHARGE_MS = 900;
const FLICKER_MS = 2600;
const BURST_MS = 520;

/** How fast the forms swap, as the flicker phase progresses from 0 to 1. */
const swapIntervalAt = (progress: number): number => 220 - 180 * Math.min(1, progress);

export function EvolutionPopup({ evolution }: { evolution: Evolution }) {
  const dismiss = useRunStore((s) => s.dismissEvolution);
  const [phase, setPhase] = useState<Phase>('charging');
  const [showingNew, setShowingNew] = useState(false);

  // Restarted whenever a different evolution comes up the queue, so a second one in the same
  // batch plays from the beginning rather than inheriting the first one's finished state.
  const key = `${evolution.instanceId}:${evolution.to.id}`;
  const startedAt = useRef(0);

  useEffect(() => {
    setPhase('charging');
    setShowingNew(false);
    startedAt.current = Date.now();

    let raf = 0;
    const tick = (): void => {
      const elapsed = Date.now() - startedAt.current;

      if (elapsed < CHARGE_MS) {
        setPhase('charging');
      } else if (elapsed < CHARGE_MS + FLICKER_MS) {
        setPhase('flickering');
        // Accelerating swap: count how many intervals fit in the time elapsed so far, rather
        // than toggling on a fixed timer, so the rate can change continuously.
        const into = elapsed - CHARGE_MS;
        let consumed = 0;
        let swaps = 0;
        while (consumed < into && swaps < 400) {
          consumed += swapIntervalAt(consumed / FLICKER_MS);
          swaps++;
        }
        setShowingNew(swaps % 2 === 1);
      } else if (elapsed < CHARGE_MS + FLICKER_MS + BURST_MS) {
        setPhase('bursting');
        setShowingNew(true);
      } else {
        setPhase('revealed');
        setShowingNew(true);
        return; // Settled: stop the loop rather than spinning on a static frame.
      }
      raf = requestAnimationFrame(tick);
    };

    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [key]);

  const species = showingNew ? evolution.to : evolution.from;
  const settled = phase === 'revealed';

  return (
    <div className="modal-scrim evolution-scrim">
      <div className={`evolution-stage ${phase}`}>
        <div className="evolution-rays" aria-hidden="true" />
        <div className="evolution-burst" aria-hidden="true" />

        <img
          className="evolution-sprite"
          src={spriteUrl(species, { facing: 'front' })}
          alt={settled ? evolution.to.name : ''}
        />

        <p className="evolution-caption" role="status">
          {settled ? (
            <>
              <strong>{evolution.from.name}</strong> evolved into{' '}
              <strong className="evolution-new">{evolution.to.name}</strong>!
            </>
          ) : (
            <>What? {evolution.from.name} is evolving!</>
          )}
        </p>

        {settled && (
          <>
            <p className="evolution-reward">
              +{STAT_POINTS_PER_EVOLUTION} stat points to assign
            </p>
            <button className="primary" onClick={dismiss} autoFocus>
              Continue
            </button>
          </>
        )}
      </div>
    </div>
  );
}
