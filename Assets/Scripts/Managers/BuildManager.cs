using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    public enum BuildTool { Rack, Wall, Inbound, Outbound, Resting, AGV, Remove }

    /// <summary>
    /// Stavební editor simulace. Spravuje vizuální umisťování prefabrikátů infrastruktury 
    /// a AGV vozidel prostřednictvím paprskového castingu (Raycast). Všechny zásahy do 
    /// sítě jsou synchronizovány s instancí GridManager.
    /// </summary>
    public class BuildManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        
        [Header("Prefabs Resource Binding")]
        public GameObject rackPrefab;
        public GameObject wallPrefab;
        public GameObject inboundPrefab;
        public GameObject outboundPrefab;
        public GameObject restingPrefab; 
        public GameObject agvPrefab;

        [Header("State")]
        public BuildTool currentTool = BuildTool.Rack;

        private void Update()
        {
            if (Mouse.current == null) return;

            // Ochrana proti propadávání stisku skrze prvky uživatelského rozhraní (Canvas/UI Layer)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            // Manipulace s prostředím je povolena pouze v editačním režimu (pozastavený logistický časovač)
            if (Time.timeScale != 0f) 
                return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (currentTool == BuildTool.Remove)
                    RemoveObjectAtMouse();
                else
                    PlaceObjectAtMouse();
            }
        }

        /// <summary>
        /// Vyhledává aktivní instanční kameru napříč dostupnými tagy a objekty scény.
        /// </summary>
        private Camera GetCamera()
        {
            if (Camera.main != null) return Camera.main;
            
            Camera anyCam = FindFirstObjectByType<Camera>();
            if (anyCam != null) return anyCam;

            NotificationManager.LogError("Chyba Render Pipeline: Nebyla nalezena žádná Camera složka ve scéně.");
            return null;
        }

        private void PlaceObjectAtMouse()
        {
            Vector2Int gridPos = GetMouseGridPosition();
            
            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridManager.gridConfig.gridX || gridPos.y >= gridManager.gridConfig.gridY) 
                return; 

            int width = currentTool == BuildTool.Rack ? 4 : 1; 
            
            // Validace prostorové volnosti pro vícerozměrné subjekty před zahájením inicializace
            if (currentTool != BuildTool.AGV)
            {
                for (int i = 0; i < width; i++)
                {
                    if (gridPos.x + i >= gridManager.gridConfig.gridX) return;
                    
                    Node n = gridManager.GetNode(gridPos.x + i, gridPos.y);
                    if (n == null || n.Type != NodeType.Empty)
                    {
                        NotificationManager.LogWarning("Zamítnuto: Kolidující infrastruktura v požadovaném vkládacím prostoru.");
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
                float heightOffset = currentTool == BuildTool.AGV ? 0.3f : 0f;
                Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y + heightOffset, worldPos.z);
                
                GameObject newObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                
                if (currentTool == BuildTool.Wall) rootNode.Type = NodeType.Wall;
                
                if (currentTool == BuildTool.Rack) 
                {
                    rootNode.Type = NodeType.Rack; 
                    
                    // Asignace blokátorů obsazenosti pro zamezení průjezdu pod 3D modelem
                    for (int i = 1; i < 4; i++) 
                    {
                        Node partNode = gridManager.GetNode(gridPos.x + i, gridPos.y);
                        if (partNode != null) partNode.Type = NodeType.RackPart;
                    }
                }
                
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

            // Fáze 1: Metoda fyzikální identifikace
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
                    return; 
                }
            }

            // Fáze 2: Analytická iterace skrze grid pro entity bez fyzikálních kolizí
            Vector2Int pos = GetMouseGridPosition();
            if (pos.x < 0 || pos.y < 0) return;

            foreach (var a in FindObjectsByType<AGVController>(FindObjectsSortMode.None))
            {
                int agvX = Mathf.RoundToInt(a.transform.position.x / gridManager.gridConfig.nodeSize);
                int agvY = Mathf.RoundToInt(a.transform.position.z / gridManager.gridConfig.nodeSize);
                if (agvX == pos.x && agvY == pos.y) { RemoveAGV(a); return; }
            }

            foreach (var r in FindObjectsByType<RackController>(FindObjectsSortMode.None))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (r.gridPosition.x + i == pos.x && r.gridPosition.y == pos.y) { RemoveRack(r); return; }
                }
            }

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
