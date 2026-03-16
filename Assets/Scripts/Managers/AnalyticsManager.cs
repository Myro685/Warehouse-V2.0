using UnityEngine;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Centrální sběrna dat z logistického řetězce. Agreguje relevantní metriky, 
    /// jako je efektivita trasy nebo výkonnost algoritmů, za účelem statistického prokazování.
    /// Integruje systém procedurální vizualizace teplotní mapy (Heatmap).
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        public static AnalyticsManager Instance { get; private set; }

        public float TotalDistanceTraveled { get; private set; }
        public int TotalItemsDelivered { get; private set; }
        
        private int[,] _heatmapData;
        private int _maxVisits = 1;

        [Header("Heatmap Visualization")]
        private GameObject _heatmapContainer;
        private MeshRenderer[,] _heatmapRenderers;
        public bool isHeatmapVisible = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void InitializeHeatmap(int gridX, int gridY)
        {
            _heatmapData = new int[gridX, gridY];
        }

        public void AddDistance(float distance)
        {
            TotalDistanceTraveled += distance;
        }

        public void RegisterItemDelivered()
        {
            TotalItemsDelivered++;
        }

        // ==========================================
        // Procedurální Heatmapa (Event-Driven Optimalizace)
        // ==========================================
        
        public void RegisterNodeVisited(int x, int y)
        {
            if (_heatmapData == null)
            {
                GridManager gm = FindFirstObjectByType<GridManager>();
                if (gm != null && gm.gridConfig != null)
                    _heatmapData = new int[gm.gridConfig.gridX, gm.gridConfig.gridY];
            }

            if (_heatmapData != null && x >= 0 && x < _heatmapData.GetLength(0) && y >= 0 && y < _heatmapData.GetLength(1))
            {
                _heatmapData[x, y]++;
                
                bool newMax = false;
                if (_heatmapData[x, y] > _maxVisits)
                {
                    _maxVisits = _heatmapData[x, y];
                    newMax = true;
                }

                if (isHeatmapVisible && _heatmapRenderers != null)
                {
                    if (newMax) RefreshAllHeatmapTiles();
                    else RefreshSingleTile(x, y);
                }
            }
        }

        public void ToggleHeatmap()
        {
            isHeatmapVisible = !isHeatmapVisible;
            
            if (isHeatmapVisible)
            {
                if (_heatmapContainer == null) CreateHeatmapGrid();
                _heatmapContainer.SetActive(true);
                RefreshAllHeatmapTiles(); 
            }
            else
            {
                if (_heatmapContainer != null) _heatmapContainer.SetActive(false);
            }
        }

        private void CreateHeatmapGrid()
        {
            GridManager gm = FindFirstObjectByType<GridManager>();
            if (gm == null) return;

            int xSize = gm.gridConfig.gridX;
            int ySize = gm.gridConfig.gridY;
            float nodeSize = gm.gridConfig.nodeSize;

            _heatmapContainer = new GameObject("HeatmapContainer");
            _heatmapRenderers = new MeshRenderer[xSize, ySize];

            Shader shader = Shader.Find("Sprites/Default");

            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.SetParent(_heatmapContainer.transform);
                    
                    Vector3 pos = gm.GetNode(x, y).GetWorldPosition(nodeSize);
                    pos.y = 0.15f; 
                    
                    quad.transform.position = pos;
                    quad.transform.rotation = Quaternion.Euler(90, 0, 0); 
                    quad.transform.localScale = new Vector3(nodeSize * 0.95f, nodeSize * 0.95f, 1f); 

                    Destroy(quad.GetComponent<Collider>());

                    MeshRenderer mr = quad.GetComponent<MeshRenderer>();
                    mr.material = new Material(shader);
                    mr.material.color = new Color(0, 0, 0, 0); 
                    
                    _heatmapRenderers[x, y] = mr;
                }
            }
        }

        private void RefreshAllHeatmapTiles()
        {
            for (int x = 0; x < _heatmapRenderers.GetLength(0); x++)
            {
                for (int y = 0; y < _heatmapRenderers.GetLength(1); y++)
                {
                    RefreshSingleTile(x, y);
                }
            }
        }

        private void RefreshSingleTile(int x, int y)
        {
            int visits = _heatmapData[x, y];
            if (visits == 0)
            {
                _heatmapRenderers[x, y].material.color = new Color(0, 0, 0, 0);
            }
            else
            {
                float ratio = (float)visits / _maxVisits;
                Color col = Color.Lerp(Color.green, Color.red, ratio);
                
                col.a = 0.5f; 
                _heatmapRenderers[x, y].material.color = col;
            }
        }

        public int GetNodeVisits(int x, int y)
        {
            if (_heatmapData != null && x >= 0 && x < _heatmapData.GetLength(0) && y >= 0 && y < _heatmapData.GetLength(1))
                return _heatmapData[x, y];
            return 0;
        }

        // ==========================================
        // Tvorba datového reportu
        // ==========================================
        
        /// <summary>
        /// Agreguje veškeré KPI simulace a zapisuje strukturovaný CSV soubor 
        /// na lokální systém běžícího projektu do jeho kořenového adresáře.
        /// </summary>
        public void ExportToCSV()
        {
            string folderPath = Application.dataPath + "/../"; 
            string fileName = "Warehouse_Report_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
            string fullPath = System.IO.Path.Combine(folderPath, fileName);

            System.Text.StringBuilder csv = new System.Text.StringBuilder();
            
            csv.AppendLine("Warehouse Simulation - Analytics Report");
            csv.AppendLine("Datum generovani;" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            csv.AppendLine("");

            PathfindingManager pm = FindFirstObjectByType<PathfindingManager>();
            string algo = (pm != null) ? pm.activeAlgorithm.ToString() : "Neznamy";
            csv.AppendLine("Pouziti algoritmus;" + algo);
            csv.AppendLine("");

            csv.AppendLine("METRIKY VYSTUPU");
            csv.AppendLine("Celkova ujeta vzdalenost (m);" + TotalDistanceTraveled.ToString("F2"));
            csv.AppendLine("Celkem doruceno krabic;" + TotalItemsDelivered);
            csv.AppendLine("");

            TaskSystem ts = FindFirstObjectByType<TaskSystem>();
            if (ts != null && ts.fleet.Count > 0)
            {
                csv.AppendLine("FLOTILA AGV");
                csv.AppendLine("Jmeno vozu;Zbyvajici Baterie %;Aktualni Stav");
                foreach (var agv in ts.fleet)
                {
                    csv.AppendLine($"{agv.gameObject.name};{agv.currentBattery:F1}%;{agv.currentState}");
                }
            }

            try
            {
                System.IO.File.WriteAllText(fullPath, csv.ToString());
                NotificationManager.LogSuccess($"[Analytics] Dokončeno. Report uložen do: {fullPath}");
            }
            catch (System.Exception e)
            {
                NotificationManager.LogError($"[Analytics] Selhal pokus o zápis dat na disk: {e.Message}");
            }
        }
    }
}
