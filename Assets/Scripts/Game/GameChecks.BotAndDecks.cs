using System;
using System.Collections.Generic;

namespace Strands.Game
{
    public static partial class GameChecks
    {
        private static void DeckEditingAndSelection(CardDefinition[] definitions)
        {
            var catalog = new Dictionary<string, CardDefinition>();
            foreach (var d in definitions) catalog.Add(d.id, d);
            var book = new DeckBook(); var deck = book.Create("Test crossing");
            Require(book.Select(deck.id, catalog) != null && book.activeDeckId == null, "Cannot select empty draft");
            for (int i = 0; i < 60; i++) deck.cards.Add(definitions[i].id);
            Require(book.Select(deck.id, catalog) == null && book.activeDeckId == deck.id, "Select valid deck");
            var draft = deck.Copy(); draft.name = "Renamed crossing"; book.Save(draft);
            draft.cards.Clear();
            Require(book.Find(deck.id).cards.Count == 60 && book.Find(deck.id).name == "Renamed crossing", "Save copies the draft and persists its name");
            var duplicate = book.Duplicate(deck.id); duplicate.cards.RemoveAt(0);
            Require(book.Find(deck.id).cards.Count == 60 && duplicate.id != deck.id, "Duplicate is independent");
            Require(book.Select(duplicate.id, catalog) != null && book.activeDeckId == deck.id, "Invalid selection preserves active deck");
            var invalid = new List<string>(deck.cards); invalid[0] = "not-in-catalog";
            Require(DeckRules.Validate(invalid, catalog) != null, "Unknown card rejected");
            invalid = new List<string>(deck.cards);
            for (int i = 0; i < 5; i++) invalid[i] = definitions[0].id;
            Require(DeckRules.Validate(invalid, catalog) != null, "Five copies rejected");
            invalid[4] = definitions[4].id;
            Require(DeckRules.Validate(invalid, catalog) == null, "Four copies accepted");
            bool rejected = false;
            try { new GameSession(definitions, 4, playerDeck: duplicate.cards, againstBot: true); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Session rejects invalid selected deck");
            var game = new GameSession(definitions, 4, playerDeck: invalid, againstBot: true);
            var actual = new Dictionary<string, int>();
            foreach (Zone zone in Enum.GetValues(typeof(Zone))) foreach (var card in game.State.Player.Cards(zone))
            { int count; actual.TryGetValue(card.DefinitionId, out count); actual[card.DefinitionId] = count + 1; }
            Require(actual[definitions[0].id] == 4 && actual.Count == 57, "Selected deck copies survive shuffling and opening draw");
            CheckZones(game.State.Player); CheckZones(game.State.Opponent);
            Require(book.Delete(deck.id) && book.activeDeckId == null && book.Find(deck.id) == null, "Delete active deck clears selection");
            Require(!book.Delete(deck.id), "Repeated delete is harmless");
        }

        private static void AlternatingSides()
        {
            var game = new GameSession(new[] { Unit("courier", 1, 3, 2) }, 7, () => 6, againstBot: true);
            var p = game.State.Player; var o = game.State.Opponent;
            var resource = p.Cards(Zone.Hand)[0];
            Require(game.ConvertToQuiralium(resource.InstanceId) && game.StartDelivery(), "Human preparation");
            var human = p.Cards(Zone.Hand)[0];
            Require(game.PlayCard(human.InstanceId) && !game.ActivateCard(human.InstanceId), "Human pays and waits");
            Require(game.EndTurn() && game.IsBotTurn && game.ActingPlayer == o, "Turn passes to bot");
            Require(game.PlayBlockReason(human.InstanceId) != null, "Bot cannot play human cards");
            Require(game.ConvertToQuiralium(o.Cards(Zone.Hand)[0].InstanceId), "Bot converts its own card");
            Require(p.TotalQuiralium == 1 && p.AvailableQuiralium == 0 && o.AvailableQuiralium == 1, "Resources belong to each side");
            Require(game.StartDelivery(), "Bot starts delivery");
            var enemy = o.Cards(Zone.Hand)[0];
            Require(game.PlayCard(enemy.InstanceId) && !game.ActivateCard(enemy.InstanceId), "Bot pays and waits");
            Require(!game.ActivateCard(human.InstanceId), "Bot cannot activate human field cards");
            Require(game.EndTurn() && !game.IsBotTurn && p.AvailableQuiralium == 1, "Human resources regenerate");
            game.SkipConversion(); game.StartDelivery();
            Require(game.ActivateCard(human.InstanceId) && p.DeliveryScore == 2 && o.DeliveryScore == 0, "Human score isolated");
            game.EndTurn();
            Require(human.IsTapped && !enemy.IsTapped, "Only active side untaps");
            game.SkipConversion(); game.StartDelivery();
            Require(game.ActivateCard(enemy.InstanceId) && o.DeliveryScore == 2 && p.DeliveryScore == 2, "Bot score isolated");
            game.EndTurn(); Require(!human.IsTapped && enemy.IsTapped, "Tapping survives the opposing turn");
            CheckZones(p); CheckZones(o);
        }

        private static void BotSimulation(CardDefinition[] definitions)
        {
            var bot = new SoloBot();
            var game = new GameSession(definitions, 28, () => 6, againstBot: true);
            Require(bot.Step(game) == null && game.State.Turn == 1, "Bot cannot act during human turn");
            for (int turn = 0; turn < 35; turn++)
            {
                game.SkipConversion(); game.StartDelivery(); game.EndTurn();
                int steps = 0;
                while (game.IsBotTurn && steps++ < 150)
                {
                    string result = bot.Step(game);
                    Require(result != null && !result.StartsWith("Bot action blocked:"), "Every bot action is legal");
                    Require(game.State.Opponent.AvailableQuiralium >= 0, "Bot never overspends");
                    CheckZones(game.State.Player); CheckZones(game.State.Opponent);
                }
                Require(!game.IsBotTurn, "Bot finishes each turn in bounded steps");
            }
            Require(game.State.Opponent.DeliveryScore > 0 && game.State.Opponent.TotalQuiralium > 0, "Bot develops resources and delivers");
            Require(game.State.Opponent.Cards(Zone.Deck).Count == 0, "Bot survives an empty library");
        }
    }
}
