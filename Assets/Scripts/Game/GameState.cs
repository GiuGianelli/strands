using System;
using System.Collections.Generic;

namespace Strands.Game
{
    public enum CardType { Porter, Equipment, Structure, Event, Entity }
    public enum Zone { Deck, Hand, Field, Beach, Discard, Quiralium }
    public enum TurnPhase { PreparingTheVoyage, Delivery }
    public enum CardEffect { None, GlobalDamage, GlobalDeliveryPenalty, GlobalAttackPenalty,
        IncreaseDeliveryDifficulty, DecreaseDeliveryDifficulty, EquipmentDeliveryBonus }

    public sealed class DeliveryAttempt
    {
        public string CardName { get; internal set; }
        public int Roll { get; internal set; }
        public int Bonus { get; internal set; }
        public int Difficulty { get; internal set; }
        public int Points { get; internal set; }
        public bool Success { get { return Roll + Bonus >= Difficulty; } }
    }

    [Serializable]
    public class CardDefinition
    {
        public string id, name, faction, rarity, image, text, description;
        public CardType type;
        public int cost, power, defense, deliveryPoints;
        public CardEffect effect;
        public int effectAmount;
    }

    public sealed class CardInstance
    {
        public readonly string InstanceId;
        public readonly string DefinitionId;
        public int EnteredOnTurn { get; internal set; }
        public bool IsTapped { get; internal set; }
        public int Damage { get; internal set; }
        public CardInstance(string instanceId, string definitionId)
        { InstanceId = instanceId; DefinitionId = definitionId; }
    }

    public sealed class PlayerState
    {
        public int AvailableQuiralium { get; internal set; }
        public int TotalQuiralium { get { return zones[Zone.Quiralium].Count; } }
        public int DeliveryScore { get; internal set; }
        private readonly Dictionary<Zone, List<CardInstance>> zones = new Dictionary<Zone, List<CardInstance>>();
        public PlayerState()
        { foreach (Zone zone in Enum.GetValues(typeof(Zone))) zones.Add(zone, new List<CardInstance>()); }
        public IReadOnlyList<CardInstance> Cards(Zone zone) { return zones[zone].AsReadOnly(); }
        internal List<CardInstance> Mutable(Zone zone) { return zones[zone]; }
    }

    public sealed class GameState
    {
        public PlayerState Player { get; private set; }
        public PlayerState Opponent { get; private set; }
        public int Turn { get; internal set; }
        public string ActivePlayer { get; internal set; }
        public TurnPhase Phase { get; internal set; }
        public bool PreparationResolved { get; internal set; }
        public bool ConvertedThisTurn { get; internal set; }
        public int GlobalDeliveryPenalty { get; internal set; }
        public int GlobalAttackPenalty { get; internal set; }
        public int DeliveryDifficultyModifier { get; internal set; }
        public int DeliveryDifficulty { get { return Math.Max(2, Math.Min(6, 3 + DeliveryDifficultyModifier)); } }
        public DeliveryAttempt LastDelivery { get; internal set; }
        private readonly List<string> activeEvents = new List<string>();
        public IReadOnlyList<string> ActiveEvents { get { return activeEvents.AsReadOnly(); } }
        internal List<string> MutableEvents { get { return activeEvents; } }
        public GameState()
        { Player = new PlayerState(); Opponent = new PlayerState(); Turn = 1; ActivePlayer = "player"; }
    }
}
