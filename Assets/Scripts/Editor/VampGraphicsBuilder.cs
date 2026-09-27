using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Vamp.EditorTools
{
    /// <summary>
    /// Graphics pass run by Build All Scenes:
    ///  • map materials switch to VAMP/World (world-space tri-planar PBR) with tiling concrete / metal-panel / brick
    ///    surfaces (Assets/Art/Surfaces, generated textures), so blocks no longer look like flat prototype colours;
    ///  • the URP renderer gets Screen Space Ambient Occlusion (toggled at runtime by the AO graphics setting);
    ///  • soft shadows on.
    /// </summary>
    public static class VampGraphicsBuilder
    {
        private const string Dir = "Assets/Art/Surfaces";
        private static Shader _world;

        /// <summary>Called for every map material (from VampPrototypeBuilder.Mat).</summary>
        public static void Surface(Material mat, string name, float metallic)
        {
            if (mat == null) return;
            foreach (var skip in new[] { "Glow", "Dummy", "Gun", "Grid", "Stick", "Hand", "Glass", "Water" })
                if (name.Contains(skip)) return;
            if (_world == null) _world = Shader.Find("VAMP/World");
            if (_world == null) return;

            string set; float scale, bump, detail;
            if (name.Contains("Brick")) { set = "Brick"; scale = 1.0f; bump = 1f; detail = 1f; }
            else if (metallic >= 0.5f || name.Contains("Metal") || name.Contains("Steel") || name.Contains("Vent") || name.Contains("WallRun") || name.Contains("Roof"))
            { set = "Metal"; scale = 0.5f; bump = 0.9f; detail = 0.9f; }
            else if (name.Contains("Sand")) { set = "Concrete"; scale = 0.8f; bump = 0.35f; detail = 0.6f; }
            else { set = "Concrete"; scale = 0.33f; bump = 1f; detail = 1f; }

            var a = Tex(set + "_A", false);
            var n = Tex(set + "_N", true);
            if (a == null) return;
            bool emissive = mat.IsKeywordEnabled("_EMISSION");
            Color emission = emissive && mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            if (mat.shader != _world) mat.shader = _world;
            mat.SetTexture("_BaseMap", a);
            if (n != null) mat.SetTexture("_BumpMap", n);
            mat.SetFloat("_Scale", scale);
            mat.SetFloat("_BumpScale", bump);
            mat.SetFloat("_DetailStrength", detail);
            mat.SetColor("_EmissionColor", emission);
            EditorUtility.SetDirty(mat);
        }

        private static Texture2D Tex(string file, bool normal)
        {
            string path = Dir + "/" + file + ".png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; dirty = true; }
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>SSAO renderer feature + soft shadows on every URP renderer in the project.</summary>
        public static void SetupRenderer()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                string dp = AssetDatabase.GUIDToAssetPath(guid);
                if (!dp.StartsWith("Assets/")) continue; // never touch package assets
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(dp);
                if (data == null) continue;
                bool has = false;
                foreach (var f in data.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) has = true;
                if (has) continue;
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "ScreenSpaceAmbientOcclusion";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
                // Keep the renderer's feature-id map in step (what the inspector's "Add Renderer Feature" does).
                long localId;
                string g;
                var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.Instance | BindingFlags.NonPublic);
                if (mapField != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out g, out localId))
                {
                    var map = mapField.GetValue(data) as System.Collections.Generic.List<long>;
                    if (map != null) map.Add(localId);
                }
                data.SetDirty();
                EditorUtility.SetDirty(data);
                Debug.Log("[VAMP] Added SSAO to " + data.name);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                string up = AssetDatabase.GUIDToAssetPath(guid);
                if (!up.StartsWith("Assets/")) continue;
                var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(up);
                if (urp == null) continue;
                var so = new SerializedObject(urp);
                var soft = so.FindProperty("m_SoftShadowsSupported");
                if (soft != null && !soft.boolValue) { soft.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
