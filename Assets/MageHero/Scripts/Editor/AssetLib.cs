#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MageHero.Editor
{
    /// Materiales y mallas compartidos, guardados como assets para poder editarlos en Unity.
    public static class AssetLib
    {
        public const string Root = "Assets/MageHero";
        public const string Gen = Root + "/Generated";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material Make(string path, string shaderName, Action<Material> setup)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogError("[MageHero] No se encuentra el shader " + shaderName + ". ¿Está instalado URP?"); sh = Shader.Find("Hidden/InternalErrorShader"); }
            m = new Material(sh);
            setup(m);
            EnsureFolder(Gen + "/Materials");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// Toon con color de vértice (todos los props y el terreno comparten este material).
        public static Material VertexToon()
        {
            return Make(Gen + "/Materials/ToonVertex.mat", "MageHero/ToonLit", m =>
            {
                m.SetColor("_BaseColor", Color.white); m.SetColor("_ShadeColor", new Color(.55f, .58f, .72f, 1f)); m.SetFloat("_UseVertexColor", 1f);
            });
        }

        /// Toon de color plano (casas, madera...).
        public static Material Toon(int hex)
        {
            return Make(Gen + "/Materials/Toon_" + hex.ToString("x6") + ".mat", "MageHero/ToonLit", m =>
            {
                Color32 c = World.Hex(hex);
                Color b = c; m.SetColor("_BaseColor", b); m.SetColor("_ShadeColor", new Color(b.r * .55f, b.g * .58f, b.b * .72f, 1f)); m.SetFloat("_UseVertexColor", 0f);
            });
        }

        public static Material Glow(int hex)
        {
            return Make(Gen + "/Materials/Glow_" + hex.ToString("x6") + ".mat", "Universal Render Pipeline/Unlit", m =>
            {
                Color c = World.Hex(hex); m.SetColor("_BaseColor", c); m.color = c;
            });
        }

        public static Material Water(int hex, string levelId)
        {
            return Make(Gen + "/Materials/Water_" + levelId + ".mat", "MageHero/ToonWater", m =>
            {
                Color c = World.Hex(hex); c.a = 0.8f; m.SetColor("_BaseColor", c);
            });
        }

        /// Guarda la malla como asset (o reutiliza la existente con el mismo nombre).
        public static Mesh SaveMesh(string folder, string name, Func<Mesh> make)
        {
            string path = folder + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            EnsureFolder(folder);
            var mesh = make();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh ReplaceMesh(string folder, string name, Mesh mesh)
        {
            string path = folder + "/" + name + ".asset";
            EnsureFolder(folder);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
#endif
