/**
 * The run loop: map, fight, result, repeat, until the badges or the morale run out.
 *
 * The battle screen is handed a line-up and an opponent and reports an outcome. It knows nothing
 * about badges, morale or EXP — that all lives in the run store, which is what lets the same
 * screen serve a wild encounter and a Gym.
 */

import { useMemo } from 'react';

import { BattleScreen } from './ui/BattleScreen.js';
import {
  BoxScreen,
  LocationMap,
  ResultPanel,
  RunHeader,
  RunOverPanel,
  ShopPanel,
  TeamBuilder,
} from './ui/RunScreen.js';
import { useRunStore } from './state/runStore.js';
import './ui/theme.css';

export default function App() {
  const phase = useRunStore((s) => s.phase);
  const run = useRunStore((s) => s.run);
  const opponents = useRunStore((s) => s.opponents);
  const activeNode = useRunStore((s) => s.activeNode);
  const finishBattle = useRunStore((s) => s.finishBattle);

  // The line-up is copied at the moment the fight starts, so reordering the team mid-battle
  // cannot change the fight already in progress.
  const runKey = useMemo(
    () => `${run.seed}-${run.badges}-${activeNode?.id ?? 'none'}`,
    [run.seed, run.badges, activeNode],
  );

  if (phase === 'battle' && activeNode !== null) {
    return (
      <div className="app">
        <RunHeader />
        <BattleScreen
          own={[...run.lineUp]}
          foe={opponents}
          seed={run.seed + activeNode.layer}
          scenarioNote={activeNode.type === 'Gym' ? 'Gym Leader' : activeNode.label}
          runKey={runKey}
          onFinished={finishBattle}
        />
      </div>
    );
  }

  // The Box is a screen of its own rather than a panel, because it is where you compare
  // everything you own and that wants the whole width.
  if (phase === 'box') {
    return (
      <div className="app">
        <RunHeader />
        <BoxScreen />
      </div>
    );
  }

  return (
    <div className="app">
      <RunHeader />
      <div className="run-main">
        {phase === 'over' ? (
          <RunOverPanel />
        ) : phase === 'result' ? (
          <ResultPanel />
        ) : phase === 'shop' ? (
          <ShopPanel />
        ) : (
          <LocationMap />
        )}
      </div>
      <TeamBuilder />
    </div>
  );
}
