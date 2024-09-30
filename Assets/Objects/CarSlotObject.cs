using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Models;

namespace Objects
{

public class CarSlotObject : MonoBehaviour
{

    private Plane daggingPlane;
   
    private Vector3 offset;
   
    private Camera mainCamera;
    private bool isDragging = false;
    private bool isDraggable = false;

    [SerializeField] private GameObject hover;
    private GameObject currentHover = null;
    public VirtualMapLocation info;
    public Transform canvas;

    void Start()
    {
        canvas = GameObject.FindWithTag("TAG").transform;

        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (isDraggable && Input.GetKeyDown(KeyCode.R))
        {
            RotateObject();
        }
    }

    private void RotateObject()
    {
        transform.Rotate(Vector3.up, 30f); // Rotates the object 90 degrees around the Y-axis
    }

    public void SetDraggable(bool draggable)
    {
        isDraggable = draggable;
    }

    void OnMouseDown()
    {

        if (!isDraggable)
            return;

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
        if (!isDraggable || !isDragging)
            return;

        Ray camRay = mainCamera.ScreenPointToRay(Input.mousePosition);
        float planeDistance;
        daggingPlane.Raycast(camRay, out planeDistance);

        Vector3 targetPosition = camRay.GetPoint(planeDistance) + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 15f);

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


    void OnMouseEnter()
    {
        DisplayHover();
    }

    void OnMouseExit()
    {
        DestroyHover();
    }

    public void DisplayHover()
    {
        if (currentHover != null)
        {
            Destroy(currentHover);
        }

        if (info != null)
        {
            Vector3 Position = Input.mousePosition;
            Position.y += 50;
            Position.x += 50;

            currentHover = Instantiate(hover, Position, Quaternion.identity, canvas);
            string hoverInfo = info.name;
            currentHover.GetComponent<CarHover>().SetUp(hoverInfo);
        }
    }

    public void DestroyHover()
    {
        if (currentHover != null)
        {
            Destroy(currentHover);
        }
    }

}

}
