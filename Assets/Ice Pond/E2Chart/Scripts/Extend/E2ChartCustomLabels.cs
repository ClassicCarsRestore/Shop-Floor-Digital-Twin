using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C.ChartBuilder
{
    public class E2ChartCustomLabels : MonoBehaviour
    {
        [System.Serializable]
        public class CustomLabelInfo
        {
            public int seriesIndex;
            public int dataIndex;

            public string labelContent;
            public E2ChartOptions.TextOptions labelTextOption = new E2ChartOptions.TextOptions(new Color(0.2f, 0.2f, 0.2f, 1.0f), null, 14);
            public float labelOffset = 0.0f;
            public float labelAnchoredPosition = 1.0f;

            public Sprite iconSprite;
            public Color iconColor = Color.white;
            public float iconOffset = 0.0f;
            public float iconAnchoredPosition = 1.0f;
            public Vector2 iconSize = new Vector2(9.0f, 9.0f);
        }

        public CustomLabelInfo[] labelInfo;

        bool m_changed = false;
        //internal value, do not use
        public bool hasChanged { get => m_changed; set => m_changed = value; }

        private void OnValidate()
        {
            m_changed = true;
        }
    }
}