using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WarehouseSim.Core;
using WarehouseSim.Data;

namespace WarehouseSim.Managers
{
    /// <summary>
    /// Slouží pro názornou ukázku toho, jak A* nebo Dijkstra prohledává prostor skladu.
    /// Využívá Coroutines pro vytvoření asynchronní animace v reálném čase.
    /// Animace se zastaví v přesném okamžiku nalezení cíle.
    /// </summary>
    public class PathfindingVisualizer : MonoBehaviour
    {
        private List<GameObject> _visualTiles = new List<GameObject>();
        private Coroutine _currentAnimRoutine;

        public void VisualizeSearch(List<Node> history, List<Node> path, PathfindingAlgorithm algo, GridManager gm, Node targetNode)
        {
            if (_currentAnimRoutine != null)
            {
                StopCoroutine(_currentAnimRoutine);
            }
            
            ClearVisuals();
            _currentAnimRoutine = StartCoroutine(AnimateSearch(history, path, algo, gm, targetNode));
        }

        private IEnumerator AnimateSearch(List<Node> history, List<Node> path, PathfindingAlgorithm algo, GridManager gm, Node targetNode)
        {
            Color searchColor = (algo == PathfindingAlgorithm.AStar) ? new Color(1f, 1f, 0f, 0.4f) : new Color(0f, 0.5f, 1f, 0.4f);
            Color pathColor = new Color(0f, 1f, 0f, 0.6f);
            Color targetColor = new Color(1f, 0f, 0f, 0.8f);
            
            float nodeSize = gm.gridConfig.nodeSize;
            Material mat = new Material(Shader.Find("Sprites/Default"));

            int batchSize = history.Count > 100 ? 5 : 1; 

            for (int i = 0; i < history.Count; i += batchSize)
            {
                for (int b = 0; b < batchSize && i + b < history.Count; b++)
                {
                    Node n = history[i + b];
                    
                    // Cílový uzel se zvýrazní červeně a celá animace prohledávání se zastaví
                    if (n == targetNode)
                    {
                        SpawnQuad(n.GetWorldPosition(nodeSize), nodeSize, targetColor, mat, 0.18f);
                        goto searchComplete;
                    }
                    
                    SpawnQuad(n.GetWorldPosition(nodeSize), nodeSize, searchColor, mat);
                }
                
                yield return new WaitForSeconds(0.01f);
            }
            
            searchComplete:

            // Krátká dramatická pauza po nalezení cíle
            yield return new WaitForSeconds(0.4f);

            // Zobrazení finální trasy zelenou barvou
            if (path != null)
            {
                foreach (Node n in path)
                {
                    SpawnQuad(n.GetWorldPosition(nodeSize), nodeSize, pathColor, mat, 0.2f);
                    yield return new WaitForSeconds(0.015f);
                }
            }

            yield return new WaitForSeconds(3f);

            ClearVisuals();
        }

        private void SpawnQuad(Vector3 pos, float size, Color color, Material mat, float yOffset = 0.16f)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            
            quad.transform.SetParent(this.transform);
            quad.transform.position = new Vector3(pos.x, yOffset, pos.z);
            quad.transform.rotation = Quaternion.Euler(90, 0, 0);
            quad.transform.localScale = new Vector3(size * 0.9f, size * 0.9f, 1f);
            
            MeshRenderer mr = quad.GetComponent<MeshRenderer>();
            mr.material = mat;
            mr.material.color = color;
            
            _visualTiles.Add(quad);
        }

        private void ClearVisuals()
        {
            foreach (var tile in _visualTiles)
            {
                if (tile != null) Destroy(tile);
            }
            _visualTiles.Clear();
        }
    }
}
