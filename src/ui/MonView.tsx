/**
 * One Pokemon on the field: its sprite, the charge arc bent over its head, a shield bubble when
 * it has one, and its numbers sitting on the field beside it.
 *
 * Every position comes from `layout.ts`. This component computes no coordinates of its own — the
 * rule from §6.7, where positions living in two places silently disagreed and two rounds of fixes
 * shipped without appearing on screen.
 */

import { useEffect, useState } from 'react';

import { CHARGE_THRESHOLD, type Combatant, type Side } from '../sim/index.js';
import type { Species } from '../content/index.js';
import { spriteUrl } from '../content/sprites.js';
import type { MonFlash } from '../playback/display.js';
import { CHARGE_ARC, NOMINAL_SPRITE, SHIELD_BUBBLE, spriteBox, type Slot } from './layout.js';

interface MonViewProps {
  combatant: Combatant;
  /** See ChargeArc. */
  chargeDurationMs?: number;
  species: Species;
  side: Side;
  slot: Slot;
  flash: MonFlash | undefined;
  fainting: boolean;
}

/**
 * The charge arc — a bar bent over the mon's head, and the one saturated element on the field,
 * because charge is the game's clock: Speed does nothing but fill it, and the passive fires when
 * it's full.
 *
 * It can read below empty. Ice and Ground synergies open the enemy Lead in charge *debt*, and
 * that has to be visible or a Lead that seems inexplicably slow is just confusing. Debt shows as
 * a red stub at the leading edge rather than as an empty track.
 */
function ChargeArc({
  charge,
  cx,
  cy,
  durationMs,
}: {
  charge: number;
  cx: number;
  cy: number;
  /** Overrides the CSS default so the fill runs continuously rather than in short snaps. */
  durationMs?: number;
}) {
  const { radius, thickness, sweepDegrees } = CHARGE_ARC;
  const half = (sweepDegrees / 2) * (Math.PI / 180);

  const point = (t: number) => {
    const angle = -Math.PI / 2 + (t * 2 - 1) * half;
    return { x: cx + Math.cos(angle) * radius, y: cy + Math.sin(angle) * radius };
  };

  const a = point(0);
  const b = point(1);
  const track = `M ${a.x} ${a.y} A ${radius} ${radius} 0 0 1 ${b.x} ${b.y}`;

  // The fill is the *same* path revealed by a dash offset, rather than a second path recomputed
  // to a shorter sweep. An SVG path's `d` cannot be transitioned, so redrawing it made the bar
  // jump from one value to the next; `stroke-dashoffset` is a plain number and animates.
  const length = radius * (2 * half);
  const filled = Math.max(0, Math.min(1, charge / CHARGE_THRESHOLD));
  const full = filled >= 1;

  return (
    <g>
      <path
        d={track}
        fill="none"
        stroke="rgba(14,22,16,0.5)"
        strokeWidth={thickness + 6}
        strokeLinecap="round"
      />
      <path d={track} fill="none" stroke="#e9eee6" strokeWidth={thickness} strokeLinecap="round" />
      <path
        className={`charge-fill ${full ? 'full' : ''}`}
        d={track}
        fill="none"
        stroke={full ? '#8ff06a' : '#4fbb3f'}
        strokeWidth={thickness}
        strokeLinecap="round"
        strokeDasharray={length}
        strokeDashoffset={length * (1 - filled)}
        style={
          durationMs !== undefined
            ? { transition: `stroke-dashoffset ${durationMs}ms linear, stroke 160ms linear` }
            : undefined
        }
      />
      {charge < 0 && (
        <path
          d={track}
          fill="none"
          stroke="#d9564c"
          strokeWidth={thickness}
          strokeLinecap="round"
          strokeDasharray={length}
          strokeDashoffset={length * 0.9}
        />
      )}
    </g>
  );
}

/** The spiky burst the attack figure sits in. Drawn rather than imported so it scales with use. */
export function burstPath(cx: number, cy: number, outer: number, inner: number, points = 9): string {
  const steps: string[] = [];
  for (let i = 0; i < points * 2; i++) {
    const r = i % 2 === 0 ? outer : inner;
    const angle = (i / (points * 2)) * Math.PI * 2 - Math.PI / 2;
    steps.push(`${cx + Math.cos(angle) * r},${cy + Math.sin(angle) * r}`);
  }
  return `M ${steps.join(' L ')} Z`;
}

export function MonView({
  combatant,
  species,
  side,
  slot,
  flash,
  fainting,
  chargeDurationMs,
}: MonViewProps) {
  // Sprites run 23x39 to 153x94 and are drawn at true relative scale, so the real dimensions
  // matter and a fixed box would be wrong for every one of them. Lay out against a nominal box
  // until the image reports its own size.
  const [natural, setNatural] = useState(NOMINAL_SPRITE);
  const [hovered, setHovered] = useState(false);

  useEffect(() => {
    setNatural(NOMINAL_SPRITE);
  }, [species.id, slot.facing]);

  const box = spriteBox(slot, natural);
  const url = spriteUrl(species, { facing: slot.facing });

  const maxHP = combatant.currentStats.health;
  const hp = Math.max(0, combatant.currentHP);
  const hpFraction = Math.max(0, Math.min(1, hp / Math.max(1, maxHP)));
  const hpClass = hpFraction <= 0.25 ? 'critical' : hpFraction <= 0.5 ? 'hurt' : '';

  const pad = CHARGE_ARC.radius + 34 + SHIELD_BUBBLE.pad;
  const overlay = {
    left: box.left - pad,
    top: box.top - pad,
    width: box.width + pad * 2,
    height: box.height + pad * 2,
  };
  const localCX = pad + box.width / 2;
  // The arc's lowest point sits just above the sprite; its centre is a radius further up.
  const arcCY = pad - CHARGE_ARC.liftAboveHead + CHARGE_ARC.radius;

  const attackClass =
    flash?.attacking === true ? (side === 'A' ? 'attacking-own' : 'attacking-foe') : '';

  // Numbers sit on the grass against the mon: to the right of your side, beneath the foe's,
  // because that is where there is empty field in each case.
  // Clear of the sprite, not on top of it: to the right of the mon, or beneath it where the
  // right-hand side is occupied by its own Lead.
  const figureStyle: React.CSSProperties =
    slot.readout === 'right'
      ? { left: box.left + box.width + 8, top: box.top + box.height - 62 }
      : { left: box.left + box.width / 2 - 46, top: box.top + box.height + 6 };

  return (
    <div
      className="mon"
      style={{ inset: 0, zIndex: slot.z }}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div
        className="mon-shadow"
        style={{
          left: box.left + box.width * 0.2,
          top: slot.y - 7,
          width: box.width * 0.6,
          height: 4 * box.scale,
        }}
      />

      <img
        className={`mon-sprite ${attackClass} ${fainting ? 'fainting' : ''}`}
        src={url}
        alt={species.name}
        style={{ left: box.left, top: box.top, width: box.width, height: box.height }}
        onLoad={(e) => {
          const img = e.currentTarget;
          if (img.naturalWidth > 0) {
            setNatural({ width: img.naturalWidth, height: img.naturalHeight });
          }
        }}
      />

      <svg
        style={{ position: 'absolute', ...overlay, pointerEvents: 'none', overflow: 'visible' }}
        viewBox={`0 0 ${overlay.width} ${overlay.height}`}
        aria-hidden="true"
      >
        {combatant.shield > 0 && (
          <ellipse
            cx={localCX}
            cy={pad + box.height / 2}
            rx={box.width / 2 + SHIELD_BUBBLE.pad}
            ry={box.height / 2 + SHIELD_BUBBLE.pad}
            fill="rgba(111,211,232,0.12)"
            stroke="var(--shield)"
            strokeWidth={SHIELD_BUBBLE.strokeWidth}
          />
        )}
        <ChargeArc charge={combatant.charge} cx={localCX} cy={arcCY} durationMs={chargeDurationMs} />
      </svg>

      {combatant.shield > 0 && (
        <div className="shield-amount" style={{ left: box.left + box.width / 2 - 16, top: box.top - 2 }}>
          {combatant.shield}
        </div>
      )}

      {/* Attack in a burst, HP beside it, and a thin bar underneath so "how hurt is it" stays a
          glance rather than arithmetic. The name is on hover: with 183 species nobody recognises
          every sprite, but four permanent labels is a lot of text over the art. */}
      <div className="figures" style={figureStyle}>
        <div className="figures-row">
          <span className="figure-attack">
            <svg viewBox="0 0 46 46" width="46" height="46" aria-hidden="true">
              <path d={burstPath(23, 23, 22, 14.5)} fill="#14161a" stroke="#05070a" strokeWidth="1.5" />
            </svg>
            <em>{combatant.currentStats.attack}</em>
          </span>
          <span className="figure-hp">{hp}</span>
          <span className="figure-special" title="Special — what the ability is worth when the bar fills">
            {combatant.currentStats.special}
          </span>
        </div>
        <div className="hp-track">
          <div className={`hp-fill ${hpClass}`} style={{ width: `${hpFraction * 100}%` }} />
        </div>
        <MonBadges combatant={combatant} />
      </div>

      {hovered && (
        <div
          className="mon-name"
          style={{
            left: box.left + box.width / 2,
            top: slot.readout === 'right' ? box.top - 30 : box.top + box.height + 84,
          }}
        >
          <strong>{species.name}</strong>
          <span>
            {species.types.join(' / ')} &middot; {hp}/{maxHP} hp &middot;{' '}
            {combatant.currentStats.attack} atk &middot; {combatant.currentStats.special} sp
            &middot; spd {combatant.currentStats.speed}
          </span>
          {combatant.passive !== null && (
            <span className="mon-name-passive">{combatant.passive.displayName}</span>
          )}
        </div>
      )}

      {flash?.damage !== undefined && flash.damage > 0 && (
        <div className="float damage" style={{ left: box.left + box.width / 2, top: box.top - 6 }}>
          -{flash.damage}
        </div>
      )}
      {flash?.heal !== undefined && flash.heal > 0 && (
        <div className="float heal" style={{ left: box.left + box.width / 2 + 26, top: box.top - 6 }}>
          +{flash.heal}
        </div>
      )}
      {flash?.absorbed !== undefined && flash.absorbed > 0 && (
        <div className="float absorbed" style={{ left: box.left + box.width / 2 - 36, top: box.top + 6 }}>
          {flash.absorbed} blocked
        </div>
      )}
    </div>
  );
}

/**
 * Standing modifiers, written as words.
 *
 * Deliberately not icons. A badge row that showed lifesteal with a Grass leaf and damage
 * reduction with a Steel mark read, next to a mon, as that mon's typing — it was wrong and it was
 * removed. The type icons in the trait counters are the legitimate case: there the icon means a
 * type, because the count *is* a count of that type.
 */
function MonBadges({ combatant }: { combatant: Combatant }) {
  const badges: { key: string; className: string; label: string }[] = [];

  if (combatant.status !== null) {
    badges.push({ key: 'status', className: 'status', label: combatant.status.toLowerCase() });
  }
  if (combatant.damageReductionFlat > 0) {
    badges.push({ key: 'dr', className: 'block', label: `blocks ${combatant.damageReductionFlat}` });
  }
  if (combatant.lifestealPercent > 0) {
    badges.push({
      key: 'drain',
      className: 'drain',
      label: `drains ${Math.round(combatant.lifestealPercent * 100)}%`,
    });
  }
  if (combatant.statusWards > 0) {
    badges.push({
      key: 'ward',
      className: 'ward',
      label: combatant.statusWards === 1 ? 'warded' : `warded x${combatant.statusWards}`,
    });
  }

  if (badges.length === 0) return null;
  return (
    <div className="badges">
      {badges.map((b) => (
        <span key={b.key} className={`badge ${b.className}`}>
          {b.label}
        </span>
      ))}
    </div>
  );
}

export { ChargeArc };
