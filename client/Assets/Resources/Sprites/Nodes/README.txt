Region Map node icons (PLAN.md Phase 1).

Drop each node type's icon PNG in this folder with the exact file name below (no other change
needed — RegionMapController.cs loads them by these names via Resources.Load, and falls back to a
flat color swatch for any type whose file is missing):

  battle.png            PvE      "Battle"           black-exclamation-mark-in-bushes icon
  encounter.png         Event    "Encounter"         gold question-mark-over-Pokeball icon
  mystery_trainer.png   PvP      "Mystery Trainer"   trainer-silhouette-with-Pokeball icon
  pokemon_center.png    Camp     "Pokémon Center"    red/white Pokeball-domed building icon
  gym.png               Gym      "Gym"                gold ornate crowned building icon

Any square-ish PNG with a transparent background works — RegionMapController displays it with
preserveAspect on, so it doesn't need to be exactly square. After adding files here, re-run
Pets > Build Region Map Scene (or just enter Play) to see them.
