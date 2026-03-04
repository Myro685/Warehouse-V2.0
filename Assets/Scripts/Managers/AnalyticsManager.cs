using UnityEngine;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Slouží jako centrální sběrna dat z celého logistického řetězce.
    /// Perfektní podklad pro bakalářskou obhajobu, dokazuje že algoritmy
    /// reálně šetří ujetou vzdálenost a čas. Dále obsahuje procedurální vizualizaci barevné heatmapy.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        public static AnalyticsManager Instance { get; private set; }

        public float TotalDistanceTraveled { get; private set; }
        public int TotalItemsDelivered { get; private set; }
        
        // Data pro heatmapu
        private int[,] _heatmapData;
        private int _maxVisits = 1;

        [Header("Heatmap Vizuál")]
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
        // TVORBA A AKTUALIZACE HEATMAPY (EVENT DRIVEN OPTIMALIZACE!)
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
                
                // EVENT-DRIVEN přístup:
                // Abchom nepočítali poměry barev v updatu 60x za vteřinu a neničili výkon CPU,
                // aktualizujeme barvy pouze exaktně, když dojde k návštěvě (našlapu) uzlu!
                bool newMax = false;
                if (_heatmapData[x, y] > _maxVisits)
                {
                    _maxVisits = _heatmapData[x, y];
                    newMax = true;
                }

                if (isHeatmapVisible && _heatmapRenderers != null)
                {
                    // Pokud je prolomen rekord a je nová maximalní hodnota, musí se matematicky 
                    // překreslit celé plátno v poměru vůči novému The Best číslu, jinak stačí 1 pixel!
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
                RefreshAllHeatmapTiles(); // Nutno vykreslit dosavadní historii při zapnutí
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

            // Default Sprite shader žere průhlednost nativně z Color structury a nepodléhá stínům
            Shader shader = Shader.Find("Sprites/Default");

            for (int x = 0; x < xSize; x++)
            {
                for (int y = 0; y < ySize; y++)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.transform.SetParent(_heatmapContainer.transform);
                    
                    Vector3 pos = gm.GetNode(x, y).GetWorldPosition(nodeSize);
                    pos.y = 0.15f; // Těsně nad zemí, aby to blikalo nad zónami
                    
                    quad.transform.position = pos;
                    quad.transform.rotation = Quaternion.Euler(90, 0, 0); // Položíme jako dlažbu na podlahu
                    quad.transform.localScale = new Vector3(nodeSize * 0.95f, nodeSize * 0.95f, 1f); // 5% díry dodají pixel art efekt

                    // Kolize nejsou nutné, žraly by strašně moc fyziky
                    Destroy(quad.GetComponent<Collider>());

                    MeshRenderer mr = quad.GetComponent<MeshRenderer>();
                    mr.material = new Material(shader);
                    mr.material.color = new Color(0, 0, 0, 0); // Inicializace na 100% průhledné ticho
                    
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
                // Čím blíž k maxVisits, tím víc 1.0. Tím se posouvá Lerp ze Zelené do Červené!
                float ratio = (float)visits / _maxVisits;
                Color col = Color.Lerp(Color.green, Color.red, ratio);
                
                col.a = 0.5f; // Mírná průhlednost (50%) aby pres to byly hezky vidět grid čáry a AGVčka
                _heatmapRenderers[x, y].material.color = col;
            }
        }

        public int GetNodeVisits(int x, int y)
        {
            if (_heatmapData != null && x >= 0 && x < _heatmapData.GetLength(0) && y >= 0 && y < _heatmapData.GetLength(1))
                return _heatmapData[x, y];
            return 0;
        }
    }
}
