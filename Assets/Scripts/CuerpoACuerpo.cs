using System.Collections;
using UnityEngine;

//Ataque cuerpo a cuerpo con el cuchillo. Es la alternativa silenciosa al disparo:
//hace poco ruido y tumba de un golpe a un zombie que no te ha visto, pero obliga
//a pegarse, que es justo lo que el sigilo intenta evitar.
public class CuerpoACuerpo : MonoBehaviour
{
    [Header("Golpe")]
    public KeyCode tecla = KeyCode.F;
    public int danio = 2;
    public float alcance = 2.2f;

    //angulo del cono al frente dentro del que entra el golpe
    public float anguloDeGolpe = 110f;
    public float golpesPorSegundo = 1.2f;

    [Header("Sigilo")]
    //un disparo suena a 25; esto casi no se oye
    public float ruido = 4f;

    //a un zombie que todavia no te ha visto se le tumba de un solo golpe
    public bool ejecucionPorSorpresa = true;

    [Header("Armas")]
    //el cuchillo aparece solo mientras dura el golpe y luego se guarda
    public Transform armaCuerpoACuerpo;
    public Transform armaDeFuego;
    public float duracionDelGolpe = 0.6f;

    [Header("Sonido")]
    public AudioClip sonidoGolpe;
    public AudioSource fuenteAudio;

    [Header("Estado")]
    public bool puedeGolpear = true;

    private Jugador jugador;
    private Animator animador;
    private Transform camara;
    private float tiempoSiguienteGolpe;
    private bool golpeando;

    void Start()
    {
        jugador = GetComponent<Jugador>();
        animador = GetComponentInChildren<Animator>();

        if (fuenteAudio == null) fuenteAudio = GetComponent<AudioSource>();
        if (Camera.main != null) camara = Camera.main.transform;

        //el cuchillo empieza guardado
        if (armaCuerpoACuerpo != null) armaCuerpoACuerpo.gameObject.SetActive(false);
    }

    void Update()
    {
        //no encadenar golpes encima de otro que sigue en curso
        if (!puedeGolpear || golpeando) return;

        if (Input.GetKeyDown(tecla) && Time.time >= tiempoSiguienteGolpe)
        {
            tiempoSiguienteGolpe = Time.time + 1f / Mathf.Max(0.1f, golpesPorSegundo);
            StartCoroutine(Golpear());
        }
    }

    IEnumerator Golpear()
    {
        golpeando = true;

        //mirar hacia donde apunta la camara, igual que al disparar
        if (jugador != null && camara != null)
        {
            Vector3 direccion = camara.forward;
            direccion.y = 0f;
            if (direccion.sqrMagnitude > 0.01f) jugador.MirarHacia(direccion.normalized);
        }

        SacarCuchillo(true);

        if (animador != null) animador.SetTrigger("Golpear");
        if (sonidoGolpe != null && fuenteAudio != null) fuenteAudio.PlayOneShot(sonidoGolpe);

        Ruido.Emitir(transform.position, ruido, true);

        //el daño se aplica a media animacion, no al empezar
        yield return new WaitForSeconds(duracionDelGolpe * 0.35f);
        AplicarGolpe();

        yield return new WaitForSeconds(duracionDelGolpe * 0.65f);
        SacarCuchillo(false);

        golpeando = false;
    }

    void AplicarGolpe()
    {
        Vector3 frente = Frente();

        foreach (Collider tocado in Physics.OverlapSphere(transform.position, alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (tocado.transform.root == transform.root) continue;

            Vida vida = tocado.GetComponentInParent<Vida>();
            if (vida == null || vida.EstaMuerto) continue;

            //solo entra lo que este dentro del cono de adelante
            Vector3 haciaElObjetivo = vida.transform.position - transform.position;
            haciaElObjetivo.y = 0f;
            if (Vector3.Angle(frente, haciaElObjetivo) > anguloDeGolpe * 0.5f) continue;

            vida.RecibirDanio(DanioContra(vida), transform);
        }
    }

    //tumba de un golpe al zombie que no se ha dado cuenta de que estas ahi
    int DanioContra(Vida vida)
    {
        if (!ejecucionPorSorpresa) return danio;

        Npc npc = vida.GetComponent<Npc>();
        if (npc == null || !npc.esEnemigo) return danio;

        bool teTieneFichado = npc.EstadoActual == Npc.Estado.Persiguiendo
                           || npc.EstadoActual == Npc.Estado.Atacando;

        return teTieneFichado ? danio : vida.vidaMaxima;
    }

    Vector3 Frente()
    {
        Transform referencia = (jugador != null && jugador.visual != null) ? jugador.visual : transform;
        Vector3 frente = referencia.forward;
        frente.y = 0f;
        return frente.sqrMagnitude > 0.01f ? frente.normalized : Vector3.forward;
    }

    void SacarCuchillo(bool sacar)
    {
        if (armaCuerpoACuerpo != null) armaCuerpoACuerpo.gameObject.SetActive(sacar);
        if (armaDeFuego != null) armaDeFuego.gameObject.SetActive(!sacar);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alcance);
    }
}
