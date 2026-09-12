using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Pets.Data;
using Pets.Meta;
using Pets.Simulation;
using Pets.UI;

namespace Pets.Gameplay
{
    /// <summary>Team Management (design doc §5.2, §7): the run's line-up as six party slots in a
    /// row, with the Box's first six below them, each filled slot showing the mon's sprite, types
    /// and stats the same way a Character Select card does (both are built by
    /// Pets.UI.PokemonCardBuilder).
    ///
    /// Six slots is a *view* of the line-up "train" (§7), not six active mons: only the front two
    /// are ever mechanically live, so slot 0 is labelled Lead, slot 1 Support, and slots 2-5 are
    /// labelled Reserve and dimmed to read as dormant. Still read-only apart from the Lead/Support
    /// swap TeamScreenController owns — promoting out of the Box and reordering the reserves need
    /// the Box rules that are still PLAN.md Phase 1.</summary>
    public sealed class TeamPanelController : MonoBehaviour
    {
        /// <summary>Slots rendered per row. The party is capped at this; the Box is not (design
        /// doc §7 makes it the player's whole collection), so a fuller Box shows its first six
        /// here and says so in the section header — paging it is Phase 1 work along with the rest
        /// of Box management.</summary>
        public const int SlotsPerRow = 6;

        // Card budget for the 186-unit slot cell TeamSceneBuilder lays out, in the same terms as
        // CharacterSelectController's: 12 padding + 20 role + 80 sprite + 23 name + 26 types row +
        // 21 stats + 4 one-unit gaps = 186. Every line sits above Theme.FontSizeSmall (13) rather
        // than at it — at 13 the stat line came out as grey mush in the first capture of this
        // screen, the same way Character Select's card did before its sizes went up. Grow
        // TeamSceneBuilder.SlotHeight with any of these.
        private const int CardSpriteHeight = 80;
        private const int CardNameFontSize = 17;
        private const int CardLineFontSize = 15;
        private const int CardRoleFontSize = 14;
        private const int CardTypesRowHeight = 26;

        /// <summary>How far a Reserve slot's card is faded, to show that everything behind the
        /// Support is dormant (design doc §7) without hiding what it is — far enough to read as
        /// inactive next to the Lead/Support pair, not so far that its stats stop being
        /// readable.</summary>
        private const float ReserveAlpha = 0.7f;

        /// <summary>An unfilled slot is faded further still: it's a frame with a label in it, and
        /// it shouldn't compete with the mons on either side of it.</summary>
        private const float EmptySlotAlpha = 0.45f;

        [SerializeField] private RectTransform partySlots;
        [SerializeField] private RectTransform boxSlots;
        [SerializeField] private Text partyHeaderText;
        [SerializeField] private Text boxHeaderText;
        [SerializeField] private GameObject typeIconPrefab;

        public void Refresh(RunState state, PokemonSpeciesLibrary library)
        {
            partyHeaderText.text = $"Party  {state.LineUp.Count} / {SlotsPerRow}";
            boxHeaderText.text = state.Box.Count > SlotsPerRow
                ? $"Box  showing {SlotsPerRow} of {state.Box.Count}"
                : $"Box  {state.Box.Count} / {SlotsPerRow}";

            BuildRow(partySlots, "PartySlot", state.LineUp, library, isParty: true);
            BuildRow(boxSlots, "BoxSlot", state.Box, library, isParty: false);
        }

        private void BuildRow(RectTransform row, string slotNamePrefix, List<PokemonInstance> mons,
            PokemonSpeciesLibrary library, bool isParty)
        {
            Clear(row);
            for (int i = 0; i < SlotsPerRow; i++)
            {
                var mon = i < mons.Count ? mons[i] : null;
                var slot = new GameObject($"{slotNamePrefix}{i}", typeof(RectTransform));
                slot.transform.SetParent(row, false);
                if (mon == null)
                {
                    BuildEmptySlot(slot.transform, isParty ? RoleLabel(i) : "Empty");
                }
                else
                {
                    BuildFilledSlot(slot.transform, mon, library, isParty ? RoleLabel(i) : "Box",
                        dormant: isParty && i >= 2);
                }
            }
        }

        /// <summary>Lead/Support/Reserve for a party index. Only the first two are mechanically
        /// active (design doc §7) — the label is the whole point of showing six slots rather than
        /// two, so a player can see who steps up next without it implying they're in the fight.</summary>
        private static string RoleLabel(int partyIndex)
        {
            switch (partyIndex)
            {
                case 0: return "Lead";
                case 1: return "Support";
                default: return "Reserve";
            }
        }

        private void BuildFilledSlot(Transform slot, PokemonInstance mon, PokemonSpeciesLibrary library,
            string roleLabel, bool dormant)
        {
            var species = library.GetById(mon.SpeciesId);
            var card = PokemonCardBuilder.CreateCard(slot, "Card");
            Stretch(card.GetComponent<RectTransform>());

            PokemonCardBuilder.AddLine(card.transform, $"{roleLabel}  Lv.{mon.Level}",
                CardRoleFontSize, FontStyle.Bold, Theme.TextMuted).name = "RoleText";
            PokemonCardBuilder.AddSprite(card.transform, PokemonSprites.Load(species), CardSpriteHeight);
            PokemonCardBuilder.AddLine(card.transform, DisplayName(mon, species),
                CardNameFontSize, FontStyle.Bold, Theme.TextDark).name = "NameText";
            if (species != null)
            {
                PokemonCardBuilder.AddTypeIcons(card.transform, typeIconPrefab, CardTypesRowHeight,
                    species.Type1, species.HasSecondType, species.Type2);
            }
            // The instance's own stats, not the species' base ones: these are what a fight would
            // actually use once levels and line-up modifiers are folded in (battle-sim-spec.md §8).
            PokemonCardBuilder.AddLine(card.transform,
                $"ATK {mon.CurrentStats.Attack}  HP {mon.CurrentStats.Health}  SPD {mon.CurrentStats.Speed}",
                CardLineFontSize, FontStyle.Bold, Theme.TextDark).name = "StatsText";

            if (dormant)
            {
                Fade(card, ReserveAlpha);
            }
        }

        /// <summary>An unfilled slot: the same card frame, faded further, holding only what the
        /// slot is for. Drawn rather than left blank so the party reads as six slots with room to
        /// grow instead of as however many mons the run happens to have.</summary>
        private void BuildEmptySlot(Transform slot, string label)
        {
            var card = PokemonCardBuilder.CreateCard(slot, "Card");
            Stretch(card.GetComponent<RectTransform>());
            card.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            PokemonCardBuilder.AddLine(card.transform, label, CardLineFontSize, FontStyle.Bold, Theme.TextMuted)
                .name = "RoleText";
            Fade(card, EmptySlotAlpha);
        }

        private static string DisplayName(PokemonInstance mon, PokemonSpeciesDefinitionAsset species)
        {
            if (!string.IsNullOrEmpty(mon.Nickname))
            {
                return mon.Nickname;
            }
            return species != null ? species.DisplayName : mon.SpeciesId.ToString();
        }

        /// <summary>Fades a whole card in one place via a CanvasGroup rather than tinting each
        /// Graphic: the type icons carry their own baked colour, so per-Image tinting would have
        /// to know which children are allowed to be recoloured and which aren't.</summary>
        private static void Fade(GameObject card, float alpha)
        {
            card.AddComponent<CanvasGroup>().alpha = alpha;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Deactivates each old slot as well as destroying it: Destroy only takes
        /// effect at the end of the frame, so a Refresh triggered by a Lead/Support swap would
        /// otherwise leave the previous slots alive alongside the new ones for the rest of that
        /// frame — long enough to be found, drawn on top of, or clicked.</summary>
        private static void Clear(RectTransform row)
        {
            for (int i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
