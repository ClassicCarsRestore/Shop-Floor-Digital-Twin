using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C.ChartBuilder
{
    public class E2ChartDataInfoLowerLimit : E2ChartDataInfo
    {
        public float[][] dataValueL;
        public int zoomMin;
        public int zoomMax;
        public int zoomMinInterval;

        public E2ChartDataInfoLowerLimit(E2ChartData chartData) : base(chartData) { }

        public override void Init()
        {
            List<E2ChartData.Series> series = VerifyData();

            //create data array
            seriesNames = new string[seriesCount];
            seriesShow = new bool[seriesCount];
            dataValue = new float[seriesCount][];
            dataValueL = new float[seriesCount][];
            dataShow = new bool[seriesCount][];
            for (int i = 0; i < seriesCount; ++i)
            {
                seriesNames[i] = series[i] == null || string.IsNullOrEmpty(series[i].name) ? "-" : series[i].name;
                seriesShow[i] = series[i] == null ? false : series[i].show;
                dataValue[i] = new float[maxDataCount];
                dataValueL[i] = new float[maxDataCount];
                dataShow[i] = new bool[maxDataCount];
            }

            //assign values
            for (int i = 0; i < seriesCount; ++i)
            {
                bool hasValueSeries = series[i] != null && series[i].dataY != null && series[i].dataY.Count > 0;
                bool hasValueSeriesL = series[i] != null && series[i].dataZ != null && series[i].dataZ.Count > 0;
                bool hasShowSeries = series[i] != null && series[i].dataShow != null && series[i].dataShow.Count > 0;
                for (int j = 0; j < maxDataCount; ++j)
                {
                    if (hasValueSeries && j < series[i].dataY.Count)
                    {
                        dataValue[i][j] = series[i].dataY[j];
                        dataValueL[i][j] = hasValueSeriesL && j < series[i].dataZ.Count ? series[i].dataZ[j] : 0.0f;
                        dataShow[i][j] = !hasShowSeries || j >= series[i].dataShow.Count || series[i].dataShow[j];
                    }
                    else
                    {
                        dataValue[i][j] = 0;
                        dataValueL[i][j] = 0;
                        dataShow[i][j] = false;
                    }
                    if (dataValueL[i][j] > dataValue[i][j]) dataValueL[i][j] = dataValue[i][j];
                }
            }
        }

        public override void SetZoomRange(float zMin, float zMax, float zInterval)
        {
            int indexRange = maxDataCount - 1;
            zoomMin = Mathf.RoundToInt(indexRange * Mathf.Clamp01(zMin));
            zoomMax = Mathf.RoundToInt(indexRange * Mathf.Clamp01(zMax));
            zoomMinInterval = Mathf.RoundToInt(indexRange * Mathf.Clamp01(zInterval));
            if (zoomMax < zoomMin + zoomMinInterval)
            {
                zoomMax = zoomMin + zoomMinInterval;
                if (zoomMax > indexRange) zoomMax = indexRange;
                zoomMin = zoomMax - zoomMinInterval;
            }
            if (zoomMin < 0) zoomMin = 0;
            if (zoomMax > indexRange) zoomMax = indexRange;
        }

        public bool SetZoomMin(int zMin)
        {
            int indexRange = maxDataCount - 1;
            int interval = zoomMax - zoomMin;

            int newMin = Mathf.Clamp(zMin, 0, indexRange);
            int newMax = newMin + interval;
            if (newMax > indexRange)
            {
                newMax = indexRange;
                newMin = newMax - interval;
            }

            bool changed = newMin != zoomMin || newMax != zoomMax;
            if (changed) { zoomMin = newMin; zoomMax = newMax; }
            return changed;
        }

        public bool AddZoom(float zAdd)
        {
            int indexRange = maxDataCount - 1;
            int interval = Mathf.RoundToInt((zoomMax - zoomMin) * zAdd * 0.5f);
            if (interval == 0) interval = (int)Mathf.Sign(zAdd);

            int newMin = zoomMin;
            int newMax = zoomMax;
            if (newMin + interval <= newMax - zoomMinInterval) newMin += interval;
            if (newMax - interval >= newMin + zoomMinInterval) newMax -= interval;
            if (newMin < 0) newMin = 0;
            if (newMax > indexRange) newMax = indexRange;

            bool changed = newMin != zoomMin || newMax != zoomMax;
            if (changed) { zoomMin = newMin; zoomMax = newMax; }
            return changed;
        }

        public override void ComputeData()
        {
            minValue = float.MaxValue;
            maxValue = float.MinValue;
            activeSeriesCount = 0;
            activeDataCount = new int[seriesCount];
            for (int j = 0; j < maxDataCount; ++j)
            {
                if (j < zoomMin || j > zoomMax) continue;
                for (int i = 0; i < seriesCount; ++i)
                {
                    if (!seriesShow[i] || !dataShow[i][j]) continue;
                    if (dataValueL[i][j] < minValue) minValue = dataValueL[i][j];
                    if (dataValue[i][j] > maxValue) maxValue = dataValue[i][j];
                    activeDataCount[i]++;
                }
            }
            for (int i = 0; i < seriesCount; ++i)
            {
                if (activeDataCount[i] > 0) activeSeriesCount++;
            }
        }

        public void GetValueRatio(float[][] dStart, float[][] dValue, float[][] dsValue, float[][] dBase)
        {
            for (int i = 0; i < seriesCount; ++i)
            {
                if (!seriesShow[i]) continue;
                for (int j = 0; j < dataValue[i].Length; ++j)
                {
                    dValue[i][j] = (dataValue[i][j] - dataValueL[i][j]) / valueAxis.span;
                    dsValue[i][j] = (dataValueL[i][j] - valueAxis.baseLine) / valueAxis.span;
                    dBase[i][j] = valueAxis.baseLineRatio;
                    dStart[i][j] = dBase[i][j] + dsValue[i][j];
                }
            }
        }

        public override string GetLabelText(string content, string nFormat, int seriesIndex, int dataIndex)
        {
            float value = dataValue[seriesIndex][dataIndex];
            float valueL = dataValueL[seriesIndex][dataIndex];
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesNames[seriesIndex]);
            content = content.Replace("{category}", GetCategoryX(dataIndex));
            content = content.Replace("{dataName}", "");
            content = content.Replace("{dataY}", E2ChartBuilderUtility.GetFloatString(value, nFormat, cultureInfo));
            content = content.Replace("{dataX}", "");
            content = content.Replace("{dataZ}", E2ChartBuilderUtility.GetFloatString(valueL, nFormat, cultureInfo));
            content = content.Replace("{abs(dataY)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(value), nFormat, cultureInfo));
            content = content.Replace("{pct(dataY)}", "");
            content = content.Replace("{abs(dataZ)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(valueL), nFormat, cultureInfo));
            content = content.Replace("{pct(dataZ)}", "");
            return content;
        }

        public override string GetTooltipHeaderText(string content, int seriesIndex, int dataIndex)
        {
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesIndex < 0 ? "" : seriesNames[seriesIndex]);
            content = content.Replace("{category}", GetCategoryX(dataIndex));
            content = content.Replace("{dataName}", "");
            return content;
        }
    }
}