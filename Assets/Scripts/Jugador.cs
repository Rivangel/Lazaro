using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Jugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 6f;
    public float velocidadCorriendo = 10f;
    public KeyCode teclaCorrer = KeyCode.LeftShift;

    [Header("Sigilo")]
    //agacharse es lento pero silencioso y los zombies te ven mucho mas cerca
    public KeyCode teclaAgacharse = KeyCode.C;
    public float velocidadAgachado = 2.5f;

    //multiplica el radio de vista de los zombies segun como se mueva el jugador
    public float visibilidadAgachado = 0.35f;
    public float visibilidadQuieto = 0.5f;
    public float visibilidadCaminando = 0.65f;
    public float visibilidadCorriendo = 1f;

    //radio en el que se oyen los pasos; agachado no hace ruido
    public float ruidoCaminando = 3f;
    public float ruidoCorriendo = 9f;
    public float ruidoAterrizaje = 6f;
    public float tiempoEntrePasos = 0.45f;

    //que tan rapido gira el personaje hacia la direccion en la que camina
    public float velocidadGiro = 12f;

    [Header("Salto y gravedad")]
    public float fuerzaSalto = 8f;
    public float gravedad = -20f;

    [Header("Camara")]
    //la camara decide hacia donde es "adelante"; si se deja vacio se usa la camara principal
    public Transform camara;

    [Header("Caida")]
    //si el jugador cae por debajo de esta altura vuelve al punto de inicio
    public float alturaLimite = -15f;

    [Header("Visual")]
    //objeto hijo con el modelo; aqui va el personaje importado
    public Transform visual;

    //si se deja vacio se busca el Animator dentro de los hijos
    public Animator animador;

    //que tan rapido reacciona la animacion al cambiar de velocidad
    public float suavizadoAnimacion = 10f;

    private CharacterController controller;
    private Vida vida;
    private Disparo disparo;
    private Vector3 velocidadVertical;
    private Vector3 puntoInicio;
    private float velocidadAnimada;
    private bool muerto;
    private bool estabaEnElSuelo = true;
    private float tiempoSiguientePaso;
    private bool animadorTieneAgachado;

    //direccion horizontal en la que se movio el jugador este frame (la usan otros scripts)
    public Vector3 DireccionMovimiento { get; private set; }

    public enum Postura { Quieto, Agachado, Caminando, Corriendo }
    public Postura PosturaActual { get; private set; }
    public bool Agachado { get; private set; }

    //cuanto se deja ver ahora mismo (0 = invisible, 1 = a la vista de todos)
    public float Visibilidad
    {
        get
        {
            switch (PosturaActual)
            {
                case Postura.Agachado: return visibilidadAgachado;
                case Postura.Caminando: return visibilidadCaminando;
                case Postura.Corriendo: return visibilidadCorriendo;
                default: return visibilidadQuieto;
            }
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        puntoInicio = transform.position;

        if (camara == null && Camera.main != null)
        {
            camara = Camera.main.transform;
        }

        if (visual == null)
        {
            Transform hijo = transform.Find("Visual");
            if (hijo != null) visual = hijo;
        }

        if (animador == null)
        {
            animador = GetComponentInChildren<Animator>();
        }

        //si el Animator Controller es viejo y no tiene el parametro, no se le manda
        animadorTieneAgachado = TieneParametro(animador, "Agachado");

        disparo = GetComponent<Disparo>();

        vida = GetComponent<Vida>();
        if (vida != null)
        {
            vida.AlRecibirGolpe += Golpeado;
            vida.AlMorir += Morir;
        }
    }

    void OnDestroy()
    {
        if (vida != null)
        {
            vida.AlRecibirGolpe -= Golpeado;
            vida.AlMorir -= Morir;
        }
    }

    void Update()
    {
        //mientras esta muerto no se mueve ni dispara, solo espera a reaparecer
        if (muerto) return;
        if (ControlPartida.instancia != null && ControlPartida.instancia.PartidaTerminada) return;

        Movimiento();
        RevisarCaida();
    }

    //gira al personaje de golpe hacia una direccion (lo usa el disparo al apuntar)
    public void MirarHacia(Vector3 direccion)
    {
        if (direccion.sqrMagnitude < 0.01f) return;

        Transform aGirar = visual != null ? visual : transform;
        aGirar.rotation = Quaternion.LookRotation(direccion);
    }

    void Golpeado()
    {
        if (animador != null) animador.SetTrigger("Golpe");
    }

    void Morir()
    {
        muerto = true;

        if (animador != null)
        {
            animador.SetFloat("Velocidad", 0f);
            animador.ResetTrigger("Revivir");
            animador.SetTrigger("Morir");
        }

        if (disparo != null) disparo.puedeDisparar = false;

        float espera = 3f;
        if (ControlPartida.instancia != null)
        {
            ControlPartida.instancia.ContarMuerteDelJugador();
            espera = ControlPartida.instancia.tiempoReaparicionJugador;
        }

        Invoke("Revivir", espera);
    }

    //vuelve al punto de inicio con la vida llena
    public void Revivir()
    {
        muerto = false;
        Agachado = false;
        velocidadAnimada = 0f;

        Reiniciar();

        if (vida != null) vida.Restaurar();
        if (disparo != null) disparo.puedeDisparar = true;

        if (animador != null)
        {
            animador.ResetTrigger("Morir");
            animador.SetTrigger("Revivir");
        }
    }

    void Movimiento()
    {
        //entrada del teclado (WASD / flechas)
        Vector3 entrada = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        if (entrada.sqrMagnitude > 1f) entrada.Normalize();

        //convertir la entrada a la orientacion de la camara
        Vector3 direccion = entrada;
        if (camara != null)
        {
            Vector3 adelante = camara.forward;
            Vector3 derecha = camara.right;
            adelante.y = 0f;
            derecha.y = 0f;
            adelante.Normalize();
            derecha.Normalize();
            direccion = adelante * entrada.z + derecha * entrada.x;
        }

        DireccionMovimiento = direccion;

        if (Input.GetKeyDown(teclaAgacharse)) Agachado = !Agachado;

        //correr te levanta
        bool corriendo = Input.GetKey(teclaCorrer);
        if (corriendo) Agachado = false;

        float velocidadActual = Agachado ? velocidadAgachado : (corriendo ? velocidadCorriendo : velocidad);
        controller.Move(direccion * velocidadActual * Time.deltaTime);

        bool seMueve = direccion.sqrMagnitude > 0.01f;
        if (Agachado) PosturaActual = Postura.Agachado;
        else if (!seMueve) PosturaActual = Postura.Quieto;
        else PosturaActual = corriendo ? Postura.Corriendo : Postura.Caminando;

        //girar el personaje hacia donde camina
        if (direccion.sqrMagnitude > 0.01f)
        {
            Transform aGirar = visual != null ? visual : transform;
            Quaternion rotacionDeseada = Quaternion.LookRotation(direccion);
            aGirar.rotation = Quaternion.Slerp(aGirar.rotation, rotacionDeseada, velocidadGiro * Time.deltaTime);
        }

        //si esta en el suelo se resetea la velocidad vertical para que no se acumule
        if (controller.isGrounded && velocidadVertical.y < 0f)
        {
            velocidadVertical.y = -2f;
        }

        bool salto = controller.isGrounded && Input.GetButtonDown("Jump");
        if (salto)
        {
            velocidadVertical.y = fuerzaSalto;
            Agachado = false;
        }

        velocidadVertical.y += gravedad * Time.deltaTime;
        controller.Move(velocidadVertical * Time.deltaTime);

        HacerRuido();
        Animar(direccion, corriendo && !Agachado, salto);
    }

    //los pasos y los aterrizajes avisan a los zombies cercanos
    void HacerRuido()
    {
        bool enSuelo = controller.isGrounded;
        if (enSuelo && !estabaEnElSuelo)
        {
            Ruido.Emitir(transform.position, ruidoAterrizaje, true);
        }
        estabaEnElSuelo = enSuelo;

        if (!enSuelo || Time.time < tiempoSiguientePaso) return;

        float radio = 0f;
        if (PosturaActual == Postura.Caminando) radio = ruidoCaminando;
        else if (PosturaActual == Postura.Corriendo) radio = ruidoCorriendo;
        if (radio <= 0f) return;

        tiempoSiguientePaso = Time.time + tiempoEntrePasos;
        Ruido.Emitir(transform.position, radio, true);
    }

    //pasa el estado del movimiento al Animator
    void Animar(Vector3 direccion, bool corriendo, bool salto)
    {
        if (animador == null) return;

        //0 = quieto, 0.5 = caminando, 1 = corriendo (son los cortes del blend tree)
        float destino = direccion.magnitude * (corriendo ? 1f : 0.5f);
        velocidadAnimada = Mathf.Lerp(velocidadAnimada, destino, suavizadoAnimacion * Time.deltaTime);

        animador.SetFloat("Velocidad", velocidadAnimada);
        animador.SetBool("EnSuelo", controller.isGrounded);
        if (animadorTieneAgachado) animador.SetBool("Agachado", Agachado);

        if (salto) animador.SetTrigger("Saltar");
    }

    static bool TieneParametro(Animator animador, string nombre)
    {
        if (animador == null || animador.runtimeAnimatorController == null) return false;

        foreach (AnimatorControllerParameter parametro in animador.parameters)
        {
            if (parametro.name == nombre) return true;
        }
        return false;
    }

    void RevisarCaida()
    {
        if (transform.position.y < alturaLimite)
        {
            Reiniciar();
        }
    }

    //devuelve al jugador al punto donde empezo la partida
    public void Reiniciar()
    {
        controller.enabled = false;
        transform.position = puntoInicio;
        velocidadVertical = Vector3.zero;
        controller.enabled = true;
    }
}
