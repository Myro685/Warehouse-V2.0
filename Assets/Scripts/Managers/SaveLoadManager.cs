using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WarehouseSim.Controllers;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    // ==========================================
    // Serializační modely DTO (Data Transfer Objects)
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
    /// Servisní třída obstarávající trvalou persistenci procedurální scény na pevný disk.
    /// Využívá JSON serializaci k ukládání absolutních pozic AGV a infrastrukturních bariér Gridu.
    /// </summary>
    public class SaveLoadManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        public BuildManager buildManager;

        private string SavePath => Application.persistentDataPath + "/warehouse_save.json";

        /// <summary>
        /// Agreguje statickou infrastrukturu a dynamické subjekty do DTO kontejneru a serializuje jej.
        /// </summary>
        public void SaveWarehouse()
        {
            if (gridManager == null || gridManager.Grid == null) return;

            WarehouseSaveData data = new WarehouseSaveData();

            for (int x = 0; x < gridManager.gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridManager.gridConfig.gridY; y++)
                {
                    Node node = gridManager.GetNode(x, y);
                    if (node != null && (node.Type == NodeType.Wall || node.Type == NodeType.Rack || node.Type == NodeType.InboundZone || node.Type == NodeType.OutboundZone))
                    {
                        data.blocks.Add(new SavedBlock { x = x, y = y, type = node.Type });
                    }
                }
            }

            foreach (var agv in FindObjectsByType<AGVController>(FindObjectsSortMode.None))
            {
                data.agvs.Add(new SavedAGV { posX = agv.transform.position.x, posZ = agv.transform.position.z });
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            NotificationManager.LogSuccess($"[Systém persistence] Scéna uložena do: {SavePath}");
        }

        /// <summary>
        /// Deserializuje JSON stream zpět do paměti a plně rekonstruuje prostorově rozložené objekty (Instancing).
        /// </summary>
        public void LoadWarehouse()
        {
            if (!File.Exists(SavePath))
            {
                NotificationManager.LogWarning("[Systém persistence] Žádná záloha haly nebyla na disku nalezena.");
                return;
            }

            string json = File.ReadAllText(SavePath);
            WarehouseSaveData data = JsonUtility.FromJson<WarehouseSaveData>(json);

            int loadedCount = 0;
            foreach (var block in data.blocks)
            {
                Node node = gridManager.GetNode(block.x, block.y);
                if (node != null)
                {
                    Vector3 worldPos = node.GetWorldPosition(gridManager.gridConfig.nodeSize);
                    
                    GameObject prefab = null;
                    if (block.type == NodeType.Rack) 
                    {
                        prefab = buildManager.rackPrefab;
                        
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
                        
                        if (block.type == NodeType.Wall) node.Type = NodeType.Wall;
                        
                        ZoneController zc = newObj.GetComponent<ZoneController>();
                        if (zc != null) zc.gridPosition = new Vector2Int(block.x, block.y);

                        RackController rc = newObj.GetComponent<RackController>();
                        if (rc != null) rc.gridPosition = new Vector2Int(block.x, block.y);

                        loadedCount++;
                    }
                }
            }

            foreach (var agvData in data.agvs)
            {
                if (buildManager.agvPrefab != null)
                {
                    Vector3 pos = new Vector3(agvData.posX, buildManager.agvPrefab.transform.position.y, agvData.posZ);
                    Instantiate(buildManager.agvPrefab, pos, Quaternion.identity);
                }
            }

            NotificationManager.LogInfo($"[Systém persistence] Úspěšné načtení. Vytvořeno objektů: {loadedCount}, AGV vozidel: {data.agvs.Count}");
        }
    }
}
