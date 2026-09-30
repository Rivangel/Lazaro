using UnityEngine;
using UnityEngine.SceneManagement;

//Lleva la cuenta de la partida. Los personajes no se destruyen al morir:
//se reutilizan, asi que reaparecer es solo devolverlos a su sitio con la vida llena.
//La partida se gana juntando las piezas del vehiculo, reparandolo y huyendo.
public class ControlPartida : MonoBehaviour
{
    public static ControlPartida instancia;

    [Header("Reaparicion")]
    public float tiempoReaparicionEnemigo = 5f;
    public float tiempoReaparicionJugador = 3f;

    [Header("Final")]
    public KeyCode teclaReiniciar = KeyCode.R;
    public AudioClip sonidoPieza;
    public AudioClip sonidoVictoria;
    public AudioSource fuenteAudio;

    [Header("Avisos")]
    public float duracionAviso = 3f;

    public int ZombiesEliminados { get; private set; }
    public int VecesMuerto { get; private set; }

    public int PiezasRecogidas { get; private set; }
    //se cuentan al empezar las piezas que hay repartidas en el mapa
    public int PiezasNecesarias { get; private set; }
    public bool TienePiezas { get { return PiezasRecogidas >= PiezasNecesarias; } }

    public bool PartidaTerminada { get; private set; }
    public int AliadosRescatados { get; private set; }
    public float TiempoDePartida { get; private set; }

    //mensaje corto que la interfaz muestra un momento en pantalla
    public string AvisoActual { get; private set; }
    private float avisoHasta;

    public int AliadosVivos
    {
        get
        {
            int cantidad = 0;
            foreach (Npc npc in Npc.Todos)
            {
                if (!npc.esEnemigo && !npc.EstaMuerto) cantidad++;
            }
            return cantidad;
        }
    }

    void Awake()
    {
        instancia = this;
        Time.timeScale = 1f;
        if (fuenteAudio == null) fuenteAudio = GetComponent<AudioSource>();
    }

    void Start()
    {
        foreach (Recogible recogible in FindObjectsByType<Recogible>(FindObjectsSortMode.None))
        {
            if (recogible.tipo == Recogible.Tipo.Pieza) PiezasNecesarias++;
        }

        if (PiezasNecesarias > 0)
        {
            Avisar("Encuentra " + PiezasNecesarias + " piezas y repara la camioneta para escapar");
        }
    }

    void Update()
    {
        if (!PartidaTerminada)
        {
            TiempoDePartida += Time.deltaTime;
        }
        else if (Input.GetKeyDown(teclaReiniciar))
        {
            Reiniciar();
        }

        if (AvisoActual != null && Time.unscaledTime > avisoHasta) AvisoActual = null;
    }

    public void ContarZombieEliminado()
    {
        ZombiesEliminados++;
    }

    public void ContarMuerteDelJugador()
    {
        VecesMuerto++;
    }

    public void Avisar(string mensaje)
    {
        AvisoActual = mensaje;
        avisoHasta = Time.unscaledTime + duracionAviso;
    }

    public void RecogerPieza(string nombre)
    {
        PiezasRecogidas++;

        if (TienePiezas) Avisar("Tienes todas las piezas. Ve al vehiculo y reparalo");
        else Avisar(nombre + " (" + PiezasRecogidas + "/" + PiezasNecesarias + ")");

        if (fuenteAudio != null && sonidoPieza != null) fuenteAudio.PlayOneShot(sonidoPieza);
    }

    //el vehiculo arranco: se congela el juego y se muestra el resumen
    public void Escapar(int aliados)
    {
        if (PartidaTerminada) return;

        PartidaTerminada = true;
        AliadosRescatados = aliados;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fuenteAudio != null && sonidoVictoria != null) fuenteAudio.PlayOneShot(sonidoVictoria);
    }

    //El generador agrega la escena a Build Settings, que es lo que pide LoadScene.
    void Reiniciar()
    {
        int indice = SceneManager.GetActiveScene().buildIndex;
        if (indice < 0)
        {
            Debug.LogWarning("La escena no esta en Build Settings; vuelve a generar el mapa o agregala a mano.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(indice);
    }
}
