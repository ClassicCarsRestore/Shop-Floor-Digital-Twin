using System.Collections.Generic;
using Models;


namespace Models
{
    public class ActivityAndLocationHistory
    {
        public string Id;
        public string CaseInstanceId;
        public List<ActivityAndLocation> History;
    }
}

