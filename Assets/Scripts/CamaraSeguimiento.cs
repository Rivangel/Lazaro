using UnityEngine;

public class CamaraSeguimiento : MonoBehaviour
{
    [Header("Objetivo")]
    //objeto que la camara sigue (arrastrar el Jugador aqui en el Inspector)
    public Transform objetivo;

    //altura del punto al que mira la camara, medida desde los pies del objetivo
    public float alturaMirada = 1.5f;

    [Header("Distancia")]
    public float distancia = 8f;

    //que tan rapido se acerca al chocar con una pared y que tan rapido vuelve a alejarse;
    //alejarse va mas lento para que no rebote si la pared aparece y desaparece
    public float suavizadoAcercar = 20f;
    public float suavizadoAlejar = 4f;

    [Header("Mouse")]
    public bool usarMouse = true;
    public float sensibilidad = 3f;
    public float anguloMinimo = -20f;
    public float anguloMaximo = 65f;
    public bool bloquearCursor = true;

    [Header("Colision")]
    //evita que la camara se meta dentro de paredes
    public bool evitarParedes = true;
    public LayerMask capasQueBloquean = ~0;

    //grosor del "rayo" de colision; una esfera evita que se cuele por rendijas
    public float radioColision = 0.25f;
    public float distanciaMinima = 1f;

    private float anguloHorizontal;
    private float anguloVertical = 20f;
    private float distanciaActual;

    void Start()
    {
        if (objetivo == null)
        {
            Jugador jugador = FindFirstObjectByType<Jugador>();
            if (jugador != null) objetivo = jugador.transform;
        }

        anguloHorizontal = transform.eulerAngles.y;
        distanciaActual = distancia;

        if (bloquearCursor && usarMouse)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (objetivo == null) return;

        //al terminar la partida el cursor queda libre para la pantalla final
        bool partidaTerminada = ControlPartida.instancia != null && ControlPartida.instancia.PartidaTerminada;

        if (usarMouse && !partidaTerminada)
        {
            anguloHorizontal += Input.GetAxis("Mouse X") * sensibilidad;
            anguloVertical -= Input.GetAxis("Mouse Y") * sensibilidad;
            anguloVertical = Mathf.Clamp(anguloVertical, anguloMinimo, anguloMaximo);
        }

        //La camara va pegada al jugador, sin retraso: si se suavizara la posicion, al
        //correr se quedaria atras y la distancia cambiaria con la velocidad.
        //Lo unico que se suaviza es la distancia, para acercarse al chocar con paredes.
        Vector3 puntoMirada = objetivo.position + Vector3.up * alturaMirada;
        Quaternion rotacion = Quaternion.Euler(anguloVertical, anguloHorizontal, 0f);
        Vector3 haciaAtras = rotacion * Vector3.back;

        float distanciaDeseada = evitarParedes ? DistanciaLibre(puntoMirada, haciaAtras) : distancia;

        float velocidad = distanciaDeseada < distanciaActual ? suavizadoAcercar : suavizadoAlejar;
        distanciaActual = Mathf.Lerp(distanciaActual, distanciaDeseada, velocidad * Time.deltaTime);

        transform.position = puntoMirada + haciaAtras * distanciaActual;
        transform.rotation = Quaternion.LookRotation(puntoMirada - transform.position);
    }

    //Hasta donde puede alejarse la camara sin meterse en una pared. Los personajes
    //no cuentan: si un zombie o un aliado corre detras del jugador, la camara no
    //debe saltar hacia adelante cada vez que se cruza.
    float DistanciaLibre(Vector3 origen, Vector3 direccion)
    {
        float libre = distancia;

        RaycastHit[] tocados = Physics.SphereCastAll(origen, radioColision, direccion, distancia,
                                                     capasQueBloquean, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit tocado in tocados)
        {
            if (tocado.collider is CharacterController) continue;
            if (tocado.transform.IsChildOf(objetivo)) continue;

            //distance 0 = la esfera ya empezaba tocando algo; eso no es una pared detras
            if (tocado.distance <= 0f) continue;

            libre = Mathf.Min(libre, tocado.distance);
        }

        return Mathf.Max(distanciaMinima, libre);
    }
}
