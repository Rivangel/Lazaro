using UnityEngine;

//Linterna del jugador. De noche es la unica forma de ver bien, pero encenderla
//te vuelve mucho mas visible para los zombies: es el mismo trato que el resto
//del juego, ver mejor a cambio de que te vean.
public class Linterna : MonoBehaviour
{
    [Header("Control")]
    public KeyCode tecla = KeyCode.L;
    public bool encendidaAlEmpezar = true;

    [Header("Haz")]
    public float alcance = 25f;
    public float angulo = 55f;
    public float intensidad = 3f;
    public Color color = new Color(1f, 0.96f, 0.85f);

    //altura a la que sale el haz, en proporcion a la altura del jugador
    public float alturaRelativa = 0.8f;

    [Header("Sigilo")]
    //cuanto multiplica la visibilidad del jugador mientras esta encendida
    public float penalizacionVisibilidad = 1.6f;

    [Header("Sonido")]
    public AudioClip sonidoInterruptor;
    public AudioSource fuenteAudio;

    public bool Encendida { get; private set; }

    //lo lee el Jugador para sumarlo a su visibilidad
    public float MultiplicadorVisibilidad
    {
        get { return Encendida ? penalizacionVisibilidad : 1f; }
    }

    private Light luz;
    private Transform camara;

    void Start()
    {
        if (fuenteAudio == null) fuenteAudio = GetComponent<AudioSource>();
        if (Camera.main != null) camara = Camera.main.transform;

        CrearHaz();
        Encender(encendidaAlEmpezar);
    }

    void CrearHaz()
    {
        GameObject objeto = new GameObject("Haz");
        objeto.transform.SetParent(transform, false);

        float altura = 1.6f;
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) altura = controller.height * alturaRelativa;

        objeto.transform.localPosition = new Vector3(0f, altura, 0.2f);

        luz = objeto.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.spotAngle = angulo;
        luz.range = alcance;
        luz.intensity = intensidad;
        luz.color = color;
        luz.shadows = LightShadows.Soft;
    }

    void Update()
    {
        if (Input.GetKeyDown(tecla)) Encender(!Encendida);

        //el haz apunta a donde mira la camara, no a donde camina el personaje
        if (luz != null && camara != null)
        {
            luz.transform.rotation = camara.rotation;
        }
    }

    public void Encender(bool encender)
    {
        Encendida = encender;
        if (luz != null) luz.enabled = encender;

        if (sonidoInterruptor != null && fuenteAudio != null)
        {
            fuenteAudio.PlayOneShot(sonidoInterruptor, 0.4f);
        }
    }
}
