# Sistema de restricciones de movimiento (Unity 6000.3, físicas 3D)

## Capas y Layer Collision Matrix
| Capa | Qué es | Player | Environment | Obstacles | Water | MapBounds | Enemies |
|---|---|:-:|:-:|:-:|:-:|:-:|:-:|
| Player | CharacterController del mago | – | ✔ | ✔ | ✔ (trigger) | ✔ | ✔ |
| Environment | Terreno, puentes, suelo transitable | ✔ | ✘ | ✘ | ✘ | ✘ | ✔ |
| Obstacles | Rocas, árboles (tronco), montañas, casas, muros de margen | ✔ | ✘ | ✘ | ✘ | ✘ | ✔ |
| Water | Volúmenes del cauce del río | ✔ (trigger) | ✘ | ✘ | ✘ | ✘ | ✘ |
| MapBounds | Perímetro invisible de seguridad | ✔ | ✘ | ✘ | ✘ | ✘ | ✔ |
| Enemies | Enemigos numéricos | ✔ | ✔ | ✔ | ✘ | ✔ | ✘ |

La matriz vive en `GameLayers.cs` y se aplica desde el menú y también al iniciar el juego (`BeforeSceneLoad`).

## Pasos
1. `Tools > Mage Hero > Physics > 1 - Create Layers and Collision Matrix` (luego File > Save Project).
2. Con tu escena abierta: `2 - Apply Fitted Colliders to Open Scene`.
   Reglas por nombre: tree/pine/palm/cactus/joshua → cápsula en el tronco;
   rock/boulder → MeshCollider convexo; mesa/spire/mountain/cliff/hut/house → MeshCollider exacto (estático);
   terrain/ground/bank/hill/bridge → capa Environment (transitable).
3. Río: añade `RiverBuilder` a un objeto vacío, define el eje (Y = superficie del agua), arrastra los colliders de los
   puentes y pulsa `Build River Colliders`. Elige `policy`: Block / Penalty / Defeat.
4. Perímetro: añade `MapBoundary`, ajusta `area` (detrás de las montañas) y pulsa `Build Boundary`.
5. Jugador: `PlayerSafety` + `WaterSensor` en `Player_Prefab` (capa Player). Conecta `onDefeated` a tu GameManager.
6. `3 - Audit Colliders` en cada escena antes de probar.

## Notas de diseño
- Árboles: solo el tronco bloquea; la copa queda por encima de la cabeza y no debe bloquear.
- Montañas/acantilados: MeshCollider NO convexo, estático. Para mallas de muchos triángulos usa `collisionMeshOverride`.
- Separa el terreno jugable (Environment) de las mallas de montaña (Obstacles) para que el pie de la montaña bloquee.
- El agua se detecta con `WaterSensor` (OverlapSphere en los pies), no con OnTriggerEnter, porque los triggers con
  CharacterController dependen de reglas de Rigidbody. Los muros de margen dejan hueco en los puentes.
- `PlayerSafety` guarda la última posición segura (suelo, pendiente suave, lejos del agua) y recupera al jugador si cae bajo `killY`.
