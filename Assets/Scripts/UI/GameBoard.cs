using System.Collections.Generic;
using UnityEngine;
using Strands.Data;
using Strands.Game;

namespace Strands.UI
{
    // Immediate-mode rendering keeps this small prototype free of UI packages.
    // Coordinates use a 1600 x 900 canvas, letterboxed to preserve the table.
    public sealed partial class GameBoard : MonoBehaviour
    {
        private GameSession session;
        private readonly Dictionary<string, Texture2D> artwork = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, float> lift = new Dictionary<string, float>();
        private CardInstance selected;
        private sealed class Flight { public CardInstance Card; public Vector2 From, To; public float Started; }
        private readonly List<Flight> flights = new List<Flight>();
        private Zone selectedZone;
        private Zone viewedZone = Zone.Beach;
        private bool beachOpen, reducedMotion;
        private int handPage, fieldPage, beachPage;
        private int inputLayer;
        private string notice = "Select a card to convert into Chiralium, or choose to draw two.";
        private GUIStyle label;
        private Vector2 mouse;
        private static readonly Color Cyan = new Color(.39f, .83f, .89f);
        private static readonly Color Muted = new Color(.48f, .6f, .65f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<GameBoard>() == null)
                new GameObject("Strands • Game Board").AddComponent<GameBoard>();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("Table Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.015f, .025f, .035f);
                camera.cullingMask = 0;
            }
            session = new GameSession(CardCatalog.Load());
            session.CardMoved += OnCardMoved;
            InitializeFrontEnd();
        }

        private void OnCardMoved(Zone from, Zone to, CardInstance card)
        {
            if (session.IsBotTurn) return; // Keep the opponent's hand hidden.
            flights.Add(new Flight { Card = card, From = ZonePosition(from), To = ZonePosition(to), Started = Time.unscaledTime });
            notice = session.Definition(card).name + "  /  " + ZoneName(from) + " → " + ZoneName(to);
            selected = null;
        }

        private static string ZoneName(Zone zone)
        {
            switch (zone) { case Zone.Deck: return "Library"; case Zone.Hand: return "Hand";
                case Zone.Field: return "Field"; case Zone.Beach: return "Beach";
                case Zone.Quiralium: return "Chiralium"; default: return "Discard"; }
        }

        private void OnGUI()
        {
            if (session == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            Vector2 offset = new Vector2((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
            mouse = Event.current.mousePosition;
            inputLayer = 0;
            if (screen != AppScreen.Match)
            {
                DrawFrontEnd();
                GUI.matrix = Matrix4x4.identity;
                return;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                selected = null; beachOpen = false; screen = AppScreen.Menu; Event.current.Use();
                GUI.matrix = Matrix4x4.identity; return;
            }
            Background();
            Text(new Rect(46, 26, 600, 40), "S T R A N D S", 29, Color.white);
            Text(new Rect(48, 70, 650, 24), "THE SPACE BETWEEN WORLDS   /   CARD PLAYGROUND", 11, Muted);
            if (FrontButton(new Rect(1370, 28, 180, 33), "RETURN TO MENU"))
            { selected = null; beachOpen = false; screen = AppScreen.Menu; }
            Text(new Rect(1130, 72, 420, 25), session.IsBotTurn ? "BOT IS THINKING…" : "YOUR TURN", 12, Cyan, TextAnchor.MiddleRight);
            OpponentArea();
            TurnControls();
            Field();
            PlayerArea();
            DrawHand();
            Text(new Rect(300, 859, 1000, 35), notice, 13, Cyan, TextAnchor.MiddleCenter);
            Transition();
            if (beachOpen) { inputLayer = 1; Beach(); }
            if (selected != null) { inputLayer = 2; Preview(); }
            GUI.matrix = Matrix4x4.identity;
        }

        private Texture2D arenaArt, avatarArt;
        private readonly Dictionary<string, float> tapAngles = new Dictionary<string, float>();
        private static readonly Color Gold = new Color(.84f, .71f, .46f);

        private void Background()
        {
            if (arenaArt == null) arenaArt = Resources.Load<Texture2D>("Cards/structure-001");
            Fill(new Rect(0, 0, 1600, 900), new Color(.024f, .04f, .052f));
            if (arenaArt != null) GUI.DrawTexture(new Rect(0, 0, 1600, 900), arenaArt, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, 1600, 900), new Color(.012f, .025f, .035f, .72f));
            for (int i = 0; i < 10; i++)
                Panel(new Rect(205 + i * 3, 182 + i * 2, 1130 - i * 6, 457 - i * 4), new Color(.10f, .14f, .14f, .09f), new Color(.4f, .46f, .4f, .08f));
            float time = reducedMotion ? 0 : Time.unscaledTime;
            for (int i = 0; i < 80; i++)
            {
                float x = (i * 173.7f) % 1600, y = (i * 79.1f + time * (32 + i % 30)) % 900;
                Fill(new Rect(x, y, 1, 10 + i % 16), new Color(.4f, .7f, .8f, .065f));
            }
            Fill(new Rect(230, 401, 1080, 1), new Color(.54f, .73f, .69f, .4f));
            Text(new Rect(675, 386, 210, 30), "◇  THE THRESHOLD  ◇", 11, Gold, TextAnchor.MiddleCenter);
            Fill(new Rect(0, 857, 1600, 43), new Color(.015f, .025f, .03f, .94f));
        }

        private void CardBack(Rect r)
        {
            Fill(new Rect(r.x + 5, r.y + 6, r.width, r.height), new Color(0, 0, 0, .5f));
            Panel(r, new Color(.027f, .058f, .065f), Gold * .6f);
            Panel(new Rect(r.x + 6, r.y + 6, r.width - 12, r.height - 12), new Color(.045f, .09f, .1f), Muted * .4f);
            Text(r, "│\n◇\n│", 24, Gold, TextAnchor.MiddleCenter);
        }

        private void Avatar(Rect r, string name, int score, bool active)
        {
            if (avatarArt == null) avatarArt = Resources.Load<Texture2D>("Cards/porter-001");
            float pulse = reducedMotion ? 0 : (Mathf.Sin(Time.unscaledTime * 2) + 1) * .1f;
            Panel(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(.02f, .04f, .05f), active ? Gold * (1 + pulse) : Muted);
            if (avatarArt != null) GUI.DrawTexture(r, avatarArt, ScaleMode.ScaleAndCrop);
            Fill(new Rect(r.x, r.yMax - 27, r.width, 27), new Color(.015f, .025f, .03f, .95f));
            Text(new Rect(r.x, r.yMax - 27, r.width, 27), score + " PT", 19, Gold, TextAnchor.MiddleCenter);
            Text(new Rect(r.x - 100, r.yMax + 7, r.width + 200, 22), name, 11, Color.white, TextAnchor.MiddleCenter);
        }

        private void OpponentArea()
        {
            var opponent = session.State.Opponent;
            int count = Mathf.Min(7, opponent.Cards(Zone.Hand).Count);
            for (int i = 0; i < count; i++)
            {
                float delta = i - (count - 1) / 2f;
                var r = new Rect(750 + delta * 51, -48 - Mathf.Abs(delta) * 3, 60, 86);
                var old = GUI.matrix; RotateCanvas(-delta * 4, r.center); CardBack(r); GUI.matrix = old;
            }
            Avatar(new Rect(735, 53, 90, 101), "THE WAYFARER / BOT", opponent.DeliveryScore, session.IsBotTurn);
            CardBack(new Rect(1390, 113, 62, 87));
            Text(new Rect(1465, 122, 125, 65), "LIBRARY " + opponent.Cards(Zone.Deck).Count + "\nHAND " + opponent.Cards(Zone.Hand).Count, 11, Muted);
            DrawOpponentField();
        }

        private void TurnControls()
        {
            var state = session.State; var player = state.Player;
            bool preparing = state.Phase == TurnPhase.PreparingTheVoyage;
            Text(new Rect(46, 119, 220, 25), "TURN " + state.Turn.ToString("00"), 16, Gold);
            Text(new Rect(46, 151, 220, 25), (session.IsBotTurn ? "BOT / " : "YOU / ") + (preparing ? "PREPARATION" : "DELIVERY"), 12, Cyan);
            Text(new Rect(1350, 313, 210, 27), "ROUTE " + state.DeliveryDifficulty + " / " + DeliveryChanceText(), 12, Cyan, TextAnchor.MiddleCenter);
            Text(new Rect(1350, 344, 210, 25), "1d6 + " + session.EquipmentBonus(player) + " EQUIPMENT", 10, Muted, TextAnchor.MiddleCenter);
            if (Button(new Rect(1350, 386, 210, 49), preparing ? "START DELIVERY →" : "END TURN →", !preparing || state.PreparationResolved))
            {
                bool success = preparing ? session.StartDelivery() : session.EndTurn();
                notice = success ? (preparing ? "Delivery: play cards and make deliveries." : "The bot is preparing its voyage.") : session.LastError;
            }
            Text(new Rect(1350, 451, 210, 63), preparing ? (state.PreparationResolved ? "Preparation complete." : "Convert a card from your hand or draw two from the library.") : "Play cards and attempt deliveries.", 12, Muted, TextAnchor.MiddleCenter);
            Text(new Rect(46, 443, 160, 25), "CHIRALIUM", 14, Gold);
            Text(new Rect(46, 478, 160, 44), player.AvailableQuiralium + " / " + player.TotalQuiralium, 32, Color.white);
            for (int i = 0; i < Mathf.Min(8, player.TotalQuiralium); i++)
                Panel(new Rect(48 + i * 18, 538, 12, 18), i < player.AvailableQuiralium ? Gold : new Color(.08f, .1f, .12f), Gold * .5f);
            if (Button(new Rect(46, 575, 150, 31), "VIEW RESOURCES")) OpenZone(Zone.Quiralium);
            if (state.ActiveEvents.Count > 0)
                Text(new Rect(270, 353, 1020, 28), "EVENTS / " + string.Join(" • ", state.ActiveEvents), 11, Gold, TextAnchor.MiddleCenter);
        }
        private string DeliveryChanceText()
        { return (session.SuccessfulDeliveryFaces(session.State.Player) * 100f / 6).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "% success"; }

        private void OpenZone(Zone zone) { viewedZone = zone; beachPage = 0; beachOpen = true; }

        private void Field()
        {
            var cards = session.State.Player.Cards(Zone.Field);
            Text(new Rect(265, 415, 620, 25), "01   /   YOUR FIELD     —     " + cards.Count.ToString("00") + " CARDS", 12, Cyan);
            if (cards.Count == 0)
            {
                Panel(new Rect(440, 464, 700, 140), new Color(.035f, .062f, .072f, .7f), new Color(.1f, .19f, .22f));
                Text(new Rect(450, 470, 700, 48), "＋", 32, Cyan, TextAnchor.MiddleCenter);
                Text(new Rect(450, 524, 700, 26), "Every connection begins with a delivery.", 17, new Color(.68f, .75f, .76f), TextAnchor.MiddleCenter);
                Text(new Rect(450, 559, 700, 24), "Click a card in your hand to play it onto the field", 12, Muted, TextAnchor.MiddleCenter);
            }
            fieldPage = Mathf.Clamp(fieldPage, 0, Mathf.Max(0, (cards.Count - 1) / 5));
            for (int i = fieldPage * 5; i < Mathf.Min(cards.Count, fieldPage * 5 + 5); i++)
            {
                var card = cards[i];
                var r = new Rect(310 + (i % 5) * 194, 464, 122, 139);
                Matrix4x4 old = GUI.matrix;
                float angle; tapAngles.TryGetValue(card.InstanceId, out angle);
                if (Event.current.type == EventType.Repaint)
                { angle = reducedMotion ? (card.IsTapped ? 90 : 0) : Mathf.MoveTowards(angle, card.IsTapped ? 90 : 0, Time.unscaledDeltaTime * 280); tapAngles[card.InstanceId] = angle; }
                RotateCanvas(angle, r.center);
                DrawCard(r, card, true);
                GUI.matrix = old;
                var hitRect = card.IsTapped ? new Rect(r.center.x - r.height / 2, r.center.y - r.width / 2, r.height, r.width) : r;
                if (!beachOpen && selected == null && Hit(hitRect)) { selected = card; selectedZone = Zone.Field; }
                string status = card.IsTapped ? "TAPPED / EXPOSED" : card.EnteredOnTurn == session.State.Turn ? "NEW ARRIVAL" : "READY";
                Text(new Rect(r.x - 26, 613, 180, 20), status, 10, card.IsTapped ? new Color(.86f, .66f, .4f) : Muted, TextAnchor.MiddleCenter);
            }
            if (cards.Count > 5) Pagination(ref fieldPage, cards.Count, 5, new Rect(1060, 415, 230, 30));
        }

        private void PlayerArea()
        {
            var player = session.State.Player;
            Text(new Rect(36, 631, 170, 25), "BEACH / " + player.Cards(Zone.Beach).Count, 14, Cyan, TextAnchor.MiddleCenter);
            var r = new Rect(78, 668, 83, 116);
            var beach = player.Cards(Zone.Beach);
            if (beach.Count > 0) DrawCard(r, beach[beach.Count - 1]); else CardBack(r);
            if (Button(new Rect(36, 800, 170, 32), "EXPLORE BEACH")) OpenZone(Zone.Beach);
            for (int i = 3; i >= 0; i--) CardBack(new Rect(1403 + i * 3, 545 - i * 3, 83, 116));
            Text(new Rect(1350, 674, 210, 25), "LIBRARY / " + player.Cards(Zone.Deck).Count, 14, Cyan, TextAnchor.MiddleCenter);
            bool canChoose = session.State.Phase == TurnPhase.PreparingTheVoyage && !session.State.PreparationResolved;
            if (Button(new Rect(1350, 710, 210, 43), "SKIP CONVERSION / DRAW 2", canChoose))
            {
                int before = player.Cards(Zone.Hand).Count;
                session.SkipConversion();
                notice = "No conversion: " + (player.Cards(Zone.Hand).Count - before) + " card(s) drawn.";
            }
            if (Button(new Rect(1350, 764, 101, 29), "SHUFFLE")) { session.ShuffleDeck(); notice = "Library shuffled."; }
            if (Button(new Rect(1457, 764, 103, 29), "DISCARD " + player.Cards(Zone.Discard).Count)) OpenZone(Zone.Discard);
            if (Button(new Rect(1350, 803, 210, 29), reducedMotion ? "MOTION: REDUCED" : "MOTION: SMOOTH")) reducedMotion = !reducedMotion;
            Avatar(new Rect(740, 638, 80, 88), "COURIER", player.DeliveryScore, !session.IsBotTurn);
            Text(new Rect(36, 865, 250, 25), "● LOCAL / SOLO VS BOT", 11, Muted);
            Text(new Rect(1350, 865, 210, 25), "HAND " + player.Cards(Zone.Hand).Count + " / FIELD " + player.Cards(Zone.Field).Count, 11, Muted);
        }
        private void DrawHand()
        {
            var cards = session.State.Player.Cards(Zone.Hand);
            handPage = Mathf.Clamp(handPage, 0, Mathf.Max(0, (cards.Count - 1) / 9));
            int start = handPage * 9, count = Mathf.Min(9, cards.Count - start), hovered = -1;
            float spacing = Mathf.Min(118, 850f / Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                var r = HandRect(i, count, spacing);
                float raised; lift.TryGetValue(cards[start + i].InstanceId, out raised);
                r.y -= raised * 156; r.height += raised * 65;
                if (r.Contains(mouse)) hovered = i;
            }
            if (selected != null || beachOpen || session.IsBotTurn) hovered = -1;
            for (int pass = 0; pass < 2; pass++) for (int i = 0; i < count; i++)
            {
                if ((i == hovered) != (pass == 1)) continue;
                var card = cards[start + i];
                float value; lift.TryGetValue(card.InstanceId, out value);
                if (Event.current.type == EventType.Repaint)
                { value = reducedMotion ? (i == hovered ? 1 : 0) : Mathf.MoveTowards(value, i == hovered ? 1 : 0, Time.unscaledDeltaTime * 7); lift[card.InstanceId] = value; }
                var r = HandRect(i, count, spacing); r.y -= value * 156; r.x -= value * 22; r.width += value * 44; r.height += value * 65;
                Matrix4x4 old = GUI.matrix;
                RotateCanvas((i - (count - 1) / 2f) * 3 * (1 - value), new Vector2(r.center.x, r.yMax));
                DrawCard(r, card);
                GUI.matrix = old;
                if (i == hovered && Hit(r)) { selected = card; selectedZone = Zone.Hand; }
            }
            if (cards.Count == 0) Text(new Rect(460, 724, 680, 50), "Empty hand. Draw cards during turn preparation.", 16, Muted, TextAnchor.MiddleCenter);
            if (cards.Count > 9) Pagination(ref handPage, cards.Count, 9, new Rect(1080, 693, 230, 30));
        }

        private Rect HandRect(int i, int count, float spacing)
        { float delta = i - (count - 1) / 2f; return new Rect(800 + delta * spacing - 70, 753 + Mathf.Abs(delta) * 7, 140, 197); }

        private void Beach()
        {
            Fill(new Rect(0, 0, 1600, 900), new Color(0, .015f, .025f, .94f));
            Panel(new Rect(170, 120, 1260, 660), new Color(.035f, .071f, .086f), new Color(.18f, .42f, .46f));
            Text(new Rect(220, 150, 800, 45), ZoneName(viewedZone).ToUpperInvariant() + "  /  " + (viewedZone == Zone.Beach ? "DRIFTING MEMORIES" : viewedZone == Zone.Quiralium ? "PERMANENT CONNECTIONS" : "RESOLVED EVENTS"), 25, Cyan);
            string hint = viewedZone == Zone.Beach ? "Select a card to inspect. Returning cards manually remains available as a playground tool." : viewedZone == Zone.Quiralium ? "Each card here provides 1 permanent Chiralium. Spent resources regenerate next turn." : "Events go to the discard pile after affecting both fields.";
            Text(new Rect(220, 207, 1000, 40), hint, 15, Muted);
            if (Button(new Rect(1260, 152, 120, 35), "CLOSE ×")) beachOpen = false;
            for (int i = 0; i < 22; i++) Fill(new Rect(200, 400 + i * 15, 1200, 1), new Color(.25f, .52f, .57f, .08f));
            var cards = session.State.Player.Cards(viewedZone);
            beachPage = Mathf.Clamp(beachPage, 0, Mathf.Max(0, (cards.Count - 1) / 6));
            for (int i = beachPage * 6; i < Mathf.Min(cards.Count, beachPage * 6 + 6); i++)
            {
                var r = new Rect(220 + i % 6 * 194, 315, 170, 246); DrawCard(r, cards[i]);
                if (selected == null && Hit(r)) { selected = cards[i]; selectedZone = viewedZone; }
            }
            if (cards.Count == 0) Text(new Rect(300, 350, 1000, 200), "No cards in this zone.", 23, Muted, TextAnchor.MiddleCenter);
            Pagination(ref beachPage, cards.Count, 6, new Rect(1110, 706, 260, 35));
            Text(new Rect(220, 710, 750, 30), cards.Count + " CARDS  /  " + ZoneName(viewedZone), 13, Cyan);
        }

        private void Preview()
        {
            Fill(new Rect(0, 0, 1600, 900), new Color(.005f, .01f, .016f, .94f));
            Panel(new Rect(390, 90, 820, 730), new Color(.035f, .06f, .076f), new Color(.2f, .37f, .41f));
            var card = selected;
            var definition = session.Definition(card);
            DrawCard(new Rect(425, 155, 300, 435), card, selectedZone == Zone.Field);
            Text(new Rect(425, 616, 300, 100), definition.description ?? "", 17, Muted);
            Text(new Rect(765, 122, 410, 28), ZoneName(selectedZone).ToUpperInvariant() + "  /  " + (definition.rarity ?? "Common").ToUpperInvariant(), 12, Cyan);
            Text(new Rect(765, 164, 405, 87), definition.name, 29, Color.white);
            Text(new Rect(765, 256, 405, 40), "COST " + definition.cost + "    ATTACK " + (selectedZone == Zone.Field ? session.AttackValue(card) : definition.power) + "    DEFENSE " + (selectedZone == Zone.Field ? session.RemainingDefense(card) : definition.defense), 14, Cyan);
            Text(new Rect(765, 299, 405, 30), "DELIVERY  /  " + (selectedZone == Zone.Field ? session.DeliveryValue(card) : definition.deliveryPoints) + " POINTS", 17, Cyan);
            Text(new Rect(765, 344, 405, 102), definition.text, 18, new Color(.75f, .82f, .83f));
            string id = card.InstanceId;
            if (selectedZone == Zone.Hand)
            {
                string reason = session.PlayBlockReason(id);
                Text(new Rect(765, 455, 405, 48), reason ?? "Chiralium available. This card can be played.", 13, Muted);
                string action = definition.type == CardType.Event ? "PLAY GLOBAL EVENT" : "PLAY ONTO FIELD";
                if (Button(new Rect(765, 520, 405, 45), action + "  /  " + definition.cost + " CH", reason == null))
                {
                    session.PlayCard(id); notice = definition.name + " played. " + (definition.type == CardType.Event ? "Global effect resolved." : "Wait until your next turn to activate."); return;
                }
                string conversionReason = session.ConversionBlockReason(id);
                if (Button(new Rect(765, 584, 405, 45), "CONVERT TO CHIRALIUM + DRAW 1", conversionReason == null))
                {
                    int before = session.State.Player.Cards(Zone.Deck).Count;
                    session.ConvertToQuiralium(id);
                    notice = "+1 permanent Chiralium. " + (before - session.State.Player.Cards(Zone.Deck).Count) + " card(s) drawn."; return;
                }
                Text(new Rect(765, 644, 405, 50), conversionReason ?? "This card leaves your hand and becomes a permanent resource.", 12, Muted);
            }
            else if (selectedZone == Zone.Field)
            {
                string reason = session.ActivationBlockReason(id);
                string deliveryInfo = "1d6 + " + session.EquipmentBonus(session.State.Player) + " ≥ " + session.State.DeliveryDifficulty + " / " + DeliveryChanceText();
                if (definition.type == CardType.Equipment)
                    deliveryInfo = "Passive bonus while on the field; tapping is not required.";
                Text(new Rect(765, 451, 405, 58), (reason ?? "Ready to attempt a delivery.") + "\n" + deliveryInfo, 13, Muted);
                if (Button(new Rect(765, 520, 405, 45), "ATTEMPT DELIVERY  /  " + session.DeliveryValue(card) + " POINTS", reason == null))
                {
                    if (!session.ActivateCard(id)) { notice = session.LastError; return; }
                    var result = session.State.LastDelivery;
                    selected = null;
                    notice = (result.Success ? "DELIVERY COMPLETE" : "DELIVERY FAILED") + " / roll " + result.Roll + " + " + result.Bonus + " vs difficulty " + result.Difficulty + " / +" + result.Points + " points.";
                    return;
                }
                if (Button(new Rect(765, 601, 405, 37), "PLAYGROUND: SEND TO BEACH")) { session.SendToBeach(id); return; }
                Text(new Rect(765, 650, 405, 43), "Lethal damage automatically sends cards to the Beach. Combat is not implemented yet.", 12, Muted);
            }
            else if (selectedZone == Zone.Beach)
            {
                Text(new Rect(765, 460, 405, 50), "Memory preserved. Returning cards manually is a testing tool.", 13, Muted);
                if (Button(new Rect(765, 554, 405, 45), "PLAYGROUND: RETURN TO HAND")) { session.ReturnFromBeach(id); return; }
            }
            else Text(new Rect(765, 460, 405, 90), selectedZone == Zone.Quiralium ? "This copy is now a permanent Chiralium resource." : "Event resolved. This copy is in the discard pile.", 17, Muted);
            if (Button(new Rect(765, 740, 405, 38), "CLOSE DETAILS")) selected = null;
        }

        private static Vector2 ZonePosition(Zone zone)
        {
            switch (zone)
            {
                case Zone.Deck: return new Vector2(1444, 603);
                case Zone.Field: return new Vector2(780, 530);
                case Zone.Beach: return new Vector2(119, 726);
                case Zone.Quiralium: return new Vector2(120, 530);
                case Zone.Discard: return new Vector2(1508, 778);
                default: return new Vector2(780, 810);
            }
        }

        private void Transition()
        {
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var flight = flights[i];
                float elapsed = Time.unscaledTime - flight.Started;
                if (elapsed >= .65f || reducedMotion) { flights.RemoveAt(i); continue; }
                float t = Mathf.SmoothStep(0, 1, elapsed / .65f);
                Vector2 p = Vector2.Lerp(flight.From, flight.To, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * 85;
                var old = GUI.color; GUI.color = new Color(1, 1, 1, 1 - t * .6f);
                DrawCard(new Rect(p.x - 50, p.y - 70, 100, 140), flight.Card); GUI.color = old;
            }
        }

        private void DrawCard(Rect r, CardInstance instance, bool onField = false)
        {
            var d = session.Definition(instance);
            Color accent = d.type == CardType.Entity ? new Color(.59f, .63f, .82f) : d.rarity == "Legendary" ? new Color(.78f, .65f, .4f) : Cyan;
            Fill(new Rect(r.x + 5, r.y + 7, r.width, r.height), new Color(0, 0, 0, .6f));
            Panel(r, new Color(.065f, .089f, .1f), accent * .65f);
            if (onField && r.width < 200)
            {
                GUI.DrawTexture(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), Art(d), ScaleMode.ScaleAndCrop);
                Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, 29), new Color(.015f, .025f, .03f, .9f));
                Text(new Rect(r.x + 9, r.y + 7, r.width - 18, 27), d.name, 10, Color.white);
                Panel(new Rect(r.xMax - 57, r.yMax - 29, 55, 27), new Color(.025f, .055f, .06f), accent);
                Text(new Rect(r.xMax - 57, r.yMax - 29, 55, 27), session.AttackValue(instance) + " / " + session.RemainingDefense(instance), 15, Color.white, TextAnchor.MiddleCenter);
                return;
            }
            var art = new Rect(r.x + 5, r.y + 5, r.width - 10, r.height * .49f);
            GUI.DrawTexture(art, Art(d), ScaleMode.ScaleAndCrop);
            Fill(new Rect(r.x + 9, r.y + 9, r.width * .18f, r.width * .18f), new Color(.015f, .035f, .05f, .9f));
            Text(new Rect(r.x + 9, r.y + 9, r.width * .18f, r.width * .18f), d.cost.ToString(), Mathf.RoundToInt(r.width * .105f), accent, TextAnchor.MiddleCenter);
            float x = r.x + 9, w = r.width - 18;
            Text(new Rect(x, r.y + r.height * .5f, w, r.height * .15f), d.name, Mathf.RoundToInt(r.width * .092f), Color.white);
            Text(new Rect(x, r.y + r.height * .655f, w, r.height * .065f), d.type.ToString().ToUpperInvariant() + " / " + d.faction, Mathf.RoundToInt(r.width * .051f), accent);
            Text(new Rect(x, r.y + r.height * .735f, w, r.height * .17f), d.text, Mathf.RoundToInt(r.width * .063f), new Color(.68f, .76f, .77f));
            Text(new Rect(x, r.y + r.height * .918f, w, r.height * .06f), "ATK " + (onField ? session.AttackValue(instance) : d.power) + "  DEF " + (onField ? session.RemainingDefense(instance) : d.defense) + "  DEL " + (onField ? session.DeliveryValue(instance) : d.deliveryPoints), Mathf.RoundToInt(r.width * .057f), accent);
        }

        private Texture2D Art(CardDefinition d)
        {
            Texture2D texture;
            if (artwork.TryGetValue(d.id, out texture)) return texture;
            if (!string.IsNullOrEmpty(d.image)) texture = Resources.Load<Texture2D>(d.image);
            if (texture == null)
            {
                texture = new Texture2D(192, 144, TextureFormat.RGB24, false);
                int seed = 0; foreach (char c in d.id) seed = (seed * 31 + c) & 65535;
                var pixels = new Color[192 * 144];
                for (int y = 0; y < 144; y++) for (int x = 0; x < 192; x++)
                {
                    float u = x / 192f, v = y / 144f, noise = Mathf.PerlinNoise(u * 5 + seed % 71, v * 5);
                    Color top = d.type == CardType.Entity ? new Color(.28f, .29f, .39f) : new Color(.22f, .38f, .42f);
                    Color color = Color.Lerp(new Color(.025f, .052f, .065f), top, v) * (.7f + noise * .4f);
                    float ridge = .26f + Mathf.PerlinNoise(u * 6 + seed % 17, 0) * .22f;
                    if (v < ridge) color *= .36f;
                    bool silhouette = false;
                    if (d.type == CardType.Porter || d.type == CardType.Entity)
                    {
                        float cx = .52f + (d.type == CardType.Entity ? Mathf.Sin(v * 15) * .028f : 0);
                        silhouette = (Mathf.Abs(u - cx) < .08f * (1.15f - v) && v > .18f && v < .63f) || ((u - cx) * (u - cx) + (v - .69f) * (v - .69f) < .003f);
                        if (d.type == CardType.Porter && u > .56f && u < .66f && v > .35f && v < .57f) silhouette = true;
                    }
                    else if (d.type == CardType.Structure) silhouette = u > .22f && u < .79f && v > .23f && v < .56f + Mathf.Floor(u * 8) % 2 * .11f;
                    else if (d.type == CardType.Equipment) silhouette = Mathf.Abs(u - .5f) + Mathf.Abs(v - .5f) < .24f;
                    else if (Mathf.Abs(v - .5f - Mathf.Sin(u * 8) * .08f) < .015f) color = Cyan * .8f;
                    if (silhouette) color = new Color(.015f, .023f, .03f);
                    if ((x + y * 3 + seed) % 73 == 0) color += new Color(.05f, .07f, .08f);
                    pixels[y * 192 + x] = color;
                }
                texture.SetPixels(pixels); texture.Apply();
            }
            artwork[d.id] = texture; return texture;
        }

        private void Pagination(ref int page, int total, int size, Rect r)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)size));
            if (Button(new Rect(r.x, r.y, 44, r.height), "←", page > 0)) page--;
            Text(new Rect(r.x + 48, r.y, r.width - 96, r.height), (page + 1) + " / " + pages, 12, Muted, TextAnchor.MiddleCenter);
            if (Button(new Rect(r.xMax - 44, r.y, 44, r.height), "→", page < pages - 1)) page++;
        }

        private static void Fill(Rect rect, Color color)
        { Color old = GUI.color; GUI.color = old * color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
        private static void RotateCanvas(float degrees, Vector2 pivot)
        {
            // Rotate in logical canvas coordinates, before applying its screen scale.
            GUI.matrix = GUI.matrix * Matrix4x4.Translate(pivot)
                * Matrix4x4.Rotate(Quaternion.Euler(0, 0, degrees)) * Matrix4x4.Translate(-pivot);
        }
        private static void Panel(Rect r, Color background, Color border)
        { Fill(r, border); Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), background); }
        private void Text(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
        { label.fontSize = Mathf.Max(8, size); label.normal.textColor = color; label.alignment = align; GUI.Label(r, text, label); }
        private bool Button(Rect r, string text, bool enabled = true)
        {
            bool blocked = session.IsBotTurn || inputLayer != (selected != null ? 2 : beachOpen ? 1 : 0);
            bool hover = r.Contains(mouse) && enabled;
            Panel(r, hover ? new Color(.1f, .23f, .26f) : new Color(.055f, .11f, .13f), enabled ? new Color(.19f, .39f, .43f) : new Color(.1f, .15f, .17f));
            Text(r, text, 11, enabled ? Cyan : Muted, TextAnchor.MiddleCenter);
            return enabled && !blocked && Hit(r);
        }
        private bool Hit(Rect r)
        {
            if (screen == AppScreen.Match && session.IsBotTurn) return false;
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use(); return true;
        }
    }
}

