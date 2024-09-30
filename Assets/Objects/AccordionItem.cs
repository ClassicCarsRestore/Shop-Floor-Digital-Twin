using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Models;

namespace Objects
{

    public class AccordionItem : MonoBehaviour
    {
        public Text processNameText;
        public Transform userTasksContainer; 
        public GameObject userTaskPrefab; 
        public Button button;
        public Sprite down;
        public Sprite up;
        public List<GameObject> instantiatedUserTasks = new(); 
        private bool hasUserTaskChecked = false;

        private bool isExpanded = false;

        void Start()
        {
            userTaskPrefab.gameObject.SetActive(false);
            button.image.sprite = down;
            SetUserTasksVisibility(false);
            if (hasUserTaskChecked)
            {
                SetUserTasksVisibility(true);
                button.image.sprite = up;
                isExpanded = true;
            }

        }

        public void SetProcessDetails1(string processName, List<UserTaskBPMN> userTaskNames, List<string> activityIdsChecked)
        {
            processNameText.text = processName;

            // Clear any existing user tasks
            foreach (var task in instantiatedUserTasks)
            {
                Destroy(task.gameObject);
            }
            instantiatedUserTasks.Clear();

            // Populate user tasks
            foreach (var userTaskName in userTaskNames)
            {
                GameObject newUserTask = Instantiate(userTaskPrefab, userTasksContainer);
                newUserTask.gameObject.SetActive(true); // Ensure the new user task is active
                newUserTask.name = userTaskName.Id;
                Text taskText = newUserTask.GetComponentInChildren<Text>();
                taskText.text = "- " + userTaskName.Name;
                Toggle toggle = newUserTask.GetComponentInChildren<Toggle>();
                toggle.isOn = false;
                if (activityIdsChecked != null && activityIdsChecked.Contains(userTaskName.Id))
                {
                    toggle.isOn = true;
                    hasUserTaskChecked = true;
                }
                instantiatedUserTasks.Add(newUserTask);
            }

            // Hide user tasks container initially
            SetUserTasksVisibility(true);

            // Setup button click listener
            button.onClick.AddListener(ToggleUserTasks);
            LayoutRebuilder.ForceRebuildLayoutImmediate(userTasksContainer.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());

        }

        public void ToggleUserTasks()
        {
            isExpanded = !isExpanded;
            SetUserTasksVisibility(isExpanded);
            LayoutRebuilder.ForceRebuildLayoutImmediate(userTasksContainer.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
            if (isExpanded)
            {
                button.image.sprite = up;
            }
            else
            {
                button.image.sprite = down;
            }
        }

        private void SetUserTasksVisibility(bool isVisible)
        {
            userTasksContainer.gameObject.SetActive(isVisible);
        }
    }
}
