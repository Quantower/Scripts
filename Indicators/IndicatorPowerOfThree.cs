// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;

namespace TrendIndicators;
public class IndicatorPowerOfThree : Indicator
{
    private Period tfPeriod = Period.DAY1;
    private bool useTFPeriod = true;
    private int barsPeriod = 20;
    private int offset = 0;
    private bool useCustomBarWidth = false;
    private int customBarWidth = 10;
    public Color decreasingCandleColor
    {
        get => this.decreasingCandleBrush.Color;
        set => this.decreasingCandleBrush.Color = value;
    }
    private readonly SolidBrush decreasingCandleBrush;
    public Color growingCandleColor
    {
        get => this.growingCandleBrush.Color;
        set => this.growingCandleBrush.Color = value;
    }
    private readonly SolidBrush growingCandleBrush;
    public Color dojiCandleColor
    {
        get => this.dojiCandleBrush.Color;
        set => this.dojiCandleBrush.Color = value;
    }
    private readonly SolidBrush dojiCandleBrush;
    private bool drawBorder = true;
    private int borderWidth = 1;
    public Color decreasingBorderColor
    {
        get => this.decreasingBorderPen.Color;
        set => this.decreasingBorderPen.Color = value;
    }
    private readonly Pen decreasingBorderPen;
    public Color growingBorderColor
    {
        get => this.growingBorderPen.Color;
        set => this.growingBorderPen.Color = value;
    }
    private readonly Pen growingBorderPen;
    public Color dojiBorderColor
    {
        get => this.dojiBorderPen.Color;
        set => this.dojiBorderPen.Color = value;
    }
    private readonly Pen dojiBorderPen;
    private int wick = 1;
    public Color decreasingWickColor
    {
        get => this.decreasingWickPen.Color;
        set => this.decreasingWickPen.Color = value;
    }
    private readonly Pen decreasingWickPen;
    public Color growingWickColor
    {
        get => this.growingWickPen.Color;
        set => this.growingWickPen.Color = value;
    }
    private readonly Pen growingWickPen;
    public Color dojiWickColor
    {
        get => this.dojiWickPen.Color;
        set => this.dojiWickPen.Color = value;
    }
    private readonly Pen dojiWickPen;

    private bool showLabel = false;
    private Font labelFont;
    private HorizontalPosition horizontalPosition = HorizontalPosition.Right;
    private bool showLevelLine = false;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPowerOfThree.cs";

    private bool tfDataLoaded = false;
    private HistoricalData tfData;
    public IndicatorPowerOfThree()
        : base()
    {
        this.Name = "Power of Three";
        this.SeparateWindow = false;
        this.growingCandleBrush = new SolidBrush(Color.FromArgb(85, Color.DarkGreen));
        this.decreasingCandleBrush = new SolidBrush(Color.FromArgb(85, Color.IndianRed));
        this.dojiCandleBrush = new SolidBrush(Color.FromArgb(85, Color.Gray));
        this.growingBorderPen = new Pen(Color.FromArgb(255, Color.DarkGreen));
        this.decreasingBorderPen = new Pen(Color.FromArgb(255, Color.IndianRed));
        this.dojiBorderPen = new Pen(Color.FromArgb(255, Color.Gray));
        this.growingWickPen = new Pen(Color.FromArgb(255, Color.DarkGreen));
        this.decreasingWickPen = new Pen(Color.FromArgb(255, Color.IndianRed));
        this.dojiWickPen = new Pen(Color.FromArgb(255, Color.Gray));
        this.labelFont = new Font("Tahoma", 8);
    }
    protected override void OnInit()
    {
        if (this.useTFPeriod && !tfDataLoaded)
        {
            this.tfData = this.Symbol.GetHistory(this.tfPeriod, this.Symbol.HistoryType, 1);
            this.tfDataLoaded = true;
        }
        this.growingBorderPen.Width = this.borderWidth;
        this.decreasingBorderPen.Width = this.borderWidth;
        this.dojiBorderPen.Width = this.borderWidth;

    }
    protected override void OnUpdate(UpdateArgs args)
    {
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        var gr = args.Graphics;
        var currWindow = this.CurrentChart.Windows[args.WindowIndex];
        RectangleF prevClipRectangle = gr.ClipBounds;
        gr.SetClip(args.Rectangle);
        try
        {
            // bar width correction logic
            int barWidth = this.CurrentChart.BarsWidth;
            int barLeftOffset = 0;
            if (barWidth > 5)
            {
                if (barWidth % 2 == 1)
                {
                    barLeftOffset = 1;
                    barWidth -= 2;
                }
                else
                {
                    barWidth -= 1;
                }
            }
            RectangleF candleBody = new RectangleF();
            PointF shadowTop = new PointF();
            PointF shadowBottom = new PointF();
            SolidBrush currBodyBrush = new SolidBrush(Color.White);
            Pen currBodyPen = new Pen(currBodyBrush);
            Pen currWickPen = new Pen(currBodyBrush);
            candleBody.X = (float)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[0].TimeLeft) + this.offset + barLeftOffset + barWidth;
            candleBody.Width = this.useCustomBarWidth ? this.customBarWidth : barWidth;
            shadowTop.X = candleBody.X + candleBody.Width / 2;
            shadowBottom.X = shadowTop.X;
            HistoryItemBar resultBar = new HistoryItemBar();
            if (this.useTFPeriod)
            {
                if (this.tfData == null || this.tfData.Count == 0)
                    return;
                if (this.tfPeriod != Period.TICK1)
                {
                    HistoryItemBar lastTFBar = (HistoryItemBar)this.tfData[0];
                    resultBar.High = lastTFBar.High;
                    resultBar.Low = lastTFBar.Low;
                    resultBar.Close = lastTFBar.Close;
                    resultBar.Open = lastTFBar.Open;
                }
                else
                {
                    HistoryItemLast lastTick = (HistoryItemLast)this.tfData[0];
                    resultBar.High = lastTick.Price;
                    resultBar.Low = lastTick.Price;
                    resultBar.Close = lastTick.Price;
                    resultBar.Open = lastTick.Price;
                }
            }
            else
            {
                if (this.Count < this.barsPeriod)
                    return;
                resultBar.Close = this.Close();
                resultBar.Low = this.Low();
                resultBar.Open = this.Open(this.barsPeriod-1);
                for (int i = 0; i<this.barsPeriod; i++)
                {
                    if (this.High(i) >= resultBar.High)
                        resultBar.High = this.High(i);
                    if (this.Low(i) <= resultBar.Low)
                        resultBar.Low = this.Low(i);
                }
            }
            shadowTop.Y = (float)currWindow.CoordinatesConverter.GetChartY(resultBar.High);
            shadowBottom.Y = (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Low);
            if (resultBar.Close == resultBar.Open)
            {
                candleBody.Height = 1;
                currBodyBrush = this.dojiCandleBrush;
                currBodyPen = this.dojiBorderPen;
                currWickPen = this.dojiWickPen;
                candleBody.Y = (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Close);
            }
            else
            {
                candleBody.Y = resultBar.Close > resultBar.Open ? (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Close) : (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Open);
                currBodyBrush = resultBar.Close > resultBar.Open ? this.growingCandleBrush : this.decreasingCandleBrush;
                currBodyPen = resultBar.Close > resultBar.Open ? this.growingBorderPen : this.decreasingBorderPen;
                currWickPen = resultBar.Close > resultBar.Open ? this.growingWickPen : this.decreasingWickPen;
                candleBody.Height = Math.Abs((float)currWindow.CoordinatesConverter.GetChartY(resultBar.Close) - (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Open));
            }
            gr.FillRectangle(currBodyBrush, candleBody);
            if (this.drawBorder)
                gr.DrawRectangle(currBodyPen, candleBody);
            else
                currBodyPen = new Pen(currBodyBrush.Color, this.borderWidth);
            gr.DrawLine(currWickPen, shadowTop, shadowBottom);

            if (this.showLabel)
            {
                Brush labelBrush = new SolidBrush(currBodyPen.Color);
                PointF labelPoint = new PointF();
                labelPoint = this.GetLabelCoordinates(candleBody, resultBar.High, false, currWindow, gr);
                gr.DrawString("H:" + resultBar.High, this.labelFont, labelBrush, labelPoint);
                labelPoint = this.GetLabelCoordinates(candleBody, resultBar.Low, false, currWindow, gr);
                gr.DrawString("L:" + resultBar.Low, this.labelFont, labelBrush, labelPoint);
                labelPoint = this.GetLabelCoordinates(candleBody, resultBar.Close, true, currWindow, gr);
                gr.DrawString("C:" + resultBar.Close, this.labelFont, labelBrush, labelPoint);
                labelPoint = this.GetLabelCoordinates(candleBody, resultBar.Open, true, currWindow, gr);
                gr.DrawString("O:" + resultBar.Open, this.labelFont, labelBrush, labelPoint);
            }
            if (this.showLevelLine)
            {
                PointF endOpenLine = new PointF(shadowTop.X, (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Open));
                PointF endHighLine = new PointF(shadowTop.X, (float)currWindow.CoordinatesConverter.GetChartY(resultBar.High));
                PointF endLowLine = new PointF(shadowTop.X, (float)currWindow.CoordinatesConverter.GetChartY(resultBar.Low));
                PointF startOpenLine = new PointF(0, endOpenLine.Y);
                PointF startHighLine = new PointF(0, endHighLine.Y);
                PointF startLowLine = new PointF(0, endLowLine.Y);
                int leftIndex = 0;
                if (this.useTFPeriod)
                {
                    DateTime leftBorderTime = currWindow.CoordinatesConverter.GetTime(0);
                    if (leftBorderTime < this.tfData[0].TimeLeft)
                        leftBorderTime = this.tfData[0].TimeLeft;
                    leftIndex = (int)currWindow.CoordinatesConverter.GetBarIndex(leftBorderTime);
                }
                else
                {
                    leftIndex = this.HistoricalData.Count - 1 - this.barsPeriod;
                }
                if (leftIndex < 0)
                    leftIndex = 0;
                for (int i = this.HistoricalData.Count-1; i >= leftIndex; i--)
                {
                    HistoryItemBar currBar = (HistoryItemBar)this.HistoricalData[i, SeekOriginHistory.Begin];
                    if (currBar.High >= resultBar.High)
                        startHighLine.X = (float)currWindow.CoordinatesConverter.GetChartX(currBar.TimeLeft) + barWidth/2;
                    if (currBar.Low <= resultBar.Low)
                        startLowLine.X = (float)currWindow.CoordinatesConverter.GetChartX(currBar.TimeLeft) + barWidth / 2;
                    if (currBar.Open == resultBar.Open)
                        startOpenLine.X = (float)currWindow.CoordinatesConverter.GetChartX(currBar.TimeLeft) + barWidth / 2;

                }
                gr.DrawLine(currWickPen, startOpenLine, endOpenLine);
                gr.DrawLine(currWickPen, startHighLine, endHighLine);
                gr.DrawLine(currWickPen, startLowLine, endLowLine);
            }
        }
        finally
        {
            gr.SetClip(prevClipRectangle);
        }
    }
    private PointF GetLabelCoordinates(RectangleF candleBody, double price, bool isBody, IChartWindow currWindow, Graphics gr)
    {
        float labelY = 0;
        float labelX = 0;
        labelY = (float)currWindow.CoordinatesConverter.GetChartY(price);
        int multiplier = 1;
        float stringHeight = gr.MeasureString(price.ToString(), this.labelFont).Height;
        if (isBody)
            multiplier = 2;
        if (labelY < 0)
            labelY = 0 + stringHeight * (multiplier - 1);
        if (labelY > currWindow.ClientRectangle.Height)
            labelY = currWindow.ClientRectangle.Height-stringHeight*multiplier;
        labelX = this.horizontalPosition == HorizontalPosition.Right ? candleBody.X + (float)candleBody.Width : candleBody.X - gr.MeasureString("M:" + price, this.labelFont).Width;
        return new PointF(labelX, labelY);
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemInteger("offset", this.offset)
            {
                Text = "Drawing Offset",
                SortIndex = 1,
            });
            settings.Add(new SettingItemBoolean("useCustomBarWidth", this.useCustomBarWidth)
            {
                Text = "Custom Bar Width",
                SortIndex = 1,
            });
            SettingItemRelationVisibility customWidth = new SettingItemRelationVisibility("useCustomBarWidth", true);
            settings.Add(new SettingItemInteger("customBarWidth", this.customBarWidth)
            {
                Text = "Bar Width",
                SortIndex = 1,
                Minimum = 2,
                Relation = customWidth
            });
            settings.Add(new SettingItemBoolean("useTFPeriod", this.useTFPeriod)
            {
                Text = "Use Specified Time Frame",
                SortIndex = 1,
            });
            SettingItemRelationVisibility tfPeriodRelation = new SettingItemRelationVisibility("useTFPeriod", true);
            SettingItemRelationVisibility notTFPeriodRelation = new SettingItemRelationVisibility("useTFPeriod", false);
            settings.Add(new SettingItemInteger("barsPeriod", this.barsPeriod)
            {
                Text = "Period",
                SortIndex = 1,
                Minimum = 2,
                Relation = notTFPeriodRelation
            });
            settings.Add(new SettingItemPeriod("tfPeriod", this.tfPeriod)
            {
                Text = "Period",
                SortIndex = 1,
                Relation = tfPeriodRelation,
            });
            settings.Add(new SettingItemColor("decreasingCandleColor", this.decreasingCandleColor)
            {
                Text = "Decreasing Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("growingCandleColor", this.growingCandleColor)
            {
                Text = "Growing Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("dojiCandleColor", this.dojiCandleColor)
            {
                Text = "Doji Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("decreasingWickColor", this.decreasingWickColor)
            {
                Text = "Decreasing Wick Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("growingWickColor", this.growingWickColor)
            {
                Text = "Growing Wick Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemColor("dojiWickColor", this.dojiWickColor)
            {
                Text = "Doji Wick Color",
                SortIndex = 2,
            });
            settings.Add(new SettingItemBoolean("drawBorder", this.drawBorder)
            {
                Text = "Draw Border",
                SortIndex = 3,
            });
            SettingItemRelationVisibility borderRelation = new SettingItemRelationVisibility("drawBorder", true);
            settings.Add(new SettingItemInteger("borderWidth", this.borderWidth)
            {
                Text = "Border Width",
                SortIndex = 3,
                Minimum = 1,
                Relation = borderRelation
            });
            settings.Add(new SettingItemColor("decreasingBorderColor", this.decreasingBorderColor)
            {
                Text = "Decreasing Border Color",
                SortIndex = 3,
                Relation = borderRelation,
            });
            settings.Add(new SettingItemColor("growingBorderColor", this.growingBorderColor)
            {
                Text = "Growing Border Color",
                SortIndex = 3,
                Relation = borderRelation,
            });
            settings.Add(new SettingItemColor("dojiBorderColor", this.dojiBorderColor)
            {
                Text = "Doji Border Color",
                SortIndex = 3,
                Relation = borderRelation,
            });
            settings.Add(new SettingItemBoolean("showLabel", this.showLabel)
            {
                Text = "Show Label",
                SortIndex = 4,
            });
            SettingItemRelationVisibility visibleRelationLabel = new SettingItemRelationVisibility("showLabel", true);
            settings.Add(new SettingItemFont("Font", this.labelFont)
            {
                Text = "Font",
                SortIndex = 4,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemSelectorLocalized("HorizontalPosition", this.horizontalPosition, new List<SelectItem> { new SelectItem("Right", HorizontalPosition.Right), new SelectItem("Left", HorizontalPosition.Left) })
            {
                Text = "Label position",
                Relation = visibleRelationLabel,
                SortIndex = 4
            });
            settings.Add(new SettingItemBoolean("showLevelLine", this.showLevelLine)
            {
                Text = "Show Level Lines",
                SortIndex = 5,
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("barsPeriod", out int barsPeriod))
                this.barsPeriod = barsPeriod;
            if (value.TryGetValue("offset", out int offset))
                this.offset = offset;
            if (value.TryGetValue("useCustomBarWidth", out bool useCustomBarWidth))
                this.useCustomBarWidth = useCustomBarWidth;
            if (value.TryGetValue("customBarWidth", out int customBarWidth))
                this.customBarWidth = customBarWidth;
            if (value.TryGetValue("tfPeriod", out Period tfPeriod))
            {
                this.tfPeriod = tfPeriod;
                this.tfDataLoaded = false;
            }
            if (value.TryGetValue("useTFPeriod", out bool useTFPeriod))
                this.useTFPeriod = useTFPeriod;
            if (value.TryGetValue("decreasingCandleColor", out Color decreasingCandleColor))
                this.decreasingCandleColor = decreasingCandleColor;
            if (value.TryGetValue("growingCandleColor", out Color growingCandleColor))
                this.growingCandleColor = growingCandleColor;
            if (value.TryGetValue("dojiCandleColor", out Color dojiCandleColor))
                this.dojiCandleColor = dojiCandleColor;
            if (value.TryGetValue("drawBorder", out bool drawBorder))
                this.drawBorder = drawBorder;
            if (value.TryGetValue("borderWidth", out int borderWidth))
                this.borderWidth = borderWidth;
            if (value.TryGetValue("decreasingBorderColor", out Color decreasingBorderColor))
                this.decreasingBorderColor = decreasingBorderColor;
            if (value.TryGetValue("growingBorderColor", out Color growingBorderColor))
                this.growingBorderColor = growingBorderColor;
            if (value.TryGetValue("dojiBorderColor", out Color dojiBorderColor))
                this.dojiBorderColor = dojiBorderColor;
            if (value.TryGetValue("decreasingWickColor", out Color decreasingWickColor))
                this.decreasingWickColor = decreasingWickColor;
            if (value.TryGetValue("growingWickColor", out Color growingWickColor))
                this.growingWickColor = growingWickColor;
            if (value.TryGetValue("dojiWickColor", out Color dojiWickColor))
                this.dojiWickColor = dojiWickColor;
            if (value.TryGetValue("showLabel", out bool showLabel))
                this.showLabel = showLabel;
            if (value.TryGetValue("Font", out Font labelFont))
                this.labelFont = labelFont;
            if (value.TryGetValue("HorizontalPosition", out HorizontalPosition HorizontalPosition))
                this.horizontalPosition = HorizontalPosition;
            if (value.TryGetValue("showLevelLine", out bool showLevelLine))
                this.showLevelLine = showLevelLine;
            this.OnSettingsUpdated();
        }
    }
    internal enum HorizontalPosition
    {
        Right,
        Left
    }
}
