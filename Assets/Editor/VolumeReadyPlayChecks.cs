using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VolumeReadyPlayChecks
{
    private const string Flag = "PacStudent.VolumeReadyPlayChecks";
    private static float expectedVolume;
    private const double TimeoutSeconds = 60d;
    private static readonly List<string> passed = new List<string>();
    private static readonly Dictionary<int, float> sourceVolumes = new Dictionary<int, float>();
    private static readonly Dictionary<int, bool> sourceIgnoreListenerVolumes = new Dictionary<int, bool>();
    private static double started, stageStarted;
    private static int stage;
    private static string pendingScene;
    private static string arrowLevel, previousArrowMessage;
    private static bool countdownStaticChecked, arrowCaptureDone, goArrowChecked;
    private static readonly Dictionary<string, int> arrowStates = new Dictionary<string, int>();
    private static readonly HashSet<string> arrowGeometryChecked = new HashSet<string>();

    static VolumeReadyPlayChecks() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

    [MenuItem("Boba Kingdom/Run volume and Ready Play Mode checks")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("請先停止目前的 Play Mode，再執行音量與 Ready 檢查。");
        string originalPath = SceneManager.GetActiveScene().path;
        if (!Application.isBatchMode && SceneManager.sceneCount != 1)
            throw new InvalidOperationException("請只保留一個已儲存場景，再執行音量與 Ready 檢查。");
        if (string.IsNullOrEmpty(originalPath) && !Application.isBatchMode)
            throw new InvalidOperationException("目前場景尚未儲存，無法在檢查後還原。");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("請先儲存所有已開啟場景，再執行音量與 Ready 檢查。");
        SessionState.SetString(Flag + ".OriginalScenePath", originalPath);
        const string startPath = "Assets/Scenes/StartScene.unity";
        if (originalPath != startPath) EditorSceneManager.OpenScene(startPath, OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        WriteReport("RUNNING");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            passed.Clear(); sourceVolumes.Clear(); sourceIgnoreListenerVolumes.Clear();
            stage = 0; pendingScene = null;
            arrowLevel = null; previousArrowMessage = null; countdownStaticChecked = false;
            arrowCaptureDone = false; goArrowChecked = false; arrowStates.Clear(); arrowGeometryChecked.Clear();
            started = stageStarted = EditorApplication.timeSinceStartup;
            Application.runInBackground = true;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            string reportPath = ReportPath();
            if (File.Exists(reportPath) && File.ReadAllText(reportPath).StartsWith("RUNNING"))
                WriteReport(File.ReadAllText(reportPath).Replace("RUNNING", "INTERRUPTED: Play Mode stopped before completion"));
            RestoreOriginalScene();
            SessionState.SetBool(Flag, false);
            SessionState.EraseString(Flag + ".OriginalScenePath");
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - started > TimeoutSeconds) throw new Exception("檢查超過 60 秒。");
            if (pendingScene != null)
            {
                if (now - stageStarted < 0.1) return;
                string scene = pendingScene; pendingScene = null;
                arrowLevel = null; previousArrowMessage = null; countdownStaticChecked = false;
                arrowCaptureDone = false; goArrowChecked = false; arrowStates.Clear(); arrowGeometryChecked.Clear();
                LoadScene(scene); Next(now); return;
            }
            if (stage == 0) CheckMenu(now);
            else if (stage == 1) CheckLevel1Countdown(now);
            else if (stage == 2) CheckLevel2Countdown(now);
            else if (stage == 3) CheckMenuPersistence(now);
        }
        catch (Exception error) { Finish("FAILED: " + error.Message); }
    }

    private static void CheckMenu(double now)
    {
        if (now - stageStarted < 0.6) return;
        Check(SceneManager.GetActiveScene().name == "StartScene", "主選單場景已載入");
        expectedVolume = AudioListener.volume;
        CheckVolume("主選單"); CheckNoVolumeControls("主選單");
        Check(FindSpawnArrow() == null, "主選單沒有 Spawn Arrow");
        SnapshotAudioSources(); CheckAudioSourcesPreserved();
        Capture("volume-ready-menu.png");
        pendingScene = "Level1Scene"; stageStarted = now;
    }

    private static void CheckLevel1Countdown(double now)
    {
        if (SceneManager.GetActiveScene().name != "Level1Scene") return;
        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (ui == null) return;
        ObserveArrow(ui, "L1");
        if (ui.State == UIManager.RoundState.Running) { CheckLevel1Running(now); return; }
        if (ui.State != UIManager.RoundState.Countdown) return;
        TMP_Text message = UiText(ui, "roundMessage");
        if (!countdownStaticChecked && message != null && message.text == "3")
        {
            CheckVolume("第一關"); CheckNoVolumeControls("第一關"); SnapshotAudioSources();
            TMP_Text details = UiText(ui, "roundDetails");
            Check(details != null && details.text == "Get ready\n\n<size=21>WASD to move</size>", "第一關 Ready 顯示 WASD 操作");
            CheckReadyText(details, message, "第一關 Ready 文字");
            PacStudentController player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
            Vector3 position = player == null ? Vector3.zero : player.transform.position;
            if (player != null) player.SetInput(Vector3.right);
            Check(player == null || player.transform.position == position, "第一關倒數期間禁止移動");
            Check(ui.ElapsedTime == 0f, "第一關倒數期間計時維持零");
            countdownStaticChecked = true;
        }
    }

    private static void CheckLevel1Running(double now)
    {
        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (ui == null || ui.State != UIManager.RoundState.Running || ui.ElapsedTime < 0.1f) return;
        CheckVolume("第一關遊玩"); CheckNoVolumeControls("第一關遊玩");
        CheckAudioSourcesPreserved(); Check(!FindBlockingImage(ui).activeSelf, "第一關 Ready 遮罩關閉");
        Check(ui.ElapsedTime > 0f, "第一關開始後計時運作");
        CheckCountdownArrowComplete("L1", "第一關");
        pendingScene = "InnovationScene"; stageStarted = now;
    }

    private static void CheckLevel2Countdown(double now)
    {
        if (SceneManager.GetActiveScene().name != "InnovationScene") return;
        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        InnovationController innovation = UnityEngine.Object.FindFirstObjectByType<InnovationController>();
        if (ui == null || innovation == null) return;
        ObserveArrow(ui, "L2");
        if (ui.State == UIManager.RoundState.Running) { CheckLevel2Running(now); return; }
        if (ui.State != UIManager.RoundState.Countdown) return;
        TMP_Text message = UiText(ui, "roundMessage");
        if (!countdownStaticChecked && message != null && message.text == "3")
        {
            CheckVolume("第二關"); CheckNoVolumeControls("第二關"); SnapshotAudioSources();
            TMP_Text details = UiText(ui, "roundDetails");
            Check(details != null && details.text == "Get ready\n\n<size=21>WASD to move\nSPACE to shoot</size>", "第二關 Ready 顯示 WASD 與 SPACE 操作");
            CheckReadyText(details, message, "第二關 Ready 文字");
            Check(!innovation.TryShoot() && innovation.ActiveShotCount == 0, "第二關倒數期間禁止射擊");
            Check(ui.ElapsedTime == 0f, "第二關倒數期間計時維持零");
            countdownStaticChecked = true;
        }
    }

    private static void CheckLevel2Running(double now)
    {
        UIManager ui = UnityEngine.Object.FindFirstObjectByType<UIManager>();
        if (ui == null || ui.State != UIManager.RoundState.Running || ui.ElapsedTime < 0.1f) return;
        CheckVolume("第二關遊玩"); CheckNoVolumeControls("第二關遊玩");
        CheckAudioSourcesPreserved(); Check(!FindBlockingImage(ui).activeSelf, "第二關 Ready 遮罩關閉");
        Check(ui.ElapsedTime > 0f, "第二關開始後計時運作");
        CheckCountdownArrowComplete("L2", "第二關");
        pendingScene = "StartScene"; stageStarted = now;
    }

    private static void CheckMenuPersistence(double now)
    {
        if (SceneManager.GetActiveScene().name != "StartScene" || now - stageStarted < 0.7) return;
        CheckVolume("返回主選單"); CheckNoVolumeControls("返回主選單");
        Check(FindSpawnArrow() == null, "返回主選單沒有 Spawn Arrow"); Finish("PASS");
    }

    private static void CheckVolume(string label)
    {
        Check(Mathf.Abs(AudioListener.volume - expectedVolume) < 0.001f, label + "總音量維持開局設定");
    }

    private static void CheckNoVolumeControls(string label)
    {
        Transform[] objects = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(!objects.Any(item => item.name == "Volume Control" || item.name.StartsWith("Volume ")), label + "沒有音量調整介面");
    }

    private static void SnapshotAudioSources()
    {
        sourceVolumes.Clear(); sourceIgnoreListenerVolumes.Clear();
        foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int id = source.GetInstanceID();
            sourceVolumes[id] = source.volume; sourceIgnoreListenerVolumes[id] = source.ignoreListenerVolume;
            Check(!source.ignoreListenerVolume, "AudioSource 未繞過 AudioListener 音量");
        }
    }

    private static void CheckAudioSourcesPreserved()
    {
        AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(sources.Length == sourceVolumes.Count, "場景內 AudioSource 數量保持不變");
        foreach (AudioSource source in sources)
        {
            int id = source.GetInstanceID();
            Check(sourceVolumes.ContainsKey(id) && Mathf.Abs(source.volume - sourceVolumes[id]) < 0.001f, "AudioSource 原有相對音量保持不變");
            Check(sourceIgnoreListenerVolumes.ContainsKey(id) && !source.ignoreListenerVolume, "AudioSource ignoreListenerVolume 為 false");
        }
    }

    private static void CheckReadyText(TMP_Text details, TMP_Text message, string label)
    {
        Canvas.ForceUpdateCanvases();
        Check(details != null, label + " 已指定");
        if (details == null) return;
        details.ForceMeshUpdate();
        Check(!details.isTextOverflowing, label + " 沒有文字溢出");
        Check(details.text.Contains("Get ready\n\n<size=21>"), label + " 在 Get ready 後有空白行且提示字級為 21");
        if (message == null) return;
        message.ForceMeshUpdate();
        Check(!message.isTextOverflowing, label + " 的倒數文字沒有溢出");
        Check(!WorldBounds(details.rectTransform).Overlaps(WorldBounds(message.rectTransform)), label + " 與倒數文字沒有重疊");
    }

    private static void ObserveArrow(UIManager ui, string level)
    {
        TMP_Text messageText = UiText(ui, "roundMessage");
        string message = messageText == null ? string.Empty : messageText.text;
        bool messageChanged = false;
        if (arrowLevel != level)
        {
            arrowLevel = level;
            previousArrowMessage = null;
        }
        if (message != previousArrowMessage)
        {
            previousArrowMessage = message;
            messageChanged = true;
        }
        if (message != "3" && message != "2" && message != "1" && message != "GO!") return;

        GameObject arrow = FindSpawnArrow();
        if (message == "GO!")
        {
            if (goArrowChecked) return;
            Check(arrow == null || !arrow.GetComponent<MaskableGraphic>().enabled, level + " GO! 時箭頭不再顯示");
            goArrowChecked = true;
            return;
        }

        if (arrow == null)
        {
            Check(false, level + " 倒數 " + message + " 時找不到 Spawn Arrow");
            return;
        }
        MaskableGraphic graphic = arrow.GetComponent<MaskableGraphic>();
        if (graphic == null) throw new Exception(level + " Spawn Arrow 缺少 MaskableGraphic");

        string key = level + ":" + message;
        int state = arrowStates.TryGetValue(key, out int recorded) ? recorded : 0;
        arrowStates[key] = state | (graphic.enabled ? 1 : 2);
        if (!messageChanged && graphic.enabled && !arrowGeometryChecked.Contains(key))
        {
            CheckArrowGeometry(arrow, level + " 倒數 " + message);
            arrowGeometryChecked.Add(key);
        }
        if (message == "2" && graphic.enabled && !arrowCaptureDone)
        {
            Capture(level == "L1" ? "volume-ready-level1.png" : "volume-ready-level2.png");
            arrowCaptureDone = true;
        }
    }

    private static void CheckArrowGeometry(GameObject arrow, string label)
    {
        Canvas.ForceUpdateCanvases();
        MaskableGraphic graphic = arrow.GetComponent<MaskableGraphic>();
        RectTransform rect = arrow.GetComponent<RectTransform>();
        Canvas canvas = arrow.GetComponentInParent<Canvas>()?.rootCanvas;
        Check(graphic != null && !graphic.raycastTarget, label + " Spawn Arrow 不攔截射線");
        Check(rect != null && Mathf.Abs(rect.rect.width - 36f) < 0.5f && Mathf.Abs(rect.rect.height - 44f) < 0.5f,
            label + " Spawn Arrow 尺寸為 36x44");
        Check(rect != null && Vector2.Distance(rect.pivot, new Vector2(0.5f, 0f)) < 0.01f,
            label + " Spawn Arrow pivot 為底部中央");
        Check(canvas != null && rect != null && rect.parent == canvas.transform && rect.GetSiblingIndex() == canvas.transform.childCount - 1,
            label + " Spawn Arrow 是 rootCanvas 最後一個 child");

        GameObject blocking = FindBlockingImage(UnityEngine.Object.FindFirstObjectByType<UIManager>());
        if (blocking != null && canvas != null && blocking.transform.parent == canvas.transform)
            Check(rect.GetSiblingIndex() > blocking.transform.GetSiblingIndex(), label + " Spawn Arrow 位於暗幕上方");

        PacStudentController player = UnityEngine.Object.FindFirstObjectByType<PacStudentController>();
        Camera camera = Camera.main;
        Check(player != null && camera != null, label + " 可取得玩家與主攝影機定位箭頭");
        if (player == null || camera == null || rect == null || canvas == null) return;

        Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
        Rect screenRect = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        Vector3[] canvasCorners = new Vector3[4]; canvasRect.GetWorldCorners(canvasCorners);
        Vector2 canvasBottomLeft = RectTransformUtility.WorldToScreenPoint(canvasCamera, canvasCorners[0]);
        Vector2 canvasTopRight = RectTransformUtility.WorldToScreenPoint(canvasCamera, canvasCorners[2]);
        Rect screenCanvas = Rect.MinMaxRect(canvasBottomLeft.x, canvasBottomLeft.y, canvasTopRight.x, canvasTopRight.y);
        Vector3 playerScreen = camera.WorldToScreenPoint(player.transform.position);
        Check(Mathf.Abs(screenRect.center.x - playerScreen.x) <= 3f, label + " Spawn Arrow 水平對齊玩家");
        Check(screenRect.yMin > playerScreen.y, label + " Spawn Arrow 底端位於玩家上方");
        float margin = 5.25f * canvas.scaleFactor;
        Check(screenRect.xMin >= screenCanvas.xMin + margin && screenRect.xMax <= screenCanvas.xMax - margin
            && screenRect.yMin >= screenCanvas.yMin + margin && screenRect.yMax <= screenCanvas.yMax - margin,
            label + " Spawn Arrow 保持縮放後的安全邊距");
    }

    private static void CheckCountdownArrowComplete(string level, string label)
    {
        Check(countdownStaticChecked, label + " Ready 靜態檢查已完成");
        foreach (string digit in new[] { "3", "2", "1" })
        {
            string key = level + ":" + digit;
            Check(arrowStates.TryGetValue(key, out int state) && state == 3,
                label + " 倒數 " + digit + " 箭頭各觀測到亮滅");
        }
        Check(arrowCaptureDone, label + " 已擷取箭頭亮起的 2 畫面");
        Check(goArrowChecked, label + " GO! 時箭頭不再顯示");
        Check(FindSpawnArrow() == null, label + " Running 時沒有 Spawn Arrow");
    }

    private static GameObject FindSpawnArrow()
    {
        Transform arrow = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == "Spawn Arrow");
        return arrow == null ? null : arrow.gameObject;
    }

    private static TMP_Text UiText(UIManager ui, string fieldName)
    {
        var field = typeof(UIManager).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field == null ? null : field.GetValue(ui) as TMP_Text;
    }

    private static GameObject FindBlockingImage(UIManager ui)
    {
        var field = typeof(UIManager).GetField("blockingImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field == null ? null : field.GetValue(ui) as GameObject;
    }

    private static Rect WorldBounds(RectTransform rect)
    {
        Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return new Rect(corners[0], corners[2] - corners[0]);
    }

    private static void LoadScene(string scene) { Time.timeScale = 1f; SceneManager.LoadScene(scene); }

    private static void Next(double now)
    {
        stage++; stageStarted = now;
        WriteReport("RUNNING: stage " + stage + "\n" + string.Join("\n", passed));
    }

    private static void Capture(string file)
    {
        string path = Path.Combine(Path.GetDirectoryName(ReportPath()), file);
        if (!Application.isBatchMode) { ScreenCapture.CaptureScreenshot(path); return; }
        Camera camera = Camera.main;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        Camera[] previousCameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        float[] previousDistances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        int[] previousOrders = canvases.Select(canvas => canvas.sortingOrder).ToArray();
        int[] previousLayers = canvases.Select(canvas => canvas.sortingLayerID).ToArray();
        int overlayOrder = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Select(canvas => canvas.sortingOrder)
            .Concat(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Select(renderer => renderer.sortingOrder))
            .DefaultIfEmpty(0).Max() + 1;
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
        Texture2D texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 1.6f;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera; canvases[i].worldCamera = camera;
                canvases[i].planeDistance = camera.nearClipPlane + 1f; canvases[i].sortingLayerID = SortingLayer.layers.Last().id;
                canvases[i].sortingOrder = overlayOrder + previousOrders[i];
            }
            Canvas.ForceUpdateCanvases(); RefreshSpawnArrowPosition(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
            GameObject arrowObject = FindSpawnArrow();
            if (arrowObject != null)
            {
                SpawnArrow graphic = arrowObject.GetComponent<SpawnArrow>();
                Mesh mesh = graphic.canvasRenderer.GetMesh();
                Vector2 sample = RectTransformUtility.WorldToScreenPoint(camera, graphic.rectTransform.TransformPoint(new Vector3(0, 30, 0)));
                Color pixel = texture.GetPixel(Mathf.RoundToInt(sample.x), Mathf.RoundToInt(sample.y));
                Debug.Log("Arrow render: enabled=" + graphic.enabled + ", cull=" + graphic.canvasRenderer.cull
                    + ", depth=" + graphic.canvasRenderer.absoluteDepth + ", alpha=" + graphic.canvasRenderer.GetAlpha()
                    + ", vertices=" + (mesh == null ? 0 : mesh.vertexCount) + ", materials=" + graphic.canvasRenderer.materialCount
                    + ", sample=" + sample + ", pixel=" + pixel);
                Check(mesh != null && mesh.vertexCount > 0 && !graphic.canvasRenderer.cull, "倒數箭頭具有可見的渲染網格");
                Check(pixel.r > 0.8f && pixel.g > 0.6f && pixel.b < 0.7f, "倒數箭頭實際渲染出黃色填色");
            }
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = previousCameras[i];
                canvases[i].planeDistance = previousDistances[i]; canvases[i].sortingLayerID = previousLayers[i];
                canvases[i].sortingOrder = previousOrders[i];
            }
            camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(texture); Canvas.ForceUpdateCanvases(); RefreshSpawnArrowPosition(); Canvas.ForceUpdateCanvases();
        }
    }

    private static void RefreshSpawnArrowPosition()
    {
        GameObject arrow = FindSpawnArrow();
        if (arrow == null) return;
        SpawnArrow graphic = arrow.GetComponent<SpawnArrow>();
        var lateUpdate = typeof(SpawnArrow).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (graphic != null && lateUpdate != null) lateUpdate.Invoke(graphic, null);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed.Add(message);
    }

    private static void Finish(string status)
    {
        EditorApplication.update -= Tick; WriteReport(status + "\n" + string.Join("\n", passed));
        Debug.Log("Volume and Ready Play checks: " + status);
        if (Application.isBatchMode)
        {
            SessionState.SetBool(Flag, false); EditorApplication.Exit(status == "PASS" ? 0 : 1); return;
        }
        EditorApplication.isPlaying = false;
    }

    private static void RestoreOriginalScene()
    {
        string originalPath = SessionState.GetString(Flag + ".OriginalScenePath", string.Empty);
        if (!string.IsNullOrEmpty(originalPath) && SceneManager.GetActiveScene().path != originalPath && File.Exists(originalPath))
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
    }

    private static string ReportPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/CodexChecks/volume-ready-checks.txt"));
    }

    private static void WriteReport(string report)
    {
        string directory = Path.GetDirectoryName(ReportPath());
        Directory.CreateDirectory(directory); File.WriteAllText(ReportPath(), report);
    }
}
