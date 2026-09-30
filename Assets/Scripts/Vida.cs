using System;
using UnityEngine;

//Componente de vida compartido: lo llevan el jugador y los zombies.
//Quien dispara solo busca este componente, no le importa a quien pertenece.
public class Vida : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMaxima = 5;

    //tiempo sin poder recibir otro golpe despues de uno; evita morir de diez golpes seguidos
    public float tiempoDeGracia = 0.3f;

    [Header("Sonidos")]
    public AudioClip sonidoGolpe;
    public AudioClip sonidoMuerte;

    //si se deja vacio se busca un AudioSource en el objeto
    public AudioSource fuenteAudio;

    public int VidaActual { get; private set; }
    public bool EstaMuerto { get { return VidaActual <= 0; } }

    public float Porcentaje
    {
        get { return vidaMaxima > 0 ? (float)VidaActual / vidaMaxima : 0f; }
    }

    //avisan a la interfaz, al script del personaje y a lo que haga falta
    public event Action<int, int> AlCambiar;
    public event Action AlRecibirGolpe;
    public event Action AlMorir;

    private float tiempoUltimoGolpe = -999f;

    void Awake()
    {
        VidaActual = vidaMaxima;

        if (fuenteAudio == null)
        {
            fuenteAudio = GetComponent<AudioSource>();
        }
    }

    public void RecibirDanio(int cantidad)
    {
        if (EstaMuerto || cantidad <= 0) return;
        if (Time.time < tiempoUltimoGolpe + tiempoDeGracia) return;

        tiempoUltimoGolpe = Time.time;
        VidaActual = Mathf.Max(0, VidaActual - cantidad);

        if (AlCambiar != null) AlCambiar(VidaActual, vidaMaxima);

        if (EstaMuerto)
        {
            Reproducir(sonidoMuerte);
            if (AlMorir != null) AlMorir();
        }
        else
        {
            Reproducir(sonidoGolpe);
            if (AlRecibirGolpe != null) AlRecibirGolpe();
        }
    }

    public void Curar(int cantidad)
    {
        if (EstaMuerto || cantidad <= 0) return;

        VidaActual = Mathf.Min(vidaMaxima, VidaActual + cantidad);
        if (AlCambiar != null) AlCambiar(VidaActual, vidaMaxima);
    }

    //deja la vida al maximo; se usa al reaparecer
    public void Restaurar()
    {
        VidaActual = vidaMaxima;
        tiempoUltimoGolpe = -999f;
        if (AlCambiar != null) AlCambiar(VidaActual, vidaMaxima);
    }

    //para que la interfaz pueda pedir el valor en cuanto se suscribe
    public void Avisar()
    {
        if (AlCambiar != null) AlCambiar(VidaActual, vidaMaxima);
    }

    void Reproducir(AudioClip clip)
    {
        if (clip == null || fuenteAudio == null) return;
        fuenteAudio.PlayOneShot(clip);
    }
}
