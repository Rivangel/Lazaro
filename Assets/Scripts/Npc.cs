using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class Npc : MonoBehaviour
{
    public enum Estado { Quieto, Paseando, Persiguiendo, Atacando, Muerto }

    [Header("Identidad")]
    public string nombre = "NPC";

    //cuando este en true persigue y ataca al jugador; en false solo pasea
    public bool esEnemigo = true;

    [Header("Movimiento")]
    public float velocidad = 1.2f;
    public float velocidadPersecucion = 3.5f;
    public float velocidadGiro = 8f;
    public float gravedad = -20f;

    [Header("Paseo")]
    //radio alrededor de su punto de origen dentro del cual escoge destinos
    public float radioPaseo = 8f;
    public float tiempoEsperaMinimo = 1f;
    public float tiempoEsperaMaximo = 4f;

    [Header("Deteccion")]
    public float radioDeteccion = 12f;
    //si el jugador se aleja mas de esto, deja de perseguirlo
    public float radioPerdida = 18f;
    public float distanciaAtaque = 1.8f;

    [Header("Ataque")]
    public int danio = 1;
    public float tiempoEntreAtaques = 1.5f;

    [Header("Muerte")]
    //el zombie no se destruye: se esconde y vuelve con la vida llena
    public float tiempoParaReaparecer = 5f;

    //al reaparecer se coloca en un punto al azar dentro de este radio de su origen
    public float radioReaparicion = 6f;

    [Header("Visual")]
    //objeto hijo con el modelo; aqui va el zombie importado
    public Transform visual;

    //si se deja vacio se busca el Animator dentro de los hijos
    public Animator animador;

    public float suavizadoAnimacion = 8f;

    public Estado EstadoActual { get; private set; }

    private Vida vida;
    private Vida vidaDelJugador;
    private CharacterController controller;
    private Transform jugador;
    private Vector3 origen;
    private Vector3 destino;
    private float tiempoDeEspera;
    private float tiempoSiguienteAtaque;
    private float velocidadVertical;
    private float velocidadAnimada;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        origen = transform.position;
        destino = origen;

        vida = GetComponent<Vida>();
        if (vida != null)
        {
            vida.AlRecibirGolpe += Golpeado;
            vida.AlMorir += Morir;
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

        Jugador scriptJugador = FindFirstObjectByType<Jugador>();
        if (scriptJugador != null)
        {
            jugador = scriptJugador.transform;
            vidaDelJugador = scriptJugador.GetComponent<Vida>();
        }

        Esperar();
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
        if (EstadoActual == Estado.Muerto) return;

        RevisarJugador();

        switch (EstadoActual)
        {
            case Estado.Quieto:
                Quieto();
                break;
            case Estado.Paseando:
                Pasear();
                break;
            case Estado.Persiguiendo:
                Perseguir();
                break;
            case Estado.Atacando:
                Atacar();
                break;
        }

        AplicarGravedad();
        Animar();
    }

    //decide si ve al jugador o lo pierde de vista
    void RevisarJugador()
    {
        bool persiguiendo = EstadoActual == Estado.Persiguiendo || EstadoActual == Estado.Atacando;

        if (!esEnemigo || jugador == null)
        {
            if (persiguiendo) Esperar();
            return;
        }

        float distancia = Vector3.Distance(transform.position, jugador.position);

        if (persiguiendo)
        {
            if (distancia > radioPerdida) Esperar();
        }
        else if (distancia <= radioDeteccion)
        {
            EstadoActual = Estado.Persiguiendo;
        }
    }

    void Quieto()
    {
        tiempoDeEspera -= Time.deltaTime;
        if (tiempoDeEspera <= 0f)
        {
            destino = BuscarDestino();
            EstadoActual = Estado.Paseando;
        }
    }

    void Pasear()
    {
        Vector3 direccion = destino - transform.position;
        direccion.y = 0f;

        //ya llego al destino, se queda quieto un rato
        if (direccion.magnitude < 0.5f)
        {
            Esperar();
            return;
        }

        Mover(direccion.normalized, velocidad);
    }

    void Perseguir()
    {
        if (jugador == null)
        {
            Esperar();
            return;
        }

        Vector3 direccion = jugador.position - transform.position;
        direccion.y = 0f;

        if (direccion.magnitude <= distanciaAtaque)
        {
            EstadoActual = Estado.Atacando;
            return;
        }

        Mover(direccion.normalized, velocidadPersecucion);
    }

    void Atacar()
    {
        if (jugador == null)
        {
            Esperar();
            return;
        }

        Vector3 direccion = jugador.position - transform.position;
        direccion.y = 0f;

        //si el jugador se alejo, volver a perseguirlo
        if (direccion.magnitude > distanciaAtaque * 1.2f)
        {
            EstadoActual = Estado.Persiguiendo;
            return;
        }

        Girar(direccion.normalized);

        if (Time.time >= tiempoSiguienteAtaque)
        {
            tiempoSiguienteAtaque = Time.time + tiempoEntreAtaques;

            //no pegarle a un jugador que ya esta muerto esperando reaparecer
            if (vidaDelJugador != null && !vidaDelJugador.EstaMuerto)
            {
                vidaDelJugador.RecibirDanio(danio);
            }
        }
    }

    void Mover(Vector3 direccion, float velocidadActual)
    {
        controller.Move(direccion * velocidadActual * Time.deltaTime);
        Girar(direccion);
    }

    void Girar(Vector3 direccion)
    {
        if (direccion.sqrMagnitude < 0.01f) return;

        Transform aGirar = visual != null ? visual : transform;
        Quaternion rotacionDeseada = Quaternion.LookRotation(direccion);
        aGirar.rotation = Quaternion.Slerp(aGirar.rotation, rotacionDeseada, velocidadGiro * Time.deltaTime);
    }

    void AplicarGravedad()
    {
        if (controller.isGrounded && velocidadVertical < 0f)
        {
            velocidadVertical = -2f;
        }

        velocidadVertical += gravedad * Time.deltaTime;
        controller.Move(Vector3.up * velocidadVertical * Time.deltaTime);
    }

    //pasa el estado al Animator
    void Animar()
    {
        if (animador == null) return;

        //0 = quieto, 0.5 = caminando, 1 = corriendo (son los cortes del blend tree)
        float destinoVelocidad = 0f;
        if (EstadoActual == Estado.Paseando) destinoVelocidad = 0.5f;
        else if (EstadoActual == Estado.Persiguiendo) destinoVelocidad = 1f;

        velocidadAnimada = Mathf.Lerp(velocidadAnimada, destinoVelocidad, suavizadoAnimacion * Time.deltaTime);

        animador.SetFloat("Velocidad", velocidadAnimada);
        animador.SetBool("Atacando", EstadoActual == Estado.Atacando);
    }

    void Esperar()
    {
        EstadoActual = Estado.Quieto;
        tiempoDeEspera = Random.Range(tiempoEsperaMinimo, tiempoEsperaMaximo);
    }

    //escoge un punto al azar dentro del radio de paseo
    Vector3 BuscarDestino()
    {
        Vector2 punto = Random.insideUnitCircle * radioPaseo;
        return origen + new Vector3(punto.x, 0f, punto.y);
    }

    //los dispara el componente Vida cuando le pegan
    void Golpeado()
    {
        if (animador != null) animador.SetTrigger("Golpe");

        //aunque no lo hubiera visto, ahora sabe donde esta el jugador
        if (esEnemigo && EstadoActual != Estado.Atacando)
        {
            EstadoActual = Estado.Persiguiendo;
        }
    }

    void Morir()
    {
        EstadoActual = Estado.Muerto;

        if (animador != null)
        {
            animador.SetFloat("Velocidad", 0f);
            animador.SetBool("Atacando", false);
            animador.ResetTrigger("Revivir");
            animador.SetTrigger("Morir");
        }

        //se apaga el collider para poder pasar por encima del cuerpo
        controller.enabled = false;

        if (ControlPartida.instancia != null)
        {
            ControlPartida.instancia.ContarZombieEliminado();
        }

        float espera = tiempoParaReaparecer;
        if (ControlPartida.instancia != null)
        {
            espera = ControlPartida.instancia.tiempoReaparicionEnemigo;
        }

        Invoke("Revivir", espera);
    }

    //vuelve a la vida en un punto cercano a donde aparecio al empezar
    public void Revivir()
    {
        Vector2 punto = Random.insideUnitCircle * radioReaparicion;
        transform.position = origen + new Vector3(punto.x, 0f, punto.y);

        controller.enabled = true;
        velocidadVertical = 0f;
        velocidadAnimada = 0f;

        if (vida != null) vida.Restaurar();

        if (animador != null)
        {
            animador.ResetTrigger("Morir");
            animador.SetTrigger("Revivir");
        }

        Esperar();
    }

    //dibuja los radios en la vista de escena para poder ajustarlos a ojo
    void OnDrawGizmosSelected()
    {
        Vector3 centro = Application.isPlaying ? origen : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(centro, radioPaseo);

        Gizmos.color = esEnemigo ? Color.red : Color.gray;
        Gizmos.DrawWireSphere(transform.position, radioDeteccion);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaque);
    }
}
