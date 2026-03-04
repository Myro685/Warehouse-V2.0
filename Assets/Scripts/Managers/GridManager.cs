using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.ScriptableObjects;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Hlavní správce datové sítě skladu. Generuje pole Node[,] a slouží
    /// k dotazům ostatních systémů (Pathfinding, AGV) na konkrétní buňky.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Vlož vytvořený GridConfig (z okna Project)")]
        public GridConfig gridConfig;

        // Naše interní 2D pole všech uzlů. 
        // Readonly zvenčí, zevnitř ho vytvoříme v Awake.
        public Node[,] Grid { get; private set; }

        private void Awake()
        {
            GenerateGrid();
        }

        /// <summary>
        /// Vytvoří logickou paměťovou síť buněk podle rozměrů z konfigurace.
        /// </summary>
        private void GenerateGrid()
        {
            if (gridConfig == null)
            {
                Debug.LogError("Chyba: GridManager nemá přiřazený GridConfig! Inicializace zrušena.");
                return;
            }

            Grid = new Node[gridConfig.gridX, gridConfig.gridY];

            for (int x = 0; x < gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridConfig.gridY; y++)
                {
                    // Na začátku je každá buňka volná (Empty)
                    Grid[x, y] = new Node(x, y, NodeType.Empty);
                }
            }
            
            Debug.Log($"GridManager úspěšně vygeneroval mapu o velikosti {gridConfig.gridX}x{gridConfig.gridY}.");
        }

        /// <summary>
        /// Slouží jako bezpečný getter pro ostatní skripty (např. Pathfinding).
        /// Vrací null, pokud hledáme souřadnice mimo rozsah haly.
        /// </summary>
        public Node GetNode(int x, int y)
        {
            if (x >= 0 && x < gridConfig.gridX && y >= 0 && y < gridConfig.gridY)
            {
                return Grid[x, y];
            }
            return null;
        }

        // ==========================================================
        // VIZUALIZACE V EDITORU (GIZMOS)
        // ==========================================================
        private void OnDrawGizmos()
        {
            // Vykreslujeme jen když máme konfiguraci
            if (gridConfig == null) return;

            // Zjistíme, zda už hra běží a máme reálně nafouklou paměť `Grid`.
            bool hasRunningGrid = Application.isPlaying && Grid != null;

            for (int x = 0; x < gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridConfig.gridY; y++)
                {
                    // Defaultní barva prázdné buňky v editoru
                    NodeType type = NodeType.Empty;
                    
                    if (hasRunningGrid)
                    {
                        // Ve hře (Play Mode) získáme aktuální typ překážky z paměti!
                        type = Grid[x, y].Type;
                    }

                    // Určení barvy k vykreslení
                    Gizmos.color = GetColorForNodeType(type);
                    
                    // Pozice buňky (Y = 0)
                    Vector3 pos = new Vector3(x * gridConfig.nodeSize, 0f, y * gridConfig.nodeSize);
                    
                    // Velikost čtverečku uměle zmenšená pro "okraje"
                    Vector3 size = Vector3.one * (gridConfig.nodeSize - gridConfig.gizmoGap);
                    // Gizmos neumí dobře kreslit jen 2D Plane, takže vykreslíme placatou krychli a stáhneme Y na nulu
                    size.y = 0.05f; 

                    // Samotné nakreslení čtverce
                    Gizmos.DrawCube(pos, size);
                    
                    // Černá kontura buňky, aby vynikla síť
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireCube(pos, size);
                }
            }
        }

        private Color GetColorForNodeType(NodeType type)
        {
            return type switch
            {
                NodeType.Empty => new Color(0.8f, 0.8f, 0.8f, 0.4f), // Šedá s poloprůhledností
                NodeType.Wall => Color.black,
                NodeType.Rack => Color.clear,         // Přestaneme čmárat modře přes reálné 3D regály!
                NodeType.InboundZone => Color.clear,  // Přenecháme grafické ztvárnění Prefab dlaždici hráče
                NodeType.OutboundZone => Color.clear, // Přenecháme grafické ztvárnění Prefab dlaždici hráče
                NodeType.RestingZone => Color.yellow,
                NodeType.RackPart => Color.clear,     // Neviditelný blokátor provozu pod regálem
                _ => Color.white
            };
        }
    }
}
