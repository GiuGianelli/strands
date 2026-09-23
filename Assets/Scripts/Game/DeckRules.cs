using System;
using System.Collections.Generic;

namespace Strands.Game
{
    [Serializable]
    public sealed class DeckDefinition
    {
        public string id, name;
        public List<string> cards = new List<string>();
        public DeckDefinition Copy()
        { return new DeckDefinition { id = id, name = name, cards = new List<string>(cards) }; }
    }

    public static class DeckRules
    {
        public const int DeckSize = 60, MaxCopies = 4;

        public static string Validate(IList<string> cards, IDictionary<string, CardDefinition> catalog)
        {
            if (cards == null || cards.Count != DeckSize) return "A playable deck needs exactly 60 cards.";
            var counts = new Dictionary<string, int>();
            foreach (string id in cards)
            {
                if (id == null || !catalog.ContainsKey(id)) return "This deck contains a card missing from the catalog.";
                int count; counts.TryGetValue(id, out count); counts[id] = count + 1;
                if (counts[id] > MaxCopies) return "A deck can contain at most 4 copies of each card.";
            }
            return null;
        }
    }
}
