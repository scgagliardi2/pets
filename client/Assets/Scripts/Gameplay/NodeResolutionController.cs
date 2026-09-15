using System.Collections.Generic;
using UnityEngine;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;

namespace Pets.Gameplay
{
    /// <summary>Turns arriving at a map node into the thing that node is (design doc §5.1) — the
    /// piece that was missing while the map was walkable but inert. Lives on the Map scene alongside
    /// LocationMapController, which owns the graph and the walk and raises
    /// <see cref="LocationMapController.NodeArrived"/>; this owns what happens next.
    ///
    /// Where each node type is resolved:
    /// - PvE and Gym leave for the Battle scene, handing it the encounter through PendingBattle.
    ///   BattleScreenController is what writes the result back to the run (Morale, EXP, a catch) and
    ///   sends the player back here — see its OnResultContinueClicked.
    /// - Camp, shown as the Pokémon Center, leaves for the PokemonCenter scene — a shop (ADR 0013).
    ///   Its shelf is rolled here, from the node's seed, and kept on the run, so the scene itself
    ///   only has to show it; its Leave button brings the player back to this node.
    /// - Event rolls one of Meta/RoadEvents' encounters (ADR 0014) and resolves it in place, as an
    ///   overlay over the map: a choice, then what it did. The one choice that starts a fight — taking
    ///   on a Legendary — leaves for the Battle scene like a PvE node, with the bounty in PendingBattle.
    /// - PvP is still a stub, shown in the same overlay. The overlay covers the canvas and takes the
    ///   raycast, so the nodes underneath can't be clicked until it's dismissed — without that,
    ///   walking on would be a way to skip the node you just stepped onto.
    ///
    /// Resolution is skipped entirely when there's no species library behind the screen
    /// (ActiveRun.Library) — the Map scene opened on its own, with no run, can't roll an encounter
    /// from content it hasn't got, and walking a map to look at it is a normal thing to do while
    /// building the scene.</summary>
    public sealed class NodeResolutionController : MonoBehaviour
    {
        [SerializeField] private LocationMapController map;
        [SerializeField] private ResourceBarController resourceBar;
        [SerializeField] private NodeEventOverlayController eventOverlay;

        /// <summary>What encounters and the Pokémon Center's item row are drawn from.</summary>
        [SerializeField] private ItemLibrary itemLibrary;

        private string currentEventNodeId;

        /// <summary>True while a node's overlay is up — the map is not to be walked on until it's
        /// dismissed. Exposed for the PlayMode tests, which resolve a node the way a player does.</summary>
        public bool IsResolvingInPlace => eventOverlay.gameObject.activeSelf;

        /// <summary>The encounter the overlay is showing, until it's dismissed. Null for the PvP stub.</summary>
        public RoadEvent CurrentEvent { get; private set; }

        private void Start()
        {
            eventOverlay.gameObject.SetActive(false);
            eventOverlay.OnContinue = CloseOverlays;
            eventOverlay.OnChoice = OnEventChoice;

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

        private void OnNodeArrived(LocationMapNode node)
        {
            var library = ActiveRun.Library;
            if (library == null)
            {
                return;
            }

            var run = map.Run;
            // A Map opened on its own has no Location behind it; LocationCatalog falls back to the
            // Forest so there's still something to fight.
            var typeBias = LocationCatalog.CurrentFor(run).TypeBias;
            switch (node.Type)
            {
                case NodeType.PvE:
                    // Built at the Location's tier, not at base stats: what a wild fight is drawn
                    // from and how strong it is both follow the run's progress (RunProgression).
                    StartBattle(EncounterGenerator.GenerateWildLineUp(
                        library, typeBias, run.BadgeCount, node.Layer, SeedFor(run, node), $"wild-{node.Id}"), node, isGym: false);
                    break;
                case NodeType.Gym:
                    StartBattle(GymTeamGenerator.Generate(
                        library, typeBias, run.BadgeCount, run.LineUp.Count, SeedFor(run, node)), node, isGym: true);
                    break;
                case NodeType.Camp:
                    PokemonCenterShop.OpenFor(run, node.Id, SeedFor(run, node), library, itemLibrary);
                    ScreenFade.TransitionTo(SceneNames.PokemonCenter);
                    break;
                case NodeType.Event:
                    ShowRoadEvent(RoadEvents.Roll(run, library, itemLibrary, SeedFor(run, node)), node.Id);
                    break;
                default:
                    ShowPvPStub();
                    break;
            }
        }

        /// <summary>One seed per node per run, rolling both the encounter and the fight that follows
        /// it, so the whole thing is reproducible from the run seed (design doc §10.5).
        ///
        /// Built from the node's position rather than its id: string.GetHashCode isn't guaranteed to
        /// be the same value in the next process, so once runs are saved and resumed, hashing the id
        /// would quietly re-roll the encounter a saved run was carrying. The badge count is folded in
        /// so the same map position in two Locations isn't the same fight.</summary>
        private static int SeedFor(RunState run, LocationMapNode node) =>
            run.RunSeed ^ (node.Layer * 397 + node.IndexInLayer) ^ (run.BadgeCount * 7919);

        private void StartBattle(List<PokemonInstance> enemyLineUp, LocationMapNode node, bool isGym)
        {
            PendingBattle.Set(enemyLineUp, node.Id, isGym, SeedFor(map.Run, node));
            ScreenFade.TransitionTo(SceneNames.Battle);
        }

        /// <summary>Puts an encounter up and waits for a choice. Public so a dev capture can show a
        /// chosen encounter without walking to one.</summary>
        public void ShowRoadEvent(RoadEvent roadEvent, string nodeId)
        {
            CurrentEvent = roadEvent;
            currentEventNodeId = nodeId;
            eventOverlay.gameObject.SetActive(true);
            eventOverlay.ShowChoices(roadEvent.Title, roadEvent.Body, roadEvent.Choices);
        }

        private void OnEventChoice(int index)
        {
            var run = map.Run;
            var outcome = RoadEvents.Resolve(CurrentEvent, index, run, ActiveRun.Library, itemLibrary);
            if (outcome == null)
            {
                return;
            }

            if (outcome.StartsBattle)
            {
                eventOverlay.gameObject.SetActive(false);
                PendingBattle.SetLegendary(outcome.BattleLineUp, currentEventNodeId, CurrentEvent.Seed, outcome.Bounty);
                CurrentEvent = null;
                ScreenFade.TransitionTo(SceneNames.Battle);
                return;
            }

            eventOverlay.Show(CurrentEvent.Title, outcome.Message);
            resourceBar.Refresh(run);
        }

        private void ShowPvPStub()
        {
            CurrentEvent = null;
            eventOverlay.gameObject.SetActive(true);
            eventOverlay.Show("Mystery Trainer",
                "A Mystery Trainer's team would be waiting here.\nAsynchronous PvP isn't built yet.");
        }

        private void CloseOverlays()
        {
            eventOverlay.gameObject.SetActive(false);
            CurrentEvent = null;
            resourceBar.Refresh(map.Run);

            // An encounter can cost the last Morale, which ends the run exactly as a lost fight does.
            if (map.Run != null && map.Run.IsRunOver)
            {
                ActiveRun.End();
                ScreenFade.TransitionTo(SceneNames.Home);
            }
        }
    }
}
