using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using E2C.ChartGraphic;
#if CHART_TMPRO
using TMPro;
using E2ChartText = TMPro.TextMeshProUGUI;
using E2ChartTextFont = TMPro.TMP_FontAsset;
#else
using E2ChartText = UnityEngine.UI.Text;
using E2ChartTextFont = UnityEngine.Font;
#endif

namespace E2C.ChartBuilder
{
    public class AreaChartBuilder : E2ChartBuilder
    {
        public const int MAX_DATA_POINTS = 14000;
        public const int MIN_ZOOM_UNIT_LENGTH = 5;
        public const float ZOOM_SENSTIVITY = 0.1f;

        E2ChartGraphicLineChartShade[] areaList;
        E2ChartGraphicLineChartLine[] lineList;
        E2ChartGraphicLineChartLine[] lineListL;
        Image highlight;
        List<int> trackingList;
        SortedDictionary<int, int[]> trackingDict; //pos and series index
        int currTracking;
        float beginZoomMin;
        int currMouse, lastMouse;

        public AreaChartBuilder(E2Chart c) : base(c) { }

        public AreaChartBuilder(E2Chart c, RectTransform rect) : base(c, rect) { }

        protected override void CreateDataInfo()
        {
            if (options.xAxis.type == E2ChartOptions.AxisType.Category)
            {
                dataInfo = new E2ChartDataInfoLowerLimit(data);
                tooltipHeaderContent = string.IsNullOrEmpty(options.tooltip.headerContent) ? "{category}" : options.tooltip.headerContent;
            }
            else if (options.xAxis.type == E2ChartOptions.AxisType.Linear)
            {
                dataInfo = new E2ChartDataInfoLinearLowerLimit(data);
                tooltipHeaderContent = string.IsNullOrEmpty(options.tooltip.headerContent) ? "" : options.tooltip.headerContent;
            }
            else if (options.xAxis.type == E2ChartOptions.AxisType.DateTime)
            {
                dataInfo = new E2ChartDataInfoDateTimeLowerLimit(data);
                tooltipHeaderContent = string.IsNullOrEmpty(options.tooltip.headerContent) ? "" : options.tooltip.headerContent;
            }
            labelContent = string.IsNullOrEmpty(options.label.content) ? "{dataZ}-{dataY}" : options.label.content;
            tooltipPointContent = string.IsNullOrEmpty(options.tooltip.pointContent) ? "{series}: {dataZ}-{dataY}" : options.tooltip.pointContent;
            legendContent = string.IsNullOrEmpty(options.legend.content) ? "{series}" : options.legend.content;
        }

        protected override void CreateItemMaterials()
        {
            itemMat = new Material[2];
            itemMatFade = new Material[2];

            itemMat[0] = new Material(Resources.Load<Material>("Materials/E2ChartUI"));
            itemMatFade[0] = new Material(itemMat[0]);
            itemMatFade[0].color = new Color(1.0f, 1.0f, 1.0f, ITEM_FADE_RATIO);

            if (options.chartStyles.areaChart.enableLine)
            {
                itemMat[1] = new Material(Resources.Load<Material>("Materials/E2ChartUBlur"));
                itemMat[1].SetFloat("_Smoothness", Mathf.Clamp01(3.0f / options.chartStyles.areaChart.lineWidth));
                itemMatFade[1] = new Material(itemMat[1]);
                itemMatFade[1].color = new Color(1.0f, 1.0f, 1.0f, ITEM_FADE_RATIO);
            }
        }

        protected override void CreateGrid()
        {
            grid = new RectGrid(this);
            grid.isInverted = options.rectOptions.inverted;
            grid.InitGrid();

            if (isLinear) InitGridLinear();
            else InitGridCategory();

            grid.UpdateGrid();
            dataInfo.valueAxis = yAxis;
            dataInfo.posAxis = xAxis;
        }

        void InitGridCategory()
        {
            E2ChartDataInfoLowerLimit dataInfo = (E2ChartDataInfoLowerLimit)this.dataInfo;

            //y axis
            if (options.yAxis.autoAxisRange)
            {
                if (options.yAxis.restrictAutoRange)
                    yAxis.Compute(dataInfo.minValue, dataInfo.maxValue, options.yAxis.axisDivision);
                else
                    yAxis.Compute(dataInfo.minValue, dataInfo.maxValue, options.yAxis.axisDivision, options.yAxis.startFromZero);
            }
            else
            {
                yAxis.Compute(options.yAxis.min, options.yAxis.max, options.yAxis.axisDivision);
            }
            yAxis.SetNumericFormat(options.yAxis.labelNumericFormat, false);
            if (options.yAxis.enableLabel)
            {
                if (yAxis.axisOptions.type == E2ChartOptions.AxisType.Category)
                {
                    List<string> texts = yAxis.GetCateTexts(data.categoriesY, true);
                    yAxis.InitContent(texts, true);
                }
                else
                {
                    List<string> texts = yAxis.GetValueTexts(dataInfo, yAxisContent);
                    yAxis.InitContent(texts, false);
                }
            }
            else yAxis.InitContent(null, false);

            //x axis
            xAxis.Compute(dataInfo.zoomMin, dataInfo.zoomMax, dataInfo.zoomMax - dataInfo.zoomMin + 1);
            if (options.xAxis.enableLabel)
            {
                List<string> texts = xAxis.GetCateTexts(data.categoriesX, false);
                xAxis.InitContent(texts, true);
            }
            else xAxis.InitContent(null, true);
        }

        void InitGridLinear()
        {
            E2ChartDataInfoLinear dataInfo = (E2ChartDataInfoLinear)this.dataInfo;

            //y axis
            if (options.yAxis.autoAxisRange)
            {
                if (options.yAxis.restrictAutoRange)
                    yAxis.Compute(dataInfo.minValue, dataInfo.maxValue, options.yAxis.axisDivision);
                else
                    yAxis.Compute(dataInfo.minValue, dataInfo.maxValue, options.yAxis.axisDivision, options.yAxis.startFromZero);
            }
            else
            {
                yAxis.Compute(options.yAxis.min, options.yAxis.max, options.yAxis.axisDivision);
            }
            yAxis.SetNumericFormat(options.yAxis.labelNumericFormat, false);
            List<string> yTexts = options.yAxis.enableLabel ? yAxis.GetValueTexts(dataInfo, yAxisContent) : null;
            yAxis.InitContent(yTexts, false);

            //x axis
            if (options.xAxis.autoAxisRange && !options.xAxis.restrictAutoRange)
            {
                xAxis.Compute(dataInfo.zoomMin, dataInfo.zoomMax, options.xAxis.axisDivision, options.xAxis.startFromZero);
            }
            else
            {
                xAxis.Compute(dataInfo.zoomMin, dataInfo.zoomMax, options.xAxis.axisDivision);
            }
            if (options.xAxis.type == E2ChartOptions.AxisType.DateTime)
            {
                E2ChartDataInfoDateTimeLowerLimit dtInfo = (E2ChartDataInfoDateTimeLowerLimit)this.dataInfo;
                xAxis.numericFormat = dtInfo.dtFormat = options.xAxis.labelNumericFormat;
                List<string> xTexts = options.xAxis.enableLabel ? xAxis.GetDateTimeTexts(dtInfo, xAxisContent) : null;
                xAxis.InitContent(xTexts, false);
            }
            else
            {
                xAxis.SetNumericFormat(options.xAxis.labelNumericFormat, false);
                List<string> xTexts = options.xAxis.enableLabel ? xAxis.GetValueTexts(dataInfo, xAxisContent) : null;
                xAxis.InitContent(xTexts, false);
            }
        }

        protected override void CreateBackground()
        {
            backgroundRect = E2ChartBuilderUtility.CreateEmptyRect("Background", contentRect, true);
            backgroundRect.SetAsFirstSibling();
            backgroundRect.offsetMin = grid.gridRect.offsetMin;
            backgroundRect.offsetMax = grid.gridRect.offsetMax;
            Image background = backgroundRect.gameObject.AddComponent<Image>();
            background.color = options.plotOptions.backgroundColor;
        }

        protected override void CreateItems()
        {
            dataRect = E2ChartBuilderUtility.CreateEmptyRect("Data", contentRect, true);
            dataRect.offsetMin = new Vector2(hAxis.minPadding, vAxis.minPadding);
            dataRect.offsetMax = new Vector2(-hAxis.maxPadding, -vAxis.maxPadding);
            if (dataInfo.activeSeriesCount == 0) return;

            if (isLinear)
            {
                CreateItemsLinear();
                CreateTrackingData();
            }
            else CreateItemsCategory();
        }

        void CreateItemsCategory()
        {
            E2ChartDataInfoLowerLimit dataInfo = (E2ChartDataInfoLowerLimit)this.dataInfo;

            //data values
            RectTransform[] seriesRect = new RectTransform[dataInfo.seriesCount];
            RectTransform[] seriesRectMasked = new RectTransform[dataInfo.seriesCount];
            float[][] dataStart = new float[dataInfo.seriesCount][];
            float[][] dataValue = new float[dataInfo.seriesCount][];
            float[][] dataValueS = new float[dataInfo.seriesCount][];
            float[][] dataBase = new float[dataInfo.seriesCount][];
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                seriesRect[i] = E2ChartBuilderUtility.CreateEmptyRect(dataInfo.seriesNames[i], dataRect, true);
                seriesRect[i].SetAsFirstSibling();
                seriesRectMasked[i] = E2ChartBuilderUtility.CreateImage("Area", seriesRect[i], false, true).rectTransform;
                seriesRectMasked[i].gameObject.AddComponent<Mask>().showMaskGraphic = false;
                dataStart[i] = new float[dataInfo.dataValue[i].Length];
                dataValue[i] = new float[dataInfo.dataValue[i].Length];
                dataValueS[i] = new float[dataInfo.dataValue[i].Length];
                dataBase[i] = new float[dataInfo.dataValue[i].Length];
            }
            dataInfo.GetValueRatio(dataStart, dataValue, dataValueS, dataBase);

            areaList = new E2ChartGraphicLineChartShade[dataInfo.seriesCount];
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                GameObject areaGo = E2ChartBuilderUtility.CreateEmptyRect("Area", seriesRectMasked[i], true).gameObject;
                E2ChartGraphicLineChartShade area = areaGo.AddComponent<E2ChartGraphicLineChartShade>();
                area.material = itemMat[0];
                Color c = GetColor(i); c.a = options.chartStyles.areaChart.areaOpacity;
                area.color = c;
                area.inverted = options.rectOptions.inverted;
                area.curve = options.chartStyles.areaChart.splineCurve;
                area.show = dataInfo.dataShow[i];
                area.dataStart = dataStart[i];
                area.dataValue = dataValue[i];
                area.posMin = dataInfo.zoomMin;
                area.posMax = dataInfo.zoomMax;
                areaList[i] = area;

                //batch for large data
                int batchCount = dataValue[i].Length / MAX_DATA_POINTS;
                if (batchCount > 0)
                {
                    area.startIndex = 0;
                    area.endIndex = MAX_DATA_POINTS - 1;
                    for (int n = 0; n < batchCount; ++n)
                    {
                        E2ChartGraphicLineChartShade batchItem = GameObject.Instantiate(area, seriesRect[i]);
                        batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                        batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                    }
                }
            }

            //line
            if (options.chartStyles.areaChart.enableLine)
            {
                lineList = new E2ChartGraphicLineChartLine[dataInfo.seriesCount];
                lineListL = new E2ChartGraphicLineChartLine[dataInfo.seriesCount];
                for (int i = 0; i < dataInfo.seriesCount; ++i)
                {
                    if (dataInfo.activeDataCount[i] == 0) continue;

                    GameObject lineGo = E2ChartBuilderUtility.CreateEmptyRect("Line", seriesRectMasked[i], true).gameObject;
                    E2ChartGraphicLineChartLine line = lineGo.AddComponent<E2ChartGraphicLineChartLine>();
                    line.material = itemMat[1];
                    line.color = options.chartStyles.areaChart.lineColor;
                    line.width = options.chartStyles.areaChart.lineWidth;
                    line.inverted = options.rectOptions.inverted;
                    line.curve = options.chartStyles.areaChart.splineCurve;
                    line.show = dataInfo.dataShow[i];
                    line.dataStart = dataStart[i];
                    line.dataValue = dataValue[i];
                    line.posMin = dataInfo.zoomMin;
                    line.posMax = dataInfo.zoomMax;
                    lineList[i] = line;

                    GameObject lineLGo = E2ChartBuilderUtility.CreateEmptyRect("LineL", seriesRectMasked[i], true).gameObject;
                    E2ChartGraphicLineChartLine lineL = lineLGo.AddComponent<E2ChartGraphicLineChartLine>();
                    lineL.material = itemMat[1];
                    lineL.color = options.chartStyles.areaChart.lineColor;
                    lineL.width = options.chartStyles.areaChart.lineWidth;
                    lineL.inverted = options.rectOptions.inverted;
                    lineL.curve = options.chartStyles.areaChart.splineCurve;
                    lineL.show = dataInfo.dataShow[i];
                    lineL.dataStart = dataBase[i];
                    lineL.dataValue = dataValueS[i];
                    lineL.posMin = dataInfo.zoomMin;
                    lineL.posMax = dataInfo.zoomMax;
                    lineListL[i] = lineL;

                    //batch for large data
                    int batchCount = dataValue[i].Length / MAX_DATA_POINTS;
                    if (batchCount > 0)
                    {
                        line.startIndex = 0;
                        line.endIndex = MAX_DATA_POINTS - 1;
                        for (int n = 0; n < batchCount; ++n)
                        {
                            E2ChartGraphicLineChartLine batchItem = GameObject.Instantiate(line, seriesRect[i]);
                            batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                            batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                        }

                        lineL.startIndex = 0;
                        lineL.endIndex = MAX_DATA_POINTS - 1;
                        for (int n = 0; n < batchCount; ++n)
                        {
                            E2ChartGraphicLineChartLine batchItem = GameObject.Instantiate(lineL, seriesRect[i]);
                            batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                            batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                        }
                    }
                }
            }
        }

        void CreateItemsLinear()
        {
            E2ChartDataInfoLinear dataInfo = (E2ChartDataInfoLinear)this.dataInfo;

            //data values
            RectTransform[] seriesRect = new RectTransform[dataInfo.seriesCount];
            RectTransform[] seriesRectMasked = new RectTransform[dataInfo.seriesCount];
            float[][] dataStart = new float[dataInfo.seriesCount][];
            float[][] dataValue = new float[dataInfo.seriesCount][];
            float[][] dataValueS = new float[dataInfo.seriesCount][];
            float[][] dataBase = new float[dataInfo.seriesCount][];
            float[][] dataPos = new float[dataInfo.seriesCount][];
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                seriesRect[i] = E2ChartBuilderUtility.CreateEmptyRect(dataInfo.seriesNames[i], dataRect, true);
                seriesRect[i].SetAsFirstSibling();
                seriesRectMasked[i] = E2ChartBuilderUtility.CreateImage("Line", seriesRect[i], false, true).rectTransform;
                seriesRectMasked[i].gameObject.AddComponent<Mask>().showMaskGraphic = false;
                dataStart[i] = new float[dataInfo.dataValue[i].Length];
                dataValue[i] = new float[dataInfo.dataValue[i].Length];
                dataValueS[i] = new float[dataInfo.dataValue[i].Length];
                dataBase[i] = new float[dataInfo.dataValue[i].Length];
                dataPos[i] = new float[dataInfo.dataValue[i].Length];
            }

            if (isDateTime) ((E2ChartDataInfoDateTimeLowerLimit)dataInfo).GetValueRatio(dataStart, dataValue, dataValueS, dataBase);
            else ((E2ChartDataInfoLinearLowerLimit)dataInfo).GetValueRatio(dataStart, dataValue, dataValueS, dataBase);
            dataInfo.GetPosRatio(dataPos);

            areaList = new E2ChartGraphicLineChartShadeLinear[dataInfo.seriesCount];
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                GameObject areaGo = E2ChartBuilderUtility.CreateEmptyRect("Area", seriesRectMasked[i], true).gameObject;
                E2ChartGraphicLineChartShadeLinear area = areaGo.AddComponent<E2ChartGraphicLineChartShadeLinear>();
                area.material = itemMat[0];
                Color c = GetColor(i); c.a = options.chartStyles.areaChart.areaOpacity;
                area.color = c;
                area.inverted = options.rectOptions.inverted;
                area.curve = options.chartStyles.areaChart.splineCurve;
                area.show = dataInfo.dataShow[i];
                area.dataStart = dataStart[i];
                area.dataValue = dataValue[i];
                area.dataPos = dataPos[i];
                areaList[i] = area;

                //batch for large data
                int batchCount = dataValue[i].Length / MAX_DATA_POINTS;
                if (batchCount > 0)
                {
                    area.startIndex = 0;
                    area.endIndex = MAX_DATA_POINTS - 1;
                    for (int n = 0; n < batchCount; ++n)
                    {
                        E2ChartGraphicLineChartShadeLinear batchItem = GameObject.Instantiate(area, seriesRect[i]);
                        batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                        batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                    }
                }
            }

            //line
            if (options.chartStyles.areaChart.enableLine)
            {
                lineList = new E2ChartGraphicLineChartLineLinear[dataInfo.seriesCount];
                lineListL = new E2ChartGraphicLineChartLineLinear[dataInfo.seriesCount];
                for (int i = 0; i < dataInfo.seriesCount; ++i)
                {
                    if (dataInfo.activeDataCount[i] == 0) continue;

                    GameObject lineGo = E2ChartBuilderUtility.CreateEmptyRect("Line", seriesRectMasked[i], true).gameObject;
                    E2ChartGraphicLineChartLineLinear line = lineGo.AddComponent<E2ChartGraphicLineChartLineLinear>();
                    line.material = itemMat[1];
                    line.color = options.chartStyles.areaChart.lineColor;
                    line.width = options.chartStyles.areaChart.lineWidth;
                    line.inverted = options.rectOptions.inverted;
                    line.curve = options.chartStyles.areaChart.splineCurve;
                    line.show = dataInfo.dataShow[i];
                    line.dataStart = dataStart[i];
                    line.dataValue = dataValue[i];
                    line.dataPos = dataPos[i];
                    lineList[i] = line;

                    GameObject lineLGo = E2ChartBuilderUtility.CreateEmptyRect("LineL", seriesRectMasked[i], true).gameObject;
                    E2ChartGraphicLineChartLineLinear lineL = lineLGo.AddComponent<E2ChartGraphicLineChartLineLinear>();
                    lineL.material = itemMat[1];
                    lineL.color = options.chartStyles.areaChart.lineColor;
                    lineL.width = options.chartStyles.areaChart.lineWidth;
                    lineL.inverted = options.rectOptions.inverted;
                    lineL.curve = options.chartStyles.areaChart.splineCurve;
                    lineL.show = dataInfo.dataShow[i];
                    lineL.dataStart = dataBase[i];
                    lineL.dataValue = dataValueS[i];
                    lineL.dataPos = dataPos[i];
                    lineListL[i] = lineL;

                    //batch for large data
                    int batchCount = dataValue[i].Length / MAX_DATA_POINTS;
                    if (batchCount > 0)
                    {
                        line.startIndex = 0;
                        line.endIndex = MAX_DATA_POINTS - 1;
                        for (int n = 0; n < batchCount; ++n)
                        {
                            E2ChartGraphicLineChartLine batchItem = GameObject.Instantiate(line, seriesRect[i]);
                            batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                            batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                        }

                        lineL.startIndex = 0;
                        lineL.endIndex = MAX_DATA_POINTS - 1;
                        for (int n = 0; n < batchCount; ++n)
                        {
                            E2ChartGraphicLineChartLine batchItem = GameObject.Instantiate(lineL, seriesRect[i]);
                            batchItem.startIndex = MAX_DATA_POINTS * (n + 1);
                            batchItem.endIndex = batchItem.startIndex + MAX_DATA_POINTS - 1;
                        }
                    }
                }
            }
        }

        void CreateTrackingData()
        {
            trackingList = new List<int>();
            trackingDict = new SortedDictionary<int, int[]>();

            //caculate tracking dictionary
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;
                for (int j = 0; j < dataInfo.dataValue[i].Length; ++j)
                {
                    if (!dataInfo.dataShow[i][j]) continue;
                    E2ChartGraphicLineChartShadeLinear area = (E2ChartGraphicLineChartShadeLinear)areaList[i];
                    int posX = Mathf.RoundToInt(area.dataPos[j] * xAxis.axisLength);
                    if (!trackingDict.ContainsKey(posX))
                    {
                        int[] info = new int[dataInfo.seriesCount];
                        for (int k = 0; k < info.Length; ++k) info[k] = -1;
                        trackingDict.Add(posX, info);
                    }
                    trackingDict[posX][i] = j;
                }
            }

            //add tracking list
            int counter = 0;
            List<int> trackingKeys = new List<int>(trackingDict.Keys);
            for (int i = 0; i < trackingKeys.Count - 1; ++i)
            {
                int j = i + 1;
                float mid = (trackingKeys[i] + trackingKeys[j]) * 0.5f;
                while (counter < trackingKeys[j])
                {
                    if (counter < mid) trackingList.Add(trackingKeys[i]);
                    else trackingList.Add(trackingKeys[j]);
                    counter++;
                }
            }
            if (trackingKeys.Count > 0) //add last
            {
                while (counter < xAxis.axisLength)
                {
                    trackingList.Add(trackingKeys[trackingKeys.Count - 1]);
                    counter++;
                }
            }
        }

        protected override void CreateLabels()
        {
            labelRect = E2ChartBuilderUtility.CreateEmptyRect("Labels", contentRect, true);
            labelRect.offsetMin = dataRect.offsetMin;
            labelRect.offsetMax = dataRect.offsetMax;

            //template
            float labelRotation = E2ChartBuilderUtility.GetLabelRotation(options.label.rotationMode);
            E2ChartText labelTemp = E2ChartBuilderUtility.CreateText("Label", labelRect, options.label.textOption, options.plotOptions.generalFont);
            labelTemp.rectTransform.anchorMin = Vector2.zero;
            labelTemp.rectTransform.anchorMax = Vector2.zero;
            labelTemp.rectTransform.sizeDelta = Vector2.zero;
            labelTemp.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, labelRotation);

            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                int sIndex = isLinear ? 0 : ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin;
                int eIndes = isLinear ? dataInfo.dataValue[i].Length - 1 : ((E2ChartDataInfoLowerLimit)dataInfo).zoomMax;

                RectTransform seriesLabelRect = E2ChartBuilderUtility.CreateEmptyRect(dataInfo.seriesNames[i], labelRect, true);
                seriesLabelRect.SetAsFirstSibling();
                for (int j = sIndex; j <= eIndes; ++j)
                {
                    if (!dataInfo.dataShow[i][j]) continue;
                    Vector2 anchor = GetItemAnchorPosition(i, j, options.label.anchoredPosition);
                    if (!IsAnchorPointInsideRect(anchor)) continue;

                    float offset = options.label.offset * Mathf.Sign(areaList[i].dataValue[j]);
                    E2ChartText label = GameObject.Instantiate(labelTemp, seriesLabelRect);
                    label.text = dataInfo.GetLabelText(labelContent, options.label.numericFormat, i, j);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
                    label.rectTransform.anchoredPosition = options.rectOptions.inverted ? new Vector2(offset, 0.0f) : new Vector2(0.0f, offset);
                }
            }

            E2ChartBuilderUtility.Destroy(labelTemp.gameObject);
        }

        protected override void CreateHighlight()
        {
            float width = isLinear ? xAxis.axisLength * 0.02f : xAxis.unitLength;
            highlight = E2ChartBuilderUtility.CreateImage("Highlight", backgroundRect);
            highlight.color = options.plotOptions.itemHighlightColor;
            if (options.rectOptions.inverted)
            {
                highlight.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                highlight.rectTransform.anchorMax = new Vector2(1.0f, 0.0f);
                highlight.rectTransform.offsetMin = new Vector2(yAxis.minPadding, -width * 0.5f);
                highlight.rectTransform.offsetMax = new Vector2(-yAxis.maxPadding, width * 0.5f);
            }
            else
            {
                highlight.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
                highlight.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                highlight.rectTransform.offsetMin = new Vector2(-width * 0.5f, yAxis.minPadding);
                highlight.rectTransform.offsetMax = new Vector2(width * 0.5f, -yAxis.maxPadding);
            }
            highlight.gameObject.SetActive(false);
        }

        protected override void UpdateHighlight()
        {
            if (currData < 0)
            {
                highlight.gameObject.SetActive(false);
            }
            else
            {
                if (isLinear)
                {
                    float posX = currTracking + xAxis.minPadding;
                    if (options.rectOptions.inverted)
                    {
                        highlight.rectTransform.anchoredPosition = new Vector2(highlight.rectTransform.anchoredPosition.x, posX);
                    }
                    else
                    {
                        highlight.rectTransform.anchoredPosition = new Vector2(posX, highlight.rectTransform.anchoredPosition.y);
                    }
                }
                else
                {
                    float posX = xAxis.unitLength * (currData - ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin + 0.5f) + xAxis.minPadding;
                    if (options.rectOptions.inverted)
                    {
                        highlight.rectTransform.sizeDelta = new Vector2(highlight.rectTransform.sizeDelta.x, xAxis.unitLength);
                        highlight.rectTransform.anchoredPosition = new Vector2(highlight.rectTransform.anchoredPosition.x, posX);
                    }
                    else
                    {
                        highlight.rectTransform.sizeDelta = new Vector2(xAxis.unitLength, highlight.rectTransform.sizeDelta.y);
                        highlight.rectTransform.anchoredPosition = new Vector2(posX, highlight.rectTransform.anchoredPosition.y);
                    }
                }
                highlight.gameObject.SetActive(true);
            }

            if (isLinear || options.plotOptions.mouseTracking == E2ChartOptions.MouseTracking.BySeries)
            {
                if (currSeries < 0)
                {
                    for (int i = 0; i < areaList.Length; ++i)
                    {
                        if (dataInfo.activeDataCount[i] == 0) continue;
                        areaList[i].material = itemMat[0];
                        if (lineList != null) lineList[i].material = itemMat[1];
                        if (lineListL != null) lineListL[i].material = itemMat[1];
                    }
                }
                else
                {
                    for (int i = 0; i < areaList.Length; ++i)
                    {
                        if (dataInfo.activeDataCount[i] == 0) continue;
                        if (i == currSeries)
                        {
                            areaList[i].material = itemMat[0];
                            if (lineList != null) lineList[i].material = itemMat[1];
                            if (lineListL != null) lineListL[i].material = itemMat[1];
                        }
                        else
                        {
                            areaList[i].material = itemMatFade[0];
                            if (lineList != null) lineList[i].material = itemMatFade[1];
                            if (lineListL != null) lineListL[i].material = itemMatFade[1];
                        }
                    }
                }
            }
        }

        protected override void UpdateTooltip()
        {
            if (!isLinear && options.plotOptions.mouseTracking == E2ChartOptions.MouseTracking.ByCategory)
            {
                if (currData < 0)
                {
                    tooltip.SetActive(false);
                }
                else
                {
                    tooltip.headerText = dataInfo.GetTooltipHeaderText(tooltipHeaderContent, currSeries, currData);
                    tooltip.pointText.Clear();
                    for (int i = 0; i < dataInfo.seriesCount; ++i)
                    {
                        if (dataInfo.activeDataCount[i] == 0 || !dataInfo.dataShow[i][currData]) continue;
                        tooltip.pointText.Add(dataInfo.GetLabelText(tooltipPointContent, options.tooltip.numericFormat, i, currData));
                    }
                    tooltip.Refresh();
                    tooltip.SetActive(true, true);
                }
            }
            else
            {
                if (currData < 0 || currSeries < 0)
                {
                    tooltip.SetActive(false);
                }
                else
                {
                    tooltip.headerText = dataInfo.GetTooltipHeaderText(tooltipHeaderContent, currSeries, currData);
                    tooltip.pointText.Clear();
                    tooltip.pointText.Add(dataInfo.GetLabelText(tooltipPointContent, options.tooltip.numericFormat, currSeries, currData));
                    tooltip.Refresh();

                    Vector2 pos = GetItemPosition(currSeries, currData, 0.5f) - pointerHandler.rectTransform.rect.size * 0.5f;
                    pos += new Vector2(hAxis.minPadding, vAxis.minPadding);
                    tooltip.SetPosition(ChartToTooltipPosition(pos), Mathf.Sign(areaList[currSeries].dataValue[currData]));
                    tooltip.SetActive(true, false);
                }
            }
        }

        protected override void UpdatePointer(Vector2 mousePosition)
        {
            mousePosition += pointerHandler.rectTransform.rect.size * 0.5f;
            mousePosition = options.rectOptions.inverted ? new Vector2(mousePosition.y, mousePosition.x) : mousePosition;
            mousePosition -= new Vector2(hAxis.minPadding, vAxis.minPadding);
            currData = -1;
            currSeries = -1;
            if (mousePosition.x < 0 || mousePosition.x >= xAxis.axisLength || mousePosition.y < 0 || mousePosition.y >= yAxis.axisLength) return;
            if (isLinear) UpdatePointerLinear(mousePosition);
            else UpdatePointerCategory(mousePosition);
        }

        void UpdatePointerCategory(Vector2 mousePosition)
        {
            currData = Mathf.FloorToInt(mousePosition.x / xAxis.unitLength) + ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin;
            if (options.plotOptions.mouseTracking == E2ChartOptions.MouseTracking.ByCategory) return;

            float min = float.MaxValue;
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0 || !dataInfo.dataShow[i][currData]) continue;

                E2ChartGraphicLineChartShade area = areaList[i];
                float posY = (area.dataStart[currData] + area.dataValue[currData] * 0.5f) * yAxis.axisLength;
                float dif = Mathf.Abs(mousePosition.y - posY);
                if (dif < min) { min = dif; currSeries = i; }
            }
        }

        void UpdatePointerLinear(Vector2 mousePosition)
        {
            float min = float.MaxValue;
            currTracking = trackingList[Mathf.RoundToInt(mousePosition.x)];
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;
                int dataIndex = trackingDict[currTracking][i];
                if (dataIndex < 0) continue;

                E2ChartGraphicLineChartShadeLinear area = (E2ChartGraphicLineChartShadeLinear)areaList[i];
                float posY = (area.dataStart[dataIndex] + area.dataValue[dataIndex] * 0.5f) * yAxis.axisLength;
                float dif = Mathf.Abs(mousePosition.y - posY);
                if (dif < min) { min = dif; currSeries = i; currData = dataIndex; }
            }
        }

        protected override void UpdateZoomRangeBegin(Vector2 mousePosition)
        {
            if (currData < 0) return; //outside chart area
            beginZoomMin = isLinear ? ((E2ChartDataInfoLinearLowerLimit)dataInfo).zoomMin : ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin;
            lastMouse = currMouse = 0;
        }

        protected override void UpdateZoomRange(Vector2 dragDistance)
        {
            dragDistance = options.rectOptions.inverted ? new Vector2(dragDistance.y, dragDistance.x) : dragDistance;
            float xUnitLength = isLinear ? 1 : xAxis.unitLength;
            if (xUnitLength < MIN_ZOOM_UNIT_LENGTH) xUnitLength = MIN_ZOOM_UNIT_LENGTH;
            currMouse = Mathf.RoundToInt(dragDistance.x / xUnitLength);
            if (currMouse == lastMouse) return;
            lastMouse = currMouse;

            bool zoomUpdated = false;
            if (isLinear)
            {
                E2ChartDataInfoLinearLowerLimit dataInfo = (E2ChartDataInfoLinearLowerLimit)this.dataInfo;
                float unitRange = (dataInfo.zoomMax - dataInfo.zoomMin) / xAxis.axisLength;
                float dragData = dragDistance.x * unitRange;
                float targetZoomMin = beginZoomMin - dragData;
                zoomUpdated = dataInfo.SetZoomMin(targetZoomMin);
            }
            else
            {
                int targetZoomMin = (int)beginZoomMin - currMouse;
                zoomUpdated = ((E2ChartDataInfoLowerLimit)dataInfo).SetZoomMin(targetZoomMin);
            }
            if (!zoomUpdated) return;

            ClearContent();
            Build();
        }

        protected override void UpdateZoom(float zoomValue)
        {
            if (currData < 0) return; //outside chart area
            bool zoomUpdated = isLinear ?
                ((E2ChartDataInfoLinearLowerLimit)dataInfo).AddZoom(zoomValue * ZOOM_SENSTIVITY) :
                ((E2ChartDataInfoLowerLimit)dataInfo).AddZoom(zoomValue * ZOOM_SENSTIVITY);
            if (!zoomUpdated) return;

            ClearContent();
            Build();
        }

        protected override Vector2 GetPointerValue(Vector2 mousePosition)
        {
            mousePosition += pointerHandler.rectTransform.rect.size * 0.5f;
            mousePosition = options.rectOptions.inverted ? new Vector2(mousePosition.y, mousePosition.x) : mousePosition;
            mousePosition -= new Vector2(hAxis.minPadding, vAxis.minPadding);
            Vector2 value = new Vector2();
            value.y = yAxis.GetValue(mousePosition.y / yAxis.axisLength);
            if (isLinear) value.x = xAxis.GetValue(mousePosition.x / xAxis.axisLength);
            return value;
        }

        protected override Vector2 GetItemPosition(int seriesIndex, int dataIndex, float ratio = 1.0f)
        {
            Vector2 position = new Vector2();
            if (isLinear)
            {
                E2ChartGraphicLineChartShadeLinear area = (E2ChartGraphicLineChartShadeLinear)areaList[seriesIndex];
                float posX = area.dataPos[dataIndex] * xAxis.axisLength;
                float posY = (area.dataStart[dataIndex] + area.dataValue[dataIndex] * ratio) * yAxis.axisLength;
                position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            }
            else
            {
                E2ChartGraphicLineChartShade area = areaList[seriesIndex];
                float posX = xAxis.unitLength * (dataIndex - ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin + 0.5f);
                float posY = (area.dataStart[dataIndex] + area.dataValue[dataIndex] * ratio) * yAxis.axisLength;
                position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            }
            return position;
        }

        Vector2 GetItemAnchorPosition(int seriesIndex, int dataIndex, float ratio = 1.0f)
        {
            Vector2 position = new Vector2();
            if (isLinear)
            {
                E2ChartGraphicLineChartShadeLinear area = (E2ChartGraphicLineChartShadeLinear)areaList[seriesIndex];
                float posX = area.dataPos[dataIndex];
                float posY = area.dataStart[dataIndex] + area.dataValue[dataIndex] * ratio;
                position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            }
            else
            {
                E2ChartGraphicLineChartShade area = areaList[seriesIndex];
                float posX = (xAxis.unitLength * (dataIndex - ((E2ChartDataInfoLowerLimit)dataInfo).zoomMin + 0.5f)) / xAxis.axisLength;
                float posY = area.dataStart[dataIndex] + area.dataValue[dataIndex] * ratio;
                position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            }
            return position;
        }
    }
}