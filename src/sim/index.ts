/**
 * The battle simulation: pure TypeScript, zero React.
 *
 * This is the single most important structural decision in the rebuild
 * (REACT_REBUILD_REFERENCE.md §5.1). In the Unity version the equivalent separation is what made
 * the combat testable at all, and every bug that *wasn't* caught early lived in the layer that
 * had engine dependencies. Nothing under /src/sim may import React, touch the DOM, or read a
 * clock.
 */

export * from './types.js';
export * from './config.js';
export * from './rng.js';
export * from './damage.js';
export * from './advanceStep.js';
export * from './synergy.js';
export * from './runners.js';
