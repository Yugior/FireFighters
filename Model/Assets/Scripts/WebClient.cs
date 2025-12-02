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

    [Header("Grid Mapping")]
    public Vector3 gridOrigin = Vector3.zero;
    public float cellSize = 1f;
    public bool invertZ = true;

    // Diccionarios por tipo
    Dictionary<int, GameObject> agentsGO = new Dictionary<int, GameObject>();
    Dictionary<string, GameObject> firesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> smokesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> basesGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> civilsGO = new Dictionary<string, GameObject>();
    Dictionary<string, GameObject> poisGO = new Dictionary<string, GameObject>();

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
        UpdateAgents(world.agents);
        UpdateFires(world.fires);
        UpdateSmokes(world.smokes);
        UpdateBases(world.bases);
        UpdateCivils(world.civils);
        UpdatePOIs(world.pois);
    }

    // =============================================================
    // CONVERSIÓN DE GRID → UNITY
    // =============================================================

    Vector3 GridToWorld(float gx, float gy, float gz)
    {
        float worldX = gridOrigin.x + gx * cellSize;
        float worldZ = gridOrigin.z + (invertZ ? -gz * cellSize : gz * cellSize);
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
                go = Instantiate(agentPrefab);
                go.name = "Agent_" + a.id;
                agentsGO[a.id] = go;
            }
            else
            {
                go = agentsGO[a.id];
            }

            go.transform.position = GridToWorld(a.x, a.y, a.z);
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
                go = Instantiate(firePrefab);
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
                go = Instantiate(smokePrefab);
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
                GameObject go = Instantiate(basePrefab);
                go.transform.position = GridToWorld(b.x, b.y, b.z);
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
                go = Instantiate(civilPrefab);
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
                go = Instantiate(poiPrefab);
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
