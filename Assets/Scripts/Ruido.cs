using System;
using UnityEngine;

//Todo lo que hace ruido (disparos, pasos, golpes, la reparacion del vehiculo)
//pasa por aqui. Los zombies que esten dentro del radio van a investigar el punto,
//aunque haya paredes en medio: el ruido atraviesa todo, la vista no.
public static class Ruido
{
    //radio de referencia para la barra de ruido de la interfaz (un disparo llena la barra)
    public const float RadioMaximo = 25f;

    //lo escucha la interfaz para dibujar la barra de ruido del jugador
    public static event Action<Vector3, float, bool> AlEmitir;

    public static void Emitir(Vector3 posicion, float radio, bool delJugador)
    {
        if (radio <= 0f) return;

        foreach (Npc npc in Npc.Todos)
        {
            npc.Oir(posicion, radio);
        }

        if (AlEmitir != null) AlEmitir(posicion, radio, delJugador);
    }
}
