using System;
using System.Collections.Generic;

namespace Models
{

    public class TaskDTO
    {

        public string id;

        public string activityId;

        public string processInstanceId;

        public DateTime startTime;

        public DateTime completionTime;

        public string commentReport;

        public string commentExtra;

        public string boardSectionId;

        public string boardSectionUrl;

        public List<string> pins;

        public string blockChainId;
    }
}
