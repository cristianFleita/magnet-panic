# Combat Vertical Slice Plan

> **Status**: Implementation Plan Draft
> **Author**: cris + agents
> **Last Updated**: 2026-05-12
> **Scope**: Convertir los sistemas de combate ya prototipados en una run dinamica jugable.

## Objetivo

El proximo paso de Magnet Panic no es buscar arte final, sino cerrar una **vertical slice gris** donde el juego se pueda jugar de punta a punta:

1. Crear arena dinamicamente.
2. Spawnear player dinamicamente.
3. Spawnear enemigos desde 4 puertas.
4. Spawnear chatarra cerca del jugador.
5. Spawnear curaciones en puntos definidos.
6. Escalar oleadas.
7. Terminar la run al morir.

Esto permite balancear el core real de la jam: Pull, Repel, overload, combate estilo Arkham, vida, curacion, dodge y presion por oleadas.

## Estado Actual

Ya existe:

- Combate base estilo Arkham: strike, counter, dodge con roll, target scanner y animaciones principales.
- Magnetismo funcional: Pull, Repel, orbit, overload.
- Sistema de vida: `CombatHealth`, HUD de vida, pickups de curacion.
- Prefabs genericos de enemigos y attractable objects.
- Enemigos con AI base, vida, ataque, counter cue y marca magnetica.
- Attractables con tipos funcionales y comportamiento de proyectil.
- Animator del personaje con movimiento, pull, repel, hit, attack, counter y dodge.

Falta:

- Director de run/oleadas.
- Arena runtime con puertas y bounds.
- Spawners dinamicos.
- Pooling integrado al loop.
- Debug/tuning para balance rapido.
- Score/XP/upgrades, despues de validar la presion de combate.

## Prioridad Recomendada

### 1. Arena Gris Runtime

Crear una arena jugable inspirada en la referencia visual:

- Plano rectangular/square con paredes.
- 4 puertas cardinales.
- Centro visual simple.
- 4 pads de curacion.
- Bounds consultables.
- Door spawn anchors.
- Sin depender de objetos manuales en la escena principal.

Resultado esperado: una escena puede arrancar con un `RunBootstrap` y construir la arena.

### 2. Run Bootstrap

Crear un componente responsable de ensamblar la partida:

- Instancia arena prefab/procedural.
- Instancia player prefab.
- Instancia managers: enemy manager, wave director, pools, camera follow.
- Conecta referencias entre sistemas.
- Lanza `StartRun()`.

Este componente reemplaza el setup estatico del editor para jugar runs repetibles.

### 3. Object Pooling MVP

Antes de spawnear oleadas largas, integrar pooling minimo:

- Pool de Scrapling.
- Pool de Metal Enemy.
- Pool de Runner Bot si ya existe prefab.
- Pool de LightScrap / Plate / Mine / Heavy.
- Pool de HealingPickup.
- Reset basico por `OnSpawn` / `OnDespawn`.

Si el pooling completo se atrasa, se puede usar Instantiate temporal con una interfaz similar, pero el codigo del Wave Director debe quedar preparado para pooling.

### 4. Wave Director MVP

Implementar primero un director simple:

- Acto actual.
- Wave index.
- Threat budget.
- Enemigos vivos.
- Cap de enemigos vivos.
- Seleccion de puertas.
- Warning de puerta.
- Spawn por cola.
- Rest period.

No incluir upgrades ni score todavia. La meta es responder: "La presion se siente bien?"

### 5. Scrap Director

Separar la logica de chatarra de la logica de enemigos, aunque viva dentro del mismo Wave Director al inicio:

- Sampling alrededor del jugador.
- Clamp a bounds.
- Validacion contra paredes, puertas, enemigos y player.
- Ammo floor cerca del jugador.
- Mix de tipos por acto.

La chatarra debe sostener el loop. Si el jugador se queda sin municion, la run se vuelve un brawler comun y pierde identidad.

### 6. Healing Spawn Points

Implementar curacion simple:

- 4 spawn points/pads.
- Max 1 pickup activo.
- Cooldown.
- Solo spawnea si el player no esta full HP.
- Mayor probabilidad si HP <= 50%.

Esto da un objetivo espacial sin crear otro sistema grande.

### 7. Debug Overlay De Tuning

Agregar una vista de debug temporal:

- Acto.
- Wave.
- Enemigos vivos.
- Threat budget usado.
- Puertas elegidas.
- Chatarra activa cerca del jugador.
- Cooldown de curacion.

Para una jam, esto vale oro: permite ajustar ritmo sin adivinar.

## Combate: Mejoras Futuras Despues De La Slice

Estas mejoras conviene hacer despues de tener oleadas jugables:

### Combos

El combo debe premiar alternar verbos:

- Strike hit.
- Counter exitoso.
- Repel kill.
- Wall slam.
- Multi-kill con chatarra.
- Kill con enemigo magnetizado.
- Dodge perfecto cerca de ataque enemigo, opcional futuro.

Regla recomendada: el combo vive en `scoring-xp-system`, no en `combat-system`, porque mezcla eventos de combat, magnetism y arena.

### Fluidez Del Combate

Posibles mejoras:

- Cancel windows desde ataque hacia dodge.
- Buffer de proximo ataque para combos.
- Attack chain con finisher cada 3-4 golpes.
- Counter que puede encadenar directo a Strike.
- Dodge con pequena ventana de invulnerabilidad ya incluida, ajustable.

No hacer todo ahora. Primero medir con oleadas reales.

### Enemigos

Orden recomendado:

1. Scrapling: horda basica.
2. Metal Enemy: municion viva siempre pullable.
3. Runner Bot: obliga a dodge y reposicionamiento.
4. Heavy Bot: presion lenta, bueno para wall slam.

Buscar modelos 3D y animaciones despues de validar estos roles con placeholders. El rol de gameplay manda la eleccion del asset, no al reves.

## Criterios De Exito De La Slice

La slice esta lista para pasar a arte/modelos cuando:

1. Puedo iniciar una run desde una escena vacia.
2. El player aparece en el centro y la camara lo sigue.
3. Los enemigos salen por las 4 puertas con warning.
4. La chatarra aparece cerca del jugador y siempre dentro del mapa.
5. El jugador puede sobrevivir al menos 2-3 minutos con skill razonable.
6. El dodge, counter, pull y repel se usan todos naturalmente.
7. La curacion genera decisiones de posicionamiento.
8. El director puede ajustarse con knobs sin tocar codigo.
9. No hay dependencia critica de objetos puestos a mano en escena.
10. La partida termina correctamente al morir.

## Orden De Implementacion Sugerido

1. `ArenaRuntime` o `ArenaSystem` con bounds, puertas y pickup points.
2. `RunBootstrap` para crear arena/player/managers.
3. Pool minimo o `SpawnService` con API compatible con pooling.
4. `WaveDirector` enemigo-only con 1 tipo y 4 puertas.
5. Agregar threat budget y tipos de enemigo.
6. Agregar `ScrapSpawnDirector` por sampling cerca del player.
7. Agregar healing pickup director.
8. Agregar debug overlay.
9. Primer balance de 5 minutos.
10. Recien despues, buscar/reemplazar modelos y animaciones enemigas.

## Riesgos

| Riesgo | Mitigacion |
|---|---|
| Oleadas se sienten injustas | Warning de puertas, cap de enemigos vivos, entrada sin ataque inmediato |
| El jugador se queda sin chatarra | Ammo floor cerca del jugador |
| Demasiadas cosas en pantalla | Threat budget + cap + WebGL-first |
| Arena linda pero mala para combate | Props decorativos primero, colliders internos despues de playtest |
| Curacion trivializa daño | Max 1 activo + cooldown + spawn lejos del centro |
| Arte bloquea gameplay | Placeholders hasta que los roles esten validados |

## Nota De Produccion

La referencia visual es muy buena para direccion de arte: cuatro puertas claras, centro reconocible, pads de color y props industriales. Pero para el primer playable conviene traducirla a reglas simples:

- Puertas son gameplay.
- Centro es landmark.
- Props son lectura visual.
- Chatarra es municion dinamica.
- Pickups son objetivos de riesgo.

Cuando esto funcione con cubos y cilindros, el arte final va a entrar con mucha mas seguridad.
