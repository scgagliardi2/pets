using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pets.Data
{
    /// <summary>Runtime-safe ordered registry of bot rounds. Populated by Editor/ContentSeeder.cs.</summary>
    [CreateAssetMenu(fileName = "BotRosterLibrary", menuName = "Pets/Bot Roster Library")]
    public sealed class BotRosterLibrary : ScriptableObject
    {
        public List<BotTeamDefinition> Rounds = new List<BotTeamDefinition>();

        public int RoundCount => Rounds.Count;

        public BotTeamDefinition GetByRound(int round)
        {
            return Rounds.FirstOrDefault(r => r.Round == round);
        }
    }
}
