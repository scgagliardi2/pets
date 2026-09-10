# Playtest Checklist

Quick manual pass to run through after any UI-affecting change, per CLAUDE.md's rule to actually
press Play and click through before calling UI work done. Not a substitute for automated tests —
just cheap insurance against "I assumed it still worked." See docs/testing-harness-plan.md §6.

- [ ] **Home screen** — New Run and Continue (with and without a save present) both work; Stats
      is reachable and shows past runs.
- [ ] **Shop phase** — Buy, sell, freeze, and reroll each visibly update gold/board/shop in the
      UI, not just internally.
- [ ] **Combine** — buying a 3rd copy of a board creature merges it up a level and the UI reflects
      the new level.
- [ ] **Board arrangement** — reordering board creatures actually changes fight order.
- [ ] **Battle** — pressing Fight animates and resolves a battle; the result (win/loss) is legible.
- [ ] **Run end** — both win and loss end states are reachable and show the right screen.
- [ ] **Save/continue** — closing and reopening mid-run (or Continue from Home) resumes the same
      state, not a fresh run.
- [ ] **Stats screen** — a just-completed run shows up with the right outcome/round reached.
