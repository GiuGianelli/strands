using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Strands.Data;
using Strands.Game;

namespace Strands.Editor
{
    // Drafts are ordinary JSON files in the project, not an account or database.
    public sealed class CardWorkshop : EditorWindow
    {
        private const string DraftPath = "Assets/CardWorkshop/drafts.json";
        private const string CatalogPath = "Assets/Resources/cards.json";
        [SerializeField] private CardDefinition draft = new CardDefinition();
        [SerializeField] private string message = "Write an idea. Saving a draft does not change the deck.";
        private Vector2 scroll;
        private int draftIndex, catalogIndex;
        private bool dirty;
        private CardDocument drafts = new CardDocument();
        private CardDocument catalog = new CardDocument();

        [MenuItem("Strands/Card Workshop")]
        public static void Open()
        {
            var window = GetWindow<CardWorkshop>("Card Workshop");
            window.minSize = new Vector2(570, 660);
            window.Show();
        }

        private void OnEnable()
        {
            try { Reload(); }
            catch (Exception ex) { message = "Failed to read data: " + ex.Message; }
        }
        private void OnDisable()
        {
            // Keep uncommitted text across closing the window and domain reloads.
            if (dirty) SaveDraft();
        }
        private void Reload()
        {
            drafts = Read(DraftPath); catalog = Read(CatalogPath);
            draftIndex = Mathf.Clamp(draftIndex, 0, Mathf.Max(0, drafts.cards.Length - 1));
            catalogIndex = Mathf.Clamp(catalogIndex, 0, Mathf.Max(0, catalog.cards.Length - 1));
        }
        private static CardDocument Read(string path)
        {
            if (!File.Exists(path)) return new CardDocument();
            var document = JsonUtility.FromJson<CardDocument>(File.ReadAllText(path));
            if (document == null || document.cards == null) throw new InvalidDataException("Invalid card document: " + path);
            return document;
        }
        private static CardDefinition Clone(CardDefinition card)
        { return JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(card)); }
        private static string[] Names(CardDocument document)
        {
            var names = new string[document.cards.Length];
            for (int i = 0; i < names.Length; i++) names[i] = document.cards[i].name + "  [" + document.cards[i].id + "]";
            return names;
        }

        private void OnGUI()
        {
            GUILayout.Label("WORKSHOP / NEW CONNECTIONS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Drafts stay in the project. Adding a card to the catalog makes it available for the next game. Effect text is freeform; only the listed behaviors execute automatically.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New idea"))
                {
                    if (dirty) SaveDraft();
                    draft = new CardDefinition { id = "card-" + Guid.NewGuid().ToString("N").Substring(0, 8), name = "New card", faction = "Connection", rarity = "Common", defense = 1, deliveryPoints = 1 };
                    dirty = true;
                }
                if (GUILayout.Button("Reload lists"))
                { if (dirty) SaveDraft(); OnEnable(); }
            }
            if (drafts.cards.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    draftIndex = EditorGUILayout.Popup("Drafts", draftIndex, Names(drafts));
                    if (GUILayout.Button("Open", GUILayout.Width(80)))
                    { if (dirty) SaveDraft(); draft = Clone(drafts.cards[draftIndex]); dirty = false; }
                }
            }
            if (catalog.cards.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    catalogIndex = EditorGUILayout.Popup("Game cards", catalogIndex, Names(catalog));
                    if (GUILayout.Button("Edit copy", GUILayout.Width(100)))
                    { if (dirty) SaveDraft(); draft = Clone(catalog.cards[catalogIndex]); dirty = false; }
                }
            }
            EditorGUILayout.Space(12);
            EditorGUI.BeginChangeCheck();
            draft.id = EditorGUILayout.TextField("Unique ID", draft.id ?? "");
            draft.name = EditorGUILayout.TextField("Name", draft.name ?? "");
            draft.type = (CardType)EditorGUILayout.EnumPopup("Type", draft.type);
            draft.cost = EditorGUILayout.IntField("Chiralium cost", draft.cost);
            draft.power = EditorGUILayout.IntField("Attack", draft.power);
            draft.defense = EditorGUILayout.IntField("Defense", draft.defense);
            draft.deliveryPoints = EditorGUILayout.IntField("Delivery points", draft.deliveryPoints);
            draft.faction = EditorGUILayout.TextField("Faction", draft.faction ?? "");
            draft.rarity = EditorGUILayout.TextField("Rarity", draft.rarity ?? "");
            GUILayout.Label("Description / story", EditorStyles.boldLabel);
            draft.description = EditorGUILayout.TextArea(draft.description ?? "", GUILayout.MinHeight(65));
            GUILayout.Label("Effect / card text", EditorStyles.boldLabel);
            draft.text = EditorGUILayout.TextArea(draft.text ?? "", GUILayout.MinHeight(85));
            draft.effect = (CardEffect)EditorGUILayout.EnumPopup("Executable behavior", draft.effect);
            draft.effectAmount = EditorGUILayout.IntField("Effect strength", draft.effectAmount);
            draft.image = EditorGUILayout.TextField("Artwork in Resources", draft.image ?? "");
            if (EditorGUI.EndChangeCheck()) dirty = true;
            EditorGUILayout.HelpBox("IncreaseDeliveryDifficulty / DecreaseDeliveryDifficulty: events that change difficulty for both fields until the next turn (range 2–6). EquipmentDeliveryBonus: passive bonus to your rolls while the equipment is on the field (maximum total +3).\nNone: basic delivery. Legacy damage and penalty effects remain available. Artwork: Cards/porter-001 (path without extension).", MessageType.None);
            var art = string.IsNullOrWhiteSpace(draft.image) ? null : Resources.Load<Texture2D>(draft.image);
            if (art != null)
            {
                var rect = GUILayoutUtility.GetRect(250, 160, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, art, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save draft", GUILayout.Height(35))) SaveDraft();
                if (GUILayout.Button("Add / update in game", GUILayout.Height(35))) Publish();
            }
            EditorGUILayout.HelpBox(message, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void SaveDraft()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(draft.id)) draft.id = "idea-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                if (string.IsNullOrWhiteSpace(draft.name)) draft.name = "Untitled idea";
                Upsert(DraftPath, draft); dirty = false; Reload();
                message = "Draft saved to " + DraftPath;
            }
            catch (Exception ex) { message = "Could not save: " + ex.Message; Debug.LogError(message); }
        }

        private void Publish()
        {
            string error = GameSession.ValidateDefinition(draft);
            if (error != null) { message = error; return; }
            try
            {
                // Re-read before writing to preserve edits made outside this window.
                var latest = Read(CatalogPath);
                bool exists = Array.Exists(latest.cards, card => card.id == draft.id);
                if (exists && !EditorUtility.DisplayDialog("Update card", "Update the definition of " + draft.id + " in the game catalog?", "Update", "Cancel")) return;
                Upsert(CatalogPath, draft); SaveDraft();
                AssetDatabase.ImportAsset(CatalogPath);
                message = "Card added to the catalog. Restart Play to test; create a new build for the browser.";
            }
            catch (Exception ex) { message = "Could not update: " + ex.Message; Debug.LogError(message); }
        }

        private static void Upsert(string path, CardDefinition card)
        {
            var document = Read(path); var cards = new List<CardDefinition>(document.cards);
            int index = cards.FindIndex(existing => existing.id == card.id);
            if (index >= 0) cards[index] = Clone(card); else cards.Add(Clone(card));
            document.cards = cards.ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(document, true) + "\n", new System.Text.UTF8Encoding(false));
        }
    }
}
