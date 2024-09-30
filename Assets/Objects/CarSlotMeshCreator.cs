using System;
using System.Collections.Generic;
using API;
using Models;
using TS.ColorPicker;
using UnityEngine;

namespace Objects
{

    public class CarSlotMeshCreator : MonoBehaviour
    {
        private MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Mesh mesh;

        private Vector3[] vertices;
        private int[] triangles;
        private ColorPickerLocation currentColorPickerLocation;
        [SerializeField] private ColorPicker colorPicker;

        public Material material;

        public void CreateMesh(Vector3[] spherePositions, int index)
        {

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.convex = false;
            meshCollider.providesContacts = true;

            
            if (spherePositions.Length != 4) // Ensure there are exactly 4 vertices
            {
                Debug.LogError("Four vertex positions are required to create the mesh.");
                return;
            }

            // Convert world positions to local positions relative to the CarSlot object
            vertices = spherePositions;

            vertices[index] = transform.InverseTransformPoint(spherePositions[index]);

            // Define triangles (two triangles to make a quad)
            triangles = new int[6]
            {
            0, 1, 2, // First triangle
            0, 2, 3  // Second triangle
            };

            // Create the mesh
            mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals(); // Calculate normals for lighting

            meshFilter.mesh = mesh; // Assign the mesh to the mesh filter
            meshCollider.sharedMesh = mesh; // Assign the mesh to the MeshCollider for collision detection

        }

        public void CreateMesh(Vector3[] spherePositions)
        {

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.convex = true;
            meshCollider.isTrigger = true;
            meshCollider.providesContacts = true;
            Debug.Log(meshCollider.providesContacts);

            
            if (spherePositions.Length != 4) // Ensure there are exactly 4 vertices
            {
                Debug.LogError("Four vertex positions are required to create the mesh.");
                return;
            }

            // Convert world positions to local positions relative to the CarSlot object
            vertices = spherePositions;
            Debug.Log(vertices[0]);
            Debug.Log(vertices[1]);

            Debug.Log(vertices[2]);

            Debug.Log(vertices[3]);


            // Define triangles (two triangles to make a quad)
            triangles = new int[6]
            {
            0, 1, 2, // First triangle
            0, 2, 3  // Second triangle
            };

            // Create the mesh
            mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.RecalculateNormals(); // Calculate normals for lighting

            meshFilter.mesh = null; // Assign the mesh to the mesh filter

            meshFilter.mesh = mesh; // Assign the mesh to the mesh filter
            meshCollider.sharedMesh = null;  // Clear the collider first
            meshCollider.sharedMesh = mesh; // Assign the mesh to the MeshCollider for collision detection

        }

        public List<VerticesCoordinates> GetVertices()
        {
            List<VerticesCoordinates> verticesCoordinates = new List<VerticesCoordinates>();

            // Loop through the vertices array and convert them into VerticesCoordinates objects
            foreach (Vector3 vertex in vertices)
            {
                VerticesCoordinates verticeCoord = new VerticesCoordinates
                {
                    X = vertex.x,
                    Z = vertex.z
                };

                verticesCoordinates.Add(verticeCoord);
            }

            return verticesCoordinates;
        }

        public void MeshColliderTriggerTrue()
        {
            meshCollider.convex = true;
            meshCollider.isTrigger = true;
        }

        public void MeshColliderTriggerFalse()
        {
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
        }

    }
}