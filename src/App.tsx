/**
 * The run loop: map, fight, result, repeat, until the badges or the morale run out.
 *
 * The battle screen is handed a line-up and an opponent and reports an outcome. It knows nothing
 * about badges, morale or EXP — that all lives in the run store, which is what lets the same
 * screen serve a wild encounter and a Gym.
 */

import { useMemo } from 'react';

import { BattleScreen } from './ui/BattleScreen.js';
import { BagPopup } from './ui/BagPopup.js';
import { EvolutionPopup } from './ui/EvolutionPopup.js';
import { PokemonDetailModal } from './ui/PokemonDetailModal.js';
import {
  BoxScreen,
  BuffPanel,
  CelebrationPanel,
  EncounterPanel,
  GrowthPanel,
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
  const detailInstanceId = useRunStore((s) => s.detailInstanceId);
  const evolutionQueue = useRunStore((s) => s.evolutionQueue);
  const bagOpen = useRunStore((s) => s.bagOpen);
  // Sits above every other screen, including the battle: an evolution can land on the result
  // overlay, and it should be the thing in front when it does.
  const evolving = evolutionQueue[0];

  // The line-up is copied at the moment the fight starts, so reordering the team mid-battle
  // cannot change the fight already in progress.
  const runKey = useMemo(
    () => `${run.seed}-${run.badges}-${activeNode?.id ?? 'none'}`,
    [run.seed, run.badges, activeNode],
  );

  // The result is an overlay on the finished battle, not a screen that replaces it. The board is
  // what the player wants to read at that moment, and cutting to the map throws it away.
  if ((phase === 'battle' || phase === 'result') && activeNode !== null) {
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
        {phase === 'result' && (
          <div className="result-overlay">
            <div className="result-stack">
              <ResultPanel />
              <GrowthPanel />
            </div>
          </div>
        )}
        {evolving !== undefined && <EvolutionPopup evolution={evolving} />}
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
        ) : phase === 'shop' ? (
          <ShopPanel />
        ) : phase === 'encounter' ? (
          <EncounterPanel />
        ) : phase === 'celebration' ? (
          <CelebrationPanel />
        ) : phase === 'buff' ? (
          <BuffPanel />
        ) : (
          <LocationMap />
        )}
      </div>
      <TeamBuilder />
      {bagOpen && <BagPopup />}
      {detailInstanceId !== null && <PokemonDetailModal instanceId={detailInstanceId} />}
      {evolving !== undefined && <EvolutionPopup evolution={evolving} />}
    </div>
  );
}
