using UnityEngine;
using UnityEngine.UI;

public sealed class FacilityShiftDisplay : MonoBehaviour
{
    [SerializeField] private FacilityShiftController shift;
    [SerializeField] private NullLaboratoryController laboratory;
    [SerializeField] private SignalArchiveController archive;
    [SerializeField] private SkunkWorksCommissioning skunkWorks;
    private GameObject canvasRoot;
    private GameObject notebook;
    private Text location;
    private Text objective;
    private Text guidance;
    private Text measurement;
    private Text notebookBody;
    private Text notebookTitle;
    private Image progress;
    private Text progressLabel;
    private RectTransform assignmentCard;
    private RectTransform controlsStrip;

    private static readonly Color Ink = new(0.025f, 0.05f, 0.06f, 0.94f);
    private static readonly Color Accent = new(0.45f, 0.91f, 0.83f);

    public void Configure(FacilityShiftController controller) => shift = controller;
    public void ConfigureLaboratory(NullLaboratoryController controller) => laboratory = controller;
    public void ConfigureArchive(SignalArchiveController controller) => archive = controller;
    public void ConfigureSkunkWorks(SkunkWorksCommissioning controller) => skunkWorks = controller;

    private void Awake()
    {
        canvasRoot = new GameObject("Signal Watch Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasRoot.transform.SetParent(transform, false);
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform card = Panel("Current Assignment", canvasRoot.transform,
            new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(610f, 258f));
        assignmentCard = card;
        location = Label("Location", card, new Vector2(22f, -16f), new Vector2(566f, 28f), 19, Accent);
        objective = Label("Objective", card, new Vector2(22f, -54f), new Vector2(566f, 32f), 22, Color.white);
        guidance = Label("Guidance", card, new Vector2(22f, -96f), new Vector2(566f, 72f), 21, Color.white);
        measurement = Label("Measurement", card, new Vector2(22f, -182f), new Vector2(566f, 28f), 19, Accent);

        RectTransform progressTrack = Panel("Capture Track", card, new Vector2(0f, 1f),
            new Vector2(0f, -253f), new Vector2(610f, 5f));
        progress = progressTrack.GetComponent<Image>();
        progress.color = Accent;
        progressLabel = Label("Capture Status", card, new Vector2(22f, -220f), new Vector2(566f, 26f), 20, Accent);

        RectTransform controls = Panel("Field Controls", canvasRoot.transform,
            new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(395f, 48f));
        controls.pivot = new Vector2(1f, 1f);
        controlsStrip = controls;
        Text controlsText = Label("Hints", controls, new Vector2(16f, -10f), new Vector2(365f, 30f), 20, Color.white);
        controlsText.text = "TAB field notes     L inspection light";

        RectTransform notes = Panel("Field Notebook", canvasRoot.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 780f));
        notes.pivot = new Vector2(0.5f, 0.5f);
        notes.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.99f);
        notebook = notes.gameObject;
        notebookTitle = Label("Notebook Title", notes, new Vector2(34f, -28f), new Vector2(732f, 45f), 30, Accent);
        notebookTitle.text = "SIGNAL WATCH / FIELD NOTES";
        notebookBody = Label("Notebook Entries", notes, new Vector2(34f, -90f), new Vector2(732f, 650f), 22, Color.white);
        notebook.SetActive(false);
    }

    private void LateUpdate()
    {
        bool visible = shift != null && !RuntimeSceneSwitcher.IsOpen;
        canvasRoot.SetActive(visible);
        if (!visible) return;
        bool inSkunkWorks = skunkWorks != null && skunkWorks.PlayerInArea;
        bool inArchive = archive != null && archive.PlayerInArea;
        bool inLab = laboratory != null && laboratory.PlayerInArea;
        location.text = $"{shift.LocationName}  /  LIGHT {(shift.FlashlightOn ? "ON" : "OFF")}";
        objective.text = inSkunkWorks ? skunkWorks.ObjectiveTitle : inArchive ? archive.ObjectiveTitle : inLab ? laboratory.ObjectiveTitle : shift.ObjectiveTitle;
        guidance.text = inSkunkWorks ? skunkWorks.Guidance : inArchive ? archive.Guidance : inLab ? laboratory.Guidance : shift.Guidance;
        measurement.text = inSkunkWorks ? skunkWorks.Measurement : inArchive ? archive.Measurement : inLab ? laboratory.Measurement : shift.Measurement;
        float capture = inSkunkWorks ? skunkWorks.CaptureProgress01 : inArchive ? archive.PlaybackProgress01 : inLab ? laboratory.CaptureProgress01 : shift.CaptureProgress01;
        progress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 610f * capture);
        progressLabel.text = inSkunkWorks ? skunkWorks.CaptureStatus : inArchive ? (archive.IsPerforming ? $"PLAYBACK  /  {capture * 100f:0}%"
            : capture >= 1f ? "RECORDING RECOVERED" : "")
            : capture > 0f ? $"HOLD STEADY  /  CAPTURING {capture * 100f:0}%" : "";
        notebook.SetActive(shift.NotebookOpen);
        assignmentCard.gameObject.SetActive(!shift.NotebookOpen);
        controlsStrip.gameObject.SetActive(!shift.NotebookOpen);
        bool narrow = canvasRoot.GetComponent<RectTransform>().rect.width < 1120f;
        controlsStrip.anchorMin = controlsStrip.anchorMax = new Vector2(narrow ? 0f : 1f, 1f);
        controlsStrip.pivot = controlsStrip.anchorMin;
        controlsStrip.anchoredPosition = narrow ? new Vector2(32f, -308f) : new Vector2(-32f, -32f);
        if (!shift.NotebookOpen) return;
        notebookTitle.text = inSkunkWorks ? "FIRST LIGHT / SKUNK WORKS" : inArchive ? "SIGNAL ARCHIVE / FIELD NOTES"
            : inLab ? "NULL REFERENCE / FIELD NOTES" : "SIGNAL WATCH / FIELD NOTES";
        if (inSkunkWorks)
        {
            notebookBody.text = skunkWorks.Notes;
            return;
        }
        if (inArchive)
        {
            notebookBody.text = archive.Notes;
            return;
        }
        if (inLab)
        {
            notebookBody.text = laboratory.Notes;
            return;
        }

        string[] jobs = { "Capture the chamber reference", "Acquire the satellite",
            "Collect Recorder 07 on the ridge", "File the report at the DSN racks" };
        string checklist = "";
        for (int i = 0; i < jobs.Length; i++)
        {
            string state = i < (int)shift.Stage ? "DONE" : i == (int)shift.Stage ? "NEXT" : "     ";
            checklist += $"{state}  {i + 1:00}  {jobs[i]}\n";
        }
        int minutes = (int)shift.ElapsedSeconds / 60;
        int seconds = (int)shift.ElapsedSeconds % 60;
        notebookBody.text = "One quiet shift. Four checks. A complete signal chain.\n\n"
            + checklist + "\n" + shift.LatestEntry + "\n\n"
            + shift.Measurement + $"\nElapsed {minutes:00}:{seconds:00}\n\n"
            + "Signs mark the route through the facility. The truck waits for one W press at either stop.\n\n"
            + "TAB closes notes. Exploration remains available after handover.";
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject item = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = item.GetComponent<Image>();
        image.color = Ink;
        image.raycastTarget = false;
        return rect;
    }

    private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        GameObject item = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        item.transform.SetParent(parent, false);
        Text text = item.GetComponent<Text>();
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
        text.rectTransform.pivot = new Vector2(0f, 1f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
