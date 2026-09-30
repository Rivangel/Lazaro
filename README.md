# Lázaro

Juego de acción en tercera persona hecho en Unity. Recorres un pueblo invadido por zombies, disparas para sobrevivir y vuelves a aparecer cada vez que caes.

> **En desarrollo.** La idea es que el juego no se trate de matar zombies, sino de curarlos y devolverlos a la vida (de ahí el nombre).

## Controles

| Acción | Tecla |
|---|---|
| Moverse | `W` `A` `S` `D` / flechas |
| Correr | `Shift` izquierdo |
| Saltar | `Espacio` |
| Mirar / apuntar | Mouse |
| Disparar | Clic izquierdo / `Ctrl` izquierdo |

## Requisitos

- Unity **2023.2.20f1**

## Cómo abrirlo

1. Clona el repositorio:
   ```bash
   git clone https://github.com/Rivangel/Lazaro.git
   ```
2. Abre la carpeta con Unity Hub (**Add project from disk**). La primera vez tarda un poco porque Unity regenera la carpeta `Library/`.
3. Si la escena está vacía o algo falla, usa el menú **Practica1**:
   - **1 - Configurar Modelos**: prepara los FBX, los rigs y los Animator Controllers.
   - **2 - Generar Mapa**: arma la escena con el jugador, los zombies, la cámara y la interfaz.
4. Abre `Assets/Scenes/SampleScene.unity` y presiona **Play**.

## Estructura

```
Assets/
├── Scripts/       Lógica del juego
│   ├── Jugador.cs            Movimiento, salto, muerte y reaparición
│   ├── Npc.cs                IA de los zombies (pasear, perseguir, atacar)
│   ├── Vida.cs               Vida compartida por jugador y zombies
│   ├── Disparo.cs            Disparo instantáneo o con proyectil
│   ├── Proyectil.cs          Bala que viaja y detecta impactos
│   ├── CamaraSeguimiento.cs  Cámara en tercera persona con mouse
│   ├── ControlPartida.cs     Contadores de la partida
│   ├── InterfazJuego.cs      Barra de vida y contador
│   └── Obstaculo.cs          Zonas que regresan al jugador al inicio
├── Editor/        Herramientas del menú Practica1
├── Modelos/       Personajes, zombies, vehículos y entorno
├── Animaciones/   Animator Controllers
├── Materiales/
├── Audio/
├── Prefabs/
└── Scenes/
```

## Ideas en camino

- Munición que cura zombies y los convierte en sobrevivientes
- Ruido y sigilo: disparar atrae zombies
- Reparar un vehículo para escapar del pueblo
- Ciclo de día y noche

## Créditos

- Modelos 3D: [Quaternius](https://quaternius.com) (CC0).
- Los efectos de sonido pertenecen a sus respectivos autores y se usan solo con fines educativos.
