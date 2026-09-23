using System.Collections.Generic;

namespace Strands.Game
{
    // A local decision skill. It sees its own hand and the public field only.
    // Each step requests one ordinary GameSession action, never edits the state.
    public sealed class SoloBot
    {
        public string Step(GameSession game)
        {
            if (!game.IsBotTurn) return null;
            var player = game.State.Opponent;
            if (game.State.Phase == TurnPhase.PreparingTheVoyage)
            {
                if (!game.State.PreparationResolved)
                {
                    CardInstance resource = null;
                    float lowest = float.MaxValue;
                    int resourceGoal = 6;
                    foreach (var card in player.Cards(Zone.Hand))
                    {
                        var d = game.Definition(card);
                        if (d.type != CardType.Event) resourceGoal = System.Math.Max(resourceGoal, System.Math.Min(9, d.cost));
                        float value = Value(d, game) - (d.cost > player.TotalQuiralium + 2 ? 5 : 0);
                        if (value < lowest) { lowest = value; resource = card; }
                    }
                    if (resource != null && player.TotalQuiralium < resourceGoal)
                        return Result(game.ConvertToQuiralium(resource.InstanceId), game, "Bot converted " + game.Definition(resource).name + " and drew a card.");
                    return Result(game.SkipConversion(), game, "Bot skipped conversion and drew up to two cards.");
                }
                foreach (var card in player.Cards(Zone.Hand))
                {
                    var d = game.Definition(card);
                    if (d.type == CardType.Event && d.effect == CardEffect.DecreaseDeliveryDifficulty
                        && game.State.DeliveryDifficulty > 2 && HasReadyDelivery(game)
                        && game.PlayBlockReason(card.InstanceId) == null)
                        return Result(game.PlayCard(card.InstanceId), game, "Bot played " + d.name + ".");
                }
                return Result(game.StartDelivery(), game, "Bot started Delivery.");
            }

            CardInstance best = null;
            float bestValue = float.MinValue;
            foreach (var card in player.Cards(Zone.Hand))
            {
                if (game.PlayBlockReason(card.InstanceId) != null) continue;
                var d = game.Definition(card);
                if (d.type == CardType.Equipment && game.EquipmentBonus(player) >= 3) continue;
                float value = Value(d, game) / (1 + d.cost);
                if (value > bestValue) { best = card; bestValue = value; }
            }
            if (best != null)
                return Result(game.PlayCard(best.InstanceId), game, "Bot played " + game.Definition(best).name + ".");

            foreach (var card in player.Cards(Zone.Field))
            {
                if (game.ActivationBlockReason(card.InstanceId) != null) continue;
                if (!game.ActivateCard(card.InstanceId)) return game.LastError;
                var result = game.State.LastDelivery;
                return "Bot: " + result.CardName + " rolled " + result.Roll + " + " + result.Bonus
                    + " vs " + result.Difficulty + (result.Success ? ". Delivered +" + result.Points + " points." : ". Delivery failed.");
            }
            return Result(game.EndTurn(), game, "Your turn. Choose a resource conversion or draw two.");
        }

        private static bool HasReadyDelivery(GameSession game)
        {
            foreach (var card in game.State.Opponent.Cards(Zone.Field))
                if (!card.IsTapped && card.EnteredOnTurn < game.State.Turn && game.Definition(card).deliveryPoints > 0) return true;
            return false;
        }

        private static float Value(CardDefinition card, GameSession game)
        {
            if (card.type == CardType.Event) return card.effect == CardEffect.DecreaseDeliveryDifficulty ? 2 : -3;
            if (card.type == CardType.Equipment)
                return game.EquipmentBonus(game.State.Opponent) >= 3 ? 0 : 7 + card.effectAmount;
            return card.deliveryPoints * 3 + card.defense * .25f;
        }

        private static string Result(bool success, GameSession game, string message)
        { return success ? message : "Bot action blocked: " + game.LastError; }
    }
}
