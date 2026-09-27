using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Vamp.Graphics
{
    /// <summary>
    /// Graphics revamp: every gameplay scene gets one realtime reflection probe covering the map (rendered once when
    /// the scene loads), so metal and wet-looking surfaces pick up the sky and the level instead of flat grey.
    /// </summary>
    public static class SceneReflections
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => Add(s);
            Add(SceneManager.GetActiveScene());
        }

        private static void Add(Scene scene)
        {
            if (!scene.IsValid() || Object.FindAnyObjectByType<ReflectionProbe>() != null) return;
            var renderers = Object.FindObjectsByType<MeshRenderer>();
            if (renderers.Length < 20) return; // menus / boot
            var b = new Bounds(renderers[0].bounds.center, Vector3.zero);
            foreach (var r in renderers) if (r.gameObject.isStatic || r.bounds.size.magnitude > 1f) b.Encapsulate(r.bounds);
            var go = new GameObject("[VAMP Reflections]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = b.center + Vector3.up * Mathf.Min(8f, b.extents.y * 0.3f);
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            p.resolution = 128;
            p.size = b.size + Vector3.one * 10f;
            p.boxProjection = false;
            p.intensity = 0.9f;
            p.RenderProbe();
        }
    }
}
