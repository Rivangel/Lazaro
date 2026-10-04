# Lázaro

Juego de acción y sigilo en tercera persona hecho en Unity. Estás atrapado en un pueblo invadido por zombies, pero los zombies son personas y tienes un suero que puede devolverlas a la vida (de ahí el nombre).

## Cómo se juega

- **Objetivo:** encuentra las 4 piezas repartidas por el pueblo (rueda, bidón de gasolina, tubo de escape y caja de herramientas), repara la camioneta marcada con luz amarilla y huye.
- **Curar en vez de matar:** el suero (dardo verde) convierte a un zombie en un sobreviviente que te sigue y pelea a tu lado. Hay pocas dosis, pero encontrarás frascos verdes por el mapa. Si a un aliado lo matan, vuelve a ser zombie.
- **Sigilo o escándalo:** los zombies te ven según cómo te muevas (agachado casi no te ven, corriendo te ven de lejos) y oyen todo lo que hace ruido: disparos, pasos, saltos. El suero casi no suena y las balas se oyen en medio pueblo. Las paredes tapan la vista, pero no el ruido.
- **Distracciones:** lanza una piedra y los zombies irán a revisar dónde cayó.
- **El final:** reparar la camioneta hace muchísimo ruido y atrae a los zombies cercanos. Los aliados que estén cerca al arrancar escapan contigo y cuentan en el resumen final.

## Controles

| Acción | Tecla |
|---|---|
| Moverse | `W` `A` `S` `D` / flechas |
| Correr | `Shift` izquierdo |
| Agacharse (sigilo) | `C` |
| Saltar | `Espacio` |
| Mirar / apuntar | Mouse |
| Disparar | Clic izquierdo / `Ctrl` izquierdo |
| Balas / Suero | `1` / `2` (o `Q` para alternar) |
| Lanzar piedra | `G` |
| Reparar la camioneta | Mantener `E` |
| Jugar otra vez (al ganar) | `R` |

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
   - **2 - Generar Mapa**: arma la escena con el jugador, los zombies, las piezas, el suero, la camioneta, la cámara y la interfaz.
4. Abre `Assets/Scenes/SampleScene.unity`, guárdala y presiona **Play**. Guárdala antes de generar el mapa para que se agregue a Build Settings y funcione el reinicio con `R`.

## Estructura

```
Assets/
├── Scripts/       Lógica del juego
│   ├── Jugador.cs            Movimiento, agacharse, pasos ruidosos, muerte y reaparición
│   ├── Npc.cs                Zombies (vista, oído, investigar, atacar) y aliados curados
│   ├── Vida.cs               Vida compartida por jugador, zombies y aliados
│   ├── Disparo.cs            Balas, suero y piedras de distracción
│   ├── Proyectil.cs          Bala o dardo que viaja y detecta impactos
│   ├── Ruido.cs              Avisa a los zombies de cada ruido y su alcance
│   ├── Distraccion.cs        Piedra que hace ruido donde cae
│   ├── Recogible.cs          Piezas del vehículo y dosis de suero
│   ├── VehiculoEscape.cs     Reparación de la camioneta y huida
│   ├── CamaraSeguimiento.cs  Cámara en tercera persona con mouse
│   ├── ControlPartida.cs     Piezas, avisos, final de la partida y reinicio
│   ├── InterfazJuego.cs      Vida, ruido, munición, piezas, aliados y pantalla final
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

- Ciclo de día y noche con farolas que asustan a los zombies
- Perro compañero que avisa cuando se acercan zombies

## Créditos

- Modelos 3D: [Quaternius](https://quaternius.com) (CC0).
- Los efectos de sonido pertenecen a sus respectivos autores y se usan solo con fines educativos.
