using UnityEngine;

public class Obstaculo : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        Jugador jugador = other.GetComponent<Jugador>();
        if (jugador != null)
        {
            jugador.Reiniciar();
        }
    }
}
