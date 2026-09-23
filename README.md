# STRANDS — TCG

Have you ever wondered how TCGs are actually made? How cards are balanced? Have you ever seen a completely broken card get released and thought, “How did they let this happen?”

I’ve definitely asked myself those questions. And for a while now, I’ve been trying to build a TCG of my own — obviously, like most things I do, it started as a fun little project =P — and I’ve been scratching my head with every new interaction, mechanic, and idea I come up with.

Strands is my tribute to a game that means a lot to me. A game about bonds, connections, loss, the people we love, and, above all, what it means to be human.

The idea is to take all of that and turn it into a TCG: experimenting with mechanics, figuring out balance, designing cards, and slowly learning what it actually takes to build a card game from the ground up.

<img width="1135" height="625" alt="image" src="https://github.com/user-attachments/assets/3c6a6e39-123d-41b0-8c20-5f49627c1358" />


A local prototype built with **C# + Unity 6.3 LTS**, prepared for Web export.
No external libraries, official assets, game accounts or backend are used.

The playable catalog contains **204 cards** with that cost curve:
90 Porters, 35 Entities, 24 Structures, 35 Events and 20 Equipment cards.
The [complete spreadsheet](Docs/Balance/strands-204-cards.csv) helps review the ideas.

**This is a nonprofit initiative**

## Open and play

1. In Unity Hub, add `C:\strands` as an existing project.
2. Open it with Unity 6.3 LTS and wait for compilation and artwork import.
3. Open `Assets/Scenes/Playground.unity`. If needed, use
   **Strands > Prepare playground**.
4. Press **Play** to open the main menu. The Game view works best at 16:9,
   with 1600 × 900 resolution.

## Main menu

- **Play Solo vs Bot** starts a match against **The Wayfarer**, using your
  selected deck. A starter deck is created on first launch. You play first;
  the bot acts one step at a time, with the same costs and delivery rules.
- **Check your Decks** opens the deck builder. Create, rename, duplicate,
  delete and edit decks; search/filter the catalog, inspect cards and add
  or remove copies. **Save Deck** saves a draft; **Use This Deck** saves and
  selects a complete deck. Playable decks require **60 cards**, with a maximum
  of **4 copies per definition**. These are the initial deck-building limits.
- **Rules** displays the current rules from [Rules.txt](Assets/Resources/Rules.txt).

Decks use local application storage (Unity PlayerPrefs; browser storage in
Web builds), with a previous-save backup. They are not synced to an account.
Unsaved edits prompt before navigation or deck switching; deletion requires
confirmation. Existing unreadable storage is preserved and an error is shown.

**Return to Menu** or Escape pauses the match. **Resume Match** continues it;
starting another match asks before replacing it. Matches themselves are not
saved when the application closes. Up/Down and Enter navigate the main menu.

The menu uses original generated landscape artwork and live lettering with
wide spacing and animated trailing strands inspired by the supplied references.
**Motion: reduced** keeps the decorative strands still.

## Turn sequence

<img width="1171" height="656" alt="image" src="https://github.com/user-attachments/assets/3721e842-f303-4e13-b3db-f0d8cac7adbf" />

### 1. Preparing the voyage

- The turn begins with spent Chiralium restored and cards untapped.
- On the first turn, you have 0 Chiralium and seven cards in hand.
- Click a card in your hand and choose **Convert to Chiralium + draw 1**.
  The card moves to a permanent resource zone and provides 1 Chiralium,
  available immediately. You can convert only one card per turn.
- Alternatively, click **Skip conversion / Draw 2** next to the library.
- This choice is made once per turn. There is no unrestricted draw button.
  If the library runs out, only the remaining cards are drawn.
- Events can be played before or after this choice by paying their cost.
- Click **Start Delivery** after resolving the choice.

### 2. Delivery

- Click a card in your hand and choose **Play onto field**. Its cost is
  deducted from available Chiralium. Insufficient resources block the action.
- Cards that entered this turn must wait until your next turn to activate.
- Click a ready card and choose **Attempt delivery**. Roll 1d6 plus your
  equipment bonus; reaching the difficulty earns delivery points.
  The card rotates 90 degrees and becomes exposed even if the attempt fails.
- A card can deliver only once before untapping. Equipment with 0 delivery
  points cannot make deliveries in this version.
- **End turn** passes to the other side. That side restores its resources and
  untaps its cards. The displayed turn number increases on every change of side.

### 3. Combat

- Combat is planned for a later stage. The attackable-target rule already
checks whether a card is tapped. Lethal damage, including event damage,
immediately sends a card to the **Beach**. There is no score-based victory
condition yet.
- Combat will feature interactions with the Beach, where Porters will face effects
and encounters emerging from the other side, adding an extra layer of tension 
and strategy to each round through thematic and intuitive mechanics.
- Unlike traditional TCGs, where the "graveyard" is often treated as a separate or
passive zone, in **Strands**, the Beach is an active part of the battlefield.
It will constantly influence the state of the game, creating new interactions,
opportunities, and meaningful decisions for the player throughout each match.


## Difficulty and equipment

Difficulty starts at **3**. The rule is `1d6 + bonus >= difficulty`.
Without equipment or events, the chance is 4/6, approximately **66.7%**.
The interface shows the chance before an attempt and the roll result afterward.

Events change the difficulty on both fields until the next side starts preparation.
Modifiers stack and the result is clamped to **2–6**. Some events represent
obstacles; others provide a more favorable route.

Equipment on the field immediately provides a passive bonus to your deliveries,
without attaching or tapping. The total is capped at **+3**. When equipment
leaves the field or dies, its bonus stops applying. Equipment cannot deliver.
Porters, Entities and Structures can attempt deliveries after their arrival turn.

## Example events

These values are starting proposals, editable in the Workshop or JSON.
Events can be played **only during preparation**, resolve for both fields
and go to the discard pile.

| Event | Cost | Implemented effect |
| --- | ---: | --- |
| Timefall | 1 | Increases difficulty by 1 this turn |
| Forest Fire | 2 | Increases difficulty by 1 this turn |
| BT Territory | 1 | Increases difficulty by 1 this turn |

Global events also affect cards that enter after resolution. Legacy damage,
delivery-penalty and attack-penalty behaviors remain available for card authoring,
but the 35 generic events use difficulty modifiers.

**Prototype decisions:** untap at the start of your next turn; new resources
are available immediately; events are exclusive to preparation; resolved
events are discarded; modifiers and damage last until the next turn.
These choices are isolated in `GameSession` for future adjustments.

## Zones and interaction

The arena follows a composition inspired by games such as MTG Arena: opponent
at the top, a central field divider, Beach on the left, library on the right
and a fan-shaped hand at the bottom. The scenery and portraits reuse Strands
artwork. Field cards show their artwork, name and attack/defense; click for details.
Animations include drawing and zone changes, smooth tapping/untapping, a glowing
local portrait and rain. **Motion: reduced** disables these movements.
The opponent now plays deliveries through a local bot. Combat is not implemented.

The hand uses a fan layout, hover enlargement and click-to-inspect details.
Zones have pages when needed. **View resources** shows converted cards;
**Discard** shows resolved events. Each player has a library, hand, field,
Beach, discard pile and Chiralium. Your selected deck supplies your 60 cards,
including chosen duplicates; seven are initially drawn. The bot selects 60
different definitions from the full catalog. The exact cost curve describes
the collection, not every individual deck or opening hand.

The Beach preserves cards. **Playground: send to Beach** and
**Playground: return to hand** remain available for manual testing; these
are not free card abilities. Replaying a returned card costs resources
and resets its one-turn activation delay.

## Write your own cards

In the Unity menu bar, open **Strands > Card Workshop**.

1. Click **New idea**.
2. Enter a name, description, effect text, type, attack, defense, delivery
   points and cost. Faction, rarity and artwork path are optional.
3. **Save draft** stores the idea in `Assets/CardWorkshop/drafts.json`
   without changing the deck. Closing the window also saves pending changes.
4. **Add / update in game** copies the card to `Assets/Resources/cards.json`.
   Restart Play to make it available for deck selection.
5. Use **Game cards > Edit copy** to adjust existing cards.

Effect text is freeform and is not interpreted as code. **Executable behavior**
selects a difficulty modifier, an equipment bonus or a legacy effect; new
behaviors require code. Keep the text consistent with the chosen behavior
and strength.

A freeform notebook is also available in [Docs/CARD-IDEAS.md](Docs/CARD-IDEAS.md).

### Edit the data directly

All 204 definitions live in `Assets/Resources/cards.json`. This is the main
file for your changes and is also editable through the Workshop.

```json
{
  "id": "porter-003",
  "name": "Shore Guardian",
  "type": 0,
  "cost": 2,
  "power": 2,
  "defense": 3,
  "deliveryPoints": 2,
  "description": "No delivery is left behind.",
  "text": "Tap to attempt a 2-point delivery: 1d6 + equipment must reach the difficulty.",
  "effect": 0,
  "effectAmount": 0,
  "faction": "Connection",
  "rarity": "Common",
  "image": ""
}
```

Types: 0 Porter, 1 Equipment, 2 Structure, 3 Event, 4 Entity.
Effects: 0 None, 1 GlobalDamage, 2 GlobalDeliveryPenalty, 3 GlobalAttackPenalty,
**4 IncreaseDeliveryDifficulty**, **5 DecreaseDeliveryDifficulty**,
**6 EquipmentDeliveryBonus**. `effectAmount` controls strength.
Effects 1–5 require Event; effect 6 requires Equipment.
Attributes must be nonnegative. Use unique IDs. `power` is attack;
`text` is the effect text; `description` is the narrative description.

Existing card IDs, JSON field names, resource paths and the internal C#
`Quiralium` identifiers are preserved for compatibility. The displayed
English resource name is **Chiralium**.

## Artwork

Ten original illustrations generated with the integrated **image_gen** tool
are stored in `Assets/Resources/Cards/`. No official assets were used.
These ten illustrations are reused across all 204 cards as placeholder artwork.
The set features rain, ruins, volcanic coastlines and technology with a dark
atmosphere inspired by Death Stranding. Card prompts: [Docs/art-prompts.json](Docs/art-prompts.json).
The menu landscape uses the built-in image_gen tool; its full prompt and asset
path are recorded in [Docs/menu-art-prompt.json](Docs/menu-art-prompt.json).

To replace artwork, put an image in `Assets/Resources/Cards` and enter
`Cards/filename` in the `image` field, without an extension. If no valid file
is found, the interface uses a procedural placeholder. The importer limits
in-game textures to 1024 pixels, preserving the original PNGs in the project.

## Architecture

```text
Assets/
  Editor/
    ProjectSetup.cs           Scene preparation, validation and Web build
    CardWorkshop.cs           Idea and catalog editor
    CardArtImporter.cs        Illustration import settings
  CardWorkshop/drafts.json    Ideas outside the playable catalog
  Resources/
    cards.json               Catalog definitions
    Cards/                   Original card artwork
    Menu/shore.png           Main menu landscape
    Rules.txt                Current rules shown in the menu
  Scripts/
    Data/CardCatalog.cs      Unity data loading
    Game/GameState.cs        Instances, zones, phases and resources
    Game/GameSession.cs      Actions and rules without UnityEngine
    Game/GameChecks.cs       Scenario checks
    Game/SoloBot.cs          Local opponent decision skill
    Game/DeckBook.cs         Deck CRUD and active selection
    Game/DeckRules.cs        Deck definitions and validation
    Data/DeckStorage.cs      Local persistence
    UI/GameBoard.cs          Match interface and animations
    UI/GameBoard.Menu.cs     Main menu, deck builder and rules
Docs/                        Idea notebook, balancing references and prompts
Tools/Test-Core.ps1           Compile and test rules without Unity
Tools/serve.mjs               Local server for the Web build
```

The interface uses Unity's built-in IMGUI. Each card copy has an InstanceId,
separate from its DefinitionId. The UI reads zones and requests actions from
GameSession. Phase, costs and readiness are validated in the core, not just
in buttons. There is no multiplayer, backend, equipment attachment or combat.
A local heuristic bot runs through the same GameSession actions as the player.
See [ARCHITECTURE.md](ARCHITECTURE.md) for details.

## Validation

In the Editor, use **Strands > Validate rules**. Without the Editor, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Test-Core.ps1
```

Checks cover preparation, a single draw choice, costs, regeneration, arrival
delay, deliveries, tapped cards, events on both fields, lethal damage, returns
from the Beach, an empty library and conservation of all 60 instances.
They also cover failed and successful rolls, equality with difficulty, exact
probabilities, difficulty/bonus limits, equipment removal and selection of
definitions beyond the first 60 in the catalog. Additional checks cover deck
CRUD, invalid deck rejection, exact custom-deck composition, alternating
sides, resource and score ownership, and 35 complete bot turns through
library exhaustion.

`node Tools/create-generic-catalog.mjs` validates the curve and recreates only
reference files in `Docs/Balance`. It **does not overwrite your playable
catalog**. The original ten-card seed is in `Tools/Seeds`.

The integration check `Strands.Editor.PlayModeSmoke.Run` can be run with
`-batchmode -executeMethod` in the Editor (without `-quit`, because the check
exits the process itself). It enters Play Mode, loads artwork, advances turns,
makes a delivery, checks deck persistence, runs a bot turn and captures the
menu, deck builder, rules, board and details in `TestResults`. Batch checks
use a separate PlayerPrefs key so normal saved decks are not touched.
It uses internal Game View APIs in the Editor and may need adjustments when
upgrading Unity. See [Docs/VALIDATION.md](Docs/VALIDATION.md) for validation
history and current limitations.

## Browser

Install **Web Build Support** for your Editor through Unity Hub.
In the Editor, choose **Strands > Build Web**, then run:

```powershell
cd C:\strands
node Tools/serve.mjs
```

Open **http://localhost:8080**. The server uses only Node; no npm install is
needed. Do not open the HTML by double-clicking. This is C# compiled by Unity
for the Web. Changes to the catalog or artwork require a new build.
