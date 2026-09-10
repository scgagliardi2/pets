namespace Pets.Simulation
{
    /// <summary>
    /// Battle effects (DealDamage/Heal/BuffAttack/BuffHealth/Summon) are applied by
    /// BattleSimulator. GainGold is shop-only, applied by Gameplay/ShopEconomy.cs against
    /// RunState.Gold — BattleSimulator never sees it fire, since only OnBattleStart/OnHurt/OnFaint
    /// abilities are ever selected during a battle (see content-schema.md's shop-phase trigger
    /// section).
    /// </summary>
    public enum EffectType
    {
        DealDamage,
        Heal,
        BuffAttack,
        BuffHealth,
        Summon,
        GainGold
    }
}
