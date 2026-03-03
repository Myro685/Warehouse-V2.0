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
    public class WarehouseSaveData
    {
        public List<SavedBlock> blocks = new List<SavedBlock>();
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
                    // Pro tuto fázi ukládáme Zeď a Regál
                    if (node != null && (node.Type == NodeType.Wall || node.Type == NodeType.Rack))
                    {
                        data.blocks.Add(new SavedBlock { x = x, y = y, type = node.Type });
                    }
                }
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
                    GameObject prefab = block.type == NodeType.Rack ? buildManager.rackPrefab : buildManager.wallPrefab;
                    
                    if (prefab != null)
                    {
                        Instantiate(prefab, worldPos, Quaternion.identity);
                        
                        // Rack se postará o svou node sám, Zeď zapíšeme ručně
                        if (block.type == NodeType.Wall)
                        {
                            node.Type = NodeType.Wall;
                        }

                        loadedCount++;
                    }
                }
            }
            Debug.Log($"[Save/Load] Skladová struktura obnovena. Načteno {loadedCount} překážek.");
        }
    }
}
