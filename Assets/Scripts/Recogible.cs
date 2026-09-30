using UnityEngine;

//Objeto que el jugador recoge al pasarle cerca: una pieza del vehiculo o una
//dosis de suero. Flota y gira, y lleva una luz para que se vea de lejos.
//Se comprueba por distancia y no con triggers, asi los zombies no lo tocan.
public class Recogible : MonoBehaviour
{
    public enum Tipo { Pieza, Suero }

    public Tipo tipo = Tipo.Pieza;

    //lo que se muestra en el aviso al recogerlo ("Rueda", "Bidon de gasolina"...)
    public string nombre = "Pieza";
    public int cantidad = 1;

    public float radioRecogida = 1.6f;
    public AudioClip sonido;

    [Header("Animacion")]
    //el modelo que flota; si se deja vacio se mueve el objeto entero
    public Transform modelo;
    public float velocidadGiro = 60f;
    public float alturaFlote = 0.15f;

    private Jugador jugador;
    private Vector3 posicionModelo;

    void Start()
    {
        jugador = FindFirstObjectByType<Jugador>();
        if (modelo == null) modelo = transform;
        posicionModelo = modelo.localPosition;
    }

    void Update()
    {
        modelo.Rotate(Vector3.up, velocidadGiro * Time.deltaTime, Space.World);
        modelo.localPosition = posicionModelo + Vector3.up * (Mathf.Sin(Time.time * 2f) * alturaFlote);

        if (jugador == null) return;

        Vector3 diferencia = jugador.transform.position - transform.position;
        diferencia.y = 0f;
        if (diferencia.magnitude <= radioRecogida) Recoger();
    }

    void Recoger()
    {
        ControlPartida partida = ControlPartida.instancia;

        if (tipo == Tipo.Suero)
        {
            Disparo disparo = jugador.GetComponent<Disparo>();
            if (disparo == null) return;

            //con el maletin lleno se queda en el suelo para despues
            if (!disparo.AgregarSuero(cantidad)) return;

            if (partida != null) partida.Avisar("+" + cantidad + " suero");
        }
        else if (partida != null)
        {
            partida.RecogerPieza(nombre);
        }

        //se reproduce desde un punto aparte porque este objeto se destruye ya
        if (sonido != null) AudioSource.PlayClipAtPoint(sonido, transform.position);

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = tipo == Tipo.Pieza ? Color.yellow : Color.green;
        Gizmos.DrawWireSphere(transform.position, radioRecogida);
    }
}
