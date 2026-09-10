# Battle Simulation Spec

Status: **not yet written** — this is a Phase 0 placeholder.

This will be the source of truth for trigger ordering, tie-breaking, and stat formulas (see
PLAN.md §3 and CLAUDE.md). Per PLAN.md's Phase 1 plan, write this alongside the first ~5 creature
worked example, before generalizing the trigger/effect system — design it against real cases,
not guesses.

Sections to fill in during Phase 1:
- Turn/phase structure (shop phase vs. battle phase)
- Trigger types and their firing order (OnBattleStart, OnHurt, OnFaint, OnLevelUp, OnBuy, OnSell,
  OnTurnStart)
- Tie-breaking rules (simultaneous triggers, simultaneous faints, position-based ordering)
- Stat formulas (attack/health math, buff stacking, damage resolution)
- Determinism notes (RNG seeding, float vs. integer math — see PLAN.md §10 risk)
