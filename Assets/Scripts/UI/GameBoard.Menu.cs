using System;
using System.Collections.Generic;
using UnityEngine;
using Strands.Data;
using Strands.Game;

namespace Strands.UI
{
    public sealed partial class GameBoard
    {
        private enum AppScreen { Menu, Decks, Rules, Match }
        private AppScreen screen = AppScreen.Menu;
        private CardDefinition[] definitions;
        private readonly Dictionary<string, CardDefinition> definitionsById = new Dictionary<string, CardDefinition>();
        private DeckBook deckBook;
        private DeckDefinition editingDeck;
        private readonly SoloBot bot = new SoloBot();
        private bool deckDirty, matchStarted, storageBlocked, drawingDialog;
        private string frontNotice = "", search = "", rulesText = "", dialogText;
        private int catalogPage, deckPage, savedDeckPage, typeFilter, opponentPage, menuFocus;
        private Vector2 rulesScroll;
        private Texture2D menuBackground;
        private float nextBotAction;
        private Action dialogAction;
        private CardDefinition inspecting;
        private static readonly string[] Filters = { "All", "Porter", "Equipment", "Structure", "Event", "Entity" };

        private void InitializeFrontEnd()
        {
            definitions = CardCatalog.Load();
            foreach (var definition in definitions) definitionsById.Add(definition.id, definition);
            menuBackground = Resources.Load<Texture2D>("Menu/shore");
            var rules = Resources.Load<TextAsset>("Rules");
            rulesText = rules == null ? "Rules could not be loaded." : rules.text;
            try
            {
                deckBook = DeckStorage.Load();
                if (deckBook.decks.Count == 0)
                {
                    var starter = deckBook.Create("The First Crossing");
                    // Preserve the actual 60-card opening selection as the starter deck.
                    foreach (Zone zone in Enum.GetValues(typeof(Zone)))
                        foreach (var card in session.State.Player.Cards(zone)) starter.cards.Add(card.DefinitionId);
                    deckBook.Select(starter.id, definitionsById);
                    PersistDecks();
                }
            }
            catch (Exception ex)
            {
                deckBook = new DeckBook(); storageBlocked = true;
                frontNotice = "Saved decks could not be loaded. Storage was preserved. " + ex.Message;
            }
            if (deckBook.decks.Count > 0) EditDeck(deckBook.Find(deckBook.activeDeckId) ?? deckBook.decks[0]);
        }

        private void Update()
        {
            if (screen != AppScreen.Match || !session.IsBotTurn || Time.unscaledTime < nextBotAction) return;
            selected = null; beachOpen = false;
            notice = bot.Step(session) ?? notice;
            nextBotAction = Time.unscaledTime + .85f;
        }

        private bool PersistDecks()
        {
            if (storageBlocked) { frontNotice = "Deck storage is unavailable. Existing data has been preserved."; return false; }
            try { DeckStorage.Save(deckBook); return true; }
            catch (Exception ex) { frontNotice = "Could not save decks: " + ex.Message; return false; }
        }

        private void EditDeck(DeckDefinition deck)
        {
            editingDeck = deck == null ? null : deck.Copy();
            deckDirty = false; deckPage = 0;
        }

        private bool SaveDeck()
        {
            if (editingDeck == null) return false;
            try
            {
                deckBook.Save(editingDeck);
                if (!PersistDecks()) return false;
                deckDirty = false;
                frontNotice = DeckRules.Validate(editingDeck.cards, definitionsById) == null ? "Deck saved. Ready to play." : "Draft saved. Complete the deck to play.";
                return true;
            }
            catch (Exception ex) { frontNotice = ex.Message; return false; }
        }

        private void GuardChanges(Action action)
        {
            if (!deckDirty) { action(); return; }
            dialogText = "Discard unsaved deck changes?\nChoose Cancel to go back and save your draft.";
            dialogAction = () => { deckDirty = false; action(); };
        }

        private void RequestMatch()
        {
            var deck = deckBook.Find(deckBook.activeDeckId);
            string error = deck == null ? "Select a complete deck before starting a match." : DeckRules.Validate(deck.cards, definitionsById);
            if (error != null) { screen = AppScreen.Decks; frontNotice = error; return; }
            if (matchStarted)
            {
                dialogText = "Start a new match?\nThe current match will be replaced.";
                dialogAction = StartBotMatch;
            }
            else StartBotMatch();
        }

        private void StartBotMatch()
        {
            var deck = deckBook.Find(deckBook.activeDeckId);
            if (deck == null || DeckRules.Validate(deck.cards, definitionsById) != null)
            { frontNotice = "Select a valid 60-card deck first."; screen = AppScreen.Decks; return; }
            session.CardMoved -= OnCardMoved;
            session = new GameSession(definitions, playerDeck: deck.cards, againstBot: true);
            session.CardMoved += OnCardMoved;
            selected = null; beachOpen = false; flights.Clear(); lift.Clear(); tapAngles.Clear();
            handPage = fieldPage = opponentPage = 0;
            notice = "Your turn. Convert a card into Chiralium or draw two.";
            nextBotAction = Time.unscaledTime + .85f;
            matchStarted = true; screen = AppScreen.Match;
        }

        private void DrawFrontEnd()
        {
            Fill(new Rect(0, 0, 1600, 900), new Color(.045f, .07f, .08f));
            if (menuBackground != null) GUI.DrawTexture(new Rect(0, 0, 1600, 900), menuBackground, ScaleMode.ScaleAndCrop);
            // A graduated scrim keeps the distant landscape visible behind real controls.
            for (int x = 0; x < 1600; x += 20)
                Fill(new Rect(x, 0, 20, 900), new Color(.01f, .025f, .035f, Mathf.Lerp(.76f, .04f, x / 1600f)));
            if (screen != AppScreen.Menu) Fill(new Rect(0, 0, 1600, 900), new Color(.012f, .03f, .04f, .89f));
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                if (inspecting != null) inspecting = null;
                else if (dialogAction != null) dialogAction = null;
                else GuardChanges(() => { screen = AppScreen.Menu; });
                Event.current.Use();
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = dialogAction == null && inspecting == null;
            if (screen == AppScreen.Menu) MainMenu();
            else if (screen == AppScreen.Decks) DeckEditor();
            else RulesPage();
            GUI.enabled = oldEnabled;
            if (inspecting != null) InspectCatalogCard();
            if (dialogAction != null) Confirmation();
        }

        private void MainMenu()
        {
            Text(new Rect(130, 93, 600, 25), "A CONNECTION WORTH CROSSING FOR", 11, new Color(.77f, .85f, .86f));
            StrandLetters(new Rect(124, 142, 720, 95), "STRANDS", 64, 15, 78, Color.white);
            Text(new Rect(132, 271, 530, 27), "THE SPACE BETWEEN WORLDS", 12, Muted);
            var e = Event.current;
            if (GUI.enabled && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.DownArrow) { menuFocus = (menuFocus + 1) % 3; e.Use(); }
                else if (e.keyCode == KeyCode.UpArrow) { menuFocus = (menuFocus + 2) % 3; e.Use(); }
            }
            if (StrandButton(new Rect(124, 365, 540, 89), "Play Solo vs Bot", "01", 0)) RequestMatch();
            if (StrandButton(new Rect(124, 479, 540, 89), "Check your Decks", "02", 1)) screen = AppScreen.Decks;
            if (StrandButton(new Rect(124, 593, 540, 89), "Rules", "03", 2)) screen = AppScreen.Rules;
            var deck = deckBook.Find(deckBook.activeDeckId);
            Text(new Rect(132, 732, 630, 26), deck == null ? "NO ACTIVE DECK / SELECT ONE IN YOUR COLLECTION" : "ACTIVE DECK / " + deck.name + "  ·  " + deck.cards.Count + " CARDS", 12, new Color(.78f, .85f, .85f));
            if (matchStarted && FrontButton(new Rect(130, 775, 220, 35), "RESUME MATCH")) screen = AppScreen.Match;
            if (FrontButton(new Rect(1255, 827, 285, 34), reducedMotion ? "MOTION: REDUCED" : "MOTION: SMOOTH")) reducedMotion = !reducedMotion;
            Text(new Rect(130, 845, 1070, 28), string.IsNullOrEmpty(frontNotice) ? "LOCAL PLAY  /  THE WAYFARER AWAITS" : frontNotice, 11, Cyan);
        }

        private void StrandLetters(Rect r, string text, int size, float spacing, float strands, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = color;
            float x = r.x;
            for (int i = 0; i < text.Length; i++)
            {
                string glyph = text[i].ToString(); float width = style.CalcSize(new GUIContent(glyph)).x;
                GUI.Label(new Rect(x, r.y, width + 3, r.height), glyph, style);
                if (text[i] != ' ' && i % 3 != 1)
                {
                    float length = strands * (.35f + (i * 17 % 13) / 13f);
                    if (!reducedMotion) length *= 1 + Mathf.Sin(Time.unscaledTime * .65f + i) * .08f;
                    float start = r.y + r.height / 2 + size * .31f;
                    for (int segment = 0; segment < 8; segment++)
                        Fill(new Rect(x + width * .48f, start + segment * length / 8, segment < 2 ? 1.2f : .7f, length / 8 + .2f), new Color(color.r, color.g, color.b, color.a * .5f * (1 - segment / 8f)));
                }
                x += width + spacing;
            }
        }

        private bool StrandButton(Rect r, string title, string number, int index)
        {
            bool hover = GUI.enabled && r.Contains(mouse);
            if (hover && Event.current.type == EventType.MouseMove) menuFocus = index;
            bool focus = GUI.enabled && (hover || menuFocus == index);
            Fill(r, new Color(.035f, .065f, .08f, focus ? .5f : .22f));
            Fill(new Rect(r.x, r.yMax, r.width, 1), new Color(.65f, .83f, .85f, focus ? .8f : .25f));
            if (focus) Fill(new Rect(r.x, r.y + 13, 2, r.height - 26), Cyan);
            Text(new Rect(r.x + 19, r.y + 33, 35, 24), number, 10, Muted);
            StrandLetters(new Rect(r.x + 62, r.y + 6, r.width - 95, 62), title, 21, 3, focus ? 42 : 25, focus ? Color.white : new Color(.81f, .87f, .89f));
            Text(new Rect(r.xMax - 38, r.y + 29, 30, 26), "→", 20, focus ? Cyan : Muted);
            if (!GUI.enabled) return false;
            var e = Event.current;
            if (menuFocus == index && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) { e.Use(); return true; }
            return RawClick(r);
        }

        private bool FrontButton(Rect r, string title, bool enabled = true)
        {
            enabled = enabled && GUI.enabled && (drawingDialog || (dialogAction == null && inspecting == null));
            bool hover = enabled && r.Contains(mouse);
            Panel(r, hover ? new Color(.12f, .24f, .27f, .97f) : new Color(.04f, .085f, .10f, .93f), enabled ? new Color(.3f, .49f, .51f) : new Color(.13f, .19f, .21f));
            Text(r, title, 11, enabled ? Color.white : Muted, TextAnchor.MiddleCenter);
            return enabled && RawClick(r);
        }

        private static bool RawClick(Rect r)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use(); return true;
        }

        private void PageHeader(string title, string subtitle)
        {
            StrandLetters(new Rect(45, 23, 1140, 70), title, 27, 5, 27, Color.white);
            Text(new Rect(47, 103, 1170, 26), subtitle, 12, Muted);
            if (FrontButton(new Rect(1375, 45, 180, 35), "BACK TO MENU")) GuardChanges(() => { screen = AppScreen.Menu; });
        }

        private void DeckEditor()
        {
            PageHeader("YOUR CONNECTIONS", "Build a 60-card deck · Up to 4 copies per card · Incomplete decks can be saved as drafts");
            Panel(new Rect(35, 151, 245, 666), new Color(.025f, .05f, .065f, .95f), Muted * .4f);
            Text(new Rect(52, 167, 210, 28), "YOUR DECKS / " + deckBook.decks.Count, 14, Cyan);
            if (FrontButton(new Rect(50, 207, 215, 34), "+ CREATE DECK", !storageBlocked)) GuardChanges(() =>
            { var deck = deckBook.Create("New deck"); EditDeck(deck); PersistDecks(); });
            savedDeckPage = Mathf.Clamp(savedDeckPage, 0, Mathf.Max(0, (deckBook.decks.Count - 1) / 8));
            for (int i = savedDeckPage * 8; i < Mathf.Min(deckBook.decks.Count, savedDeckPage * 8 + 8); i++)
            {
                var deck = deckBook.decks[i];
                string marker = deckBook.activeDeckId == deck.id ? "● " : "";
                if (FrontButton(new Rect(50, 259 + (i % 8) * 54, 215, 44), marker + deck.name + "\n" + deck.cards.Count + "/60"))
                    GuardChanges(() => EditDeck(deck));
            }
            FrontPages(ref savedDeckPage, deckBook.decks.Count, 8, new Rect(50, 756, 215, 32));

            Text(new Rect(305, 155, 380, 26), "CARD CATALOG / " + definitions.Length, 14, Cyan);
            var newSearch = GUI.TextField(new Rect(305, 194, 428, 34), search, 80);
            if (newSearch != search) { search = newSearch; catalogPage = 0; }
            if (search.Length == 0) Text(new Rect(316, 201, 405, 25), "Search by name, faction or effect…", 12, Muted);
            for (int i = 0; i < Filters.Length; i++)
                if (FrontButton(new Rect(305 + i * 120, 239, 113, 30), (typeFilter == i ? "● " : "") + Filters[i])) { typeFilter = i; catalogPage = 0; }
            var filtered = new List<CardDefinition>();
            foreach (var card in definitions)
                if ((typeFilter == 0 || (int)card.type == typeFilter - 1)
                    && (card.name + " " + card.faction + " " + card.text).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) filtered.Add(card);
            catalogPage = Mathf.Clamp(catalogPage, 0, Mathf.Max(0, (filtered.Count - 1) / 6));
            for (int i = catalogPage * 6; i < Mathf.Min(filtered.Count, catalogPage * 6 + 6); i++)
            {
                var definition = filtered[i]; int slot = i % 6;
                var r = new Rect(335 + slot % 3 * 238, 290 + slot / 3 * 244, 146, 198);
                DrawCard(r, new CardInstance("catalog-" + definition.id, definition.id));
                if (GUI.enabled && RawClick(r)) inspecting = definition;
                int count = editingDeck == null ? 0 : editingDeck.cards.FindAll(id => id == definition.id).Count;
                if (FrontButton(new Rect(r.x - 10, r.yMax + 7, 166, 30), "+ ADD / " + count + " OF 4", editingDeck != null && editingDeck.cards.Count < 60 && count < 4 && !storageBlocked))
                { editingDeck.cards.Add(definition.id); deckDirty = true; }
            }
            if (filtered.Count == 0) Text(new Rect(320, 410, 680, 50), "No cards match your search.", 18, Muted, TextAnchor.MiddleCenter);
            FrontPages(ref catalogPage, filtered.Count, 6, new Rect(535, 793, 280, 32));

            Panel(new Rect(1062, 151, 500, 678), new Color(.025f, .05f, .065f, .95f), Muted * .4f);
            if (editingDeck == null) Text(new Rect(1100, 290, 425, 80), "Create or select a deck to start building.", 19, Muted, TextAnchor.MiddleCenter);
            else DrawDeckContents();
            Text(new Rect(45, 853, 1490, 35), frontNotice, 13, Cyan);
        }

        private void DrawDeckContents()
        {
            string name = GUI.TextField(new Rect(1080, 166, 350, 34), editingDeck.name ?? "", 40);
            if (name != editingDeck.name) { editingDeck.name = name; deckDirty = true; }
            Text(new Rect(1440, 169, 100, 30), editingDeck.cards.Count + " / 60", 19, Gold, TextAnchor.MiddleRight);
            string problem = DeckRules.Validate(editingDeck.cards, definitionsById);
            Text(new Rect(1080, 212, 460, 32), problem ?? "READY TO PLAY" + (deckDirty ? " / UNSAVED" : " / SAVED"), 12, problem == null ? Cyan : Gold);
            var unique = new List<string>();
            foreach (string id in editingDeck.cards) if (!unique.Contains(id)) unique.Add(id);
            deckPage = Mathf.Clamp(deckPage, 0, Mathf.Max(0, (unique.Count - 1) / 9));
            for (int i = deckPage * 9; i < Mathf.Min(unique.Count, deckPage * 9 + 9); i++)
            {
                string id = unique[i]; CardDefinition d; definitionsById.TryGetValue(id, out d);
                int count = editingDeck.cards.FindAll(value => value == id).Count;
                float y = 258 + i % 9 * 44;
                Fill(new Rect(1080, y, 460, 39), new Color(.09f, .14f, .16f, .75f));
                Text(new Rect(1090, y + 6, 315, 30), count + "×  " + (d == null ? "Missing: " + id : d.name), 12, Color.white);
                if (FrontButton(new Rect(1410, y + 3, 35, 32), "−", !storageBlocked)) { editingDeck.cards.Remove(id); deckDirty = true; }
                if (FrontButton(new Rect(1452, y + 3, 35, 32), "+", d != null && count < 4 && editingDeck.cards.Count < 60 && !storageBlocked)) { editingDeck.cards.Add(id); deckDirty = true; }
                if (d != null) Text(new Rect(1491, y + 7, 45, 25), d.cost + " CH", 10, Gold, TextAnchor.MiddleCenter);
            }
            if (unique.Count == 0) Text(new Rect(1090, 350, 440, 70), "Your deck is empty.\nAdd cards from the catalog.", 17, Muted, TextAnchor.MiddleCenter);
            FrontPages(ref deckPage, unique.Count, 9, new Rect(1180, 663, 270, 28));
            if (FrontButton(new Rect(1080, 707, 225, 36), "SAVE DECK" + (deckDirty ? " *" : ""), !storageBlocked)) SaveDeck();
            if (FrontButton(new Rect(1315, 707, 225, 36), "USE THIS DECK", problem == null && !storageBlocked))
            {
                if (SaveDeck()) { frontNotice = deckBook.Select(editingDeck.id, definitionsById) ?? "Active deck selected."; PersistDecks(); }
            }
            if (FrontButton(new Rect(1080, 758, 225, 33), "DUPLICATE", !storageBlocked)) GuardChanges(() =>
            { var copy = deckBook.Duplicate(editingDeck.id); EditDeck(copy); PersistDecks(); });
            if (FrontButton(new Rect(1315, 758, 225, 33), "DELETE DECK", !storageBlocked))
            {
                string id = editingDeck.id;
                dialogText = "Delete “" + editingDeck.name + "”?\nThis also discards any unsaved edits to this deck.";
                dialogAction = () => { deckBook.Delete(id); EditDeck(deckBook.decks.Count == 0 ? null : deckBook.decks[0]); PersistDecks(); };
            }
        }

        private void FrontPages(ref int page, int count, int size, Rect r)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)size));
            if (FrontButton(new Rect(r.x, r.y, 40, r.height), "←", page > 0)) page--;
            Text(new Rect(r.x + 40, r.y, r.width - 80, r.height), (page + 1) + " / " + pages, 12, Muted, TextAnchor.MiddleCenter);
            if (FrontButton(new Rect(r.xMax - 40, r.y, 40, r.height), "→", page < pages - 1)) page++;
        }

        private void RulesPage()
        {
            PageHeader("RULES OF THE CROSSING", "The rules currently implemented in Strands");
            Panel(new Rect(155, 155, 1290, 660), new Color(.025f, .05f, .065f, .96f), Muted * .4f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = new Color(.81f, .87f, .88f);
            float height = style.CalcHeight(new GUIContent(rulesText), 1160) + 40;
            rulesScroll = GUI.BeginScrollView(new Rect(190, 180, 1220, 610), rulesScroll, new Rect(0, 0, 1180, height));
            GUI.Label(new Rect(0, 0, 1160, height), rulesText, style);
            GUI.EndScrollView();
            Text(new Rect(190, 840, 1220, 25), "Scroll to read · Escape returns to the menu", 12, Muted);
        }

        private void InspectCatalogCard()
        {
            drawingDialog = true;
            Fill(new Rect(0, 0, 1600, 900), new Color(0, .01f, .02f, .94f));
            DrawCard(new Rect(370, 150, 340, 480), new CardInstance("inspect", inspecting.id));
            Text(new Rect(760, 180, 490, 95), inspecting.name, 31, Color.white);
            Text(new Rect(760, 290, 490, 145), inspecting.text, 20, Cyan);
            Text(new Rect(760, 450, 490, 140), inspecting.description, 18, Muted);
            if (FrontButton(new Rect(760, 660, 410, 42), "CLOSE DETAILS")) inspecting = null;
            drawingDialog = false;
        }

        private void Confirmation()
        {
            drawingDialog = true;
            Fill(new Rect(0, 0, 1600, 900), new Color(0, .01f, .02f, .88f));
            Panel(new Rect(470, 290, 660, 290), new Color(.035f, .075f, .09f), Gold);
            Text(new Rect(510, 325, 580, 120), dialogText, 20, Color.white, TextAnchor.MiddleCenter);
            if (FrontButton(new Rect(510, 483, 270, 43), "CONFIRM")) { var action = dialogAction; dialogAction = null; action(); }
            if (FrontButton(new Rect(800, 483, 270, 43), "CANCEL")) dialogAction = null;
            drawingDialog = false;
        }

        private void DrawOpponentField()
        {
            var cards = session.State.Opponent.Cards(Zone.Field);
            Text(new Rect(265, 188, 760, 25), "BOT FIELD / " + cards.Count + " CARDS     CHIRALIUM " + session.State.Opponent.AvailableQuiralium + "/" + session.State.Opponent.TotalQuiralium, 12, Muted);
            opponentPage = Mathf.Clamp(opponentPage, 0, Mathf.Max(0, (cards.Count - 1) / 5));
            for (int i = opponentPage * 5; i < Mathf.Min(cards.Count, opponentPage * 5 + 5); i++)
            {
                var card = cards[i]; var r = new Rect(310 + i % 5 * 194, 225, 122, 120);
                float angle; tapAngles.TryGetValue(card.InstanceId, out angle);
                if (Event.current.type == EventType.Repaint)
                { angle = reducedMotion ? (card.IsTapped ? 90 : 0) : Mathf.MoveTowards(angle, card.IsTapped ? 90 : 0, Time.unscaledDeltaTime * 280); tapAngles[card.InstanceId] = angle; }
                var old = GUI.matrix; RotateCanvas(angle, r.center); DrawCard(r, card, true); GUI.matrix = old;
                Text(new Rect(r.x - 20, 354, 165, 18), card.IsTapped ? "TAPPED" : card.EnteredOnTurn == session.State.Turn ? "NEW ARRIVAL" : "READY", 10, Muted, TextAnchor.MiddleCenter);
            }
            if (cards.Count == 0) Text(new Rect(500, 275, 560, 35), "The Wayfarer is preparing a route.", 14, Muted, TextAnchor.MiddleCenter);
            if (cards.Count > 5) FrontPages(ref opponentPage, cards.Count, 5, new Rect(1090, 185, 210, 28));
        }
    }
}
