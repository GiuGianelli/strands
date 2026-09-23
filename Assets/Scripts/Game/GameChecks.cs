using System;
using System.Collections.Generic;

namespace Strands.Game
{
    // Real rule scenarios, callable in Unity and in Windows PowerShell.
    public static partial class GameChecks
    {
        public static string Run(CardDefinition[] definitions)
        {
            var actual = new GameSession(definitions, 42);
            Require(actual.State.Player.Cards(Zone.Deck).Count == 53 && actual.State.Player.Cards(Zone.Hand).Count == 7, "Opening hand");
            Require(actual.State.Opponent.Cards(Zone.Deck).Count == 53, "Opponent deck");
            CheckZones(actual.State.Player); CheckZones(actual.State.Opponent);
            PreparationAndDelivery(); EventsAndDeaths(); DeliveryDifficultyAndEquipment(); EmptyDeckAndSeed(definitions);
            DeckEditingAndSelection(definitions); AlternatingSides(); BotSimulation(definitions);
            Require(GameSession.ValidateDefinition(new CardDefinition { id = "bad", name = "Invalid", cost = -1 }) != null, "Reject negative costs");
            return "PASS: turns, costs, zones, delivery odds, events, equipment, deck sampling, deck CRUD/validation, custom deck loading, alternating sides and bounded bot turns.";
        }

        private static void DeliveryDifficultyAndEquipment()
        {
            int roll = 1, rolls = 0;
            var equipment = new CardDefinition { id = "gear", name = "Gear", type = CardType.Equipment,
                defense = 2, effect = CardEffect.EquipmentDeliveryBonus, effectAmount = 2 };
            var storm = EventCard("storm", CardEffect.IncreaseDeliveryDifficulty, 2); storm.cost = 0;
            var severe = EventCard("severe", CardEffect.IncreaseDeliveryDifficulty, 99); severe.cost = 0;
            var calm = EventCard("calm", CardEffect.DecreaseDeliveryDifficulty, 200); calm.cost = 0;
            var game = new GameSession(new[] { Unit("unit", 0, 3, 2), equipment, storm, severe, calm }, 12, () => { rolls++; return roll; });
            var p = game.State.Player;
            var unit = Arrange(game, p, "unit", Zone.Field);
            Arrange(game, game.State.Opponent, "gear", Zone.Field);
            Require(game.EquipmentBonus(p) == 0 && game.SuccessfulDeliveryFaces(p) == 4, "Base 4/6 odds, enemy equipment does not help player");
            game.SkipConversion(); game.StartDelivery();
            Require(game.ActivateCard(unit.InstanceId) && unit.IsTapped && p.DeliveryScore == 0, "Failed delivery taps without points");
            Require(!game.State.LastDelivery.Success && game.State.LastDelivery.Roll == 1 && game.State.LastDelivery.Difficulty == 3, "Recorded failure");
            Require(!game.ActivateCard(unit.InstanceId) && rolls == 1, "Invalid retry does not roll");
            game.EndTurn();
            var stormCard = Arrange(game, p, "storm", Zone.Hand);
            Require(game.PlayCard(stormCard.InstanceId) && game.State.DeliveryDifficulty == 5, "Event raises route difficulty");
            Require(game.SuccessfulDeliveryFaces(p) == 2 && game.SuccessfulDeliveryFaces(game.State.Opponent) == 4, "Same difficulty for both, local equipment only");
            var gear = Arrange(game, p, "gear", Zone.Hand);
            game.SkipConversion(); game.StartDelivery();
            Require(game.PlayCard(gear.InstanceId) && game.EquipmentBonus(p) == 2 && game.SuccessfulDeliveryFaces(p) == 4, "Equipment passive applies on entry");
            roll = 3;
            Require(game.ActivateCard(unit.InstanceId) && game.State.LastDelivery.Success && p.DeliveryScore == 2, "Roll plus gear equals target succeeds");
            game.EndTurn();
            Require(game.State.DeliveryDifficulty == 3 && game.State.LastDelivery == null && game.EquipmentBonus(p) == 2, "Events expire; gear persists");
            Require(game.SuccessfulDeliveryFaces(p) == 6, "100 percent odds with enough support");
            Arrange(game, p, "gear", Zone.Field);
            Require(game.EquipmentBonus(p) == 3, "Equipment total capped at three");
            Require(game.SendToBeach(gear.InstanceId) && game.EquipmentBonus(p) == 2, "Leaving field removes bonus");
            var remainingGear = p.Mutable(Zone.Field).Find(c => c.DefinitionId == "gear");
            Require(game.ApplyDamage(p, remainingGear.InstanceId, 2) && game.EquipmentBonus(p) == 0, "Death removes passive bonus");
            var severeCard = Arrange(game, p, "severe", Zone.Hand);
            var calmCard = Arrange(game, p, "calm", Zone.Hand);
            Require(game.PlayCard(severeCard.InstanceId) && game.State.DeliveryDifficulty == 6, "Difficulty ceiling");
            Require(game.PlayCard(calmCard.InstanceId) && game.State.DeliveryDifficulty == 2, "Difficulty floor");
            game.SkipConversion(); game.StartDelivery();
            roll = 7;
            Require(!game.ActivateCard(unit.InstanceId) && !unit.IsTapped && p.DeliveryScore == 2, "Invalid injected die does not mutate turn");
            for (int face = 1; face <= 6; face++)
            {
                unit.IsTapped = false; roll = face;
                Require(game.ActivateCard(unit.InstanceId) && game.State.LastDelivery.Success == (face >= 2), "Exact d6 threshold for each face");
            }
            CheckZones(p); CheckZones(game.State.Opponent);
            Require(GameSession.ValidateDefinition(new CardDefinition { id = "bad", name = "Bad", type = CardType.Porter, effect = CardEffect.EquipmentDeliveryBonus }) != null, "Reject gear effect on non-equipment");
        }

        private static CardDefinition Unit(string id, int cost, int defense, int delivery)
        { return new CardDefinition { id = id, name = id, type = CardType.Porter, cost = cost, power = 3, defense = defense, deliveryPoints = delivery }; }
        private static CardDefinition EventCard(string id, CardEffect effect, int amount)
        { return new CardDefinition { id = id, name = id, type = CardType.Event, effect = effect, effectAmount = amount, cost = 1 }; }

        private static void PreparationAndDelivery()
        {
            var game = new GameSession(new[] { Unit("unit", 1, 3, 2) }, 42, () => 6);
            var p = game.State.Player;
            Require(p.TotalQuiralium == 0 && p.AvailableQuiralium == 0, "Turn one resources");
            string id = p.Cards(Zone.Hand)[0].InstanceId;
            Require(!game.StartDelivery() && !game.EndTurn(), "Cannot bypass preparation");
            Require(!game.PlayCard(id) && p.Cards(Zone.Hand).Count == 7, "No permanent in preparation");
            Require(!game.ConvertToQuiralium("missing") && !game.State.PreparationResolved, "Invalid conversion does not resolve choice");
            Require(game.ConvertToQuiralium(id), "Convert");
            Require(p.Cards(Zone.Quiralium)[0].InstanceId == id && p.Cards(Zone.Hand).Count == 7 && p.Cards(Zone.Deck).Count == 52, "Convert one, draw one, retain resource identity");
            Require(p.TotalQuiralium == 1 && p.AvailableQuiralium == 1, "New resource immediately available");
            Require(!game.ConvertToQuiralium(p.Cards(Zone.Hand)[0].InstanceId) && !game.SkipConversion(), "Only one decision per turn");
            Require(p.Cards(Zone.Deck).Count == 52, "No duplicate draw");
            Require(game.StartDelivery(), "Start delivery");
            string unit = p.Cards(Zone.Hand)[0].InstanceId;
            Require(game.PlayCard(unit) && p.AvailableQuiralium == 0, "Pay cost");
            Require(!game.PlayCard(unit), "Reject duplicate play");
            Require(!game.PlayCard(p.Cards(Zone.Hand)[0].InstanceId) && p.AvailableQuiralium == 0, "Cannot overspend");
            Require(!game.ActivateCard(unit) && p.DeliveryScore == 0, "Arrival delay");
            Require(!game.CanBeAttacked(p, unit), "Untapped cannot be attacked");
            Require(game.EndTurn() && game.State.Turn == 2 && p.AvailableQuiralium == 1, "Next turn refresh");
            Require(!game.ActivateCard(unit), "No delivery during preparation");
            int handBefore = p.Cards(Zone.Hand).Count;
            Require(game.SkipConversion() && p.Cards(Zone.Hand).Count == handBefore + 2 && p.TotalQuiralium == 1, "Skip draws two without resource");
            Require(!game.SkipConversion(), "No repeated skip");
            Require(game.StartDelivery() && game.ActivateCard(unit), "Ready next turn");
            Require(p.DeliveryScore == 2 && game.CanBeAttacked(p, unit), "Tap grants score and vulnerability");
            Require(!game.ActivateCard(unit) && p.DeliveryScore == 2, "Only one activation");
            Require(game.EndTurn() && !game.CanBeAttacked(p, unit), "Untap at next turn");
            Require(game.SendToBeach(unit) && game.ReturnFromBeach(unit), "Beach round trip");
            Require(!game.ReturnFromBeach(unit), "Cannot return twice");
            Require(game.SkipConversion() && game.StartDelivery() && game.PlayCard(unit), "Replay returned card");
            Require(!game.ActivateCard(unit), "Replay resets arrival delay");
            CheckZones(p);
        }

        private static void EventsAndDeaths()
        {
            var game = new GameSession(new[] {
                Unit("fragile", 0, 2, 2), Unit("strong", 0, 5, 3),
                EventCard("rain", CardEffect.GlobalDeliveryPenalty, 1),
                EventCard("fire", CardEffect.GlobalDamage, 2),
                EventCard("ep", CardEffect.GlobalAttackPenalty, 8)
            }, 17, () => 6);
            var p = game.State.Player; var opponent = game.State.Opponent;
            p.AvailableQuiralium = 8; // Arrange resources independently of conversion tests.
            var fragile = Arrange(game, p, "fragile", Zone.Field);
            var strong = Arrange(game, p, "strong", Zone.Field);
            var enemy = Arrange(game, opponent, "fragile", Zone.Field);
            var rain = Arrange(game, p, "rain", Zone.Hand);
            var fire = Arrange(game, p, "fire", Zone.Hand);
            var ep = Arrange(game, p, "ep", Zone.Hand);
            Require(game.PlayCard(rain.InstanceId), "Play global rain in preparation");
            Require(game.DeliveryValue(fragile) == 1 && game.DeliveryValue(enemy) == 1, "Rain affects both players");
            Require(p.Cards(Zone.Discard).Count == 1, "Resolved event is discarded");
            Require(game.PlayCard(ep.InstanceId) && game.AttackValue(strong) == 0 && game.AttackValue(enemy) == 0, "Global attack floor");
            Require(game.PlayCard(fire.InstanceId), "Global damage event");
            Require(p.Cards(Zone.Beach).Count == 1 && opponent.Cards(Zone.Beach).Count == 1, "Both players lethal cards reach Beach");
            Require(game.RemainingDefense(strong) == 3, "Nonlethal damage retained");
            Require(!game.ApplyDamage(p, "missing", 2) && !game.ApplyDamage(p, strong.InstanceId, -1), "Invalid damage rejected");
            var anotherEvent = Arrange(game, p, "rain", Zone.Hand);
            Require(game.SkipConversion() && game.StartDelivery(), "Advance after events");
            Require(!game.PlayCard(anotherEvent.InstanceId), "Event gated to preparation");
            Require(game.ActivateCard(strong.InstanceId) && p.DeliveryScore == 2, "Delivery uses global modifier");
            Require(game.EndTurn(), "Finish event turn");
            Require(game.DeliveryValue(strong) == 3 && game.AttackValue(strong) == 3 && game.RemainingDefense(strong) == 5 && game.State.ActiveEvents.Count == 0, "Temporary effects and damage reset");
            CheckZones(p); CheckZones(opponent);
        }

        private static CardInstance Arrange(GameSession game, PlayerState player, string definitionId, Zone target)
        {
            foreach (Zone zone in new[] { Zone.Deck, Zone.Hand })
            {
                var card = player.Mutable(zone).Find(c => c.DefinitionId == definitionId);
                if (card == null) continue;
                player.Mutable(zone).Remove(card); player.Mutable(target).Add(card); return card;
            }
            throw new InvalidOperationException("Fixture missing: " + definitionId);
        }

        private static void EmptyDeckAndSeed(CardDefinition[] definitions)
        {
            var game = new GameSession(definitions, 7);
            var same = new GameSession(definitions, 7);
            for (int i = 0; i < 53; i++) Require(game.State.Player.Cards(Zone.Deck)[i].InstanceId == same.State.Player.Cards(Zone.Deck)[i].InstanceId && game.State.Player.Cards(Zone.Deck)[i].DefinitionId == same.State.Player.Cards(Zone.Deck)[i].DefinitionId, "Deterministic seed");
            if (definitions.Length > 60)
            {
                var seen = new HashSet<string>();
                for (int seed = 0; seed < 40; seed++)
                {
                    var sample = new GameSession(definitions, seed);
                    foreach (Zone zone in new[] { Zone.Deck, Zone.Hand })
                        foreach (var card in sample.State.Player.Cards(zone)) seen.Add(card.DefinitionId);
                }
                Require(seen.Contains(definitions[definitions.Length - 1].id), "Last catalogue entry is eligible for a deck");
                Require(seen.Count > 60, "Deck selection is not restricted to first 60 definitions");
            }
            game.ShuffleDeck(); CheckZones(game.State.Player);
            for (int i = 0; i < 30; i++)
            { Require(game.SkipConversion() && game.StartDelivery() && game.EndTurn(), "Turn works with exhausted deck"); }
            Require(game.State.Player.Cards(Zone.Hand).Count == 60 && game.State.Player.Cards(Zone.Deck).Count == 0, "Odd deck remainder draws only available cards");
            Require(game.ConvertToQuiralium(game.State.Player.Cards(Zone.Hand)[0].InstanceId), "Convert with no card to draw");
            Require(game.State.Player.Cards(Zone.Hand).Count == 59, "No fabricated draw");
            game.ShuffleDeck(); CheckZones(game.State.Player);
        }

        private static void CheckZones(PlayerState player)
        {
            var ids = new HashSet<string>(); int total = 0;
            foreach (Zone zone in Enum.GetValues(typeof(Zone))) foreach (var card in player.Cards(zone))
            { total++; Require(ids.Add(card.InstanceId), "Unique instance across zones"); }
            Require(total == 60, "Conservation of 60 cards");
        }
        private static void Require(bool condition, string name)
        { if (!condition) throw new InvalidOperationException("FAIL: " + name); }
    }
}
