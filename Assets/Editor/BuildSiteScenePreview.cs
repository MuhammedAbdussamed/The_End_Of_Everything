#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows the runtime construction locations in Scene view without adding or
/// moving anything in the saved map.
/// </summary>
[InitializeOnLoad]
public static class BuildSiteScenePreview
{
    static BuildSiteScenePreview()
    {
        SceneView.duringSceneGui -= DrawBuildSites;
        SceneView.duringSceneGui += DrawBuildSites;
    }

    private static void DrawBuildSites(SceneView sceneView)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") return;

        foreach (Vector3 position in TowerBuildSystem.PreviewPositions)
        {
            Vector3 surface = position + Vector3.up * 0.05f;
            Handles.color = BuildSite.NormalColor;
            Handles.DrawSolidDisc(surface, Vector3.up, TowerBuildSystem.SiteRadius);
            Handles.color = BuildSite.HighlightColor;
            Handles.DrawWireDisc(surface, Vector3.up, TowerBuildSystem.SiteRadius, 2f);
            Handles.Label(surface + Vector3.up * 0.15f, "İnşa Alanı");
        }
    }
}
#endif
