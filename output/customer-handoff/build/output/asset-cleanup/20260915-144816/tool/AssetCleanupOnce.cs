using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>검토한 에셋 목록만 백업하고 GUID를 유지해 정리하는 임시 도구입니다.</summary>
public static class AssetCleanupOnce
{
    [Serializable] private sealed class Entry { public string source, destination, guid; }
    [Serializable] private sealed class Manifest { public Entry[] items; public string backupRoot; }

    /// <summary>열린 씬의 참조와 미저장 상태를 보존하고 모든 정리 대상의 원본을 백업합니다.</summary>
    [MenuItem("Dystopia/Asset Cleanup/Prepare")]
    public static void Prepare()
    {
        var manifest = Read();
        ValidateLiveReferences(manifest);
        Directory.CreateDirectory(manifest.backupRoot);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            if (!EditorSceneManager.SaveScene(scene, manifest.backupRoot + "/live-" + i + "-before.unity", true)) throw new IOException("Scene backup failed.");
        }
        foreach (var item in manifest.items)
        {
            string backup = manifest.backupRoot + "/" + item.source;
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            File.Copy(item.source, backup, false);
            File.Copy(item.source + ".meta", backup + ".meta", false);
        }
        File.WriteAllText(manifest.backupRoot + "/prepared.txt", DateTime.UtcNow.ToString("O"));
        Debug.Log("Asset cleanup backup complete: " + manifest.backupRoot);
    }

    /// <summary>미리 백업한 목록만 이동·삭제하고 열린 씬의 배치와 저장 상태는 변경하지 않습니다.</summary>
    [MenuItem("Dystopia/Asset Cleanup/Apply")]
    public static void Apply()
    {
        var manifest = Read();
        if (!File.Exists(manifest.backupRoot + "/prepared.txt")) throw new InvalidOperationException("Prepare first.");
        ValidateLiveReferences(manifest);
        foreach (var item in manifest.items)
        {
            if (!File.Exists(manifest.backupRoot + "/" + item.source + ".meta")) throw new IOException("Missing backup: " + item.source);
            if (AssetDatabase.AssetPathToGUID(item.source) != item.guid) throw new InvalidOperationException("GUID changed: " + item.source);
            if (!string.IsNullOrEmpty(item.destination)) EnsureFolder(Path.GetDirectoryName(item.destination).Replace('\\', '/'));
        }
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var item in manifest.items)
            {
                if (string.IsNullOrEmpty(item.destination))
                {
                    if (!AssetDatabase.DeleteAsset(item.source)) throw new IOException("Delete failed: " + item.source);
                }
                else
                {
                    string error = AssetDatabase.MoveAsset(item.source, item.destination);
                    if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (string root in new[] { "Assets/DystopiaPrototype/Art", "Assets/DystopiaPrototype/TopDownTest/Art" })
        {
            foreach (string folder in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Concat(new[] { root }))
                if (Directory.GetFileSystemEntries(folder).Length == 0 && !AssetDatabase.DeleteAsset(folder.Replace('\\', '/'))) throw new IOException("Empty folder cleanup failed: " + folder);
        }
        foreach (var item in manifest.items)
            if (!string.IsNullOrEmpty(item.destination) && AssetDatabase.AssetPathToGUID(item.destination) != item.guid) throw new InvalidOperationException("Moved GUID mismatch: " + item.destination);
        File.WriteAllText(manifest.backupRoot + "/completed.txt", DateTime.UtcNow.ToString("O"));
        Debug.Log("Asset cleanup completed. Source/meta backups retained. Scene not saved or reloaded.");
    }

    /// <summary>현재 명세의 경로가 허용한 리소스 폴더 안인지 확인합니다.</summary>
    private static Manifest Read()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before asset cleanup.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("output/asset-cleanup/manifest.json"));
        foreach (var item in manifest.items)
        {
            string absolute = Path.GetFullPath(item.source);
            if ((!item.source.StartsWith("Assets/DystopiaPrototype/Art/") && !item.source.StartsWith("Assets/DystopiaPrototype/TopDownTest/Art/")) || !absolute.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected source path.");
            if (!string.IsNullOrEmpty(item.destination) && !new[] { "Assets/Textures/Checkout/", "Assets/Materials/Checkout/", "Assets/Shaders/Checkout/", "Assets/Fonts/Checkout/", "Assets/Editor/Checkout/" }.Any(p => item.destination.StartsWith(p))) throw new InvalidOperationException("Unexpected destination.");
        }
        return manifest;
    }

    /// <summary>저장되지 않은 현재 씬이 삭제 후보를 사용하면 삭제 전에 중단합니다.</summary>
    private static void ValidateLiveReferences(Manifest manifest)
    {
        var roots = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded).SelectMany(s => s.GetRootGameObjects()).Cast<UnityEngine.Object>().ToArray();
        var paths = EditorUtility.CollectDependencies(roots).Select(AssetDatabase.GetAssetPath).ToHashSet();
        foreach (var item in manifest.items)
            if (string.IsNullOrEmpty(item.destination) && paths.Contains(item.source)) throw new InvalidOperationException("Live scene still uses deletion candidate: " + item.source);
    }

    /// <summary>Unity가 새 폴더의 meta와 GUID를 생성하도록 폴더를 준비합니다.</summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder)))) throw new IOException("Cannot create " + folder);
    }
}
