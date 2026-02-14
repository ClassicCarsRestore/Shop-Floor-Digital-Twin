using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C.ChartBuilder
{
    public class E2ChartDataInfo
    {
        public int seriesCount;
        public bool[] seriesShow;
        public string[] seriesNames;

        public int maxDataCount;
        public int activeSeriesCount;
        public int[] activeDataCount;

        public float minValue;
        public float maxValue;
        public float[][] dataValue;
        public bool[][] dataShow;

        public System.Globalization.CultureInfo cultureInfo;
        public E2ChartGridAxis valueAxis, posAxis;

        protected E2ChartData cData;

        public bool isDataValid { get => maxDataCount > 0; }

        public E2ChartDataInfo(E2ChartData chartData)
        {
            cData = chartData;
        }

        public string GetCategoryX(int index)
        {
            return cData.categoriesX != null && index < cData.categoriesX.Count ? cData.categoriesX[index] : "-";
        }

        public string GetCategoryY(int index)
        {
            return cData.categoriesY != null && index < cData.categoriesY.Count ? cData.categoriesY[index] : "-";
        }

        public List<string> GetActiveSeriesNames()
        {
            List<string> nameList = new List<string>();
            for (int i = 0; i < seriesCount; ++i)
            {
                if (activeDataCount[i] == 0) continue;
                nameList.Add(seriesNames[i]);
            }
            return nameList;
        }

        public virtual void Init()
        {
            List<E2ChartData.Series> series = VerifyData();

            //create data array
            seriesNames = new string[seriesCount];
            seriesShow = new bool[seriesCount];
            dataValue = new float[seriesCount][];
            dataShow = new bool[seriesCount][];
            for (int i = 0; i < seriesCount; ++i)
            {
                seriesNames[i] = series[i] == null || string.IsNullOrEmpty(series[i].name) ? "-" : series[i].name;
                seriesShow[i] = series[i] == null ? false : series[i].show;
                dataValue[i] = new float[maxDataCount];
                dataShow[i] = new bool[maxDataCount];
            }

            //assign values
            for (int i = 0; i < seriesCount; ++i)
            {
                bool hasValueSeries = series[i] != null && series[i].dataY != null && series[i].dataY.Count > 0;
                bool hasShowSeries = series[i] != null && series[i].dataShow != null && series[i].dataShow.Count > 0;
                for (int j = 0; j < maxDataCount; ++j)
                {
                    if (hasValueSeries && j < series[i].dataY.Count)
                    {
                        dataValue[i][j] = series[i].dataY[j];
                        dataShow[i][j] = !hasShowSeries || j >= series[i].dataShow.Count || series[i].dataShow[j];
                    }
                    else
                    {
                        dataValue[i][j] = 0;
                        dataShow[i][j] = false;
                    }
                }
            }
        }

        public virtual void SetZoomRange(float zMin, float zMax, float zRange) { }

        public virtual void ComputeData()
        {
            minValue = float.MaxValue;
            maxValue = float.MinValue;
            activeSeriesCount = 0;
            activeDataCount = new int[seriesCount];
            for (int j = 0; j < maxDataCount; ++j)
            {
                for (int i = 0; i < seriesCount; ++i)
                {
                    if (!seriesShow[i] || !dataShow[i][j]) continue;
                    if (dataValue[i][j] > maxValue) maxValue = dataValue[i][j];
                    activeDataCount[i]++;
                }
            }
            for (int i = 0; i < seriesCount; ++i)
            {
                if (activeDataCount[i] > 0) activeSeriesCount++;
            }
        }

        public void GetValueRatio(float[][] dStart, float[][] dValue)
        {
            for (int i = 0; i < seriesCount; ++i)
            {
                if (!seriesShow[i]) continue;
                for (int j = 0; j < dataValue[i].Length; ++j)
                {
                    dValue[i][j] = (dataValue[i][j] - valueAxis.baseLine) / valueAxis.span;
                    dStart[i][j] = valueAxis.baseLineRatio;
                }
            }
        }

        protected List<E2ChartData.Series> VerifyData()
        {
            List<E2ChartData.Series> series = cData == null || cData.series == null ? new List<E2ChartData.Series>() : cData.series;
            seriesCount = series.Count;
            maxDataCount = 0;
            for (int i = 0; i < series.Count; ++i)
            {
                if (series[i] == null || series[i].dataY == null) continue;
                if (series[i].dataY.Count > maxDataCount) maxDataCount = series[i].dataY.Count;
            }
            if (maxDataCount <= 0)
            {
                E2ChartData.Series s = new E2ChartData.Series();
                s.dataY = new List<float>();
                s.dataY.Add(0);
                s.show = false;
                s.name = "-";
                series.Add(s);
                maxDataCount = 1;
            }
            return series;
        }

        //{dataY}, {abs(dataY)}
        public string GetAxisLabelText(string content, string nFormat, float value)
        {
            content = content.Replace("{data}", E2ChartBuilderUtility.GetFloatString(value, nFormat, cultureInfo));
            content = content.Replace("{abs(data)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(value), nFormat, cultureInfo));
            return content;
        }

        //{series}, {dataName}, {dataY}, {pct(dataY)}
        public string GetLegendText(string content, int seriesIndex)
        {
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesNames[seriesIndex]);
            content = content.Replace("{dataName}", "");
            content = content.Replace("{dataY}", "");
            content = content.Replace("{pct(dataY)}", "");
            return content;
        }

        //{series}, {category}, {dataName}, {dataY}, {abs(dataY)}, {pct(dataY)}
        public virtual string GetLabelText(string content, string nFormat, int seriesIndex, int dataIndex)
        {
            float value = dataValue[seriesIndex][dataIndex];
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesNames[seriesIndex]);
            content = content.Replace("{category}", GetCategoryX(dataIndex));
            content = content.Replace("{dataName}", "");
            content = content.Replace("{dataY}", E2ChartBuilderUtility.GetFloatString(value, nFormat, cultureInfo));
            content = content.Replace("{dataX}", "");
            content = content.Replace("{abs(dataY)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(value), nFormat, cultureInfo));
            content = content.Replace("{pct(dataY)}", "");
            return content;
        }

        //{series}, {category}, {dataName}
        public virtual string GetTooltipHeaderText(string content, int seriesIndex, int dataIndex)
        {
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesNames[seriesIndex]);
            content = content.Replace("{category}", GetCategoryX(dataIndex));
            content = content.Replace("{dataName}", "");
            return content;
        }
    }
}