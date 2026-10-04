using UnityEngine;

//Bala que viaja por el aire. El choque no se calcula con colliders sino lanzando
//un rayo por el tramo que recorre cada frame: a esta velocidad un collider normal
//se saltaria las paredes delgadas de un frame al siguiente.
public class Proyectil : MonoBehaviour
{
    [Header("Vuelo")]
    public float velocidad = 30f;

    //se destruye sola si no le pega a nada, para no dejar balas volando para siempre
    public float tiempoDeVida = 3f;

    //grosor de la bala al comprobar el choque; con un rayo puro se falla por milimetros
    public float radio = 0.06f;

    [Header("Impacto")]
    public LayerMask capasQueGolpea = ~0;
    public Color colorImpacto = new Color(1f, 0.8f, 0.3f);
    public float duracionDestello = 0.05f;

    private int danio = 1;
    //true = dardo de suero: cura zombies en vez de herirlos
    private bool esSuero;
    private Transform duenio;
    private float tiempoDeMuerte;

    //lo llama el arma justo despues de instanciar la bala
    public void Lanzar(Vector3 direccion, int danioDelDisparo, Transform quienDispara, float velocidadDeseada, bool dardoDeSuero = false)
    {
        danio = danioDelDisparo;
        esSuero = dardoDeSuero;
        duenio = quienDispara;
        velocidad = velocidadDeseada;

        transform.rotation = Quaternion.LookRotation(direccion);
        tiempoDeMuerte = Time.time + tiempoDeVida;
    }

    void Start()
    {
        //por si alguien la instancia sin llamar a Lanzar
        if (tiempoDeMuerte <= 0f) tiempoDeMuerte = Time.time + tiempoDeVida;
    }

    void Update()
    {
        float paso = velocidad * Time.deltaTime;

        RaycastHit impacto;
        if (BuscarImpacto(paso, out impacto))
        {
            Impactar(impacto);
            return;
        }

        transform.position += transform.forward * paso;

        if (Time.time >= tiempoDeMuerte)
        {
            Destroy(gameObject);
        }
    }

    //revisa el tramo que va a recorrer este frame, ignorando a quien disparo
    bool BuscarImpacto(float distancia, out RaycastHit impacto)
    {
        impacto = new RaycastHit();

        RaycastHit[] tocados = Physics.SphereCastAll(transform.position, radio, transform.forward,
                                                     distancia, capasQueGolpea, QueryTriggerInteraction.Ignore);
        if (tocados.Length == 0) return false;

        System.Array.Sort(tocados, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit tocado in tocados)
        {
            if (duenio != null && tocado.transform.root == duenio) continue;

            impacto = tocado;
            return true;
        }

        return false;
    }

    void Impactar(RaycastHit impacto)
    {
        Disparo.AplicarImpacto(impacto.collider, danio, esSuero, duenio);

        CrearDestello(impacto.point);
        Destroy(gameObject);
    }

    //chispazo de luz en el punto de impacto, sin necesidad de particulas ni assets
    void CrearDestello(Vector3 posicion)
    {
        GameObject objeto = new GameObject("Impacto");
        objeto.transform.position = posicion;

        Light luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = colorImpacto;
        luz.range = 3f;
        luz.intensity = 4f;

        Destroy(objeto, duracionDestello);
    }
}
