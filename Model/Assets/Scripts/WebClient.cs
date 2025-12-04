using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// =============================================================
//  CLASES PARA DESERIALIZAR JSON
// =============================================================

[System.Serializable]
public class AgentState {
    public int id;
    public float x;
    public float y;
    public float z;
    public bool has_civil;
}

[System.Serializable]
public class SimplePos {
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class WorldState {
    public List<AgentState> agents;
    public List<SimplePos> fires;
    public List<SimplePos> smokes;
    public List<SimplePos> bases;
    public List<SimplePos> civils;
    public List<SimplePos> pois;
    public bool finished;
    public string outcome;
    public string end_reason;
    public string end_code;

    public int steps;
    public int civiles_rescatados;
    public int civiles_perdidos;
    public int puntos_dano;
    public int fuegos_activos;
    public int humos_activos;
    public int pois_restantes;
}

// =============================================================
//  WEBCLIENT PRINCIPAL
// =============================================================

public class WebClient : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject agentPrefab;
    public GameObject firePrefab;
    public GameObject smokePrefab;
    public GameObject basePrefab;
    public GameObject civilPrefab;
    public GameObject poiPrefab;

    [Header("Agent Carry Visual")]
    public GameObject carriedCivilPrefab;

    [Header("Grid Mapping")]
    public Vector3 gridOrigin = Vector3.zero;
    public float cellSize = 1f;
    public bool invertZ = false;
    public int gridWidth = 8;
    public int gridHeight = 6;
    // Girar la simulación 90° a la derecha (clockwise)
    public bool rotate90Right = true;

    [Header("Y Offsets por tipo")]
    public float baseYOffset = 0f;

    // Diccionarios por tipo
    Dictionary<int, GameObject> agentsGO = new Dictionary<int, GameObject>();
    Dictionary<string, GameObject> firesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> smokesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> basesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> civilsGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> poisGO = new Dictionary<string, GameObject>();
    
    Dictionary<int, GameObject> carriedCivilsGO = new Dictionary<int, GameObject>();

    bool simulationFinished = false;


    // =============================================================
    //  ENVÍO AL SERVIDOR
    // =============================================================

    IEnumerator SendData(string data)
    {
        string url = "http://localhost:8585";

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(data);

            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            // Nuevo sistema de Input System (compatible)
            if (www.result == UnityWebRequest.Result.ConnectionError ||
                www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log("Error: " + www.error);
            }
            else
            {
                string json = www.downloadHandler.text;
                Debug.Log("Respuesta Python: " + json);

                WorldState world = JsonUtility.FromJson<WorldState>(json);
                UpdateWorld(world);
            }
        }
    }

    // =============================================================
    //  ACTUALIZAR ESCENA COMPLETA
    // =============================================================

    void UpdateWorld(WorldState world)
    {
        if (world == null)
        {
            Debug.LogError("❌ WorldState es null. Revisa el JSON que envía el servidor.");
            return;
        }

        // Log rápido para ver qué viene null
        Debug.Log(
            $"World recibida -> " +
            $"agents: {(world.agents == null ? "null" : world.agents.Count.ToString())}, " +
            $"fires: {(world.fires == null ? "null" : world.fires.Count.ToString())}, " +
            $"smokes: {(world.smokes == null ? "null" : world.smokes.Count.ToString())}, " +
            $"bases: {(world.bases == null ? "null" : world.bases.Count.ToString())}, " +
            $"civils: {(world.civils == null ? "null" : world.civils.Count.ToString())}, " +
            $"pois: {(world.pois == null ? "null" : world.pois.Count.ToString())}"
        );

        // Solo actualizamos si la lista NO es null
        if (world.agents != null)  UpdateAgents(world.agents);
        if (world.fires != null)   UpdateFires(world.fires);
        if (world.smokes != null)  UpdateSmokes(world.smokes);
        if (world.bases != null)   UpdateBases(world.bases);
        if (world.civils != null)  UpdateCivils(world.civils);
        if (world.pois != null)    UpdatePOIs(world.pois);

        // ====== LOG POR CADA STEP ======
        Debug.Log(
            $"[STEP {world.steps}] " +
            $"Rescatados: {world.civiles_rescatados} | " +
            $"Perdidos: {world.civiles_perdidos} | " +
            $"Daño: {world.puntos_dano} | " +
            $"Fuego: {world.fuegos_activos} | " +
            $"Humo: {world.humos_activos} | " +
            $"POIs activos: {world.pois_restantes}"
        );

        // Si aún no termina, no intentamos leer stats de fin
        if (!world.finished)
            return;

        if (simulationFinished)
            return;

        simulationFinished = true;

        // Mensaje según el tipo de final
        if (world.outcome == "win")
        {
            Debug.Log("🎉 VICTORIA: " + (world.end_reason ?? "Se cumplió la condición de victoria."));
        }
        else if (world.outcome == "lose")
        {
            string msg;

            if (world.end_code == "lose_civiles")
                msg = "Perdiste: se perdieron 4 civiles.";
            else if (world.end_code == "lose_colapso")
                msg = "Perdiste: el edificio colapsó (25+ puntos de daño).";
            else
                msg = "Perdiste.";

            Debug.Log("💀 " + msg + " Detalle: " + (world.end_reason ?? ""));
        }
        else
        {
            Debug.Log("Simulación terminada (sin outcome definido).");
        }

        // Estadísticas (cada campo puede venir en 0 si no lo manda el server)
        Debug.Log(
            "===== RESUMEN DE LA SIMULACIÓN =====\n" +
            $"Pasos totales: {world.steps}\n" +
            $"Civiles rescatados: {world.civiles_rescatados}\n" +
            $"Civiles perdidos: {world.civiles_perdidos}\n" +
            $"Puntos de daño al edificio: {world.puntos_dano}\n" +
            $"Celdas con fuego al final: {world.fuegos_activos}\n" +
            $"Celdas con humo al final: {world.humos_activos}\n" +
            $"POI restantes sin resolver: {world.pois_restantes}"
        );
    }


    // =============================================================
    // CONVERSIÓN DE GRID → UNITY
    // =============================================================

    Vector3 GridToWorld(float gx, float gy, float gz)
    {
        // 1) Aplicar rotación 90° a la derecha si está activada
        float rx = gx;
        float rz = gz;

        if (rotate90Right)
        {
            // Rotación 90° clockwis alrededor del grid:
            // (x,z) -> (z, gridWidth - 1 - x)
            float oldX = rx;
            float oldZ = rz;
            rx = gridHeight - 1 - oldZ;
            rz = oldX;
        }

        // 2) Invertir Z si se pide (aunque ahora lo tienes en false)
        float finalZ = invertZ ? (gridHeight - 1 - rz) : rz;

        // 3) Pasar a coordenadas de mundo
        float worldX = gridOrigin.x + rx * cellSize;
        float worldZ = gridOrigin.z + finalZ * cellSize;
        float worldY = gridOrigin.y + gy;

        return new Vector3(worldX, worldY, worldZ);
    }


    string Key(float x, float z)
    {
        return $"{x}_{z}";
    }

    // =============================================================
    // ACTUALIZACIÓN DE BOMBEROS
    // =============================================================

    void UpdateAgents(List<AgentState> agents)
    {
        foreach (var a in agents)
        {
            GameObject go;

            if (!agentsGO.ContainsKey(a.id))
            {
                go = Instantiate(agentPrefab, transform);
                go.name = "Agent_" + a.id;
                agentsGO[a.id] = go;
            }
            else
            {
                go = agentsGO[a.id];
            }

            // Posición del agente
            go.transform.position = GridToWorld(a.x, a.y, a.z);

            // ---------- Civil cargado visual ----------
            if (a.has_civil)
            {
                // Si no existe aún el civil cargado para este agente, crearlo
                if (!carriedCivilsGO.ContainsKey(a.id))
                {
                    if (carriedCivilPrefab != null)
                    {
                        GameObject civGO = Instantiate(carriedCivilPrefab, go.transform);
                        carriedCivilsGO[a.id] = civGO;

                        // posicionar al lado del agente (offset local)
                        civGO.transform.localPosition = new Vector3(-0.6f, 0f, -1f); 
                        // puedes ajustar el 0.3f para izquierda/derecha/distancia
                    }
                }
                else
                {
                    // ya existe, asegúrate de que siga como hijo
                    GameObject civGO = carriedCivilsGO[a.id];
                    if (civGO != null && civGO.transform.parent != go.transform)
                    {
                        civGO.transform.SetParent(go.transform);
                        civGO.transform.localPosition = new Vector3(0.3f, 0f, 0f);
                    }
                }
            }
            else
            {
                // Ya no tiene civil: destruir el modelo “pegado”
                if (carriedCivilsGO.ContainsKey(a.id))
                {
                    GameObject civGO = carriedCivilsGO[a.id];
                    if (civGO != null)
                        Destroy(civGO);

                    carriedCivilsGO.Remove(a.id);
                }
            }
        }
    }


    // =============================================================
    // FUEGO
    // =============================================================

    void UpdateFires(List<SimplePos> fires)
    {
        HashSet<string> alive = new HashSet<string>();

        foreach (var f in fires)
        {
            string k = Key(f.x, f.z);
            alive.Add(k);

            GameObject go;
            if (!firesGO.ContainsKey(k))
            {
                go = Instantiate(firePrefab, transform);
                firesGO[k] = go;
            }
            else
            {
                go = firesGO[k];
            }

            go.transform.position = GridToWorld(f.x, f.y, f.z);
        }

        // destruir fuegos que ya no existen
        List<string> remove = new List<string>();
        foreach (var kv in firesGO)
            if (!alive.Contains(kv.Key))
                remove.Add(kv.Key);

        foreach (var k in remove)
        {
            Destroy(firesGO[k]);
            firesGO.Remove(k);
        }
    }

    // =============================================================
    // HUMO
    // =============================================================

    void UpdateSmokes(List<SimplePos> smokes)
    {
        HashSet<string> alive = new HashSet<string>();

        foreach (var s in smokes)
        {
            string k = Key(s.x, s.z);
            alive.Add(k);

            GameObject go;
            if (!smokesGO.ContainsKey(k))
            {
                go = Instantiate(smokePrefab, transform);
                smokesGO[k] = go;
            }
            else
            {
                go = smokesGO[k];
            }

            go.transform.position = GridToWorld(s.x, s.y, s.z);
        }

        List<string> remove = new List<string>();
        foreach (var kv in smokesGO)
            if (!alive.Contains(kv.Key))
                remove.Add(kv.Key);

        foreach (var k in remove)
        {
            Destroy(smokesGO[k]);
            smokesGO.Remove(k);
        }
    }

    // =============================================================
    // BASES (pueden permanecer estáticas)
    // =============================================================

    void UpdateBases(List<SimplePos> bases)
    {
        foreach (var b in bases)
        {
            string k = Key(b.x, b.z);

            if (!basesGO.ContainsKey(k))
            {
                GameObject go = Instantiate(basePrefab, transform);
                go.transform.position = GridToWorld(b.x, b.y, b.z) + new Vector3(0f, baseYOffset - 0.8f, 0f);

                basesGO[k] = go;
            }
        }
    }

    // =============================================================
    // CIVILES REVELADOS
    // =============================================================

    void UpdateCivils(List<SimplePos> civils)
    {
        HashSet<string> alive = new HashSet<string>();

        foreach (var c in civils)
        {
            string k = Key(c.x, c.z);
            alive.Add(k);

            GameObject go;
            if (!civilsGO.ContainsKey(k))
            {
                go = Instantiate(civilPrefab, transform);
                civilsGO[k] = go;
            }
            else
            {
                go = civilsGO[k];
            }

            go.transform.position = GridToWorld(c.x, c.y, c.z);
        }

        List<string> remove = new List<string>();
        foreach (var kv in civilsGO)
            if (!alive.Contains(kv.Key))
                remove.Add(kv.Key);

        foreach (var k in remove)
        {
            Destroy(civilsGO[k]);
            civilsGO.Remove(k);
        }
    }

    // =============================================================
    // PUNTOS DE INTERÉS (POIs)
    // =============================================================

    void UpdatePOIs(List<SimplePos> pois)
    {
        HashSet<string> alive = new HashSet<string>();

        foreach (var p in pois)
        {
            string k = Key(p.x, p.z);
            alive.Add(k);

            GameObject go;
            if (!poisGO.ContainsKey(k))
            {
                go = Instantiate(poiPrefab, transform);
                poisGO[k] = go;
            }
            else
            {
                go = poisGO[k];
            }

            go.transform.position = GridToWorld(p.x, p.y, p.z);
        }

        List<string> remove = new List<string>();
        foreach (var kv in poisGO)
            if (!alive.Contains(kv.Key))
                remove.Add(kv.Key);

        foreach (var k in remove)
        {
            Destroy(poisGO[k]);
            poisGO.Remove(k);
        }
    }

    // =============================================================
    // LLAMADO INICIAL
    // =============================================================

    void Start()
    {
        StartCoroutine(SendData("{\"command\":\"step\"}"));
    }

    // =============================================================
    // PRESIONAR ESPACIO = SIGUIENTE STEP
    // =============================================================

    void Update()
    {
    if (simulationFinished)
        return; // ya no mandar mas psos si termino
    
#if ENABLE_INPUT_SYSTEM
        // si usas el nuevo Input System
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
#else
        // si usas el sistema clásico o BOTH
        if (Input.GetKeyDown(KeyCode.Space))
#endif
        {
            StartCoroutine(SendData("{\"command\":\"step\"}"));
        }
    }
}
