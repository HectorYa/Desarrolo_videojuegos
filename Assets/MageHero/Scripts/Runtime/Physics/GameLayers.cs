using UnityEngine;

namespace MageHero
{
    /// Fuente única de verdad de las capas y de la Layer Collision Matrix.
    /// La matriz se aplica (1) desde el menú Tools > Mage Hero > Physics y (2) al iniciar el juego,
    /// de modo que builds y Play Mode siempre respetan la misma configuración.
    public static class GameLayers
    {
        public const string Player = "Player", Environment = "Environment", Obstacles = "Obstacles",
                            Water = "Water", MapBounds = "MapBounds", Enemies = "Enemies",
                            Projectiles = "Projectiles";

        public static readonly string[] All = { Player, Environment, Obstacles, Water, MapBounds, Enemies, Projectiles };

        // Pares que SÍ colisionan. Cualquier otro par entre estas capas se ignora.
        // (Player-Water debe estar activo: los volúmenes de agua son triggers y la matriz también los filtra.)
        static readonly string[,] Collides =
        {
            { Player,  Environment }, { Player,  Obstacles }, { Player, MapBounds }, { Player, Water }, { Player, Enemies },
            { Enemies, Environment }, { Enemies, Obstacles }, { Enemies, MapBounds },
            // Hechizos: golpean enemigos y se deshacen contra terreno/obstáculos/bordes. Ignoran al jugador y al agua.
            { Projectiles, Enemies }, { Projectiles, Environment }, { Projectiles, Obstacles }, { Projectiles, MapBounds },
        };

        public static int Index(string layerName) => LayerMask.NameToLayer(layerName);
        public static bool Exists(string layerName) => Index(layerName) >= 0;
        public static int Mask(params string[] names) => LayerMask.GetMask(names);

        public static void Set(GameObject go, string layerName, bool recursive = false)
        {
            int i = Index(layerName);
            if (i < 0)
            {
                Debug.LogError($"[MageHero] La capa '{layerName}' no existe. Ejecuta Tools > Mage Hero > Physics > 1 - Create Layers and Collision Matrix.");
                return;
            }
            go.layer = i;
            if (recursive) foreach (Transform t in go.transform) Set(t.gameObject, layerName, true);
        }

        static bool Pair(string a, string b)
        {
            for (int i = 0; i < Collides.GetLength(0); i++)
                if ((Collides[i, 0] == a && Collides[i, 1] == b) || (Collides[i, 0] == b && Collides[i, 1] == a)) return true;
            return false;
        }

        public static void ApplyMatrix()
        {
            for (int i = 0; i < All.Length; i++)
                for (int j = i; j < All.Length; j++)
                {
                    int a = Index(All[i]), b = Index(All[j]);
                    if (a < 0 || b < 0) continue;
                    Physics.IgnoreLayerCollision(a, b, !Pair(All[i], All[j]));
                }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => ApplyMatrix();
    }
}
