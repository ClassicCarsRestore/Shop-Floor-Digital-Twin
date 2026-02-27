
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Models;
using Newtonsoft.Json;
using UnityEngine;

namespace API
{

    public class CamundaAPIscript : MonoBehaviour
    {
        //private readonly string baseUrl = "http://194.210.120.34:591/engine-rest";
        private readonly string baseUrl = "/camunda/engine-rest";

        public delegate void OnProcessDefinitionsReceived(List<ProcessDefinition> processDefinitions);
        public event OnProcessDefinitionsReceived ProcessDefinitionsReceived;

        public delegate void OnUserTasksReceived(List<UserTaskBPMN> userTasks);
        public event OnUserTasksReceived UserTasksReceived;

        private HttpClient httpClient;

        private void Awake()
        {
            httpClient = new HttpClient();
        }

        public async Task GetLatestProcessDefinitions()
        {
            string url = $"{baseUrl}/process-definition?latestVersion=true";

            using (HttpClient client = new())
            {
                HttpResponseMessage response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    List<ProcessDefinition> processDefinitions = JsonConvert.DeserializeObject<List<ProcessDefinition>>(json);

                    ProcessDefinitionsReceived?.Invoke(processDefinitions);
                }
                else
                {
                    Debug.LogError("Get Latest Process Definitions failed: " + response.ReasonPhrase);
                }
            }
        }

        public async Task GetProcessXmlAndUserTasks(string processDefinitionId)
        {
            try
            {
                string url = $"{baseUrl}/process-definition/{processDefinitionId}/xml";

                using (HttpClient client = new())
                {
                    HttpResponseMessage response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        BpmnXmlResponse bpmnXmlResponse = JsonConvert.DeserializeObject<BpmnXmlResponse>(json);

                        XDocument xdoc = XDocument.Parse(bpmnXmlResponse.Bpmn20Xml);

                        XNamespace ns = "http://www.omg.org/spec/BPMN/20100524/MODEL";
                        var userTasksElements = xdoc.Descendants(ns + "userTask");

                        List<UserTaskBPMN> userTasks = new();

                        foreach (var userTaskElement in userTasksElements)
                        {
                            userTasks.Add(new UserTaskBPMN
                            {
                                Id = userTaskElement.Attribute("id")?.Value,
                                Name = userTaskElement.Attribute("name")?.Value
                            });
                        }

                        UserTasksReceived?.Invoke(userTasks);
                    }
                    else
                    {
                        Debug.LogError($"Failed to retrieve XML: {response.ReasonPhrase}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in GetProcessXmlAndUserTasks: {ex.Message}");
            }
        }
    }
}