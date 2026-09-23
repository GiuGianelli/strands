// Generates reference files only. Live edits in Assets/Resources/cards.json are never overwritten.
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
const root = fileURLToPath(new URL('../', import.meta.url));
const read = file => JSON.parse(fs.readFileSync(root + file, 'utf8').replace(/^\uFEFF/, ''));
const curve = read('Docs/Balance/cost-distribution.json').curve;
const cards = read('Tools/Seeds/initial-cards.json').cards;
const regions = ['of the Black Coast', 'of the Silent Valley', 'of the Broken Ridge', 'of the Gray Horizon', 'of the Last Station', 'of the Distant Shore'];
const roles = ['Courier', 'Scout', 'Guide', 'Lookout', 'Explorer', 'Tracker', 'Hauler', 'Pioneer', 'Wanderer', 'Rescuer', 'Navigator', 'Guardian', 'Surveyor', 'Driver', 'Veteran'];
const entities = ['Echo', 'Shadow', 'Shade', 'Memory', 'Specter', 'Presence', 'Vestige'];
const structures = ['Shelter', 'Bridge', 'Terminal', 'Beacon', 'Depot', 'Outpost', 'Station', 'Tower'];
const equipmentNames = ['Crossing Boots', 'Safety Rope', 'Route Map', 'Rain Filter', 'Insulated Cape', 'Walking Staff', 'Reinforced Backpack', 'Cargo Stabilizer', 'Folding Ladder', 'Long-Range Radio', 'Magnetic Glove', 'Protective Suit', 'Portable Anchor', 'Backup Battery', 'Crossing Radar', 'Light Exoskeleton', 'Navigation Kit', 'Rescue Winch', 'Mist Shield', 'Portable Bridge'];
const eventNames = ['Dense Fog', 'Crosswinds', 'Clear Passage', 'Flooded Route', 'Lost Signal', 'Unstable Ground', 'Clear Skies', 'High Tide', 'Unexpected Detour', 'Electrical Storm', 'Moment of Calm', 'Broken Bridges', 'Ash in the Air', 'Chiral Silence', 'Marked Trail', 'Strong Current', 'Rockslide', 'Obscured Horizon', 'Restored Path', 'Network Failure', 'Shower of Fragments', 'Frozen Crossing', 'Safe Corridor', 'Void on the Road', 'Echo of the Storm', 'Fallen Antennas', 'Beacons Alight', 'Signal Lost at Night', 'Airborne Sand', 'Mountain Rift', 'Regional Reconnection', 'Route Collapse'];
const countType = type => cards.filter(c => c.type === type).length;
let serial = 1, genericUnit = 0, equipmentIndex = 0, eventIndex = 0;
function create(cost, type) {
  let name;
  if (type === 3) name = eventNames[eventIndex++];
  else if (type === 1) name = equipmentNames[equipmentIndex++];
  else if (type === 2) { const i = countType(2) - 1; name = structures[i % 8] + ' ' + regions[Math.floor(i / 8)]; }
  else if (type === 4) { const i = countType(4) - 2; name = entities[i % 7] + ' ' + regions[Math.floor(i / 7)]; }
  else { const i = countType(0) - 2; name = roles[i % 15] + ' ' + regions[Math.floor(i / 15)]; }
  assert.ok(name && !name.includes('undefined'));
  const image = type === 0 ? 'Cards/porter-002' : type === 1 ? 'Cards/equipment-001' : type === 2 ? 'Cards/structure-001' : type === 4 ? 'Cards/entity-002' : 'Cards/event-001';
  return { id: 'generic-' + String(serial++).padStart(3, '0'), name, type, cost,
    power: 0, defense: 0, deliveryPoints: 0, faction: type === 4 || type === 3 ? 'Threshold' : 'Connection',
    rarity: cost >= 7 ? 'Legendary' : cost >= 4 ? 'Rare' : 'Common', image,
    description: type === 3 ? 'Travel conditions change for every courier.' : type === 1 ? 'A simple tool for crossing difficult terrain.' : type === 2 ? 'A connection still stands among the ruins.' : type === 4 ? 'Not every delivery begins on the same side of the Beach.' : 'One more step toward connecting people beyond the horizon.',
    text: '', effect: 0, effectAmount: 0 };
}
// Retain the reference's counts of events and equipment at each printed cost.
// Its 149 character slots become 90 Porters, 35 Entities and 24 Structures.
for (const row of curve) {
  for (const [type, target] of [[3, row.actions], [1, row.items]])
    while (cards.filter(c => c.cost === row.cost && c.type === type).length < target)
      cards.push(create(row.cost, type));
  while (cards.filter(c => c.cost === row.cost).length < row.count) {
    const pattern = [0, 0, 4, 0, 2, 0];
    let type = pattern[genericUnit++ % pattern.length];
    const quota = { 0: 90, 2: 24, 4: 35 };
    if (countType(type) >= quota[type]) type = [0, 4, 2].find(t => countType(t) < quota[t]);
    cards.push(create(row.cost, type));
  }
}

const favorableEvents = new Set(['Clear Passage', 'Clear Skies', 'Moment of Calm', 'Marked Trail', 'Restored Path', 'Safe Corridor', 'Beacons Alight', 'Regional Reconnection']);
for (const [index, card] of cards.entries()) {
  card.effect = 0; card.effectAmount = 0;
  if (card.type === 3) {
    const favorable = favorableEvents.has(card.name);
    card.effect = favorable ? 5 : 4;
    card.effectAmount = Math.min(3, 1 + Math.floor((card.cost - 1) / 2));
    card.power = 0; card.defense = 0; card.deliveryPoints = 0;
    card.text = 'Global difficulty ' + (favorable ? '-' : '+') + card.effectAmount + ' until the next turn (range: 2–6).';
  } else if (card.type === 1) {
    card.effect = 6; card.effectAmount = Math.min(3, Math.ceil(card.cost / 2));
    card.power = 0; card.defense = card.cost + 1; card.deliveryPoints = 0;
    card.text = 'On the field: +' + card.effectAmount + ' to your delivery rolls. Equipment bonuses stack up to +3.';
  } else {
    card.power = card.type === 2 ? 0 : Math.max(1, card.cost + (index % 3) - 1);
    card.defense = card.cost + (card.type === 2 ? 3 : 1 + index % 2);
    card.deliveryPoints = Math.min(5, 1 + Math.floor((card.cost - 1) / 2));
    card.text = 'From your next turn, tap: 1d6 + equipment ≥ route grants ' + card.deliveryPoints + ' point(s).';
  }
}
cards.sort((a, b) => a.cost - b.cost || a.type - b.type || a.id.localeCompare(b.id));
assert.equal(cards.length, 204);
assert.equal(new Set(cards.map(c => c.id)).size, 204);
assert.equal(new Set(cards.map(c => c.name)).size, 204);
for (const row of curve) assert.equal(cards.filter(c => c.cost === row.cost).length, row.count);
for (const [type, count] of [[0, 90], [1, 20], [2, 24], [3, 35], [4, 35]]) assert.equal(countType(type), count);
const document = { cards };
fs.writeFileSync(root + 'Docs/Balance/generated-generic-catalog.json', JSON.stringify(document, null, 2) + '\n');
const headers = ['Id', 'Name', 'Type', 'Chiralium', 'Attack', 'Defense', 'Delivery', 'Description', 'Effect', 'Behavior', 'Strength'];
const types = ['Porter', 'Equipment', 'Structure', 'Event', 'Entity'];
const rows = cards.map(c => [c.id, c.name, types[c.type], c.cost, c.power, c.defense, c.deliveryPoints, c.description, c.text, c.effect, c.effectAmount]);
const quote = value => '"' + String(value).replaceAll('"', '""') + '"';
fs.writeFileSync(root + 'Docs/Balance/strands-204-cards.csv', '\uFEFF' + [headers, ...rows].map(r => r.map(quote).join(';')).join('\r\n'));
console.log('PASS: 204 unique cards; exact cost curve; 90 Porters, 35 Entities, 24 Structures, 35 Events and 20 Equipment.');
console.log('Reference generated. Live catalog was not overwritten.');
