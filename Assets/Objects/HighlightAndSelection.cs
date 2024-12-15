using System;
using System.Collections;
using System.Collections.Generic;
using Models;
using Objects;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class HighlightAndSelection : MonoBehaviour
{

    private Transform highlight;
    private Transform selection;
    private RaycastHit raycastHit;
    public GameObject hover;
    public GameObject carInfoPanel;
    private GameObject currentHover = null;
    private readonly GameObject currentInfoPanel = null;
    public Transform canvas;
    private AddCar addCarScript;

    private Boolean isSelectionOn = true;

    private void Start()
    {
        addCarScript = FindObjectOfType<AddCar>();
    }

    private void Update()
    {
        // Highlight
        if (highlight != null)
        {
            highlight.gameObject.GetComponent<Outline>().enabled = false;
            highlight = null;
            DestroyHover();
        }
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!EventSystem.current.IsPointerOverGameObject() && Physics.Raycast(ray, out raycastHit)) 
        {
            highlight = raycastHit.transform;
            if (highlight.CompareTag("Selectable") && highlight != selection)
            {
                if (highlight.gameObject.GetComponent<Outline>() != null)
                {
                    highlight.gameObject.GetComponent<Outline>().enabled = true;
                }
                else
                {
                    Outline outline = highlight.gameObject.AddComponent<Outline>();
                    outline.enabled = true;
                    highlight.gameObject.GetComponent<Outline>().OutlineColor = UnityEngine.Color.blue;
                    highlight.gameObject.GetComponent<Outline>().OutlineWidth = 2.0f;
                }
                DisplayHover();
            }
            else
            {
                highlight = null;
            }
        }

        // Selection
        if (isSelectionOn && Input.GetMouseButtonDown(0))
        {

            if (EventSystem.current.IsPointerOverGameObject())
            {
                return; // Do nothing if clicking on UI
            }


            if (highlight)
            {
                Debug.Log("aqui1");

                if (selection != null)
                {
                    selection.gameObject.GetComponent<Outline>().enabled = false;
                }
                selection = raycastHit.transform;
                selection.gameObject.GetComponent<Outline>().enabled = true;
                DestroyHover();
                DisplayCarInfoPanel();
                highlight = null;
            }
            else
            {
                Debug.Log("aqui2");

                if (selection)
                {
                    selection.gameObject.GetComponent<Outline>().enabled = false;
                    selection = null;
                    DestroyCarInfoPanel();
                }
            }

        }

        if (addCarScript != null && addCarScript.simulationModeOn && selection != null &&Input.GetKeyDown(KeyCode.R))
        {
            selection.gameObject.transform.Rotate(0, 15, 0);
        }

    }

    public void DisplayHover()
    {
        if (currentHover != null)
        {
            Destroy(currentHover);
        }
        Car carInfo = highlight.gameObject.GetComponent<CarObject>().carInfo;

        if (carInfo != null)
        {
            Vector3 Position = Input.mousePosition;
            Position.y += 40;
            Position.x += 40;

            currentHover = Instantiate(hover, Position, Quaternion.identity, canvas);
            string info = carInfo.make + " " + carInfo.model + " " + carInfo.year;
            currentHover.GetComponent<CarHover>().SetUp(info);
        }
    }

    public void DestroyHover()
    {
        if (currentHover != null)
        {
            Destroy(currentHover);
        }
    }

    public void DisplayCarInfoPanel()
    {

        Car carInfo = selection.gameObject.GetComponent<CarObject>().carInfo;
        if (carInfo != null)
        {
            var projectsList = GameObject.Find("ProjectsList").GetComponent<ProjectsList>();
            projectsList.ShowCarInfo(carInfo);
        }

    }

    public void DestroyCarInfoPanel()
    {
        if (currentInfoPanel != null)
        {
            Destroy(currentInfoPanel);
        }
    }

    public void SelectionOn()
    {
        isSelectionOn = true;
    }
    public void SelectionOff()
    {
        isSelectionOn = false;
    }
}

