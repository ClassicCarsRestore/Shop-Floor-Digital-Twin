using System.Collections.Generic;
namespace Models
{
    public class CamundaTaskDto
    {
        public string Id;
        public string Name;
        public string Assignee;
        public string Created;
        public string ExecutionId;
        public string ParentTaskId;
        public string ProcessDefinitionId;
        public string ProcessInstanceId;
        public string TaskDefinitionKey;
        public bool Suspended;
    }

}