using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps generated room ceilings out of Scene View without changing their
/// runtime rendering, collision, lighting, or shadow behavior.
/// </summary>
[InitializeOnLoad]
public static class GeneratedCeilingSceneVisibility
{
    static GeneratedCeilingSceneVisibility()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += ApplyToActiveScene;
    }

    public static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (IsGeneratedCeiling(renderer.gameObject.name))
                {
                    SceneVisibilityManager.instance.Hide(renderer.gameObject, true);
                }
            }

            // Local volumes and reflection probes are useful runtime helpers,
            // but their large wire boxes obscure the blockout in Scene View.
            foreach (Volume volume in root.GetComponentsInChildren<Volume>(true))
            {
                SceneVisibilityManager.instance.Hide(volume.gameObject, true);
            }
            foreach (ReflectionProbe probe in root.GetComponentsInChildren<ReflectionProbe>(true))
            {
                SceneVisibilityManager.instance.Hide(probe.gameObject, true);
            }
        }

        SceneView.RepaintAll();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ApplyToScene(scene);
    }

    private static void ApplyToActiveScene()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            ApplyToScene(SceneManager.GetActiveScene());
        }
    }

    private static bool IsGeneratedCeiling(string objectName)
    {
        return objectName.Equals("Ceiling", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Frustum Ceiling", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Operations Room Ceiling Slab", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Server Room Ceiling Slab", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Hallway L Return Ceiling Slab", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Hallway Long Ceiling Slab", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("High Bay Ceiling Slab", StringComparison.OrdinalIgnoreCase)
            || objectName.Equals("Cleanroom Ceiling", StringComparison.OrdinalIgnoreCase);
    }
}
