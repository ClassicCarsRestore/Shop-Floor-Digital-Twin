using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace E2C.ChartGraphic
{
    public class E2ChartGraphicHeatmap : E2ChartGraphic
    {
        const int LOOKUP_COLOR_COUNT = 256;

        public Vector2 spacing = new Vector2(2.0f, 2.0f);
        public float[][] dataValue;
        public bool[][] show;
        public int[] activeDataCount;
        public int activeSeriesCount;
        public int maxDataCount;
        public E2ChartOptions.HeatmapColor[] colors;
        public bool inverted = false;

        Color[] lookupTable;
        List<E2ChartOptions.HeatmapColor> colorList;
        float minValue { get => colorList[0].value; }
        float maxValue { get => colorList[colorList.Count - 1].value; }
        Color minColor { get => colorList[0].color; }
        Color maxColor { get => colorList[colorList.Count - 1].color; }

        protected override void Awake()
        {
            base.Awake();
            rectTransform.pivot = Vector2.zero;
        }

        Color LookupColor(float value)
        {
            if (value <= minValue) return minColor;
            if (value >= maxValue) return maxColor;

            int a = 0, b = 0;
            if (colorList.Count > 1)
            {
                for (int i = 0; i < colorList.Count; ++i)
                {
                    if (value >= colorList[i].value && value < colorList[i + 1].value)
                    {
                        a = i;
                        b = i + 1;
                        break;
                    }
                }
            }

            return E2ChartOptions.HeatmapColor.GetColor(colorList[a], colorList[b], value);
        }

        private static int CompareHeatmapColor(E2ChartOptions.HeatmapColor a, E2ChartOptions.HeatmapColor b)
        {
            if (a.value < b.value) return -1;
            else if (a.value == b.value) return 0;
            else return 1;
        }

        Color GetColor(float value)
        {
            float t = Mathf.InverseLerp(minValue, maxValue, value);
            int index = Mathf.FloorToInt((LOOKUP_COLOR_COUNT - 1) * t);
            return lookupTable[index];
        }

        public override void RefreshBuffer()
        {
            //for testing
            //dataValue = new float[4][];
            //dataValue[0] = new float[] { 1, 2, 3 };
            //dataValue[1] = new float[] { 1, 2, 3 };
            //dataValue[2] = new float[] { 1, 2, 3 };
            //dataValue[3] = new float[] { 1, 2, 3 };
            //show = new bool[4][];
            //show[0] = new bool[] { true, true, true };
            //show[1] = new bool[] { true, false, true };
            //show[2] = new bool[] { true, false, true };
            //show[3] = new bool[] { true, true, true };

            if (dataValue == null || dataValue.Length == 0 ||
                activeDataCount == null || activeDataCount.Length != dataValue.Length ||
                show == null || show.Length != dataValue.Length ||
                colors == null || colors.Length == 0 || 
                maxDataCount == 0 || activeSeriesCount == 0)
            { isDirty = true; inited = false; return; }

            lookupTable = new Color[LOOKUP_COLOR_COUNT];
            colorList = new List<E2ChartOptions.HeatmapColor>(colors);
            if (colorList.Count == 0) colorList.Add(new E2ChartOptions.HeatmapColor());
            colorList.Sort(CompareHeatmapColor);

            for (int i = 0; i < LOOKUP_COLOR_COUNT; ++i)
            {
                float value = Mathf.Lerp(minValue, maxValue, i / (float)LOOKUP_COLOR_COUNT);
                lookupTable[i] = LookupColor(value);
            }

            isDirty = false;
            inited = true;
        }

        protected override void GenerateMesh()
        {
            Vector2[] points = new Vector2[4];

            if (inverted)
            {
                Vector2 unit = new Vector2(rectSize.y / maxDataCount, rectSize.x / activeSeriesCount);
                Vector2 size = new Vector2(unit.x - spacing.y, unit.y - spacing.x);
                Vector2 offset = new Vector2(spacing.y, spacing.x) * 0.5f;
                int activeCount = 0;
                for (int i = 0; i < dataValue.Length; ++i)
                {
                    if (activeDataCount[i] == 0) continue;
                    for (int j = 0; j < dataValue[i].Length; ++j)
                    {
                        if (!show[i][j]) continue;

                        float posX = unit.x * j + offset.x;
                        float posY = unit.y * activeCount + offset.y;

                        points[0] = new Vector2(posY, posX);
                        points[1] = new Vector2(posY + size.y, posX);
                        points[2] = new Vector2(posY + size.y, posX + size.x);
                        points[3] = new Vector2(posY, posX + size.x);

                        AddPolygonRect(points, GetColor(dataValue[i][j]));
                    }
                    activeCount++;
                }
            }
            else
            {
                Vector2 unit = new Vector2(rectSize.x / maxDataCount, rectSize.y / activeSeriesCount);
                Vector2 size = new Vector2(unit.x - spacing.x, unit.y - spacing.y);
                Vector2 offset = new Vector2(spacing.x, spacing.y) * 0.5f;
                int activeCount = 0;
                for (int i = 0; i < dataValue.Length; ++i)
                {
                    if (activeDataCount[i] == 0) continue;
                    for (int j = 0; j < dataValue[i].Length; ++j)
                    {
                        if (!show[i][j]) continue;

                        float posX = unit.x * j + offset.x;
                        float posY = unit.y * activeCount + offset.y;

                        points[0] = new Vector2(posX, posY);
                        points[1] = new Vector2(posX, posY + size.y);
                        points[2] = new Vector2(posX + size.x, posY + size.y);
                        points[3] = new Vector2(posX + size.x, posY);

                        AddPolygonRect(points, GetColor(dataValue[i][j]));
                    }
                    activeCount++;
                }
            }
        }
    }
}