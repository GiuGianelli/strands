using System;
using System.Collections.Generic;

namespace Strands.Game
{
    // Pure C#: the UI requests actions; phase, cost and readiness checks live here.
    public sealed class GameSession
    {
        public GameState State { get; private set; }
        public bool AgainstBot { get; private set; }
        public bool IsBotTurn { get { return AgainstBot && State.ActivePlayer == "opponent"; } }
        public PlayerState ActingPlayer { get { return IsBotTurn ? State.Opponent : State.Player; } }
        public string LastError { get; private set; }
        public event Action<Zone, Zone, CardInstance> CardMoved;
        private readonly Random random;
        private readonly Func<int> rollDelivery;
        private readonly Dictionary<string, CardDefinition> catalog = new Dictionary<string, CardDefinition>();
        public CardDefinition Definition(CardInstance card) { return catalog[card.DefinitionId]; }

        public GameSession(IEnumerable<CardDefinition> definitions, int? seed = null, Func<int> deliveryRoll = null, IEnumerable<string> playerDeck = null, bool againstBot = false)
        {
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            rollDelivery = deliveryRoll ?? (() => random.Next(1, 7));
            var ids = new List<string>();
            foreach (var definition in definitions)
            {
                string error = ValidateDefinition(definition);
                if (error != null) throw new ArgumentException(error);
                if (catalog.ContainsKey(definition.id)) throw new ArgumentException("Duplicate ID: " + definition.id);
                catalog.Add(definition.id, definition); ids.Add(definition.id);
            }
            if (ids.Count == 0) throw new ArgumentException("The catalog is empty.");
            State = new GameState();
            AgainstBot = againstBot;
            if (playerDeck == null) Initialize(State.Player, ids, "p");
            else
            {
                var chosen = new List<string>(playerDeck);
                string error = DeckRules.Validate(chosen, catalog);
                if (error != null) throw new ArgumentException(error, "playerDeck");
                InitializeExact(State.Player, chosen, "p");
            }
            Initialize(State.Opponent, ids, "o");
        }

        public static string ValidateDefinition(CardDefinition card)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.name))
                return "Enter the card ID and name.";
            if (!Enum.IsDefined(typeof(CardType), card.type) || !Enum.IsDefined(typeof(CardEffect), card.effect))
                return "Invalid type or effect.";
            if (card.cost < 0 || card.power < 0 || card.defense < 0 || card.deliveryPoints < 0 || card.effectAmount < 0)
                return "Attributes cannot be negative.";
            if (card.effect == CardEffect.EquipmentDeliveryBonus && card.type != CardType.Equipment)
                return "An equipment bonus requires the Equipment type.";
            if (card.effect != CardEffect.None && card.effect != CardEffect.EquipmentDeliveryBonus && card.type != CardType.Event)
                return "In this version, automatic effects are exclusive to events.";
            return null;
        }

        private void Initialize(PlayerState player, List<string> ids, string prefix)
        {
            // Shuffle the full catalogue before selection; all definitions can enter a game.
            var pool = new List<string>();
            while (pool.Count < 60) pool.AddRange(ids);
            for (int i = pool.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); string value = pool[i]; pool[i] = pool[j]; pool[j] = value; }
            for (int i = 0; i < 60; i++) player.Mutable(Zone.Deck).Add(new CardInstance(prefix + i, pool[i]));
            Shuffle(player);
            for (int i = 0; i < 7; i++) Draw(player);
        }

        private bool Fail(string error) { LastError = error; return false; }
        private CardInstance Find(PlayerState player, Zone zone, string id)
        { return player.Mutable(zone).Find(c => c.InstanceId == id); }

        public string ConversionBlockReason(string id)
        {
            if (State.Phase != TurnPhase.PreparingTheVoyage) return "Convert during Preparing the voyage.";
            if (State.PreparationResolved) return "The Chiralium choice has already been made this turn.";
            return Find(ActingPlayer, Zone.Hand, id) == null ? "Select a card from your hand." : null;
        }

        public bool ConvertToQuiralium(string id)
        {
            string error = ConversionBlockReason(id);
            if (error != null) return Fail(error);
            State.PreparationResolved = true; State.ConvertedThisTurn = true;
            Move(ActingPlayer, id, Zone.Hand, Zone.Quiralium);
            ActingPlayer.AvailableQuiralium++;
            Draw(ActingPlayer); LastError = null; return true;
        }

        public bool SkipConversion()
        {
            if (State.Phase != TurnPhase.PreparingTheVoyage || State.PreparationResolved)
                return Fail("The preparation choice is unavailable.");
            State.PreparationResolved = true;
            Draw(ActingPlayer); Draw(ActingPlayer); LastError = null; return true;
        }

        public bool StartDelivery()
        {
            if (State.Phase != TurnPhase.PreparingTheVoyage || !State.PreparationResolved)
                return Fail("Convert a card or choose to draw two before departing.");
            State.Phase = TurnPhase.Delivery; LastError = null; return true;
        }

        // Both sides use the same phase, payment, readiness and delivery rules.
        public bool EndTurn()
        {
            if (State.Phase != TurnPhase.Delivery) return Fail("Complete preparation before ending the turn.");
            State.Turn++;
            if (AgainstBot) State.ActivePlayer = IsBotTurn ? "player" : "opponent";
            State.Phase = TurnPhase.PreparingTheVoyage;
            State.PreparationResolved = false; State.ConvertedThisTurn = false;
            State.GlobalDeliveryPenalty = 0; State.GlobalAttackPenalty = 0; State.MutableEvents.Clear();
            State.DeliveryDifficultyModifier = 0; State.LastDelivery = null;
            ActingPlayer.AvailableQuiralium = ActingPlayer.TotalQuiralium;
            foreach (var card in ActingPlayer.Cards(Zone.Field)) card.IsTapped = false;
            foreach (var card in State.Player.Cards(Zone.Field)) card.Damage = 0;
            foreach (var card in State.Opponent.Cards(Zone.Field)) card.Damage = 0;
            LastError = null; return true;
        }

        private void InitializeExact(PlayerState player, List<string> ids, string prefix)
        {
            for (int i = 0; i < ids.Count; i++) player.Mutable(Zone.Deck).Add(new CardInstance(prefix + i, ids[i]));
            Shuffle(player);
            for (int i = 0; i < 7; i++) Draw(player);
        }
        public string PlayBlockReason(string id)
        {
            var card = Find(ActingPlayer, Zone.Hand, id);
            if (card == null) return "The card is not in your hand.";
            var definition = Definition(card);
            if (definition.type != CardType.Event && State.Phase != TurnPhase.Delivery)
                return "This card can only enter the field during Delivery.";
            if (definition.type == CardType.Event && State.Phase != TurnPhase.PreparingTheVoyage)
                return "Play events during Preparing the voyage.";
            if (ActingPlayer.AvailableQuiralium < definition.cost) return "Not enough Chiralium: cost " + definition.cost + ".";
            return null;
        }

        public bool PlayCard(string id)
        {
            string error = PlayBlockReason(id);
            if (error != null) return Fail(error);
            var card = Find(ActingPlayer, Zone.Hand, id); var definition = Definition(card);
            ActingPlayer.AvailableQuiralium -= definition.cost;
            if (definition.type == CardType.Event)
            {
                Move(ActingPlayer, id, Zone.Hand, Zone.Discard);
                ResolveEvent(definition);
            }
            else
            {
                card.EnteredOnTurn = State.Turn; card.IsTapped = false; card.Damage = 0;
                Move(ActingPlayer, id, Zone.Hand, Zone.Field);
            }
            LastError = null; return true;
        }

        public int DeliveryValue(CardInstance card) { return Math.Max(0, Definition(card).deliveryPoints - State.GlobalDeliveryPenalty); }
        public int AttackValue(CardInstance card) { return Math.Max(0, Definition(card).power - State.GlobalAttackPenalty); }
        public int RemainingDefense(CardInstance card) { return Math.Max(0, Definition(card).defense - card.Damage); }

        public int EquipmentBonus(PlayerState player)
        {
            int bonus = 0;
            foreach (var card in player.Cards(Zone.Field))
                if (Definition(card).effect == CardEffect.EquipmentDeliveryBonus)
                    bonus += Definition(card).effectAmount;
            return Math.Min(3, bonus);
        }

        public int SuccessfulDeliveryFaces(PlayerState player)
        {
            int successes = 0;
            for (int roll = 1; roll <= 6; roll++)
                if (roll + EquipmentBonus(player) >= State.DeliveryDifficulty) successes++;
            return successes;
        }

        public string ActivationBlockReason(string id)
        {
            var card = Find(ActingPlayer, Zone.Field, id);
            if (card == null) return "The card is not on the field.";
            if (State.Phase != TurnPhase.Delivery) return "Deliveries can only be attempted during Delivery.";
            if (card.EnteredOnTurn >= State.Turn) return "New arrival: wait until your next turn.";
            if (card.IsTapped) return "This card is already tapped.";
            if (Definition(card).deliveryPoints == 0) return "This card cannot make deliveries.";
            return null;
        }

        public bool ActivateCard(string id)
        {
            string error = ActivationBlockReason(id);
            if (error != null) return Fail(error);
            var card = Find(ActingPlayer, Zone.Field, id);
            int roll = rollDelivery();
            if (roll < 1 || roll > 6) return Fail("The delivery roll must be between 1 and 6.");
            var result = new DeliveryAttempt { CardName = Definition(card).name, Roll = roll,
                Bonus = EquipmentBonus(ActingPlayer), Difficulty = State.DeliveryDifficulty };
            result.Points = result.Success ? DeliveryValue(card) : 0;
            card.IsTapped = true; ActingPlayer.DeliveryScore += result.Points;
            State.LastDelivery = result;
            LastError = null; return true;
        }

        public bool CanBeAttacked(PlayerState owner, string id)
        {
            var card = Find(owner, Zone.Field, id);
            return card != null && card.IsTapped;
        }

        private void ResolveEvent(CardDefinition definition)
        {
            if (definition.effect == CardEffect.GlobalDamage)
            {
                DamageField(State.Player, definition.effectAmount);
                DamageField(State.Opponent, definition.effectAmount);
            }
            if (definition.effect == CardEffect.GlobalDeliveryPenalty) State.GlobalDeliveryPenalty += definition.effectAmount;
            if (definition.effect == CardEffect.GlobalAttackPenalty) State.GlobalAttackPenalty += definition.effectAmount;
            if (definition.effect == CardEffect.IncreaseDeliveryDifficulty) State.DeliveryDifficultyModifier += definition.effectAmount;
            if (definition.effect == CardEffect.DecreaseDeliveryDifficulty) State.DeliveryDifficultyModifier -= definition.effectAmount;
            if (definition.effect != CardEffect.None) State.MutableEvents.Add(definition.name);
        }

        private void DamageField(PlayerState player, int amount)
        {
            // Copy: lethal damage removes cards from the field during iteration.
            foreach (var card in new List<CardInstance>(player.Cards(Zone.Field))) ApplyDamage(player, card.InstanceId, amount);
        }

        // Shared death path for global events now and combat later.
        public bool ApplyDamage(PlayerState owner, string id, int amount)
        {
            if (owner != State.Player && owner != State.Opponent) return Fail("Invalid player.");
            var card = Find(owner, Zone.Field, id);
            if (card == null || amount <= 0) return Fail("Damage requires a card on the field and a positive amount.");
            card.Damage += amount;
            if (card.Damage >= Definition(card).defense) Move(owner, id, Zone.Field, Zone.Beach);
            LastError = null; return true;
        }

        // Playground zone controls remain explicit, separate from normal turn actions.
        public bool SendToBeach(string id) { return Move(ActingPlayer, id, Zone.Field, Zone.Beach); }
        public bool ReturnFromBeach(string id) { return Move(ActingPlayer, id, Zone.Beach, Zone.Hand); }
        public void ShuffleDeck() { Shuffle(ActingPlayer); }

        private bool Draw(PlayerState player)
        {
            var deck = player.Cards(Zone.Deck);
            return deck.Count > 0 && Move(player, deck[0].InstanceId, Zone.Deck, Zone.Hand);
        }
        private void Shuffle(PlayerState player)
        {
            var deck = player.Mutable(Zone.Deck);
            for (int i = deck.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); var card = deck[i]; deck[i] = deck[j]; deck[j] = card; }
        }
        private bool Move(PlayerState player, string id, Zone from, Zone to)
        {
            var source = player.Mutable(from); var card = source.Find(c => c.InstanceId == id);
            if (card == null) return Fail("The card is not in the source zone.");
            source.Remove(card); player.Mutable(to).Add(card);
            if (to == Zone.Beach || to == Zone.Hand || to == Zone.Quiralium)
            { card.IsTapped = false; card.Damage = 0; card.EnteredOnTurn = 0; }
            if (CardMoved != null) CardMoved(from, to, card);
            LastError = null; return true;
        }
    }
}
