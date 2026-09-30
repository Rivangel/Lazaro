using System.Collections;
using UnityEngine;

//Disparo instantaneo (raycast) desde la camara: se apunta con la mira del centro
//de la pantalla y el trazo se dibuja saliendo del arma.
//El arma lleva dos municiones: balas, que hieren y hacen mucho ruido, y suero,
//que cura zombies, casi no suena y es escaso. Tambien lanza objetos para distraer.
public class Disparo : MonoBehaviour
{
    public enum Modo
    {
        Instantaneo,   //el impacto es inmediato, se dibuja solo una linea
        Proyectil,     //sale una bala que viaja y se puede ver volar
    }

    public enum Municion { Balas, Suero }

    [Header("Arma")]
    public Modo modo = Modo.Proyectil;
    public int danio = 1;
    public float alcance = 60f;

    //cadencia: cuantos disparos por segundo como maximo
    public float disparosPorSegundo = 4f;

    //punta del arma, de donde sale el trazo (el generador le pone la Pistol)
    public Transform puntoDisparo;

    [Header("Apuntado")]
    //si se deja vacio se usa la camara principal
    public Transform camara;

    //que capas puede tocar la bala
    public LayerMask capasQueRecibenDisparos = ~0;

    [Header("Sonido")]
    public AudioClip sonidoDisparo;
    public AudioSource fuenteAudio;
    [Range(0f, 1f)] public float volumen = 0.6f;

    [Header("Proyectil")]
    //lo crea el generador en Assets/Prefabs; si falta, se dispara en modo instantaneo
    public GameObject prefabProyectil;
    public float velocidadProyectil = 45f;

    [Header("Fogonazo")]
    public bool usarFogonazo = true;
    public Color colorFogonazo = new Color(1f, 0.85f, 0.4f);
    public float duracionFogonazo = 0.04f;

    [Header("Trazo")]
    public Material materialTrazo;
    public Color colorTrazo = new Color(1f, 0.85f, 0.3f);
    public float grosorTrazo = 0.04f;
    public float duracionTrazo = 0.05f;

    [Header("Suero")]
    public int dosisSuero = 2;
    public int dosisMaximas = 6;
    //el suero se dispara mas lento que las balas
    public float suerosPorSegundo = 1.5f;
    public Color colorSuero = new Color(0.35f, 1f, 0.45f);
    public AudioClip sonidoSuero;
    public AudioClip sonidoSinSuero;

    [Header("Ruido")]
    //radio en el que los zombies oyen cada disparo
    public float ruidoBalas = 25f;
    public float ruidoSuero = 2f;

    [Header("Distraccion")]
    //objeto que se lanza y hace ruido donde cae, para alejar a los zombies
    public KeyCode teclaLanzar = KeyCode.G;
    public float fuerzaLanzamiento = 14f;
    public float esperaEntreLanzamientos = 3f;
    public float ruidoDistraccion = 16f;

    [Header("Controles")]
    public KeyCode teclaBalas = KeyCode.Alpha1;
    public KeyCode teclaSuero = KeyCode.Alpha2;
    public KeyCode teclaAlternar = KeyCode.Q;

    [Header("Estado")]
    //lo apaga el Jugador mientras esta muerto
    public bool puedeDisparar = true;

    public Municion MunicionActual { get; private set; }
    public int DosisSuero { get { return dosisSuero; } }

    //0 = recien lanzado, 1 = listo para lanzar otra vez
    public float CargaDistraccion
    {
        get { return Mathf.Clamp01(1f - (tiempoSiguienteLanzamiento - Time.time) / esperaEntreLanzamientos); }
    }

    private Jugador jugador;
    private CharacterController controller;
    private float tiempoSiguienteDisparo;
    private float tiempoSiguienteLanzamiento;
    private Light luzFogonazo;

    void Start()
    {
        jugador = GetComponent<Jugador>();
        controller = GetComponent<CharacterController>();

        if (camara == null && Camera.main != null)
        {
            camara = Camera.main.transform;
        }

        if (fuenteAudio == null)
        {
            fuenteAudio = GetComponent<AudioSource>();
        }

        CrearFogonazo();
    }

    //luz apagada colgada de la punta del arma; se enciende un instante en cada disparo
    void CrearFogonazo()
    {
        if (!usarFogonazo || puntoDisparo == null) return;

        GameObject objeto = new GameObject("Fogonazo");
        objeto.transform.SetParent(puntoDisparo, false);

        luzFogonazo = objeto.AddComponent<Light>();
        luzFogonazo.type = LightType.Point;
        luzFogonazo.color = colorFogonazo;
        luzFogonazo.range = 5f;
        luzFogonazo.intensity = 5f;
        luzFogonazo.enabled = false;
    }

    void Update()
    {
        if (!puedeDisparar) return;
        if (ControlPartida.instancia != null && ControlPartida.instancia.PartidaTerminada) return;

        CambiarMunicion();

        //Fire1 es clic izquierdo o Ctrl izquierdo
        if (Input.GetButton("Fire1") && Time.time >= tiempoSiguienteDisparo)
        {
            float cadencia = MunicionActual == Municion.Suero ? suerosPorSegundo : disparosPorSegundo;
            tiempoSiguienteDisparo = Time.time + 1f / Mathf.Max(0.1f, cadencia);
            Disparar();
        }

        if (Input.GetKeyDown(teclaLanzar) && Time.time >= tiempoSiguienteLanzamiento)
        {
            tiempoSiguienteLanzamiento = Time.time + esperaEntreLanzamientos;
            LanzarDistraccion();
        }
    }

    void CambiarMunicion()
    {
        if (Input.GetKeyDown(teclaBalas)) MunicionActual = Municion.Balas;
        else if (Input.GetKeyDown(teclaSuero)) MunicionActual = Municion.Suero;
        else if (Input.GetKeyDown(teclaAlternar))
        {
            MunicionActual = MunicionActual == Municion.Balas ? Municion.Suero : Municion.Balas;
        }
    }

    //la usan los botiquines del mapa; devuelve false si ya no cabe mas
    public bool AgregarSuero(int cantidad)
    {
        if (dosisSuero >= dosisMaximas) return false;

        dosisSuero = Mathf.Min(dosisMaximas, dosisSuero + cantidad);
        return true;
    }

    void Disparar()
    {
        if (camara == null) return;

        bool esSuero = MunicionActual == Municion.Suero;
        if (esSuero)
        {
            if (dosisSuero <= 0)
            {
                Reproducir(sonidoSinSuero);
                if (ControlPartida.instancia != null) ControlPartida.instancia.Avisar("No te queda suero");
                return;
            }

            dosisSuero--;
        }

        //el personaje voltea hacia donde apunta la camara al disparar
        Vector3 direccionPlana = camara.forward;
        direccionPlana.y = 0f;
        if (jugador != null && direccionPlana.sqrMagnitude > 0.01f)
        {
            jugador.MirarHacia(direccionPlana.normalized);
        }

        Reproducir(esSuero ? sonidoSuero : sonidoDisparo);
        if (!esSuero) StartCoroutine(Fogonazo());

        Vector3 origen = puntoDisparo != null ? puntoDisparo.position : transform.position + Vector3.up;
        Ruido.Emitir(origen, esSuero ? ruidoSuero : ruidoBalas, true);

        //hacia donde mira la mira del centro de la pantalla
        Vector3 destino = camara.position + camara.forward * alcance;
        RaycastHit impacto;
        bool hayImpacto = BuscarImpacto(out impacto);
        if (hayImpacto) destino = impacto.point;

        if (modo == Modo.Proyectil && prefabProyectil != null)
        {
            LanzarProyectil(origen, destino, esSuero);
        }
        else
        {
            //instantaneo: el daño se aplica ya y solo se dibuja la linea
            if (hayImpacto)
            {
                AplicarImpacto(impacto.collider, danio, esSuero, transform);
            }

            StartCoroutine(DibujarTrazo(origen, destino, esSuero ? colorSuero : colorTrazo));
        }
    }

    //Lo que pasa al tocar a alguien. Lo comparten el disparo instantaneo y el proyectil.
    //El suero cura zombies; las balas hieren, pero no a los aliados.
    public static void AplicarImpacto(Collider tocado, int danio, bool esSuero, Transform atacante)
    {
        Npc npc = tocado.GetComponentInParent<Npc>();

        if (esSuero)
        {
            if (npc != null) npc.Curar();
            return;
        }

        if (npc != null && !npc.esEnemigo) return;

        Vida vida = tocado.GetComponentInParent<Vida>();
        if (vida != null) vida.RecibirDanio(danio, atacante);
    }

    //la bala sale del arma pero apunta al punto de la mira, para que coincidan
    void LanzarProyectil(Vector3 origen, Vector3 destino, bool esSuero)
    {
        Vector3 direccion = destino - origen;
        if (direccion.sqrMagnitude < 0.01f) direccion = camara.forward;
        direccion.Normalize();

        //nace un poco adelante para no salir dentro del propio cuerpo
        GameObject objeto = Instantiate(prefabProyectil, origen + direccion * 0.3f, Quaternion.LookRotation(direccion));

        Proyectil proyectil = objeto.GetComponent<Proyectil>();
        if (proyectil != null)
        {
            proyectil.capasQueGolpea = capasQueRecibenDisparos;
            proyectil.Lanzar(direccion, danio, transform.root, velocidadProyectil, esSuero);
        }

        if (esSuero) Pintar(objeto, colorSuero);
    }

    //el dardo de suero usa el mismo prefab que la bala, pero pintado de verde
    static void Pintar(GameObject objeto, Color color)
    {
        Renderer render = objeto.GetComponent<Renderer>();
        if (render != null)
        {
            render.material.color = color;
            render.material.SetColor("_EmissionColor", color * 2f);
        }

        TrailRenderer estela = objeto.GetComponent<TrailRenderer>();
        if (estela != null)
        {
            estela.startColor = new Color(color.r, color.g, color.b, 0.9f);
            estela.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }

    //Lanza una piedra hacia donde mira la camara. Donde cae hace ruido y los zombies
    //cercanos van a revisar, lo que deja pasar al jugador por otro lado.
    void LanzarDistraccion()
    {
        if (camara == null) return;

        Vector3 direccion = (camara.forward + Vector3.up * 0.25f).normalized;
        Vector3 origen = transform.position + Vector3.up * 1.6f + direccion * 0.8f;

        GameObject piedra = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        piedra.name = "Distraccion";
        piedra.transform.position = origen;
        piedra.transform.localScale = Vector3.one * 0.2f;

        Renderer render = piedra.GetComponent<Renderer>();
        if (render != null) render.material.color = new Color(0.45f, 0.42f, 0.4f);

        Collider colision = piedra.GetComponent<Collider>();
        if (controller != null) Physics.IgnoreCollision(colision, controller);

        Rigidbody cuerpo = piedra.AddComponent<Rigidbody>();
        cuerpo.mass = 0.3f;
        cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        cuerpo.velocity = direccion * fuerzaLanzamiento;

        Distraccion distraccion = piedra.AddComponent<Distraccion>();
        distraccion.radioRuido = ruidoDistraccion;
    }

    IEnumerator Fogonazo()
    {
        if (luzFogonazo == null) yield break;

        luzFogonazo.enabled = true;
        yield return new WaitForSeconds(duracionFogonazo);
        luzFogonazo.enabled = false;
    }

    //lanza el rayo desde la camara e ignora al propio jugador
    bool BuscarImpacto(out RaycastHit impacto)
    {
        impacto = new RaycastHit();

        Ray rayo = new Ray(camara.position, camara.forward);
        RaycastHit[] tocados = Physics.RaycastAll(rayo, alcance, capasQueRecibenDisparos, QueryTriggerInteraction.Ignore);
        if (tocados.Length == 0) return false;

        System.Array.Sort(tocados, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit tocado in tocados)
        {
            //saltarse al propio personaje, que esta entre la camara y el objetivo
            if (tocado.transform.root == transform.root) continue;

            impacto = tocado;
            return true;
        }

        return false;
    }

    void Reproducir(AudioClip clip)
    {
        if (clip == null || fuenteAudio == null) return;
        fuenteAudio.PlayOneShot(clip, volumen);
    }

    //linea que va del arma al punto de impacto y se desvanece
    IEnumerator DibujarTrazo(Vector3 origen, Vector3 destino, Color colorLinea)
    {
        GameObject objeto = new GameObject("Trazo");
        LineRenderer linea = objeto.AddComponent<LineRenderer>();

        linea.useWorldSpace = true;
        linea.positionCount = 2;
        linea.SetPosition(0, origen);
        linea.SetPosition(1, destino);
        linea.startWidth = grosorTrazo;
        linea.endWidth = grosorTrazo * 0.4f;
        linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linea.receiveShadows = false;

        if (materialTrazo != null)
        {
            linea.material = materialTrazo;
        }

        linea.startColor = colorLinea;
        linea.endColor = colorLinea;

        float tiempo = 0f;
        while (tiempo < duracionTrazo)
        {
            tiempo += Time.deltaTime;

            //se va poniendo transparente hasta desaparecer
            float alfa = 1f - (tiempo / duracionTrazo);
            Color color = colorLinea;
            color.a = alfa;
            linea.startColor = color;
            linea.endColor = color;

            yield return null;
        }

        Destroy(objeto);
    }
}
