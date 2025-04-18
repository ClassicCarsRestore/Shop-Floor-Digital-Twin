using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Models;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Objects
{

    public class CarObject : MonoBehaviour
    {

        private Plane daggingPlane;
        private Vector3 offset;
        private Camera mainCamera;
        private bool isDraggable = false;
        private bool isDragging = false;
        private bool isOverSlot = false;
        private Vector3 slotPosition;
        public CarSlotObject carSlotOver;
        public GameObject warningSign;
        public Car carInfo;

        void Start()
        {
            // Cache the camera at the start. 
            mainCamera = Camera.main;
            warningSign.SetActive(false);
        }

        void OnMouseDown()
        {
            if (!isDraggable)
            {
                return;
            }
            daggingPlane = new Plane(Vector3.up, 0); // ground plane

            Ray camRay = mainCamera.ScreenPointToRay(Input.mousePosition);

            float planeDistance;
            daggingPlane.Raycast(camRay, out planeDistance);

            // Calculate the offset only once when mouse is pressed
            offset = transform.position - camRay.GetPoint(planeDistance);
            isDragging = true;

        }

        void OnMouseDrag()
        {
            if (!isDraggable)
            {
                return;
            }
            if (isDragging)
            {
                Ray camRay = mainCamera.ScreenPointToRay(Input.mousePosition);
                float planeDistance;
                daggingPlane.Raycast(camRay, out planeDistance);

                Vector3 targetPosition = camRay.GetPoint(planeDistance) + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);
            }
        }

        void OnMouseUp()
        {
            if (isDragging)
            {
                isDragging = false;

                if (isOverSlot)
                {
                    // Get the car's bounds (e.g., using its collider or mesh renderer)
                    Collider carCollider = GetComponent<Collider>();
                    Bounds carBounds = carCollider.bounds;

                    // Get the car slot vertices
                    CarSlotMeshCreator currentCarSlotMeshCreator = carSlotOver.GetComponent<CarSlotMeshCreator>();
                    List<VerticesCoordinates> verticesCoordinates = currentCarSlotMeshCreator.GetVertices();

                    // Check if the car is fully inside the slot
                    if (IsCarFullyInsideSlot(carBounds, verticesCoordinates))
                    {
                        Debug.Log("entrou");
                        // Snap car to the center of the slot if fully inside
                        transform.position = new Vector3(
                       Mathf.Round(transform.position.x),
                       Mathf.Round(transform.position.y),
                       Mathf.Round(transform.position.z));
                        warningSign.SetActive(false);
                    }
                    else
                    {
                        Debug.Log(" nao entrou");

                        // Car is partially outside, snap it fully inside
                        // Vector3 adjustedPosition = CalculateSnapPosition(carBounds, verticesCoordinates);
                        Vector3 adjustedPosition = PlaceCarInSlot(verticesCoordinates, 30, 100);
                        Debug.Log("adjustedPosition " + adjustedPosition);
                        transform.position = adjustedPosition;

                        warningSign.SetActive(false); // Hide warning when adjusted
                    }
                    if (ShouldRotateCar(carBounds, verticesCoordinates))
                    {
                        transform.Rotate(0, 90, 0); // Rotate 90 degrees around the Y axis
                    }
                }
                else
                {
                    // If not over slot, snap back to original position
                    transform.position = new Vector3(
                        Mathf.Round(transform.position.x),
                        Mathf.Round(transform.position.y),
                        Mathf.Round(transform.position.z)
                    );
                    warningSign.SetActive(true);
                    carSlotOver = null;
                }

            }
        }

        Vector3 CalculateSnapPosition(Bounds carBounds, List<VerticesCoordinates> slotVertices)
        {
            // Get the car slot's transform
            Transform carSlotTransform = carSlotOver.transform;

            // Initialize min and max slot boundaries in world space
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in slotVertices)
            {
                // Convert local vertex position to world position
                Vector3 worldPoint = carSlotTransform.TransformPoint(new Vector3(vertex.X, 0, vertex.Z));

                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            // Get the current car position
            Vector3 newPosition = transform.position;

            // Calculate if the car is outside on the X axis
            if (carBounds.min.x < minSlot.x)
            {
                newPosition.x += (minSlot.x - carBounds.min.x);
            }
            else if (carBounds.max.x > maxSlot.x)
            {
                newPosition.x -= (carBounds.max.x - maxSlot.x);
            }

            // Calculate if the car is outside on the Z axis
            if (carBounds.min.z < minSlot.z)
            {
                newPosition.z += (minSlot.z - carBounds.min.z);
            }
            else if (carBounds.max.z > maxSlot.z)
            {
                newPosition.z -= (carBounds.max.z - maxSlot.z); // Corrected this line
            }

            // Return the adjusted position to fit within the slot
            return newPosition;
        }

        bool IsCarFullyInsideSlot(Bounds carBounds, List<VerticesCoordinates> slotVertices)
        {
            // Assuming slotVertices defines the rectangular boundary of the car slot in local space.
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            // Get the car slot's transform
            Transform carSlotTransform = carSlotOver.transform;

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in slotVertices)
            {
                // Convert local vertex position to world position
                Vector3 worldPoint = carSlotTransform.TransformPoint(new Vector3(vertex.X, 0, vertex.Z));
                Debug.Log(worldPoint);

                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            // Check if the car's bounds are fully inside the slot's boundary
            if (carBounds.min.x >= minSlot.x && carBounds.max.x <= maxSlot.x &&
                carBounds.min.z >= minSlot.z && carBounds.max.z <= maxSlot.z)
            {
                return true; // Car is fully inside the slot
            }

            return false; // Part of the car is outside the slot
        }
        public Vector3 PlaceCarInSlot(List<VerticesCoordinates> verticesCoordinates, float carLength, float carWidth)
        {
            Vector3 carSlotPos = carSlotOver.transform.position;

            // Initialize min and max slot boundaries in world space
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            List<Vector3> worldVertices = new List<Vector3>();

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in verticesCoordinates)
            {
                // Convert local vertex position to world position using the carSlotPosition
                Vector3 worldPoint = carSlotPos + new Vector3(vertex.X, 0, vertex.Z);
                worldVertices.Add(worldPoint);

                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            // Calculate the half extents of the car based on its dimensions
            float halfCarLength = carLength / 2;
            float halfCarWidth = carWidth / 2;

            // Generate random X and Z within the slot's boundaries, ensuring the car fits
            float randomX = UnityEngine.Random.Range(minSlot.x + halfCarWidth, maxSlot.x - halfCarWidth);
            float randomZ = UnityEngine.Random.Range(minSlot.z + halfCarLength, maxSlot.z - halfCarLength);

            Vector3 randomPosition = new Vector3(randomX, 20, randomZ);

            return randomPosition;

        }


        // Helper method to check if a point is inside a polygon (using Raycasting or Winding Number algorithm)
        bool IsPointInsidePolygon(Vector3 point, List<Vector3> polygonVertices)
        {
            int n = polygonVertices.Count;
            bool inside = false;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((polygonVertices[i].z > point.z) != (polygonVertices[j].z > point.z) &&
                    point.x < (polygonVertices[j].x - polygonVertices[i].x) * (point.z - polygonVertices[i].z) / (polygonVertices[j].z - polygonVertices[i].z) + polygonVertices[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        bool ShouldRotateCar(Bounds carBounds, List<VerticesCoordinates> slotVertices)
        {
            // Get the car slot's transform
            Transform carSlotTransform = carSlotOver.transform;

            // Initialize min and max slot boundaries in world space
            Vector3 minSlot = new Vector3(float.MaxValue, 0, float.MaxValue);
            Vector3 maxSlot = new Vector3(float.MinValue, 0, float.MinValue);

            // Calculate the min and max points of the car slot area in world space
            foreach (VerticesCoordinates vertex in slotVertices)
            {
                Vector3 worldPoint = carSlotTransform.TransformPoint(new Vector3(vertex.X, 0, vertex.Z));
                minSlot.x = Mathf.Min(minSlot.x, worldPoint.x);
                minSlot.z = Mathf.Min(minSlot.z, worldPoint.z);
                maxSlot.x = Mathf.Max(maxSlot.x, worldPoint.x);
                maxSlot.z = Mathf.Max(maxSlot.z, worldPoint.z);
            }

            // Slot dimensions
            float slotWidth = maxSlot.x - minSlot.x;
            float slotLength = maxSlot.z - minSlot.z;

            // Car dimensions (current orientation)
            float carWidth = carBounds.size.x;
            float carLength = carBounds.size.z;

            // Car dimensions if rotated by 90 degrees
            float rotatedCarWidth = carLength;
            float rotatedCarLength = carWidth;

            // Check if the car fits better without rotation
            bool fitsWithoutRotation = (carWidth <= slotWidth && carLength <= slotLength);

            // Check if the car fits better with rotation
            bool fitsWithRotation = (rotatedCarWidth <= slotWidth && rotatedCarLength <= slotLength);

            // Decide whether to rotate the car or not
            return fitsWithRotation && !fitsWithoutRotation; // Rotate if it fits better with rotation
        }


        void OnTriggerEnter(Collider carSlot)
        {
            if (carSlot.CompareTag("carSlot"))
            {
                isOverSlot = true;
                slotPosition = carSlot.transform.position; // Store the position of the slot
                carSlotOver = carSlot.gameObject.GetComponent<CarSlotObject>();
            }
        }

        void OnTriggerExit(Collider carSlot)
        {
            if (carSlot.CompareTag("carSlot"))
            {
                isOverSlot = false;
            }
        }

        public bool hasCarSlot()
        {
            return isOverSlot;
        }

        public CarSlotObject GetCarSlot()
        {
            return carSlotOver;
        }

        public void SetDraggable(bool draggable)
        {
            isDraggable = draggable;
        }

    }
}

