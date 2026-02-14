using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using E2C.ChartBuilder;

namespace E2C
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(RectTransform))]
    public class E2Chart : MonoBehaviour
    {
        const float PREVIEW_REFRESH_RATE = 0.05f;

        public enum PreviewMode { HideInHierarchy, HideAndDontSave, None }

        public enum ChartType
        {
            BarChart, 
            LineChart, 
            PieChart, 
            RoseChart, 
            RadarChart,
            SolidGauge,
            Gauge,
            AreaChart,
            Table,
            Heatmap
        }

        public ChartType chartType;
        public E2ChartOptions chartOptions;
        public E2ChartData chartData;

        [Tooltip("Automatically refresh preview")]
        [SerializeField] bool autoRefreshPreview = true;

        [Tooltip("Whether preview object can be saved in scene and show in hierarchy")]
        [SerializeField] PreviewMode previewMode = PreviewMode.HideInHierarchy;

        E2ChartBuilder cBuilder;
        E2ChartPreviewHandler previewInstance;

        public RectTransform rectTransform { get => (RectTransform)transform; }
        public E2ChartPreviewHandler Preview { get => previewInstance; set => previewInstance = value; }
        public E2ChartBuilder Builder { get => cBuilder; }

        float lastPreviewTime = 0.0f;
        bool hasChanged = false;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                //play mode
                ClearPreview();
            }
            else
            {
                //edit mode do nothing
            }
        }

        IEnumerator Start()
        {
            if (Application.isPlaying)
            {
                //play mode
                ClearPreview();
                yield return null;
                UpdateChart();
            }
            else
            {
                //edit mode do nothing
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                //play mode do nothing
            }
            else
            {
                //edit mode
                E2ChartThresholdLines thl = GetComponent<E2ChartThresholdLines>();
                bool thresholdChanged = thl != null && thl.hasChanged;
                E2ChartCustomLabels cl = GetComponent<E2ChartCustomLabels>();
                bool customLabelChanged = cl != null && cl.hasChanged;

                if (previewInstance != null && chartOptions != null && chartData != null &&
                    autoRefreshPreview && Time.time - lastPreviewTime > PREVIEW_REFRESH_RATE &&
                    (hasChanged || transform.hasChanged || chartOptions.hasChanged || chartData.hasChanged || 
                    thresholdChanged || customLabelChanged))
                {
                    lastPreviewTime = Time.time;
                    CreatePreview();
                }
            }
        }

        private void OnEnable()
        {
            if (previewInstance != null) previewInstance.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (previewInstance != null) previewInstance.gameObject.SetActive(false);
        }

        private void OnValidate()
        {
            hasChanged = true;
        }

        private void OnDestroy()
        {
            ClearPreview();
            if (cBuilder != null)
            {
                cBuilder.OnDestroy();
                cBuilder = null;
            }
        }

        public void Clear()
        {
            if (cBuilder == null) return;
            cBuilder.Clear();
            cBuilder.OnDestroy();
            cBuilder = null;
        }

        public void UpdateChart()
        {
            Clear();
            cBuilder = E2ChartBuilderUtility.GetChartBuilder(this);
            cBuilder.Init();
            cBuilder.Build();
        }

        //manually trigger series highlight
        //only works when mouse tracking is set to series mode
        public void SetHighlight(int seriesIndex, int dataIndex = -1)
        {
            cBuilder.SetHighlight(seriesIndex, dataIndex);
        }

        //do not call this function at runtime
        public E2ChartPreviewHandler CreatePreview()
        {
            if (Application.isPlaying) return null;

            hasChanged = false;
            transform.hasChanged = false;
            chartOptions.hasChanged = false;
            chartData.hasChanged = false;
            E2ChartThresholdLines thl = GetComponent<E2ChartThresholdLines>();
            if (thl != null) thl.hasChanged = false;
            E2ChartCustomLabels cl = GetComponent<E2ChartCustomLabels>();
            if (cl != null) cl.hasChanged = false;

            ClearPreview();
            previewInstance = E2ChartBuilderUtility.DuplicateRect(gameObject.name + "(Preview)", rectTransform).gameObject.AddComponent<E2ChartPreviewHandler>();
            previewInstance.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            previewInstance.chart = this;
            previewInstance.UpdateChart();
            if (previewMode == PreviewMode.HideInHierarchy)
            {
                RectTransform[] trans = previewInstance.GetComponentsInChildren<RectTransform>();
                foreach (RectTransform tran in trans) tran.gameObject.hideFlags = HideFlags.NotEditable;
                previewInstance.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
            else if (previewMode == PreviewMode.HideAndDontSave)
            {
                RectTransform[] trans = previewInstance.GetComponentsInChildren<RectTransform>();
                foreach (RectTransform tran in trans) tran.gameObject.hideFlags = HideFlags.NotEditable;
                previewInstance.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            return previewInstance;
        }

        public void ClearPreview()
        {
            if (previewInstance == null) return;
            E2ChartBuilderUtility.Destroy(previewInstance.gameObject);
            previewInstance = null;
        }
    }
}