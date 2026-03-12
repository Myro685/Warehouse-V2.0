using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Důležité pro blokování kliků za UI!
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    public enum BuildTool { Rack, Wall, Inbound, Outbound, Resting, AGV, Remove }

    public class BuildManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        
        [Header("Zdroje k postavení (Ze složky Prefabs)")]
        public GameObject rackPrefab;
        public GameObject wallPrefab;
        public GameObject inboundPrefab;
        public GameObject outboundPrefab;
        public GameObject restingPrefab; // Přidáno pro Parkovací doky
        public GameObject agvPrefab;

        [Header("Aktuální nástroj v ruce")]
        public BuildTool currentTool = BuildTool.Rack;

        private void Update()
        {
            if (Mouse.current == null) return;

            // Klíčová oprava! 
            // Kdykoliv máš myš nad Texty, Canvasem nebo Tlačítky, nesmíme propálit laser dolů a stavět kostku!
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (currentTool == BuildTool.Remove)
                    RemoveObjectAtMouse();
                else
                    PlaceObjectAtMouse();
            }
        }

        // Tvoje hlavní kamera ve scéně asi nemá nativní Unity Tag "MainCamera", takže to hazelo "Object reference not set". 
        // Tento Fallback kód ji najde vždy ať má štítek jakýkoliv:
        private Camera GetCamera()
        {
            if (Camera.main != null) return Camera.main;
            
            Camera anyCam = FindFirstObjectByType<Camera>();
            if (anyCam != null) return anyCam;

            Debug.LogError("[BuildManager] Ve scéně není vůbec žádná GameObject Kamera!");
            return null;
        }

        private void PlaceObjectAtMouse()
        {
            Vector2Int gridPos = GetMouseGridPosition();
            
            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridManager.gridConfig.gridX || gridPos.y >= gridManager.gridConfig.gridY) 
                return; 

            int width = currentTool == BuildTool.Rack ? 4 : 1; // Nový Asset Store regál je velký přes 4 bloky!
            
            // Kontrola volného místa pro všechny bloky nového velko-objektu
            if (currentTool != BuildTool.AGV)
            {
                for (int i = 0; i < width; i++)
                {
                    if (gridPos.x + i >= gridManager.gridConfig.gridX) return; // Jsem okrajem mapy venku!
                    
                    Node n = gridManager.GetNode(gridPos.x + i, gridPos.y);
                    if (n == null || n.Type != NodeType.Empty)
                    {
                        Debug.LogWarning("Builder: Nedostatek místa pro celou šířku obřího regálu.");
                        return;
                    }
                }
            }

            Node rootNode = gridManager.GetNode(gridPos.x, gridPos.y);
            Vector3 worldPos = rootNode.GetWorldPosition(gridManager.gridConfig.nodeSize);
            
            GameObject prefabToSpawn = null;
            switch(currentTool)
            {
                case BuildTool.Rack: prefabToSpawn = rackPrefab; break;
                case BuildTool.Wall: prefabToSpawn = wallPrefab; break;
                case BuildTool.Inbound: prefabToSpawn = inboundPrefab; break;
                case BuildTool.Outbound: prefabToSpawn = outboundPrefab; break;
                case BuildTool.Resting: prefabToSpawn = restingPrefab; break;
                case BuildTool.AGV: prefabToSpawn = agvPrefab; break;
            }
            
            if (prefabToSpawn != null)
            {
                // Unikátní vynesení výšky, AGV chceme postavit shora nad dlaždici
                float heightOffset = currentTool == BuildTool.AGV ? 0.3f : 0f;
                Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y + heightOffset, worldPos.z);
                
                GameObject newObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                
                if (currentTool == BuildTool.Wall) rootNode.Type = NodeType.Wall;
                
                if (currentTool == BuildTool.Rack) 
                {
                    rootNode.Type = NodeType.Rack; // Mozek regálu na první (levé) buňce
                    
                    // Bezpečnostně neprodyšně zavřeme zbylé 3 buňky pod modelem
                    for (int i = 1; i < 4; i++) 
                    {
                        Node partNode = gridManager.GetNode(gridPos.x + i, gridPos.y);
                        if (partNode != null) partNode.Type = NodeType.RackPart;
                    }
                }
                
                // Ujištění, že si myší postavený objekt nebude myslet že je na souřadnicích 0,0
                ZoneController zc = newObj.GetComponent<ZoneController>();
                if (zc != null) zc.gridPosition = gridPos;

                RackController rc = newObj.GetComponent<RackController>();
                if (rc != null) rc.gridPosition = gridPos;
                
                AGVController ac = newObj.GetComponent<AGVController>();
                if (ac != null) ac.startCoords = gridPos;
            }
        }

        private void RemoveObjectAtMouse()
        {
            Camera cam = GetCamera();
            if (cam == null) return;

            // Zkusíme najít objekt primárně přes fyziku (kvůli přesnosti kliknutí na model)
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                RackController rack = hit.collider.GetComponentInParent<RackController>();
                ZoneController zone = hit.collider.GetComponentInParent<ZoneController>();
                AGVController agv = hit.collider.GetComponentInParent<AGVController>();
                
                if (rack != null) { RemoveRack(rack); return; }
                if (zone != null) { RemoveZone(zone); return; }
                if (agv != null) { RemoveAGV(agv); return; }
                
                Vector2Int posPhysics = GetMouseGridPosition();
                Node nodePhysics = gridManager.GetNode(posPhysics.x, posPhysics.y);
                if (nodePhysics != null && nodePhysics.Type == NodeType.Wall)
                {
                    nodePhysics.Type = NodeType.Empty;
                    Destroy(hit.collider.gameObject);
                    return; // Zastavit, abychom nešli do fallbacku, protože zdi se mažou čistě přes raycast
                }
            }

            // POKUD FYZIKA SELHALA (Např. Regál a AGV nemají 3D Collider, klikli jsme "skrz" ně přímo na zem Gridu),
            // použijeme matematické vyhledávání pole z GridManageru!
            Vector2Int pos = GetMouseGridPosition();
            if (pos.x < 0 || pos.y < 0) return;

            // A) Hledáme AGV
            foreach (var a in FindObjectsByType<AGVController>(FindObjectsSortMode.None))
            {
                int agvX = Mathf.RoundToInt(a.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(a.transform.position.z / gridManager.gridConfig.nodeSize);
                if (agvX == pos.x && agvY == pos.y) { RemoveAGV(a); return; }
            }

            // B) Hledáme obří Regál (šířka 4 bloky)
            foreach (var r in FindObjectsByType<RackController>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (r.gridPosition.x + i == pos.x && r.gridPosition.y == pos.y) { RemoveRack(r); return; }
                }
            }

            // C) Hledáme Zónu
            foreach (var z in FindObjectsByType<ZoneController>(FindObjectsSortMode.None))
            {
                if (z.gridPosition.x == pos.x && z.gridPosition.y == pos.y) { RemoveZone(z); return; }
            }
        }

        private void RemoveRack(RackController rack)
        {
            for (int i = 0; i < 4; i++)
            {
                Node node = gridManager.GetNode(rack.gridPosition.x + i, rack.gridPosition.y);
                if (node != null && (node.Type == NodeType.Rack || node.Type == NodeType.RackPart)) 
                    node.Type = NodeType.Empty;
            }
            Destroy(rack.gameObject);
        }

        private void RemoveZone(ZoneController zone)
        {
            Node node = gridManager.GetNode(zone.gridPosition.x, zone.gridPosition.y);
            if (node != null) node.Type = NodeType.Empty;
            Destroy(zone.gameObject);
        }

        private void RemoveAGV(AGVController agv)
        {
            // O odstranění z flotil se postará událost OnDestroy() uvnitř AGVControlleru
            Destroy(agv.gameObject);
        }

        private Vector2Int GetMouseGridPosition()
        {
            Camera cam = GetCamera();
            if (cam == null) return new Vector2Int(-1, -1);

            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (groundPlane.Raycast(ray, out float entry))
            {
                Vector3 hitPoint = ray.GetPoint(entry);
                float nodeSize = gridManager.gridConfig.nodeSize;
                
                int x = Mathf.RoundToInt(hitPoint.x / nodeSize);
                int y = Mathf.RoundToInt(hitPoint.z / nodeSize);

                return new Vector2Int(x, y);
            }
            return new Vector2Int(-1, -1);
        }

        public void BtnAction_SelectRackTool() { currentTool = BuildTool.Rack; }
        public void BtnAction_SelectWallTool() { currentTool = BuildTool.Wall; }
        public void BtnAction_SelectInboundTool() { currentTool = BuildTool.Inbound; }
        public void BtnAction_SelectOutboundTool() { currentTool = BuildTool.Outbound; }
        public void BtnAction_SelectRestingTool() { currentTool = BuildTool.Resting; }
        public void BtnAction_SelectAGVTool() { currentTool = BuildTool.AGV; }
        public void BtnAction_SelectRemoveTool() { currentTool = BuildTool.Remove; }
    }
}
