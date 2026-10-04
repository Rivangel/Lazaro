using UnityEngine;

//Piedra que lanza el jugador. La primera vez que choca con algo hace ruido,
//y los zombies que la oigan van a revisar ese punto.
[RequireComponent(typeof(Rigidbody))]
public class Distraccion : MonoBehaviour
{
    public float radioRuido = 16f;
    public float tiempoDeVida = 6f;

    private bool yaSono;

    void Start()
    {
        Destroy(gameObject, tiempoDeVida);
    }

    void OnCollisionEnter(Collision choque)
    {
        if (yaSono) return;
        yaSono = true;

        //no cuenta como ruido del jugador: sirve justamente para no delatarlo
        Ruido.Emitir(transform.position, radioRuido, false);
    }
}
