using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>The Pokémon Center scene (ADR 0013): a shop, reached by walking onto a Location map's
    /// Center node. Three shelf rows (ADR 0014) — three Pokémon up for adoption, a card per Poké Ball
    /// tier, and three held items — a clerk's line that answers each purchase, and the run's Money,
    /// Balls and bag in the title bar. Leave goes back to the map; Team goes to the Team screen and
    /// comes back here, which is where a freshly bought item gets put on a mon.
    ///
    /// What's for sale and what it costs is Meta/PokemonCenterShop's; this only shows it and forwards
    /// the clicks. The shelf is rolled by NodeResolutionController when the node is reached and kept
    /// on the run (RunState.CenterStock), so this scene can be left and re-entered without rerolling.
    /// Balls go into the same BallInventory the battle screen's tray throws from (ADR 0012), and are
    /// drawn in the same per-tier tint (CatchTrayView.TintFor).
    ///
    /// Card counts are fixed by the scene builder: ball card n is BallCatalog.AllTiers[n], and Pokémon
    /// and item card n are the stock's nth offer.</summary>
    public sealed class PokemonCenterController : MonoBehaviour
    {
        public const string WelcomeLine =
            "Welcome to the Pokémon Center! Pokémon looking for a trainer, Poké Balls, and held items.";

        [SerializeField] private ItemLibrary itemLibrary;
        [SerializeField] private Text moneyValue;
        [SerializeField] private Text ballsValue;
        [SerializeField] private Text itemsValue;
        [SerializeField] private Text clerkText;
        [SerializeField] private ShopPokemonCardView[] pokemonCards;
        [SerializeField] private ShopItemCardView[] ballCards;
        [SerializeField] private ShopItemCardView[] itemCards;

        public PokemonCenterStock Stock { get; private set; }
        public Text ClerkText => clerkText;

        private void Start()
        {
            var state = ActiveRun.State;
            if (state == null)
            {
                // Opened on its own, with no run: nothing to buy with and nothing to buy for.
                clerkText.text = "The Pokémon Center is closed. Start a New Game from the Home screen.";
                foreach (var card in pokemonCards)
                {
                    card.gameObject.SetActive(false);
                }
                foreach (var card in ballCards.Concat(itemCards))
                {
                    card.gameObject.SetActive(false);
                }
                return;
            }

            // Normally already open — the map rolled it on arrival. A Center scene reached any other
            // way (opened directly with a run in memory) opens one against where the run stands.
            Stock = state.CenterStock ?? PokemonCenterShop.OpenFor(state,
                state.VisitedMapNodeIds.LastOrDefault() ?? "center", state.RunSeed, ActiveRun.Library, itemLibrary);

            clerkText.text = WelcomeLine;
            Refresh();
        }

        /// <summary>A ball card's Buy button (wired with its card index by PokemonCenterSceneBuilder).</summary>
        public void OnBuyBallClicked(int index)
        {
            var state = ActiveRun.State;
            if (state == null || index < 0 || index >= BallCatalog.AllTiers.Length)
            {
                return;
            }

            var tier = BallCatalog.AllTiers[index];
            Say(PokemonCenterShop.BuyBall(state, tier),
                $"Here's your {BallCatalog.DisplayName(tier)}! Throw it at a wild Pokémon during a fight.");
            Refresh();
        }

        /// <summary>An item card's Buy button (wired with its card index by PokemonCenterSceneBuilder).</summary>
        public void OnBuyItemClicked(int index)
        {
            var state = ActiveRun.State;
            if (state == null || Stock == null || index < 0 || index >= Stock.Items.Count)
            {
                return;
            }

            string name = itemLibrary != null ? itemLibrary.GetById(Stock.Items[index])?.DisplayName : null;
            Say(PokemonCenterShop.BuyItem(state, index, itemLibrary),
                $"The {name} is in your bag. Give it to a Pokémon from the Team screen.",
                "I'm sorry, that was our last one.");
            Refresh();
        }

        /// <summary>A Pokémon card's Adopt button (wired with its card index by PokemonCenterSceneBuilder).</summary>
        public void OnAdoptClicked(int index)
        {
            var state = ActiveRun.State;
            if (state == null || Stock == null || index < 0 || index >= Stock.Pokemon.Count)
            {
                return;
            }

            var mon = Stock.Pokemon[index];
            string name = mon != null ? ActiveRun.Library?.GetById(mon.SpeciesId)?.DisplayName : null;
            Say(PokemonCenterShop.BuyPokemon(state, index),
                $"{name} is going home with you! It's waiting in your Box — field it from the Team screen.",
                "That Pokémon has already found a home.");
            Refresh();
        }

        private void Say(PokemonCenterShop.Purchase result, string boughtLine, string soldOutLine = null)
        {
            switch (result)
            {
                case PokemonCenterShop.Purchase.Bought:
                    clerkText.text = boughtLine;
                    break;
                case PokemonCenterShop.Purchase.NotEnoughMoney:
                    clerkText.text = "I'm sorry, you don't have enough money for that.";
                    break;
                case PokemonCenterShop.Purchase.SoldOut:
                    clerkText.text = soldOutLine ?? "That's sold out.";
                    break;
                default:
                    clerkText.text = "That isn't for sale.";
                    break;
            }
        }

        private void Refresh()
        {
            var state = ActiveRun.State;
            var library = ActiveRun.Library;

            moneyValue.text = $"Money {state.Money}";
            ballsValue.text = $"Balls {state.Balls.Total()}";
            itemsValue.text = $"Items {state.Items.Count}";

            RefreshPokemon(state, library);
            RefreshBalls(state);
            RefreshItems(state);
        }

        private void RefreshPokemon(RunState state, PokemonSpeciesLibrary library)
        {
            for (int i = 0; i < pokemonCards.Length; i++)
            {
                var card = pokemonCards[i];
                if (Stock == null || i >= Stock.Pokemon.Count)
                {
                    card.ShowEmpty();
                    continue;
                }

                var mon = Stock.Pokemon[i];
                if (mon == null)
                {
                    card.MarkSold();
                    continue;
                }

                var species = library?.GetById(mon.SpeciesId);
                if (species == null)
                {
                    card.ShowEmpty();
                    continue;
                }
                int? toGo = ExperienceResolver.ExpToNextEvolution(mon, library);
                string info = toGo.HasValue
                    ? $"Tier {species.Tier}\nEvo in {toGo.Value}"
                    : $"Tier {species.Tier}\n{mon.Exp} EXP";
                // LoadFront, not Load: the adoption cards sit alongside the Pokédex and the party,
                // and the official artwork here would be the one screen still showing illustrations.
                card.Show(PokemonSprites.LoadFront(species), species.DisplayName, species.Type1, species.HasSecondType,
                    species.Type2, mon.CurrentStats, SpeciesTier.MaxSpeed, info, PokemonCenterShop.PokemonPrice,
                    state.Money >= PokemonCenterShop.PokemonPrice);
            }
        }

        private void RefreshBalls(RunState state)
        {
            for (int i = 0; i < ballCards.Length; i++)
            {
                if (i >= BallCatalog.AllTiers.Length)
                {
                    ballCards[i].gameObject.SetActive(false);
                    continue;
                }
                var tier = BallCatalog.AllTiers[i];
                int price = PokemonCenterShop.BallPrice(tier);
                ballCards[i].Show(BallCatalog.DisplayName(tier), Theme.PokeballSprite, CatchTrayView.TintFor(tier),
                    BallDescription(tier), $"Have {state.Balls.CountOf(tier)}", price, state.Money >= price);
            }
        }

        private void RefreshItems(RunState state)
        {
            for (int i = 0; i < itemCards.Length; i++)
            {
                var card = itemCards[i];
                if (Stock == null || i >= Stock.Items.Count)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }
                if (Stock.Items[i] == null)
                {
                    card.MarkSold();
                    continue;
                }

                var item = itemLibrary != null ? itemLibrary.GetById(Stock.Items[i]) : null;
                if (item == null)
                {
                    card.gameObject.SetActive(false);
                    continue;
                }
                int owned = state.Items.Count(id => id == item.Id)
                    + state.LineUp.Concat(state.Box).Count(m => m.HeldItemId == item.Id);
                card.Show(item.DisplayName, item.Icon, Color.white, item.Description, $"Have {owned}",
                    item.Price, state.Money >= item.Price);
            }
        }

        /// <summary>The two numbers that tell the tiers apart (BallCatalog): the odds against a
        /// full-health target, and how much of a catch's EXP the ball keeps.</summary>
        private static string BallDescription(BallTier tier)
        {
            int odds = Mathf.RoundToInt(BallCatalog.BaseChance(tier) * 100f);
            int? cap = BallCatalog.ExpCap(tier);
            return cap.HasValue
                ? $"{odds}% to catch at full health. Keeps up to {cap.Value} EXP."
                : $"{odds}% to catch at full health. Keeps all its EXP.";
        }
    }
}
