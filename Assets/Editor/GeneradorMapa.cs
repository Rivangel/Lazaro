using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GeneradorMapa
{
    //cuantas casillas tiene el pueblo por lado y cada cuantas casillas pasa una calle
    const int CasillasPorLado = 13;
    const int PasoEntreCalles = 4;

    //Si las calles salen giradas 90 grados sobre el piso, cambia esto a 90.
    const float GradosCalleRecta = 0f;

    //Los props del kit vienen con Z hacia arriba y Unity usa Y, asi que salen de pie.
    //Esto los acuesta. Si quedan boca abajo, cambialo a 90.
    const float CorreccionDeEje = -90f;

    const float AlturaPared = 6f;
    const float GrosorPared = 1f;

    //se calculan al generar, midiendo el modelo de calle y el del personaje
    static float tamanoCasilla = 4f;
    static float tamanoMapa = 52f;
    static float alturaPersonaje = 1.8f;

    //cuantos zombies se crean y a que distancia del centro
    const int CantidadNpcs = 10;

    //altura que se usa solo si falta el FBX del personaje y hay que poner una capsula
    const float AlturaPersonajeSinModelo = 1.8f;

    //Ya esta Zombie_Atlas.png en el proyecto, asi que los modelos usan su textura real.
    //Ponlo en true si alguna vez quieres volver a verlos en color plano.
    static readonly bool PintarConColorPlano = false;

    //objetos de la escena que NO se borran al limpiar (por ejemplo el que trae la musica)
    static readonly string[] Conservar = { "Juego" };

    const string CarpetaMateriales = "Assets/Materiales/Mapa";
    const string CarpetaAudio = "Assets/Audio/";
    const string CarpetaPrefabs = "Assets/Prefabs";
    const string CarpetaEntorno = "Assets/Modelos/Entorno/";
    const string CarpetaVehiculos = "Assets/Modelos/Vehiculos/";

    //props que se reparten dentro de las manzanas
    static readonly string[] PropsDeManzana =
    {
        "Container_Green", "Container_Red", "Couch", "Pallet", "Pallet_Broken",
        "Barrel", "CinderBlock", "Pipes", "TrashBag_1", "TrashBag_2",
        "Wheel", "Wheels_Stack", "Chest",
    };

    //props que se ponen pegados a la calle
    static readonly string[] PropsDeCalle =
    {
        "TrafficCone_1", "TrafficCone_2", "TrafficBarrier_1",
        "TrafficBarrier_2", "PlasticBarrier", "FireHydrant",
    };

    static readonly string[] ModelosVehiculo =
    {
        "Vehicle_Pickup", "Vehicle_Pickup_Armored", "Vehicle_Sports",
        "Vehicle_Sports_Armored", "Vehicle_Truck", "Vehicle_Truck_Armored",
    };

    static readonly string[] Manchas = { "Blood_1", "Blood_2", "Blood_3" };

    //Los modelos traen las 10 armas metidas como mallas y todas vienen visibles,
    //asi que hay que apagar las que no se usen.
    static readonly string[] ArmasDelModelo =
    {
        "Axe", "Guitar", "Spear", "Pistol", "Rifle", "Shotgun",
        "SMG", "WoodenBat_Barbed", "WoodenBat_Saw", "Knife",
    };

    //cambia esto por "Rifle", "Shotgun" o "SMG" si quieres otra arma
    const string ArmaEquipada = "Pistol";

    [MenuItem("Practica1/2 - Generar Mapa", false, 20)]
    public static void GenerarMapa()
    {
        //asegura que los modelos tengan rig y Animator Controller antes de usarlos
        ConfiguradorModelos.ConfigurarTodo();

        LimpiarEscena();

        GameObject mapa = new GameObject("Mapa");
        Undo.RegisterCreatedObjectUndo(mapa, "Generar Mapa");

        //hay que medir la casilla antes de colocar nada, todo depende de ella
        MedirCasilla();

        CrearSuelo(mapa.transform);
        CrearParedes(mapa.transform);
        CrearCiudad(mapa.transform);

        CrearLuz();
        GameObject jugador = CrearJugador();
        CrearCamara(jugador.transform);
        CrearNpcs();
        CrearControlPartida();
        CrearInterfaz(jugador.GetComponent<Vida>());

        Selection.activeGameObject = jugador;
        MarcarEscenaModificada();
        Debug.Log("Mapa generado. Play: WASD para moverte, Shift para correr, Espacio para saltar, mouse para la camara.");
    }

    [MenuItem("Practica1/Limpiar Escena", false, 40)]
    public static void LimpiarEscena()
    {
        Scene escena = SceneManager.GetActiveScene();
        foreach (GameObject objeto in escena.GetRootGameObjects())
        {
            if (System.Array.IndexOf(Conservar, objeto.name) >= 0) continue;
            Undo.DestroyObjectImmediate(objeto);
        }

        MarcarEscenaModificada();
    }

    // ---------- mapa ----------

    static void CrearSuelo(Transform padre)
    {
        //cubo aplastado: la cara de arriba queda exactamente en y = 0
        GameObject suelo = CrearCubo("Suelo", new Vector3(0f, -0.52f, 0f), new Vector3(tamanoMapa, 1f, tamanoMapa), padre);
        PintarObjeto(suelo, ObtenerMaterial("mat_suelo_mapa", new Color(0.3f, 0.36f, 0.26f)));
    }

    static void CrearParedes(Transform padre)
    {
        GameObject paredes = new GameObject("Paredes");
        paredes.transform.SetParent(padre);

        Material material = ObtenerMaterial("mat_pared_mapa", new Color(0.4f, 0.38f, 0.36f));
        float mitad = tamanoMapa / 2f;
        float largo = tamanoMapa + GrosorPared * 2f;
        float y = AlturaPared / 2f;

        CrearParedIndividual("Pared Norte", new Vector3(0f, y, mitad), new Vector3(largo, AlturaPared, GrosorPared), paredes.transform, material);
        CrearParedIndividual("Pared Sur", new Vector3(0f, y, -mitad), new Vector3(largo, AlturaPared, GrosorPared), paredes.transform, material);
        CrearParedIndividual("Pared Este", new Vector3(mitad, y, 0f), new Vector3(GrosorPared, AlturaPared, largo), paredes.transform, material);
        CrearParedIndividual("Pared Oeste", new Vector3(-mitad, y, 0f), new Vector3(GrosorPared, AlturaPared, largo), paredes.transform, material);
    }

    static void CrearParedIndividual(string nombre, Vector3 posicion, Vector3 escala, Transform padre, Material material)
    {
        GameObject pared = CrearCubo(nombre, posicion, escala, padre);
        PintarObjeto(pared, material);
    }

    //Arma el pueblo: rejilla de calles, manzanas con props y vehiculos aparcados.
    //El tamano de casilla se mide del propio modelo de calle, no va a ojo.
    static void CrearCiudad(Transform padre)
    {
        GameObject ciudad = new GameObject("Ciudad");
        ciudad.transform.SetParent(padre);

        Transform calles = CrearContenedor("Calles", ciudad.transform);
        Transform manzanas = CrearContenedor("Manzanas", ciudad.transform);
        Transform vehiculos = CrearContenedor("Vehiculos", ciudad.transform);
        Transform detalles = CrearContenedor("Detalles", ciudad.transform);

        //semilla fija: el pueblo sale igual cada vez que regeneras
        Random.InitState(20240314);

        int mitad = CasillasPorLado / 2;

        for (int x = -mitad; x <= mitad; x++)
        {
            for (int z = -mitad; z <= mitad; z++)
            {
                bool calleVertical = EsLineaDeCalle(x, mitad);
                bool calleHorizontal = EsLineaDeCalle(z, mitad);
                Vector3 centro = PosicionDeCasilla(x, z);

                if (calleVertical && calleHorizontal)
                {
                    //cruce: el modelo de 4 vias es simetrico, la rotacion da igual
                    ColocarModelo(CarpetaEntorno + "Street_4Way.fbx", centro, 0f, calles, false, false);
                }
                else if (calleVertical)
                {
                    ColocarModelo(CarpetaEntorno + "Street_Straight.fbx", centro, 90f + GradosCalleRecta, calles, false, false);
                }
                else if (calleHorizontal)
                {
                    ColocarModelo(CarpetaEntorno + "Street_Straight.fbx", centro, GradosCalleRecta, calles, false, false);
                }
                else
                {
                    LlenarManzana(centro, manzanas);
                }
            }
        }

        ColocarFarolas(detalles, mitad);
        ColocarVehiculos(vehiculos, mitad);
        ColocarManchas(detalles, mitad);
        ColocarReferencias(detalles, mitad);
    }

    static bool EsLineaDeCalle(int indice, int mitad)
    {
        return (indice + mitad) % PasoEntreCalles == 0;
    }

    static Vector3 PosicionDeCasilla(int x, int z)
    {
        return new Vector3(x * tamanoCasilla, 0f, z * tamanoCasilla);
    }

    //cada manzana recibe unos cuantos props sueltos
    static void LlenarManzana(Vector3 centro, Transform padre)
    {
        //deja libre la zona donde aparece el jugador
        if (Vector3.Distance(centro, PosicionDeSpawn()) < tamanoCasilla * 1.5f) return;

        int cantidad = Random.Range(0, 4);
        for (int i = 0; i < cantidad; i++)
        {
            string nombre = PropsDeManzana[Random.Range(0, PropsDeManzana.Length)];
            Vector3 posicion = centro + DesplazamientoDentroDeCasilla(0.7f);
            ColocarModelo(CarpetaEntorno + nombre + ".fbx", posicion, Random.Range(0f, 360f), padre, true, true);
        }
    }

    //farolas y semaforos en los cruces, mas conos y barreras sueltos por las calles
    static void ColocarFarolas(Transform padre, int mitad)
    {
        for (int x = -mitad; x <= mitad; x++)
        {
            for (int z = -mitad; z <= mitad; z++)
            {
                bool esCruce = EsLineaDeCalle(x, mitad) && EsLineaDeCalle(z, mitad);
                if (!esCruce) continue;

                Vector3 centro = PosicionDeCasilla(x, z);

                //el semaforo va en una esquina del cruce
                string semaforo = Random.value < 0.5f ? "TrafficLight_1" : "TrafficLight_2";
                Vector3 esquina = centro + new Vector3(tamanoCasilla * 0.38f, 0f, tamanoCasilla * 0.38f);
                ColocarModelo(CarpetaEntorno + semaforo + ".fbx", esquina, Random.Range(0f, 360f), padre, true, true);

                if (Random.value < 0.6f)
                {
                    Vector3 farola = centro + new Vector3(-tamanoCasilla * 0.38f, 0f, -tamanoCasilla * 0.38f);
                    ColocarModelo(CarpetaEntorno + "StreetLights.fbx", farola, Random.Range(0f, 360f), padre, true, true);
                }

                //algo de desorden en la calzada
                if (Random.value < 0.7f)
                {
                    string estorbo = PropsDeCalle[Random.Range(0, PropsDeCalle.Length)];
                    Vector3 posicion = centro + DesplazamientoDentroDeCasilla(0.6f);
                    ColocarModelo(CarpetaEntorno + estorbo + ".fbx", posicion, Random.Range(0f, 360f), padre, true, true);
                }
            }
        }
    }

    //coches aparcados a lo largo de las calles, alineados con la direccion de la via
    static void ColocarVehiculos(Transform padre, int mitad)
    {
        for (int x = -mitad; x <= mitad; x++)
        {
            for (int z = -mitad; z <= mitad; z++)
            {
                bool calleVertical = EsLineaDeCalle(x, mitad);
                bool calleHorizontal = EsLineaDeCalle(z, mitad);

                //solo en tramos rectos, no en los cruces
                if (calleVertical == calleHorizontal) continue;
                if (Random.value > 0.28f) continue;

                Vector3 centro = PosicionDeCasilla(x, z);
                if (Vector3.Distance(centro, PosicionDeSpawn()) < tamanoCasilla * 1.5f) continue;

                //aparcado a un lado, mirando en la direccion de la calle
                float ladoDeLaCalle = (Random.value < 0.5f ? -1f : 1f) * tamanoCasilla * 0.22f;
                Vector3 posicion = calleVertical
                    ? centro + new Vector3(ladoDeLaCalle, 0f, 0f)
                    : centro + new Vector3(0f, 0f, ladoDeLaCalle);

                float rotacion = calleVertical ? 0f : 90f;
                //uno de cada cinco esta atravesado, como abandonado de golpe
                if (Random.value < 0.2f) rotacion += Random.Range(20f, 50f);

                string modelo = ModelosVehiculo[Random.Range(0, ModelosVehiculo.Length)];
                ColocarModelo(CarpetaVehiculos + modelo + ".fbx", posicion, rotacion, padre, true, true);
            }
        }
    }

    static void ColocarManchas(Transform padre, int mitad)
    {
        for (int i = 0; i < 25; i++)
        {
            float limite = mitad * tamanoCasilla;
            Vector3 posicion = new Vector3(Random.Range(-limite, limite), 0.02f, Random.Range(-limite, limite));

            string mancha = Manchas[Random.Range(0, Manchas.Length)];
            //planas en el suelo, sin colision
            ColocarModelo(CarpetaEntorno + mancha + ".fbx", posicion, Random.Range(0f, 360f), padre, false, false);
        }
    }

    //torre de agua y cartel del pueblo, para orientarse dentro del mapa
    static void ColocarReferencias(Transform padre, int mitad)
    {
        float borde = (mitad - 1) * tamanoCasilla;

        ColocarModelo(CarpetaEntorno + "WaterTower.fbx", new Vector3(-borde, 0f, borde), 0f, padre, true, true);
        ColocarModelo(CarpetaEntorno + "TownSign.fbx", PosicionDeSpawn() + new Vector3(tamanoCasilla, 0f, 0f), -90f, padre, true, true);
    }

    static Vector3 DesplazamientoDentroDeCasilla(float margen)
    {
        float alcance = tamanoCasilla * 0.5f * margen;
        return new Vector3(Random.Range(-alcance, alcance), 0f, Random.Range(-alcance, alcance));
    }

    //el jugador aparece en un cruce, para que vea las calles desde el primer momento
    static Vector3 PosicionDeSpawn()
    {
        return PosicionDeCasilla(2, 2);
    }

    static Transform CrearContenedor(string nombre, Transform padre)
    {
        GameObject objeto = new GameObject(nombre);
        objeto.transform.SetParent(padre);
        return objeto.transform;
    }

    // ---------- medidas ----------

    //Mide el modelo de calle para saber cuanto ocupa una casilla. Si se diera por
    //supuesto un tamano y el modelo tuviera otro, las calles quedarian separadas.
    static void MedirCasilla()
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarpetaEntorno + "Street_Straight.fbx");
        if (asset == null)
        {
            Debug.LogWarning("Falta Street_Straight.fbx; se usa el tamano de casilla por defecto.");
            tamanoMapa = CasillasPorLado * tamanoCasilla;
            return;
        }

        //hay que medirlo ya acostado: de pie, el largo de la calle cae sobre el eje Y
        //y la medida saldria del ancho en vez de del largo
        GameObject temporal = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        temporal.transform.rotation = Quaternion.Euler(CorreccionDeEje, 0f, 0f);

        Bounds limites;
        if (CalcularLimites(temporal, out limites))
        {
            tamanoCasilla = Mathf.Max(limites.size.x, limites.size.z);
        }

        Object.DestroyImmediate(temporal);

        tamanoMapa = CasillasPorLado * tamanoCasilla;
        Debug.Log("Casilla de calle: " + tamanoCasilla.ToString("0.00") + " unidades. Mapa: " + tamanoMapa.ToString("0.0"));
    }

    //Coloca un FBX del kit en la escena. asentar deja la base del modelo sobre el
    //suelo; conColision le pone una caja para que el jugador no lo atraviese.
    static GameObject ColocarModelo(string rutaModelo, Vector3 posicion, float rotacionY,
                                    Transform padre, bool asentar, bool conColision)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
        if (asset == null)
        {
            Debug.LogWarning("No se encontro " + rutaModelo);
            return null;
        }

        //El modelo va dentro de un envoltorio: el modelo lleva la correccion de eje y
        //el envoltorio la rotacion del mapa. Asi la caja de colision se calcula sobre
        //un objeto sin rotar y sale ajustada en vez de hinchada.
        GameObject contenedor = new GameObject(asset.name);
        contenedor.transform.SetParent(padre);
        contenedor.transform.position = posicion;
        contenedor.transform.rotation = Quaternion.identity;

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset);

        //igual que con los personajes: si sigue ligado al prefab, lo que se le cambie
        //por script no se guarda y se pierde al dar Play
        PrefabUtility.UnpackPrefabInstance(instancia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        instancia.transform.SetParent(contenedor.transform);
        instancia.transform.localPosition = Vector3.zero;
        instancia.transform.localRotation = Quaternion.Euler(CorreccionDeEje, 0f, 0f);

        if (asentar) Asentar(contenedor, posicion.y);
        if (conColision) AgregarColision(contenedor);

        contenedor.transform.rotation = Quaternion.Euler(0f, rotacionY, 0f);
        return contenedor;
    }

    //baja o sube el objeto para que su punto mas bajo quede a la altura indicada
    static void Asentar(GameObject objeto, float alturaDelSuelo)
    {
        Bounds limites;
        if (!CalcularLimites(objeto, out limites)) return;

        objeto.transform.position += Vector3.up * (alturaDelSuelo - limites.min.y);
    }

    //caja de colision a partir del tamano real del modelo
    static void AgregarColision(GameObject objeto)
    {
        Bounds limites;
        if (!CalcularLimites(objeto, out limites)) return;

        //lo muy plano (manchas de sangre, chatarra suelta) no estorba el paso
        if (limites.size.y < 0.35f) return;

        BoxCollider caja = objeto.AddComponent<BoxCollider>();
        caja.center = objeto.transform.InverseTransformPoint(limites.center);

        Vector3 escala = objeto.transform.lossyScale;
        caja.size = new Vector3(
            limites.size.x / Mathf.Max(0.0001f, escala.x),
            limites.size.y / Mathf.Max(0.0001f, escala.y),
            limites.size.z / Mathf.Max(0.0001f, escala.z));
    }

    // ---------- personajes ----------

    static GameObject CrearJugador()
    {
        GameObject jugador = new GameObject("Jugador");
        jugador.transform.position = PosicionDeSpawn() + Vector3.up * 0.1f;
        jugador.tag = "Player";
        Undo.RegisterCreatedObjectUndo(jugador, "Generar Mapa");

        Material material = ObtenerMaterial("mat_jugador_mapa", new Color(0.35f, 0.55f, 0.75f));

        //el modelo va primero: su altura real define el tamano del CharacterController
        float altura;
        Transform arma = CrearVisual(jugador.transform, ConfiguradorModelos.ModeloJugador, material, out altura);
        alturaPersonaje = altura;

        AgregarControlador(jugador, altura);

        //sonido en 2D: el jugador siempre se oye igual de fuerte
        AudioSource audio = jugador.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        Vida vida = jugador.AddComponent<Vida>();
        vida.vidaMaxima = 10;
        vida.tiempoDeGracia = 0.5f;
        vida.fuenteAudio = audio;
        vida.sonidoGolpe = CargarAudio("steve-old-hurt-sound.mp3");
        vida.sonidoMuerte = CargarAudio("mariobros-gameover.mp3");

        jugador.AddComponent<Jugador>();

        Disparo disparo = jugador.AddComponent<Disparo>();
        disparo.fuenteAudio = audio;
        disparo.sonidoDisparo = CargarAudio("plasma-shot.mp3");
        disparo.materialTrazo = ObtenerMaterialTrazo();
        disparo.prefabProyectil = ObtenerPrefabProyectil();
        //el trazo sale de la punta del arma que quedo equipada
        disparo.puntoDisparo = arma;

        return jugador;
    }

    static void CrearNpcs()
    {
        GameObject contenedor = new GameObject("NPCs");
        Undo.RegisterCreatedObjectUndo(contenedor, "Generar Mapa");

        Material material = ObtenerMaterial("mat_zombie_mapa", new Color(0.45f, 0.6f, 0.35f));
        string[] modelos = ConfiguradorModelos.ModelosZombie;

        for (int i = 0; i < CantidadNpcs; i++)
        {
            //repartidos en circulo alrededor del centro del pueblo
            float angulo = (360f / CantidadNpcs) * i;
            float radio = tamanoMapa * 0.3f;
            Vector3 posicion = Quaternion.Euler(0f, angulo, 0f) * new Vector3(0f, 0f, radio);
            posicion.y = 0.1f;

            //se van alternando los modelos de zombie disponibles
            string rutaModelo = modelos[i % modelos.Length];
            string nombreCorto = System.IO.Path.GetFileNameWithoutExtension(rutaModelo);

            GameObject npc = new GameObject(nombreCorto + " " + (i + 1));
            npc.transform.SetParent(contenedor.transform);
            npc.transform.position = posicion;

            float altura;
            CrearVisual(npc.transform, rutaModelo, material, out altura);
            AgregarControlador(npc, altura);

            //sonido en 3D: se oye mas fuerte cuanto mas cerca este el zombie
            AudioSource audio = npc.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 30f;

            Vida vida = npc.AddComponent<Vida>();
            vida.vidaMaxima = 3;
            vida.tiempoDeGracia = 0.05f;
            vida.fuenteAudio = audio;
            vida.sonidoGolpe = CargarAudio("bonk-meme.mp3");
            vida.sonidoMuerte = CargarAudio("tom-scream.mp3");

            Npc script = npc.AddComponent<Npc>();
            script.nombre = nombreCorto;
            //ya persiguen al jugador; desmarcalo si los quieres pacificos
            script.esEnemigo = true;
        }
    }

    static void AgregarControlador(GameObject objeto, float altura)
    {
        CharacterController controller = objeto.AddComponent<CharacterController>();
        controller.height = altura;
        //proporcional a la altura, para que sirva igual si el kit viene a otra escala
        controller.radius = altura * 0.2f;
        controller.center = new Vector3(0f, altura / 2f, 0f);
        controller.slopeLimit = 50f;
        controller.stepOffset = altura * 0.25f;
    }

    //crea el hijo "Visual", mete dentro el modelo importado y devuelve el arma equipada.
    //alturaModelo sale con la altura real del personaje, medida del propio modelo.
    static Transform CrearVisual(Transform padre, string rutaModelo, Material material, out float alturaModelo)
    {
        alturaModelo = AlturaPersonajeSinModelo;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(padre);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaModelo);
        if (modelo == null)
        {
            Debug.LogWarning("No se encontro " + rutaModelo + "; se usa una capsula de relleno.");
            CrearCapsulaDeRelleno(visual.transform, material);
            return null;
        }

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        instancia.transform.SetParent(visual.transform);
        instancia.transform.localPosition = Vector3.zero;
        instancia.transform.localRotation = Quaternion.identity;

        //Se desvincula del prefab del FBX. Si sigue ligado, lo que se le cambie por script
        //(controller, avatar, materiales) no se guarda como override y se pierde al dar Play.
        PrefabUtility.UnpackPrefabInstance(instancia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        //se respeta la escala nativa del kit para que encaje con los props;
        //solo se asienta para que los pies queden en el suelo
        Asentar(instancia, padre.position.y);

        Bounds limites;
        if (CalcularLimites(instancia, out limites) && limites.size.y > 0.01f)
        {
            alturaModelo = limites.size.y;
        }

        //deja visible solo un arma (los zombies no tienen ninguna y esto no hace nada)
        Transform arma = EquiparArma(instancia, ArmaEquipada);

        if (PintarConColorPlano)
        {
            PintarPersonaje(instancia, material);
        }

        Animator animador = instancia.GetComponentInChildren<Animator>();
        if (animador == null)
        {
            Debug.LogWarning("El modelo " + rutaModelo + " no trae Animator.");
            return arma;
        }

        RuntimeAnimatorController controlador = ConfiguradorModelos.CargarControlador(rutaModelo);
        if (controlador != null)
        {
            animador.runtimeAnimatorController = controlador;
        }
        else
        {
            Debug.LogWarning("Sin Animator Controller para " + rutaModelo + ": corre primero 1 - Configurar Modelos.");
        }

        //sin Avatar el rig generico no reproduce nada, asi que se asigna a mano
        Avatar avatar = ConfiguradorModelos.CargarAvatar(rutaModelo);
        if (avatar != null)
        {
            animador.avatar = avatar;
        }
        else
        {
            Debug.LogWarning("Sin Avatar en " + rutaModelo + ": las animaciones no se van a ver.");
        }

        //el personaje se mueve con el CharacterController, no con la animacion
        animador.applyRootMotion = false;

        return arma;
    }

    //apaga todas las armas del modelo menos la elegida; devuelve la que quedo visible
    static Transform EquiparArma(GameObject modelo, string armaElegida)
    {
        Transform equipada = null;

        foreach (string nombre in ArmasDelModelo)
        {
            Transform arma = BuscarHijo(modelo.transform, nombre);
            if (arma == null) continue;

            bool esLaElegida = nombre == armaElegida;
            arma.gameObject.SetActive(esLaElegida);
            if (esLaElegida) equipada = arma;
        }

        return equipada;
    }

    //busca un hijo por nombre a cualquier profundidad
    static Transform BuscarHijo(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;

        foreach (Transform hijo in padre)
        {
            Transform encontrado = BuscarHijo(hijo, nombre);
            if (encontrado != null) return encontrado;
        }

        return null;
    }

    static bool CalcularLimites(GameObject objeto, out Bounds limites)
    {
        limites = new Bounds();
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        limites = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            limites.Encapsulate(renderers[i].bounds);
        }
        return true;
    }

    static void PintarPersonaje(GameObject modelo, Material material)
    {
        foreach (Renderer renderer in modelo.GetComponentsInChildren<Renderer>())
        {
            Material[] materiales = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
            renderer.sharedMaterials = materiales;
        }
    }

    //solo se usa si falta el FBX, para que el personaje no quede invisible
    static void CrearCapsulaDeRelleno(Transform padre, Material material)
    {
        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cuerpo.name = "Cuerpo";
        cuerpo.transform.SetParent(padre);
        cuerpo.transform.localPosition = new Vector3(0f, AlturaPersonajeSinModelo / 2f, 0f);
        cuerpo.transform.localScale = new Vector3(0.7f, AlturaPersonajeSinModelo / 2f, 0.7f);
        Object.DestroyImmediate(cuerpo.GetComponent<Collider>());
        PintarObjeto(cuerpo, material);
    }

    // ---------- escena ----------

    static void CrearCamara(Transform objetivo)
    {
        GameObject camara = new GameObject("Main Camera");
        camara.tag = "MainCamera";
        camara.transform.position = new Vector3(0f, 4f, -13f);
        Undo.RegisterCreatedObjectUndo(camara, "Generar Mapa");

        camara.AddComponent<Camera>();
        camara.AddComponent<AudioListener>();

        CamaraSeguimiento seguimiento = camara.AddComponent<CamaraSeguimiento>();
        seguimiento.objetivo = objetivo;
        seguimiento.alturaMirada = alturaPersonaje * 0.8f;
    }

    static void CrearLuz()
    {
        GameObject luz = new GameObject("Directional Light");
        luz.transform.position = new Vector3(0f, 12f, 0f);
        luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        Undo.RegisterCreatedObjectUndo(luz, "Generar Mapa");

        Light componente = luz.AddComponent<Light>();
        componente.type = LightType.Directional;
        componente.shadows = LightShadows.Soft;
        componente.intensity = 1f;
    }

    // ---------- partida e interfaz ----------

    static void CrearControlPartida()
    {
        GameObject objeto = new GameObject("Control Partida");
        Undo.RegisterCreatedObjectUndo(objeto, "Generar Mapa");
        objeto.AddComponent<ControlPartida>();
    }

    //Canvas con la barra de vida, la mira del centro y el contador de zombies
    static void CrearInterfaz(Vida vidaJugador)
    {
        GameObject objetoCanvas = new GameObject("Interfaz");
        Undo.RegisterCreatedObjectUndo(objetoCanvas, "Generar Mapa");

        Canvas canvas = objetoCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        //la interfaz se reescala sola con el tamano de la ventana
        CanvasScaler escalador = objetoCanvas.AddComponent<CanvasScaler>();
        escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escalador.referenceResolution = new Vector2(1920f, 1080f);
        escalador.matchWidthOrHeight = 0.5f;

        InterfazJuego interfaz = objetoCanvas.AddComponent<InterfazJuego>();
        interfaz.vidaJugador = vidaJugador;

        CrearBarraDeVida(objetoCanvas.transform, interfaz);
        CrearMira(objetoCanvas.transform);
        interfaz.textoZombies = CrearContador(objetoCanvas.transform);
    }

    static void CrearBarraDeVida(Transform padre, InterfazJuego interfaz)
    {
        //fondo oscuro de la barra, pegado arriba a la izquierda
        Image fondo = CrearImagen("Barra Vida", padre, new Color(0f, 0f, 0f, 0.55f));
        AnclarArribaIzquierda(fondo.rectTransform, new Vector2(40f, -40f), new Vector2(420f, 40f));

        //el relleno es la parte que se vacia al recibir daño
        //El ancho se controla moviendo el ancla derecha, no con fillAmount:
        //fillAmount necesita que la Image tenga sprite y estas se crean sin uno.
        Image relleno = CrearImagen("Relleno", fondo.transform, interfaz.colorLleno);
        EstirarACompleto(relleno.rectTransform, 4f);

        Text texto = CrearTexto("Texto Vida", fondo.transform, 24, TextAnchor.MiddleCenter);
        EstirarACompleto(texto.rectTransform, 0f);

        interfaz.rellenoBarra = relleno;
        interfaz.textoVida = texto;
    }

    //cuatro rayitas alrededor del centro de la pantalla
    static void CrearMira(Transform padre)
    {
        GameObject objeto = new GameObject("Mira", typeof(RectTransform));
        objeto.transform.SetParent(padre, false);

        RectTransform rect = (RectTransform)objeto.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(40f, 40f);

        Color color = new Color(1f, 1f, 1f, 0.75f);
        float largo = 12f;
        float grosor = 2f;
        float separacion = 8f;

        CrearRayaDeMira("Arriba", objeto.transform, color, new Vector2(0f, separacion + largo / 2f), new Vector2(grosor, largo));
        CrearRayaDeMira("Abajo", objeto.transform, color, new Vector2(0f, -separacion - largo / 2f), new Vector2(grosor, largo));
        CrearRayaDeMira("Izquierda", objeto.transform, color, new Vector2(-separacion - largo / 2f, 0f), new Vector2(largo, grosor));
        CrearRayaDeMira("Derecha", objeto.transform, color, new Vector2(separacion + largo / 2f, 0f), new Vector2(largo, grosor));
    }

    static void CrearRayaDeMira(string nombre, Transform padre, Color color, Vector2 posicion, Vector2 tamano)
    {
        Image raya = CrearImagen(nombre, padre, color);
        RectTransform rect = raya.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamano;
    }

    static Text CrearContador(Transform padre)
    {
        Text texto = CrearTexto("Texto Zombies", padre, 26, TextAnchor.UpperLeft);
        AnclarArribaIzquierda(texto.rectTransform, new Vector2(40f, -92f), new Vector2(420f, 40f));
        texto.text = "Zombies: 0";
        return texto;
    }

    static Image CrearImagen(string nombre, Transform padre, Color color)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        objeto.transform.SetParent(padre, false);

        //una Image sin sprite se dibuja como un rectangulo de color, que es lo que hace falta
        Image imagen = objeto.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        return imagen;
    }

    static Text CrearTexto(string nombre, Transform padre, int tamano, TextAnchor alineacion)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform));
        objeto.transform.SetParent(padre, false);

        Text texto = objeto.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamano;
        texto.alignment = alineacion;
        texto.color = Color.white;
        texto.raycastTarget = false;
        return texto;
    }

    static void AnclarArribaIzquierda(RectTransform rect, Vector2 posicion, Vector2 tamano)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamano;
    }

    //hace que el elemento ocupe todo su padre, con un margen
    static void EstirarACompleto(RectTransform rect, float margen)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(margen, margen);
        rect.offsetMax = new Vector2(-margen, -margen);
    }

    // ---------- utilidades ----------

    static GameObject CrearCubo(string nombre, Vector3 posicion, Vector3 escala, Transform padre)
    {
        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = nombre;
        cubo.transform.SetParent(padre);
        cubo.transform.position = posicion;
        cubo.transform.localScale = escala;
        return cubo;
    }

    static void PintarObjeto(GameObject objeto, Material material)
    {
        Renderer renderer = objeto.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
    }

    //crea el material una sola vez y lo reutiliza en las siguientes generaciones
    static Material ObtenerMaterial(string nombre, Color color)
    {
        AsegurarCarpetaMateriales();

        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            material.color = color;
            AssetDatabase.CreateAsset(material, ruta);
        }

        return material;
    }

    //Bala reutilizable guardada como prefab, para que la puedas editar desde el proyecto
    //en vez de tener que tocar el codigo.
    static GameObject ObtenerPrefabProyectil()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaPrefabs))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string ruta = CarpetaPrefabs + "/Proyectil.prefab";
        GameObject existente = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (existente != null) return existente;

        GameObject temporal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        temporal.name = "Proyectil";
        temporal.transform.localScale = Vector3.one * 0.14f;

        //el choque lo calcula el script con un SphereCast, el collider sobra
        Object.DestroyImmediate(temporal.GetComponent<Collider>());
        PintarObjeto(temporal, ObtenerMaterialProyectil());

        //estela para que se vea el recorrido de la bala
        TrailRenderer estela = temporal.AddComponent<TrailRenderer>();
        estela.time = 0.12f;
        estela.startWidth = 0.1f;
        estela.endWidth = 0f;
        estela.material = ObtenerMaterialTrazo();
        estela.startColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        estela.endColor = new Color(1f, 0.6f, 0.2f, 0f);
        estela.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        estela.receiveShadows = false;

        temporal.AddComponent<Proyectil>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temporal, ruta);
        Object.DestroyImmediate(temporal);

        return prefab;
    }

    //material de la bala: color brillante con emision para que resalte de noche
    static Material ObtenerMaterialProyectil()
    {
        AsegurarCarpetaMateriales();

        string ruta = CarpetaMateriales + "/mat_proyectil.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            Color color = new Color(1f, 0.8f, 0.3f);

            material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);

            AssetDatabase.CreateAsset(material, ruta);
        }

        return material;
    }

    static AudioClip CargarAudio(string nombreArchivo)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CarpetaAudio + nombreArchivo);
        if (clip == null)
        {
            Debug.LogWarning("No se encontro el audio " + CarpetaAudio + nombreArchivo);
        }
        return clip;
    }

    //material del trazo del disparo; Sprites/Default respeta el color y la transparencia
    static Material ObtenerMaterialTrazo()
    {
        AsegurarCarpetaMateriales();

        string ruta = CarpetaMateriales + "/mat_trazo_disparo.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, ruta);
        }

        return material;
    }

    static void AsegurarCarpetaMateriales()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales"))
        {
            AssetDatabase.CreateFolder("Assets", "Materiales");
        }
        if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
        {
            AssetDatabase.CreateFolder("Assets/Materiales", "Mapa");
        }
    }

    static void MarcarEscenaModificada()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
