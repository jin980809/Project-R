using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

// 개발 보조 도구. Temp/tower_cmd.txt에 명령을 쓰면 에디터가 대신 실행하고 Temp/tower_cmd_result.txt에 결과를 남긴다.
// Claude Code가 에디터 창을 직접 조작하지 않고도 새로 고침, 초기 구성, 플레이 시작·정지를 할 수 있게 한다.
// 실행 중 발생한 오류와 예외는 Temp/tower_log.txt에 기록한다. 빌드에는 포함되지 않는다(Editor 폴더).
[InitializeOnLoad]
public static class EditorCommandWatcher
{
    private const string CommandPath = "Temp/tower_cmd.txt";
    private const string ResultPath = "Temp/tower_cmd_result.txt";
    private const string LogPath = "Temp/tower_log.txt";
    private const double PollInterval = 0.5;

    private const int GcSampleFrames = 300;

    private static double nextPollTime;
    private static bool gcSampling;
    private static ProfilerRecorder gcRecorder;
    private static ProfilerRecorder gcUsedRecorder;
    private static ProfilerRecorder totalUsedRecorder;
    private static ProfilerRecorder textureRecorder;

    static EditorCommandWatcher()
    {
        EditorApplication.update += Poll;
        Application.logMessageReceived += OnLog;
    }

    private static void Poll()
    {
        if (gcSampling)
        {
            if (gcRecorder.Count >= GcSampleFrames)
            {
                FinishGcSample();
            }
        }

        if (EditorApplication.timeSinceStartup >= nextPollTime)
        {
            nextPollTime = EditorApplication.timeSinceStartup + PollInterval;
            if (File.Exists(CommandPath))
            {
                string command = File.ReadAllText(CommandPath).Trim();
                File.Delete(CommandPath);
                Run(command);
            }
        }
    }

    private static void Run(string command)
    {
        string result = "ok " + command;
        try
        {
            if (command == "refresh")
            {
                AssetDatabase.Refresh();
            }
            else if (command == "build")
            {
                TowerBootstrap.BuildAll();
            }
            else if (command == "play")
            {
                EditorApplication.isPlaying = true;
            }
            else if (command == "stop")
            {
                EditorApplication.isPlaying = false;
            }
            else if (command == "screenshot")
            {
                ScreenCapture.CaptureScreenshot("Temp/tower_shot.png");
            }
            else if (command == "gcsample")
            {
                StartGcSample();
            }
            else if (command == "clearlog")
            {
                File.WriteAllText(LogPath, string.Empty);
            }
            else if (command.StartsWith("open "))
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(command.Substring(5), UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            else if (command.StartsWith("menu "))
            {
                bool executed = EditorApplication.ExecuteMenuItem(command.Substring(5));
                if (!executed)
                {
                    result = "fail menu not found: " + command.Substring(5);
                }
            }
            else
            {
                result = "fail unknown command: " + command;
            }
        }
        catch (System.Exception exception)
        {
            result = "fail " + exception;
        }
        File.WriteAllText(ResultPath, result);
    }

    // 플레이 중 프레임당 관리 힙 할당량(GC Allocated In Frame)을 300프레임 동안 측정해 Temp/tower_gc.txt에 기록한다.
    // 에디터 자체 할당도 섞이므로 정확한 값은 빌드에서 프로파일러로 다시 확인해야 한다.
    private static void StartGcSample()
    {
        gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", GcSampleFrames);
        gcUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
        totalUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        textureRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        gcSampling = true;
    }

    private static void FinishGcSample()
    {
        gcSampling = false;
        long total = 0;
        long max = 0;
        int zeroFrames = 0;
        int count = gcRecorder.Count;
        for (int i = 0; i < count; i++)
        {
            long value = gcRecorder.GetSample(i).Value;
            total += value;
            if (value > max)
            {
                max = value;
            }
            if (value == 0)
            {
                zeroFrames++;
            }
        }
        string text = "frames=" + count + " avgBytesPerFrame=" + (total / count) + " maxBytes=" + max + " zeroAllocFrames=" + zeroFrames
            + " gcUsedMB=" + (gcUsedRecorder.LastValue / 1048576f).ToString("F1")
            + " totalUsedMB=" + (totalUsedRecorder.LastValue / 1048576f).ToString("F1")
            + " textureMB=" + (textureRecorder.LastValue / 1048576f).ToString("F1");
        File.WriteAllText("Temp/tower_gc.txt", text);
        gcRecorder.Dispose();
        gcUsedRecorder.Dispose();
        totalUsedRecorder.Dispose();
        textureRecorder.Dispose();
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            File.AppendAllText(LogPath, "[" + type + "] " + condition + "\n" + stackTrace + "\n");
        }
    }
}
