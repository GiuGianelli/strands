# Cost reference — The First Chapter → Strands

This count uses the **204 regular cards** from Disney Lorcana: The First
Chapter, numbered 1/204 through 204/204. Each definition is counted once;
Enchanted cards, promos and alternate finishes are excluded.

Data source: [LorcanaJSON — set 1](https://lorcanajson.org/files/current/en/sets/setdata.1.json), snapshot generated on
2026-09-04T17:01:12. Type totals were cross-checked against
[Mushu Report — The First Chapter List](https://wiki.mushureport.com/wiki/The_First_Chapter_List).

The count uses the **printed ink cost**, without substituting alternate Shift
costs, singing costs or discounts. The corresponding Strands reference uses
the same number of definitions at each Chiralium cost.

| Ink / Chiralium | Collection cards | % | Characters | Actions¹ | Items | Inkable | In 60² |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 0 | 0.00% | 0 | 0 | 0 | 0 | 0 |
| 1 | 30 | 14.71% | 15 | 11 | 4 | 26 | 9 |
| 2 | 37 | 18.14% | 21 | 8 | 8 | 31 | 11 |
| 3 | 38 | 18.63% | 25 | 8 | 5 | 27 | 11 |
| 4 | 31 | 15.20% | 26 | 2 | 3 | 24 | 9 |
| 5 | 29 | 14.22% | 24 | 5 | 0 | 19 | 9 |
| 6 | 19 | 9.31% | 19 | 0 | 0 | 14 | 6 |
| 7 | 11 | 5.39% | 10 | 1 | 0 | 5 | 3 |
| 8 | 8 | 3.92% | 8 | 0 | 0 | 4 | 2 |
| 9 | 1 | 0.49% | 1 | 0 | 0 | 1 | 0 |
| **Total** | **204** | **100%** | **149** | **35** | **20** | **151** | **60** |

¹ Songs are included in actions. This set has no Locations.
² A proportional mathematical reduction using the largest-remainder method.
This is not an official deck, competitive list or deck-building recommendation.
Cost 9 receives zero slots due to rounding; this does not mean cost 9 is
prohibited or necessarily poor in a deck.

## Balancing notes

- Mean cost: **3.68**; median: **3**.
- Costs 1–3: **105 cards / 51.47%**.
- Costs 1–4: **136 cards / 66.67%**.
- **151 inkable cards** and **53 non-inkable cards**.
- Each of the six colors has 34 cards; the table combines all six.

This is the curve of a **collection of 204 definitions**, not a 204-card deck.
A 60-card deck selects and repeats definitions according to its strategy.
Costs alone do not establish balance: also compare delivery points, attack,
defense, effects, card draw, resource restrictions and match performance.

In the current Strands rules, any card can become Chiralium; skipping
conversion lets you draw two cards. Copying this distribution provides a
starting hypothesis for testing, but does not automatically reproduce
Lorcana's pacing or balance. Higher costs need testing under our own rules.

## Files

- [All 204 cards](lorcana-first-chapter-204-cards.csv): full name, cost, type,
  color, rarity and inkability. UTF-8 CSV with semicolon delimiters.
- [Strands planning sheet](strands-planning-204-cards.csv): 204 slots with
  costs already distributed. Fill in name, type, description, effect,
  attack, defense and delivery. These are planning slots, not playable cards.
- [JSON summary](cost-distribution.json): counts and source metadata.
- [Source snapshot](lorcana-set1-source.json): the public download used for the count.

This analysis generates planning slots and does not change the playable
catalog. The generic Strands implementation is in Assets/Resources/cards.json;
see also [the 204 implemented ideas](strands-204-cards.csv).
Use the Unity Card Workshop to edit and publish your own cards.

## Reproduce the count

Run from the project root:

```powershell
node Tools/analyze-lorcana.mjs
```

The script validates 204 distinct consecutive numbers, unique names, integer
costs, type totals and a total of 60 in the approximation. It also checks
that the 204 Strands slots match the source distribution. The filter uses
the full identifier: filtering by card number alone would include promos.
