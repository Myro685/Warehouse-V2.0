using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Data;
using WarehouseSim.Managers;

namespace WarehouseSim.Controllers
{
    // Celočíselný Stavový automat (Finite State Machine)
    public enum AGVState
    {
        Idle,
        MovingToPickup,
        MovingToDropoff,
        Charging
    }

    /// <summary>
    /// Mozek samotného vozíku. Přehodili jsme jeho logiku přes callbacky (události),
    /// aby byl pod plnou nadvládou centrálního TaskSystemu.
    /// </summary>
    public class AGVController : MonoBehaviour
    {
        [Header("References")]
        public PathfindingManager pathfindingManager;
        public GridManager gridManager;

        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public Vector2Int startCoords = new Vector2Int(0, 0);

        [Header("State Data (Pro bakalářku a monitorování)")]
        public AGVState currentState = AGVState.Idle;
        public Item loadedItem = null;
        public bool IsIdle => currentState == AGVState.Idle;

        private List<Node> _currentPath;
        private int _targetPathIndex;
        private bool _isMoving;

        private void Start()
        {
            if (gridManager != null && gridManager.Grid != null)
            {
                Node startNode = gridManager.GetNode(startCoords.x, startCoords.y);
                if (startNode != null) transform.position = startNode.GetWorldPosition(gridManager.gridConfig.nodeSize);
            }
        }

        public void LoadItem(Item item)
        {
            loadedItem = item;
            // TODO: Zde přidáme aktivaci vizuální kostky krabice na vozíku
        }

        public Item UnloadItem()
        {
            Item temp = loadedItem;
            loadedItem = null;
            return temp;
        }

        /// <summary>
        /// Srdce chytrosti AGV - dojede na místo a samo pošle TaskSystemu zprávu (Action onComplete), že tam je.
        /// </summary>
        public void MoveToAndNotify(Vector2Int targetCoords, System.Action onComplete)
        {
            StopAllCoroutines();
            _isMoving = false;

            int currentX = Mathf.RoundToInt(transform.position.x / gridManager.gridConfig.nodeSize);
            int currentY = Mathf.RoundToInt(transform.position.z / gridManager.gridConfig.nodeSize);

            // !! EXTRÉMNĚ CHYTRÝ DETAIL !!
            // Pokud jede k RACKU (Regálu), tak by Pathfinding vyhodil chybu, protože Regál svítí jako Zeď.
            // Naučíme vozík zastavit Z BOKU/ZEPŘEDU u regálu!
            Node targetNode = gridManager.GetNode(targetCoords.x, targetCoords.y);
            if (targetNode != null && !targetNode.IsWalkable)
            {
                targetCoords = FindWalkableNeighbor(targetCoords.x, targetCoords.y);
            }

            _currentPath = pathfindingManager.RequestPath(new Vector2Int(currentX, currentY), targetCoords);

            if (_currentPath != null && _currentPath.Count > 0)
            {
                _targetPathIndex = 0;
                StartCoroutine(FollowPathRoutine(onComplete));
            }
            else
            {
                // Jakmile narazí na chybu nebo tam už jsme, rovnou zahlásíme "skončil jsem cestu".
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Vyzkouší všechny 4 směry do kříže u cizí buňky a najde tu, kam může auto zaparkovat.
        /// </summary>
        private Vector2Int FindWalkableNeighbor(int x, int y)
        {
            Vector2Int[] dirs = { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
            foreach (var d in dirs)
            {
                Node n = gridManager.GetNode(x + d.x, y + d.y);
                if (n != null && n.IsWalkable) 
                    return new Vector2Int(n.GridX, n.GridY);
            }
            return new Vector2Int(x, y); // Vrací sebe v případě kolapsu ze všech stran
        }

        private IEnumerator FollowPathRoutine(System.Action onComplete)
        {
            _isMoving = true;

            while (_targetPathIndex < _currentPath.Count)
            {
                Node targetNode = _currentPath[_targetPathIndex];
                Vector3 targetWorldPos = targetNode.GetWorldPosition(gridManager.gridConfig.nodeSize);

                // Záznam stopy pro budoucí vybarvování tepelné mapy
                if (AnalyticsManager.Instance != null && _targetPathIndex > 0)
                {
                    AnalyticsManager.Instance.RegisterNodeVisited(targetNode.GridX, targetNode.GridY);
                }

                while (Vector3.Distance(transform.position, targetWorldPos) > 0.05f)
                {
                    Vector3 prevPos = transform.position;
                    transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);
                    
                    // Exaktní sběr ujetých decimetrů pro statistiku!
                    if (AnalyticsManager.Instance != null) 
                        AnalyticsManager.Instance.AddDistance(Vector3.Distance(prevPos, transform.position));

                    Vector3 direction = (targetWorldPos - transform.position).normalized;
                    if (direction != Vector3.zero) transform.rotation = Quaternion.LookRotation(direction);
                    yield return null;
                }

                transform.position = targetWorldPos;
                _targetPathIndex++;
            }

            _isMoving = false;
            _currentPath = null;
            onComplete?.Invoke(); // UPOZORNĚNÍ -> Haló, dojel jsem do cíle! (Zavolá TaskSystem)
        }

        private void OnDrawGizmos()
        {
            if (_currentPath != null && gridManager != null && _isMoving)
            {
                Gizmos.color = Color.magenta;
                if (_targetPathIndex < _currentPath.Count)
                    Gizmos.DrawLine(transform.position, _currentPath[_targetPathIndex].GetWorldPosition(gridManager.gridConfig.nodeSize));

                for (int i = _targetPathIndex; i < _currentPath.Count - 1; i++)
                {
                    Vector3 p1 = _currentPath[i].GetWorldPosition(gridManager.gridConfig.nodeSize);
                    Vector3 p2 = _currentPath[i + 1].GetWorldPosition(gridManager.gridConfig.nodeSize);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }
    }
}
