using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    /// labelled Reserve and dimmed to read as dormant.
    ///
    /// Slots are rearranged by dragging a mon onto another slot, in either row — the rules for
    /// what a given drop means, including that the party can never be emptied, live in
    /// RunState.MoveMon. This class owns the gesture: it lifts the dragged card out of its slot
    /// onto a drag layer so the slot underneath can receive the drop, and rebuilds both rows once
    /// the drag is over.</summary>
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

        /// <summary>A card being dragged is slightly see-through, so the slot it's currently over
        /// stays visible underneath it.</summary>
        private const float DraggedCardAlpha = 0.85f;

        [SerializeField] private RectTransform partySlots;
        [SerializeField] private RectTransform boxSlots;
        [SerializeField] private Text partyHeaderText;
        [SerializeField] private Text boxHeaderText;
        [SerializeField] private GameObject typeIconPrefab;

        /// <summary>Where a card being dragged is parked while it follows the pointer: a
        /// full-screen, non-raycasting rect above the rest of the screen. It has to be outside the
        /// slot rows for two reasons — the card must draw over its neighbours, and the slot
        /// underneath the pointer has to be the thing that receives the drop.</summary>
        [SerializeField] private RectTransform dragLayer;

        /// <summary>Raised after a drag actually changed the run's line-up or Box, so the screen
        /// around this panel can update whatever else depends on it.</summary>
        public event Action Changed;

        /// <summary>Raised when a card is dropped on the release zone: which collection and slot
        /// it came from, and the name to put in front of the player. Nothing has been let go at
        /// this point — releasing is irreversible, so the screen confirms it first.</summary>
        public event Action<RosterGroup, int, string> ReleaseRequested;

        // The run this panel last drew. Kept so a completed drag can rebuild both rows itself
        // rather than the screen having to hand the state back in on every gesture.
        private RunState state;
        private PokemonSpeciesLibrary library;

        private RectTransform draggedCard;
        private Vector2 draggedCardSize;

        public void Refresh(RunState state, PokemonSpeciesLibrary library)
        {
            this.state = state;
            this.library = library;
            Rebuild();
        }

        private void Rebuild()
        {
            // Anything still parked on the drag layer belongs to the rows about to be thrown away.
            Clear(dragLayer);
            draggedCard = null;

            partyHeaderText.text = $"Party  {state.LineUp.Count} / {SlotsPerRow}";
            boxHeaderText.text = state.Box.Count > SlotsPerRow
                ? $"Box  showing {SlotsPerRow} of {state.Box.Count}"
                : $"Box  {state.Box.Count} / {SlotsPerRow}";

            BuildRow(partySlots, "PartySlot", state.LineUp, RosterGroup.Party);
            BuildRow(boxSlots, "BoxSlot", state.Box, RosterGroup.Box);
        }

        private void BuildRow(RectTransform row, string slotNamePrefix, List<PokemonInstance> mons, RosterGroup group)
        {
            Clear(row);
            bool isParty = group == RosterGroup.Party;
            for (int i = 0; i < SlotsPerRow; i++)
            {
                var mon = i < mons.Count ? mons[i] : null;
                var slot = new GameObject($"{slotNamePrefix}{i}", typeof(RectTransform));
                slot.transform.SetParent(row, false);
                var card = mon == null
                    ? BuildEmptySlot(slot.transform, isParty ? RoleLabel(i) : "Empty")
                    : BuildFilledSlot(slot.transform, mon, library, isParty ? RoleLabel(i) : "Box",
                        dormant: isParty && i >= 2);
                slot.AddComponent<TeamSlotView>()
                    .Initialize(this, group, i, mon != null, card.GetComponent<RectTransform>());
            }
        }

        /// <summary>Lifts the dragged slot's card onto the drag layer. Its size has to be pinned
        /// first: inside a slot the card is stretched to fill it, and a stretched rect reparented
        /// under a full-screen layer would stretch to the whole screen.</summary>
        internal void BeginSlotDrag(TeamSlotView slot, PointerEventData eventData)
        {
            draggedCard = slot.Card;
            draggedCardSize = draggedCard.rect.size;

            draggedCard.SetParent(dragLayer, false);
            draggedCard.anchorMin = draggedCard.anchorMax = new Vector2(0.5f, 0.5f);
            draggedCard.pivot = new Vector2(0.5f, 0.5f);
            draggedCard.sizeDelta = draggedCardSize;

            var group = draggedCard.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = draggedCard.gameObject.AddComponent<CanvasGroup>();
            }
            // Held cards read as picked up, and stop absorbing the raycast so the slot under the
            // pointer is what receives the drop.
            group.alpha = DraggedCardAlpha;
            group.blocksRaycasts = false;

            DragSlot(slot, eventData);
        }

        internal void DragSlot(TeamSlotView slot, PointerEventData eventData)
        {
            if (draggedCard == null)
            {
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, eventData.position, eventData.pressEventCamera, out var local))
            {
                draggedCard.anchoredPosition = local;
            }
        }

        /// <summary>Applies a completed drop. A refused move (see RunState.MoveMon) still rebuilds,
        /// which is what snaps the card back to where it came from.</summary>
        internal void MoveBetweenSlots(TeamSlotView source, TeamSlotView target)
        {
            if (state == null)
            {
                return;
            }
            bool moved = state.MoveMon(source.Group, source.Index, target.Group, target.Index);
            Rebuild();
            if (moved)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>A card dropped on the release zone. The rows are rebuilt right away — the
        /// card goes back to its slot while the question is asked — and the actual release waits
        /// on the screen's confirmation.</summary>
        internal void ReleaseFromSlot(TeamSlotView source)
        {
            if (state == null)
            {
                return;
            }
            var collection = state.CollectionFor(source.Group);
            if (source.Index >= collection.Count)
            {
                return;
            }
            var mon = collection[source.Index];
            string name = DisplayName(mon, library.GetById(mon.SpeciesId));

            Rebuild();
            ReleaseRequested?.Invoke(source.Group, source.Index, name);
        }

        /// <summary>End of the gesture. After a drop the rows have already been rebuilt and the
        /// drag layer is empty, so this only has work to do when the card was released somewhere
        /// that wasn't a slot.</summary>
        internal void EndSlotDrag()
        {
            if (draggedCard != null)
            {
                Rebuild();
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

        private GameObject BuildFilledSlot(Transform slot, PokemonInstance mon, PokemonSpeciesLibrary library,
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
            return card;
        }

        /// <summary>An unfilled slot: the same card frame, faded further, holding only what the
        /// slot is for. Drawn rather than left blank so the party reads as six slots with room to
        /// grow instead of as however many mons the run happens to have.</summary>
        private GameObject BuildEmptySlot(Transform slot, string label)
        {
            var card = PokemonCardBuilder.CreateCard(slot, "Card");
            Stretch(card.GetComponent<RectTransform>());
            card.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            PokemonCardBuilder.AddLine(card.transform, label, CardLineFontSize, FontStyle.Bold, Theme.TextMuted)
                .name = "RoleText";
            Fade(card, EmptySlotAlpha);
            return card;
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
            // Explicit == null rather than ??: GetComponent can hand back a "fake null" — a live
            // managed wrapper around no native component — which the null-coalescing operator
            // happily accepts, and the first property access on it then throws.
            var group = card.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = card.AddComponent<CanvasGroup>();
            }
            group.alpha = alpha;
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
