using UnityEngine;

namespace Pets.Data
{
    /// <summary>
    /// Tunable shop/run economy numbers, kept as data instead of scattered code constants.
    /// Placeholder balance per PLAN.md §10 — retune in the Inspector, no code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "Pets/Shop Config")]
    public sealed class ShopConfig : ScriptableObject
    {
        public int StartingGold = 10;
        public int GoldPerRound = 10;
        public int ShopSize = 3;
        public int RerollCost = 1;
        public int BoardMaxSize = 5;
        public int StartingLives = 3;
        public int TierUnlockEveryNRounds = 3;
        public int MaxLevel = 3;
        public int BaseBuyCost = 2;
        public int BuyCostPerTier = 1;
        public int SellRefundDivisor = 2;

        public int BuyCost(int tier)
        {
            return BaseBuyCost + tier * BuyCostPerTier;
        }

        public int SellRefund(int tier)
        {
            return BuyCost(tier) / SellRefundDivisor;
        }

        public int TierForRound(int round)
        {
            return 1 + (round - 1) / TierUnlockEveryNRounds;
        }
    }
}
