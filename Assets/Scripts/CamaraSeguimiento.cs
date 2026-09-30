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
    public float suavizado = 12f;

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

    private float anguloHorizontal;
    private float anguloVertical = 20f;

    void Start()
    {
        if (objetivo == null)
        {
            Jugador jugador = FindFirstObjectByType<Jugador>();
            if (jugador != null) objetivo = jugador.transform;
        }

        anguloHorizontal = transform.eulerAngles.y;

        if (bloquearCursor && usarMouse)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (objetivo == null) return;

        if (usarMouse)
        {
            anguloHorizontal += Input.GetAxis("Mouse X") * sensibilidad;
            anguloVertical -= Input.GetAxis("Mouse Y") * sensibilidad;
            anguloVertical = Mathf.Clamp(anguloVertical, anguloMinimo, anguloMaximo);
        }

        Vector3 puntoMirada = objetivo.position + Vector3.up * alturaMirada;
        Quaternion rotacion = Quaternion.Euler(anguloVertical, anguloHorizontal, 0f);
        Vector3 posicionDeseada = puntoMirada - rotacion * Vector3.forward * distancia;

        //si hay algo entre el jugador y la camara, acercar la camara
        if (evitarParedes)
        {
            Vector3 direccion = (posicionDeseada - puntoMirada).normalized;
            RaycastHit hit;
            if (Physics.Raycast(puntoMirada, direccion, out hit, distancia, capasQueBloquean, QueryTriggerInteraction.Ignore))
            {
                posicionDeseada = hit.point - direccion * 0.3f;
            }
        }

        transform.position = Vector3.Lerp(transform.position, posicionDeseada, suavizado * Time.deltaTime);
        transform.LookAt(puntoMirada);
    }
}
