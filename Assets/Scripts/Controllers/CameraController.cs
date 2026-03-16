using UnityEngine;
using UnityEngine.InputSystem;

namespace WarehouseSim.Controllers
{
    /// <summary>
    /// Režijní ovladač uživatelské perspektivy ve scéně.
    /// Zajišťuje plynulou interpolaci kamerového objektivu nad maticí skladu (RTS styl pohybu).
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Translational Dynamics")]
        public float moveSpeed = 30f;
        
        [Header("Edge Panning Metrics")]
        public bool useEdgePanning = true;
        public float edgePanBorderThickness = 15f;
        public float edgePanSpeed = 20f;

        [Header("Zoom Thresholds")]
        public float zoomSpeed = 50f;
        public float minYHeight = 5f;  
        public float maxYHeight = 60f; 

        [Header("Kinematics")]
        public float rotationSpeed = 15f;
        private Vector2 lastMousePosition;
        private bool isDragging = false;

        private void Update()
        {
            if (Mouse.current == null || Keyboard.current == null) return;

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

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveDirection += forward;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveDirection -= forward;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveDirection += right;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveDirection -= right;

            transform.Translate(moveDirection.normalized * moveSpeed * Time.unscaledDeltaTime, Space.World);
        }

        private void HandleEdgePanning(Vector3 forward, Vector3 right)
        {
            Vector3 moveDirection = Vector3.zero;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (mousePos.y >= Screen.height - edgePanBorderThickness) moveDirection += forward;
            if (mousePos.y <= edgePanBorderThickness) moveDirection -= forward;
            if (mousePos.x >= Screen.width - edgePanBorderThickness) moveDirection += right;
            if (mousePos.x <= edgePanBorderThickness) moveDirection -= right;

            transform.Translate(moveDirection.normalized * edgePanSpeed * Time.unscaledDeltaTime, Space.World);
        }

        private void HandleMouseRotation()
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMousePosition = mousePos;
            }

            if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }

            if (isDragging)
            {
                Vector2 delta = mousePos - lastMousePosition;
                
                transform.Rotate(Vector3.up, delta.x * rotationSpeed * Time.unscaledDeltaTime, Space.World);
                transform.Rotate(Vector3.right, -delta.y * rotationSpeed * Time.unscaledDeltaTime, Space.Self);
                
                lastMousePosition = mousePos;
            }
        }

        private void HandleZoom()
        {
            float scroll = Mouse.current.scroll.y.ReadValue();

            if (Mathf.Abs(scroll) > 0.1f)
            {
                float scrollDir = Mathf.Sign(scroll); 
                
                Vector3 zoomMove = transform.forward * scrollDir * zoomSpeed * Time.unscaledDeltaTime;
                Vector3 newPos = transform.position + zoomMove;

                if (newPos.y >= minYHeight && newPos.y <= maxYHeight)
                {
                    transform.position = newPos;
                }
            }
        }
    }
}
