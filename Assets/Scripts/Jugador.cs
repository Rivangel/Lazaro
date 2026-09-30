using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Jugador : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidad = 6f;
    public float velocidadCorriendo = 10f;
    public KeyCode teclaCorrer = KeyCode.LeftShift;

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

    //direccion horizontal en la que se movio el jugador este frame (la usan otros scripts)
    public Vector3 DireccionMovimiento { get; private set; }

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

        bool corriendo = Input.GetKey(teclaCorrer);
        float velocidadActual = corriendo ? velocidadCorriendo : velocidad;
        controller.Move(direccion * velocidadActual * Time.deltaTime);

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
        }

        velocidadVertical.y += gravedad * Time.deltaTime;
        controller.Move(velocidadVertical * Time.deltaTime);

        Animar(direccion, corriendo, salto);
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

        if (salto) animador.SetTrigger("Saltar");
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
