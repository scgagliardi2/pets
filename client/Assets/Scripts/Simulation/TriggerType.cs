namespace Pets.Simulation
{
    /// <summary>
    /// Full ability trigger vocabulary (PLAN.md §2.3). Only OnBattleStart/OnHurt/OnFaint are
    /// fired by BattleSimulator today — see battle-sim-spec.md §3 for what's live.
    /// </summary>
    public enum TriggerType
    {
        OnBattleStart,
        OnHurt,
        OnFaint,
        OnLevelUp,
        OnBuy,
        OnSell,
        OnTurnStart
    }
}
