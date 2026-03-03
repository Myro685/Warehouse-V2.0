using UnityEngine;
using UnityEngine.InputSystem;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Umožňuje plně interaktivní profesní pohyb nad skladem (RTS styl).
    /// Hráč může jezdit WASD, táhnout myší po obrazovce a libovolně zoomovat.
    /// Využívá moderní InputSystem Unity, aby nekolidoval s BuildManagerem.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Rychlost pohybu (WASD / Šipky)")]
        public float moveSpeed = 30f;
        
        [Header("Posuv od okrajů obrazovky")]
        public bool useEdgePanning = true;
        public float edgePanBorderThickness = 15f;
        public float edgePanSpeed = 20f;

        [Header("Přibližování (Kolečko myši)")]
        public float zoomSpeed = 50f;
        public float minYHeight = 5f;  // Maximální přiblížení krabicím
        public float maxYHeight = 60f; // Vzdálený ptačí pohled nad halou

        [Header("Tažení kamery (Střední tlačítko)")]
        public float dragSpeed = 0.5f;
        private Vector2 lastMousePosition;
        private bool isDragging = false;

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;

            HandleMovementKeys();
            if (useEdgePanning) HandleEdgePanning();
            HandleMouseDrag();
            HandleZoom();
        }

        private void HandleMovementKeys()
        {
            Vector3 moveDirection = Vector3.zero;

            // Globální posuny nezávisle na rotaci kamery v X/Z ose skladu
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveDirection += Vector3.forward;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveDirection += Vector3.back;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDirection += Vector3.left;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDirection += Vector3.right;

            transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime, Space.World);
        }

        private void HandleEdgePanning()
        {
            Vector3 moveDirection = Vector3.zero;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Kontrola okrajů obrazovky (nahoru, doů, leva, prava)
            if (mousePos.y >= Screen.height - edgePanBorderThickness) moveDirection += Vector3.forward;
            if (mousePos.y <= edgePanBorderThickness) moveDirection += Vector3.back;
            if (mousePos.x >= Screen.width - edgePanBorderThickness) moveDirection += Vector3.right;
            if (mousePos.x <= edgePanBorderThickness) moveDirection += Vector3.left;

            transform.Translate(moveDirection.normalized * edgePanSpeed * Time.deltaTime, Space.World);
        }

        private void HandleMouseDrag()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            // Zmáčknutí kolečka u myši (zahájení posunu)
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMousePosition = mousePos;
            }

            // Puštění kolečka
            if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            // Aplikování tažení v obráceném směru pohybu myši (jako v mapách)
            if (isDragging)
            {
                Vector2 delta = mousePos - lastMousePosition;
                Vector3 move = new Vector3(-delta.x, 0, -delta.y) * dragSpeed * Time.deltaTime;
                
                transform.Translate(move, Space.World);
                lastMousePosition = mousePos;
            }
        }

        private void HandleZoom()
        {
            float scroll = Mouse.current.scroll.y.ReadValue();

            if (Mathf.Abs(scroll) > 0.1f)
            {
                float scrollDir = Mathf.Sign(scroll); // Nahoru (1) nebo Dolů (-1)
                
                // Přibližujeme přesně ve směru rotace kamery (diagonálně k podlaze)
                Vector3 zoomMove = transform.forward * scrollDir * zoomSpeed * Time.deltaTime;
                Vector3 newPos = transform.position + zoomMove;

                // Restrikce výšky, abychom nepropadli texturou podlahy nebo neodletěli na Mars
                if (newPos.y >= minYHeight && newPos.y <= maxYHeight)
                {
                    transform.position = newPos;
                }
            }
        }
    }
}
