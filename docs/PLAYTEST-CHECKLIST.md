# Playtest Checklist

Quick manual pass to run through after any UI-affecting change, per CLAUDE.md's rule to actually
press Play and click through the affected flow before calling UI work done. Not a substitute for
automated tests — just cheap insurance against "I assumed it still worked." See
docs/testing-harness-plan.md §6. Matches the current Phase 0 Forest hub
(`Assets/Editor/ForestSceneBuilder.cs`) — update this list alongside any change to the hub's
tabs/overlays.

- [ ] **Scene load** — the scene bootstraps a run with a 2-mon starting line-up and lands on the
      Map tab.
- [ ] **Map tab** — the node sequence renders in order with the right cleared/current/upcoming
      markers; the Go button is only interactable on the current, uncleared node.
- [ ] **PvE node** — clicking Go starts a fight, the Step log animates, and a win/loss/draw result
      shows clearly.
- [ ] **Catching** — after a PvE win, a catch button appears for each defeated wild mon and adding
      one to the Box works.
- [ ] **PvE loss** — Morale visibly decrements and the same node is offered again (not advanced).
- [ ] **Camp node** — resolves EXP/buff and returns to the Map with the node cleared.
- [ ] **Team tab** — shows the current Lead/Support line-up correctly.
- [ ] **Shop / Center tabs** — currently static placeholder text (Phase 0) — confirm they still
      show as placeholders, not broken/blank.
- [ ] **Run over** — running Morale to 0 shows the Run Over overlay, not a stuck screen.
