# Mago de los Números — proyecto Unity 6 (6000.3.23f1)

Prototipo jugable con 3 niveles (Otoño, Desierto, Primavera), mago low-poly, enemigos numéricos decorativos,
montañas como límite del mapa, río/mar con colisión, menú de niveles, mapa (M) y HUD de zona.

## Clonar
```bash
git clone https://github.com/HectorYa/Desarrolo_videojuegos.git
```
La rama `main` ya contiene el proyecto completo (Assets, escenas, scripts y settings).

## Abrir
1. Unity Hub > Add > "Add project from disk" > elige la carpeta clonada `Desarrolo_videojuegos` (Editor 6000.3.23f1).
2. La primera vez Unity regenera la carpeta `Library` (tarda 1-2 min). Todas las escenas y assets ya están incluidos.
   Si algo falta o quieres regenerarlo todo: `Tools > Mage Hero > Build Everything (agua = bloqueo)`.
3. Abre `Assets/MageHero/Scenes/MainMenu.unity` y pulsa Play.

Alternativa: crea un proyecto 3D (URP) y importa `MageHero.unitypackage` (Assets > Import Package > Custom Package). Requiere Input System y uGUI.

## Controles
WASD caminar · Shift correr · Q/E girar cámara · Z/X o rueda zoom · M mapa · Esc menú · 1/2/3 elegir nivel en el menú.

## Estructura (Assets/MageHero)
- `Scripts/Runtime/Core`: `LevelData` (datos de los 3 niveles), `World` (altura, montañas, rutas, río), `LevelModel`.
- `Scripts/Runtime/Gameplay`: jugador, cámara, mago FX, enemigos, HUD, mapa, menú.
- `Scripts/Runtime/Physics`: capas + matriz, colliders ajustados, agua, perímetro, auditoría (ver `PHYSICS.md`).
- `Scripts/Editor`: generadores (`ProjectBuilder`, `LevelBuilder`, `MageBuilder`, `MenuBuilder`).
- `Shaders`: `MageToonLit` (toon URP con color de vértice) y `MageToonWater`.
- `Generated/` y `Scenes/`: se crean al generar.

## Editar
- Datos de nivel (rutas, zonas, paletas, montañas): `LevelData.cs`; luego `Tools > Mage Hero > Rebuild > Nivel …`.
- El mago: `MageBuilder.cs` y `Tools > Mage Hero > Rebuild > Mago`.
- Agua: `Build Everything (agua = penalización)` convierte río y mar en zona de penalización/derrota (`PlayerSafety.onDefeated` recarga el nivel).
- Los objetos de la escena son GameObjects normales: puedes mover, borrar o añadir los tuyos (usa `ObstacleCollider` para colliders ajustados).

## Importante
Este proyecto se generó sin abrir Unity: el código se validó con un compilador contra una API simplificada y la matemática del mundo
se verificó contra el prototipo web, pero no se ha ejecutado dentro del Editor. Si aparece un error de compilación o de paquete,
copia el mensaje de la Console. Si `manifest.json` no resuelve alguna versión, ajústala en Window > Package Manager
(Input System, Universal RP, Unity UI).
