using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.ScriptableObjects;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Správce navigační a logické 2D sítě simulace. Inicializuje paměťovou instanci Gridu
    /// na základě zadané konfigurace a poskytuje rozhraní pro dotazování ze strany Pathfinding algoritmů.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Configuration")]
        public GridConfig gridConfig;

        /// <summary> Instancovaná pole všech logických uzlů mapy. </summary>
        public Node[,] Grid { get; private set; }

        private void Awake()
        {
            GenerateGrid();
        }

        /// <summary>
        /// Alokuje paměťové struktury matice uzlů o velikosti definované v GridConfig.
        /// Inicializuje všechny buňky jako propustné (NodeType.Empty).
        /// </summary>
        private void GenerateGrid()
        {
            if (gridConfig == null)
            {
                NotificationManager.LogError("Chyba inicializace: GridManager postrádá konfigurační objekt GridConfig.");
                return;
            }

            Grid = new Node[gridConfig.gridX, gridConfig.gridY];

            for (int x = 0; x < gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridConfig.gridY; y++)
                {
                    Grid[x, y] = new Node(x, y, NodeType.Empty);
                }
            }
            
            NotificationManager.LogSuccess($"[Systém] Grid vytvořen. Rozlišení sítě: {gridConfig.gridX}x{gridConfig.gridY} uzlů.");
        }

        /// <summary>
        /// Bezpečný přístupový bod (getter) pro získání reference na specifický uzel sítě.
        /// </summary>
        /// <param name="x">Mřížková souřadnice X</param>
        /// <param name="y">Mřížková souřadnice Y</param>
        /// <returns>Objekt uzlu nebo null, pokud je dotaz mimo rozsah.</returns>
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
            if (gridConfig == null) return;

            bool hasRunningGrid = Application.isPlaying && Grid != null;

            for (int x = 0; x < gridConfig.gridX; x++)
            {
                for (int y = 0; y < gridConfig.gridY; y++)
                {
                    NodeType type = NodeType.Empty;
                    
                    if (hasRunningGrid)
                    {
                        type = Grid[x, y].Type;
                    }

                    Gizmos.color = GetColorForNodeType(type);
                    
                    Vector3 pos = new Vector3(x * gridConfig.nodeSize, 0f, y * gridConfig.nodeSize);
                    
                    Vector3 size = Vector3.one * (gridConfig.nodeSize - gridConfig.gizmoGap);
                    size.y = 0.05f; 

                    Gizmos.DrawCube(pos, size);
                    
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireCube(pos, size);
                }
            }
        }

        /// <summary>
        /// Definuje renderovací barvy specifické pro debugovací účely uvnitř okna Scene.
        /// Viditelné plošky jsou primárně deaktivovány (Color.clear) ke zmírnění Z-Fightingu a nepřehlednosti.
        /// </summary>
        private Color GetColorForNodeType(NodeType type)
        {
            return type switch
            {
                NodeType.Empty => new Color(0.8f, 0.8f, 0.8f, 0.4f),
                NodeType.Wall => Color.black,
                NodeType.Rack => Color.clear,         
                NodeType.InboundZone => Color.clear,  
                NodeType.OutboundZone => Color.clear, 
                NodeType.RestingZone => Color.clear,  
                NodeType.RackPart => Color.clear,     
                _ => Color.white
            };
        }
    }
}
