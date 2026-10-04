using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Un mismo NPC puede ser zombie o aliado. De zombie pasea, ve, oye y ataca;
//curado con el suero se convierte en sobreviviente, sigue al jugador y pelea
//contra los zombies. Si a un aliado lo matan, lo muerden y vuelve a ser zombie.
[RequireComponent(typeof(CharacterController))]
public class Npc : MonoBehaviour
{
    public enum Estado { Quieto, Paseando, Investigando, Persiguiendo, Atacando, Siguiendo, Muerto }

    //todos los NPC de la escena; los usa el ruido y los que buscan a quien atacar
    public static readonly List<Npc> Todos = new List<Npc>();

    [Header("Identidad")]
    public string nombre = "NPC";

    //true = zombie, false = sobreviviente aliado
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

    [Header("Vista")]
    //distancia a la que ve a un jugador que corre; si camina o va agachado lo ve mas cerca
    public float radioDeteccion = 12f;
    //por la espalda ve peor: el radio se multiplica por esto
    public float factorEspalda = 0.5f;
    //si el objetivo se aleja mas de esto, deja de perseguirlo
    public float radioPerdida = 18f;
    //segundos sin ver al objetivo antes de ir a buscarlo donde lo vio por ultima vez
    public float tiempoParaOlvidar = 4f;

    [Header("Oido")]
    //cuanto se queda mirando alrededor al llegar al punto donde oyo el ruido
    public float tiempoInvestigando = 3f;
    public float velocidadInvestigando = 2f;

    [Header("Ataque")]
    public float distanciaAtaque = 1.8f;
    public int danio = 1;
    public float tiempoEntreAtaques = 1.5f;

    [Header("Muerte")]
    //el zombie no se destruye: se esconde y vuelve con la vida llena
    public float tiempoParaReaparecer = 5f;

    //al reaparecer se coloca en un punto al azar dentro de este radio de su origen
    public float radioReaparicion = 6f;

    [Header("Aliado")]
    //nombre y modelo del sobreviviente que aparece al curarlo
    //los perros no se curan: el suero es para personas
    public bool sePuedeCurar = true;

    public string nombreCurado = "Sobreviviente";
    public Transform visualCurado;
    public int vidaComoAliado = 5;
    public float distanciaSeguir = 3f;
    //busca zombies para pelear dentro de este radio
    public float radioDefensa = 9f;
    //si el jugador se aleja mas que esto, deja la pelea y vuelve con el
    public float distanciaMaximaAlJugador = 14f;
    public float tiempoParaReinfectarse = 2.5f;
    public AudioClip sonidoCurado;

    [Header("Visual")]
    //objeto hijo con el modelo; aqui va el zombie importado
    public Transform visual;

    //si se deja vacio se busca el Animator dentro de los hijos
    public Animator animador;

    public float suavizadoAnimacion = 8f;

    public Estado EstadoActual { get; private set; }
    public bool EstaMuerto { get { return EstadoActual == Estado.Muerto; } }

    private Vida vida;
    private CharacterController controller;
    private Jugador jugador;
    private Vida vidaDelJugador;
    private AudioSource fuenteAudio;

    private Transform visualZombie;
    private Animator animadorZombie;
    private Animator animadorCurado;
    private int vidaComoZombie;

    private Vector3 origen;
    private Vector3 destino;
    private float tiempoDeEspera;
    private float tiempoInvestigacionMaximo;
    private float tiempoSiguienteAtaque;
    private float velocidadVertical;
    private float velocidadAnimada;
    private float ritmoAnimacion;
    private float alturaOjos = 1.5f;

    //a quien persigue o ataca ahora mismo (el jugador, un aliado o un zombie)
    private Transform objetivo;
    private Vida vidaObjetivo;
    private Vector3 ultimaPosicionConocida;
    private float tiempoSinVer;

    //cada aliado se pone en un sitio distinto alrededor del jugador
    private Vector3 desplazamientoAlSeguir;

    void OnEnable()
    {
        Todos.Add(this);
    }

    void OnDisable()
    {
        Todos.Remove(this);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        origen = transform.position;
        destino = origen;
        alturaOjos = controller.height * 0.85f;

        vida = GetComponent<Vida>();
        if (vida != null)
        {
            vida.AlRecibirGolpe += Golpeado;
            vida.AlMorir += Morir;
            vidaComoZombie = vida.vidaMaxima;
        }

        fuenteAudio = GetComponent<AudioSource>();

        if (visual == null)
        {
            Transform hijo = transform.Find("Visual");
            if (hijo != null) visual = hijo;
        }

        if (visualCurado == null)
        {
            visualCurado = transform.Find("VisualCurado");
        }

        if (animador == null)
        {
            animador = visual != null ? visual.GetComponentInChildren<Animator>() : GetComponentInChildren<Animator>();
        }

        visualZombie = visual;
        animadorZombie = animador;
        if (visualCurado != null)
        {
            animadorCurado = visualCurado.GetComponentInChildren<Animator>(true);
            visualCurado.gameObject.SetActive(false);
        }

        //un NPC marcado como aliado desde el Inspector empieza ya curado
        if (!esEnemigo)
        {
            CambiarVisual(true);
            ElegirSitioAlSeguir();
        }

        jugador = FindFirstObjectByType<Jugador>();
        if (jugador != null)
        {
            vidaDelJugador = jugador.GetComponent<Vida>();
        }

        if (esEnemigo) Esperar();
        else Seguir();
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

        //Mover lo vuelve a subir si el NPC se desplaza este frame
        ritmoAnimacion = 0f;

        if (esEnemigo) PensarComoZombie();
        else PensarComoAliado();

        switch (EstadoActual)
        {
            case Estado.Quieto:
                Quieto();
                break;
            case Estado.Paseando:
                Pasear();
                break;
            case Estado.Investigando:
                Investigar();
                break;
            case Estado.Persiguiendo:
                Perseguir();
                break;
            case Estado.Atacando:
                Atacar();
                break;
            case Estado.Siguiendo:
                SeguirAlJugador();
                break;
        }

        AplicarGravedad();
        Animar();
    }

    // ---------- decisiones ----------

    bool EstaPeleando()
    {
        return EstadoActual == Estado.Persiguiendo || EstadoActual == Estado.Atacando;
    }

    //decide si ve a alguien, si lo pierde de vista o si se rinde
    void PensarComoZombie()
    {
        if (!EstaPeleando())
        {
            Transform visto = BuscarPresa();
            if (visto != null) PerseguirA(visto);
            return;
        }

        if (!ObjetivoSigueValido())
        {
            Esperar();
            return;
        }

        float distancia = Vector3.Distance(transform.position, objetivo.position);

        if (HayLineaDeVista(objetivo))
        {
            tiempoSinVer = 0f;
            ultimaPosicionConocida = objetivo.position;
        }
        else
        {
            tiempoSinVer += Time.deltaTime;
        }

        //lo perdio: va a buscarlo donde lo vio por ultima vez
        if (distancia > radioPerdida || tiempoSinVer > tiempoParaOlvidar)
        {
            IrAInvestigar(ultimaPosicionConocida);
        }
    }

    //el jugador o el aliado mas cercano que este zombie alcance a ver
    Transform BuscarPresa()
    {
        Transform mejor = null;
        float mejorDistancia = float.MaxValue;

        if (jugador != null && vidaDelJugador != null && !vidaDelJugador.EstaMuerto)
        {
            float radio = RadioDeVista(jugador.transform, radioDeteccion * jugador.Visibilidad);
            float distancia = Vector3.Distance(transform.position, jugador.transform.position);

            if (distancia <= radio && HayLineaDeVista(jugador.transform))
            {
                mejor = jugador.transform;
                mejorDistancia = distancia;
            }
        }

        foreach (Npc otro in Todos)
        {
            if (otro == this || otro.esEnemigo || otro.EstaMuerto) continue;

            float distancia = Vector3.Distance(transform.position, otro.transform.position);
            if (distancia >= mejorDistancia) continue;
            if (distancia > RadioDeVista(otro.transform, radioDeteccion * 0.7f)) continue;
            if (!HayLineaDeVista(otro.transform)) continue;

            mejor = otro.transform;
            mejorDistancia = distancia;
        }

        return mejor;
    }

    //si el otro esta a su espalda, lo ve peor
    float RadioDeVista(Transform otro, float radio)
    {
        Transform mirada = visual != null ? visual : transform;
        Vector3 haciaElOtro = otro.position - transform.position;
        haciaElOtro.y = 0f;

        if (haciaElOtro.sqrMagnitude > 0.01f && Vector3.Angle(mirada.forward, haciaElOtro) > 110f)
        {
            return radio * factorEspalda;
        }

        return radio;
    }

    //el aliado pelea con el zombie mas cercano, pero sin alejarse demasiado del jugador
    void PensarComoAliado()
    {
        bool jugadorLejos = jugador != null &&
            Vector3.Distance(transform.position, jugador.transform.position) > distanciaMaximaAlJugador;

        if (EstaPeleando())
        {
            if (!ObjetivoSigueValido() || jugadorLejos) Seguir();
            return;
        }

        if (jugadorLejos) return;

        Npc zombie = ZombieMasCercano();
        if (zombie != null) PerseguirA(zombie.transform);
    }

    Npc ZombieMasCercano()
    {
        Npc mejor = null;
        float mejorDistancia = radioDefensa;

        foreach (Npc otro in Todos)
        {
            if (otro == this || !otro.esEnemigo || otro.EstaMuerto) continue;

            float distancia = Vector3.Distance(transform.position, otro.transform.position);
            if (distancia < mejorDistancia)
            {
                mejor = otro;
                mejorDistancia = distancia;
            }
        }

        return mejor;
    }

    //el objetivo puede haber muerto, reaparecido o cambiado de bando
    bool ObjetivoSigueValido()
    {
        if (objetivo == null || vidaObjetivo == null || vidaObjetivo.EstaMuerto) return false;

        Npc otro = objetivo.GetComponent<Npc>();
        if (otro != null && otro.esEnemigo == esEnemigo) return false;

        return true;
    }

    // ---------- estados ----------

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

    //camina hasta donde oyo el ruido y se queda mirando un rato
    void Investigar()
    {
        tiempoInvestigacionMaximo -= Time.deltaTime;

        Vector3 direccion = destino - transform.position;
        direccion.y = 0f;

        //si llego o se atoro contra algo, se da por vencido
        if (direccion.magnitude < 1f || tiempoInvestigacionMaximo <= 0f)
        {
            EstadoActual = Estado.Quieto;
            tiempoDeEspera = tiempoInvestigando;
            return;
        }

        Mover(direccion.normalized, velocidadInvestigando);
    }

    void Perseguir()
    {
        if (objetivo == null)
        {
            Descansar();
            return;
        }

        Vector3 direccion = objetivo.position - transform.position;
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
        if (objetivo == null)
        {
            Descansar();
            return;
        }

        Vector3 direccion = objetivo.position - transform.position;
        direccion.y = 0f;

        //si el objetivo se alejo, volver a perseguirlo
        if (direccion.magnitude > distanciaAtaque * 1.2f)
        {
            EstadoActual = Estado.Persiguiendo;
            return;
        }

        Girar(direccion.normalized);

        if (Time.time >= tiempoSiguienteAtaque)
        {
            tiempoSiguienteAtaque = Time.time + tiempoEntreAtaques;

            //no pegarle a alguien que ya esta muerto esperando reaparecer
            if (vidaObjetivo != null && !vidaObjetivo.EstaMuerto)
            {
                vidaObjetivo.RecibirDanio(danio, transform);
            }
        }
    }

    //el aliado camina detras del jugador y corre si se queda muy atras
    void SeguirAlJugador()
    {
        if (jugador == null) return;

        Vector3 punto = jugador.transform.position + desplazamientoAlSeguir;
        Vector3 direccion = punto - transform.position;
        direccion.y = 0f;

        float distancia = direccion.magnitude;
        if (distancia < 1f) return;

        float velocidadActual = distancia > distanciaSeguir * 2.5f ? velocidadPersecucion : velocidad * 2f;
        Mover(direccion.normalized, velocidadActual);
    }

    // ---------- cambios de estado ----------

    public void PerseguirA(Transform nuevoObjetivo)
    {
        objetivo = nuevoObjetivo;
        vidaObjetivo = nuevoObjetivo != null ? nuevoObjetivo.GetComponent<Vida>() : null;
        ultimaPosicionConocida = nuevoObjetivo != null ? nuevoObjetivo.position : transform.position;
        tiempoSinVer = 0f;
        EstadoActual = Estado.Persiguiendo;
    }

    void IrAInvestigar(Vector3 punto)
    {
        objetivo = null;
        vidaObjetivo = null;
        destino = punto;
        tiempoInvestigacionMaximo = 15f;
        EstadoActual = Estado.Investigando;
    }

    void Esperar()
    {
        objetivo = null;
        vidaObjetivo = null;
        EstadoActual = Estado.Quieto;
        tiempoDeEspera = Random.Range(tiempoEsperaMinimo, tiempoEsperaMaximo);
    }

    void Seguir()
    {
        objetivo = null;
        vidaObjetivo = null;
        EstadoActual = Estado.Siguiendo;
    }

    //vuelve a su comportamiento normal segun el bando
    void Descansar()
    {
        if (esEnemigo) Esperar();
        else Seguir();
    }

    //lo llama Ruido: un zombie tranquilo va a ver que fue eso
    public void Oir(Vector3 posicion, float radio)
    {
        if (!esEnemigo || EstaMuerto || EstaPeleando()) return;

        if (Vector3.Distance(transform.position, posicion) <= radio)
        {
            IrAInvestigar(posicion);
        }
    }

    // ---------- curar y reinfectar ----------

    //lo llama el suero; devuelve false si no habia nada que curar
    public bool Curar()
    {
        //los perros no son personas: el suero no les hace nada
        if (!esEnemigo || EstaMuerto || !sePuedeCurar) return false;

        esEnemigo = false;
        CambiarVisual(true);

        if (vida != null)
        {
            vida.vidaMaxima = vidaComoAliado;
            vida.Restaurar();
        }

        ElegirSitioAlSeguir();

        if (fuenteAudio != null && sonidoCurado != null) fuenteAudio.PlayOneShot(sonidoCurado);
        StartCoroutine(Destello(new Color(0.4f, 1f, 0.5f)));

        if (ControlPartida.instancia != null)
        {
            ControlPartida.instancia.Avisar(nombreCurado + " se unio al grupo");
        }

        Seguir();
        return true;
    }

    void ElegirSitioAlSeguir()
    {
        Vector2 lado = Random.insideUnitCircle.normalized * distanciaSeguir;
        desplazamientoAlSeguir = new Vector3(lado.x, 0f, lado.y);
    }

    //un aliado al que matan vuelve a ser zombie en el mismo sitio
    void Reinfectar()
    {
        esEnemigo = true;
        CambiarVisual(false);

        controller.enabled = true;
        velocidadVertical = 0f;
        velocidadAnimada = 0f;

        if (vida != null)
        {
            vida.vidaMaxima = vidaComoZombie;
            vida.Restaurar();
        }

        if (animador != null)
        {
            animador.ResetTrigger("Morir");
            animador.SetTrigger("Revivir");
        }

        StartCoroutine(Destello(new Color(1f, 0.3f, 0.3f)));
        Esperar();
    }

    //enciende el modelo que toca y apaga el otro, conservando hacia donde mira
    void CambiarVisual(bool curado)
    {
        if (visualCurado == null || visualZombie == null) return;

        Quaternion rotacion = visual != null ? visual.rotation : transform.rotation;

        visualZombie.gameObject.SetActive(!curado);
        visualCurado.gameObject.SetActive(curado);

        visual = curado ? visualCurado : visualZombie;
        animador = curado ? animadorCurado : animadorZombie;
        visual.rotation = rotacion;
        velocidadAnimada = 0f;
    }

    //luz breve para que se note el cambio de bando
    IEnumerator Destello(Color color)
    {
        GameObject objeto = new GameObject("Destello");
        objeto.transform.SetParent(transform, false);
        objeto.transform.localPosition = Vector3.up * alturaOjos;

        Light luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.range = 6f;

        float duracion = 0.6f;
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            luz.intensity = Mathf.Lerp(6f, 0f, t / duracion);
            yield return null;
        }

        Destroy(objeto);
    }

    // ---------- movimiento ----------

    void Mover(Vector3 direccion, float velocidadActual)
    {
        controller.Move(direccion * velocidadActual * Time.deltaTime);
        Girar(direccion);

        //0.5 = caminando, 1 = corriendo (son los cortes del blend tree)
        ritmoAnimacion = velocidadActual >= velocidadPersecucion ? 1f : 0.5f;
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

    //Mira de ojos a ojos. Las paredes y los props tapan la vista; los personajes no,
    //para que un zombie no deje de ver al jugador solo porque otro se cruzo.
    bool HayLineaDeVista(Transform otro)
    {
        Vector3 ojos = transform.position + Vector3.up * alturaOjos;
        Vector3 destinoMirada = otro.position + Vector3.up * alturaOjos;
        Vector3 direccion = destinoMirada - ojos;
        float distancia = direccion.magnitude;
        if (distancia < 0.01f) return true;

        RaycastHit[] tocados = Physics.RaycastAll(ojos, direccion / distancia, distancia, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit tocado in tocados)
        {
            if (tocado.collider is CharacterController) continue;
            if (tocado.transform.IsChildOf(transform) || tocado.transform.IsChildOf(otro)) continue;
            return false;
        }

        return true;
    }

    //pasa el estado al Animator
    void Animar()
    {
        if (animador == null) return;

        velocidadAnimada = Mathf.Lerp(velocidadAnimada, ritmoAnimacion, suavizadoAnimacion * Time.deltaTime);

        animador.SetFloat("Velocidad", velocidadAnimada);
        animador.SetBool("Atacando", EstadoActual == Estado.Atacando);
    }

    //escoge un punto al azar dentro del radio de paseo
    Vector3 BuscarDestino()
    {
        Vector2 punto = Random.insideUnitCircle * radioPaseo;
        return origen + new Vector3(punto.x, 0f, punto.y);
    }

    // ---------- golpes y muerte ----------

    //los dispara el componente Vida cuando le pegan
    void Golpeado()
    {
        if (animador != null) animador.SetTrigger("Golpe");

        if (EstaPeleando()) return;

        //aunque no lo hubiera visto, ahora sabe quien le pego
        Transform atacante = vida.UltimoAtacante;
        if (atacante == null && jugador != null) atacante = jugador.transform;
        if (atacante != null) PerseguirA(atacante);
    }

    void Morir()
    {
        EstadoActual = Estado.Muerto;
        objetivo = null;
        vidaObjetivo = null;

        if (animador != null)
        {
            animador.SetFloat("Velocidad", 0f);
            animador.SetBool("Atacando", false);
            animador.ResetTrigger("Revivir");
            animador.SetTrigger("Morir");
        }

        //se apaga el collider para poder pasar por encima del cuerpo
        controller.enabled = false;

        if (!esEnemigo)
        {
            if (ControlPartida.instancia != null)
            {
                ControlPartida.instancia.Avisar("Mordieron a " + nombreCurado + ". Va a volver a ser zombie");
            }

            Invoke("Reinfectar", tiempoParaReinfectarse);
            return;
        }

        float espera = tiempoParaReaparecer;
        if (ControlPartida.instancia != null)
        {
            ControlPartida.instancia.ContarZombieEliminado();
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

        Gizmos.color = esEnemigo ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, esEnemigo ? radioDeteccion : radioDefensa);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, distanciaAtaque);
    }
}
