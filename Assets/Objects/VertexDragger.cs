using Objects;
using UnityEngine;

namespace Objects
{

    public class VertexDragger : MonoBehaviour
    {
        private CreatePolygon polygonCreator;
        private Plane draggingPlane;
        private Vector3 offset;
        private Camera mainCamera;
        private bool isDragging = false;

        private void Start()
        {
            mainCamera = Camera.main;
        }

        public void Initialize(CreatePolygon creator)
        {
            polygonCreator = creator;
            mainCamera = Camera.main;
        }

        void OnMouseDown()
        {


            draggingPlane = new Plane(Vector3.up, 0); // ground plane

            Ray camRay = mainCamera.ScreenPointToRay(Input.mousePosition);

            float planeDistance;
            draggingPlane.Raycast(camRay, out planeDistance);

            // Calculate the offset only once when mouse is pressed
            offset = transform.position - camRay.GetPoint(planeDistance);
            isDragging = true;
        }

        void OnMouseDrag()
        {
            if (isDragging)
            {
                Ray camRay = mainCamera.ScreenPointToRay(Input.mousePosition);
                float planeDistance;
                draggingPlane.Raycast(camRay, out planeDistance);

                Vector3 targetPosition = camRay.GetPoint(planeDistance) + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
            }

            if (polygonCreator != null)
            {
                polygonCreator.UpdateVertex(transform.GetSiblingIndex(), transform.position);
            }

        }

        void OnMouseUp()
        {
            if (isDragging)
            {
                isDragging = false;

                transform.position = new Vector3(Mathf.Round(transform.position.x),
                                                 Mathf.Round(transform.position.y),
                                                 Mathf.Round(transform.position.z));
            }
        }
    }
}
