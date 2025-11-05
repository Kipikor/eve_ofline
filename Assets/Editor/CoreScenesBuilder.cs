using EveOffline.Game;
using EveOffline.Space;
using EveOffline.Station;
using EveOffline.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEditor.Events;

public static class CoreScenesBuilder
{
    private const string ScenesFolder = "Assets/Scenes";

    [MenuItem("EVE Offline/Собрать базовые сцены", priority = 0)]
    public static void BuildCoreScenes()
    {
        EnsureScenesFolder();

        var mainMenu = CreateCleanScene(SceneNames.MainMenu);
        ConfigureMainMenu(mainMenu);
        CleanAllEventSystems();
        SaveScene(mainMenu);

        var station = CreateCleanScene(SceneNames.Station);
        ConfigureStation(station);
        CleanAllEventSystems();
        SaveScene(station);

        var space = CreateCleanScene(SceneNames.Space);
        ConfigureSpace(space);
        CleanAllEventSystems();
        SaveScene(space);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath(SceneNames.MainMenu), true),
            new EditorBuildSettingsScene(ScenePath(SceneNames.Station), true),
            new EditorBuildSettingsScene(ScenePath(SceneNames.Space), true)
        };

        AssetDatabase.SaveAssets();
        Debug.Log("EVE Offline: Базовые сцены собраны и добавлены в Build Settings.");
    }

    private static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder(ScenesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
    }

    private static string ScenePath(string sceneName) => $"{ScenesFolder}/{sceneName}.unity";

    private static Scene CreateCleanScene(string sceneName)
    {
        string path = ScenePath(sceneName);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, path);
        return scene;
    }

    private static Scene CreateOrOpenScene(string sceneName)
    {
        string path = ScenePath(sceneName);
        var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (existing == null)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }
        else
        {
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }

    private static void SaveScene(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject EnsureEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>();
        if (es != null)
        {
            // Приводим к Input System
            ConvertEventSystemToInputSystem(es);
            return es.gameObject;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var ui = go.AddComponent<InputSystemUIInputModule>();
        AssignActionsAsset(ui);
        return go;
    }

    private static void CleanAllEventSystems()
    {
        var all = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var es in all)
        {
            ConvertEventSystemToInputSystem(es);
        }
    }

    private static void ConvertEventSystemToInputSystem(EventSystem es)
    {
        if (es == null) return;
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null)
        {
            Object.DestroyImmediate(legacy);
        }
        var ui = es.GetComponent<InputSystemUIInputModule>();
        if (ui == null)
        {
            ui = es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        AssignActionsAsset(ui);
    }

    private static void AssignActionsAsset(InputSystemUIInputModule ui)
    {
        if (ui == null) return;
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        if (asset != null)
        {
            ui.actionsAsset = asset;
        }
    }

    private static Canvas CreateCanvas(string name)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject CreateButton(Transform parent, string text, Vector2 anchoredPos, Vector2 size, System.Action onClickAssign)
    {
        var buttonGo = new GameObject(text + "_Button");
        buttonGo.transform.SetParent(parent, false);
        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var btn = buttonGo.AddComponent<Button>();

        var rt = buttonGo.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);
        var label = labelGo.AddComponent<Text>();
        label.text = text;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.raycastTarget = false;
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        // Не добавляем непостоянные слушатели здесь, все клики настраиваем как persistent ниже

        return buttonGo;
    }

    private static Text CreateLabel(Transform parent, string text, Vector2 anchoredPos, int fontSize = 18)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(600, 40);
        rt.anchoredPosition = anchoredPos;
        return t;
    }

    private static void ConfigureMainMenu(Scene scene)
    {
        var root = new GameObject("MainMenuRoot").AddComponent<MainMenuController>();
        EnsureEventSystem();
        var canvas = CreateCanvas("MainMenuCanvas");

        CreateLabel(canvas.transform, "EVE Offline — Главное меню", new Vector2(0, 180), 24);

        const int slots = 4;
        float startY = 80f;
        for (int i = 0; i < slots; i++)
        {
            int slotIndex = i;
            CreateLabel(canvas.transform, $"Слот {i + 1}", new Vector2(-200, startY - i * 80));

            var playGo = CreateButton(canvas.transform, "Играть", new Vector2(0, startY - i * 80), new Vector2(140, 40), null);
            var playBtn = playGo.GetComponent<Button>();
            var playInvoker = playGo.AddComponent<MainMenuSlotButton>();
            playInvoker.Configure(root, slotIndex, false);
            UnityEventTools.AddPersistentListener(playBtn.onClick, playInvoker.Invoke);

            var delGo = CreateButton(canvas.transform, "Удалить", new Vector2(200, startY - i * 80), new Vector2(140, 40), null);
            var delBtn = delGo.GetComponent<Button>();
            var delInvoker = delGo.AddComponent<MainMenuSlotButton>();
            delInvoker.Configure(root, slotIndex, true);
            UnityEventTools.AddPersistentListener(delBtn.onClick, delInvoker.Invoke);
        }

        var exitGo = CreateButton(canvas.transform, "Выход", new Vector2(0, -220), new Vector2(200, 40), null);
        var exitBtn = exitGo.GetComponent<Button>();
        var exit = exitGo.AddComponent<ExitGameButton>();
        UnityEventTools.AddPersistentListener(exitBtn.onClick, exit.Exit);

        var cam = EnsureCamera2D();
        cam.backgroundColor = Color.black;
    }

    private static void ConfigureStation(Scene scene)
    {
        var root = new GameObject("StationRoot").AddComponent<StationController>();
        EnsureEventSystem();
        var canvas = CreateCanvas("StationCanvas");

        CreateLabel(canvas.transform, "Станция", new Vector2(0, 180), 24);
        var launchGo = CreateButton(canvas.transform, "Вылет", new Vector2(0, 60), new Vector2(220, 50), null);
        var launchBtn = launchGo.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(launchBtn.onClick, new UnityAction(root.OnLaunchToSpace));

        var backGo = CreateButton(canvas.transform, "В меню", new Vector2(0, 0), new Vector2(220, 50), null);
        var backBtn = backGo.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(backBtn.onClick, new UnityAction(root.OnBackToMenu));

        var cam = EnsureCamera2D();
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
    }

    private static void ConfigureSpace(Scene scene)
    {
        var root = new GameObject("SpaceRoot").AddComponent<SpaceController>();
        EnsureEventSystem();
        var canvas = CreateCanvas("SpaceHud");

        // Pause & speed UI
        // Pause open button (top-right)
        var pauseBtnGo = CreateButton(canvas.transform, "Пауза", new Vector2(260, 180), new Vector2(140, 40), null);
        var pauseBtn = pauseBtnGo.GetComponent<Button>();
        var pauseOpen = pauseBtnGo.AddComponent<PauseOpenButton>();
        var pauseRoot = new GameObject("PauseUI");
        pauseRoot.transform.SetParent(canvas.transform, false);
        var prt = pauseRoot.AddComponent<RectTransform>();
        prt.sizeDelta = new Vector2(600, 300);
        prt.anchoredPosition = Vector2.zero;

        var panel = pauseRoot.AddComponent<Image>();
        panel.color = new Color(0f, 0f, 0f, 0.6f);

        var tscHolder = new GameObject("TimeScaleController");
        tscHolder.transform.SetParent(pauseRoot.transform, false);
        var tsc = tscHolder.AddComponent<EveOffline.Game.TimeScaleController>();

        var pmHolder = new GameObject("PauseManager");
        pmHolder.transform.SetParent(canvas.transform, false);
        var pm = pmHolder.AddComponent<EveOffline.Game.PauseManager>();
        // bind later after we create slider

        // Labels
        CreateLabel(pauseRoot.transform, "Пауза", new Vector2(0, 100), 24);
        CreateLabel(pauseRoot.transform, "Скорость времени (x0..x5)", new Vector2(0, 40), 18);

        // Slider
        var sliderGo = new GameObject("SpeedSlider");
        sliderGo.transform.SetParent(pauseRoot.transform, false);
        var sliderBg = sliderGo.AddComponent<Image>();
        sliderBg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        var slider = sliderGo.AddComponent<Slider>();
        var srt = sliderGo.GetComponent<RectTransform>();
        srt.sizeDelta = new Vector2(400, 30);
        srt.anchoredPosition = new Vector2(0, 0);
        slider.minValue = 0f;
        slider.maxValue = 5f;
        slider.wholeNumbers = false;
        slider.value = 1f;

        // Slider handle visuals
        var fillArea = new GameObject("Fill");
        fillArea.transform.SetParent(sliderGo.transform, false);
        var fillImg = fillArea.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 1f, 1f);
        var fillRt = fillArea.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0.25f);
        fillRt.anchorMax = new Vector2(1, 0.75f);
        fillRt.offsetMin = new Vector2(10, 0);
        fillRt.offsetMax = new Vector2(-10, 0);
        slider.fillRect = fillRt;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(sliderGo.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        var hrt = handle.GetComponent<RectTransform>();
        hrt.sizeDelta = new Vector2(20, 30);
        slider.targetGraphic = handleImg;
        slider.handleRect = hrt;

        // Buttons
        var resumeGo = CreateButton(pauseRoot.transform, "Продолжить (Esc)", new Vector2(0, -80), new Vector2(260, 40), null);
        var resumeBtn = resumeGo.GetComponent<Button>();
        var resume = resumeGo.AddComponent<PauseResumeButton>();
        resume.Configure(pm);
        UnityEventTools.AddPersistentListener(resumeBtn.onClick, resume.Resume);

        var stationGo = CreateButton(pauseRoot.transform, "На станцию", new Vector2(0, -130), new Vector2(260, 40), null);
        var stationBtn = stationGo.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(stationBtn.onClick, new UnityAction(root.OnReturnToStation));

        // Controllers hookup
        var pmcGo = new GameObject("PauseMenuController");
        pmcGo.transform.SetParent(pauseRoot.transform, false);
        var pmc = pmcGo.AddComponent<PauseMenuController>();

        // Wire refs
        pmc.GetType().GetField("pauseManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pmc, pm);
        pmc.GetType().GetField("timeScaleController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pmc, tsc);
        pmc.GetType().GetField("speedSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pmc, slider);

        // Slider persistent binding
        UnityEventTools.AddPersistentListener(slider.onValueChanged, new UnityAction<float>(pmc.OnSpeedChanged));

        pm.GetType().GetField("pauseUiRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pm, pauseRoot);
        pm.GetType().GetField("timeScaleController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(pm, tsc);

        // wire pause open button now that pm exists
        pauseOpen.Configure(pm);
        UnityEventTools.AddPersistentListener(pauseBtn.onClick, pauseOpen.Open);

        // Hidden by default
        pauseRoot.SetActive(false);

        var cam = EnsureCamera2D();
        cam.backgroundColor = Color.black;
    }

    private static Camera EnsureCamera2D()
    {
        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        return cam;
    }
}


