# Magnet Panic: Scrapstorm — GDD Game Jam

**Versión:** 0.1  
**Scope:** prototipo jugable para game jam de 10 días  
**Motor:** Unity  
**Plataforma objetivo:** WebGL  
**Género:** arena combat micro-roguelite  
**Modo principal:** singleplayer  
**Modo opcional:** co-op si el core ya está terminado  

---

# 1. Pitch

**Magnet Panic: Scrapstorm** es un juego de combate en arena donde el jugador usa un imán para atraer chatarra, marcar enemigos y repeler objetos o cuerpos como proyectiles.

La idea central:

> **No peleás contra enemigos. Los convertís en munición.**

La versión de game jam debe demostrar una fantasía simple:

> Atraer peligro, cargar poder y liberar una descarga magnética satisfactoria.

---

# 2. Objetivo de la versión jam

Crear una experiencia de 4 a 5 minutos que sea:

- intuitiva,
- rápida,
- rejugable,
- con combos,
- con mucho feedback visual y sonoro,
- suficientemente profunda para no sentirse genérica.

La prioridad no es cantidad de contenido. La prioridad es que el core sea divertido.

---

# 3. Pilares de diseño

## 3.1 El escenario es el arma

La munición está en el mapa: chatarra, placas, minas, objetos pesados y enemigos magnetizados.

## 3.2 Pull / Strike / Repel

El combate gira alrededor de tres verbos:

- **Pull:** atraer chatarra o enemigos magnetizados.
- **Strike:** golpear para marcar, interrumpir o mantener combo.
- **Repel:** lanzar objetos/enemigos para hacer daño real.

## 3.3 El daño grande viene del magnetismo

El ataque básico no debe matar demasiado. Sirve para preparar.  
El daño importante viene de:

- chatarra repelida,
- enemigos lanzados,
- choques contra paredes,
- minas,
- combos,
- sobrecarga.

## 3.4 Riesgo/recompensa

Cuanto más metal retenés:

- más daño potencial tenés,
- más lento te movés,
- más sube la sobrecarga,
- más peligroso es equivocarte.

---

# 4. Cámara y controles

## Cámara

Top-down 3D o isométrica con seguimiento suave.

## Controles recomendados

| Acción | Input |
|---|---|
| Movimiento | WASD |
| Apuntar | Mouse |
| Atraer | Click izquierdo |
| Repeler | Segundo click izquierdo |
| Strike | Click derecho |
| Counter / pulso | Espacio |
| Elegir upgrade | Mouse / 1-3 |

---

# 5. Core loop

## Loop corto

1. El jugador ve chatarra y enemigos.
2. Activa atraer con click izquierdo.
3. La chatarra entra en órbita.
4. Golpea a un enemigo para marcarlo.
5. Repele objetos o enemigo hacia un grupo con otro click izquierdo.
6. Impacta, mata, genera combo.
7. Gana Scrap XP.
8. Elige upgrades.

## Loop de run

1. Inicio con imán básico.
2. Oleadas de enemigos.
3. Misiones de estilo.
4. Upgrades del imán.
5. Oleada intensa.
6. Mini-boss.
7. Extracción o score final.

---

# 6. Mecánicas principales

## 6.1 Movimiento

Movimiento libre en 8 direcciones.

Variables iniciales sugeridas:

| Variable | Valor |
|---|---:|
| Velocidad base | 5 |
| Penalización con carga completa | -20% |
| Invulnerabilidad al recibir daño | 0.5 s |

---

## 6.2 Atraer chatarra

Con un click izquierdo, el jugador activa o alterna un campo magnético.

### Reglas

- Atrae objetos metálicos dentro de un radio.
- Los objetos vuelan hacia el jugador.
- Al llegar, entran en órbita.
- No atrae enemigos al inicio, salvo enemigos magnetizados o enemigos metálicos definidos como atraíbles.
- Los enemigos magnetizados se acercan al jugador y quedan retenidos al frente, no orbitan como chatarra.

Variables sugeridas:

| Variable | Valor |
|---|---:|
| Radio base | 5 m |
| Capacidad base | 8 puntos |
| Máximo visual en órbita | 8-12 objetos |

---

## 6.3 Órbita / retención

Los objetos atraídos orbitan alrededor del jugador.

### Reglas

- Cada objeto ocupa capacidad.
- Algunos objetos orbitando bloquean proyectiles.
- Algunos objetos hacen daño leve por contacto.
- La órbita debe ser controlada, no física caótica completa.

Capacidad sugerida:

| Objeto | Capacidad |
|---|---:|
| Chatarra liviana | 1 |
| Placa | 2 |
| Mina | 2 |
| Objeto pesado | 3 |

---

## 6.4 Repeler

Con un segundo click izquierdo, los objetos orbitando y enemigos retenidos salen disparados hacia el cursor.

### Reglas

- Consume todos los objetos retenidos.
- Daño por impacto.
- Knockback.
- Puede lanzar enemigos magnetizados.
- Es la fuente principal de daño.

Variables sugeridas:

| Variable | Valor |
|---|---:|
| Ángulo | cono de 50° |
| Cooldown | 0.25 s |
| Fuerza | alta |

---

## 6.5 Strike

Ataque corto en cono frontal.

### Función

- Marcar enemigos.
- Interrumpir ataques.
- Mantener combo.
- Preparar Pull/Repel.

### Reglas

- Bajo daño.
- Aplica marca magnética.
- Con 2 marcas, el enemigo queda magnetizado.
- Enemigos magnetizados pueden ser atraídos o repelidos.

Variables sugeridas:

| Variable | Valor |
|---|---:|
| Daño | bajo |
| Ángulo | 70° |
| Stacks para magnetizar | 2 |
| Cooldown | 0.35 s |

---

## 6.6 Marca magnética

Estados de enemigo:

| Estado | Efecto |
|---|---|
| Normal | No manipulable |
| Marcado x1 | Recibe bonus leve de daño por chatarra |
| Magnetizado x2 | Puede ser atraído/repelido |
| Aturdido | Ventana breve para lanzar |

Para la jam, basta con Normal, Marcado y Magnetizado.

---

## 6.7 Pull de enemigo magnetizado

Si un enemigo está magnetizado, atraer puede arrastrarlo.

### Reglas

- Enemigos pequeños se atraen rápido.
- Enemigos medianos se atraen lento.
- Enemigos pesados no se atraen salvo upgrade.
- Pull genera sobrecarga extra.

---

## 6.8 Counter / pulso

Cuando un enemigo ataca, aparece una ventana breve.  
Si el jugador presiona espacio:

- evita daño,
- repele al atacante,
- suma combo,
- genera Scrap XP.

Variables sugeridas:

| Variable | Valor |
|---|---:|
| Ventana | 0.35-0.5 s |
| Cooldown | 4-6 s |

---

## 6.9 Sobrecarga

La sobrecarga sube al retener muchos objetos o atraer enemigos.

### Estados

- Normal.
- Crítico.
- Overload.

### Reglas MVP

Si se llena:

- libera explosión radial,
- empuja enemigos,
- vacía la carga,
- deja al jugador vulnerable 0.5 s,
- no daña al jugador en la jam.

---

# 7. Objetos atraíbles

## 7.1 Chatarra liviana

- Capacidad: 1.
- Daño: bajo.
- Velocidad: alta.
- Abundante.
- Ideal para combos.

## 7.2 Placa metálica

- Capacidad: 2.
- Daño: medio.
- Puede bloquear proyectiles.
- Buena defensa orbital.

## 7.3 Mina metálica

- Capacidad: 2.
- Daño alto en área.
- Explota al impactar después de repeler.
- Alto riesgo / alta recompensa.

Regla de seguridad: no explota durante los primeros 0.5 s tras ser atraída.

## 7.4 Objeto pesado

Opcional.

- Capacidad: 3.
- Daño alto.
- Knockback alto.
- Velocidad baja.

---

# 8. Enemigos de la jam

## 8.1 Scrapling

Horda básica.

- Persigue al jugador.
- Bajo HP.
- Bajo daño.
- Sirve para combos.

## 8.2 Runner Bot

Presión rápida.

- Carga en línea recta.
- Tiene aviso antes de atacar.
- Ideal para enseñar counter.

## 8.3 Shield Bot

Enemigo de posicionamiento.

- Bloquea impactos frontales.
- Vulnerable por lados/espalda.
- Se puede lanzar contra paredes o minas.

## 8.4 Spitter Drone

Opcional.

- Dispara proyectiles metálicos.
- Sus proyectiles pueden ser atraídos y devueltos.

## 8.5 Mini-boss: Scrap Brute

Cierre de run.

### Comportamiento

- Grande y lento.
- Invoca Scraplings.
- Absorbe chatarra para repararse.
- Tiene ventanas de vulnerabilidad.

### Fases simples

1. Persigue y golpea.
2. Absorbe chatarra.
3. Se sobrecarga.
4. Ventana para dañarlo con minas u objetos pesados.

---

# 9. Scrap XP

La experiencia debe premiar estilo, no solo kills.

## Fuentes

| Fuente | Valor |
|---|---|
| Matar enemigos | base |
| Kill con chatarra repelida | medio |
| Combo multi-kill | alto |
| Counter perfecto | medio |
| Misión de estilo | alto |
| Reciclar restos | medio |

---

# 10. Misiones de estilo

Aparece una misión cada 45-60 segundos.

## Misiones MVP

### Combo Hunter

Mata 4 enemigos con una sola repulsión.

### Counterstorm

Realiza 2 counters correctos.

### Scrap Collector

Atrae 15 objetos.

### Wall Slam

Derriba 3 enemigos contra paredes.

### No Hands

Mata 5 enemigos usando solo objetos repelidos, sin Strike.

## Recompensas

- Scrap XP.
- Curación.
- Powerup.
- Reroll de upgrade.

---

# 11. Upgrades de jam

## 11.1 Pull

### Campo amplio

+20% radio de atracción.

### Bobina rápida

+25% velocidad de atracción.

### Garra magnética

Atraer enemigos magnetizados desde más lejos.

---

## 11.2 Repel

### Cañón de chatarra

+25% velocidad de objetos repelidos.

### Impacto brutal

+20% daño de repulsión.

### Derribo

Los impactos fuertes causan knockdown.

---

## 11.3 Combo

### Combo vital

Cada combo x8 cura 1 HP o da escudo temporal.

### Counter perfecto

Counter exitoso ralentiza el tiempo 1 segundo.

### Style bonus

Misiones de estilo dan +30% Scrap XP.

---

## 11.4 Capacidad / supervivencia

### Bolsillos magnéticos

+3 capacidad máxima.

### Núcleo estable

Sobrecarga sube 20% más lento.

### Placas de emergencia

+1 HP máximo.

---

## 11.5 Especiales

### Repeler 360

Cada X repulsiones, la siguiente es radial.

### Atraer enemigos

Pull afecta enemigos pequeños aunque no estén magnetizados.

### Mina amiga

Las minas no explotan cerca del jugador durante 8 segundos después de ser atraídas.

---

# 12. Powerups de jam

## Repeler 360

La próxima repulsión dispara objetos en todas las direcciones.

## Slow Time

Ralentiza enemigos y proyectiles durante 4 segundos.

## Magnet Fever

Durante 8 segundos, aumenta mucho el radio y velocidad de atracción.

## Enemy Pull

Durante 6 segundos, los enemigos pequeños pueden ser atraídos directamente.

---

# 13. Pacing de run

| Tiempo | Evento |
|---|---|
| 0:00 | Inicio y tutorial visual |
| 0:20 | Primeros Scraplings |
| 0:45 | Primer level up |
| 1:00 | Entra Runner Bot |
| 1:30 | Primera misión de estilo |
| 2:00 | Entra Shield Bot |
| 2:30 | Lluvia de chatarra / powerup |
| 3:00 | Oleada intensa |
| 3:30 | Aparece Scrap Brute |
| 4:30 | Extracción o final |
| 5:00 | Score screen |

---

# 14. UI mínima

- HP.
- Barra de sobrecarga.
- Capacidad actual.
- Scrap XP.
- Combo.
- Misión activa.
- Cooldown de counter.
- Indicador de enemigo magnetizado.
- Cono de repulsión.

La UI 2D del HUD se implementa con UI Toolkit. Las ayudas espaciales sobre el mundo, como el cono de repulsión, retícula de dirección e indicadores sobre enemigos, usan UI/meshes/VFX world-space de Unity.

---

# 15. Feedback obligatorio

- Sonido de atracción.
- Sonido de objetos entrando en órbita.
- Sonido potente de repulsión.
- Impactos claros.
- Hitstop breve.
- Cámara shake moderado.
- Combo popups.
- Alerta de sobrecarga.
- Slow motion en jugadas grandes.

---

# 16. Condiciones de victoria y derrota

## Victoria

- Derrotar al Scrap Brute, o
- sobrevivir hasta extracción.

## Derrota

- HP llega a 0.

## Score final

- Enemigos destruidos.
- Combo máximo.
- Misiones completadas.
- Daño recibido.
- Scrap reciclado.
- Mini-boss derrotado.

---

# 17. MVP técnico

## Debe estar

- Movimiento.
- Cámara.
- Atraer objetos.
- Órbita.
- Repeler.
- Daño por impacto.
- Strike.
- Marca magnética.
- Counter simple.
- Sobrecarga.
- 3 objetos.
- 3 enemigos.
- Scrap XP.
- 9-12 upgrades.
- 3 powerups.
- Oleadas.
- Score final.

## Puede quedar fuera

- Co-op.
- Polaridad avanzada.
- Meta-progresión.
- Muchos biomas.
- Boss complejo.
- Animaciones elaboradas.
- Narrativa.
- Leaderboards.

---

# 18. Roadmap de 10 días

## Día 1

Movimiento, cámara, atraer, órbita, repeler.

## Día 2

Daño, impactos, objetos, feedback inicial.

## Día 3

Strike, marca magnética, Pull de enemigo marcado.

## Día 4

Scrapling, Runner, Shield Bot.

## Día 5

XP, level up, upgrades.

## Día 6

Misiones de estilo y powerups.

## Día 7

Mini-boss y final de run.

## Día 8

UI, sonido, partículas, cámara shake.

## Día 9

Balance, optimización WebGL, bugs.

## Día 10

Tutorial, menú, build, deploy, página de jam.

---

# 19. Criterios de éxito

El prototipo es exitoso si:

1. Se entiende en menos de 20 segundos.
2. Atraer y repeler se siente bien.
3. Hay al menos una jugada espectacular por run.
4. Los combos salen naturalmente.
5. Las misiones empujan a jugar distinto.
6. El jugador quiere repetir una run.
