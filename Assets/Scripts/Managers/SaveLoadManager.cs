using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    // ==========================================
    // DATA PRO EXPORT (Musí být [Serializable])
    // ==========================================
    [System.Serializable]
    public class SavedBlock
    {
        public int x;
        public int y;
        public NodeType type;
    }

    [System.Serializable]
    public class SavedAGV
    {
        public float posX;
        public float posZ;
    }

    [System.Serializable]
    public class WarehouseSaveData
    {
        public List<SavedBlock> blocks = new List<SavedBlock>();
        public List<SavedAGV> agvs = new List<SavedAGV>();
    }

    /// <summary>
    /// Zajišťuje trvalou persistenci mapy (Zdí a Regálů) do formátu JSON.
    /// Zabraňuje ztrátě postaveného skladu po vypnutí Play režimu.
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        public BuildManager buildManager; // Potřebujeme pro přístup ke 3D prefabům

        private string SavePath => Application.persistentDataPath + "/warehouse_save.json";

        public void SaveWarehouse()
        {
            if (gridManager == null || gridManager.Grid == null) return;

            WarehouseSaveData data = new WarehouseSaveData();

            // Skenujeme celý 2D grid a hledáme jakýkoliv pevný objekt
            for (int x = 0; x < gridManager.gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridManager.gridConfig.gridY; y++)
                {
                    Node node = gridManager.GetNode(x, y);
                    // Nyní ukládáme Zdi, Regály A NAVÍC logistické rampy!
                    if (node != null && (node.Type == NodeType.Wall || node.Type == NodeType.Rack || node.Type == NodeType.InboundZone || node.Type == NodeType.OutboundZone))
                    {
                        data.blocks.Add(new SavedBlock { x = x, y = y, type = node.Type });
                    }
                }
            }

            // Uložení AGVček
            foreach (var agv in FindObjectsOfType<AGVController>())
            {
                data.agvs.Add(new SavedAGV { posX = agv.transform.position.x, posZ = agv.transform.position.z });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[Save/Load] Sklad uložen do systémové paměti OS: {SavePath}");
        }

        public void LoadWarehouse()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning("[Save/Load] Uložená data nebyla nalezena!");
                return;
            }

            string json = File.ReadAllText(SavePath);
            WarehouseSaveData data = JsonUtility.FromJson<WarehouseSaveData>(json);

            // Ve vysoce produkční verzi zde prvně zničíme staré bloky ze scény.
            // V rámci tohoto dema doporučujeme mačkat "Load" na prázdném skladu (na začátku spuštění)
            
            int loadedCount = 0;
            foreach (var block in data.blocks)
            {
                Node node = gridManager.GetNode(block.x, block.y);
                if (node != null)
                {
                    Vector3 worldPos = node.GetWorldPosition(gridManager.gridConfig.nodeSize);
                    
                    // Rozhodnutí o prefabu podle uloženého typu z JSONu
                    GameObject prefab = null;
                    if (block.type == NodeType.Rack) 
                    {
                        prefab = buildManager.rackPrefab;
                        
                        // Znovuvytvoření ochrany pod celým dlouhým Asset Store modelem regálu
                        for (int i = 1; i < 4; i++) 
                        {
                            Node partNode = gridManager.GetNode(block.x + i, block.y);
                            if (partNode != null) partNode.Type = NodeType.RackPart;
                        }
                    }
                    else if (block.type == NodeType.Wall) prefab = buildManager.wallPrefab;
                    else if (block.type == NodeType.InboundZone) prefab = buildManager.inboundPrefab;
                    else if (block.type == NodeType.OutboundZone) prefab = buildManager.outboundPrefab;

                    if (prefab != null)
                    {
                        GameObject newObj = Instantiate(prefab, worldPos, Quaternion.identity);
                        
                        // Rack a Zony se postaraji o uzly samy
                        if (block.type == NodeType.Wall) node.Type = NodeType.Wall;
                        
                        ZoneController zc = newObj.GetComponent<ZoneController>();
                        if (zc != null) zc.gridPosition = new Vector2Int(block.x, block.y);

                        RackController rc = newObj.GetComponent<RackController>();
                        if (rc != null) rc.gridPosition = new Vector2Int(block.x, block.y);

                        loadedCount++;
                    }
                }
            }

            // Načtení AGV vozítek
            foreach (var agvData in data.agvs)
            {
                if (buildManager.agvPrefab != null)
                {
                    Vector3 pos = new Vector3(agvData.posX, buildManager.agvPrefab.transform.position.y, agvData.posZ);
                    Instantiate(buildManager.agvPrefab, pos, Quaternion.identity);
                }
            }

            Debug.Log($"[Save/Load] Skladová struktura obnovena. Načteno {loadedCount} objektů a {data.agvs.Count} AGVček.");
        }
    }
}
