using UnityEngine;
using TMPro;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Řídí vyskakovací "Modal" pro bakalářskou obhajobu a profi analýzu.
    /// Spočítá průměry a vytáhne nejtajnější statistiky ze všech systémů do jednoho panelu.
    /// </summary>
    public class AnalyticsModalController : MonoBehaviour
    {
        [Header("UI Canvas Panel k zobrazení")]
        [Tooltip("Přetáhni sem podkladový panel modalu (s tmavým poloprůhledným pozadím)")]
        public GameObject modalPanel;
        
        [Header("Textové výstupy")]
        public TextMeshProUGUI txtTotalDeliveries;
        public TextMeshProUGUI txtTotalDistance;
        public TextMeshProUGUI txtAvgDistance;
        public TextMeshProUGUI txtFleetSize;
        public TextMeshProUGUI txtBottleneck;

        private void Start()
        {
            // Pro jistotu modal na začátku hry skryjeme
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        // Tuto fuknci zavoláš z nového tlačítka "Zobrazit Statistiky"
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

        // Zavolá se z křížku/tlačítka pro zavření
        public void CloseModal()
        {
            if (modalPanel != null) modalPanel.SetActive(false);
        }

        private void RefreshStatistics()
        {
            var am = AnalyticsManager.Instance;
            if (am == null) return;

            // 1) Obyčejná tvrdá data
            if (txtTotalDeliveries != null) 
                txtTotalDeliveries.text = $"Celkem vyřízeno objednávek: <color=#00FF00>{am.TotalItemsDelivered} ks</color>";
            
            if (txtTotalDistance != null) 
                txtTotalDistance.text = $"Celková trasa flotily: <color=#00FF00>{am.TotalDistanceTraveled:F1} m</color>";
            
            // 2) Rafinovaný Průměr pro důkaz efektivity A* algoritmu
            if (txtAvgDistance != null)
            {
                float avg = am.TotalItemsDelivered > 0 ? (am.TotalDistanceTraveled / am.TotalItemsDelivered) : 0f;
                txtAvgDistance.text = $"Efektivita (Průměr na 1 dodávku): <color=#00FFFF>{avg:F1} m</color>";
            }

            // 3) Počet aut v běhu (Sonda do TaskSystemu)
            if (txtFleetSize != null)
            {
                var ts = FindObjectOfType<TaskSystem>();
                int fleetSize = ts != null ? ts.fleet.Count : 0;
                txtFleetSize.text = $"Aktivních AGV ve skladu: <color=#FFFF00>{fleetSize} vozítek</color>";
            }

            // 4) Detekce úzkého hrdla skladu (Sken Heatmapy)
            if (txtBottleneck != null)
            {
                int maxV = 0;
                Vector2Int maxCoords = new Vector2Int(0, 0);
                
                var gm = FindObjectOfType<GridManager>();
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
                    txtBottleneck.text = $"Kritické hrdlo skladu (Uzavírka): <color=#FF0000>Uzel [{maxCoords.x}, {maxCoords.y}] ({maxV}x)</color>";
                else
                    txtBottleneck.text = $"Kritické hrdlo skladu (Uzavírka): <color=#888888>Nedostatek dat</color>";
            }
        }
    }
}
