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

            // Sloučení všech dat do jednoho pole, aby se vyhnulo překryvům z důvodu chybějící LayoutGroup ve scéně
            string fullText = "";

            fullText += $"<size=120%>Celkem vyřízeno objednávek: <color=#00FF00>{am.TotalItemsDelivered} ks</color></size>\n\n";
            fullText += $"<size=120%>Celková trasa flotily: <color=#00FF00>{am.TotalDistanceTraveled:F1} m</color></size>\n\n";

            // 2) Rafinovaný Průměr pro důkaz efektivity A* algoritmu
            float avg = am.TotalItemsDelivered > 0 ? (am.TotalDistanceTraveled / am.TotalItemsDelivered) : 0f;
            fullText += $"<size=120%>Efektivita (Průměr na 1 dodávku): <color=#00FFFF>{avg:F1} m</color></size>\n\n";

            // 3) Počet aut v běhu (Sonda do TaskSystemu)
            var ts = FindFirstObjectByType<TaskSystem>();
            int fleetSize = ts != null ? ts.fleet.Count : 0;
            fullText += $"<size=120%>Aktivních AGV ve skladu: <color=#FFFF00>{fleetSize} vozítek</color></size>\n\n";

            // 4) Detekce úzkého hrdla skladu (Sken Heatmapy)
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
                fullText += $"<size=120%>Kritické hrdlo skladu (Uzavírka): <color=#FF0000>Uzel [{maxCoords.x}, {maxCoords.y}] ({maxV}x)</color></size>";
            else
                fullText += $"<size=120%>Kritické hrdlo skladu (Uzavírka): <color=#888888>Nedostatek dat</color></size>";

            // Nastavíme do prvního nalezeného textu, ostatní deaktivujeme
            if (txtTotalDeliveries != null) 
            {
                txtTotalDeliveries.alignment = TextAlignmentOptions.Center;
                txtTotalDeliveries.text = fullText;
            }

            if (txtTotalDistance != null) txtTotalDistance.gameObject.SetActive(false);
            if (txtAvgDistance != null) txtAvgDistance.gameObject.SetActive(false);
            if (txtFleetSize != null) txtFleetSize.gameObject.SetActive(false);
            if (txtBottleneck != null) txtBottleneck.gameObject.SetActive(false);
        }
    }
}
