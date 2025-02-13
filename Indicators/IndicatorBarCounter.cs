// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace BarsDataIndicators;
public class IndicatorBarCounter : Indicator
{
    private int maxCount = 12;
    private Font labelFont;
    private Color labelColor = Color.Gray;
    private bool fromStart = true;
    private bool useTimeReset = false;
    private DateTime resetTime = new DateTime();
    private int verticalOffset = 0;
    private LabelPosition labelPosition = LabelPosition.Bottom;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorBarCounter.cs";

    public IndicatorBarCounter()
        : base()
    {
        Name = "Bar Counter";
        SeparateWindow = false;
        labelFont = new Font("Verdana", 12);
    }

    protected override void OnInit()
    {
    }

    protected override void OnUpdate(UpdateArgs args)
    {
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        Graphics graphics = args.Graphics;
        RectangleF prevClipRectangle = graphics.ClipBounds;
        graphics.SetClip(args.Rectangle);
        try
        {
            if (this.HistoricalData.Aggregation is HistoryAggregationTick)
            {
                graphics.DrawString("Indicator does not work on tick aggregation", new Font("Arial", 20), Brushes.Red, 20, 50);
                return;
            }
            var mainWindow = this.CurrentChart.MainWindow;
            DateTime leftBorderTime = mainWindow.CoordinatesConverter.GetTime(0);
            DateTime rightBorderTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Width);
            int leftIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(leftBorderTime);
            int rightIndex = (int)mainWindow.CoordinatesConverter.GetBarIndex(rightBorderTime);
            if (leftIndex < 0)
                leftIndex = 0;
            if (rightIndex >= this.HistoricalData.Count)
                rightIndex = this.HistoricalData.Count - 1;
            Brush labelBrush = new SolidBrush(labelColor);
            leftIndex = this.HistoricalData.Count - leftIndex - 1;
            rightIndex = this.HistoricalData.Count - rightIndex - 1;
            int counter = 0;
            counter = this.fromStart ? (this.HistoricalData.Count - leftIndex - 1) % this.maxCount : (leftIndex + 2) % this.maxCount;
            if (counter == 0)
                counter = maxCount;
            DateTime currResetTime;
            for (int i = leftIndex; i >= rightIndex; i--)
            {
                var currBar = (HistoryItemBar)this.HistoricalData[i];
                PointF labelPoint = new PointF();
                labelPoint.X = (float)mainWindow.CoordinatesConverter.GetChartX(this.HistoricalData[i].TimeLeft) + +this.CurrentChart.BarsWidth / 2;
                switch (labelPosition)
                {
                    case LabelPosition.Top:
                        labelPoint.Y = (float)mainWindow.CoordinatesConverter.GetChartY(currBar.High);
                        break;
                    case LabelPosition.Bottom:
                        labelPoint.Y = (float)mainWindow.CoordinatesConverter.GetChartY(currBar.Low);
                        break;
                    case LabelPosition.Center:
                        labelPoint.Y = (float)mainWindow.CoordinatesConverter.GetChartY((currBar.High + currBar.Low)/2);
                        break;
                    default:
                        break;
                }
                if (this.fromStart)
                {
                    counter++;
                    if (counter > maxCount)
                        counter = 1;
                }
                else
                {
                    counter--;
                    if (counter <= 0)
                        counter = maxCount;
                }
                if (this.useTimeReset)
                {
                    currResetTime = new DateTime(currBar.TimeLeft.ToLocalTime().Year, currBar.TimeLeft.ToLocalTime().Month, currBar.TimeLeft.ToLocalTime().Day, this.resetTime.ToLocalTime().Hour, this.resetTime.Minute, this.resetTime.Second);
                    int resetIndex = (int)this.HistoricalData.GetIndexByTime(currResetTime.FromSelectedTimeZoneToUtc().Ticks);
                    if (i == resetIndex)
                        counter = 1;
                }
                SizeF labelSize = graphics.MeasureString(counter.ToString(), this.labelFont);
                labelPoint.X -= labelSize.Width / 2;
                labelPoint.Y = this.labelPosition == LabelPosition.Bottom ? labelPoint.Y : labelPoint.Y - labelSize.Height;
                labelPoint.Y += this.verticalOffset;
                graphics.DrawString(counter.ToString(), this.labelFont, labelBrush, labelPoint);

            }

        }
        finally
        {
            graphics.SetClip(prevClipRectangle);
        }

    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemInteger("maxCount", this.maxCount)
            {
                Text = "Period",
                SortIndex = 1,
                Minimum = 2,
            });
            settings.Add(new SettingItemBoolean("fromStart", this.fromStart)
            {
                Text = "Start From Beginning",
                SortIndex = 1,
            });
            settings.Add(new SettingItemFont("Font", this.labelFont)
            {
                Text = "Label Font",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("labelColor", this.labelColor)
            {
                Text = "Label Color",
                SortIndex = 2
            });
            settings.Add(new SettingItemSelectorLocalized("labelPosition", this.labelPosition, new List<SelectItem> { new SelectItem("Top", LabelPosition.Top), new SelectItem("Bottom", LabelPosition.Bottom), new SelectItem("Center", LabelPosition.Center) })
            {
                Text = "Label Location",
                SortIndex = 2,
            });
            settings.Add(new SettingItemInteger("verticalOffset", this.verticalOffset)
            {
                Text = "Vertical Offset",
                SortIndex = 2,
            });
            settings.Add(new SettingItemBoolean("useTimeReset", this.useTimeReset)
            {
                Text = "Reset Counter on Time",
                SortIndex = 3,
            });
            SettingItemRelationVisibility visibleRelationTR = new SettingItemRelationVisibility("useTimeReset", true);
            settings.Add(new SettingItemDateTime("resetTime", this.resetTime)
            {
                Text = "Counter Reset Time",
                SortIndex = 3,
                Format = DatePickerFormat.Time,
                Relation = visibleRelationTR
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("maxCount", out int maxCount))
                this.maxCount = maxCount;
            if (value.TryGetValue("fromStart", out bool fromStart))
                this.fromStart = fromStart;
            if (value.TryGetValue("Font", out Font labelFont))
                this.labelFont = labelFont;
            if (value.TryGetValue("labelColor", out Color labelColor))
                this.labelColor = labelColor;
            if (value.TryGetValue("labelPosition", out LabelPosition labelPosition))
                this.labelPosition = labelPosition;
            if (value.TryGetValue("verticalOffset", out int verticalOffset))
                this.verticalOffset = verticalOffset;
            if (value.TryGetValue("useTimeReset", out bool useTimeReset))
                this.useTimeReset = useTimeReset;
            if (value.TryGetValue("resetTime", out DateTime resetTime))
                this.resetTime = resetTime;

            this.OnSettingsUpdated();
        }
    }
}
internal enum LabelPosition
{
    Top,
    Bottom,
    Center
}
