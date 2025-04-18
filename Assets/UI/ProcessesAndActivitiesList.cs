using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.UI;
using Models;
using API;
using Objects;

namespace UI
{

    public class ProcessesAndActivitiesList : MonoBehaviour
    {
        public GameObject accordionItemPrefab;
        public Transform contentPanel;
        private CamundaAPIscript camundaAPIscript;
        private APIscript apiScript;
        private List<string> activityIds;
        private List<UserTaskBPMN> userTasks;
        private readonly List<AccordionItem> accordionItems = new();

        void Start()
        {
            camundaAPIscript = GameObject.Find("CamundaAPIscript").GetComponent<CamundaAPIscript>();
            apiScript = GameObject.Find("APIscript").GetComponent<APIscript>();
            if (camundaAPIscript != null)
            {
                camundaAPIscript.ProcessDefinitionsReceived += InstantiateList;
                camundaAPIscript.UserTasksReceived += OnUserTasksReceived;
            }
            if (apiScript == null)
            {
                Debug.Log("apiScriptnull");
            }
        }

        public async Task PopulateProcessesAndActivitiesUpdate(string virtualMapLocationId)
        {
            apiScript = GameObject.Find("APIscript").GetComponent<APIscript>();
            await apiScript.GetVirtualMapLocationByIdAsync(virtualMapLocationId);
            activityIds = apiScript.locationById.activityIds;
            await camundaAPIscript.GetLatestProcessDefinitions();
        }

        public async Task PopulateProcessesAndActivitiesNew()
        {
            camundaAPIscript = GameObject.Find("CamundaAPIscript").GetComponent<CamundaAPIscript>();
            activityIds = null;
            await camundaAPIscript.GetLatestProcessDefinitions();
        }

        public List<string> GetCheckedActivities()
        {
            List<string> checkedActivities = new();

            foreach (var accordionItem in accordionItems)
            {
                foreach (var userTask in accordionItem.instantiatedUserTasks)
                {
                    Toggle toggle = userTask.GetComponentInChildren<Toggle>();
                    if (toggle != null && toggle.isOn)
                    {
                        checkedActivities.Add(userTask.name);
                    }
                }
            }

            return checkedActivities;
        }

        private async void InstantiateList(List<ProcessDefinition> processDefinitions)
        {
            List<Task> tasks = new();
            foreach (var processDefinition in processDefinitions)
            {
                tasks.Add(OnProcessDefinitionsReceivedNew(processDefinition));
            }
            await Task.WhenAll(tasks);
        }

        private async Task OnProcessDefinitionsReceivedNew(ProcessDefinition processDefinition)
        {

            await camundaAPIscript.GetProcessXmlAndUserTasks(processDefinition.Id);
            GameObject accordionItemObject = Instantiate(accordionItemPrefab, contentPanel);
            AccordionItem accordionItem = accordionItemObject.GetComponent<AccordionItem>();
            accordionItem.SetProcessDetails1(processDefinition.Name, userTasks, activityIds);
            accordionItems.Add(accordionItem);

        }

        private void OnUserTasksReceived(List<UserTaskBPMN> userTasksList)
        {
            userTasks = userTasksList;
        }

        private void OnDestroy()
        {
            if (camundaAPIscript != null)
            {
                camundaAPIscript.ProcessDefinitionsReceived -= InstantiateList;
                camundaAPIscript.UserTasksReceived -= OnUserTasksReceived;
            }
        }

    }
}