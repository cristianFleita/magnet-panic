# Magnet Panic: Scrapstorm — Game Concept

> **Versión:** 0.1
> **Última actualización:** 2026-05-10
> **Fuente principal:** `design/gdd-gamejam.md`
> **Estado:** Concepto cerrado para alcance de game jam

Este documento es un extracto vivo del GDD de jam. Sirve como entrada para
`/map-systems` y para los `/design-system` por sistema. Si hay conflicto entre
este doc y `design/gdd-gamejam.md`, el GDD manda — actualizar este resumen.

---

## Pitch

**Magnet Panic: Scrapstorm** es un combate en arena donde el jugador usa un
imán para atraer chatarra, marcar enemigos y repeler objetos o cuerpos como
proyectiles.

Idea central:

> **No peleás contra enemigos. Los convertís en munición.**

Fantasía simple a entregar:

> Atraer peligro, cargar poder y liberar una descarga magnética satisfactoria.

---

## Género y plataforma

- **Género:** arena combat micro-roguelite
- **Modo principal:** singleplayer
- **Modo opcional:** co-op (solo si el core ya está terminado)
- **Motor:** Unity 6 (`6000.3.6f1`), URP, new Input System
- **Plataforma objetivo:** WebGL (embebido en host React/Vite)
- **Cámara:** top-down 3D o isométrica con seguimiento suave
- **Duración objetivo de run:** 4–5 minutos

---

## Pilares de diseño

Estos pilares ordenan toda decisión. Cada sistema individual debe declarar a
qué pilar responde principalmente.

### P1 — El escenario es el arma

La munición está en el mapa: chatarra, placas, minas, objetos pesados y
enemigos magnetizados. El jugador no genera munición, la cosecha del entorno.

### P2 — Pull / Strike / Repel

El combate gira alrededor de tres verbos:

- **Pull:** atraer chatarra o enemigos magnetizados.
- **Strike:** golpear para marcar, interrumpir o mantener combo.
- **Repel:** lanzar objetos/enemigos para hacer daño real.

Cualquier mecánica que no se exprese a través de estos verbos debe justificarse.

### P3 — El daño grande viene del magnetismo

El ataque básico (Strike) prepara, no mata. El daño importante viene de:
chatarra repelida, enemigos lanzados, choques contra paredes, minas, combos y
sobrecarga.

### P4 — Riesgo / recompensa por carga

Cuanto más metal retiene el jugador:

- más daño potencial,
- más lento se mueve,
- más rápido sube la sobrecarga,
- más penaliza equivocarse.

---

## Verbos del jugador (resumen rápido)

| Verbo | Input | Función | Pilar |
|---|---|---|---|
| Movimiento | WASD | Reposicionamiento 8 direcciones | — |
| Apuntar | Mouse | Dirige campo y repulsión | — |
| Pull | Click izquierdo | Atrae chatarra y enemigos magnetizados | P2, P1 |
| Repel | Segundo click izquierdo | Dispara objetos retenidos hacia cursor | P2, P3 |
| Strike | Click derecho | Marca enemigos, interrumpe, sostiene combo | P2 |
| Counter | Espacio | Ventana de pulso anti-ataque | P3 |
| Upgrade | 1-3 / Mouse | Elegir mejora al subir nivel | — |

---

## Sistemas que aparecen en el GDD

Esta es la lista cruda de sistemas que `/map-systems` debe ordenar, priorizar
y mapear con dependencias. No están priorizados acá — eso lo hace el index.

### Núcleo de combate
- **Movimiento del jugador** (8 direcciones, penalización por carga)
- **Cámara top-down/isométrica con seguimiento**
- **Magnetismo (Pull):** campo de atracción, radio, capacidad
- **Órbita / retención:** objetos orbitando, capacidad ocupada, defensa
- **Repel:** disparo cónico de objetos retenidos, knockback, daño
- **Strike:** ataque cónico corto, marca magnética
- **Marca magnética:** estados Normal / Marcado / Magnetizado / Aturdido
- **Counter / pulso:** ventana defensiva con espacio
- **Sobrecarga:** retención excesiva, explosión radial

### Contenido del mundo
- **Objetos atraíbles:** chatarra liviana, placa, mina, objeto pesado
- **Enemigos:** Scrapling, Metal Enemy, Runner Bot, Shield Bot, Spitter Drone (opcional)
- **Mini-boss:** Scrap Brute (4 fases simples)

### Meta del run
- **Scrap XP:** experiencia con bonus por estilo
- **Level up & Upgrades:** árbol de mejoras (Pull / Repel / Combo / Capacidad / Especiales)
- **Misiones de estilo:** objetivos secundarios cada 45–60s
- **Powerups temporales:** Repeler 360, Slow Time, Magnet Fever, Enemy Pull
- **Pacing de oleadas:** timeline del run con eventos a tiempos fijos

### Soporte
- **HUD / UI mínima:** HP, sobrecarga, capacidad, XP, combo, misión, cooldown
- **UI implementation:** HUD 2D con UI Toolkit; ayudas espaciales con UI/meshes/VFX world-space de Unity
- **Feedback (game feel):** sonido, hitstop, screen shake, slow-mo, popups
- **Scoring & condiciones de victoria/derrota:** score final, victoria por boss
  o extracción
- **Host React (puente WebGL):** comunicación juego ↔ página vía
  `react-unity-webgl`

---

## MVP técnico (alcance bloqueado para la jam)

Estos sistemas **deben** estar en el build final. Cualquier cosa fuera de esta
lista debe justificarse contra el plazo de 10 días.

- Movimiento
- Cámara
- Atraer (Pull)
- Órbita
- Repeler (Repel)
- Daño por impacto
- Strike
- Marca magnética
- Counter simple
- Sobrecarga
- 3 objetos atraíbles
- 3 tipos de enemigos
- Scrap XP
- 9–12 upgrades
- 3 powerups
- Oleadas
- Score final

### Fuera de alcance (puede quedar fuera)

- Co-op
- Polaridad avanzada (más allá de magnetizado básico)
- Meta-progresión entre runs
- Múltiples biomas
- Boss complejo (Scrap Brute simple es suficiente)
- Animaciones elaboradas
- Narrativa
- Leaderboards

---

## Roadmap de 10 días (referencia para priorizar)

| Día | Foco |
|---|---|
| 1 | Movimiento, cámara, Pull, órbita, Repel |
| 2 | Daño, impactos, objetos, feedback inicial |
| 3 | Strike, marca magnética, Pull de enemigo marcado |
| 4 | Scrapling, Runner, Shield Bot |
| 5 | XP, level up, upgrades |
| 6 | Misiones de estilo y powerups |
| 7 | Mini-boss y final de run |
| 8 | UI, sonido, partículas, screen shake |
| 9 | Balance, optimización WebGL, bugs |
| 10 | Tutorial, menú, build, deploy, página de jam |

El roadmap implica el orden natural de diseño: empezar por movimiento +
magnetismo, terminar por meta y polish.

---

## Criterios de éxito del prototipo

El prototipo es exitoso si:

1. Se entiende en menos de 20 segundos.
2. Atraer y repeler se siente bien (game feel).
3. Hay al menos una jugada espectacular por run.
4. Los combos salen naturalmente.
5. Las misiones empujan a jugar distinto.
6. El jugador quiere repetir una run.

Estos criterios son la prueba final de que el diseño de sistemas funcionó —
cualquier sistema que no contribuya a alguno de los seis es candidato a
recorte.

---

## Restricciones técnicas que afectan al diseño

- **WebGL es restricción dura.** Sin threading, audio APIs limitadas, cuidado
  con texturas grandes y reflection costosa. Diseñar pensando en WebGL desde
  el día 1.
- **El juego corre dentro del shell React.** Score, share buttons y UI de
  página viajan por el bridge `react-unity-webgl` — no DOM directo desde Unity.
- **Zustand es estado del host, no del gameplay.** El estado autoritativo del
  juego vive en Unity; sólo se publican eventos resumen al host.
- **Los números del GDD son puntos de partida.** Radio 5m, capacidad 8,
  ventana 0.35–0.5s, etc. — son defaults sugeridos, no contratos. Se ajustan
  en pasadas de balance.

---

## Próximos pasos

1. Correr `/map-systems` para generar `design/gdd/systems-index.md` con
   dependencias, prioridades y orden de diseño.
2. Empezar `/design-system magnetism-system` (núcleo del que dependen Pull,
   órbita, Repel y sobrecarga).
3. Seguir por dependencia descendente: combat-system, enemy-system, scoring,
   upgrades, host-bridge.
