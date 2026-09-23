using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Strands.Data;
using Strands.Game;
using Strands.UI;

namespace Strands.Editor
{
    // Batch-only integration check. It never runs during ordinary Editor use.
    [InitializeOnLoad]
    public static class PlayModeSmoke
    {
        private const string Key = "Strands.Smoke.Active";
        private static double started;
        private static bool failed;
        private static int step;
        private static EditorWindow gameView;

        static PlayModeSmoke()
        {
            if (!Application.isBatchMode || !SessionState.GetBool(Key, false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batchmode for this check.");
            ProjectSetup.EnsureScene(); ProjectSetup.ValidateCore();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Playground.unity");
            gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 45) { Finish(false, "Play mode timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup - started < 3) return;
            try
            {
                var board = UnityEngine.Object.FindFirstObjectByType<GameBoard>();
                if (board == null) throw new Exception("GameBoard did not initialize.");
                if (gameView == null) gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                gameView.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                if (step == 0)
                {
                    foreach (var card in CardCatalog.Load())
                        if (!string.IsNullOrEmpty(card.image) && Resources.Load<Texture2D>(card.image) == null)
                            throw new Exception("Artwork missing: " + card.id);
                    if (Resources.Load<Texture2D>("Menu/shore") == null || Resources.Load<TextAsset>("Rules") == null)
                        throw new Exception("Menu artwork or rules missing.");
                    var saved = DeckStorage.Load();
                    var profile = JsonUtility.FromJson<DeckBook>(JsonUtility.ToJson(saved));
                    var copy = profile.Duplicate(profile.decks[0].id);
                    copy.name = "Persistence smoke check";
                    DeckStorage.Save(profile);
                    if (DeckStorage.Load().Find(copy.id).name != copy.name) throw new Exception("Deck persistence failed.");
                    profile.Delete(copy.id); DeckStorage.Save(profile);
                    if (DeckStorage.Load().Find(copy.id) != null) throw new Exception("Deck deletion persistence failed.");
                    Capture("TestResults/main-menu.bmp");
                    SetScreen(board, "Decks"); Capture("TestResults/deck-builder.bmp");
                    SetScreen(board, "Rules"); Capture("TestResults/rules.bmp");
                    SetScreen(board, "Match");
                    Capture("TestResults/table-preparation.bmp");
                    var field = typeof(GameBoard).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic);
                    var session = (GameSession)field.GetValue(board);
                    for (int i = 0; i < 4; i++)
                    {
                        session.ConvertToQuiralium(session.State.Player.Cards(Zone.Hand)[0].InstanceId);
                        session.StartDelivery(); session.EndTurn();
                    }
                    session.SkipConversion(); session.StartDelivery();
                    CardInstance unit = null;
                    foreach (var card in session.State.Player.Cards(Zone.Hand))
                        if (session.Definition(card).deliveryPoints > 0 && session.PlayBlockReason(card.InstanceId) == null) { unit = card; break; }
                    if (unit == null || !session.PlayCard(unit.InstanceId) || session.ActivateCard(unit.InstanceId))
                        throw new Exception("Arrival scenario failed.");
                    session.EndTurn(); session.SkipConversion(); session.StartDelivery();
                    if (!session.ActivateCard(unit.InstanceId)) throw new Exception("Delivery scenario failed.");
                    step = 1; started = EditorApplication.timeSinceStartup;
                }
                else if (step == 1)
                {
                    Capture("TestResults/table-delivery.bmp");
                    var session = (GameSession)typeof(GameBoard).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
                    typeof(GameBoard).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(board, session.State.Player.Cards(Zone.Field)[0]);
                    typeof(GameBoard).GetField("selectedZone", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(board, Zone.Field);
                    step = 2; started = EditorApplication.timeSinceStartup;
                }
                else if (step == 2)
                {
                    Capture("TestResults/card-preview.bmp");
                    CardWorkshop.Open();
                    var workshop = EditorWindow.GetWindow<CardWorkshop>();
                    workshop.SendEvent(new Event { type = EventType.Layout });
                    workshop.SendEvent(new Event { type = EventType.Repaint });
                    workshop.Close();
                    typeof(GameBoard).GetMethod("StartBotMatch", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(board, null);
                    var session = (GameSession)typeof(GameBoard).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(board);
                    session.SkipConversion(); session.StartDelivery(); session.EndTurn();
                    var bot = new SoloBot(); int actions = 0;
                    while (session.IsBotTurn && actions++ < 150) bot.Step(session);
                    if (session.IsBotTurn) throw new Exception("Bot did not finish its turn.");
                    step = 3; started = EditorApplication.timeSinceStartup;
                }
                else { Capture("TestResults/bot-field.bmp"); Finish(!failed, "Menu, decks, rules, persistence, delivery and bot scenarios passed; frames captured."); }
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }

        private static void SetScreen(GameBoard board, string name)
        {
            var field = typeof(GameBoard).GetField("screen", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(board, Enum.Parse(field.FieldType, name));
        }

        private static void Capture(string path)
        {
            // GameView's off-screen render texture works even when the Editor is hidden.
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var size = gameView.GetType().GetProperty("targetSize", flags);
            if (size != null) size.SetValue(gameView, new Vector2(1600, 900), null);
            gameView.SendEvent(new Event { type = EventType.Repaint });
            var render = gameView.GetType().GetMethod("RenderView", flags);
            var previousEvent = Event.current;
            RenderTexture target;
            try
            {
                Event.current = new Event { type = EventType.Repaint };
                target = render == null ? null : render.Invoke(gameView, new object[] { new Vector2(-100, -100), false }) as RenderTexture;
            }
            finally { Event.current = previousEvent; }
            if (target == null) throw new Exception("GameView did not produce a frame.");
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                bool varied = false;
                for (int i = 1; i < pixels.Length; i += 97)
                    if (!pixels[i].Equals(pixels[0])) { varied = true; break; }
                if (!varied) throw new Exception("GameView capture is blank; visual verification unavailable.");
                int stride = (target.width * 3 + 3) & ~3;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write((ushort)0x4D42); writer.Write(54 + stride * target.height);
                    writer.Write(0); writer.Write(54); writer.Write(40);
                    // The GameView render target has a top-first orientation on D3D.
                    writer.Write(target.width); writer.Write(SystemInfo.graphicsUVStartsAtTop ? -target.height : target.height);
                    writer.Write((ushort)1); writer.Write((ushort)24);
                    writer.Write(0); writer.Write(stride * target.height);
                    writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                    for (int y = 0; y < target.height; y++)
                    {
                        for (int x = 0; x < target.width; x++)
                        { var color = pixels[y * target.width + x]; writer.Write(color.b); writer.Write(color.g); writer.Write(color.r); }
                        for (int i = target.width * 3; i < stride; i++) writer.Write((byte)0);
                    }
                }
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Debug.Log((success ? "SMOKE PASS: " : "SMOKE FAIL: ") + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
