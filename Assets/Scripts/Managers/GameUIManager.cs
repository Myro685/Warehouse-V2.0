using UnityEngine;
using UnityEngine.UI;
using WarehouseSim.Managers;
using TMPro;

namespace WarehouseSim.UI
{
    /// <summary>
    /// Slouží jako bezpečný prostředník mezi 2D Canvasem (Tlačítky, Texty) 
    /// a hlubokými 3D systémy skladu. Cílem je zrušit nutnost používat Inspector 
    /// pro ovládání logistiky během spuštěné hry.
    /// </summary>
    public class GameUIManager : MonoBehaviour
    {
        [Header("Systémy skladu (Backend)")]
        public TaskSystem taskSystem;
        public PathfindingManager pathfindingManager;
        public RackManager rackManager;

        [Header("UI Texty na Obrazovce (Frontend)")]
        public TextMeshProUGUI txtCapacity;
        public TextMeshProUGUI txtJobsInfo;
        public TextMeshProUGUI txtAlgorithmInfo;
        public TextMeshProUGUI txtAnalytics; // Doplněno zpět

        [Header("UI Tlačítka (Simulace)")]
        public Button btnPlay;
        public Button btnPause;

        private float _currentSimulationSpeed = 1f;

        private void Start()
        {
            // Fáze 20: Každá hra začíná ve Stavebním módu (Čas se nehýbe, auta stojí)
            Time.timeScale = 0f;
            
            // Výchozí vizuál tlačítek
            if (btnPlay != null) btnPlay.interactable = true;
            if (btnPause != null) btnPause.interactable = false; 
        }

        private void Update()
        {
            RefreshDashboard();
        }

        /// <summary>
        /// Čte živá data ze skladu a propisuje je do textů na obrazovce.
        /// </summary>
        private void RefreshDashboard()
        {
            if (rackManager != null && txtCapacity != null)
            {
                int maxPotential = 0;
                int storedCount = 0;
                foreach (var r in rackManager.AllRacks) 
                {
                    maxPotential += r.maxCapacity;
                    storedCount += r.CurrentItemCount;
                }

                txtCapacity.text = $"Zaplněnost skladu: {storedCount} / {maxPotential}";
            }

            if (taskSystem != null && txtJobsInfo != null)
            {
                int workingCount = taskSystem.fleet.FindAll(a => !a.IsIdle).Count;
                txtJobsInfo.text = $"Aktivní mise AGV: {workingCount} / {taskSystem.fleet.Count}";
            }

            if (pathfindingManager != null && txtAlgorithmInfo != null)
            {
                txtAlgorithmInfo.text = $"Aktivní mozek tras: {pathfindingManager.activeAlgorithm}";
            }

            if (AnalyticsManager.Instance != null && txtAnalytics != null)
            {
                txtAnalytics.text = $"Ujeta vzdálenost: {Mathf.RoundToInt(AnalyticsManager.Instance.TotalDistanceTraveled)}m | Expedováno: {AnalyticsManager.Instance.TotalItemsDelivered}";
            }
        }

        // ==========================================
        // Tlačítka z plochy (voláno přes OnClick v Editoru)
        // ==========================================

        public void BtnAction_OrderInbound()
        {
            if (taskSystem != null) taskSystem.CreateInboundTask();
        }

        public void BtnAction_OrderOutbound()
        {
            if (taskSystem != null) taskSystem.CreateOutboundTask();
        }

        public void BtnAction_SwitchAlgorithm()
        {
            if (pathfindingManager != null)
            {
                // Překlapávání mezi A* a Dijkstrou pouhým stiskem tlačítka
                if (pathfindingManager.activeAlgorithm == PathfindingAlgorithm.AStar)
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.Dijkstra;
                else
                    pathfindingManager.activeAlgorithm = PathfindingAlgorithm.AStar;
            }
        }

        // ==========================================
        // Řízení Času Simulace a Zátěžáku (Fáze 20)
        // ==========================================

        public void BtnAction_PlaySimulation()
        {
            if (_currentSimulationSpeed <= 0f) _currentSimulationSpeed = 1f; // Failsafe
            Time.timeScale = _currentSimulationSpeed;
            
            if (taskSystem != null) taskSystem.stressTestMixed = true; // Rozjetí objednávek!
            
            if (btnPlay != null) btnPlay.interactable = false; // "Zamačkni se"
            if (btnPause != null) btnPause.interactable = true; // "Rozsviť se"
        }

        public void BtnAction_PauseSimulation()
        {
            Time.timeScale = 0f;
            
            if (taskSystem != null) taskSystem.stressTestMixed = false; // Vypnutí objednávek
            
            if (btnPlay != null) btnPlay.interactable = true;
            if (btnPause != null) btnPause.interactable = false;
        }

        // Pro Unity Slider: Hodnoty od 0.5 do 5.0 (Fast Forward)
        public void SliderAction_SetSimulationSpeed(float speed)
        {
            // Pokud ti Slider projde až k nule, hra by se pauzla a zablokovala. Zafixujeme minimální rychlost na 0.1x!
            _currentSimulationSpeed = Mathf.Max(0.1f, speed);
            
            // Pokud simulace zrovna běží, hned tu rychlost upraví
            if (Time.timeScale != 0f)
            {
                Time.timeScale = _currentSimulationSpeed;
            }
        }

        // Pro Unity Slider: Třeba 0.5 do 5 vteřin
        public void SliderAction_SetOrderInterval(float interval)
        {
            if (taskSystem != null)
            {
                // Pokud Slider pošle nulu (0 vteřin), systém padne do infinite/zero-tick loopu a zamrzne! 
                // Zafixujeme absolutně nejbrutálnější zátěžák haly matematicky natvrdo na 0.2 vteřiny (5 aut za vteřinu!).
                taskSystem.stressTestInterval = Mathf.Max(0.2f, interval);
            }
        }
        // ==========================================
        // DATOVÁ ANALYTIKA (Fáze 21)
        // ==========================================
        public void BtnAction_ExportReport()
        {
            if (AnalyticsManager.Instance != null)
            {
                AnalyticsManager.Instance.ExportToCSV();
                // Lehké vizuální potvrzení přímo to textu analytiky na ploše
                if (txtAnalytics != null) txtAnalytics.text += "\n<color=green>✓ Export Uložen do složky Hry (.csv)</color>";
            }
        }
    }
}
