# Architecture — Strands

## Layers

- Data: a JSON catalog independent of the interface; CardCatalog adapts loading to Unity.
- Game: state, turn rules and movement, without UnityEngine dependencies.
- UI: the IMGUI board and transitions; requests actions and displays core validation messages.
- Editor: Card Workshop, scene setup, artwork import and builds.

## State

GameState contains Player, Opponent, Turn, ActivePlayer, Phase, the preparation
decision and temporary global modifiers. In bot matches, ending Delivery
alternates ActivePlayer between player and opponent. ActingPlayer routes
conversion, payment, play and delivery through the same validations. Legacy
sessions without againstBot keep the single-player loop for core fixtures.

PlayerState contains deck, hand, field, beach, discard and quiralium zones.
Total resources equal the number of cards in Zone.Quiralium; available
resources regenerate at the start of the turn. DeliveryScore accumulates
successful deliveries. The UI calls the resource Chiralium; existing C#
identifiers retain Quiralium for compatibility.

CardDefinition contains attributes, a description, freeform effect text and
an explicit executable behavior. CardInstance stores the copy ID, definition
ID, arrival turn, tapped state and damage. Each of the 60 copies remains in
exactly one zone. The Beach is a real zone that may receive future actions.

## Actions

- ConvertToQuiralium: one choice per preparation; moves hand → resource and draws 1.
- SkipConversion: resolves the choice without conversion and draws up to 2 available cards.
- StartDelivery: requires the preparation decision to be resolved.
- PlayCard: validates zone, phase and cost. Events resolve and enter the discard pile.
- ActivateCard: checks readiness, rolls 1d6, adds equipment and compares against
  difficulty. Taps on success or failure; only success adds points.
- EndTurn: alternates sides in bot mode, restores the active side's resources,
  untaps only its cards and clears damage and temporary modifiers.
- ApplyDamage: shared death path; lethal damage moves field → Beach.
- CanBeAttacked: only tapped field cards qualify, in preparation for future combat.
- SendToBeach / ReturnFromBeach: explicit playground tools.

The game UI has no unrestricted public draw action. Drawing is an internal
operation used by opening hands and the preparation decision.

## Initial events

The 204-definition catalog uses IncreaseDeliveryDifficulty and
DecreaseDeliveryDifficulty events. GameState applies the global modifier to
a base of 3, clamped to 2–6. EquipmentDeliveryBonus equipment supports only
its owner, capped at +3 and calculated from the current field. There is no
attachment. The passive bonus applies on entry; delivery attempts wait a turn.

LastDelivery records the roll, bonus, difficulty and points of the latest
attempt. The die generator can be injected in tests; gameplay uses
Random.Next(1, 7). SuccessfulDeliveryFaces enumerates all six faces to report
the exact chance.

Effects use an enum and strength without interpreting freeform text. Legacy
effects include global damage, a global delivery penalty and a global attack
penalty. They affect both fields. Modifiers stack and expire at the next
preparation; effective values cannot become negative. Nonlethal damage is
also cleared then.

## Authoring

The Workshop saves drafts outside Resources, in Assets/CardWorkshop.
Adding to the catalog publishes a definition for the next local game.
The catalog is not reloaded during a game, avoiding rule changes mid-turn.
Artwork uses replaceable Resources assets with a procedural fallback.

The 60-card deck is selected after shuffling the entire catalog, so all
204 definitions can appear. Catalogs smaller than 60 are repeated in the
pool before selection. The deck is then shuffled and seven cards are drawn.

## Validation

## Menu, decks and the bot

GameBoard.Menu is the menu/deck/rules portion of the GameBoard partial class.
Its screen state pauses bot updates outside a match. The menu's letters are
rendered individually with extra spacing and fading vertical strands; reduced
motion freezes the strand lengths. The background loads from Resources/Menu.
Rules are loaded from Resources/Rules.txt, which is the in-game rules source.

DeckBook owns create, copy, rename/save, delete and active-deck selection.
DeckRules validates 60 cards, a maximum of four copies and known definition IDs.
The UI edits an independent draft and prompts before losing unsaved changes.
DeckStorage serializes DeckBook using JsonUtility and PlayerPrefs, with a previous
save backup. Invalid storage is preserved rather than overwritten. Batch-mode
checks use a separate storage key. Decks persist; active matches do not.

GameSession optionally accepts an exact playerDeck. It validates the list,
creates one distinct instance per entry, shuffles and draws seven. The bot's
deck uses full-catalog random selection. The session retains no mutable alias
to the saved deck, so editing a deck cannot alter a match already in progress.

SoloBot is a local heuristic decision skill, not an external LLM service. Each
Step requests one ordinary session action. It uses only its own hand and public
field state, develops resources, prioritizes useful equipment and deliveries,
plays helpful difficulty reductions, activates ready units and ends its turn.
The UI paces steps and locks human match actions while the bot is active.
No combat or victory threshold has been introduced; the mode is open-ended
delivery practice with separate scores.

## Validation

GameChecks runs scenarios against the real C# code through Unity or PowerShell:
phases, resources, draws, delivery, effects on both sides, death, returns and
invariants. Card IDs, enum values, numeric attributes and artwork paths remain
stable across the English translation.
