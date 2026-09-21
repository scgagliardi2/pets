/**
 * Drag and drop, using the browser's own HTML5 drag events rather than a library.
 *
 * Two kinds of drag exist in the game and they must not be confused with each other: dragging a
 * Pokeball onto an enemy, and dragging a mon around the roster. Both ride the same
 * `dataTransfer`, so each payload carries its own kind and a drop target checks the kind before
 * it reacts. Without that, dropping a ball on the team builder would try to reorder a Pokeball.
 *
 * The payload is serialised as JSON under a custom MIME type. A custom type rather than
 * `text/plain` so a stray drag from outside the app — a selected word, a file — can't look like a
 * game payload; `dataTransfer.types` is readable during dragover, which is what lets a target
 * decide whether to accept the drop before it happens.
 */

export const DRAG_MIME = 'application/x-pets-drag';

export type DragPayload =
  | { kind: 'ball'; tier: string }
  | { kind: 'mon'; instanceId: string; from: 'lineUp' | 'box' }
  /**
   * A held item in flight. `from` is the mon it is currently on, or null if it came from the bag —
   * which is what lets a drop decide between moving an item and equipping a fresh one.
   */
  | { kind: 'item'; itemId: string; from: string | null };

export function setDragPayload(event: React.DragEvent, payload: DragPayload): void {
  event.dataTransfer.setData(DRAG_MIME, JSON.stringify(payload));
  // Some browsers refuse a drag with no text/plain set at all.
  event.dataTransfer.setData('text/plain', payload.kind);
  event.dataTransfer.effectAllowed = 'move';
}

export function readDragPayload(event: React.DragEvent): DragPayload | null {
  const raw = event.dataTransfer.getData(DRAG_MIME);
  if (raw === '') return null;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed === 'object' && parsed !== null && 'kind' in parsed) {
      return parsed as DragPayload;
    }
  } catch {
    // A malformed payload is a drag from somewhere else; ignore it rather than throwing into an
    // event handler the browser will swallow anyway.
  }
  return null;
}

/**
 * True when the drag in flight carries our payload at all.
 *
 * Checked during dragover, where the payload itself is deliberately unreadable — browsers only
 * expose the data on drop, to stop a page snooping on what is being dragged over it. The type
 * list is all a target gets, and all it needs.
 */
export const isOurDrag = (event: React.DragEvent): boolean =>
  event.dataTransfer.types.includes(DRAG_MIME);

/** Accepts a drop, which requires cancelling both dragover and dragenter. */
export function acceptDrop(event: React.DragEvent): void {
  if (!isOurDrag(event)) return;
  event.preventDefault();
  event.dataTransfer.dropEffect = 'move';
}
