using UnityEngine;

namespace Pets.Gameplay
{
    /// <summary>
    /// UI-display snapshot of one creature's pre-battle stats. RunController.Fight() builds
    /// these from State.Board / the bot team's definitions before handing off to
    /// BattleSimulator.Run, since the simulator mutates its own CreatureState copies in place —
    /// this keeps the fight-lineup view showing what each side brought into the battle.
    /// </summary>
    public readonly struct FightCardInfo
    {
        public readonly string DisplayName;
        public readonly int Level;
        public readonly int Attack;
        public readonly int Health;
        public readonly Color Color;

        public FightCardInfo(string displayName, int level, int attack, int health, Color color)
        {
            DisplayName = displayName;
            Level = level;
            Attack = attack;
            Health = health;
            Color = color;
        }
    }
}
