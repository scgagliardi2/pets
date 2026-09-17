/**
 * The evolution ceremony, played after a fight that grew something.
 *
 * Evolution used to be a line of text on the results screen, which is the one moment of a run
 * that has earned a pause: EXP is a number going up, but an evolution is the mon becoming a
 * different mon, and the games it descends from stop everything to show you. So this sits *in
 * front of* the result panel and the result waits behind it.
 *
 * Three beats per evolution, and they are the beats the handheld games use: the old sprite
 * pulsing and brightening while the glow builds, a white-out on the change itself — the flash is
 * what hides the sprite swap, which is why it exists at all — and then the new mon held long
 * enough to be read. Several evolutions play one after another rather than at once; two mons
 * changing in the same frame is a mess, and the whole point is that you watch each one.
 *
 * Always skippable, and skipped by default for anyone who has asked for reduced motion — a
 * ceremony you cannot get past is an interruption, however good it looks the first time.
 */

import { useEffect, useRef, useState } from 'react';

import { spriteUrl } from '../content/sprites.js';
import type { Evolution } from '../meta/experience.js';

/** The glow builds. */
export const CHARGE_MS = 1300;
/** The white-out that covers the sprite swap. */
export const FLASH_MS = 420;
/** How long the new mon is held before the next evolution, or the results. */
export const HOLD_MS = 1600;

type Stage = 'charging' | 'flash' | 'revealed';

const prefersReducedMotion = (): boolean =>
  typeof window !== 'undefined' &&
  typeof window.matchMedia === 'function' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

export function EvolutionScene({
  evolutions,
  onDone,
}: {
  evolutions: readonly Evolution[];
  onDone: () => void;
}) {
  const [index, setIndex] = useState(0);
  const [stage, setStage] = useState<Stage>('charging');

  // Held in a ref so a parent that rebuilds the callback every render doesn't restart the
  // timeline — the animation's clock belongs to the animation.
  const done = useRef(onDone);
  done.current = onDone;

  const total = evolutions.length;

  useEffect(() => {
    if (total === 0) {
      done.current();
      return;
    }

    // `index` is current inside here because the effect re-runs whenever it changes; advancing
    // from a state updater instead would mean firing `done` from inside one, which StrictMode is
    // entitled to call twice.
    const advance = (): void => {
      if (index + 1 < total) setIndex(index + 1);
      else done.current();
    };

    if (prefersReducedMotion()) {
      setStage('revealed');
      const held = setTimeout(advance, HOLD_MS);
      return () => clearTimeout(held);
    }

    setStage('charging');
    const timers = [
      setTimeout(() => setStage('flash'), CHARGE_MS),
      setTimeout(() => setStage('revealed'), CHARGE_MS + FLASH_MS),
      setTimeout(advance, CHARGE_MS + FLASH_MS + HOLD_MS),
    ];
    return () => timers.forEach(clearTimeout);
  }, [index, total]);

  const evolution = evolutions[index];
  if (evolution === undefined) return null;

  const { from, to } = evolution;
  const revealed = stage === 'revealed';

  return (
    <div className={`panel evolution-scene stage-${stage}`}>
      <div className="evolution-stage" aria-hidden="true">
        <div className="evolution-glow" />
        <img className="evolution-sprite from" src={spriteUrl(from, { facing: 'front' })} alt="" />
        <img className="evolution-sprite to" src={spriteUrl(to, { facing: 'front' })} alt="" />
      </div>

      {/* Over the whole panel rather than the sprite: a white rectangle the size of a mon reads as
          a box being drawn, and the flash has to read as the room going white. */}
      <div className="evolution-flash" aria-hidden="true" />

      <p className="evolution-caption" role="status">
        {revealed ? (
          <>
            {from.name} evolved into <strong>{to.name}</strong>!
          </>
        ) : (
          <>What? {from.name} is evolving!</>
        )}
      </p>

      <div className="evolution-controls">
        {total > 1 && (
          <span className="evolution-progress">
            {index + 1} of {total}
          </span>
        )}
        <button onClick={() => done.current()}>Skip</button>
      </div>
    </div>
  );
}
