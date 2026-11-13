using System.Collections.Generic;
using UnityEngine;

namespace MageHero
{
    public class RouteDef { public string name, theme; public float w; public Vector2[] pts; }
    public class SeaDef { public float a, b, from, to, beach; }
    public class PierDef { public float x, z, L, W, y; }
    public class HutDef { public float x, z; public int wall, roof; }
    public class BarrelDef { public float x, z; public int hex; }
    public class FenceDef { public int route; public char axis; public float lo, hi; }
    public class ZoneDef { public string name; public float x, z; }
    public class RecipeItem { public string kind; public float step, o0, oR, p; public bool two; public float s0 = 0.8f, sR = 0.8f; public int[] cols; }
    public class PaletteDef { public int path, edge, sand, cliff, water, deck; public int[] wild; public Dictionary<string, int[]> th; }
    public class MtnDef { public float A, step; public bool dune; public int[] cols; public string[] foot; }

    /// Definición completa de un nivel (generada desde el prototipo web).
    public class LevelDef
    {
        public string id, name, sub, lv, topic, desc, accent;
        public string[] tags;
        public int sky;
        public float h0, roll, cliff, carve, spawn, rim, circle, terr;
        public bool hasMonument; public Vector2 monument;
        public Vector4[] hills;           // x, z, amplitud, 2*sigma^2
        public SeaDef sea;
        public Vector2[] riv;
        public int[] bridgeRoutes;
        public PierDef[] piers;
        public RouteDef[] routes;
        public RecipeItem[] riverProps;
        public int groveN;
        public string[] groveK, scatter;
        public int[] treeCols, pineCols, palmCols, rockCols, bushCols, flowerCols;
        public int stoneCol, tuftCol;
        public HutDef[] huts; public BarrelDef[] barrels; public FenceDef[] fences;
        public ZoneDef[] zones;
        public PaletteDef pal;
        public MtnDef mtn;
    }
}
