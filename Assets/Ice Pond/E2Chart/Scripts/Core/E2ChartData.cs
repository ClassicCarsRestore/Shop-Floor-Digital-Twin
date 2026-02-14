using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C
{
    public class E2ChartData : MonoBehaviour
    {
        [System.Serializable]
        public class Series
        {
            public string name = "New Series";
            public bool show = true;
#if UNITY_2020_2_OR_NEWER
            [NonReorderable] public List<string> dataName;
            [NonReorderable] public List<bool> dataShow;
            [NonReorderable] public List<float> dataX;
            [NonReorderable] public List<float> dataY;
            [NonReorderable] public List<float> dataZ;
            [NonReorderable] public List<long> dateTimeTick;
            [NonReorderable] public List<string> dateTimeString;
#else
            public List<string> dataName;
            public List<bool> dataShow;
            public List<float> dataX;
            public List<float> dataY;
            public List<float> dataZ;
            public List<long> dateTimeTick;
            public List<string> dateTimeString;
#endif
        }

        public string title = "New Chart";
        public string subtitle = "-";
        public string xAxisTitle = "xAxis";
        public string yAxisTitle = "yAxis";
        public string dateTimeStringFormat = "";
#if UNITY_2020_2_OR_NEWER
        [NonReorderable] public List<Series> series;
#else
        public List<Series> series;
#endif
        public List<string> categoriesX;
        public List<string> categoriesY;

        bool m_changed = false;
        //internal value, do not use
        public bool hasChanged { get => m_changed; set => m_changed = value; }

        private void OnValidate()
        {
            m_changed = true;
        }
    }
}