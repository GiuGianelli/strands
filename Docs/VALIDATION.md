# Validation

Environment: Windows, Unity 6.3 LTS (6000.3.0f1).

## English translation checks

- Core C# compilation and GameChecks through Windows PowerShell: passed.
- All runtime and Editor C# scripts compiled with Unity's bundled compiler
  and reference assemblies: passed. This is a standalone compilation check,
  not an Editor import or Play Mode run.
- All 204 card IDs, types, costs, attack, defense, delivery points, effects,
  effect strengths and artwork paths were compared with the pre-translation
  catalog and are unchanged.
- Both reference generators ran successfully. The translated live catalog
  matches the generated reference, including names and effect text.
- The cost analysis still validates 204 unique base cards, cost/type totals,
  a 60-card proportional approximation and 204 Strands planning slots.
- Runtime labels, validation messages, Workshop menus, card content, seed data,
  artwork prompt labels, spreadsheets and documentation are in English.
- Documentation paths were renamed to English and their references updated.

The previous arena smoke-test attempt could not start because the Unity
Editor reported no valid license (exit code 198; `TestResults/arena-smoke.log`).
Visual layout and Play Mode have therefore not been revalidated for the arena
or English translation. A Web build has not been generated or tested for
these changes.

## Earlier prototype validation

The project previously recorded successful validation of the earlier layout:

- Core and Editor compilation inside Unity, with no C# errors.
- GameChecks executed inside Unity.
- Catalog: 204 unique definitions, verified cost curve, populated fields and
  valid artwork paths. Events modify difficulty and equipment provides roll
  bonuses. Ten illustrations are shared across the definitions.
- Play Mode: GameBoard initialized and artwork for all 204 definitions loaded
  through Resources.
- Integration: conversion over four turns, starting Delivery, playing a card,
  rejecting an immediate delivery and allowing delivery on the next turn.
- Rendering: nonblank frames captured and inspected for preparation, a tapped
  card during Delivery and the details panel.
- Workshop: window opened and layout/repaint events completed without errors.
- Drafts: the ideas file remained empty and ready for user input.

These earlier results do not establish visual validation of the current UI.

## Rule coverage

Core checks cover both fields for global events, lethal and nonlethal damage,
invalid phases, insufficient Chiralium, duplicate draws, regeneration, returns
from the Beach, an empty library and conservation of all 60 instances.

They also verify delivery success/failure, all six die faces, equality with
difficulty, exact probabilities, difficulty bounds, the maximum +3 equipment
bonus, owner-only bonuses and bonus removal when equipment leaves or dies.
Deck selection reaches definitions beyond the first 60. An invalid die roll
does not tap the card or change the score.

Local logs and captures are in `TestResults/` (ignored by Git), including
`unity-validation.log`, `unity-playmode.log`, `table-preparation.bmp`,
`table-delivery.bmp` and `card-preview.bmp` from earlier checks.

The integration check invokes rules directly and renders the UI; it does not
replace a full manual check of clicks, hover and Workshop editing/saving.
The earlier validation also reported Web Build Support missing from that
Editor installation.
