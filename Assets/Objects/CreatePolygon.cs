using System.Linq;
using Objects;
using UnityEngine;

namespace Objects
{



    public class CreatePolygon : MonoBehaviour
    {
        private Vector3[] vertices;
        private CarSlotMeshCreator meshCreator;

        public void initialize(Vector3[] spherePositions)
        {
            vertices = spherePositions;
            meshCreator = GetComponent<CarSlotMeshCreator>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Transform child = transform.GetChild(i);
                child.localPosition = vertices[i];
            }
            if (meshCreator != null)
            {
                Debug.Log("tenho mesh creator");
                meshCreator.CreateMesh(vertices); // Create mesh from the vertices
            }

        }

        // Method to update a specific vertex position
        public void UpdateVertex(int index, Vector3 newPosition)
        {
            if (index >= 0 && index < vertices.Length)
            {

                vertices[index] = newPosition;
                // Update the vertex position
                meshCreator.CreateMesh(vertices, index); // Refresh the mesh
            }
        }

        public void UpdateVertex(int index)
        {
            if (index >= 0 && index < vertices.Length)
            {

                // Update the vertex position
                meshCreator.CreateMesh(vertices, index); // Refresh the mesh
            }
        }


        // Method to hide all spheres
        public void HideAllSpheres()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(false); // Deactivate the child GameObject
            }
        }

        public void ShowAllSpheres()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(true); // Activate the child GameObject
            }
        }
    }
}
