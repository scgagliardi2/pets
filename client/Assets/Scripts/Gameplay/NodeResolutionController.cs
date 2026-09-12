using System.Collections.Generic;
using UnityEngine;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Gameplay
{
    /// <summary>Turns arriving at a map node into the thing that node is (design doc §5.1) — the
    /// piece that was missing while the map was walkable but inert. Lives on the Map scene alongside
    /// RegionMapController, which owns the graph and the walk and raises
    /// <see cref="RegionMapController.NodeArrived"/>; this owns what happens next.
    ///
    /// Where each node type is resolved:
    /// - PvE and Gym leave for the Battle scene, handing it the encounter through PendingBattle.
    ///   BattleScreenController is what writes the result back to the run (Morale, EXP, a catch) and
    ///   sends the player back here — see its OnResultContinueClicked.
    /// - Camp (shown as the Pokémon Center) and the Event/PvP stubs resolve in place, as overlays
    ///   over the map. Each covers the canvas and takes the raycast, so the nodes underneath can't
    ///   be clicked until it's dismissed — without that, walking on would be a way to skip the node
    ///   you just stepped onto.
    ///
    /// Resolution is skipped entirely when there's no species library behind the screen
    /// (ActiveRun.Library) — the Map scene opened on its own, with no run, can't roll an encounter
    /// from content it hasn't got, and walking a map to look at it is a normal thing to do while
    /// building the scene.</summary>
    public sealed class NodeResolutionController : MonoBehaviour
    {
        [SerializeField] private RegionMapController map;
        [SerializeField] private ResourceBarController resourceBar;
        [SerializeField] private CampPanelController campOverlay;
        [SerializeField] private NodeEventOverlayController eventOverlay;

        /// <summary>True while a node's overlay is up — the map is not to be walked on until it's
        /// dismissed. Exposed for the PlayMode tests, which resolve a node the way a player does.</summary>
        public bool IsResolvingInPlace =>
            campOverlay.gameObject.activeSelf || eventOverlay.gameObject.activeSelf;

        private void Start()
        {
            campOverlay.gameObject.SetActive(false);
            eventOverlay.gameObject.SetActive(false);
            campOverlay.OnContinue = CloseOverlays;
            eventOverlay.OnContinue = CloseOverlays;

            map.NodeArrived += OnNodeArrived;

            // Morale/Money as they stand on arrival — including after a fight that cost Morale,
            // since coming back from the Battle scene reloads this one.
            resourceBar.Refresh(map.Run);
        }

        private void OnDestroy()
        {
            if (map != null)
            {
                map.NodeArrived -= OnNodeArrived;
            }
        }

        private void OnNodeArrived(RegionMapNode node)
        {
            var library = ActiveRun.Library;
            if (library == null)
            {
                return;
            }

            var run = map.Run;
            switch (node.Type)
            {
                case NodeType.PvE:
                    StartBattle(EncounterGenerator.GenerateWildLineUp(
                        library, ForestLocationFactory.TypeBias, SeedFor(run, node), $"wild-{node.Id}"), node, isGym: false);
                    break;
                case NodeType.Gym:
                    StartBattle(GymTeamGenerator.Generate(
                        library, run.LineUp.Count, SeedFor(run, node)), node, isGym: true);
                    break;
                case NodeType.Camp:
                    campOverlay.gameObject.SetActive(true);
                    campOverlay.Begin(run);
                    break;
                default:
                    ShowStub(node.Type);
                    break;
            }
        }

        /// <summary>One seed per node per run, rolling both the encounter and the fight that follows
        /// it, so the whole thing is reproducible from the run seed (design doc §10.5).
        ///
        /// Built from the node's position rather than its id: string.GetHashCode isn't guaranteed to
        /// be the same value in the next process, so once runs are saved and resumed, hashing the id
        /// would quietly re-roll the encounter a saved run was carrying.</summary>
        private static int SeedFor(RunState run, RegionMapNode node) =>
            run.RunSeed ^ (node.Layer * 397 + node.IndexInLayer);

        private void StartBattle(List<PokemonInstance> enemyLineUp, RegionMapNode node, bool isGym)
        {
            PendingBattle.Set(enemyLineUp, node.Id, isGym, SeedFor(map.Run, node));
            ScreenFade.TransitionTo(SceneNames.Battle);
        }

        private void ShowStub(NodeType type)
        {
            eventOverlay.gameObject.SetActive(true);
            if (type == NodeType.PvP)
            {
                eventOverlay.Show("Mystery Trainer",
                    "A Mystery Trainer's team would be waiting here.\nAsynchronous PvP isn't built yet.");
            }
            else
            {
                eventOverlay.Show("Encounter",
                    "Something happens on the path...\nEvent branches aren't written yet.");
            }
        }

        private void CloseOverlays()
        {
            campOverlay.gameObject.SetActive(false);
            eventOverlay.gameObject.SetActive(false);
            resourceBar.Refresh(map.Run);
        }
    }
}
