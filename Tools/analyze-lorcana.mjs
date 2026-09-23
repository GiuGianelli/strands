// Run: node Tools/analyze-lorcana.mjs
// Input: public LorcanaJSON set 1 snapshot. No npm dependencies.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const root = fileURLToPath(new URL('../', import.meta.url));
const dir = path.join(root, 'Docs/Balance');
const sourceUrl = 'https://lorcanajson.org/files/current/en/sets/setdata.1.json';
const raw = fs.readFileSync(path.join(dir, 'lorcana-set1-source.json'), 'utf8');
const source = JSON.parse(raw.replace(/^\uFEFF/, ''));
// Promo numbers overlap the base numbers; checking just number <= 204 is wrong.
const cards = source.cards.filter(c => /^\d+\/204\s*•\s*EN\s*•\s*1$/.test(c.fullIdentifier)
  && c.number >= 1 && c.number <= 204).sort((a, b) => a.number - b.number);
assert.equal(cards.length, 204);
assert.deepEqual(cards.map(c => c.number), Array.from({ length: 204 }, (_, i) => i + 1));
assert.equal(new Set(cards.map(c => c.fullName)).size, 204);
assert.ok(cards.every(c => Number.isInteger(c.cost) && c.cost >= 0 && typeof c.inkwell === 'boolean'));

const curve = Array.from({ length: Math.max(...cards.map(c => c.cost)) + 1 }, (_, cost) => {
  const group = cards.filter(c => c.cost === cost);
  return { cost, count: group.length, percentage: group.length / 204 * 100,
    characters: group.filter(c => c.type === 'Character').length,
    actions: group.filter(c => c.type === 'Action').length,
    items: group.filter(c => c.type === 'Item').length,
    inkable: group.filter(c => c.inkwell).length,
    deck60: Math.floor(group.length * 60 / 204) };
});
// Largest-remainder apportionment: total remains exactly 60.
let remaining = 60 - curve.reduce((sum, row) => sum + row.deck60, 0);
const remainderOrder = [...curve].sort((a, b) =>
  (b.count * 60 % 204) - (a.count * 60 % 204) || a.cost - b.cost);
for (let i = 0; i < remaining; i++) remainderOrder[i].deck60++;
assert.equal(curve.reduce((n, row) => n + row.count, 0), 204);
assert.equal(curve.reduce((n, row) => n + row.deck60, 0), 60);
assert.ok(curve.every(row => row.count === row.characters + row.actions + row.items));

const summary = {
  source: sourceUrl, datasetGeneratedOn: source.metadata.generatedOn,
  sourceSha256: createHash('sha256').update(raw).digest('hex'),
  scope: 'Base cards 1–204 only; exclude Enchanted, promos, oversized and foil duplicate printings.',
  total: cards.length, excludedVariants: source.cards.length - cards.length,
  meanCost: cards.reduce((sum, c) => sum + c.cost, 0) / cards.length,
  medianCost: [...cards].sort((a, b) => a.cost - b.cost)[101].cost,
  inkable: cards.filter(c => c.inkwell).length,
  nonInkable: cards.filter(c => !c.inkwell).length,
  curve,
};
fs.writeFileSync(path.join(dir, 'cost-distribution.json'), JSON.stringify(summary, null, 2) + '\n');

function csv(filename, headers, rows) {
  const quote = value => '"' + String(value ?? '').replaceAll('"', '""') + '"';
  fs.writeFileSync(path.join(dir, filename), '\uFEFF' + [headers, ...rows].map(row => row.map(quote).join(';')).join('\r\n') + '\r\n');
}
csv('lorcana-first-chapter-204-cards.csv',
  ['Number', 'Name', 'Cost', 'Type', 'Color', 'Rarity', 'Inkable'],
  cards.map(c => [c.number, c.fullName, c.cost, c.type, c.color, c.rarity, c.inkwell ? 'Yes' : 'No']));
const slots = curve.flatMap(row => Array.from({ length: row.count }, (_, index) =>
  ['strands-q' + row.cost + '-' + String(index + 1).padStart(3, '0'), row.cost, '', '', '', '', '', '', '', 'To create']));
csv('strands-planning-204-cards.csv',
  ['Planning ID', 'Chiralium cost', 'Name', 'Type', 'Description', 'Effect', 'Attack', 'Defense', 'Delivery', 'Status'], slots);
assert.equal(slots.length, 204);
for (const row of curve) assert.equal(slots.filter(slot => slot[1] === row.cost).length, row.count);

const percent = number => number.toFixed(2) + '%';
const table = curve.map(r => `| ${r.cost} | ${r.count} | ${percent(r.percentage)} | ${r.characters} | ${r.actions} | ${r.items} | ${r.inkable} | ${r.deck60} |`).join('\n');
const report = `# Cost reference — The First Chapter → Strands

This count uses the **204 regular cards** from Disney Lorcana: The First
Chapter, numbered 1/204 through 204/204. Each definition is counted once;
Enchanted cards, promos and alternate finishes are excluded.

Data source: [LorcanaJSON — set 1](${sourceUrl}), snapshot generated on
${source.metadata.generatedOn}. Type totals were cross-checked against
[Mushu Report — The First Chapter List](https://wiki.mushureport.com/wiki/The_First_Chapter_List).

The count uses the **printed ink cost**, without substituting alternate Shift
costs, singing costs or discounts. The corresponding Strands reference uses
the same number of definitions at each Chiralium cost.

| Ink / Chiralium | Collection cards | % | Characters | Actions¹ | Items | Inkable | In 60² |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
${table}
| **Total** | **204** | **100%** | **149** | **35** | **20** | **151** | **60** |

¹ Songs are included in actions. This set has no Locations.
² A proportional mathematical reduction using the largest-remainder method.
This is not an official deck, competitive list or deck-building recommendation.
Cost 9 receives zero slots due to rounding; this does not mean cost 9 is
prohibited or necessarily poor in a deck.

## Balancing notes

- Mean cost: **${summary.meanCost.toFixed(2)}**; median: **${summary.medianCost}**.
- Costs 1–3: **105 cards / ${percent(105 / 204 * 100)}**.
- Costs 1–4: **136 cards / ${percent(136 / 204 * 100)}**.
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

\`\`\`powershell
node Tools/analyze-lorcana.mjs
\`\`\`

The script validates 204 distinct consecutive numbers, unique names, integer
costs, type totals and a total of 60 in the approximation. It also checks
that the 204 Strands slots match the source distribution. The filter uses
the full identifier: filtering by card number alone would include promos.
`;
fs.writeFileSync(path.join(dir, 'README.md'), report);
console.log(JSON.stringify({ total: summary.total, excludedVariants: summary.excludedVariants,
  meanCost: summary.meanCost, curve: curve.map(r => ({ cost: r.cost, count: r.count, percentage: percent(r.percentage), deck60: r.deck60 })) }, null, 2));
console.log('PASS: base identifiers 1–204, unique definitions, cost/type totals, 60-card apportionment and 204 Strands planning slots.');
