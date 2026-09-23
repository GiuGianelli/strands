using System;
using System.Collections.Generic;
using UnityEngine;
using Strands.Game;

namespace Strands.Data
{
    // PlayerPrefs also persists in browser storage in Unity Web builds.
    public static class DeckStorage
    {
        private static string Key { get { return Application.isBatchMode ? "Strands.Decks.v1.Smoke" : "Strands.Decks.v1"; } }
        public static DeckBook Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new DeckBook();
            var book = JsonUtility.FromJson<DeckBook>(PlayerPrefs.GetString(Key));
            if (book == null || book.version != 1 || book.decks == null)
                throw new InvalidOperationException("Saved decks could not be read. Existing storage was preserved.");
            var ids = new HashSet<string>();
            foreach (var deck in book.decks)
                if (deck == null || string.IsNullOrWhiteSpace(deck.id) || !ids.Add(deck.id) || deck.cards == null || deck.name == null)
                    throw new InvalidOperationException("Saved deck data is invalid. Existing storage was preserved.");
            return book;
        }
        public static void Save(DeckBook book)
        {
            string json = JsonUtility.ToJson(book);
            if (PlayerPrefs.HasKey(Key)) PlayerPrefs.SetString(Key + ".backup", PlayerPrefs.GetString(Key));
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }
    }
}
