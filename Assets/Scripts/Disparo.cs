using System.Collections;
using UnityEngine;

//Disparo instantaneo (raycast) desde la camara: se apunta con la mira del centro
//de la pantalla y el trazo se dibuja saliendo del arma.
public class Disparo : MonoBehaviour
{
    public enum Modo
    {
        Instantaneo,   //el impacto es inmediato, se dibuja solo una linea
        Proyectil,     //sale una bala que viaja y se puede ver volar
    }

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

    [Header("Estado")]
    //lo apaga el Jugador mientras esta muerto
    public bool puedeDisparar = true;

    private Jugador jugador;
    private float tiempoSiguienteDisparo;
    private Light luzFogonazo;

    void Start()
    {
        jugador = GetComponent<Jugador>();

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

        //Fire1 es clic izquierdo o Ctrl izquierdo
        if (Input.GetButton("Fire1") && Time.time >= tiempoSiguienteDisparo)
        {
            tiempoSiguienteDisparo = Time.time + 1f / Mathf.Max(0.1f, disparosPorSegundo);
            Disparar();
        }
    }

    void Disparar()
    {
        if (camara == null) return;

        //el personaje voltea hacia donde apunta la camara al disparar
        Vector3 direccionPlana = camara.forward;
        direccionPlana.y = 0f;
        if (jugador != null && direccionPlana.sqrMagnitude > 0.01f)
        {
            jugador.MirarHacia(direccionPlana.normalized);
        }

        Reproducir();
        StartCoroutine(Fogonazo());

        Vector3 origen = puntoDisparo != null ? puntoDisparo.position : transform.position + Vector3.up;

        //hacia donde mira la mira del centro de la pantalla
        Vector3 destino = camara.position + camara.forward * alcance;
        RaycastHit impacto;
        bool hayImpacto = BuscarImpacto(out impacto);
        if (hayImpacto) destino = impacto.point;

        if (modo == Modo.Proyectil && prefabProyectil != null)
        {
            LanzarProyectil(origen, destino);
        }
        else
        {
            //instantaneo: el daño se aplica ya y solo se dibuja la linea
            if (hayImpacto)
            {
                Vida vida = impacto.collider.GetComponentInParent<Vida>();
                if (vida != null) vida.RecibirDanio(danio);
            }

            StartCoroutine(DibujarTrazo(origen, destino));
        }
    }

    //la bala sale del arma pero apunta al punto de la mira, para que coincidan
    void LanzarProyectil(Vector3 origen, Vector3 destino)
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
            proyectil.Lanzar(direccion, danio, transform.root, velocidadProyectil);
        }
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

    void Reproducir()
    {
        if (sonidoDisparo == null || fuenteAudio == null) return;
        fuenteAudio.PlayOneShot(sonidoDisparo, volumen);
    }

    //linea que va del arma al punto de impacto y se desvanece
    IEnumerator DibujarTrazo(Vector3 origen, Vector3 destino)
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

        linea.startColor = colorTrazo;
        linea.endColor = colorTrazo;

        float tiempo = 0f;
        while (tiempo < duracionTrazo)
        {
            tiempo += Time.deltaTime;

            //se va poniendo transparente hasta desaparecer
            float alfa = 1f - (tiempo / duracionTrazo);
            Color color = colorTrazo;
            color.a = alfa;
            linea.startColor = color;
            linea.endColor = color;

            yield return null;
        }

        Destroy(objeto);
    }
}
