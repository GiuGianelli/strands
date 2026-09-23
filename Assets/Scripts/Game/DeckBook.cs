using System;
using System.Collections.Generic;

namespace Strands.Game
{
    [Serializable]
    public sealed class DeckBook
    {
        public int version = 1;
        public string activeDeckId;
        public List<DeckDefinition> decks = new List<DeckDefinition>();

        public DeckDefinition Find(string id) { return decks.Find(deck => deck.id == id); }
        public DeckDefinition Create(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 40)
                throw new ArgumentException("Use a deck name between 1 and 40 characters.");
            var deck = new DeckDefinition { id = Guid.NewGuid().ToString("N"), name = name.Trim() };
            decks.Add(deck); return deck;
        }
        public DeckDefinition Duplicate(string id)
        {
            var original = Find(id);
            if (original == null) throw new ArgumentException("Deck not found.");
            var copy = Create(original.name.Substring(0, Math.Min(33, original.name.Length)) + " (copy)");
            copy.cards.AddRange(original.cards); return copy;
        }
        public void Save(DeckDefinition draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.name)) throw new ArgumentException("Give your deck a name.");
            if (draft.name.Length > 40) throw new ArgumentException("Deck names can contain up to 40 characters.");
            int index = decks.FindIndex(deck => deck.id == draft.id);
            if (index < 0) throw new ArgumentException("Deck not found.");
            var copy = draft.Copy(); copy.name = copy.name.Trim(); decks[index] = copy;
        }
        public bool Delete(string id)
        {
            var deck = Find(id); if (deck == null) return false;
            decks.Remove(deck);
            if (activeDeckId == id) activeDeckId = null;
            return true;
        }
        public string Select(string id, IDictionary<string, CardDefinition> catalog)
        {
            var deck = Find(id);
            if (deck == null) return "Deck not found.";
            string error = DeckRules.Validate(deck.cards, catalog);
            if (error == null) activeDeckId = id;
            return error;
        }
    }
}
