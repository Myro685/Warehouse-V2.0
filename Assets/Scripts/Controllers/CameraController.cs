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

        [Header("Otáčení kamery (Střední tlačítko)")]
        public float rotationSpeed = 15f;
        private Vector2 lastMousePosition;
        private bool isDragging = false;

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;

            // Získání aktuálních směrových vektorů z pohledu objektivu kamery
            // Odstraníme "Y", abychom při "W" neletěli do země, ale horizontálně!
            Vector3 camForward = transform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = transform.right;
            camRight.y = 0;
            camRight.Normalize();

            HandleMovementKeys(camForward, camRight);
            if (useEdgePanning) HandleEdgePanning(camForward, camRight);
            
            HandleMouseRotation();
            HandleZoom();
        }

        private void HandleMovementKeys(Vector3 forward, Vector3 right)
        {
            Vector3 moveDirection = Vector3.zero;

            // Plynulý pohyb po skladišti s ohledem na to, kam se hráč právě kouká!
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveDirection += forward;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveDirection -= forward;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDirection += right;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDirection -= right;

            transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime, Space.World);
        }

        private void HandleEdgePanning(Vector3 forward, Vector3 right)
        {
            Vector3 moveDirection = Vector3.zero;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (mousePos.y >= Screen.height - edgePanBorderThickness) moveDirection += forward;
            if (mousePos.y <= edgePanBorderThickness) moveDirection -= forward;
            if (mousePos.x >= Screen.width - edgePanBorderThickness) moveDirection += right;
            if (mousePos.x <= edgePanBorderThickness) moveDirection -= right;

            transform.Translate(moveDirection.normalized * edgePanSpeed * Time.deltaTime, Space.World);
        }

        private void HandleMouseRotation()
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

            // Aplikování horizontální a vertikální rotace kamery!
            if (isDragging)
            {
                Vector2 delta = mousePos - lastMousePosition;
                
                // Rotace do stran (kolem Y osy světa = doleva / doprava)
                transform.Rotate(Vector3.up, delta.x * rotationSpeed * Time.deltaTime, Space.World);
                
                // Rotace nahoru/dolů (kolem X osy samotné kamery)
                transform.Rotate(Vector3.right, -delta.y * rotationSpeed * Time.deltaTime, Space.Self);
                
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
