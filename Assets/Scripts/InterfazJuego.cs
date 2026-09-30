using UnityEngine;
using UnityEngine.UI;

//Todo lo que se ve en pantalla: vida, ruido, municion, piezas, aliados, avisos y
//la pantalla final. El generador crea el Canvas y rellena estas referencias;
//aqui solo se actualizan.
public class InterfazJuego : MonoBehaviour
{
    [Header("Referencias")]
    public Vida vidaJugador;
    public Image rellenoBarra;
    public Text textoVida;
    public Text textoZombies;

    [Header("Sigilo")]
    public Image rellenoRuido;
    public Text textoPostura;

    [Header("Mision")]
    public Text textoMunicion;
    public Text textoPiezas;
    public Text textoAliados;
    public Text textoAviso;
    //instruccion de abajo al centro ("Manten E para reparar")
    public Text textoInstruccion;

    [Header("Final")]
    public GameObject panelFinal;
    public Text textoFinal;

    [Header("Colores de la barra")]
    public Color colorLleno = new Color(0.3f, 0.8f, 0.35f);
    public Color colorMedio = new Color(0.9f, 0.75f, 0.2f);
    public Color colorBajo = new Color(0.85f, 0.25f, 0.25f);

    [Header("Ruido")]
    //cuanto baja la barra de ruido por segundo
    public float caidaRuido = 1.2f;
    public Color colorSilencio = new Color(0.4f, 0.75f, 1f);
    public Color colorEstruendo = new Color(1f, 0.35f, 0.25f);

    private Jugador jugador;
    private Disparo disparo;
    private float nivelRuido;

    void Start()
    {
        jugador = FindFirstObjectByType<Jugador>();
        if (jugador != null)
        {
            disparo = jugador.GetComponent<Disparo>();
            if (vidaJugador == null) vidaJugador = jugador.GetComponent<Vida>();
        }

        if (vidaJugador != null)
        {
            vidaJugador.AlCambiar += Refrescar;
            //pedir el valor actual, por si la vida se inicializo antes que esta suscripcion
            vidaJugador.Avisar();
        }

        Ruido.AlEmitir += AlOirRuido;

        if (panelFinal != null) panelFinal.SetActive(false);
    }

    void OnDestroy()
    {
        if (vidaJugador != null)
        {
            vidaJugador.AlCambiar -= Refrescar;
        }

        Ruido.AlEmitir -= AlOirRuido;
    }

    void Update()
    {
        ControlPartida partida = ControlPartida.instancia;

        if (textoZombies != null && partida != null)
        {
            textoZombies.text = "Zombies eliminados: " + partida.ZombiesEliminados;
        }

        RefrescarRuido();
        RefrescarMision(partida);
        RefrescarFinal(partida);
    }

    // ---------- vida ----------

    void Refrescar(int actual, int maxima)
    {
        float porcentaje = maxima > 0 ? (float)actual / maxima : 0f;

        if (rellenoBarra != null)
        {
            //se mueve el ancla derecha del relleno; la izquierda se queda pegada
            AjustarAncho(rellenoBarra, porcentaje);

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

    static void AjustarAncho(Image imagen, float porcentaje)
    {
        RectTransform rect = imagen.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(porcentaje), 1f);
    }

    // ---------- sigilo ----------

    //solo cuenta el ruido que hace el jugador, no el de las piedras que lanza
    void AlOirRuido(Vector3 posicion, float radio, bool delJugador)
    {
        if (!delJugador) return;
        nivelRuido = Mathf.Max(nivelRuido, radio / Ruido.RadioMaximo);
    }

    void RefrescarRuido()
    {
        nivelRuido = Mathf.Max(0f, nivelRuido - caidaRuido * Time.deltaTime);

        if (rellenoRuido != null)
        {
            AjustarAncho(rellenoRuido, nivelRuido);
            rellenoRuido.color = Color.Lerp(colorSilencio, colorEstruendo, nivelRuido);
        }

        if (textoPostura != null && jugador != null)
        {
            switch (jugador.PosturaActual)
            {
                case Jugador.Postura.Agachado: textoPostura.text = "Agachado (sigilo)"; break;
                case Jugador.Postura.Caminando: textoPostura.text = "Caminando"; break;
                case Jugador.Postura.Corriendo: textoPostura.text = "Corriendo (ruidoso)"; break;
                default: textoPostura.text = "Quieto"; break;
            }
        }
    }

    // ---------- mision ----------

    void RefrescarMision(ControlPartida partida)
    {
        if (textoMunicion != null && disparo != null)
        {
            bool suero = disparo.MunicionActual == Disparo.Municion.Suero;
            string piedra = disparo.CargaDistraccion >= 1f ? "lista" : "cargando";

            textoMunicion.text = (suero ? "> SUERO " : "  Suero ") + disparo.DosisSuero + "/" + disparo.dosisMaximas
                + "\n" + (suero ? "  Balas" : "> BALAS")
                + "\nPiedra (G): " + piedra;
        }

        if (partida == null) return;

        if (textoPiezas != null)
        {
            textoPiezas.text = "Piezas: " + partida.PiezasRecogidas + " / " + partida.PiezasNecesarias;
        }

        if (textoAliados != null)
        {
            textoAliados.text = "Aliados: " + partida.AliadosVivos;
        }

        if (textoAviso != null)
        {
            textoAviso.text = partida.AvisoActual ?? "";
        }

        if (textoInstruccion != null)
        {
            textoInstruccion.text = Instruccion(partida);
        }
    }

    string Instruccion(ControlPartida partida)
    {
        VehiculoEscape vehiculo = VehiculoEscape.instancia;
        if (vehiculo == null || !vehiculo.JugadorCerca || partida.PartidaTerminada) return "";

        if (!partida.TienePiezas)
        {
            int faltan = partida.PiezasNecesarias - partida.PiezasRecogidas;
            return "Faltan " + faltan + " piezas para reparar el vehiculo";
        }

        int porcentaje = Mathf.RoundToInt(vehiculo.Progreso * 100f);
        return "Manten " + vehiculo.teclaReparar + " para reparar (" + porcentaje + "%). Hace mucho ruido";
    }

    // ---------- final ----------

    void RefrescarFinal(ControlPartida partida)
    {
        if (panelFinal == null || partida == null) return;

        bool mostrar = partida.PartidaTerminada;
        if (panelFinal.activeSelf != mostrar) panelFinal.SetActive(mostrar);
        if (!mostrar || textoFinal == null) return;

        int minutos = Mathf.FloorToInt(partida.TiempoDePartida / 60f);
        int segundos = Mathf.FloorToInt(partida.TiempoDePartida % 60f);

        textoFinal.text = "ESCAPASTE DEL PUEBLO\n\n"
            + "Sobrevivientes rescatados: " + partida.AliadosRescatados + "\n"
            + "Zombies eliminados: " + partida.ZombiesEliminados + "\n"
            + "Veces que caiste: " + partida.VecesMuerto + "\n"
            + "Tiempo: " + minutos + ":" + segundos.ToString("00") + "\n\n"
            + "Presiona " + partida.teclaReiniciar + " para jugar otra vez";
    }
}
