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
    public class TableBuilder : E2ChartBuilder
    {
        Image highlight;

        public TableBuilder(E2Chart c) : base(c) { }

        public TableBuilder(E2Chart c, RectTransform rect) : base(c, rect) { }

        protected override void CreateDataInfo()
        {
            dataInfo = new E2ChartDataInfo(data);
            legendContent = string.IsNullOrEmpty(options.legend.content) ? "{series}" : options.legend.content;
            tooltipHeaderContent = string.IsNullOrEmpty(options.tooltip.headerContent) ? "{category}" : options.tooltip.headerContent;
            tooltipPointContent = string.IsNullOrEmpty(options.tooltip.pointContent) ? "{series}: {dataY}" : options.tooltip.pointContent;
        }

        protected override void CreateItemMaterials()
        {

        }

        protected override void CreateGrid()
        {
            grid = new RectGrid(this);
            grid.isInverted = options.rectOptions.inverted;
            grid.InitGrid();

            //y axis
            yAxis.Compute(0, dataInfo.activeSeriesCount, dataInfo.activeSeriesCount);
            if (options.yAxis.enableLabel)
            {
                List<string> texts = dataInfo.GetActiveSeriesNames();
                yAxis.InitContent(texts, true);
            }
            else yAxis.InitContent(null, true);

            //x axis
            xAxis.Compute(0, dataInfo.maxDataCount, dataInfo.maxDataCount);
            if (options.xAxis.enableLabel)
            {
                List<string> texts = xAxis.GetCateTexts(data.categoriesX, true);
                xAxis.InitContent(texts, true);
            }
            else xAxis.InitContent(null, true);

            grid.UpdateGrid();
            dataInfo.valueAxis = yAxis;
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
            dataRect = E2ChartBuilderUtility.CreateImage("Data", contentRect, false, true).rectTransform;
            dataRect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            dataRect.offsetMin = new Vector2(hAxis.minPadding, vAxis.minPadding);
            dataRect.offsetMax = new Vector2(-hAxis.maxPadding, -vAxis.maxPadding);
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

            int activeCount = 0;
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;

                RectTransform seriesLabelRect = E2ChartBuilderUtility.CreateEmptyRect(dataInfo.seriesNames[i], labelRect, true);
                seriesLabelRect.SetAsFirstSibling();
                for (int j = 0; j < dataInfo.maxDataCount; ++j)
                {
                    if (dataInfo.activeDataCount[i] == 0) continue;
                    Vector2 anchor = GetItemAnchorPosition(activeCount, j);
                    E2ChartText label = GameObject.Instantiate(labelTemp, seriesLabelRect);
                    label.text = dataInfo.GetLabelText(labelContent, options.label.numericFormat, i, j);
                    label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
                }
                activeCount++;
            }

            E2ChartBuilderUtility.Destroy(labelTemp.gameObject);
        }

        protected override void CreateHighlight()
        {
            highlight = E2ChartBuilderUtility.CreateImage("Highlight", backgroundRect);
            highlight.color = options.plotOptions.itemHighlightColor;
            highlight.rectTransform.anchorMin = highlight.rectTransform.anchorMax = new Vector2(0.0f, 0.0f);
            if (options.rectOptions.inverted)
            {
                highlight.rectTransform.sizeDelta = new Vector2(yAxis.unitLength, xAxis.unitLength);
            }
            else
            {
                highlight.rectTransform.sizeDelta = new Vector2(xAxis.unitLength, yAxis.unitLength);
            }
            highlight.gameObject.SetActive(false);
        }

        protected override void UpdateHighlight()
        {
            if (currData < 0 || currSeries < 0)
            {
                highlight.gameObject.SetActive(false);
            }
            else
            {
                float posX = xAxis.unitLength * (currData + 0.5f) + xAxis.minPadding;
                int activeCount = 0;
                for (int i = 0; i < dataInfo.seriesCount; ++i)
                {
                    if (dataInfo.activeDataCount[i] == 0) continue;
                    if (i == currSeries) { break; }
                    activeCount++;
                }
                float posY = yAxis.unitLength * (activeCount + 0.5f) + yAxis.minPadding;
                if (options.rectOptions.inverted)
                {
                    highlight.rectTransform.anchoredPosition = new Vector2(posY, posX);
                }
                else
                {
                    highlight.rectTransform.anchoredPosition = new Vector2(posX, posY);
                }
                highlight.gameObject.SetActive(true);
            }
        }

        protected override void UpdateTooltip()
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
                tooltip.SetActive(true, true);
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

            currData = Mathf.FloorToInt(mousePosition.x / xAxis.unitLength);
            int posY = Mathf.FloorToInt(mousePosition.y / yAxis.unitLength);
            int activeCount = 0;
            for (int i = 0; i < dataInfo.seriesCount; ++i)
            {
                if (dataInfo.activeDataCount[i] == 0) continue;
                if (activeCount == posY) { currSeries = i; break; }
                activeCount++;
            }
        }

        protected override Vector2 GetPointerValue(Vector2 mousePosition)
        {
            return new Vector2();
        }

        protected override Vector2 GetItemPosition(int seriesIndex, int dataIndex, float ratio = 1.0f)
        {
            float posX = xAxis.unitLength * dataIndex;
            float posY = yAxis.unitLength * dataIndex;
            Vector2 position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            return position;
        }

        protected Vector2 GetItemAnchorPosition(int seriesIndex, int dataIndex, float ratio = 1.0f)
        {
            float posX = xAxis.unitLength * (dataIndex + 0.5f) / xAxis.axisLength;
            float posY = yAxis.unitLength * (seriesIndex + 0.5f) / yAxis.axisLength;
            Vector2 position = options.rectOptions.inverted ? new Vector2(posY, posX) : new Vector2(posX, posY);
            return position;
        }
    }
}