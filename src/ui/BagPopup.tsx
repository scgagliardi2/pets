/**
 * The Bag: every item not currently held by somebody.
 *
 * Items are dragged out of here onto a Pokémon. It stays open while you drag, because the useful
 * motion is bag-to-mon and closing on the first click would fight that.
 *
 * Unlike the other popups, this one is not wrapped in a full-screen `.modal-scrim`. A scrim is a
 * `position: fixed; inset: 0` element, and — dark backdrop or not — a positioned element paints
 * over ordinary in-flow content underneath it, so it would sit on top of the team builder and
 * swallow every pointer and drag event meant for the Pokémon cards. That made "drag an item onto
 * a Pokémon" impossible: the thing you were supposed to drop onto was there visually but
 * unreachable. Instead this floats as its own panel, leaving the rest of the screen — the team
 * included — live underneath it, and closes on an outside click via a plain document listener
 * rather than a covering element.
 */

import { useEffect, useRef } from 'react';

import { itemById } from '../content/items.js';
import { bagContents } from '../meta/runState.js';
import { useRunStore } from '../state/runStore.js';
import { setDragPayload } from './dragDrop.js';

export function BagPopup() {
  const run = useRunStore((s) => s.run);
  const close = useRunStore((s) => s.closeBag);
  const ids = bagContents(run);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onPointerDown = (event: PointerEvent): void => {
      if (panelRef.current !== null && !panelRef.current.contains(event.target as Node)) {
        close();
      }
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [close]);

  return (
    <div className="panel bag-modal" ref={panelRef}>
      <button className="modal-close" onClick={close} aria-label="Close">
        ✕
      </button>
      <h2>Bag</h2>
      <p className="shop-sub quiet">
        Drag an item onto a Pokémon to give it. A Pokémon holds one at a time — giving it another
        returns the old one here.
      </p>

      <div className="bag-grid">
        {ids.map((id) => {
          const item = itemById(id);
          if (item === null) return null;
          const count = run.bag[id] ?? 0;
          return (
            <div
              key={id}
              className={`bag-item kind-${item.kind}`}
              draggable
              onDragStart={(e) => setDragPayload(e, { kind: 'item', itemId: id, from: null })}
              title={item.blurb}
            >
              <span className="bag-item-name">{item.name}</span>
              <span className="bag-item-blurb">{item.blurb}</span>
              {count > 1 && <span className="bag-item-count">x{count}</span>}
            </div>
          );
        })}
        {ids.length === 0 && (
          <p className="map-note">Nothing in the bag. Shops sell items, and encounters give them.</p>
        )}
      </div>
    </div>
  );
}
