using UnityEngine;

//Lleva la cuenta de la partida. Los personajes no se destruyen al morir:
//se reutilizan, asi que reaparecer es solo devolverlos a su sitio con la vida llena.
public class ControlPartida : MonoBehaviour
{
    public static ControlPartida instancia;

    [Header("Reaparicion")]
    public float tiempoReaparicionEnemigo = 5f;
    public float tiempoReaparicionJugador = 3f;

    public int ZombiesEliminados { get; private set; }
    public int VecesMuerto { get; private set; }

    void Awake()
    {
        instancia = this;
    }

    public void ContarZombieEliminado()
    {
        ZombiesEliminados++;
    }

    public void ContarMuerteDelJugador()
    {
        VecesMuerto++;
    }
}
