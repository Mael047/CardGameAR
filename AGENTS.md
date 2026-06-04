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

- **Singleton managers**: `GameManager`, `AudioManager`, `ARManager`, `ARBoardManager`, `ARPlacementManager`, `UIManager`, `ActionsPanel`, `SetupPanel`
- **Event bus**: `GameEvents` static class with `System.Action` delegates. Subscribe in `OnEnable`/`OnDisable`.
- **Card data**: `CardData : ScriptableObject` with `[CreateAssetMenu(fileName = "NewCard", menuName = "CardWars/Card Data")]`. Decks are `DeckData : ScriptableObject` with `[CreateAssetMenu(fileName = "NewDeck", menuName = "CardWars/Deck Data")]`.
- **Turn states**: `Setup → TurnStart → Actions → Fight → EndTurn → GameOver`
- **3 lanes per player**, each holds one `Creature` and one `Building`. Landscape types: `Nicelands, Cornfield, UselessSwamp, SpookyCemetery, Rainbow`.
- **Floop mechanic**: Physical card rotation (>60°) detected by `ARCardTracker.CheckFloopOrientation()` triggers `GameManager.TryFloop()`. Floop activa habilidades especiales (no es un modo defensa genérico).

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

## AudioManager — Configurar sonidos

`AudioManager` es un singleton que se suscribe a `GameEvents`. Para configurarlo:

1. Crea un GameObject vacío `"AudioManager"` en la escena y añádele `AudioManager.cs`
2. Créale dos hijos con `AudioSource`:
   - `musicSource` (loop activado, Volume ~0.5)
   - `sfxSource` (loop desactivado, Volume ~1.0)
3. Asigna clips a los campos del `AudioManager`:
   - `bgMusic` — música de fondo
   - `turnChangeSFX` — cambio de turno
   - `buttonClickSFX` — clics de UI
   - `fightSFX` — inicio de fase Fight
   - `gameOverSFX` — fin de partida

Los sonidos por carta (`attackSFX`, `damageSFX`, `floopSFX`, `spellSFX`) se asignan directamente en cada `CardData`. El `AudioManager` los reproduce automáticamente al ocurrir el evento correspondiente.

Referencia de eventos de audio:

| Evento | Clip que reproduce |
|--------|-------------------|
| `OnCreatureAttacked` | `attacker.Data.attackSFX` |
| `OnDamageTaken` | `victim.Data.damageSFX` |
| `OnFloopActivated` | `card.Data.floopSFX` |
| `OnCardPlayed` (Spell) | `card.Data.spellSFX` |
| `OnGameStateChanged → Fight` | `fightSFX` |
| `OnTurnChanged` | `turnChangeSFX` |
| `OnGameOver` | `gameOverSFX` |
| Botones UI | `buttonClickSFX` via `AudioManager.Instance.PlayButtonClick()` |

## ARCardAnimation — Animaciones por carta

`ARCardAnimation` se añade a cada prefab de criatura/edificio. Se auto-asigna el componente `Animator` (Mecanim) con `GetComponentInChildren<Animator>()`.

**No necesita AnimatorController custom.** En `Setup()` se cambia al `templateController` y se reproduce `Idle`.

Los 4 estados del template deben llamarse exactamente `Idle`, `Attack`, `Damage`, `Death` y los clips se asignan directamente en el `AnimatorController` (no en CardData).

**Disparo desde el gameplay:**

| Momento | Animación |
|---------|-----------|
| `ResolveCombat` — atacante | `PlayAttack()` en el carril del atacante |
| `ResolveCombat` — defensor recibe daño | `PlayDamage()` en el carril del defensor |
| Criatura destruida en combate | `PlayDeath()` antes de `DestroyCreature` |
| `Science Blast` sobre criatura | `PlayDamage()` + `PlayDeath()` si muere |

Las animaciones se invocan a través de `ARBoardManager.PlayAttackAnimation(player, lane)`, etc. El `cardAnimations[2,3]` se actualiza automáticamente al colocar o remover cartas.

**Asignación en el Inspector:**
1. Crea un `AnimatorController` con 4 estados: `Idle`, `Attack`, `Damage`, `Death`
2. En cada estado, arrastra el clip de animación correspondiente en el campo **Motion**
3. En el estado `Damage` y `Death`, en el Inspector desmarca **Loop Time** (para que se reproduzcan una sola vez)
4. En el prefab, en `ARCardAnimation`:
   - Arrastra el controller al campo `templateController`
   - Arrastra los mismos clips a `clipAttack`, `clipDamage`, `clipDeath` (para el timing automático)
5. Las animaciones se asignan directamente en el `AnimatorController` del prefab — el `CardData` solo referencia el prefab
6. Agrega transiciones de `Attack` y `Damage` hacia `Idle` con `Has Exit Time = true`, `Exit Time = 1` y `Transition Duration = 0.15` para blends suaves

## SpellVFX — Efectos visuales de hechizos

`SpellVFX : MonoBehaviour` (singleton). Crea un GameObject `"SpellVFX"` en la escena.

- Prioridad 1: usa el `ParticleSystem` asignado en `CardData.spellEffect` (único por hechizo), se instancia en el carril del oponente
- Prioridad 2: `globalSpellEffect` (fallback global en SpellVFX), se tiñe con el color del hechizo
- Sin prefab: placeholder automático (esfera expandible con color)
- Colores por hechizo configurables en el Inspector de SpellVFX
- Llamado desde `GameManager.ResolveSpellEffect()` vía `SpellVFX.Instance.PlayAtLane()`

### Asignar efectos por hechizo

1. Crea un GameObject con `ParticleSystem` como prefab (ej: `Assets/Prefabs/Effects/ScienceBlastVFX.prefab`)
2. Arrástralo al campo `spellEffect` del `CardData` correspondiente en el Inspector
3. El efecto se instancia automáticamente en el carril enemigo al lanzar el hechizo

## State — final de la sesión (4 jun 2026)

### Completado en código
- **Fase 1–5**: Todas las habilidades, audio, animaciones, VFX, notificaciones, mensajes de instrucción
- **Flujo AR placement**: `waitingLaneIndex` se resetea SOLO en éxito (no en fallo)
- **GameOverPanel**: fade in/out con imagen del ganador, carga `Menu.unity`
- **TurnChangePanel**: overlay con fade corto, sprite por jugador
- **SetupPanel**: botones de paisaje muestran sprite + texto, `HorizontalLayoutGroup`
- **CameraSelectorPanel** (Menu): dropdown + preview live, guarda en PlayerPrefs + VuforiaConfiguration
- **ARCameraSwitcher** (AR): botón flotante 📷 que cicla entre cámaras vía Deinit/Init de Vuforia
- **Menu.CloseCameraPanel()**: añadido (bloqueante resuelto)

### Pendiente — solo en Editor (Inspector/scene)
| Tarea | Dónde | Prioridad |
|---|---|---|
| Añadir `ARCardVisual` a Punchy y SugarGolem prefabs (no tienen indicadores ni StatsCanvas) | `Assets/Prefabs/AR/Criaturas/Punchy.prefab`, `SugarGolem.prefab` | **HIGH** |
| Crear prefab de paisaje Rainbow | `Assets/Prefabs/Lands/` | **HIGH** |
| ARBoardManager: entry [4] cambiar type a Rainbow y asignar prefab | BoardRootImageTarget > ARBoardManager | **HIGH** |
| Asignar `cardArtwork` (sprite) a los 12 CardData | `Assets/Cards/{Creatures,Builds,Spells}/` | MEDIUM |
| Asignar `globalSpellEffect` (ParticleSystem prefab) en SpellVFX | GameObject SpellVFX en AR.unity | MEDIUM |
| Asignar `laneBackground` (Image) en los 6 LaneUI | AR.unity > Lane_P{1,2}_{0,1,2} | MEDIUM |
| Poner ARCameraSwitcher en algún GameObject de AR.unity | Escena AR | LOW |
| Verificar que SwampLurker no use Idle como Death | `Slime.controller` > estado Death | LOW |

## Assets

- Card artwork in `Assets/Cards/Arts/`
- AR creature/building prefabs in `Assets/Prefabs/AR/Criaturas/`
