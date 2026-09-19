using System;
using System.Collections;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Palimpseste.Game.Drawing;
using Palimpseste.Game.Library;
using Palimpseste.Game.Service;
using Palimpseste.Game.SpellRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Palimpseste.Game.Bootstrap
{
    public sealed class PalimpsesteApp : MonoBehaviour
    {
        private enum Page { Library, Drawing, Processing, Card, Lab }
        private static PalimpsesteApp instance;
        private readonly LabApi api = new LabApi();
        private ParchmentStore store;
        private System.Collections.Generic.List<ParchmentRecord> records;
        private ParchmentRecord selected;
        private DrawingCanvas canvas;
        private Texture2D reference;
        private byte[] referenceBytes;
        private string serviceUrl;
        private string token = "";
        private string notice = "Connectez le laboratoire pour créer un parchemin. Les sorts enregistrés restent disponibles hors ligne.";
        private Page page;
        private BrushStyle brush = BrushStyle.Solid;
        private int inkIndex;
        private float diameter = 14;
        private readonly Color32[] inks = { new Color32(125, 39, 31, 220), new Color32(30, 92, 132, 220), new Color32(74, 66, 49, 235), new Color32(80, 84, 103, 205) };
        private readonly string[] inkNames = { "Braise", "Eau", "Pierre", "Souffle" };
        private Rect paperRect;
        private bool busy;
        private string spellJson;
        private SpellLab lab;
        private GUIStyle panelStyle;
        private GUIStyle libraryPanelStyle;
        private Texture2D labHudBackground;
        private Texture2D hostileSwatch;
        private Texture2D allySwatch;
        private Texture2D objectSwatch;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;
        private Texture2D libraryBackdrop;
        private Texture2D goldBar;
        private Texture2D capturePreview;
        private string capturePreviewPath;
        private Vector2 libraryScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartApp()
        {
            if (instance != null) return;
            var obj = new GameObject("Palimpseste App");
            instance = obj.AddComponent<PalimpsesteApp>();
            DontDestroyOnLoad(obj);
        }

        private void Awake()
        {
            store = new ParchmentStore();
            records = store.LoadAll();
            libraryBackdrop = Resources.Load<Texture2D>("LibraryBackdrop");
            serviceUrl = PlayerPrefs.GetString("palimpseste.lab_url", "http://127.0.0.1:8080");
            if (WindowsCredentialStore.TryRead(serviceUrl, out token, out _))
                Debug.Log("PALIMPSESTE_CREDENTIAL_READ_OK");
            if (token == null) token = "";
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == "SpellLab") page = Page.Lab;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (capturePreview != null) Destroy(capturePreview);
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "SpellLab" && page == Page.Lab) BeginLab();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus || page != Page.Drawing || canvas == null || !canvas.IsDrawing) return;
            Append("up", Vector2.zero);
            canvas.End();
            notice = "Geste interrompu et enregistré. Les régions touchées sont verrouillées.";
            if (canvas.Closed) FinalizeDrawing("all_regions_locked");
        }

        private void OnApplicationQuit()
        {
            if (page != Page.Drawing || canvas == null || selected == null || !canvas.Engaged) return;
            if (canvas.IsDrawing) { Append("up", Vector2.zero); canvas.End(); }
            FinalizeDrawing("window_closed");
        }

        private void InitializeStyles()
        {
            if (panelStyle != null) return;
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = Solid(new Color32(24, 28, 36, 245)) }, padding = new RectOffset(16, 16, 12, 12) };
            libraryPanelStyle = new GUIStyle(panelStyle) { normal = { background = Solid(new Color32(23, 27, 33, 226)) } };
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, .88f, .67f) } };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = new Color(.91f, .89f, .83f) } };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, padding = new RectOffset(12, 12, 8, 8) };
            goldBar = Solid(new Color32(215, 166, 102, 255));
            labHudBackground = Solid(new Color32(12, 20, 27, 231));
            hostileSwatch = Solid(new Color32(215, 94, 76, 255));
            allySwatch = Solid(new Color32(75, 192, 196, 255));
            objectSwatch = Solid(new Color32(188, 146, 86, 255));
        }

        private static Texture2D Solid(Color32 color)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, color);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            InitializeStyles();
            if (page == Page.Library && libraryBackdrop != null)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), libraryBackdrop, ScaleMode.ScaleAndCrop);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, 100), labHudBackground);
            }
            else if (page != Page.Lab)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), SolidBackground, ScaleMode.StretchToFill);
            GUI.Label(new Rect(28, 15, Screen.width - 50, 42), "PALIMPSESTE", titleStyle);
            GUI.Label(new Rect(30, 56, Screen.width - 60, 44), notice, textStyle);
            switch (page)
            {
                case Page.Library: DrawLibrary(); break;
                case Page.Drawing: DrawDrawing(); break;
                case Page.Processing: DrawProcessing(); break;
                case Page.Card: DrawCard(); break;
                case Page.Lab: DrawLabHud(); break;
            }
        }

        private Texture2D solidBackground;
        private Texture2D SolidBackground => solidBackground ?? (solidBackground = Solid(new Color32(17, 22, 30, 255)));

        private void DrawLibrary()
        {
            var width = Mathf.Min(930, Screen.width - 40);
            var x = (Screen.width - width) / 2f;
            GUI.Box(new Rect(x, 108, width, Screen.height - 132), GUIContent.none, libraryPanelStyle);
            GUI.DrawTexture(new Rect(x + 20, 157, 115, 3), goldBar);
            GUI.Label(new Rect(x + 20, 122, width - 40, 32), "Bibliothèque des parchemins", titleStyle);
            GUI.Label(new Rect(x + 20, 174, 110, 30), "Service", textStyle);
            var editedUrl = GUI.TextField(new Rect(x + 130, 174, width - 470, 32), serviceUrl);
            if (editedUrl != serviceUrl)
            {
                serviceUrl = editedUrl;
                api.Clear();
                WindowsCredentialStore.TryRead(serviceUrl, out token, out _);
                if (token == null) token = "";
            }
            GUI.Label(new Rect(x + 20, 214, 110, 30), "Jeton privé", textStyle);
            token = GUI.PasswordField(new Rect(x + 130, 214, width - 470, 32), token, '•');
            if (GUI.Button(new Rect(x + width - 310, 174, 280, 72), busy ? "Connexion…" : "Connecter", buttonStyle) && !busy) StartCoroutine(Connect());
            if (GUI.Button(new Rect(x + 20, 272, 280, 42), "Nouveau parchemin", buttonStyle) && !busy) StartCoroutine(NewParchment());
            if (GUI.Button(new Rect(x + 315, 272, 185, 42), "Oublier le jeton", buttonStyle) && !busy)
            {
                if (WindowsCredentialStore.TryDelete(serviceUrl, out var deleteError))
                {
                    token = "";
                    api.Clear();
                    notice = "Jeton protégé effacé. Les sorts locaux restent accessibles.";
                    Debug.Log("PALIMPSESTE_CREDENTIAL_DELETE_OK");
                }
                else notice = "Effacement du jeton impossible : " + deleteError;
            }
            GUI.Label(new Rect(x + 20, 327, width - 40, 26), "Créations locales", textStyle);
            var guideX = x + width * .59f;
            var guideWidth = width * .38f;
            GUI.Box(new Rect(guideX, 270, guideWidth, Mathf.Min(300, Screen.height - 405)), GUIContent.none);
            GUI.Label(new Rect(guideX + 18, 285, guideWidth - 36, 36), "Votre parcours", titleStyle);
            GUI.DrawTexture(new Rect(guideX + 18, 325, guideWidth - 36, 2), goldBar);
            GUI.Label(new Rect(guideX + 18, 340, guideWidth - 36, 44), "01  Dessiner sur trois régions", textStyle);
            GUI.Label(new Rect(guideX + 18, 390, guideWidth - 36, 44), "02  Transmettre votre trace", textStyle);
            GUI.Label(new Rect(guideX + 18, 440, guideWidth - 36, 48), "03  Essayer le sort dans le labo", textStyle);
            GUI.Label(new Rect(guideX + 18, 510, guideWidth - 36, 50), "Sorts téléchargés : accessibles hors ligne.", textStyle);
            var listWidth = width * .55f;
            var viewport = new Rect(x + 20, 365, listWidth, Mathf.Max(90, Screen.height - 413));
            var contentWidth = listWidth - 18;
            var contentHeight = Mathf.Max(viewport.height, records.Count * 66 + 4);
            libraryScroll = GUI.BeginScrollView(viewport, libraryScroll,
                new Rect(0, 0, contentWidth, contentHeight));
            if (records.Count == 0)
                GUI.Label(new Rect(4, 5, contentWidth - 8, 72), "La bibliothèque est vide. Créez un parchemin pour tracer votre premier sort.", textStyle);
            for (var i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var top = i * 66f;
                GUI.Box(new Rect(0, top, contentWidth - 2, 57), GUIContent.none);
                GUI.DrawTexture(new Rect(0, top + 1, 4, 55), goldBar);
                var label = (!string.IsNullOrEmpty(r.spell_id) ? "Sort disponible" : r.state == "capture_corrupted" ? "Journal endommagé" : r.state == "capture_pending" ? "En attente de transmission" : r.state == "writing" ? "Encré" : r.state == "blank" ? "Vierge" : "Traitement : " + r.state);
                GUI.Label(new Rect(14, top + 7, contentWidth - 140, 45), label + "\n" + r.created_at, textStyle);
                if (GUI.Button(new Rect(contentWidth - 111, top + 8, 106, 39), "Ouvrir", buttonStyle)) Open(r);
            }
            GUI.EndScrollView();
        }

        private void DrawDrawing()
        {
            if (canvas == null || selected == null) { page = Page.Library; return; }
            var side = Mathf.Min(Screen.height - 185, Screen.width - 410);
            side = Mathf.Max(240, side);
            paperRect = new Rect(26, 125, side, side);
            GUI.Box(new Rect(paperRect.x - 4, paperRect.y - 4, side + 8, side + 8), GUIContent.none);
            if (reference != null) GUI.DrawTexture(paperRect, reference, ScaleMode.StretchToFill);
            GUI.DrawTexture(paperRect, canvas.Texture, ScaleMode.StretchToFill);
            HandlePaperEvent(Event.current);
            var x = paperRect.xMax + 22;
            var width = Screen.width - x - 26;
            GUI.Box(new Rect(x, 125, width, Mathf.Min(610, Screen.height - 150)), GUIContent.none, panelStyle);
            GUI.Label(new Rect(x + 16, 139, width - 32, 34), "Atelier de dessin", titleStyle);
            GUI.Label(new Rect(x + 16, 185, width - 32, 58), "Une région encrée se verrouille lorsque vous relevez le pinceau.", textStyle);
            GUI.Label(new Rect(x + 16, 248, width - 32, 27), "Traces", textStyle);
            brush = (BrushStyle)GUI.Toolbar(new Rect(x + 16, 280, width - 32, 34), (int)brush, new[] { "Plein", "Double", "Pointillé" });
            GUI.Label(new Rect(x + 16, 329, width - 32, 27), "Encre : " + inkNames[inkIndex], textStyle);
            inkIndex = GUI.Toolbar(new Rect(x + 16, 360, width - 32, 35), inkIndex, inkNames);
            GUI.Label(new Rect(x + 16, 407, width - 32, 27), "Épaisseur : " + Mathf.RoundToInt(diameter) + " px", textStyle);
            diameter = GUI.HorizontalSlider(new Rect(x + 20, 446, width - 40, 25), diameter, 3, 40);
            GUI.Label(new Rect(x + 16, 473, width - 32, 54), "Encre restante : " + ((DrawingCanvas.InkLimitMicro - canvas.UsedInkMicro) / 1000000f).ToString("F1") + " / 1000\nNoyau " + RegionState(InkRegion.Core) + " · Couronne " + RegionState(InkRegion.Ring) + " · Périphérie " + RegionState(InkRegion.Outer), textStyle);
            if (GUI.Button(new Rect(x + 16, 555, width - 32, 43), "Fermer le parchemin", buttonStyle))
            {
                if (canvas.Engaged) FinalizeDrawing("window_closed");
                else page = Page.Library;
            }
        }

        private string RegionState(InkRegion r) => canvas.IsLocked(r) ? "verrouillé" : "libre";

        private void HandlePaperEvent(Event e)
        {
            if (e.isMouse && e.button != 0) return;
            var p = new Vector2((e.mousePosition.x - paperRect.x) * 1023f / paperRect.width,
                (paperRect.yMax - e.mousePosition.y) * 1023f / paperRect.height);
            var pressure = e.pressure > 0 ? Mathf.Clamp(e.pressure, .25f, 1.5f) : 1f;
            if (e.type == EventType.MouseDown && paperRect.Contains(e.mousePosition) && !canvas.Closed)
            {
                if (canvas.Begin(p, brush, inks[inkIndex], diameter, pressure))
                {
                    Append("down", p, pressure);
                    if (selected.sequence == 1 && api.Configured) StartCoroutine(SendBegin());
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && canvas.IsDrawing)
            {
                Append("move", p, pressure);
                canvas.Move(p, brush, inks[inkIndex], diameter, pressure);
                if (canvas.Closed) FinalizeDrawing("ink_exhausted");
                e.Use();
            }
            else if (e.type == EventType.MouseUp && canvas.IsDrawing)
            {
                Append("up", p);
                canvas.End();
                if (canvas.Closed) FinalizeDrawing("all_regions_locked");
                e.Use();
            }
        }

        private void Append(string op, Vector2 p, float pressure = 1f)
        {
            store.Append(selected, op, p, brush, inks[inkIndex], diameter, pressure);
            if (selected.sequence == 1) selected.needs_begin = true;
            store.Save(selected);
        }

        private void DrawProcessing()
        {
            var r = new Rect(Screen.width * .2f, 130, Screen.width * .6f, 310);
            var cacheUnavailable = selected != null && !string.IsNullOrEmpty(selected.spell_id) && string.IsNullOrEmpty(spellJson);
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 20, r.width - 48, 40), "Construction du sort", titleStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 80, r.width - 48, 80), selected == null ? "" :
                cacheUnavailable ? "État : cache local à restaurer\nLe parchemin est conservé. Reconnectez le service pour retélécharger le sort." :
                "État : " + selected.state + "\nLe parchemin engagé est conservé. Aucune progression en pourcentage n'est supposée.", textStyle);
            GUI.Box(new Rect(r.x + 24, r.y + 162, r.width - 48, 48), GUIContent.none);
            GUI.Label(new Rect(r.x + 36, r.y + 173, r.width - 72, 30),
                selected == null ? "" : cacheUnavailable ? "01 Trace  ✓      02 Capture  ✓      03 Cache à restaurer" :
                selected.state == "ready" ? "01 Trace  ✓      02 Capture  ✓      03 Sort disponible  ✓" :
                selected.needs_capture ? "01 Trace  ✓      02 Capture à transmettre      03 En attente" :
                "01 Trace  ✓      02 Capture  ✓      03 Génération en attente", textStyle);
            if (GUI.Button(new Rect(r.x + 24, r.y + 225, 210, 45), "Bibliothèque", buttonStyle)) page = Page.Library;
            if (selected != null && selected.state == "capture_pending" && api.Configured && GUI.Button(new Rect(r.x + 250, r.y + 225, 235, 45), "Transmettre", buttonStyle)) StartCoroutine(Sync(selected));
            var preview = LocalCapturePreview();
            if (preview != null)
            {
                var lower = new Rect(r.x, r.yMax + 18, r.width, Mathf.Min(210, Screen.height - r.yMax - 32));
                GUI.Box(lower, GUIContent.none, panelStyle);
                GUI.DrawTexture(new Rect(lower.x + 18, lower.y + 14, 180, 180), preview, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(lower.x + 220, lower.y + 25, lower.width - 245, 36), "Trace archivée localement", titleStyle);
                GUI.Label(new Rect(lower.x + 220, lower.y + 76, lower.width - 245, 105),
                    "Le dessin, l'encre et le journal restent sur cet appareil. La fabrication suit les étapes du service ; seul un paquet validé devient jouable.", textStyle);
            }
        }

        private Texture2D LocalCapturePreview()
        {
            if (selected == null) return null;
            var path = Path.Combine(store.DirectoryFor(selected), "drawing.png");
            if (path == capturePreviewPath) return capturePreview;
            if (!File.Exists(path)) return null;
            if (capturePreview != null) Destroy(capturePreview);
            capturePreviewPath = path;
            capturePreview = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!capturePreview.LoadImage(File.ReadAllBytes(path), false))
            {
                Destroy(capturePreview);
                capturePreview = null;
            }
            return capturePreview;
        }

        private void DrawCard()
        {
            if (selected == null) { page = Page.Library; return; }
            var r = new Rect(Screen.width * .14f, 120, Screen.width * .72f, Screen.height - 160);
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.Label(new Rect(r.x + 25, r.y + 20, r.width - 50, 40), "Fiche du sort", titleStyle);
            try
            {
                var packet = JObject.Parse(spellJson);
                var display = packet["display"];
                GUI.Label(new Rect(r.x + 25, r.y + 75, r.width - 50, 43), display?["title"]?.ToString() ?? "Sort", titleStyle);
                GUI.Label(new Rect(r.x + 25, r.y + 130, r.width - 50, 120), display?["factual_description"]?.ToString() ?? "", textStyle);
                var lines = display?["mechanical_lines"] as JArray;
                if (lines != null)
                {
                    var y = r.y + 260;
                    foreach (var line in lines)
                    {
                        if (y > r.yMax - 120) break;
                        GUI.Label(new Rect(r.x + 28, y, r.width - 56, 38), "• " + line, textStyle);
                        y += 42;
                    }
                }
            }
            catch (Exception ex) { notice = "Paquet local illisible : " + ex.Message; }
            if (GUI.Button(new Rect(r.x + 25, r.yMax - 70, 260, 45), "Lancer dans le laboratoire", buttonStyle)) EnterLab();
            if (GUI.Button(new Rect(r.x + 300, r.yMax - 70, 180, 45), "Bibliothèque", buttonStyle)) page = Page.Library;
        }

        private void DrawLabHud()
        {
            GUI.DrawTexture(new Rect(20, 105, 370, 275), labHudBackground);
            GUI.DrawTexture(new Rect(20, 105, 370, 3), goldBar);
            GUI.Label(new Rect(37, 120, 340, 32), "Scène d'épreuve", titleStyle);
            GUI.Label(new Rect(37, 160, 340, 30), lab == null ? "" : lab.SpellTitle, textStyle);
            GUI.Label(new Rect(37, 194, 340, 90), lab == null ? "Chargement de la scène…" : lab.Metrics, textStyle);
            if (GUI.Button(new Rect(37, 290, 150, 38), "Remise à zéro", buttonStyle)) lab?.ResetTargets();
            if (GUI.Button(new Rect(202, 290, 160, 38), "Bibliothèque", buttonStyle))
            {
                page = Page.Library;
                SceneManager.LoadScene("Bootstrap");
            }
            GUI.Label(new Rect(37, 336, 340, 45), "Clic : lancer · clic droit : orbiter\nMolette : zoom · RAZ : annuler", textStyle);
            var legendX = Screen.width - 209;
            GUI.DrawTexture(new Rect(legendX, 105, 186, 124), labHudBackground);
            GUI.DrawTexture(new Rect(legendX, 105, 186, 3), goldBar);
            GUI.Label(new Rect(legendX + 13, 116, 165, 29), "Cibles", textStyle);
            GUI.DrawTexture(new Rect(legendX + 15, 150, 11, 11), hostileSwatch);
            GUI.Label(new Rect(legendX + 34, 144, 135, 25), "Hostile", textStyle);
            GUI.DrawTexture(new Rect(legendX + 15, 179, 11, 11), allySwatch);
            GUI.Label(new Rect(legendX + 34, 173, 135, 25), "Allié", textStyle);
            GUI.DrawTexture(new Rect(legendX + 98, 179, 11, 11), objectSwatch);
            GUI.Label(new Rect(legendX + 116, 173, 58, 25), "Objet", textStyle);
        }

        private IEnumerator Connect()
        {
            busy = true;
            try { api.Configure(serviceUrl, token); PlayerPrefs.SetString("palimpseste.lab_url", serviceUrl); }
            catch (Exception ex) { notice = ex.Message; busy = false; yield break; }
            CapabilitiesDto caps = null;
            string error = null;
            yield return api.GetCapabilities((c, e) => { caps = c; error = e; });
            if (error != null || caps == null || caps.layout_version != "three_regions_v1")
            {
                notice = "Connexion impossible : " + (error ?? "layout incompatible"); busy = false; yield break;
            }
            byte[] bytes = null;
            string hash = null;
            yield return api.GetArtifact(caps.reference_artifact_id, (b, h, e) => { bytes = b; hash = h; error = e; });
            if (error != null || bytes == null) { notice = "Référence indisponible : " + error; busy = false; yield break; }
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!image.LoadImage(bytes, false) || image.width != DrawingCanvas.Size || image.height != DrawingCanvas.Size)
            { notice = "Image de référence incompatible"; Destroy(image); busy = false; yield break; }
            if (reference != null) Destroy(reference);
            reference = image;
            referenceBytes = bytes;
            if (WindowsCredentialStore.TryWrite(serviceUrl, token, out var credentialError))
            {
                notice = "Laboratoire connecté. Jeton conservé dans le coffre Windows.";
                Debug.Log("PALIMPSESTE_CREDENTIAL_WRITE_OK");
            }
            else notice = "Laboratoire connecté pour cette session. Jeton non conservé : " + credentialError;
            busy = false;
            foreach (var item in records) if (item.needs_capture && item.state != "capture_corrupted") StartCoroutine(Sync(item));
        }

        private IEnumerator NewParchment()
        {
            if (!api.Configured || referenceBytes == null) { notice = "Connectez d'abord le laboratoire et chargez sa référence."; yield break; }
            busy = true;
            ParchmentDto remote = null;
            string error = null;
            yield return api.Allocate(Guid.NewGuid().ToString("N"), (p, e) => { remote = p; error = e; });
            busy = false;
            if (error != null || remote == null) { notice = "Allocation impossible : " + error; yield break; }
            var record = store.Create(remote.parchment_id);
            record.reference_artifact_id = "server";
            record.reference_sha256 = ParchmentStore.Hash(referenceBytes);
            File.WriteAllBytes(Path.Combine(store.DirectoryFor(record), "reference.png"), referenceBytes);
            store.Save(record);
            records.Insert(0, record);
            Open(record);
        }

        private void Open(ParchmentRecord record)
        {
            selected = record;
            canvas = store.Replay(record);
            var refPath = Path.Combine(store.DirectoryFor(record), "reference.png");
            if (File.Exists(refPath))
            {
                var bytes = File.ReadAllBytes(refPath);
                if (ParchmentStore.Hash(bytes) == record.reference_sha256)
                {
                    if (reference != null) Destroy(reference);
                    reference = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    reference.LoadImage(bytes, false);
                    referenceBytes = bytes;
                }
            }
            var cachedSpellMissing = !string.IsNullOrEmpty(record.spell_id) && !TryOpenCached(record);
            if (!string.IsNullOrEmpty(record.spell_id) && !cachedSpellMissing) page = Page.Card;
            else if (record.state == "blank" && reference != null) page = Page.Drawing;
            else
            {
                page = Page.Processing;
                if (cachedSpellMissing)
                    notice = "Sort local indisponible : paquet, version ou artefact absent ou modifié.";
                if (record.needs_capture && api.Configured) StartCoroutine(Sync(record));
                else if (!string.IsNullOrEmpty(record.job_id) && api.Configured) StartCoroutine(Poll(record));
            }
        }

        private bool TryOpenCached(ParchmentRecord record)
        {
            spellJson = null;
            if (!CachedSpellVerifier.TryLoad(record, store.DirectoryFor(record), out var cached))
                return false;
            spellJson = cached;
            return true;
        }

        private void FinalizeDrawing(string reason)
        {
            if (selected == null || canvas == null || !canvas.Engaged) { page = Page.Library; return; }
            canvas.Close();
            Append("close", Vector2.zero);
            selected.closed_reason = reason;
            selected.state = "capture_pending";
            selected.needs_capture = true;
            store.Save(selected);
            page = Page.Processing;
            notice = "Parchemin encré fermé et conservé localement.";
            if (api.Configured) StartCoroutine(Sync(selected));
        }

        private IEnumerator SendBegin()
        {
            if (selected == null || !selected.needs_begin || !api.Configured) yield break;
            var path = Path.Combine(store.DirectoryFor(selected), "journal.jsonl");
            var first = JObject.Parse(File.ReadAllLines(path)[0]);
            var hash = first["sha256"]?.ToString();
            if (string.IsNullOrEmpty(hash)) { notice = "PremiÃ¨re inscription illisible"; yield break; }
            string error = null;
            yield return api.Begin(selected, hash, e => error = e);
            if (error == null) { selected.needs_begin = false; store.Save(selected); }
            else notice = "Première inscription conservée ; transmission en attente : " + error;
        }

        private IEnumerator Sync(ParchmentRecord record)
        {
            if (!api.Configured || busy || record == null || !record.needs_capture) yield break;
            busy = true;
            if (record.needs_begin)
            {
                selected = record;
                yield return SendBegin();
                if (record.needs_begin) { busy = false; yield break; }
            }
            var reconstructed = store.Replay(record);
            var dir = store.DirectoryFor(record);
            var refPath = Path.Combine(dir, "reference.png");
            if (!File.Exists(refPath) || ParchmentStore.Hash(File.ReadAllBytes(refPath)) != record.reference_sha256)
            { notice = "Référence du parchemin absente ou modifiée"; busy = false; yield break; }
            var refTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!refTexture.LoadImage(File.ReadAllBytes(refPath), false) || refTexture.width != 1024 || refTexture.height != 1024)
            { notice = "Référence locale invalide"; Destroy(refTexture); busy = false; yield break; }
            var drawing = reconstructed.ExportDrawingPng(refTexture.GetPixels32());
            var ink = reconstructed.ExportInkPng();
            var journal = store.JournalGzip(record);
            var pixelHash = DecodedPixelHash(drawing);
            var capture = new DrawingCaptureDto
            {
                capture_id = record.capture_id, parchment_id = record.parchment_id,
                reference_sha256 = record.reference_sha256, raster_version = DrawingCanvas.RasterVersion,
                drawing_file_sha256 = ParchmentStore.Hash(drawing), drawing_pixel_sha256 = pixelHash,
                ink_file_sha256 = ParchmentStore.Hash(ink), journal_file_sha256 = ParchmentStore.Hash(journal),
                used_ink_micro_units = reconstructed.UsedInkMicro,
                closed_reason = record.closed_reason,
                locked_regions = LockedNames(reconstructed), created_at = record.created_at
            };
            File.WriteAllBytes(Path.Combine(dir, "drawing.png"), drawing);
            File.WriteAllBytes(Path.Combine(dir, "ink.png"), ink);
            File.WriteAllBytes(Path.Combine(dir, "journal.jsonl.gz"), journal);
            File.WriteAllText(Path.Combine(dir, "capture.json"), JsonUtility.ToJson(capture, true), Encoding.UTF8);
            Destroy(refTexture);
            JobDto job = null;
            string error = null;
            yield return api.Upload(record, capture, drawing, ink, journal, (j, e) => { job = j; error = e; });
            busy = false;
            if (error != null || job == null)
            {
                if (error != null && error.Contains("\"code\":\"capture_incompatible\""))
                {
                    record.state = "capture_corrupted";
                    record.needs_capture = false;
                    store.Save(record);
                    notice = "Capture refusée : journal ou image incompatibles. Le parchemin local est conservé.";
                }
                else notice = "Capture conservée pour reprise : " + error;
                yield break;
            }
            record.needs_capture = false;
            record.job_id = job.job_id;
            record.state = job.state;
            store.Save(record);
            notice = "Capture transmise. " + job.message;
            StartCoroutine(Poll(record));
        }

        private static string[] LockedNames(DrawingCanvas c)
        {
            var list = new System.Collections.Generic.List<string>();
            if (c.IsLocked(InkRegion.Core)) list.Add("core");
            if (c.IsLocked(InkRegion.Ring)) list.Add("ring");
            if (c.IsLocked(InkRegion.Outer)) list.Add("outer");
            return list.ToArray();
        }

        private static string DecodedPixelHash(byte[] png)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            t.LoadImage(png, false);
            var pixels = t.GetPixels32();
            var rgba = new byte[pixels.Length * 4];
            for (var y = 0; y < t.height; y++)
                for (var x = 0; x < t.width; x++)
                {
                    var p = pixels[(t.height - 1 - y) * t.width + x];
                    var i = (y * t.width + x) * 4;
                    rgba[i] = p.r; rgba[i + 1] = p.g; rgba[i + 2] = p.b; rgba[i + 3] = p.a;
                }
            Destroy(t);
            return ParchmentStore.Hash(rgba);
        }

        private IEnumerator Poll(ParchmentRecord record)
        {
            while (record != null && !string.IsNullOrEmpty(record.job_id))
            {
                JobDto job = null;
                string error = null;
                yield return api.GetJob(record.job_id, (j, e) => { job = j; error = e; });
                if (error != null) { notice = "Lecture de tâche interrompue : " + error; yield break; }
                record.state = job.state;
                if (!string.IsNullOrEmpty(job.spell_id)) record.spell_id = job.spell_id;
                store.Save(record);
                notice = job.message;
                if (job.state == "ready") { yield return Download(record); yield break; }
                if (job.state == "needs_operator") { notice = "Intervention technique requise : " + job.message; yield break; }
                yield return new WaitForSecondsRealtime(Mathf.Clamp(job.poll_after_ms / 1000f, 1f, 10f));
            }
        }

        private IEnumerator Download(ParchmentRecord record)
        {
            byte[] bytes = null;
            string error = null;
            yield return api.GetSpell(record.spell_id, (b, e) => { bytes = b; error = e; });
            if (error != null) { notice = "Sort non téléchargé : " + error; yield break; }
            JObject packet;
            try { packet = JObject.Parse(Encoding.UTF8.GetString(bytes)); }
            catch (Exception ex) { notice = "Paquet invalide : " + ex.Message; yield break; }
            if (packet["schema_version"]?.ToString() != "sp.compiled/1.0" || packet["parchment_id"]?.ToString() != record.parchment_id)
            { notice = "Identité du paquet incohérente"; yield break; }
            var artifactDir = Path.Combine(store.DirectoryFor(record), "artifacts");
            Directory.CreateDirectory(artifactDir);
            var manifest = new JArray();
            if (packet["geometry_manifest"] is JArray geometries) foreach (var item in geometries) manifest.Add(item);
            if (packet["binary_assets"] is JArray binaries) foreach (var item in binaries) manifest.Add(item);
            foreach (var item in manifest)
            {
                var id = item["artifact_id"]?.ToString();
                var expected = item["sha256"]?.ToString();
                if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_.-]{0,63}$"))
                { notice = "Référence d'artefact interdite"; yield break; }
                byte[] data = null;
                yield return api.GetArtifact(id, (b, h, e) => { data = b; error = e; });
                if (error != null || data == null || ParchmentStore.Hash(data) != expected)
                { notice = "Artefact invalide : " + id + " " + error; yield break; }
                File.WriteAllBytes(Path.Combine(artifactDir, id), data);
            }
            var spellPath = Path.Combine(store.DirectoryFor(record), "spell.json");
            File.WriteAllBytes(spellPath, bytes);
            File.WriteAllText(spellPath + ".sha256", ParchmentStore.Hash(bytes), Encoding.ASCII);
            record.state = "ready";
            store.Save(record);
            if (selected == record) { spellJson = Encoding.UTF8.GetString(bytes); page = Page.Card; }
            notice = "Sort téléchargé et vérifié. Il est disponible hors ligne.";
        }

        private void EnterLab()
        {
            if (string.IsNullOrEmpty(spellJson)) return;
            page = Page.Lab;
            SceneManager.LoadScene("SpellLab");
        }

        private void BeginLab()
        {
            if (lab != null) Destroy(lab.gameObject);
            var obj = new GameObject("Spell Lab Runtime");
            lab = obj.AddComponent<SpellLab>();
            lab.Initialize(spellJson, store.DirectoryFor(selected));
            notice = lab.Ready ? "Sort local prêt. Visez une cible et cliquez pour lancer." : lab.Metrics;
        }
    }
}
