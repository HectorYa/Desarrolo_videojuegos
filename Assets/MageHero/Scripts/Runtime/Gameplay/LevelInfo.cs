using UnityEngine;

namespace MageHero
{
    [System.Serializable]
    public class ZoneInfo { public string name; public Vector2 position; }   // position = (x, z) en coordenadas de Unity

    /// Datos del nivel disponibles en tiempo de juego (HUD, mapa).
    public class LevelInfo : MonoBehaviour
    {
        public string levelId, levelName;
        public ZoneInfo[] zones;
        public float worldHalfSize = 108f;
        public Texture2D mapTexture;
    }
}
