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

    //Orientacion de las piezas en T y en curva. Si salen mal encaradas, sumales 90
    //hasta que encajen (el cruce es simetrico y no necesita constante).
    const float GradosCalleT = 0f;
    const float GradosCalleCurva = 0f;

    //Las calles se levantan un poco sobre el suelo. Si quedan a la misma altura,
    //la tarjeta grafica no sabe cual dibujar encima y se ven manchas verdes (z-fighting).
    const float AlturaCalles = 0.02f;

    const float AlturaPared = 6f;
    const float GrosorPared = 1f;

    //se calculan al generar, midiendo el modelo de calle y el del personaje
    static float tamanoCasilla = 4f;
    static float tamanoMapa = 52f;
    static float alturaPersonaje = 1.8f;

    //que casillas son calle; se calcula antes de construir para poder mirar los vecinos
    static bool[,] rejillaDeCalles;
    static int mitadRejilla;

    //cuantos zombies se crean y a que distancia del centro
    const int CantidadNpcs = 10;

    //perros zombi: pocos, pero rapidos y con muy buen oido
    const int CantidadPerros = 4;

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
        //Ojo: aqui NO van Wheel, Barrel, Pipes ni Chest_Special. Esos cuatro son las
        //piezas de la mision y si se reparten como adorno no hay quien las encuentre.
        "Container_Green", "Container_Red", "Couch", "Pallet", "Pallet_Broken",
        "CinderBlock", "TrashBag_1", "TrashBag_2", "Wheels_Stack", "Chest",
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

    //arma blanca que el jugador saca solo al golpear de cerca
    const string ArmaCuerpoACuerpo = "Knife";

    //piezas que hay que encontrar para reparar la camioneta: modelo, nombre y casilla
    static readonly string[] ModelosPieza = { "Wheel", "Barrel", "Pipes", "Chest_Special" };
    static readonly string[] NombresPieza = { "Rueda", "Bidon de gasolina", "Tubo de escape", "Caja de herramientas" };
    static readonly Vector2Int[] CasillasPieza =
    {
        new Vector2Int(-4, 4), new Vector2Int(4, -4), new Vector2Int(-4, -1), new Vector2Int(0, 5),
    };

    //la camioneta de escape queda en una calle lejos de donde aparece el jugador
    static readonly Vector2Int CasillaVehiculo = new Vector2Int(-2, -5);
    const string ModeloVehiculoEscape = "Vehicle_Pickup";

    const int CantidadSueros = 6;

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
        CrearPerros();
        CrearMision(mapa.transform);
        CrearControlPartida();
        CrearInterfaz(jugador.GetComponent<Vida>());
        AgregarEscenaABuild();

        Selection.activeGameObject = jugador;
        MarcarEscenaModificada();
        Debug.Log("Mapa generado. Play: WASD moverte, Shift correr, C agacharse, Espacio saltar, clic disparar, "
                  + "1/2/Q cambiar balas y suero, G lanzar piedra, E reparar la camioneta.");
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

        CalcularRejilla();

        int mitad = CasillasPorLado / 2;

        for (int x = -mitad; x <= mitad; x++)
        {
            for (int z = -mitad; z <= mitad; z++)
            {
                if (EsCalle(x, z))
                {
                    ColocarPiezaDeCalle(x, z, calles);
                    ColocarAcera(x, z, detalles);
                }
                else
                {
                    LlenarManzana(PosicionDeCasilla(x, z), manzanas);
                }
            }
        }

        ColocarFarolas(detalles, mitad);
        ColocarVehiculos(vehiculos, mitad);
        ColocarBarricada(vehiculos, detalles);
        ColocarManchas(detalles, mitad);
        ColocarReferencias(detalles, mitad);
    }

    // ---------- rejilla de calles ----------

    //se calcula una vez que casillas son calle, para poder mirar los vecinos
    static void CalcularRejilla()
    {
        int lado = CasillasPorLado;
        int mitad = lado / 2;

        rejillaDeCalles = new bool[lado, lado];
        mitadRejilla = mitad;

        for (int x = 0; x < lado; x++)
        {
            for (int z = 0; z < lado; z++)
            {
                bool lineaVertical = x % PasoEntreCalles == 0;
                bool lineaHorizontal = z % PasoEntreCalles == 0;

                //el borde del mapa siempre es calle: cierra el pueblo con un anillo
                bool borde = x == 0 || z == 0 || x == lado - 1 || z == lado - 1;

                rejillaDeCalles[x, z] = lineaVertical || lineaHorizontal || borde;
            }
        }

    }

    //Lo usa el sistema de mision para no dejar objetos en mitad de la calzada.
    //Va sobre la rejilla nueva, asi EsLineaDeCalle(x) || EsLineaDeCalle(z) == EsCalle(x,z).
    static bool EsLineaDeCalle(int indice, int mitad)
    {
        int i = indice + mitadRejilla;
        return i <= 0 || i >= CasillasPorLado - 1 || i % PasoEntreCalles == 0;
    }

    static bool EsCalle(int x, int z)
    {
        int ix = x + mitadRejilla;
        int iz = z + mitadRejilla;

        if (ix < 0 || iz < 0 || ix >= CasillasPorLado || iz >= CasillasPorLado) return false;
        return rejillaDeCalles[ix, iz];
    }

    //Escoge la pieza segun los vecinos: cruce, T, curva o recta. Asi el borde del
    //pueblo queda cerrado en vez de con calles cortadas a la mitad.
    static void ColocarPiezaDeCalle(int x, int z, Transform padre)
    {
        bool norte = EsCalle(x, z + 1);
        bool sur = EsCalle(x, z - 1);
        bool este = EsCalle(x + 1, z);
        bool oeste = EsCalle(x - 1, z);

        int vecinos = (norte ? 1 : 0) + (sur ? 1 : 0) + (este ? 1 : 0) + (oeste ? 1 : 0);
        Vector3 centro = PosicionDeCasilla(x, z);

        if (vecinos >= 4)
        {
            //el cruce es simetrico, da igual como se gire
            ColocarModelo(CarpetaEntorno + "Street_4Way.fbx", centro, 0f, padre, false, false);
            return;
        }

        if (vecinos == 3)
        {
            //la T se orienta por el lado que NO tiene salida
            float giro = !norte ? 0f : !este ? 90f : !sur ? 180f : 270f;
            ColocarModelo(CarpetaEntorno + "Street_T.fbx", centro, giro + GradosCalleT, padre, false, false);
            return;
        }

        if (vecinos == 2 && !(norte && sur) && !(este && oeste))
        {
            //curva: se orienta por la esquina que forman los dos lados abiertos
            float giro = (norte && este) ? 0f : (este && sur) ? 90f : (sur && oeste) ? 180f : 270f;
            ColocarModelo(CarpetaEntorno + "Street_Turn.fbx", centro, giro + GradosCalleCurva, padre, false, false);
            return;
        }

        //recta: de vez en cuando una con grietas para que no se vea todo igual
        string modelo = "Street_Straight";
        float sorteo = Random.value;
        if (sorteo < 0.12f) modelo = "Street_Straight_Crack1";
        else if (sorteo < 0.24f) modelo = "Street_Straight_Crack2";

        //si corre de norte a sur hay que girarla 90
        bool vertical = norte || sur;
        float rotacion = (vertical ? 90f : 0f) + GradosCalleRecta;

        ColocarModelo(CarpetaEntorno + modelo + ".fbx", centro, rotacion, padre, false, false);
    }

    //cosas pegadas al bordillo, en el lado de la calle que da a una manzana
    static void ColocarAcera(int x, int z, Transform padre)
    {
        if (Random.value > 0.35f) return;

        //busca un lado de esta casilla que de a una manzana
        Vector3 haciaLaManzana = Vector3.zero;
        if (!EsCalle(x + 1, z)) haciaLaManzana = Vector3.right;
        else if (!EsCalle(x - 1, z)) haciaLaManzana = Vector3.left;
        else if (!EsCalle(x, z + 1)) haciaLaManzana = Vector3.forward;
        else if (!EsCalle(x, z - 1)) haciaLaManzana = Vector3.back;
        else return;

        Vector3 centro = PosicionDeCasilla(x, z);
        if (Vector3.Distance(centro, PosicionDeSpawn()) < tamanoCasilla) return;

        Vector3 posicion = centro + haciaLaManzana * (tamanoCasilla * 0.38f);
        posicion += DesplazamientoDentroDeCasilla(0.25f);

        string prop = PropsDeCalle[Random.Range(0, PropsDeCalle.Length)];
        ColocarModelo(CarpetaEntorno + prop + ".fbx", posicion, Random.Range(0f, 360f), padre, true, true);
    }

    // ---------- relleno ----------

    //cada manzana recibe props sueltos, y algunas llevan contenedores apilados
    static void LlenarManzana(Vector3 centro, Transform padre)
    {
        //deja libre la zona donde aparece el jugador
        if (Vector3.Distance(centro, PosicionDeSpawn()) < tamanoCasilla * 1.5f) return;

        //una de cada cinco manzanas es un apilado de contenedores, que hace de edificio
        if (Random.value < 0.2f)
        {
            ApilarContenedores(centro, padre);
            return;
        }

        int cantidad = Random.Range(1, 5);
        for (int i = 0; i < cantidad; i++)
        {
            string nombre = PropsDeManzana[Random.Range(0, PropsDeManzana.Length)];
            Vector3 posicion = centro + DesplazamientoDentroDeCasilla(0.7f);
            ColocarModelo(CarpetaEntorno + nombre + ".fbx", posicion, Random.Range(0f, 360f), padre, true, true);
        }
    }

    //El kit no trae edificios, asi que los contenedores apilados hacen de estructura
    //y de paso dan sitios altos a los que subirse.
    static void ApilarContenedores(Vector3 centro, Transform padre)
    {
        int columnas = Random.Range(2, 4);

        for (int i = 0; i < columnas; i++)
        {
            Vector3 baseColumna = centro + DesplazamientoDentroDeCasilla(0.5f);
            float giro = Random.value < 0.5f ? 0f : 90f;
            int altura = Random.Range(1, 4);

            float y = 0f;
            for (int piso = 0; piso < altura; piso++)
            {
                string modelo = Random.value < 0.5f ? "Container_Green" : "Container_Red";
                Vector3 posicion = baseColumna + Vector3.up * y;

                GameObject puesto = ColocarModelo(CarpetaEntorno + modelo + ".fbx", posicion, giro, padre, true, true);
                if (puesto == null) break;

                //el siguiente va justo encima del que se acaba de poner
                Bounds limites;
                if (!CalcularLimites(puesto, out limites)) break;
                y += limites.size.y;
            }
        }
    }

    //farolas y semaforos en los cruces
    static void ColocarFarolas(Transform padre, int mitad)
    {
        for (int x = -mitad; x <= mitad; x++)
        {
            for (int z = -mitad; z <= mitad; z++)
            {
                bool esCruce = EsCalle(x, z) && EsCalle(x + 1, z) && EsCalle(x - 1, z)
                                             && EsCalle(x, z + 1) && EsCalle(x, z - 1);
                if (!esCruce) continue;

                Vector3 centro = PosicionDeCasilla(x, z);

                string semaforo = Random.value < 0.5f ? "TrafficLight_1" : "TrafficLight_2";
                Vector3 esquina = centro + new Vector3(tamanoCasilla * 0.4f, 0f, tamanoCasilla * 0.4f);
                ColocarModelo(CarpetaEntorno + semaforo + ".fbx", esquina, Random.Range(0f, 360f), padre, true, true);

                if (Random.value < 0.7f)
                {
                    Vector3 farola = centro + new Vector3(-tamanoCasilla * 0.4f, 0f, -tamanoCasilla * 0.4f);
                    ColocarModelo(CarpetaEntorno + "StreetLights.fbx", farola, Random.Range(0f, 360f), padre, true, true);
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
                if (!EsCalle(x, z)) continue;

                bool vertical = EsCalle(x, z + 1) && EsCalle(x, z - 1);
                bool horizontal = EsCalle(x + 1, z) && EsCalle(x - 1, z);

                //solo en tramos rectos, no en los cruces
                if (vertical == horizontal) continue;
                if (Random.value > 0.3f) continue;

                Vector3 centro = PosicionDeCasilla(x, z);
                if (Vector3.Distance(centro, PosicionDeSpawn()) < tamanoCasilla * 1.5f) continue;

                float ladoDeLaCalle = (Random.value < 0.5f ? -1f : 1f) * tamanoCasilla * 0.22f;
                Vector3 posicion = vertical
                    ? centro + new Vector3(ladoDeLaCalle, 0f, 0f)
                    : centro + new Vector3(0f, 0f, ladoDeLaCalle);

                float rotacion = vertical ? 0f : 90f;
                //uno de cada cinco esta atravesado, como abandonado de golpe
                if (Random.value < 0.2f) rotacion += Random.Range(20f, 50f);

                string modelo = ModelosVehiculo[Random.Range(0, ModelosVehiculo.Length)];
                ColocarModelo(CarpetaVehiculos + modelo + ".fbx", posicion, rotacion, padre, true, true);
            }
        }
    }

    //punto de interes: calle cortada con coches cruzados y barreras
    static void ColocarBarricada(Transform vehiculos, Transform detalles)
    {
        Vector3 centro = PosicionDeCasilla(-2, 0);

        ColocarModelo(CarpetaVehiculos + "Vehicle_Truck_Armored.fbx", centro, 75f, vehiculos, true, true);
        ColocarModelo(CarpetaVehiculos + "Vehicle_Pickup_Armored.fbx",
                      centro + new Vector3(0f, 0f, tamanoCasilla * 0.45f), 100f, vehiculos, true, true);

        for (int i = 0; i < 5; i++)
        {
            float desplazamiento = (i - 2f) * (tamanoCasilla * 0.16f);
            Vector3 posicion = centro + new Vector3(tamanoCasilla * 0.35f, 0f, desplazamiento);
            string barrera = i % 2 == 0 ? "TrafficBarrier_1" : "PlasticBarrier";
            ColocarModelo(CarpetaEntorno + barrera + ".fbx", posicion, 0f, detalles, true, true);
        }
    }

    static void ColocarManchas(Transform padre, int mitad)
    {
        for (int i = 0; i < 35; i++)
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

    static Vector3 PosicionDeCasilla(int x, int z)
    {
        return new Vector3(x * tamanoCasilla, 0f, z * tamanoCasilla);
    }

    static Vector3 DesplazamientoDentroDeCasilla(float margen)
    {
        float alcance = tamanoCasilla * 0.5f * margen;
        return new Vector3(Random.Range(-alcance, alcance), 0f, Random.Range(-alcance, alcance));
    }

    //el jugador aparece en un cruce, para que vea las calles desde el primer momento
    static Vector3 PosicionDeSpawn()
    {
        //con 13 casillas y paso 4, las lineas de calle caen en -6, -2, 2 y 6
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

        //se mide con la rotacion que trae del FBX, que es la que ya lo deja acostado
        GameObject temporal = (GameObject)PrefabUtility.InstantiatePrefab(asset);

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

        //El modelo va dentro de un envoltorio: el modelo conserva la rotacion del FBX y
        //el envoltorio lleva la rotacion del mapa. Asi la caja de colision se calcula sobre
        //un objeto sin rotar y sale ajustada en vez de hinchada.
        GameObject contenedor = new GameObject(asset.name);
        contenedor.transform.SetParent(padre);
        contenedor.transform.position = posicion;
        contenedor.transform.rotation = Quaternion.identity;

        GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset);

        //igual que con los personajes: si sigue ligado al prefab, lo que se le cambie
        //por script no se guarda y se pierde al dar Play
        PrefabUtility.UnpackPrefabInstance(instancia, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        //Los FBX vienen de Blender con Z hacia arriba y ya traen un giro de -90 en X que
        //los pone de pie en Unity. En los props de una pieza ese giro esta en la raiz, pero
        //en los de varias (los coches: carroceria y ruedas) esta en cada hijo. Si aqui se
        //forzara otro -90, los coches quedarian parados sobre la trompa. Se deja el del FBX.
        instancia.transform.SetParent(contenedor.transform);
        instancia.transform.localPosition = Vector3.zero;
        instancia.transform.localRotation = asset.transform.localRotation;

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
        disparo.sonidoSuero = CargarAudio("maro-jump-sound-effect_1.mp3");
        disparo.materialTrazo = ObtenerMaterialTrazo();
        disparo.prefabProyectil = ObtenerPrefabProyectil();
        //el trazo sale de la punta del arma que quedo equipada
        disparo.puntoDisparo = arma;

        CuerpoACuerpo cuchillo = jugador.AddComponent<CuerpoACuerpo>();
        cuchillo.fuenteAudio = audio;
        cuchillo.sonidoGolpe = CargarAudio("bonk-meme.mp3");
        cuchillo.armaDeFuego = arma;
        //el cuchillo vive en la mano del modelo igual que la pistola
        cuchillo.armaCuerpoACuerpo = BuscarHijo(jugador.transform, ArmaCuerpoACuerpo);

        return jugador;
    }

    static void CrearNpcs()
    {
        GameObject contenedor = new GameObject("NPCs");
        Undo.RegisterCreatedObjectUndo(contenedor, "Generar Mapa");

        Material material = ObtenerMaterial("mat_zombie_mapa", new Color(0.45f, 0.6f, 0.35f));
        string[] modelos = ConfiguradorModelos.ModelosZombie;
        string[] modelosAliado = ConfiguradorModelos.ModelosAliado;

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

            //el sobreviviente que aparece al curarlo; esta apagado hasta entonces.
            //Sin arma visible: los aliados pelean a puñetazos.
            string rutaAliado = modelosAliado[i % modelosAliado.Length];
            float alturaAliado;
            CrearVisual(npc.transform, rutaAliado, material, out alturaAliado, "VisualCurado", "");
            Transform visualCurado = npc.transform.Find("VisualCurado");
            visualCurado.gameObject.SetActive(false);

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
            script.visualCurado = visualCurado;
            script.nombreCurado = System.IO.Path.GetFileNameWithoutExtension(rutaAliado).Replace("Characters_", "");
            script.sonidoCurado = CargarAudio("metal-slug-thank-you-tenkiu.mp3");
        }
    }

    //Perros zombi. No se curan (el suero es para personas) y compensan al sigilo:
    //oyen mucho mas lejos y corren mas que los zombies humanos.
    static void CrearPerros()
    {
        GameObject contenedor = new GameObject("Perros");
        Undo.RegisterCreatedObjectUndo(contenedor, "Generar Mapa");

        Material material = ObtenerMaterial("mat_perro_mapa", new Color(0.5f, 0.45f, 0.3f));
        string[] modelos = ConfiguradorModelos.ModelosPerro;

        for (int i = 0; i < CantidadPerros; i++)
        {
            float angulo = (360f / CantidadPerros) * i + 45f;
            float radio = tamanoMapa * 0.38f;
            Vector3 posicion = Quaternion.Euler(0f, angulo, 0f) * new Vector3(0f, 0f, radio);
            posicion.y = 0.1f;

            string rutaModelo = modelos[i % modelos.Length];
            string nombreCorto = System.IO.Path.GetFileNameWithoutExtension(rutaModelo).Replace("Characters_", "");

            GameObject perro = new GameObject("Perro " + nombreCorto + " " + (i + 1));
            perro.transform.SetParent(contenedor.transform);
            perro.transform.position = posicion;

            float altura;
            CrearVisual(perro.transform, rutaModelo, material, out altura, "Visual", "");
            AgregarControlador(perro, altura);

            AudioSource audio = perro.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.maxDistance = 35f;

            Vida vida = perro.AddComponent<Vida>();
            //aguantan menos que un zombie, pero cuesta acertarles
            vida.vidaMaxima = 2;
            vida.tiempoDeGracia = 0.05f;
            vida.fuenteAudio = audio;
            vida.sonidoGolpe = CargarAudio("morrigan-hurt-2.mp3");
            vida.sonidoMuerte = CargarAudio("sm64-mario-pain.mp3");

            Npc script = perro.AddComponent<Npc>();
            script.nombre = nombreCorto;
            script.esEnemigo = true;
            script.sePuedeCurar = false;

            //mas rapidos que un humano y con mucho mejor oido
            script.velocidad = 2f;
            script.velocidadPersecucion = 6f;
            script.velocidadInvestigando = 4f;
            script.radioDeteccion = 14f;
            script.radioPerdida = 22f;
            script.tiempoParaOlvidar = 7f;
            script.distanciaAtaque = 1.4f;
            script.tiempoEntreAtaques = 1f;
            script.radioPaseo = 12f;
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
        return CrearVisual(padre, rutaModelo, material, out alturaModelo, "Visual", ArmaEquipada);
    }

    //nombreHijo permite tener dos modelos en el mismo personaje (zombie y curado);
    //con arma vacia se apagan todas las armas del modelo
    static Transform CrearVisual(Transform padre, string rutaModelo, Material material, out float alturaModelo,
                                 string nombreHijo, string arma)
    {
        alturaModelo = AlturaPersonajeSinModelo;

        GameObject visual = new GameObject(nombreHijo);
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
        Transform armaEquipada = EquiparArma(instancia, arma);

        if (PintarConColorPlano)
        {
            PintarPersonaje(instancia, material);
        }

        Animator animador = instancia.GetComponentInChildren<Animator>();
        if (animador == null)
        {
            Debug.LogWarning("El modelo " + rutaModelo + " no trae Animator.");
            return armaEquipada;
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

        return armaEquipada;
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

    // ---------- mision ----------

    //Piezas del vehiculo, dosis de suero repartidas y la camioneta para escapar
    static void CrearMision(Transform mapa)
    {
        Transform mision = CrearContenedor("Mision", mapa);
        Transform ciudad = mapa.Find("Ciudad");

        //otra semilla fija para que el suero caiga siempre en los mismos sitios
        Random.InitState(777);

        CrearVehiculoEscape(mision, ciudad);

        for (int i = 0; i < ModelosPieza.Length; i++)
        {
            Vector2Int casilla = CasillasPieza[i % CasillasPieza.Length];
            Vector3 posicion = PosicionDeCasilla(casilla.x, casilla.y);
            LiberarZona(ciudad, posicion, tamanoCasilla * 0.4f);

            CrearRecogible(mision, "Pieza - " + NombresPieza[i], posicion, Recogible.Tipo.Pieza, NombresPieza[i],
                           CarpetaEntorno + ModelosPieza[i] + ".fbx", new Color(1f, 0.8f, 0.3f));
        }

        int mitad = CasillasPorLado / 2;
        int creados = 0;
        for (int intento = 0; intento < 200 && creados < CantidadSueros; intento++)
        {
            int x = Random.Range(-mitad, mitad + 1);
            int z = Random.Range(-mitad, mitad + 1);
            if (EsLineaDeCalle(x, mitad) || EsLineaDeCalle(z, mitad)) continue;

            Vector3 posicion = PosicionDeCasilla(x, z) + DesplazamientoDentroDeCasilla(0.4f);
            if (Vector3.Distance(posicion, PosicionDeSpawn()) < tamanoCasilla * 1.5f) continue;

            LiberarZona(ciudad, posicion, 1.2f);
            CrearRecogible(mision, "Suero " + (creados + 1), posicion, Recogible.Tipo.Suero, "Suero", null,
                           new Color(0.35f, 1f, 0.45f));
            creados++;
        }
    }

    static void CrearVehiculoEscape(Transform padre, Transform ciudad)
    {
        Vector3 posicion = PosicionDeCasilla(CasillaVehiculo.x, CasillaVehiculo.y);

        //quita los coches aparcados que hayan caido en el mismo tramo de calle
        LiberarZona(ciudad, posicion, tamanoCasilla * 0.9f);

        bool calleVertical = EsLineaDeCalle(CasillaVehiculo.x, CasillasPorLado / 2);
        GameObject vehiculo = ColocarModelo(CarpetaVehiculos + ModeloVehiculoEscape + ".fbx", posicion,
                                            calleVertical ? 0f : 90f, padre, true, true);
        if (vehiculo == null)
        {
            vehiculo = new GameObject("Vehiculo de escape");
            vehiculo.transform.SetParent(padre);
            vehiculo.transform.position = posicion;
        }
        vehiculo.name = "Vehiculo de escape";

        AudioSource audio = vehiculo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.maxDistance = 35f;

        VehiculoEscape escape = vehiculo.AddComponent<VehiculoEscape>();
        escape.fuenteAudio = audio;
        escape.sonidoReparacion = CargarAudio("rico_bounce_fire_01.ogg");

        //faro amarillo encima para encontrarla desde lejos
        CrearLuzMarcador(vehiculo.transform, Vector3.up * 3.5f, new Color(1f, 0.75f, 0.2f), 10f, 3f);
    }

    //Objeto flotante que se recoge al pasar. Si no hay modelo se usa una capsula
    //brillante, que es lo que llevan las dosis de suero.
    static void CrearRecogible(Transform padre, string nombreObjeto, Vector3 posicion, Recogible.Tipo tipo,
                               string nombre, string rutaModelo, Color color)
    {
        GameObject raiz = new GameObject(nombreObjeto);
        raiz.transform.SetParent(padre);
        raiz.transform.position = posicion;

        GameObject modelo = rutaModelo != null ? ColocarModelo(rutaModelo, posicion, 0f, raiz.transform, true, false) : null;
        if (modelo == null)
        {
            modelo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            modelo.name = "Frasco";
            Object.DestroyImmediate(modelo.GetComponent<Collider>());
            modelo.transform.SetParent(raiz.transform);
            modelo.transform.localScale = new Vector3(0.25f, 0.2f, 0.25f);
            PintarObjeto(modelo, ObtenerMaterialBrillante("mat_suero_mapa", color));
        }
        else
        {
            AjustarTamano(modelo, 0.9f);
        }

        //flota un poco por encima del suelo
        modelo.transform.localPosition = Vector3.up * 0.7f;
        CrearLuzMarcador(raiz.transform, Vector3.up * 1.2f, color, 4f, 2f);

        Recogible recogible = raiz.AddComponent<Recogible>();
        recogible.tipo = tipo;
        recogible.nombre = nombre;
        recogible.modelo = modelo.transform;
        recogible.sonido = tipo == Recogible.Tipo.Suero ? CargarAudio("maro-jump-sound-effect_1.mp3") : null;
    }

    //escala el modelo para que su lado mas largo mida lo indicado
    static void AjustarTamano(GameObject objeto, float tamano)
    {
        Bounds limites;
        if (!CalcularLimites(objeto, out limites)) return;

        float mayor = Mathf.Max(limites.size.x, Mathf.Max(limites.size.y, limites.size.z));
        if (mayor > 0.01f) objeto.transform.localScale *= tamano / mayor;
    }

    static void CrearLuzMarcador(Transform padre, Vector3 posicionLocal, Color color, float alcance, float intensidad)
    {
        GameObject objeto = new GameObject("Luz");
        objeto.transform.SetParent(padre, false);
        objeto.transform.localPosition = posicionLocal;

        Light luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.range = alcance;
        luz.intensity = intensidad;
    }

    //borra los props y coches que haya cerca de un punto, para que no tapen la mision
    static void LiberarZona(Transform ciudad, Vector3 centro, float radio)
    {
        if (ciudad == null) return;

        foreach (string grupo in new[] { "Manzanas", "Vehiculos", "Detalles" })
        {
            Transform contenedor = ciudad.Find(grupo);
            if (contenedor == null) continue;

            for (int i = contenedor.childCount - 1; i >= 0; i--)
            {
                Transform hijo = contenedor.GetChild(i);
                Vector3 diferencia = hijo.position - centro;
                diferencia.y = 0f;

                //las manchas de sangre se quedan, no estorban
                if (diferencia.magnitude < radio && !hijo.name.StartsWith("Blood"))
                {
                    Object.DestroyImmediate(hijo.gameObject);
                }
            }
        }
    }

    //LoadScene solo carga escenas que esten en Build Settings; sin esto no se puede reiniciar
    static void AgregarEscenaABuild()
    {
        string ruta = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(ruta))
        {
            Debug.LogWarning("Guarda la escena y vuelve a generar el mapa para poder reiniciar la partida con R.");
            return;
        }

        foreach (EditorBuildSettingsScene escena in EditorBuildSettings.scenes)
        {
            if (escena.path == ruta) return;
        }

        var escenas = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        escenas.Insert(0, new EditorBuildSettingsScene(ruta, true));
        EditorBuildSettings.scenes = escenas.ToArray();
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

        AudioSource audio = objeto.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        ControlPartida control = objeto.AddComponent<ControlPartida>();
        control.fuenteAudio = audio;
        control.sonidoPieza = CargarAudio("Super Mario Bros. - Coin Sound Effect.mp3");
        control.sonidoVictoria = CargarAudio("plants-vs-zombies-victory-jingle.mp3");
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
        CrearBarraDeRuido(objetoCanvas.transform, interfaz);
        CrearPanelMision(objetoCanvas.transform, interfaz);
        CrearPanelFinal(objetoCanvas.transform, interfaz);
    }

    //barra delgada debajo del contador: se llena con cada ruido del jugador
    static void CrearBarraDeRuido(Transform padre, InterfazJuego interfaz)
    {
        Text etiqueta = CrearTexto("Etiqueta Ruido", padre, 22, TextAnchor.MiddleLeft);
        AnclarArribaIzquierda(etiqueta.rectTransform, new Vector2(40f, -136f), new Vector2(90f, 22f));
        etiqueta.text = "Ruido";

        Image fondo = CrearImagen("Barra Ruido", padre, new Color(0f, 0f, 0f, 0.55f));
        AnclarArribaIzquierda(fondo.rectTransform, new Vector2(130f, -138f), new Vector2(330f, 18f));

        Image relleno = CrearImagen("Relleno", fondo.transform, interfaz.colorSilencio);
        EstirarACompleto(relleno.rectTransform, 3f);
        //empieza vacia
        relleno.rectTransform.anchorMax = new Vector2(0f, 1f);

        Text postura = CrearTexto("Texto Postura", padre, 22, TextAnchor.UpperLeft);
        AnclarArribaIzquierda(postura.rectTransform, new Vector2(40f, -166f), new Vector2(420f, 30f));

        interfaz.rellenoRuido = relleno;
        interfaz.textoPostura = postura;
    }

    //arriba a la derecha: piezas, aliados y municion; arriba al centro los avisos;
    //abajo al centro la instruccion del vehiculo
    static void CrearPanelMision(Transform padre, InterfazJuego interfaz)
    {
        interfaz.textoPiezas = CrearTexto("Texto Piezas", padre, 28, TextAnchor.UpperRight);
        AnclarArribaDerecha(interfaz.textoPiezas.rectTransform, new Vector2(-40f, -40f), new Vector2(420f, 36f));

        interfaz.textoAliados = CrearTexto("Texto Aliados", padre, 26, TextAnchor.UpperRight);
        AnclarArribaDerecha(interfaz.textoAliados.rectTransform, new Vector2(-40f, -80f), new Vector2(420f, 34f));

        interfaz.textoMunicion = CrearTexto("Texto Municion", padre, 24, TextAnchor.UpperRight);
        AnclarArribaDerecha(interfaz.textoMunicion.rectTransform, new Vector2(-40f, -124f), new Vector2(420f, 100f));

        interfaz.textoAviso = CrearTexto("Texto Aviso", padre, 30, TextAnchor.UpperCenter);
        RectTransform aviso = interfaz.textoAviso.rectTransform;
        aviso.anchorMin = new Vector2(0.5f, 1f);
        aviso.anchorMax = new Vector2(0.5f, 1f);
        aviso.pivot = new Vector2(0.5f, 1f);
        aviso.anchoredPosition = new Vector2(0f, -110f);
        aviso.sizeDelta = new Vector2(1000f, 44f);
        AgregarSombra(interfaz.textoAviso);

        interfaz.textoInstruccion = CrearTexto("Texto Instruccion", padre, 28, TextAnchor.LowerCenter);
        RectTransform instruccion = interfaz.textoInstruccion.rectTransform;
        instruccion.anchorMin = new Vector2(0.5f, 0f);
        instruccion.anchorMax = new Vector2(0.5f, 0f);
        instruccion.pivot = new Vector2(0.5f, 0f);
        instruccion.anchoredPosition = new Vector2(0f, 80f);
        instruccion.sizeDelta = new Vector2(1200f, 44f);
        AgregarSombra(interfaz.textoInstruccion);
    }

    //pantalla oscura con el resumen; empieza apagada
    static void CrearPanelFinal(Transform padre, InterfazJuego interfaz)
    {
        Image fondo = CrearImagen("Panel Final", padre, new Color(0f, 0f, 0f, 0.8f));
        EstirarACompleto(fondo.rectTransform, 0f);

        Text texto = CrearTexto("Texto Final", fondo.transform, 40, TextAnchor.MiddleCenter);
        EstirarACompleto(texto.rectTransform, 0f);

        interfaz.panelFinal = fondo.gameObject;
        interfaz.textoFinal = texto;
        fondo.gameObject.SetActive(false);
    }

    static void AgregarSombra(Text texto)
    {
        Outline borde = texto.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(0f, 0f, 0f, 0.8f);
        borde.effectDistance = new Vector2(2f, -2f);
    }

    static void AnclarArribaDerecha(RectTransform rect, Vector2 posicion, Vector2 tamano)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = tamano;
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
        texto.text = "Zombies eliminados: 0";
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

    //material con emision, para objetos que tienen que verse de lejos
    static Material ObtenerMaterialBrillante(string nombre, Color color)
    {
        AsegurarCarpetaMateriales();

        string ruta = CarpetaMateriales + "/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            AssetDatabase.CreateAsset(material, ruta);
        }

        return material;
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
