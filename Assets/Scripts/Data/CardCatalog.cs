using System;
using UnityEngine;
using Strands.Game;

namespace Strands.Data
{
    [Serializable]
    public class CardDocument { public CardDefinition[] cards = new CardDefinition[0]; }

    public static class CardCatalog
    {
        public static CardDefinition[] Load()
        {
            var asset = Resources.Load<TextAsset>("cards");
            if (asset == null) throw new InvalidOperationException("Resources/cards.json not found.");
            return JsonUtility.FromJson<CardDocument>(asset.text).cards;
        }
    }
}
