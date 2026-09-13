using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Region Hub (design doc §5.2): shown after Character Select and after every Gym,
    /// it offers three Locations (LocationCatalog.OffersFor) and sends the run to the one the player
    /// picks. Each card names the Location, gives its flavor line and shows the Pokémon types it
    /// leans toward; the header says which Gym this will be and what levels to expect.
    ///
    /// Adapted to the game as built (ADR 0006): the Trailblazer travel minigame (§6) isn't built, so
    /// travel is instant; and every offer shares the run's difficulty tier — it's the badge count that
    /// sets how hard a Location is, not which one is chosen.
    ///
    /// The scene owns a RunBootstrapper, since Character Select hands its pair to this screen rather
    /// than to the map.</summary>
    public sealed class RegionHubController : MonoBehaviour
    {
        private const float TypeIconWidth = 56f;
        private const float TypeIconHeight = 28f;

        [SerializeField] private Text headerText;
        [SerializeField] private Text subheaderText;
        [SerializeField] private ResourceBarController resourceBar;
        [SerializeField] private GameObject typeIconPrefab;
        [SerializeField] private Text[] nameTexts;
        [SerializeField] private Text[] flavorTexts;
        [SerializeField] private RectTransform[] typeRows;
        [SerializeField] private Button[] travelButtons;

        private readonly List<LocationType> offers = new List<LocationType>();
        private bool travelling;

        /// <summary>The Locations on offer, in card order. Exposed for the PlayMode tests.</summary>
        public IReadOnlyList<LocationType> Offers => offers;

        private void Start()
        {
            var state = ActiveRun.State;
            if (state == null)
            {
                return;
            }

            if (!state.NeedsLocationChoice)
            {
                // Nothing to choose: a run already in a Location belongs on its map, and a finished
                // one at Home.
                ScreenFade.TransitionTo(state.CurrentLocation != null ? SceneNames.Map : SceneNames.Home);
                return;
            }

            offers.AddRange(LocationCatalog.OffersFor(state));
            resourceBar.Refresh(state);

            int badges = state.BadgeCount;
            headerText.text = badges == 0 ? "Choose your first Location" : "Choose your next Location";
            subheaderText.text =
                $"Gym {badges + 1} of {RunProgression.BadgesToWin}   -   " +
                $"wild Pokémon Lv {RunProgression.WildLevel(badges, 1)} to {RunProgression.WildLevel(badges, LocationMapGenerator.ChoiceLayerCount)}   -   " +
                $"Gym Leader Lv {RunProgression.GymLevel(badges)}";

            for (int i = 0; i < travelButtons.Length; i++)
            {
                bool offered = i < offers.Count;
                travelButtons[i].interactable = offered;
                if (!offered)
                {
                    continue;
                }

                var entry = LocationCatalog.Get(offers[i]);
                nameTexts[i].text = entry.DisplayName;
                flavorTexts[i].text = entry.Flavor;
                ShowTypes(typeRows[i], entry.TypeBias);
            }
        }

        /// <summary>A card's Travel button (wired with its card index by RegionHubSceneBuilder).</summary>
        public void OnTravelClicked(int index)
        {
            var state = ActiveRun.State;
            if (travelling || state == null || index < 0 || index >= offers.Count)
            {
                return;
            }

            // One choice per visit: a second click during the fade would otherwise re-pick.
            travelling = true;
            foreach (var button in travelButtons)
            {
                button.interactable = false;
            }

            // The Trailblazer minigame (design doc §6) would play here; until it exists, travel is instant.
            state.TravelTo(offers[index]);
            ScreenFade.TransitionTo(SceneNames.Map);
        }

        private void ShowTypes(RectTransform row, PokemonType[] types)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            foreach (var type in types)
            {
                var icon = Instantiate(typeIconPrefab, row);
                icon.name = $"TypeIcon_{type}";
                icon.GetComponent<TypeIconView>().Type = type;

                // Explicit == null rather than ??, for the fake-null reason TeamPanelController.Fade gives.
                var layout = icon.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = icon.AddComponent<LayoutElement>();
                }
                layout.preferredWidth = TypeIconWidth;
                layout.preferredHeight = TypeIconHeight;
            }
        }
    }
}
