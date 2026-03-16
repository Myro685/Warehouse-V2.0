using UnityEngine;
using TMPro;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Prezentační vrstva pro výpis logistických metrik.
    /// Transformuje surová data z AnalyticsManageru i TaskSystemu a generuje grafickou
    /// nadstavbu obsahující komplexní statistiky průjezdu uzly a efektivitu trasovacích mechanismů.
    /// </summary>
    public class AnalyticsModalController : MonoBehaviour
    {
        [Header("UI Canvas Panel")]
        public GameObject modalPanel;
        
        [Header("Vizuální výstup dat")]
        public TextMeshProUGUI txtTotalDeliveries;
        public TextMeshProUGUI txtTotalDistance;
        public TextMeshProUGUI txtAvgDistance;
        public TextMeshProUGUI txtFleetSize;
        public TextMeshProUGUI txtBottleneck;

        private void Start()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        /// <summary>
        /// Přepíná viditelnost statistického okna. Zároveň před zobrazením iniciuje překreslovací proceduru.
        /// </summary>
        public void ToggleModal()
        {
            if (modalPanel != null)
            {
                bool willBeActive = !modalPanel.activeSelf;
                modalPanel.SetActive(willBeActive);
                
                if (willBeActive)
                {
                    RefreshStatistics();
                }
            }
        }

        public void CloseModal()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        /// <summary>
        /// Sdružuje výpočetní metodiky KPI pro demonstraci přínosu pathfinding algoritmu (např. průměr na objednávku) 
        /// a forenzní odhalení nejslabších článků designu mapy (Heatmap Bottlenecks).
        /// </summary>
        private void RefreshStatistics()
        {
            var am = AnalyticsManager.Instance;
            if (am == null) return;

            string fullText = "";

            fullText += $"<size=120%>Celková expedice: <color=#00FF00>{am.TotalItemsDelivered} ks</color></size>\n\n";
            fullText += $"<size=120%>Celková pojezdová dráha flotily: <color=#00FF00>{am.TotalDistanceTraveled:F1} metru</color></size>\n\n";

            float avg = am.TotalItemsDelivered > 0 ? (am.TotalDistanceTraveled / am.TotalItemsDelivered) : 0f;
            fullText += $"<size=120%>Operační efektivita (Průměr / 1 úkon): <color=#00FFFF>{avg:F1} m</color></size>\n\n";

            var ts = FindFirstObjectByType<TaskSystem>();
            int fleetSize = ts != null ? ts.fleet.Count : 0;
            fullText += $"<size=120%>Objem nasazené flotily (AGV): <color=#FFFF00>{fleetSize} ks</color></size>\n\n";

            int maxV = 0;
            Vector2Int maxCoords = new Vector2Int(0, 0);
            
            var gm = FindFirstObjectByType<GridManager>();
            if (gm != null && gm.gridConfig != null) 
            {
                for (int x = 0; x < gm.gridConfig.gridX; x++) 
                {
                    for (int y = 0; y < gm.gridConfig.gridY; y++) 
                    {
                        int v = am.GetNodeVisits(x, y);
                        if (v > maxV) 
                        { 
                            maxV = v; 
                            maxCoords = new Vector2Int(x, y); 
                        }
                    }
                }
            }

            if (maxV > 0)
                fullText += $"<size=120%>Analýza kongesce mapy (Bod zahlcení): <color=#FF0000>Uzel [{maxCoords.x}, {maxCoords.y}] s maximem {maxV} průjezdů</color></size>";
            else
                fullText += $"<size=120%>Analýza kongesce mapy (Bod zahlcení): <color=#888888>Nedostatek kritických datových relací</color></size>";

            if (txtTotalDeliveries != null) 
            {
                txtTotalDeliveries.alignment = TextAlignmentOptions.Center;
                txtTotalDeliveries.text = fullText;
            }

            // Ostatní nevyužité bloky schováme za účelem vycentrování celého textového bloku
            if (txtTotalDistance != null) txtTotalDistance.gameObject.SetActive(false);
            if (txtAvgDistance != null) txtAvgDistance.gameObject.SetActive(false);
            if (txtFleetSize != null) txtFleetSize.gameObject.SetActive(false);
            if (txtBottleneck != null) txtBottleneck.gameObject.SetActive(false);
        }
    }
}
