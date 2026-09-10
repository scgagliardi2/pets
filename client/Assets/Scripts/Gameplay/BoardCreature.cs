using Pets.Data;

namespace Pets.Gameplay
{
    public sealed class BoardCreature
    {
        public CreatureDefinition Definition;
        public int Level = 1;

        /// <summary>Permanent stat gains from shop-trigger abilities (OnBuy/OnSell/OnLevelUp/
        /// OnTurnStart), folded into effective battle stats by TeamStateConverter.</summary>
        public int BonusAttack;
        public int BonusHealth;
    }
}
