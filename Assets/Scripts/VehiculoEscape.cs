using UnityEngine;

//La camioneta para huir del pueblo. Con todas las piezas, el jugador se acerca y
//mantiene presionada la tecla para repararla. Reparar hace mucho ruido: atrae a
//los zombies de alrededor, asi que conviene limpiar la zona o traer aliados.
public class VehiculoEscape : MonoBehaviour
{
    public KeyCode teclaReparar = KeyCode.E;
    public float radioInteraccion = 4.5f;

    //segundos de reparacion; si el jugador suelta la tecla, el avance se conserva
    public float tiempoReparacion = 6f;

    //los aliados que esten a esta distancia al arrancar escapan con el jugador
    public float radioAbordaje = 12f;

    [Header("Ruido")]
    public float ruidoReparacion = 22f;
    public float tiempoEntreGolpes = 0.8f;
    public AudioClip sonidoReparacion;
    public AudioSource fuenteAudio;

    //0 = sin reparar, 1 = listo para arrancar
    public float Progreso { get; private set; }

    //lo lee la interfaz para mostrar la instruccion en pantalla
    public static VehiculoEscape instancia;
    public bool JugadorCerca { get; private set; }

    private Jugador jugador;
    private Vida vidaJugador;
    private float tiempoSiguienteGolpe;

    void Awake()
    {
        instancia = this;
    }

    void Start()
    {
        jugador = FindFirstObjectByType<Jugador>();
        if (jugador != null) vidaJugador = jugador.GetComponent<Vida>();
        if (fuenteAudio == null) fuenteAudio = GetComponent<AudioSource>();
    }

    void Update()
    {
        ControlPartida partida = ControlPartida.instancia;
        if (jugador == null || partida == null || partida.PartidaTerminada) return;

        Vector3 diferencia = jugador.transform.position - transform.position;
        diferencia.y = 0f;
        JugadorCerca = diferencia.magnitude <= radioInteraccion && (vidaJugador == null || !vidaJugador.EstaMuerto);

        if (!JugadorCerca || !partida.TienePiezas) return;
        if (!Input.GetKey(teclaReparar)) return;

        Progreso = Mathf.Clamp01(Progreso + Time.deltaTime / tiempoReparacion);

        //martillazos: cada uno se oye por todo el barrio
        if (Time.time >= tiempoSiguienteGolpe)
        {
            tiempoSiguienteGolpe = Time.time + tiempoEntreGolpes;
            Ruido.Emitir(transform.position, ruidoReparacion, true);
            if (fuenteAudio != null && sonidoReparacion != null) fuenteAudio.PlayOneShot(sonidoReparacion);
        }

        if (Progreso >= 1f)
        {
            partida.Escapar(ContarAliadosCerca());
        }
    }

    int ContarAliadosCerca()
    {
        int cantidad = 0;
        foreach (Npc npc in Npc.Todos)
        {
            if (npc.esEnemigo || npc.EstaMuerto) continue;
            if (Vector3.Distance(npc.transform.position, transform.position) <= radioAbordaje) cantidad++;
        }
        return cantidad;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioInteraccion);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, radioAbordaje);
    }
}
