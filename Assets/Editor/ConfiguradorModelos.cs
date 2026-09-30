using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

//Prepara los FBX de Assets/Modelos: les pone rig, marca que animaciones
//se repiten en bucle y arma un Animator Controller para cada personaje.
public static class ConfiguradorModelos
{
    public const string CarpetaModelos = "Assets/Modelos";
    public const string CarpetaControladores = "Assets/Animaciones";

    //textura compartida por los 60 modelos del kit
    public const string RutaAtlas = CarpetaModelos + "/Zombie_Atlas.png";
    public const string RutaMaterialAtlas = "Assets/Materiales/mat_zombie_atlas.mat";

    public const string ModeloJugador = CarpetaModelos + "/Characters_Sam.fbx";

    //los NPC van rotando entre estos modelos de zombie
    public static readonly string[] ModelosZombie =
    {
        CarpetaModelos + "/Zombie_Basic.fbx",
        CarpetaModelos + "/Zombie_Chubby.fbx",
        CarpetaModelos + "/Zombie_Arm.fbx",
        CarpetaModelos + "/Zombie_Ribcage.fbx",
    };

    //animaciones que deben repetirse mientras dure el estado; el resto se reproduce una vez
    static readonly string[] ClipsEnBucle = { "Idle", "Walk", "Run", "Crawl", "Eating" };

    [MenuItem("Practica1/1 - Configurar Modelos")]
    public static void ConfigurarTodo()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaModelos))
        {
            Debug.LogError("No existe la carpeta " + CarpetaModelos);
            return;
        }

        ConfigurarAtlas();

        //todos los FBX del kit (personajes, entorno, vehiculos y armas) usan el mismo atlas
        foreach (string ruta in RutasDeModelos(true))
        {
            AsignarMaterialAtlas(ruta);
        }

        //solo los personajes llevan rig y animaciones; los props no
        foreach (string ruta in RutasDeModelos(false))
        {
            ConfigurarImportacion(ruta);
        }

        CrearCarpetaControladores();

        CrearControladorJugador(ModeloJugador);
        foreach (string ruta in ModelosZombie)
        {
            CrearControladorEnemigo(ruta);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Modelos configurados y Animator Controllers creados en " + CarpetaControladores);
    }

    //incluirSubcarpetas=false devuelve solo los personajes, que estan en la raiz
    public static IEnumerable<string> RutasDeModelos(bool incluirSubcarpetas)
    {
        SearchOption busqueda = incluirSubcarpetas
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        foreach (string ruta in Directory.GetFiles(CarpetaModelos, "*.fbx", busqueda))
        {
            yield return ruta.Replace('\\', '/');
        }
    }

    //El atlas es una paleta de colores planos de 512x512: con filtrado bilineal los
    //colores se mezclan en los bordes de cada celda, asi que va en Point.
    static void ConfigurarAtlas()
    {
        TextureImporter importer = AssetImporter.GetAtPath(RutaAtlas) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("Falta la textura " + RutaAtlas + "; los modelos se veran blancos.");
            return;
        }

        if (importer.filterMode != FilterMode.Point || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    //Los FBX traen sus materiales sin textura. En vez de dejar que cada modelo cree el
    //suyo, se redirigen todos a materiales compartidos del proyecto.
    static void AsignarMaterialAtlas(string ruta)
    {
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) return;

        bool cambioAlguno = false;

        //el cuerpo de los modelos; algunos traen el atlas con el nombre duplicado
        cambioAlguno |= Remapear(importer, "Atlas", ObtenerMaterialAtlas());
        cambioAlguno |= Remapear(importer, "Atlas.001", ObtenerMaterialAtlas());

        //luces: van con emision para que se vean encendidas
        cambioAlguno |= Remapear(importer, "Light", ObtenerMaterialLuz("mat_luz_farola", new Color(1f, 0.93f, 0.7f), 1.6f));
        cambioAlguno |= Remapear(importer, "Headlights", ObtenerMaterialLuz("mat_luz_faros", new Color(1f, 0.97f, 0.85f), 1.4f));
        cambioAlguno |= Remapear(importer, "BrakeLight", ObtenerMaterialLuz("mat_luz_freno", new Color(1f, 0.2f, 0.15f), 1.2f));

        if (cambioAlguno)
        {
            importer.SaveAndReimport();
        }
    }

    //devuelve true si hubo que cambiar el remapeo
    static bool Remapear(ModelImporter importer, string nombreOriginal, Material material)
    {
        if (material == null) return false;

        AssetImporter.SourceAssetIdentifier identificador =
            new AssetImporter.SourceAssetIdentifier(typeof(Material), nombreOriginal);

        Object yaAsignado;
        if (importer.GetExternalObjectMap().TryGetValue(identificador, out yaAsignado) && yaAsignado == material)
        {
            return false;
        }

        importer.AddRemap(identificador, material);
        return true;
    }

    static Material ObtenerMaterialLuz(string nombre, Color color, float fuerza)
    {
        string ruta = "Assets/Materiales/" + nombre + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;

        if (!AssetDatabase.IsValidFolder("Assets/Materiales"))
        {
            AssetDatabase.CreateFolder("Assets", "Materiales");
        }

        material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * fuerza);
        AssetDatabase.CreateAsset(material, ruta);

        return material;
    }

    public static Material ObtenerMaterialAtlas()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialAtlas);
        if (material != null) return material;

        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(RutaAtlas);
        if (textura == null) return null;

        if (!AssetDatabase.IsValidFolder("Assets/Materiales"))
        {
            AssetDatabase.CreateFolder("Assets", "Materiales");
        }

        material = new Material(Shader.Find("Standard"));
        material.mainTexture = textura;
        //los modelos son de color plano, sin brillos metalicos
        material.SetFloat("_Glossiness", 0.1f);
        AssetDatabase.CreateAsset(material, RutaMaterialAtlas);

        return material;
    }

    //deja el FBX listo: rig generico, animaciones importadas y los bucles marcados
    public static void ConfigurarImportacion(string ruta)
    {
        //Primera pasada: el rig. Hay que reimportar antes de tocar los clips,
        //porque la lista de animaciones no existe hasta que el rig esta puesto.
        ModelImporter importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importer == null) return;

        bool cambioElRig = false;

        //rig generico: cada modelo trae sus propias animaciones, no hace falta retargeting humanoide
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            cambioElRig = true;
        }

        //Se revisa aparte del animationType a proposito: Unity ya importa estos FBX
        //como Generic pero sin Avatar, y sin Avatar no se reproduce ninguna animacion.
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            cambioElRig = true;
        }

        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            cambioElRig = true;
        }

        //hace falta para poder buscar los huesos por nombre desde scripts
        if (importer.optimizeGameObjects)
        {
            importer.optimizeGameObjects = false;
            cambioElRig = true;
        }

        if (cambioElRig)
        {
            importer.SaveAndReimport();
            //el importer viejo ya no sirve despues de reimportar
            importer = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (importer == null) return;
        }

        //Segunda pasada: marcar que animaciones se repiten en bucle
        ConfigurarBucles(importer);
    }

    static void ConfigurarBucles(ModelImporter importer)
    {
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0) return;

        bool cambioAlguno = false;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            bool debeRepetirse = EsClipEnBucle(clip.name);
            if (clip.loopTime != debeRepetirse)
            {
                clip.loopTime = debeRepetirse;
                cambioAlguno = true;
            }
        }

        if (cambioAlguno)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static bool EsClipEnBucle(string nombreClip)
    {
        string nombre = NombreCorto(nombreClip);
        foreach (string enBucle in ClipsEnBucle)
        {
            if (nombre.Contains(enBucle)) return true;
        }
        return false;
    }

    // ---------- Animator Controllers ----------

    //Jugador: mezcla las animaciones con pistola segun la velocidad, mas salto,
    //golpe y muerte. Usa las variantes _Gun porque va armado todo el rato.
    static void CrearControladorJugador(string rutaModelo)
    {
        AnimationClip[] clips = CargarClips(rutaModelo);
        if (clips.Length == 0)
        {
            Debug.LogWarning("Sin animaciones en " + rutaModelo);
            return;
        }

        AnimatorController controlador = CrearControladorVacio(NombreControlador(rutaModelo));
        controlador.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
        controlador.AddParameter("EnSuelo", AnimatorControllerParameterType.Bool);
        controlador.AddParameter("Saltar", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Golpe", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Morir", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Revivir", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        AnimatorState movimiento = CrearEstadoMovimiento(controlador, clips, true);
        maquina.defaultState = movimiento;

        AnimationClip clipSalto = BuscarClip(clips, "Jump_Idle", "Jump");
        if (clipSalto != null)
        {
            AnimatorState salto = maquina.AddState("Salto");
            salto.motion = clipSalto;

            AnimatorStateTransition aSalto = movimiento.AddTransition(salto);
            aSalto.hasExitTime = false;
            aSalto.duration = 0.05f;
            aSalto.AddCondition(AnimatorConditionMode.If, 0f, "Saltar");

            //vuelve al movimiento cuando ya avanzo un poco la animacion y toco el suelo
            AnimatorStateTransition aSuelo = salto.AddTransition(movimiento);
            aSuelo.hasExitTime = true;
            aSuelo.exitTime = 0.25f;
            aSuelo.duration = 0.15f;
            aSuelo.AddCondition(AnimatorConditionMode.If, 0f, "EnSuelo");
        }

        AgregarGolpeYMuerte(maquina, clips, movimiento);
        EditorUtility.SetDirty(controlador);
    }

    //Enemigo: movimiento, ataque cuando alcanza al jugador, golpe y muerte
    static void CrearControladorEnemigo(string rutaModelo)
    {
        AnimationClip[] clips = CargarClips(rutaModelo);
        if (clips.Length == 0)
        {
            Debug.LogWarning("Sin animaciones en " + rutaModelo);
            return;
        }

        AnimatorController controlador = CrearControladorVacio(NombreControlador(rutaModelo));
        controlador.AddParameter("Velocidad", AnimatorControllerParameterType.Float);
        controlador.AddParameter("Atacando", AnimatorControllerParameterType.Bool);
        controlador.AddParameter("Golpe", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Morir", AnimatorControllerParameterType.Trigger);
        controlador.AddParameter("Revivir", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;
        AnimatorState movimiento = CrearEstadoMovimiento(controlador, clips, false);
        maquina.defaultState = movimiento;

        //no todos los zombies traen animacion de ataque, por eso las alternativas
        AnimationClip clipAtaque = BuscarClip(clips, "Idle_Attack", "Run_Attack", "Punch", "Attack");
        if (clipAtaque != null)
        {
            AnimatorState ataque = maquina.AddState("Ataque");
            ataque.motion = clipAtaque;

            AnimatorStateTransition aAtaque = movimiento.AddTransition(ataque);
            aAtaque.hasExitTime = false;
            aAtaque.duration = 0.1f;
            aAtaque.AddCondition(AnimatorConditionMode.If, 0f, "Atacando");

            AnimatorStateTransition deAtaque = ataque.AddTransition(movimiento);
            deAtaque.hasExitTime = false;
            deAtaque.duration = 0.1f;
            deAtaque.AddCondition(AnimatorConditionMode.IfNot, 0f, "Atacando");
        }

        AgregarGolpeYMuerte(maquina, clips, movimiento);
        EditorUtility.SetDirty(controlador);
    }

    //Estados compartidos: recibir un golpe, morir y volver a la vida.
    //Se salen de la muerte solo con el trigger Revivir, que dispara el script al reaparecer.
    static void AgregarGolpeYMuerte(AnimatorStateMachine maquina, AnimationClip[] clips, AnimatorState movimiento)
    {
        AnimationClip clipGolpe = BuscarClip(clips, "HitReact");
        if (clipGolpe != null)
        {
            AnimatorState golpe = maquina.AddState("Golpe");
            golpe.motion = clipGolpe;

            //desde cualquier estado, para que reaccione aunque este atacando
            AnimatorStateTransition aGolpe = maquina.AddAnyStateTransition(golpe);
            aGolpe.hasExitTime = false;
            aGolpe.duration = 0.05f;
            aGolpe.canTransitionToSelf = false;
            aGolpe.AddCondition(AnimatorConditionMode.If, 0f, "Golpe");

            AnimatorStateTransition deGolpe = golpe.AddTransition(movimiento);
            deGolpe.hasExitTime = true;
            deGolpe.exitTime = 0.8f;
            deGolpe.duration = 0.1f;
        }

        AnimationClip clipMuerte = BuscarClip(clips, "Death");
        if (clipMuerte == null) return;

        AnimatorState muerte = maquina.AddState("Muerte");
        muerte.motion = clipMuerte;

        AnimatorStateTransition aMuerte = maquina.AddAnyStateTransition(muerte);
        aMuerte.hasExitTime = false;
        aMuerte.duration = 0.05f;
        aMuerte.canTransitionToSelf = false;
        aMuerte.AddCondition(AnimatorConditionMode.If, 0f, "Morir");

        //la unica salida de la muerte es reaparecer
        AnimatorStateTransition aRevivir = muerte.AddTransition(movimiento);
        aRevivir.hasExitTime = false;
        aRevivir.duration = 0.1f;
        aRevivir.AddCondition(AnimatorConditionMode.If, 0f, "Revivir");
    }

    //arbol que mezcla quieto -> caminar -> correr con el parametro Velocidad
    static AnimatorState CrearEstadoMovimiento(AnimatorController controlador, AnimationClip[] clips, bool conArma)
    {
        BlendTree arbol;
        AnimatorState estado = controlador.CreateBlendTreeInController("Movimiento", out arbol, 0);
        arbol.blendType = BlendTreeType.Simple1D;
        arbol.blendParameter = "Velocidad";
        arbol.useAutomaticThresholds = false;

        //con arma se prefieren las variantes _Gun, que dejan al personaje apuntando
        AnimationClip quieto = conArma ? BuscarClip(clips, "Idle_Gun", "Idle") : BuscarClip(clips, "Idle");
        AnimationClip caminar = conArma ? BuscarClip(clips, "Walk_Gun", "Walk") : BuscarClip(clips, "Walk");
        AnimationClip correr = conArma ? BuscarClip(clips, "Run_Gun", "Run") : BuscarClip(clips, "Run", "Crawl");

        //si al modelo le falta alguna, se rellena con la que si tenga
        if (quieto == null) quieto = caminar ?? correr;
        if (caminar == null) caminar = correr ?? quieto;
        if (correr == null) correr = caminar;

        if (quieto != null) arbol.AddChild(quieto, 0f);
        if (caminar != null) arbol.AddChild(caminar, 0.5f);
        if (correr != null) arbol.AddChild(correr, 1f);

        return estado;
    }

    // ---------- utilidades ----------

    static AnimatorController CrearControladorVacio(string nombre)
    {
        string ruta = CarpetaControladores + "/" + nombre + ".controller";
        AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);

        if (controlador == null)
        {
            return AnimatorController.CreateAnimatorControllerAtPath(ruta);
        }

        //Se reutiliza el asset en vez de borrarlo y crearlo de nuevo. Si se borra,
        //los Animator de la escena que ya esta abierta pierden la referencia y se
        //quedan sin controller, aunque el archivo se recree en la misma ruta.
        VaciarControlador(controlador, ruta);
        return controlador;
    }

    //deja el controller sin parametros ni estados, listo para volver a armarlo
    static void VaciarControlador(AnimatorController controlador, string ruta)
    {
        for (int i = controlador.parameters.Length - 1; i >= 0; i--)
        {
            controlador.RemoveParameter(i);
        }

        AnimatorStateMachine maquina = controlador.layers[0].stateMachine;

        AnimatorStateTransition[] desdeCualquiera = maquina.anyStateTransitions;
        for (int i = desdeCualquiera.Length - 1; i >= 0; i--)
        {
            maquina.RemoveAnyStateTransition(desdeCualquiera[i]);
        }

        //al quitar un estado se van tambien sus transiciones
        ChildAnimatorState[] estados = maquina.states;
        for (int i = estados.Length - 1; i >= 0; i--)
        {
            maquina.RemoveState(estados[i].state);
        }

        //los blend tree son sub-assets y no se van solos al quitar el estado
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            BlendTree arbol = sub as BlendTree;
            if (arbol != null) Object.DestroyImmediate(arbol, true);
        }
    }

    public static string NombreControlador(string rutaModelo)
    {
        return "Controlador_" + Path.GetFileNameWithoutExtension(rutaModelo);
    }

    //devuelve el Animator Controller ya creado para ese modelo
    public static RuntimeAnimatorController CargarControlador(string rutaModelo)
    {
        string ruta = CarpetaControladores + "/" + NombreControlador(rutaModelo) + ".controller";
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ruta);
    }

    //el Avatar tambien es un sub-asset del FBX; sin el, el Animator no reproduce nada
    public static Avatar CargarAvatar(string rutaModelo)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(rutaModelo))
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null) return avatar;
        }
        return null;
    }

    //las animaciones viven dentro del FBX como sub-assets
    static AnimationClip[] CargarClips(string rutaModelo)
    {
        List<AnimationClip> lista = new List<AnimationClip>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(rutaModelo))
        {
            AnimationClip clip = asset as AnimationClip;
            //Unity mete clips ocultos de vista previa que hay que ignorar
            if (clip != null && !clip.name.StartsWith("__preview__"))
            {
                lista.Add(clip);
            }
        }
        return lista.ToArray();
    }

    //busca por nombre exacto en orden de preferencia; los clips vienen como "CharacterArmature|Idle"
    static AnimationClip BuscarClip(AnimationClip[] clips, params string[] nombres)
    {
        foreach (string buscado in nombres)
        {
            foreach (AnimationClip clip in clips)
            {
                if (NombreCorto(clip.name) == buscado) return clip;
            }
        }
        return null;
    }

    static string NombreCorto(string nombreClip)
    {
        int barra = nombreClip.LastIndexOf('|');
        return barra >= 0 ? nombreClip.Substring(barra + 1) : nombreClip;
    }

    static void CrearCarpetaControladores()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaControladores))
        {
            AssetDatabase.CreateFolder("Assets", "Animaciones");
        }
    }
}
