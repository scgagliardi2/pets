using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>
    /// A scripted-opponent team snapshot for one round. Same shape intentionally reused later for
    /// async PvP snapshots — see PLAN.md §2.2 and content-schema.md §4.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBotTeam", menuName = "Pets/Bot Team Definition")]
    public sealed class BotTeamDefinition : ScriptableObject
    {
        public int Round = 1;
        public List<BotSlot> Slots = new List<BotSlot>();
    }

    [Serializable]
    public struct BotSlot
    {
        public CreatureDefinition Creature;
        public int Level;
    }
}
