using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Důležité pro blokování kliků za UI!
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    public enum BuildTool { Rack, Wall, Inbound, Outbound, AGV }

    public class BuildManager : MonoBehaviour
    {
        [Header("References")]
        public GridManager gridManager;
        
        [Header("Zdroje k postavení (Ze složky Prefabs)")]
        public GameObject rackPrefab;
        public GameObject wallPrefab;
        public GameObject inboundPrefab;
        public GameObject outboundPrefab;
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
                PlaceObjectAtMouse();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                RemoveObjectAtMouse();
            }
        }

        // Tvoje hlavní kamera ve scéně asi nemá nativní Unity Tag "MainCamera", takže to hazelo "Object reference not set". 
        // Tento Fallback kód ji najde vždy ať má štítek jakýkoliv:
        private Camera GetCamera()
        {
            if (Camera.main != null) return Camera.main;
            
            Camera anyCam = FindObjectOfType<Camera>();
            if (anyCam != null) return anyCam;

            Debug.LogError("[BuildManager] Ve scéně není vůbec žádná GameObject Kamera!");
            return null;
        }

        private void PlaceObjectAtMouse()
        {
            Vector2Int gridPos = GetMouseGridPosition();
            
            if (gridPos.x < 0 || gridPos.y < 0 || gridPos.x >= gridManager.gridConfig.gridX || gridPos.y >= gridManager.gridConfig.gridY) 
                return; 

            Node node = gridManager.GetNode(gridPos.x, gridPos.y);
            
            // AGV můžeme stavět i na existující Zóně nebo volném poli
            if (currentTool != BuildTool.AGV && (node == null || node.Type != NodeType.Empty))
            {
                Debug.LogWarning("Builder: Na této pozici už stojí jiný objekt nebo zóna.");
                return;
            }

            Vector3 worldPos = node.GetWorldPosition(gridManager.gridConfig.nodeSize);
            
            GameObject prefabToSpawn = null;
            switch(currentTool)
            {
                case BuildTool.Rack: prefabToSpawn = rackPrefab; break;
                case BuildTool.Wall: prefabToSpawn = wallPrefab; break;
                case BuildTool.Inbound: prefabToSpawn = inboundPrefab; break;
                case BuildTool.Outbound: prefabToSpawn = outboundPrefab; break;
                case BuildTool.AGV: prefabToSpawn = agvPrefab; break;
            }
            
            if (prefabToSpawn != null)
            {
                // Unikátní vynesení výšky, AGV chceme postavit shora nad dlaždici
                float heightOffset = currentTool == BuildTool.AGV ? 0.3f : 0f;
                Vector3 spawnPos = new Vector3(worldPos.x, worldPos.y + heightOffset, worldPos.z);
                
                GameObject newObj = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
                
                if (currentTool == BuildTool.Wall) node.Type = NodeType.Wall;
                
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

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                RackController rack = hit.collider.GetComponent<RackController>();
                
                if (rack != null)
                {
                    Node node = gridManager.GetNode(rack.gridPosition.x, rack.gridPosition.y);
                    if (node != null) node.Type = NodeType.Empty;
                    Destroy(hit.collider.gameObject);
                }
                else 
                {
                    Vector2Int pos = GetMouseGridPosition();
                    Node node = gridManager.GetNode(pos.x, pos.y);
                    
                    if (node != null && node.Type == NodeType.Wall)
                    {
                        node.Type = NodeType.Empty;
                        Destroy(hit.collider.gameObject);
                    }
                }
            }
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
        public void BtnAction_SelectAGVTool() { currentTool = BuildTool.AGV; }
    }
}
