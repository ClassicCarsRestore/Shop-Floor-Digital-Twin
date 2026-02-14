using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C.ChartBuilder
{
    public class E2ChartDataInfoDateTimeLowerLimit : E2ChartDataInfoDateTime
    {
        public float[][] dataValueL;

        public E2ChartDataInfoDateTimeLowerLimit(E2ChartData chartData) : base(chartData) { }

        public override void Init()
        {
            List<E2ChartData.Series> series = VerifyData();

            //create data array
            seriesNames = new string[seriesCount];
            seriesShow = new bool[seriesCount];
            dataValue = new float[seriesCount][];
            dataValueL = new float[seriesCount][];
            dataPos = new float[seriesCount][];
            dataTick = new long[seriesCount][];
            dataShow = new bool[seriesCount][];
            for (int i = 0; i < seriesCount; ++i)
            {
                seriesNames[i] = series[i] == null || string.IsNullOrEmpty(series[i].name) ? "-" : series[i].name;
                seriesShow[i] = series[i] == null ? false : series[i].show;
                dataValue[i] = series[i] == null || series[i].dataY == null ? new float[0] : new float[series[i].dataY.Count];
                dataValueL[i] = new float[dataValue[i].Length];
                dataPos[i] = new float[dataValue[i].Length];
                dataTick[i] = new long[dataValue[i].Length];
                dataShow[i] = new bool[dataValue[i].Length];
            }

            //assign values
            for (int i = 0; i < seriesCount; ++i)
            {
                bool hasValueSeriesL = series[i] != null && series[i].dataZ != null && series[i].dataZ.Count > 0;
                bool hasShowSeries = series[i] != null && series[i].dataShow != null && series[i].dataShow.Count > 0;
                bool hasTickSeries = series[i] != null && series[i].dateTimeTick != null && series[i].dateTimeTick.Count > 0;
                bool hasStrSeries = series[i] != null && series[i].dateTimeString != null && series[i].dateTimeString.Count > 0;
                if (hasTickSeries)
                {
                    for (int j = 0; j < dataValue[i].Length; ++j)
                    {
                        dataValue[i][j] = series[i].dataY[j];
                        dataValueL[i][j] = hasValueSeriesL && j < series[i].dataZ.Count ? series[i].dataZ[j] : 0.0f;
                        dataShow[i][j] = !hasShowSeries || j >= series[i].dataShow.Count || series[i].dataShow[j];
                        dataTick[i][j] = j < series[i].dateTimeTick.Count ? series[i].dateTimeTick[j] : 0;
                        if (dataValueL[i][j] > dataValue[i][j]) dataValueL[i][j] = dataValue[i][j];
                    }
                }
                else
                {
                    for (int j = 0; j < dataValue[i].Length; ++j)
                    {
                        dataValue[i][j] = series[i].dataY[j];
                        dataValueL[i][j] = hasValueSeriesL && j < series[i].dataZ.Count ? series[i].dataZ[j] : 0.0f;
                        dataShow[i][j] = !hasShowSeries || j >= series[i].dataShow.Count || series[i].dataShow[j];
                        if (hasStrSeries && j < series[i].dateTimeString.Count)
                        {
                            try
                            {
                                System.DateTime dt = System.DateTime.ParseExact(series[i].dateTimeString[j], cData.dateTimeStringFormat, cultureInfo);
                                dataTick[i][j] = dt.Ticks;
                            }
                            catch { dataTick[i][j] = 0; }
                        }
                        else dataTick[i][j] = 0;
                        if (dataValueL[i][j] > dataValue[i][j]) dataValueL[i][j] = dataValue[i][j];
                    }
                }
            }
        }

        public override void ComputeData()
        {
            minValue = float.MaxValue;
            maxValue = float.MinValue;
            activeSeriesCount = 0;
            activeDataCount = new int[seriesCount];
            for (int i = 0; i < seriesCount; ++i)
            {
                if (!seriesShow[i]) continue;
                if (dataShow[i][0] && dataPos[i][0] >= zoomMin && dataPos[i][0] <= zoomMax)
                {
                    if (dataValueL[i][0] < minValue) minValue = dataValueL[i][0];
                    if (dataValue[i][0] > maxValue) maxValue = dataValue[i][0];
                    activeDataCount[i]++;
                }
                for (int j = 1; j < dataValue[i].Length; ++j)
                {
                    if (!dataShow[i][j]) continue;
                    if (dataPos[i][j] < zoomMin || dataPos[i][j] > zoomMax &&
                        dataPos[i][j - 1] < zoomMin || dataPos[i][j - 1] > zoomMax) continue;
                    if (dataValueL[i][j] < minValue) minValue = dataValueL[i][j];
                    if (dataValue[i][j] > maxValue) maxValue = dataValue[i][j];
                    activeDataCount[i]++;
                }
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
            float pos = dataPos[seriesIndex][dataIndex];
            content = content.Replace("\\n", "\n");
            content = content.Replace("{series}", seriesNames[seriesIndex]);
            content = content.Replace("{category}", "");
            content = content.Replace("{dataName}", "");
            content = content.Replace("{dataY}", E2ChartBuilderUtility.GetFloatString(value, nFormat, cultureInfo));
            content = content.Replace("{dataX}", E2ChartBuilderUtility.GetFloatString(pos, nFormat, cultureInfo));
            content = content.Replace("{dataZ}", E2ChartBuilderUtility.GetFloatString(valueL, nFormat, cultureInfo));
            content = content.Replace("{abs(dataY)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(value), nFormat, cultureInfo));
            content = content.Replace("{pct(dataY)}", "");
            content = content.Replace("{abs(dataZ)}", E2ChartBuilderUtility.GetFloatString(Mathf.Abs(valueL), nFormat, cultureInfo));
            content = content.Replace("{pct(dataZ)}", "");
            return content;
        }
    }
}