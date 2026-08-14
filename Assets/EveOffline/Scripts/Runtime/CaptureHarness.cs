using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace EveOffline
{
    public sealed class CaptureHarness : MonoBehaviour
    {
        const string CaptureArgument = "-eveCapture";
        const string BeltArgument = "-eveCaptureBelt";
        const string QuitArgument = "-eveCaptureQuit";

        string capturePath;
        bool openBelt;
        bool quitAfterCapture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var arguments = Environment.GetCommandLineArgs();
            var captureIndex = Array.FindIndex(arguments,
                argument => string.Equals(argument, CaptureArgument, StringComparison.OrdinalIgnoreCase));
            if (captureIndex < 0 || captureIndex + 1 >= arguments.Length) return;

            var path = arguments[captureIndex + 1];
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return;

            var host = new GameObject("EVE Capture Harness");
            DontDestroyOnLoad(host);
            var harness = host.AddComponent<CaptureHarness>();
            harness.capturePath = Path.GetFullPath(path);
            harness.openBelt = arguments.Any(argument =>
                string.Equals(argument, BeltArgument, StringComparison.OrdinalIgnoreCase));
            harness.quitAfterCapture = arguments.Any(argument =>
                string.Equals(argument, QuitArgument, StringComparison.OrdinalIgnoreCase));
        }

        IEnumerator Start()
        {
            for (var frame = 0; frame < 4; frame++) yield return null;

            if (openBelt)
            {
                Button beltButton = null;
                for (var frame = 0; frame < 120 && beltButton == null; frame++)
                {
                    beltButton = FindBeltButton();
                    if (beltButton == null) yield return null;
                }

                if (beltButton != null)
                {
                    var initialLabel = beltButton.GetComponentInChildren<Text>(true)?.text ?? beltButton.name;
                    var startsTravel = !string.Equals(initialLabel, "ОТКРЫТЬ АКТИВНЫЙ БЕЛТ", StringComparison.OrdinalIgnoreCase);
                    beltButton.onClick.Invoke();
                    // A fresh operation now starts with persisted abstract travel.
                    // Wait until the navigation action changes to opening the active
                    // belt, invoke that second action, then allow the 3D world to settle.
                    beltButton = null;
                    var deadline = Time.realtimeSinceStartup + 75f;
                    while (startsTravel && Time.realtimeSinceStartup < deadline && beltButton == null)
                    {
                        yield return null;
                        beltButton = FindButtonWithLabel("ОТКРЫТЬ АКТИВНЫЙ БЕЛТ");
                    }
                    if (startsTravel && beltButton != null) beltButton.onClick.Invoke();
                    else if (startsTravel) Debug.LogWarning("EVE capture harness timed out waiting for inter-system travel.");
                    for (var frame = 0; frame < 12; frame++) yield return null;
                }
                else
                {
                    Debug.LogWarning("EVE capture harness could not find the mining-operation button.");
                }
            }

            var directory = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(capturePath)) File.Delete(capturePath);
            ScreenCapture.CaptureScreenshot(capturePath);

            if (!quitAfterCapture)
            {
                yield return null;
                Destroy(gameObject);
                yield break;
            }

            while (!File.Exists(capturePath) || new FileInfo(capturePath).Length <= 0)
                yield return null;

            Application.Quit(0);
        }

        static Button FindBeltButton()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var label = button.GetComponentInChildren<Text>(true)?.text;
                if (IsBeltLabel(button.name) || IsBeltLabel(label)) return button;
            }
            return null;
        }

        static Button FindButtonWithLabel(string expected)
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var label = button.GetComponentInChildren<Text>(true)?.text;
                if (string.Equals(button.name, expected, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(label, expected, StringComparison.OrdinalIgnoreCase)) return button;
            }
            return null;
        }

        static bool IsBeltLabel(string value)
        {
            return string.Equals(value, "НАЧАТЬ ДОБЫЧУ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ВЫЛЕТЕТЬ К ВЫБРАННОМУ БЕЛТУ", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "ОТКРЫТЬ АКТИВНЫЙ БЕЛТ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
