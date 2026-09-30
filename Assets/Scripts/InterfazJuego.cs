using UnityEngine;
using UnityEngine.UI;

//Barra de vida del jugador y contador de zombies. El generador crea el Canvas
//y rellena estas referencias; aqui solo se actualizan.
public class InterfazJuego : MonoBehaviour
{
    [Header("Referencias")]
    public Vida vidaJugador;
    public Image rellenoBarra;
    public Text textoVida;
    public Text textoZombies;

    [Header("Colores de la barra")]
    public Color colorLleno = new Color(0.3f, 0.8f, 0.35f);
    public Color colorMedio = new Color(0.9f, 0.75f, 0.2f);
    public Color colorBajo = new Color(0.85f, 0.25f, 0.25f);

    void Start()
    {
        if (vidaJugador == null)
        {
            Jugador jugador = FindFirstObjectByType<Jugador>();
            if (jugador != null) vidaJugador = jugador.GetComponent<Vida>();
        }

        if (vidaJugador != null)
        {
            vidaJugador.AlCambiar += Refrescar;
            //pedir el valor actual, por si la vida se inicializo antes que esta suscripcion
            vidaJugador.Avisar();
        }
    }

    void OnDestroy()
    {
        if (vidaJugador != null)
        {
            vidaJugador.AlCambiar -= Refrescar;
        }
    }

    void Update()
    {
        if (textoZombies != null && ControlPartida.instancia != null)
        {
            textoZombies.text = "Zombies: " + ControlPartida.instancia.ZombiesEliminados;
        }
    }

    void Refrescar(int actual, int maxima)
    {
        float porcentaje = maxima > 0 ? (float)actual / maxima : 0f;

        if (rellenoBarra != null)
        {
            //se mueve el ancla derecha del relleno; la izquierda se queda pegada
            RectTransform rect = rellenoBarra.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(porcentaje, 1f);

            //verde -> amarillo -> rojo segun lo que quede
            if (porcentaje > 0.6f) rellenoBarra.color = colorLleno;
            else if (porcentaje > 0.3f) rellenoBarra.color = colorMedio;
            else rellenoBarra.color = colorBajo;
        }

        if (textoVida != null)
        {
            textoVida.text = actual + " / " + maxima;
        }
    }
}
