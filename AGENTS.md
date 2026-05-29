# CardGameAR

Unity **6000.3.10f1** — URP 17.3.0, Vuforia Engine 11.4.4, Input System 1.18.0.

## Project structure

```
Assets/
  Cards/          – CardData and DeckData ScriptableObject assets
  Scripts/
    Core/         – GameManager (singleton), CardData, CardInstance, PlayerState, GameEvents, DeckData, CardEnums
    AR/           – ARManager, ARBoardManager, ARCardTracker, ARPlacementManager, ARCardVisual
    UI/           – UIManager, HandPanel, FieldPanel, SetupPanel, ActionsPanel, GameOverPanel, CardUI, LaneUI
  Scenes/
    AR.unity      – Main game scene
    AR Luna.unity – Alternate scene
  Prefabs/AR/     – Creature/building prefabs spawned on tracked images
  Resources/      – VuforiaConfiguration.asset
  Settings/       – URP assets (Mobile_RPAsset, PC_RPAsset)
```

Build settings only include `Assets/Props/Low Poly Graveyard/Scenes/SampleScene.unity` — this is likely stale; the real scenes (`AR.unity`, `AR Luna.unity`) are not listed.

## Architecture

- **Singleton managers**: `GameManager`, `ARManager`, `ARBoardManager`, `ARPlacementManager`, `UIManager`, `ActionsPanel`, `SetupPanel`
- **Event bus**: `GameEvents` static class with `System.Action` delegates. Subscribe in `OnEnable`/`OnDisable`.
- **Card data**: `CardData : ScriptableObject` with `[CreateAssetMenu(fileName = "NewCard", menuName = "CardWars/Card Data")]`. Decks are `DeckData : ScriptableObject` with `[CreateAssetMenu(fileName = "NewDeck", menuName = "CardWars/Deck Data")]`.
- **Turn states**: `Setup → TurnStart → Actions → Fight → EndTurn → GameOver`
- **3 lanes per player**, each holds one `Creature` and one `Building`. Landscape types: `Nicelands, Cornfield, UselessSwamp, SpookyCemetery, Rainbow`.
- **Floop mechanic**: Physical card rotation (>60°) detected by `ARCardTracker.CheckFloopOrientation()` triggers `GameManager.TryFloop()`.

## Key workflows

**Card placement (AR):** UI selects lane → `ARPlacementManager.SetWaitingLane()` → Vuforia tracks ImageTarget → `ARPlacementManager.TryPlaceCard()` → `GameManager.TryPlayCreature/Building/Spell`.

**Floop via orientation:** `ARCardTracker.Update()` measures `Vector3.Angle(transform.up, Vector3.up)` each frame. When angle exceeds `floopAngleThreshold` (default 60°), calls `GameManager.TryFloop()`.

## ARBoardManager Inspector (runtime tweakable)

Select `BoardRootImageTarget` → `ARBoardManager` while in Play Mode to adjust these in real time:

| Campo | Default | Descripción |
|-------|---------|-------------|
| `player1LaneSettings[i].creatureOffset` | `(0, 0, 0)` | Desplazamiento relativo al anchor del carril (criaturas) |
| `player1LaneSettings[i].buildingOffset` | `(0.06, 0, 0)` | Desplazamiento relativo al anchor del carril (edificios) |
| `player1LaneSettings[i].cardRotation` | `(0, 180, 0)` | Rotación en Euler angles por carril |
| `player2LaneSettings[i].*` | igual que P1 | Ídem para el jugador 2 |
| `cardScale` | `(1, 1, 1)` | Multiplicador global sobre la escala base del prefab |

Cada carril (0, 1, 2) tiene su propia config. Buildings usan `CardData.buildingPrefab` si existe; si no, fallback a `creaturePrefab`.

### Colores de paisajes en los planos 3D

En `ARBoardManager` hay 5 colores públicos (`colorNicelands`, `colorCornfield`, etc.) que se aplican al `MeshRenderer` del hijo "Plane" de cada anchor de carril. Se actualizan automáticamente al finalizar el Setup y al trackear el board.

### Testear posición sin AR

Para ver los cambios de offset/rotación/scale sin depender del tracking de Vuforia:

1. En el Hierarchy, expande `BoardRootImageTarget`
2. Deshabilita (checkbox) el componente `ObserverBehaviour` y `DefaultObserverEventHandler`
3. El board se queda en la posición de escena donde lo pusiste en el Editor
4. Ahora los cambios en `ARBoardManager` se ven al instante porque `Update()` refresca cada vez que tocas un valor

Para volver al tracking normal, re-habilita los dos componentes.

## Code conventions

- Comments and debug logs are in **Spanish**
- All strings use `string` (not C# `String`)
- Public fields with `[SerializeField]` over C# properties where Unity needs serialization
- Event subscriptions always paired in `OnEnable`/`OnDisable`

## SetupPanel — Configurar sprites de paisajes

El `SetupPanel` tiene un array `landscapeSprites` (tipo `LandscapeSpriteEntry[]`) donde se asigna un `Sprite` por cada `LandscapeType`. Esos mismos sprites son usados por `LaneUI` durante la partida.

En el Inspector del `SetupPanel`:
1. En **Sprites de paisajes**, crea una entrada por cada `LandscapeType` (Nicelands, Cornfield, UselessSwamp, SpookyCemetery, Rainbow) y arrastra el sprite correspondiente.
2. En **Carriles — UI visual**, asigna los 3 `Image` del fondo de los carriles a `laneBackgrounds`.

Los sprites se guardan en un `Dictionary<LandscapeType, Sprite>` estático en `Awake()`. No es necesario reiniciar si se cambian en el Editor — basta con re-entrar en Play Mode.

## Known issues

- Build scene list only contains a props demo scene — AR scenes are not included
- `EditorBuildSettings.asset` has `m_UseUCBPForAssetBundles: 0`
- No automated tests exist

## Assets

- Card artwork in `Assets/Cards/Arts/`
- AR creature/building prefabs in `Assets/Prefabs/AR/Criaturas/`
