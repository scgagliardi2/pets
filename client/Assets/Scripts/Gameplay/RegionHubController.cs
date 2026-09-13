using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Region Hub (design doc §5.2): the screen between Locations, where the run picks
    /// which of three candidate Locations to travel to next. Picking one generates its node-map and
    /// leaves for the Map scene.
    ///
    /// This is what makes a run a *run* rather than a single Location. Until now, beating a Gym
    /// ended everything at Home (ADR 0003) — there was nowhere to go back to — which meant the
    /// growth curve had one Location to happen in. A run is six Locations and six badges now
    /// (RegionTier.RegionsPerRun), and this screen is the seam between them.
    ///
    /// It shows what a player needs to choose with: each offer's type bias (LocationCatalog), and
    /// the difficulty tier the next Location will be built at (RegionTier). The Trailblazer travel
    /// minigame that design doc §6 puts between choosing and arriving isn't built, so choosing goes
    /// straight to the map.</summary>
    public sealed class RegionHubController : MonoBehaviour
    {
        [SerializeField] private Text headlineText;
        [SerializeField] private Text progressText;
        [SerializeField] private RectTransform offerRow;

        /// <summary>Layers the Locations chosen here are generated with — the same default the Map
        /// scene uses, kept here because this screen is what generates the map now.</summary>
        [SerializeField] private int mapLayerCount = RegionMapGenerator.DefaultLayerCount;

        /// <summary>The offers on screen, in the order they're shown. Exposed for the PlayMode
        /// tests, which need to know what the buttons they're clicking mean.</summary>
        public IReadOnlyList<RegionHubGenerator.Offer> Offers { get; private set; }
            = new List<RegionHubGenerator.Offer>();

        private void Start()
        {
            // There is always a run here: this scene carries a RunBootstrapper, so opening it
            // directly builds a default one exactly as the Map scene does.
            var run = ActiveRun.State;

            headlineText.text = run.Badges == 0
                ? "Where to first?"
                : $"Badge {run.Badges} earned. Where next?";
            progressText.text =
                $"Location {Mathf.Min(run.RegionIndex, RegionTier.RegionsPerRun)} of {RegionTier.RegionsPerRun}" +
                $"    Badges {run.Badges} / {RegionTier.RegionsPerRun}" +
                $"    Morale {run.Morale}";

            Offers = RegionHubGenerator.Offers(run);
            BuildOfferCards(run);
        }

        private void BuildOfferCards(RunState run)
        {
            for (int i = offerRow.childCount - 1; i >= 0; i--)
            {
                Destroy(offerRow.GetChild(i).gameObject);
            }

            for (int i = 0; i < Offers.Count; i++)
            {
                BuildOfferCard(Offers[i], i, run);
            }
        }

        private void BuildOfferCard(RegionHubGenerator.Offer offer, int index, RunState run)
        {
            var entry = LocationCatalog.For(offer.Type);

            var card = new GameObject($"OfferCard{index}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(offerRow, false);
            card.GetComponent<Image>().color = Theme.PanelBg;
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            AddCardLine(card.transform, "NameText", entry.DisplayName, Theme.FontSizeHeading, FontStyle.Bold, Theme.TextDark);
            AddCardLine(card.transform, "FlavorText", entry.Flavor, Theme.FontSizeBody, FontStyle.Italic, Theme.TextMuted);
            AddCardLine(card.transform, "TypesText", TypeBiasLine(entry), Theme.FontSizeBody, FontStyle.Normal, Theme.TextDark);
            AddCardLine(card.transform, "TierText", TierLine(run), Theme.FontSizeBody, FontStyle.Normal, Theme.TextMuted);

            var button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            var chosen = offer;
            button.onClick.AddListener(() => Choose(chosen));
        }

        private static string TypeBiasLine(LocationCatalog.Entry entry) =>
            "Wildlife: " + string.Join(", ", entry.TypeBias);

        /// <summary>What the next Location's opposition will be, in the player's terms — the two
        /// levers RegionTier actually pulls, so the choice isn't made blind.</summary>
        private static string TierLine(RunState run)
        {
            int stage = RegionTier.MaxEvolutionStageFor(run.RegionIndex);
            string forms = stage == 0 ? "base forms" : stage == 1 ? "up to first evolutions" : "fully evolved";
            return $"Wild Lv {RegionTier.EnemyLevel(run, NodeType.PvE)}, {forms}" +
                $"    Gym Lv {RegionTier.EnemyLevel(run, NodeType.Gym)}";
        }

        private static void AddCardLine(Transform parent, string name, string content, int fontSize,
            FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Theme.GameFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = fontSize + 6;
        }

        /// <summary>Takes the offer: the run enters that Location and the map screen picks it up
        /// from run state, exactly as it does when resuming one.</summary>
        public void Choose(RegionHubGenerator.Offer offer)
        {
            if (!ActiveRun.HasRun)
            {
                return;
            }
            ActiveRun.State.StartLocation(offer, mapLayerCount);
            ScreenFade.TransitionTo(SceneNames.Map);
        }
    }
}
