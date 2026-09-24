using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
        private const string EmptyDrawingWarning = "Ajoutez au moins un trait avant de terminer.";
        private enum Page { Library, Drawing, Processing, Card, Interpretation, Lab }
        private static PalimpsesteApp instance;
        private readonly LabApi api = new LabApi();
        private ParchmentStore store;
        private System.Collections.Generic.List<ParchmentRecord> records;
        private ParchmentRecord selected;
        private DrawingCanvas canvas;
        private Texture2D reference;
        private byte[] referenceBytes;
        private string serviceUrl;
        private string principalId;
        private string token = "";
        private string notice = "Ouverture du laboratoire… Les sorts déjà enregistrés restent disponibles hors ligne.";
        private Page page;
        private BrushStyle brush = BrushStyle.Solid;
        private int inkIndex;
        private float diameter = 14;
        private readonly Color32[] inks = { new Color32(125, 39, 31, 220), new Color32(30, 92, 132, 220), new Color32(74, 66, 49, 235), new Color32(80, 84, 103, 205) };
        private readonly string[] inkNames = { "Braise", "Eau", "Pierre", "Souffle" };
        private Rect paperRect;
        private bool busy;
        private string spellJson;
        private SpellDescriptionView descriptionView;
        private string feedbackDraft = "";
        private bool feedbackBusy;
        private bool showLabFeedback;
        private readonly HashSet<string> pollingJobs = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 interpretationScroll;
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
        private Texture2D generatedPreview;
        private string generatedPreviewHash;
        private bool showGeneratedReference = true;
        private bool showReferenceViewer;
        private float referenceZoom = 1;
        private Vector2 referenceScroll;
        private Vector2 libraryScroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartApp()
        {
            if (SpellV2ValidationRunner.TryStartFromCommandLine()) return;
            if (SpellVisualCaptureRunner.TryStartFromCommandLine()) return;
            if (instance != null) return;
            var obj = new GameObject("Palimpseste App");
            instance = obj.AddComponent<PalimpsesteApp>();
            DontDestroyOnLoad(obj);
        }

        private void Awake()
        {
            store = new ParchmentStore();
            records = new List<ParchmentRecord>();
            libraryBackdrop = Resources.Load<Texture2D>("LibraryBackdrop");
            if (!ServiceAccess.TryLoadServiceUrl(Path.Combine(Application.streamingAssetsPath, "service.json"), out serviceUrl))
                ServiceAccess.TryLoadServiceUrl(LocalServicePath(), out serviceUrl);
            if (!string.IsNullOrEmpty(serviceUrl) && WindowsCredentialStore.TryRead(serviceUrl, out token, out _))
                Debug.Log("PALIMPSESTE_CREDENTIAL_READ_OK");
            if (token == null) token = "";
            if (!File.Exists(Path.Combine(AccessDirectory(), "access.json")) &&
                ServiceAccess.TryLoadPrincipalId(LocalIdentityPath(), serviceUrl, token, out var rememberedPrincipal))
            {
                principalId = rememberedPrincipal;
                RefreshVisibleRecords();
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == "SpellLab") page = Page.Lab;
        }

        private IEnumerator Start()
        {
            if (page != Page.Lab) yield return BootstrapSession(true);
        }

        private static string AccessDirectory() => Path.Combine(Application.persistentDataPath, "Palimpseste");
        private static string LocalServicePath() => Path.Combine(AccessDirectory(), "service.json");
        private static string LocalIdentityPath() => Path.Combine(AccessDirectory(), "identity.json");

        private void RefreshVisibleRecords()
        {
            records = store.LoadAll().FindAll(item => PlayerParchmentFilter.IsUserParchment(item, principalId));
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (capturePreview != null) Destroy(capturePreview);
            if (generatedPreview != null) Destroy(generatedPreview);
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
            notice = "Geste interrompu et enregistré. Vous pouvez continuer à dessiner.";
        }

        private void OnApplicationQuit()
        {
            if (page != Page.Drawing || canvas == null || selected == null || !canvas.Engaged) return;
            if (canvas.IsDrawing) { Append("up", Vector2.zero); canvas.End(); }
            // The free canvas is only submitted after the player's explicit finish action.
            // The journal and state have already been flushed by Append.
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
            if (showReferenceViewer) { DrawReferenceViewer(); return; }
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
                case Page.Interpretation: DrawInterpretation(); break;
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
            GUI.Label(new Rect(x + 20, 179, width - 40, 60), api.Configured ?
                "Le laboratoire est prêt. Dessinez ou retrouvez un sort enregistré." :
                "Accès au laboratoire nécessaire. Vos sorts enregistrés restent jouables hors ligne.", textStyle);
            var previousEnabled = GUI.enabled;
            GUI.enabled = api.Configured && !busy;
            if (GUI.Button(new Rect(x + 20, 255, 280, 48), "Dessiner un parchemin", buttonStyle))
                StartCoroutine(NewParchment());
            GUI.enabled = previousEnabled;
            if (!api.Configured && !busy &&
                GUI.Button(new Rect(x + 315, 255, 198, 48), "Ouvrir mon invitation", buttonStyle))
                ImportInvitation();
            GUI.Label(new Rect(x + 20, 327, width - 40, 26), "Mes parchemins", textStyle);
            var guideX = x + width * .59f;
            var guideWidth = width * .38f;
            GUI.Box(new Rect(guideX, 270, guideWidth, Mathf.Min(300, Screen.height - 405)), GUIContent.none);
            GUI.Label(new Rect(guideX + 18, 285, guideWidth - 36, 36), "Votre parcours", titleStyle);
            GUI.DrawTexture(new Rect(guideX + 18, 325, guideWidth - 36, 2), goldBar);
            GUI.Label(new Rect(guideX + 18, 340, guideWidth - 36, 44), "01  Dessiner librement", textStyle);
            GUI.Label(new Rect(guideX + 18, 390, guideWidth - 36, 44), "02  Transmettre votre trace", textStyle);
            GUI.Label(new Rect(guideX + 18, 440, guideWidth - 36, 48), "03  Lire l'interprétation du dessin", textStyle);
            GUI.Label(new Rect(guideX + 18, 485, guideWidth - 36, 48), "04  Essayer le sort dans le labo", textStyle);
            GUI.Label(new Rect(guideX + 18, 532, guideWidth - 36, 34), "Sorts téléchargés : accessibles hors ligne.", textStyle);
            var listWidth = width * .55f;
            var viewport = new Rect(x + 20, 365, listWidth, Mathf.Max(90, Screen.height - 413));
            var contentWidth = listWidth - 18;
            var contentHeight = Mathf.Max(viewport.height, records.Count * 66 + 4);
            libraryScroll = GUI.BeginScrollView(viewport, libraryScroll,
                new Rect(0, 0, contentWidth, contentHeight));
            if (records.Count == 0)
                GUI.Label(new Rect(4, 5, contentWidth - 8, 72), "Aucun parchemin enregistré. Une invitation du laboratoire permet de commencer.", textStyle);
            for (var i = 0; i < records.Count; i++)
            {
                var r = records[i];
                var top = i * 66f;
                GUI.Box(new Rect(0, top, contentWidth - 2, 57), GUIContent.none);
                GUI.DrawTexture(new Rect(0, top + 1, 4, 55), goldBar);
                GUI.Label(new Rect(14, top + 7, contentWidth - 140, 45),
                    LibraryStateLabel(r) + "\n" + LocalDate(r.created_at), textStyle);
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
            GUI.Label(new Rect(x + 16, 185, width - 32, 58), "Dessinez librement. Sol imagine le sort, puis Astra compose ses effets et son rendu 3D.", textStyle);
            GUI.Label(new Rect(x + 16, 248, width - 32, 27), "Traces", textStyle);
            brush = (BrushStyle)GUI.Toolbar(new Rect(x + 16, 280, width - 32, 34), (int)brush, new[] { "Plein", "Double", "Pointillé" });
            GUI.Label(new Rect(x + 16, 329, width - 32, 27), "Encre : " + inkNames[inkIndex], textStyle);
            inkIndex = GUI.Toolbar(new Rect(x + 16, 360, width - 32, 35), inkIndex, inkNames);
            GUI.Label(new Rect(x + 16, 407, width - 32, 27), "Épaisseur : " + Mathf.RoundToInt(diameter) + " px", textStyle);
            diameter = GUI.HorizontalSlider(new Rect(x + 20, 446, width - 40, 25), diameter, 3, 40);
            GUI.Label(new Rect(x + 16, 473, width - 32, 54), "Encre restante : " + ((DrawingCanvas.InkLimitMicro - canvas.UsedInkMicro) / 1000000f).ToString("F1") + " / 1000\nAjoutez autant de traits que souhaité, puis validez.", textStyle);
            if (GUI.Button(new Rect(x + 16, 555, width - 32, 43), "Dessin terminé", buttonStyle))
                FinalizeDrawing("user_finished");
        }

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
                if (!canvas.IsDrawing)
                {
                    Append("up", p);
                    notice = "Encre épuisée. Cliquez sur Dessin terminé pour créer le sort.";
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && canvas.IsDrawing)
            {
                Append("up", p);
                canvas.End();
                e.Use();
            }
        }

        private void Append(string op, Vector2 p, float pressure = 1f)
        {
            store.Append(selected, op, p, brush, inks[inkIndex], diameter, pressure);
            if (selected.sequence == 1) selected.needs_begin = true;
            store.Save(selected);
            if (op == "down" && notice == EmptyDrawingWarning)
                notice = "Dessin en cours. Ajoutez des traits, puis cliquez sur Dessin terminé.";
        }

        private void DrawProcessing()
        {
            var width = Mathf.Min(Screen.width - 36, 1240);
            var r = new Rect((Screen.width - width) * .5f, 116, width, Screen.height - 140);
            var cacheUnavailable = selected != null && !string.IsNullOrEmpty(selected.spell_id) && string.IsNullOrEmpty(spellJson);
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 16, r.width - 48, 40), "Du dessin au sort", titleStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 59, r.width - 48, 46),
                cacheUnavailable ? "Paquet local à restaurer. Le dessin et la lecture restent conservés." :
                selected != null && !api.Configured && selected.needs_capture ?
                    "Hors ligne · la trace est conservée ici. Reconnectez le laboratoire pour la transmettre." :
                selected != null && !api.Configured && !string.IsNullOrEmpty(selected.job_id) ?
                    "Hors ligne · le traitement reprendra après reconnexion au laboratoire." :
                selected == null ? "" : JobStateLabel(selected), textStyle);
            DrawProcessingSteps(new Rect(r.x + 24, r.y + 109, r.width - 48, 57));
            var previewWidth = Mathf.Min(260, (r.width - 72) * .31f);
            var bodyY = r.y + 181;
            var bodyHeight = Mathf.Max(185, r.height - 262);
            GUI.Box(new Rect(r.x + 24, bodyY, previewWidth, bodyHeight), GUIContent.none);
            var preview = LocalCapturePreview();
            if (preview != null)
            {
                var imageSide = Mathf.Min(previewWidth - 26, bodyHeight - 85);
                GUI.DrawTexture(new Rect(r.x + 37, bodyY + 12, imageSide, imageSide), preview, ScaleMode.ScaleToFit);
            }
            GUI.Label(new Rect(r.x + 37, bodyY + bodyHeight - 64, previewWidth - 26, 58),
                "Votre trace conservée\nsur cet appareil", textStyle);
            DrawInterpretationPanel(new Rect(r.x + 36 + previewWidth, bodyY,
                r.width - previewWidth - 60, bodyHeight));
            if (GUI.Button(new Rect(r.x + 24, r.yMax - 66, 170, 42), "Bibliothèque", buttonStyle)) page = Page.Library;
            if (selected != null && CanResumeJob(selected) && api.Configured && !busy &&
                !pollingJobs.Contains(selected.job_id) &&
                GUI.Button(new Rect(r.x + 211, r.yMax - 66, 188, 42), "Réessayer ce dessin", buttonStyle))
                StartCoroutine(ResumeJob(selected));
            else if (selected != null && selected.needs_capture && api.Configured && !busy &&
                GUI.Button(new Rect(r.x + 211, r.yMax - 66, 188, 42), "Transmettre", buttonStyle))
                StartCoroutine(Sync(selected));
            else if (selected != null && !string.IsNullOrEmpty(selected.job_id) && api.Configured && !busy &&
                     !pollingJobs.Contains(selected.job_id) &&
                     GUI.Button(new Rect(r.x + 211, r.yMax - 66, 188, 42), "Actualiser", buttonStyle))
                StartCoroutine(Poll(selected));
            else if (selected != null && !api.Configured &&
                     (selected.needs_capture || !string.IsNullOrEmpty(selected.job_id)))
            {
                var previousEnabled = GUI.enabled;
                GUI.enabled = previousEnabled && !busy;
                if (GUI.Button(new Rect(r.x + 211, r.yMax - 66, 188, 42),
                        busy ? "Reconnexion…" : "Reconnecter", buttonStyle))
                    StartCoroutine(ReconnectToPendingParchment(selected.parchment_id));
                GUI.enabled = previousEnabled;
            }
            if (descriptionView != null && spellJson == null &&
                GUI.Button(new Rect(r.x + 415, r.yMax - 66, 204, 42), "Lire et signaler", buttonStyle))
                page = Page.Interpretation;
            if (spellJson != null && descriptionView != null &&
                GUI.Button(new Rect(r.xMax - 211, r.yMax - 66, 187, 42), "Voir le sort", buttonStyle))
                page = Page.Card;
        }

        private void DrawProcessingSteps(Rect area)
        {
            var names = new[] { "Dessin", "Description", "Image du sort", "Construction", "Compilation", "Sort" };
            var current = ProcessingStep(selected);
            var gap = 7f;
            var itemWidth = (area.width - gap * (names.Length - 1)) / names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                var x = area.x + i * (itemWidth + gap);
                GUI.Box(new Rect(x, area.y, itemWidth, area.height), GUIContent.none);
                GUI.DrawTexture(new Rect(x, area.y, itemWidth, 3), i <= current ? goldBar : labHudBackground);
                var mark = i < current || (i == 5 && spellJson != null) ? "✓ " : i == current ? "… " : "· ";
                GUI.Label(new Rect(x + 7, area.y + 15, itemWidth - 12, 32), mark + names[i], textStyle);
            }
        }

        private int ProcessingStep(ParchmentRecord record)
        {
            if (record == null || record.needs_capture || string.IsNullOrEmpty(record.job_id)) return 0;
            if (spellJson != null && selected == record) return 5;
            var state = record.state is "needs_operator" or "waiting_retry" ? record.resume_stage : record.state;
            return state switch
            {
                "generating_visual_reference" => 2,
                "resolving_geometry" => 3,
                "planning" => 3,
                "refining_visuals" => 3,
                "validating" => 4,
                "ready" => 5,
                _ => descriptionView != null && selected == record ? 2 : 1
            };
        }

        private static string JobStateLabel(ParchmentRecord record)
        {
            if (record.needs_capture) return "Trace fermée · transmission de la capture en attente";
            if (record.state == "needs_operator")
                return CanResumeJob(record)
                    ? "Le sort n'est pas encore prêt. Réessayez avec ce dessin enregistré."
                    : "Ce dessin n'a pas encore produit de sort. Votre trace reste enregistrée.";
            var state = record.state switch
            {
                "queued" => "En file d'attente pour la lecture du dessin",
                "interpreting" => "Lecture du dessin et création de son interprétation",
                "generating_visual_reference" => "Création de l’image de référence depuis la description",
                "resolving_geometry" => "Recherche de références et de ressources visuelles",
                "planning" => "Construction du sort et de son animation depuis la description",
                "refining_visuals" => "Comparaison du rendu Unity à l’image · ajustements visuels",
                "validating" => "Plan compilé et ressources contrôlées avant publication",
                "ready" => "Sort validé · téléchargement et contrôle local",
                "waiting_retry" => "Nouvel essai de création prévu par le laboratoire",
                _ => "Traitement du sort en cours"
            };
            var elapsed = record.generation_elapsed_ms;
            if (record.state != "ready" && record.state != "needs_operator" &&
                DateTimeOffset.TryParse(record.elapsed_observed_at, out var observed))
                elapsed += (long)Math.Max(0, (DateTimeOffset.UtcNow - observed).TotalMilliseconds);
            var duration = TimeSpan.FromMilliseconds(Math.Max(0, elapsed));
            var timing = elapsed > 0 ? ((int)duration.TotalMinutes).ToString() + " min " + duration.Seconds.ToString("00") + " s · " : "";
            return timing + state;
        }

        private static bool CanResumeJob(ParchmentRecord record) =>
            record != null && record.state == "needs_operator" && record.job_retryable &&
            !string.IsNullOrEmpty(record.job_id) && !record.needs_capture;

        private void DrawInterpretationPanel(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            var visual = GeneratedReferencePreview();
            if (visual != null)
            {
                var buttonWidth = Mathf.Min(190, (area.width - 36) / 2);
                if (GUI.Button(new Rect(area.x + 15, area.y + 12, buttonWidth, 34), "Image du sort", buttonStyle)) showGeneratedReference = true;
                if (GUI.Button(new Rect(area.x + 21 + buttonWidth, area.y + 12, buttonWidth, 34), "Description", buttonStyle)) showGeneratedReference = false;
                if (showGeneratedReference)
                {
                    GUI.DrawTexture(new Rect(area.x + 15, area.y + 59, area.width - 30, area.height - 92), visual, ScaleMode.ScaleToFit);
                    if (GUI.Button(new Rect(area.x + 15, area.yMax - 30, area.width - 30, 26),
                            "Agrandir l’image · zoom et déplacement", buttonStyle))
                    {
                        referenceZoom = 1;
                        referenceScroll = Vector2.zero;
                        showReferenceViewer = true;
                    }
                    return;
                }
            }
            else
            GUI.Label(new Rect(area.x + 15, area.y + 12, area.width - 30, 35), "Interprétation du dessin", titleStyle);
            GUI.DrawTexture(new Rect(area.x + 15, area.y + 52, area.width - 30, 2), goldBar);
            if (descriptionView == null)
            {
                GUI.Label(new Rect(area.x + 15, area.y + 70, area.width - 30, area.height - 82),
                    "La lecture textuelle apparaît ici dès qu'elle est validée par le service. Votre dessin est conservé pendant l'attente.", textStyle);
                return;
            }
            var content = new StringBuilder();
            content.AppendLine(descriptionView.Title).AppendLine().AppendLine(descriptionView.Summary).AppendLine();
            if (descriptionView.Behaviors?.Length > 0)
                foreach (var behavior in descriptionView.Behaviors) content.AppendLine(behavior).AppendLine();
            if (descriptionView.Lifecycle?.Length > 0)
            {
                content.AppendLine("Du lancement à la disparition").AppendLine();
                foreach (var subject in descriptionView.Lifecycle) content.AppendLine(subject).AppendLine();
            }
            content.AppendLine("Ce qui a inspiré ce sort");
            foreach (var line in descriptionView.Observations) content.Append("• ").AppendLine(line).AppendLine();
            content.AppendLine("Idées de sort proposées");
            foreach (var line in descriptionView.Clauses) content.Append("• ").AppendLine(line).AppendLine();
            var text = content.ToString();
            var viewport = new Rect(area.x + 13, area.y + 68, area.width - 26, area.height - 81);
            var contentWidth = Mathf.Max(160, viewport.width - 22);
            var contentHeight = Mathf.Max(viewport.height, textStyle.CalcHeight(new GUIContent(text), contentWidth) + 18);
            interpretationScroll = GUI.BeginScrollView(viewport, interpretationScroll,
                new Rect(0, 0, contentWidth, contentHeight));
            GUI.Label(new Rect(0, 0, contentWidth, contentHeight), text, textStyle);
            GUI.EndScrollView();
        }

        private void DrawReferenceViewer()
        {
            var visual = GeneratedReferencePreview();
            if (visual == null) { showReferenceViewer = false; return; }
            var input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            {
                showReferenceViewer = false;
                input.Use();
                return;
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), SolidBackground);
            GUI.Label(new Rect(20, 12, Screen.width - 170, 38), "Image du sort", titleStyle);
            if (GUI.Button(new Rect(Screen.width - 142, 12, 122, 38), "Fermer", buttonStyle))
                showReferenceViewer = false;
            var previousZoom = referenceZoom;
            if (GUI.Button(new Rect(20, 60, 94, 34), "Vue entière", buttonStyle)) referenceZoom = 1;
            if (GUI.Button(new Rect(123, 60, 44, 34), "−", buttonStyle)) referenceZoom = Mathf.Max(1, referenceZoom / 1.4f);
            referenceZoom = GUI.HorizontalSlider(new Rect(181, 70, Mathf.Max(80, Screen.width - 466), 20), referenceZoom, 1, 7);
            if (GUI.Button(new Rect(Screen.width - 272, 60, 44, 34), "+", buttonStyle)) referenceZoom = Mathf.Min(7, referenceZoom * 1.4f);
            GUI.Label(new Rect(Screen.width - 218, 63, 195, 30), "Zoom ×" + referenceZoom.ToString("0.0", CultureInfo.InvariantCulture), textStyle);
            var viewport = new Rect(18, 108, Screen.width - 36, Mathf.Max(80, Screen.height - 146));
            var fit = Mathf.Min((viewport.width - 20) / visual.width, (viewport.height - 20) / visual.height);
            var imageSize = new Vector2(visual.width * fit, visual.height * fit) * referenceZoom;
            if (input.type == EventType.ScrollWheel && viewport.Contains(input.mousePosition))
            {
                referenceZoom = Mathf.Clamp(referenceZoom * Mathf.Pow(1.12f, -input.delta.y), 1, 7);
                input.Use();
                imageSize = new Vector2(visual.width * fit, visual.height * fit) * referenceZoom;
            }
            if (!Mathf.Approximately(referenceZoom, previousZoom))
                referenceScroll = (referenceScroll + viewport.size * .5f) * (referenceZoom / previousZoom) - viewport.size * .5f;
            if (input.type == EventType.MouseDrag && input.button == 0 && viewport.Contains(input.mousePosition))
            {
                referenceScroll -= input.delta;
                input.Use();
            }
            var content = new Rect(0, 0, Mathf.Max(viewport.width - 20, imageSize.x), Mathf.Max(viewport.height - 20, imageSize.y));
            referenceScroll.x = Mathf.Clamp(referenceScroll.x, 0, Mathf.Max(0, content.width - viewport.width + 20));
            referenceScroll.y = Mathf.Clamp(referenceScroll.y, 0, Mathf.Max(0, content.height - viewport.height + 20));
            referenceScroll = GUI.BeginScrollView(viewport, referenceScroll, content);
            GUI.DrawTexture(new Rect((content.width - imageSize.x) * .5f, (content.height - imageSize.y) * .5f,
                imageSize.x, imageSize.y), visual, ScaleMode.ScaleToFit);
            GUI.EndScrollView();
            GUI.Label(new Rect(20, Screen.height - 32, Screen.width - 40, 26),
                "Molette pour zoomer · Maintenir et glisser pour parcourir l’image · Échap pour fermer", textStyle);
        }

        private static string LibraryStateLabel(ParchmentRecord record)
        {
            if (!string.IsNullOrEmpty(record.spell_id)) return "Sort disponible";
            if (record.needs_capture) return "En attente de transmission";
            return record.state switch
            {
                "blank" => "Parchemin vierge",
                "writing" => "Encrage en cours",
                "capture_pending" => "En attente de transmission",
                "capture_corrupted" => "Journal endommagé",
                "queued" => "Lecture en attente",
                "interpreting" => "Lecture du dessin",
                "generating_visual_reference" => "Image du sort en création",
                "resolving_geometry" => "Références et ressources",
                "refining_visuals" => "Finition visuelle et animations",
                "planning" => "Construction du sort décrit",
                "validating" => "Compilation du sort",
                "ready" => "Sort à récupérer",
                "waiting_retry" => "Nouvel essai prévu",
                "needs_operator" => CanResumeJob(record) ? "Sort à relancer" : "Sort non créé",
                _ => "Traitement en cours"
            };
        }

        private static string LocalDate(string isoUtc)
        {
            return DateTimeOffset.TryParse(isoUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
                ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                : "Date inconnue";
        }

        private void DrawInterpretation()
        {
            if (selected == null) { page = Page.Library; return; }
            var area = new Rect(Screen.width * .12f, 120, Screen.width * .76f, Screen.height - 202);
            DrawInterpretationPanel(new Rect(area.x, area.y, area.width, area.height - 190));
            DrawFeedbackForm(new Rect(area.x + 15, area.yMax - 182, area.width - 30, 177));
            if (GUI.Button(new Rect(area.x + 15, area.yMax + 14, 190, 42),
                    spellJson == null ? "Retour au suivi" : "Retour au sort", buttonStyle))
                page = spellJson == null ? Page.Processing : Page.Card;
        }

        private void DrawFeedbackForm(Rect area)
        {
            if (selected == null || descriptionView == null || string.IsNullOrEmpty(selected.job_id) ||
                string.IsNullOrEmpty(selected.description_sha256)) return;
            GUI.Label(new Rect(area.x, area.y, area.width, 29), "Signaler une lecture incorrecte", textStyle);
            var pending = !string.IsNullOrEmpty(selected.feedback_key);
            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !pending && !feedbackBusy;
            feedbackDraft = GUI.TextArea(new Rect(area.x, area.y + 31, area.width, 72),
                feedbackDraft ?? "", 2000);
            GUI.enabled = previousEnabled;
            var message = selected.feedback_sent
                ? "Retour enregistré. Le sort de ce parchemin reste inchangé."
                : pending
                    ? "Retour conservé sur cet appareil. Réessayez la transmission si nécessaire."
                    : "Décrivez ce que le dessin devait évoquer. Ce retour aidera à améliorer la lecture d'Astra.";
            GUI.Label(new Rect(area.x, area.y + 107, area.width, 34), message, textStyle);
            GUI.enabled = previousEnabled && api.Configured && !feedbackBusy && !selected.feedback_sent &&
                          (pending || !string.IsNullOrWhiteSpace(feedbackDraft));
            if (GUI.Button(new Rect(area.x, area.y + 139, Mathf.Min(area.width, 255), 36),
                    pending ? "Réessayer l'envoi" : "Envoyer ce retour", buttonStyle))
                StartCoroutine(SendFeedback(selected));
            GUI.enabled = previousEnabled;
        }

        private IEnumerator SendFeedback(ParchmentRecord record)
        {
            if (feedbackBusy || !api.Configured || record == null || record.feedback_sent ||
                string.IsNullOrEmpty(record.job_id) || string.IsNullOrEmpty(record.description_sha256)) yield break;
            if (string.IsNullOrEmpty(record.feedback_key))
            {
                var correction = (feedbackDraft ?? "").Trim();
                if (correction.Length is < 1 or > 2000) yield break;
                record.feedback_key = Guid.NewGuid().ToString("N");
                record.feedback_description_sha256 = record.description_sha256;
                record.feedback_correction = correction;
                store.Save(record);
            }
            if (record.feedback_description_sha256 != record.description_sha256)
            {
                notice = "La lecture conservée a changé ; ce retour ne peut pas être transmis.";
                yield break;
            }
            feedbackBusy = true;
            InterpretationFeedbackDto response = null;
            string error = null;
            yield return api.SendInterpretationFeedback(record.job_id, record.feedback_description_sha256,
                record.feedback_correction, record.feedback_key,
                (value, failure) => { response = value; error = failure; });
            feedbackBusy = false;
            if (error != null || response == null || !response.recorded ||
                response.job_id != record.job_id ||
                response.description_sha256 != record.feedback_description_sha256 ||
                !Guid.TryParseExact(response.feedback_id, "N", out _))
            {
                notice = "Retour conservé sur cet appareil ; envoi à réessayer. " +
                         (error ?? "Réponse du laboratoire incohérente.");
                yield break;
            }
            record.feedback_id = response.feedback_id;
            record.feedback_sent = true;
            store.Save(record);
            notice = "Lecture incorrecte signalée. Ce sort reste inchangé ; le laboratoire utilisera ce retour pour ses prochaines versions.";
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

        private Texture2D GeneratedReferencePreview()
        {
            if (selected == null || string.IsNullOrEmpty(selected.visual_reference_sha256)) return null;
            if (generatedPreviewHash == selected.visual_reference_sha256) return generatedPreview;
            if (generatedPreview != null) Destroy(generatedPreview);
            generatedPreview = null;
            generatedPreviewHash = null;
            if (!VisualReferenceCache.TryRead(selected, store.DirectoryFor(selected), out var bytes)) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes, false)) { Destroy(texture); return null; }
            generatedPreview = texture;
            generatedPreviewHash = selected.visual_reference_sha256;
            return texture;
        }

        private void DrawCard()
        {
            if (selected == null) { page = Page.Library; return; }
            var r = new Rect(Screen.width * .14f, 120, Screen.width * .72f, Screen.height - 160);
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.Label(new Rect(r.x + 25, r.y + 20, r.width - 50, 40), "Fiche du sort", titleStyle);
            var visual = GeneratedReferencePreview();
            var visualWidth = visual != null ? Mathf.Min(330, r.width * .36f) : 0;
            if (visual != null)
            {
                GUI.DrawTexture(new Rect(r.xMax - visualWidth - 25, r.y + 130, visualWidth, Mathf.Max(120, r.height - 265)), visual, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.xMax - visualWidth - 25, r.yMax - 128, visualWidth, 25), "Image de référence du sort", textStyle);
            }
            try
            {
                var packet = JObject.Parse(spellJson);
                var display = packet["display"];
                GUI.Label(new Rect(r.x + 25, r.y + 75, r.width - 50, 43), display?["title"]?.ToString() ?? "Sort", titleStyle);
                GUI.Label(new Rect(r.x + 25, r.y + 130, r.width - 50 - visualWidth, 120), display?["factual_description"]?.ToString() ?? "", textStyle);
                var lines = display?["mechanical_lines"] as JArray;
                if (lines != null)
                {
                    var y = r.y + 260;
                    foreach (var line in lines)
                    {
                        if (y > r.yMax - 120) break;
                        GUI.Label(new Rect(r.x + 28, y, r.width - 56 - visualWidth, 38), "• " + line, textStyle);
                        y += 42;
                    }
                }
            }
            catch (Exception ex) { notice = "Paquet local illisible : " + ex.Message; }
            if (descriptionView != null &&
                GUI.Button(new Rect(r.x + 25, r.yMax - 119, 260, 39), "Description et image du sort", buttonStyle))
                page = Page.Interpretation;
            if (GUI.Button(new Rect(r.x + 25, r.yMax - 70, 260, 45), "Lancer dans le laboratoire", buttonStyle)) EnterLab();
            if (GUI.Button(new Rect(r.x + 300, r.yMax - 70, 180, 45), "Bibliothèque", buttonStyle)) page = Page.Library;
        }

        private void DrawLabHud()
        {
            if (lab != null) lab.InputSuppressed = showLabFeedback;
            if (showLabFeedback) { DrawLabFeedback(); return; }
            GUI.DrawTexture(new Rect(20, 105, 370, 345), labHudBackground);
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
            GUI.Label(new Rect(37, 381, 340, 45), "Clic : lancer · clic droit : orbiter\nMolette : zoom · RAZ : annuler", textStyle);
            if (descriptionView != null && GUI.Button(new Rect(37, 335, 325, 38),
                    "Lire ou signaler l'interprétation", buttonStyle))
            {
                showLabFeedback = true;
                if (lab != null) lab.InputSuppressed = true;
            }
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

        private void DrawLabFeedback()
        {
            var area = new Rect(Screen.width * .08f, 105, Screen.width * .84f, Screen.height - 128);
            GUI.Box(area, GUIContent.none, panelStyle);
            var previewWidth = Mathf.Min(250, area.width * .27f);
            var preview = LocalCapturePreview();
            if (preview != null)
                GUI.DrawTexture(new Rect(area.x + 16, area.y + 22, previewWidth - 20, previewWidth - 20),
                    preview, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(area.x + 16, area.y + previewWidth + 5, previewWidth - 20, 78),
                "Votre dessin et la lecture d'Astra", textStyle);
            var rightX = area.x + previewWidth + 10;
            var rightWidth = area.width - previewWidth - 27;
            DrawInterpretationPanel(new Rect(rightX, area.y + 12, rightWidth, area.height - 214));
            DrawFeedbackForm(new Rect(rightX + 12, area.yMax - 193, rightWidth - 24, 177));
            if (GUI.Button(new Rect(area.x + 16, area.yMax - 50, previewWidth - 20, 36),
                    "Retour au sort", buttonStyle))
            {
                showLabFeedback = false;
                if (lab != null) lab.InputSuppressed = false;
            }
        }

        private IEnumerator BootstrapSession(bool openDrawing)
        {
            if (busy) yield break;
            busy = true;
            notice = "Ouverture du laboratoire…";
            if (string.IsNullOrEmpty(serviceUrl) &&
                !ServiceAccess.TryLoadServiceUrl(Path.Combine(Application.streamingAssetsPath, "service.json"), out serviceUrl))
                ServiceAccess.TryLoadServiceUrl(LocalServicePath(), out serviceUrl);
            var accessPath = Path.Combine(AccessDirectory(), "access.json");
            var pendingInvitation = File.Exists(accessPath);
            var hasInvitation = ServiceAccess.TryLoadInvitation(accessPath, serviceUrl,
                out var invitationUrl, out var invitationCode);
            if (pendingInvitation)
            {
                // A newly installed invitation may belong to another player on this PC.
                // Hide the previous player's library before any network request.
                principalId = null;
                records.Clear();
                selected = null;
                spellJson = null;
                descriptionView = null;
                api.Clear();
                token = "";
                page = Page.Library;
                if (!hasInvitation)
                {
                    notice = "Invitation invalide ou destinée à un autre laboratoire.";
                    busy = false;
                    yield break;
                }
            }
            if (string.IsNullOrEmpty(serviceUrl) && hasInvitation) serviceUrl = invitationUrl;
            if (!LabApi.AllowedServiceUrl(serviceUrl))
            {
                notice = "Accès au laboratoire nécessaire. Installez l'invitation remise avec le jeu.";
                busy = false;
                yield break;
            }
            if (string.IsNullOrEmpty(token) && !hasInvitation)
                WindowsCredentialStore.TryRead(serviceUrl, out token, out _);
            if (string.IsNullOrEmpty(token) && hasInvitation)
            {
                var redeemed = false;
                yield return RedeemAccess(accessPath, invitationCode, success => redeemed = success);
                invitationCode = null;
                hasInvitation = false;
                if (!redeemed)
                {
                    notice = "Invitation non acceptée. Vérifiez l'accès remis avec le jeu.";
                    busy = false;
                    yield break;
                }
            }
            if (string.IsNullOrEmpty(token))
            {
                notice = "Accès au laboratoire nécessaire. Les sorts déjà enregistrés restent jouables hors ligne.";
                busy = false;
                yield break;
            }
            try { api.Configure(serviceUrl, token); }
            catch (Exception) { notice = "Configuration du laboratoire invalide."; busy = false; yield break; }
            CapabilitiesDto caps = null;
            string error = null;
            long status = 0;
            yield return api.GetCapabilities((c, e, s) => { caps = c; error = e; status = s; });
            if (status == 401)
            {
                WindowsCredentialStore.TryDelete(serviceUrl, out _);
                token = "";
                principalId = null;
                records.Clear();
                api.Clear();
                if (hasInvitation)
                {
                    var redeemed = false;
                    yield return RedeemAccess(accessPath, invitationCode, success => redeemed = success);
                    invitationCode = null;
                    if (redeemed)
                    {
                        api.Configure(serviceUrl, token);
                        caps = null; error = null; status = 0;
                        yield return api.GetCapabilities((c, e, s) => { caps = c; error = e; status = s; });
                    }
                }
                if (status == 401 || string.IsNullOrEmpty(token))
                {
                    notice = "Accès au laboratoire à renouveler. Ouvrez une nouvelle invitation.";
                    busy = false;
                    yield break;
                }
            }
            if (error != null || caps == null || caps.layout_version != "free_canvas_v2" ||
                !Guid.TryParseExact(caps.principal_id, "N", out _))
            {
                api.Clear();
                notice = "Laboratoire indisponible ou incompatible. Les sorts de ce lecteur restent hors ligne si l'identité est connue.";
                busy = false;
                yield break;
            }
            principalId = caps.principal_id;
            RefreshVisibleRecords();
            try { ServiceAccess.SavePrincipalId(LocalIdentityPath(), serviceUrl, token, principalId); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { Debug.LogWarning("PALIMPSESTE_IDENTITY_CACHE_UNAVAILABLE"); }
            byte[] bytes = null;
            string hash = null;
            yield return api.GetArtifact(caps.reference_artifact_id, (b, h, e) => { bytes = b; hash = h; error = e; });
            if (error != null || bytes == null)
            { api.Clear(); notice = "Référence du laboratoire indisponible."; busy = false; yield break; }
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!image.LoadImage(bytes, false) || image.width != DrawingCanvas.Size || image.height != DrawingCanvas.Size)
            { api.Clear(); notice = "Image de référence incompatible"; Destroy(image); busy = false; yield break; }
            if (reference != null) Destroy(reference);
            reference = image;
            referenceBytes = bytes;
            notice = "Laboratoire prêt. Votre dessin peut commencer.";
            busy = false;
            if (openDrawing)
            {
                var ongoing = records.Find(item => item.state == "blank" || item.state == "writing" ||
                    item.needs_capture || (!string.IsNullOrEmpty(item.job_id) && item.state != "ready" &&
                                           item.state != "capture_corrupted"));
                if (ongoing != null) Open(ongoing);
                else yield return NewParchment();
            }
        }

        private IEnumerator ReconnectToPendingParchment(string parchmentId)
        {
            if (busy || string.IsNullOrEmpty(parchmentId)) yield break;
            yield return BootstrapSession(false);
            if (!api.Configured) yield break;
            // BootstrapSession reloads the library under the authenticated principal.
            // Never reopen a stale record captured before that identity check.
            var restored = records.Find(item => item.parchment_id == parchmentId &&
                PlayerParchmentFilter.IsUserParchment(item, principalId));
            if (restored != null) Open(restored);
        }

        private IEnumerator RedeemAccess(string accessPath, string invitationCode, Action<bool> done)
        {
            string redeemedToken = null;
            string error = null;
            yield return api.RedeemInvitation(serviceUrl, invitationCode,
                (value, failure) => { redeemedToken = value; error = failure; });
            if (error != null || string.IsNullOrEmpty(redeemedToken))
            {
                done(false);
                yield break;
            }
            token = redeemedToken;
            if (WindowsCredentialStore.TryWrite(serviceUrl, token, out _))
            {
                try
                {
                    ServiceAccess.SaveServiceUrl(LocalServicePath(), serviceUrl);
                    File.Delete(accessPath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                { notice = "Accès ouvert, mais nettoyage local à vérifier."; }
                Debug.Log("PALIMPSESTE_CREDENTIAL_WRITE_OK");
            }
            else notice = "Accès temporaire ouvert ; conservation locale indisponible.";
            done(true);
        }

        private IEnumerator NewParchment()
        {
            if (!api.Configured || string.IsNullOrEmpty(principalId) || referenceBytes == null)
            { notice = "Accès au laboratoire nécessaire avant de dessiner."; yield break; }
            busy = true;
            ParchmentDto remote = null;
            string error = null;
            yield return api.Allocate(Guid.NewGuid().ToString("N"), (p, e) => { remote = p; error = e; });
            busy = false;
            if (error != null || remote == null) { notice = "Allocation impossible : " + error; yield break; }
            if (remote.layout_version != "free_canvas_v2")
            {
                notice = "Support incompatible avec le dessin libre.";
                yield break;
            }
            var record = store.Create(remote.parchment_id);
            record.owner_id = principalId;
            record.server_issued = true;
            record.requires_description_before_lab = true;
            record.reference_artifact_id = "server";
            record.reference_sha256 = ParchmentStore.Hash(referenceBytes);
            File.WriteAllBytes(Path.Combine(store.DirectoryFor(record), "reference.png"), referenceBytes);
            store.Save(record);
            records.Insert(0, record);
            Open(record);
        }

        private void Open(ParchmentRecord record)
        {
            if (!PlayerParchmentFilter.IsUserParchment(record, principalId)) return;
            selected = record;
            interpretationScroll = Vector2.zero;
            descriptionView = null;
            spellJson = null;
            feedbackDraft = record.feedback_correction ?? "";
            showLabFeedback = false;
            if (record.server_issued || !string.IsNullOrEmpty(record.job_id))
                DescriptionCache.TryLoad(record, store.DirectoryFor(record), out descriptionView);
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
            if (!string.IsNullOrEmpty(record.spell_id) && !cachedSpellMissing)
            {
                page = record.requires_description_before_lab && descriptionView == null
                    ? Page.Processing : Page.Card;
                if (page == Page.Processing && api.Configured && !string.IsNullOrEmpty(record.job_id))
                    StartCoroutine(Poll(record));
            }
            else if ((record.state == "blank" || record.state == "writing") && reference != null)
                page = Page.Drawing;
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

        private void ImportInvitation()
        {
            if (!WindowsInvitationPicker.TryChoose(out var source)) return;
            if (!ServiceAccess.TryLoadInvitation(source, serviceUrl, out var invitationUrl, out _))
            {
                notice = "Invitation invalide ou destinée à un autre laboratoire.";
                return;
            }
            var destination = Path.Combine(AccessDirectory(), "access.json");
            var temp = destination + ".tmp";
            try
            {
                Directory.CreateDirectory(AccessDirectory());
                File.Copy(source, temp, true);
                if (File.Exists(destination)) File.Replace(temp, destination, null);
                else File.Move(temp, destination);
                if (string.IsNullOrEmpty(serviceUrl)) serviceUrl = invitationUrl;
                api.Clear();
                token = "";
                principalId = null;
                records.Clear();
                selected = null;
                spellJson = null;
                descriptionView = null;
                page = Page.Library;
                StartCoroutine(BootstrapSession(true));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { notice = "Invitation non installée. Réessayez ou contactez le laboratoire."; }
        }

        private void FinalizeDrawing(string reason)
        {
            if (selected == null || canvas == null) { page = Page.Library; return; }
            if (!canvas.Engaged)
            {
                notice = EmptyDrawingWarning;
                return;
            }
            if (canvas.IsDrawing) { Append("up", Vector2.zero); canvas.End(); }
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
                layout_version = string.IsNullOrEmpty(record.layout_version) ? "three_regions_v1" : record.layout_version,
                reference_sha256 = record.reference_sha256,
                raster_version = string.IsNullOrEmpty(record.raster_version) ? DrawingCanvas.LegacyRasterVersion : record.raster_version,
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
            string errorCode = null;
            yield return api.Upload(record, capture, drawing, ink, journal,
                (j, e, code) => { job = j; error = e; errorCode = code; });
            busy = false;
            if (error != null || job == null)
            {
                if (errorCode == "capture_incompatible")
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
            if (!c.AllLocked && !c.IsLocked(InkRegion.Core) && !c.IsLocked(InkRegion.Ring) && !c.IsLocked(InkRegion.Outer))
                return Array.Empty<string>();
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
            if (record == null || string.IsNullOrEmpty(record.job_id) || !pollingJobs.Add(record.job_id)) yield break;
            var jobId = record.job_id;
            try
            {
                while (record.job_id == jobId && api.Configured)
                {
                    JobDto job = null;
                    string error = null;
                    yield return api.GetJob(jobId, (j, e) => { job = j; error = e; });
                    if (error != null || job == null)
                    {
                        if (selected == record) notice = "Suivi interrompu. Votre trace reste enregistrée ; rouvrez le parchemin pour réessayer.";
                        yield break;
                    }
                    if (!string.IsNullOrEmpty(job.parchment_id) && job.parchment_id != record.parchment_id)
                    {
                        if (selected == record) notice = "Identité de la tâche incohérente ; suivi interrompu.";
                        yield break;
                    }
                    record.state = job.state;
                    record.resume_stage = job.resume_stage;
                    record.last_job_message = job.message;
                    record.last_job_error_code = job.error_code;
                    record.job_retryable = job.retryable;
                    if (job.state != "needs_operator" || job.attempt_count > record.last_job_attempt_count)
                        record.resume_key = null;
                    record.last_job_attempt_count = job.attempt_count;
                    record.generation_elapsed_ms = job.elapsed_ms;
                    record.elapsed_observed_at = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(job.spell_id)) record.spell_id = job.spell_id;
                    store.Save(record);
                    if (selected == record && !string.IsNullOrEmpty(job.message)) notice = job.message;
                    if (!string.IsNullOrEmpty(job.description_artifact_id) &&
                        (record.description_artifact_id != job.description_artifact_id ||
                         !DescriptionCache.TryLoad(record, store.DirectoryFor(record), out _)))
                        yield return FetchDescription(record, job.description_artifact_id);
                    else if (selected == record && descriptionView == null)
                        DescriptionCache.TryLoad(record, store.DirectoryFor(record), out descriptionView);
                    if (!string.IsNullOrEmpty(job.visual_reference_artifact_id) &&
                        (record.visual_reference_sha256 != job.visual_reference_sha256 ||
                        !VisualReferenceCache.TryRead(record, store.DirectoryFor(record), out _)))
                        yield return FetchVisualReference(record, job.visual_reference_artifact_id, job.visual_reference_sha256);
                    if (job.state == "ready")
                    {
                        if (string.IsNullOrEmpty(record.spell_id))
                        {
                            if (selected == record) notice = "Sort annoncé sans identifiant ; suivi interrompu.";
                            yield break;
                        }
                        yield return Download(record);
                        yield break;
                    }
                    if (job.state == "needs_operator")
                    {
                        if (selected == record)
                            notice = "Le sort n'a pas encore pu être créé. Votre dessin est conservé.";
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(Mathf.Clamp(job.poll_after_ms / 1000f, 1f, 10f));
                }
            }
            finally { pollingJobs.Remove(jobId); }
        }

        private IEnumerator ResumeJob(ParchmentRecord record)
        {
            if (!api.Configured || busy || !CanResumeJob(record)) yield break;
            busy = true;
            if (string.IsNullOrEmpty(record.resume_key))
            {
                record.resume_key = Guid.NewGuid().ToString("N");
                store.Save(record);
            }
            JobDto job = null;
            string error = null;
            yield return api.ResumeJob(record.job_id, record.resume_key,
                (response, failure) => { job = response; error = failure; });
            busy = false;
            if (error != null || job == null)
            {
                if (selected == record)
                    notice = "Ce dessin est conservé. La reprise du sort n'a pas abouti : " + error;
                yield break;
            }
            if (job.job_id != record.job_id || job.parchment_id != record.parchment_id ||
                job.state is not ("queued" or "ready"))
            {
                if (selected == record) notice = "Réponse de reprise incohérente. Votre dessin est conservé.";
                yield break;
            }
            record.resume_key = null;
            record.state = job.state;
            record.resume_stage = job.resume_stage;
            record.last_job_message = job.message;
            record.last_job_error_code = job.error_code;
            record.job_retryable = job.retryable;
            record.last_job_attempt_count = job.attempt_count;
            if (!string.IsNullOrEmpty(job.spell_id)) record.spell_id = job.spell_id;
            store.Save(record);
            if (selected == record) notice = "Création du sort relancée avec votre dessin enregistré.";
            StartCoroutine(Poll(record));
        }

        private IEnumerator FetchDescription(ParchmentRecord record, string artifactId)
        {
            byte[] bytes = null;
            string hash = null;
            string error = null;
            yield return api.GetArtifact(artifactId, (b, h, e) => { bytes = b; hash = h; error = e; });
            if (error != null || bytes == null ||
                !DescriptionCache.TrySave(record, store.DirectoryFor(record), artifactId, hash, bytes, out var parsed))
            {
                if (selected == record) notice = "Lecture d'Astra indisponible ou invalide ; le suivi du sort continue.";
                yield break;
            }
            store.Save(record);
            if (selected == record) { descriptionView = parsed; interpretationScroll = Vector2.zero; }
        }

        private IEnumerator FetchVisualReference(ParchmentRecord record, string artifactId, string expectedHash)
        {
            byte[] bytes = null;
            string hash = null, error = null;
            yield return api.GetArtifact(artifactId, (b, h, e) => { bytes = b; hash = h; error = e; });
            if (error != null || hash != expectedHash ||
                !VisualReferenceCache.TrySave(record, store.DirectoryFor(record), artifactId, expectedHash, bytes))
            {
                if (selected == record) notice = "L’image du sort n’a pas encore été téléchargée. Le suivi continue.";
                yield break;
            }
            store.Save(record);
            if (selected == record) showGeneratedReference = true;
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
            if (packet["visual_reference"] is JObject visualReference) manifest.Add(visualReference);
            foreach (var item in manifest)
            {
                var id = item["artifact_id"]?.ToString();
                var expected = item["sha256"]?.ToString();
                if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_.-]{0,63}$"))
                { notice = "Référence d'artefact interdite"; yield break; }
                var artifactPath = Path.Combine(artifactDir, id);
                // The reference is normally fetched while B constructs the
                // spell. Reuse its verified bytes instead of downloading it twice.
                var cachedArtifact = false;
                try
                {
                    if (File.Exists(artifactPath) && new FileInfo(artifactPath).Length <= 8 * 1024 * 1024)
                        cachedArtifact = ParchmentStore.Hash(File.ReadAllBytes(artifactPath)) == expected;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                if (cachedArtifact) continue;
                byte[] data = null;
                yield return api.GetArtifact(id, (b, h, e) => { data = b; error = e; });
                if (error != null || data == null || ParchmentStore.Hash(data) != expected)
                { notice = "Artefact invalide : " + id + " " + error; yield break; }
                File.WriteAllBytes(Path.Combine(artifactDir, id), data);
            }
            if (packet["visual_reference"] is JObject downloadedVisual)
            {
                record.visual_reference_artifact_id = downloadedVisual["artifact_id"]?.ToString();
                record.visual_reference_sha256 = downloadedVisual["sha256"]?.ToString();
                if (!VisualReferenceCache.TryRead(record, store.DirectoryFor(record), out _))
                { notice = "Image de référence invalide ; téléchargement à reprendre."; yield break; }
            }
            else if (packet["versions"]?["min_client"]?.ToString() == "1.8.0" && packet["binary_assets"] is JArray v2Assets)
            {
                foreach (var item in v2Assets)
                {
                    if (item["file_name"]?.ToString() != "v2-animation-sheet.png") continue;
                    record.visual_reference_artifact_id = item["artifact_id"]?.ToString();
                    record.visual_reference_sha256 = item["sha256"]?.ToString();
                    if (!VisualReferenceCache.TryRead(record, store.DirectoryFor(record), out _))
                    { notice = "Planche V2 invalide ; téléchargement à reprendre."; yield break; }
                }
            }
            var spellPath = Path.Combine(store.DirectoryFor(record), "spell.json");
            try { ImageSpellPacketValidator.Validate(Encoding.UTF8.GetString(bytes), store.DirectoryFor(record)); }
            catch (Exception) { notice = "Construction du sort invalide ; votre dessin reste enregistré."; yield break; }
            File.WriteAllBytes(spellPath, bytes);
            File.WriteAllText(spellPath + ".sha256", ParchmentStore.Hash(bytes), Encoding.ASCII);
            record.state = "ready";
            store.Save(record);
            if (selected == record)
            {
                spellJson = Encoding.UTF8.GetString(bytes);
                page = descriptionView == null ? Page.Processing : Page.Card;
            }
            notice = descriptionView == null && selected == record
                ? "Sort vérifié. Lecture d'Astra encore indisponible ; actualisez pour l'afficher avant le laboratoire."
                : "Sort téléchargé et vérifié. Il est disponible hors ligne.";
        }

        private void EnterLab()
        {
            if (string.IsNullOrEmpty(spellJson) ||
                (selected != null && selected.requires_description_before_lab && descriptionView == null)) return;
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
