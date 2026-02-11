// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators;

public sealed class IndicatorFairValueGap : Indicator
{
    private bool closeGaps = false;
    private bool halfLine = false;
    private bool quarterLine = false;
    private bool threeQuarterLine = false;
    private bool drawBorder = true;
    private bool expandGap = false;
    private bool direction = false;
    private bool showShrink = true;
    private bool hideClosed = false;
    private bool continueToEnd = false;
    private bool lastContinue = false;
    private bool useBodiesOnly = false;


    private ShrinkType shrinkType = ShrinkType.Shrink;
    private DirectionType directionType = DirectionType.UpAndDown;

    public LineOptions upBorderOptions { get; set; }
    public LineOptions downBorderOptions { get; set; }
    public LineOptions upHalfLineOptions { get; set; }
    public LineOptions downHalfLineOptions { get; set; }

    private int gapLength = 200;
    private int gapsNumber = 15;
    private int percentToClose = 50;
    private int lastGapsCount = 5;
    private double minimalDeviation = 0;
    private double maxDeviation = 100;

    public Color UpColor
    {
        get => this.upBrush.Color;
        set => this.upBrush.Color = value;
    }
    private readonly SolidBrush upBrush;

    public Color DownColor
    {
        get => this.downBrush.Color;
        set => this.downBrush.Color = value;
    }
    private readonly SolidBrush downBrush;

    private readonly Pen upPen;
    private readonly Pen downPen;
    private readonly Pen upHalfPen;
    private readonly Pen downHalfPen;

    List<IndicatorFairValueGapGap> gaps;
    private bool gapAdded = false;

    public override string ShortName => $"FVG";
    public override string HelpLink => "https://help.quantower.com/quantower/analytics-panels/chart/technical-indicators/channels/fair-value-gap-fvg";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFairValueGap.cs";

    public IndicatorFairValueGap()
        : base()
    {
        Name = "Fair Value Gap";
        SeparateWindow = false;
        this.upBorderOptions = new LineOptions();
        this.downBorderOptions = new LineOptions();
        this.upHalfLineOptions = new LineOptions();
        this.downHalfLineOptions = new LineOptions();

        this.upBorderOptions.Color = Color.Green;
        this.downBorderOptions.Color = Color.Red;
        this.upHalfLineOptions.Color = Color.Green;
        this.downHalfLineOptions.Color = Color.Red;

        this.upBorderOptions.Width = 1;
        this.downBorderOptions.Width = 1;
        this.upHalfLineOptions.Width = 1;
        this.downHalfLineOptions.Width = 1;

        this.upBorderOptions.WithCheckBox = false;
        this.downBorderOptions.WithCheckBox = false;
        this.upHalfLineOptions.WithCheckBox = false;
        this.downHalfLineOptions.WithCheckBox = false;

        this.upBrush = new SolidBrush(Color.FromArgb(25, Color.Green));
        this.downBrush = new SolidBrush(Color.FromArgb(25, Color.Red));
        this.upPen = new Pen(Color.Green, 1);
        this.downPen = new Pen(Color.Red, 1);
        this.upHalfPen = new Pen(Color.Green, 1);
        this.downHalfPen = new Pen(Color.Red, 1);
    }
    protected override void OnInit()
    {
        this.gaps = new List<IndicatorFairValueGapGap>();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 4)
            return;
        double currentHigh = this.useBodiesOnly ? this.BodyHigh(1) : this.High(1);
        double currentLow = this.useBodiesOnly ? this.BodyLow(1) : this.Low(1);
        double previousHigh = this.useBodiesOnly ? this.BodyHigh(3) : this.High(3);
        double previousLow = this.useBodiesOnly ? this.BodyLow(3) : this.Low(3);

        double currentClose = this.Close(1);
        double currentOpen = this.Open(1);
        double previousClose = this.Close(3);
        double previousOpen = this.Open(3);
        double middleClose = this.Close(2);
        double middleOpen = this.Open(2);
        //Checking the conditions for creating a new gap
        if (args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar)
            if ((currentClose < currentOpen && previousClose < previousOpen && middleClose < middleOpen) || (currentClose > currentOpen && previousClose > previousOpen && middleClose > middleOpen) || !direction)
            {
                int gapStart = expandGap ? this.Count - 4 : this.Count - 2;
                //Checking if a gap is falling or rising
                bool upGap = currentLow > previousHigh && 100 * (currentLow - previousHigh) / previousHigh >= minimalDeviation && 100 * (currentLow - previousHigh) / previousHigh <= maxDeviation && (this.directionType == DirectionType.OnlyUp || this.directionType == DirectionType.UpAndDown);
                bool downGap = currentHigh < previousLow && 100 * (previousLow - currentHigh) / currentHigh >= minimalDeviation && 100 * (previousLow - currentHigh) / currentHigh <= maxDeviation && (this.directionType == DirectionType.OnlyDown || this.directionType == DirectionType.UpAndDown);
                //Creating a new gap
                if (upGap)
                    this.gaps.Insert(0, new IndicatorFairValueGapGap(gapStart, previousHigh, currentLow));
                else if (downGap)
                    this.gaps.Insert(0, new IndicatorFairValueGapGap(gapStart, previousLow, currentHigh));
            }
        //New values for current High and Low to update the gap in real time
        currentHigh = this.useBodiesOnly ? this.BodyHigh(0) : this.High(0);
        currentLow  = this.useBodiesOnly ? this.BodyLow(0) : this.Low(0);
        //Update the state of the gap from the intersection with the current candle
        for (int i = 0; i <= gaps.Count - 1; i++)
        {
            var currGap = gaps[i];
            if (!currGap.IsEnded)
            {
                //Assigning the latest gap high and low price values
                double currentDownPrice = currGap.downPoints[currGap.downPoints.Count - 1].Price;
                double currentUpPrice = currGap.upPoints[currGap.upPoints.Count - 1].Price;
                //Checking if a point on that candle has already been added in current gap
                bool downPointAdded = currGap.downPoints[currGap.downPoints.Count - 1].BarNumber == this.Count - 1;
                bool upPointAdded = currGap.upPoints[currGap.upPoints.Count - 1].BarNumber == this.Count - 1;
                //Checking whether a gap intersects with a candle
                if (currentHigh > currentDownPrice && currentHigh < currentUpPrice && this.shrinkType != ShrinkType.NoShrink)
                {
                    switch (this.shrinkType)
                    {
                        case ShrinkType.Shrink:
                            if (!downPointAdded) // Adding new point
                                currGap.AddDownPoint(this.Count - 1, currentHigh);
                            else if (downPointAdded) //Changing the position of the current point
                                currGap.downPoints[currGap.downPoints.Count - 1].Price = currentHigh;
                            break;
                        case ShrinkType.Close:
                            if (!downPointAdded)
                                currGap.AddDownPoint(this.Count - 1, currentDownPrice);
                            else
                                currGap.downPoints[currGap.downPoints.Count - 1].Price = currentDownPrice;
                            if (this.Count - 1 != currGap.downPoints[0].BarNumber) //Closing the candle when it crosses, if this is not the first gap point
                                currGap.IsEnded = true;
                            break;
                        case ShrinkType.CloseOnValue:
                            //Closing a gap if more than a specified percentage is closed
                            if (100 * ((currentUpPrice - currentHigh) / (currGap.upPoints[0].Price - currGap.downPoints[0].Price)) <= 100 - percentToClose)
                                currGap.IsEnded = true;
                            if (!downPointAdded)
                                currGap.AddDownPoint(this.Count - 1, currentHigh);
                            else
                                currGap.downPoints[currGap.downPoints.Count - 1].Price = currentHigh;
                            break;
                        default:
                            if (!downPointAdded)
                                currGap.AddDownPoint(this.Count - 1, currentHigh);
                            else
                                currGap.downPoints[currGap.downPoints.Count - 1].Price = currentHigh;
                            break;
                    }
                }
                else if (!downPointAdded) // Adding a new point if there is no intersection
                    currGap.AddDownPoint(this.Count - 1, currentDownPrice);
                if (currentLow < currentUpPrice && currentLow > currentDownPrice && this.shrinkType != ShrinkType.NoShrink)
                {
                    switch (this.shrinkType)
                    {
                        case ShrinkType.Shrink:
                            if (!upPointAdded) //Adding new point
                                currGap.AddUpPoint(this.Count - 1, currentLow);
                            else
                                currGap.upPoints[currGap.downPoints.Count - 1].Price = currentLow; //Changing the position of the current point
                            break;
                        case ShrinkType.Close:
                            if (!upPointAdded)
                                currGap.AddUpPoint(this.Count - 1, currentUpPrice);
                            else
                                currGap.upPoints[currGap.upPoints.Count - 1].Price = currentUpPrice;
                            if (this.Count - 1 != currGap.upPoints[0].BarNumber) //Closing the candle when it crosses, if this is not the first gap point
                                currGap.IsEnded = true;
                            break;
                        case ShrinkType.CloseOnValue:
                            //Closing a gap if more than a specified percentage is closed
                            if (100 * ((currentLow - currentDownPrice) / (currGap.upPoints[0].Price - currGap.downPoints[0].Price)) <= 100 - percentToClose)
                                currGap.IsEnded = true;
                            if (!upPointAdded)
                                currGap.AddUpPoint(this.Count - 1, currentLow);
                            else
                                currGap.upPoints[currGap.upPoints.Count - 1].Price = currentLow;
                            break;
                        default:
                            if (!upPointAdded)
                                currGap.AddUpPoint(this.Count - 1, currentLow);
                            else
                                currGap.upPoints[currGap.upPoints.Count - 1].Price = currentLow;
                            break;
                    }
                }
                else if (!upPointAdded) //Adding a new point if there is no intersection
                    currGap.AddUpPoint(this.Count - 1, currentUpPrice);
                // Checking whether the current candle has completely covered the gap
                if ((currentHigh >= currentUpPrice && currentLow <= currentUpPrice) && (currentHigh >= currentDownPrice && currentLow <= currentDownPrice) && !closeGaps)
                {
                    currGap.IsEnded = true;
                    currGap.upPoints[currGap.upPoints.Count - 1].Price = currGap.upPoints[currGap.upPoints.Count - 2].Price;
                    currGap.downPoints[currGap.downPoints.Count - 1].Price = currGap.downPoints[currGap.downPoints.Count - 2].Price;
                }
                if (closeGaps && (currGap.upPoints[currGap.upPoints.Count - 1].BarNumber - currGap.upPoints[0].BarNumber >= gapLength || currGap.downPoints[currGap.downPoints.Count - 1].BarNumber - currGap.downPoints[0].BarNumber >= gapLength))
                    currGap.IsEnded = true;
            }
        }
        while (this.gaps.Count > gapsNumber + 1)
            this.gaps.RemoveAt(gaps.Count-1);
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
            int continuedCount = 0;
            for (int i = 0; i < gaps.Count; i++)
            {
                var currGap = gaps[i];
                if (!(currGap.IsEnded && hideClosed))
                {
                    List<Point> Points = new List<Point>();
                    // Selecting a color depending on the type of gap
                    var brush = currGap.GapType == IndicatorFairValueGapType.Up ? this.upBrush : this.downBrush;
                    var pen = currGap.GapType == IndicatorFairValueGapType.Up ? this.upPen : this.downPen;
                    var halfPen = currGap.GapType == IndicatorFairValueGapType.Up ? this.upHalfPen : this.downHalfPen;
                    // Painting
                    if (showShrink) //Case where shrink display is required
                    {
                        for (int j = 0; j < currGap.upPoints.Count; j++)
                        {
                            DateTime barTime = this.HistoricalData[currGap.upPoints[j].BarNumber, SeekOriginHistory.Begin].TimeLeft;
                            int x = (int)currWindow.CoordinatesConverter.GetChartX(barTime) + CurrentChart.BarsWidth / 2;
                            int y = (int)currWindow.CoordinatesConverter.GetChartY(currGap.upPoints[j].Price);
                            Points.Add(new Point(x, y));
                        }
                        for (int j = currGap.downPoints.Count - 1; j >= 0; j--)
                        {
                            DateTime barTime = this.HistoricalData[currGap.downPoints[j].BarNumber, SeekOriginHistory.Begin].TimeLeft;
                            int x = (int)currWindow.CoordinatesConverter.GetChartX(barTime) + CurrentChart.BarsWidth / 2;
                            int y = (int)currWindow.CoordinatesConverter.GetChartY(currGap.downPoints[j].Price);
                            Points.Add(new Point(x, y));
                        }
                        if (!currGap.IsEnded && this.continueToEnd && (!this.lastContinue || (this.lastContinue && i < this.lastGapsCount)) && continuedCount <= this.lastGapsCount)
                        {
                            Points.Insert(currGap.upPoints.Count, new Point(currWindow.ClientRectangle.Width, Points[currGap.upPoints.Count - 1].Y));
                            Points.Insert(currGap.upPoints.Count+1, new Point(currWindow.ClientRectangle.Width, Points[currGap.upPoints.Count + 1].Y));
                            continuedCount++;
                        }
                    }
                    else //Case where shrink display is not required
                    {
                        int x1 = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[0].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        int y1 = (int)currWindow.CoordinatesConverter.GetChartY(currGap.upPoints[0].Price);
                        int width = (currGap.downPoints[currGap.downPoints.Count - 1].BarNumber - currGap.upPoints[0].BarNumber) * CurrentChart.BarsWidth;
                        if (currGap.upPoints[currGap.upPoints.Count-1].BarNumber == this.HistoricalData.Count - 1 && !currGap.IsEnded && this.continueToEnd && (!this.lastContinue || (this.lastContinue && i < this.lastGapsCount)) && continuedCount <= lastGapsCount)
                        {
                            width = currWindow.ClientRectangle.Width - x1;
                        }
                        int height = (int)currWindow.CoordinatesConverter.GetChartY(currGap.downPoints[0].Price) - y1;
                        Points.Add(new Point(x1, y1));
                        Points.Add(new Point(x1 + width, y1));
                        Points.Add(new Point(x1 + width, y1 + height));
                        Points.Add(new Point(x1, y1 + height));
                    }
                    //Drawing a gap
                    gr.FillPolygon(brush, Points.ToArray());
                    if (this.drawBorder)
                        gr.DrawPolygon(pen, Points.ToArray());
                    if (this.halfLine) //Drawing a half line
                    {
                        int halfLineY = (int)currWindow.CoordinatesConverter.GetChartY((currGap.upPoints[0].Price + currGap.downPoints[0].Price) / 2);
                        int halfLineStart = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[0].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        int halfLineEnd = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[currGap.upPoints.Count - 1].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        if (!currGap.IsEnded && this.continueToEnd && (!this.lastContinue || (this.lastContinue && i < this.lastGapsCount)) && continuedCount <= lastGapsCount)
                            halfLineEnd = currWindow.ClientRectangle.Width;
                        gr.DrawLine(halfPen, halfLineEnd, halfLineY, halfLineStart, halfLineY);
                    }
                    if (this.quarterLine) //Drawing a quarter line
                    {
                        int quarterLineY = currGap.GapType == IndicatorFairValueGapType.Up ? (int)currWindow.CoordinatesConverter.GetChartY((currGap.upPoints[0].Price + currGap.downPoints[0].Price*3) / 4) : (int)currWindow.CoordinatesConverter.GetChartY((currGap.upPoints[0].Price * 3 + currGap.downPoints[0].Price) / 4);
                        int quarterLineStart = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[0].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        int quarterLineEnd = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[currGap.upPoints.Count - 1].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        if (!currGap.IsEnded && this.continueToEnd && (!this.lastContinue || (this.lastContinue && i < this.lastGapsCount)) && continuedCount <= lastGapsCount)
                            quarterLineEnd = currWindow.ClientRectangle.Width;
                        gr.DrawLine(halfPen, quarterLineEnd, quarterLineY, quarterLineStart, quarterLineY);
                    }
                    if (this.threeQuarterLine) //Drawing a three-quarters line
                    {
                        int threeQuarterLineY = currGap.GapType == IndicatorFairValueGapType.Up ? (int)currWindow.CoordinatesConverter.GetChartY((currGap.upPoints[0].Price*3 + currGap.downPoints[0].Price) / 4) : (int)currWindow.CoordinatesConverter.GetChartY((currGap.upPoints[0].Price + currGap.downPoints[0].Price*3) / 4);
                        int threeQuarterLineStart = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[0].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        int threeQuarterLineEnd = (int)currWindow.CoordinatesConverter.GetChartX(this.HistoricalData[currGap.upPoints[currGap.upPoints.Count - 1].BarNumber, SeekOriginHistory.Begin].TimeLeft) + CurrentChart.BarsWidth / 2;
                        if (!currGap.IsEnded && this.continueToEnd && (!this.lastContinue || (this.lastContinue && i < this.lastGapsCount)) && continuedCount <= lastGapsCount)
                            threeQuarterLineEnd = currWindow.ClientRectangle.Width;
                        gr.DrawLine(halfPen, threeQuarterLineEnd, threeQuarterLineY, threeQuarterLineStart, threeQuarterLineY);
                    }
                }
            }
        }
        finally
        {
            gr.SetClip(prevClipRectangle);
        }
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            settings.Add(new SettingItemBoolean("Partially", this.closeGaps)
            {
                Text = "Close gaps partially",
                SortIndex = 1,
            });
            settings.Add(new SettingItemBoolean("UseBodiesOnly", this.useBodiesOnly)
            {
                Text = "Use candle bodies only",
                SortIndex = 1,
            });
            SettingItemRelationVisibility visibleRelationPartially = new SettingItemRelationVisibility("Partially", true);
            settings.Add(new SettingItemInteger("GapsLength", this.gapLength)
            {
                Text = "Max gaps trail length",
                SortIndex = 2,
                Dimension = "Bars",
                Relation = visibleRelationPartially,
                Minimum = 1
            });

            settings.Add(new SettingItemInteger("MaxNumber", this.gapsNumber)
            {
                Text = "Max number of gaps",
                SortIndex = 3,
                Minimum = 1
            });
            settings.Add(new SettingItemDouble("minimalDeviation", this.minimalDeviation)
            {
                Text = "Minimal Deviation",
                SortIndex = 3,
                Dimension = "%",
                Minimum = 0,
                Maximum = 100,
                DecimalPlaces = 3,
                Increment = 0.001
            });
            settings.Add(new SettingItemDouble("MaxDeviation", this.maxDeviation)
            {
                Text = "Maximal Deviation",
                SortIndex = 3,
                Dimension = "%",
                Minimum = 0.001,
                Maximum = 100,
                DecimalPlaces = 3,
                Increment = 0.001
            });
            settings.Add(new SettingItemColor("upColor", this.UpColor)
            {
                Text = "Up Color",
                SortIndex = 4,
            });
            settings.Add(new SettingItemColor("downColor", this.DownColor)
            {
                Text = "Down Color",
                SortIndex = 5,
            });
            settings.Add(new SettingItemBoolean("Border", this.drawBorder)
            {
                Text = "Draw Borders",
                SortIndex = 6,
            });
            SettingItemRelationVisibility visibleRelationBorder = new SettingItemRelationVisibility("Border", true);
            settings.Add(new SettingItemLineOptions("UpBorder", this.upBorderOptions)
            {
                Text = "Up Border Style",
                SortIndex = 6,
                Relation = visibleRelationBorder,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true
            });
            settings.Add(new SettingItemLineOptions("DownBorder", this.downBorderOptions)
            {
                Text = "Down Border Style",
                SortIndex = 6,
                Relation = visibleRelationBorder,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
            });
            settings.Add(new SettingItemBoolean("HalfLine", this.halfLine)
            {
                Text = "Draw Half Line",
                SortIndex = 7,
            });
            settings.Add(new SettingItemBoolean("quarterLine", this.quarterLine)
            {
                Text = "Draw Quarter Line",
                SortIndex = 7,
            });
            settings.Add(new SettingItemBoolean("threeQuarterLine", this.threeQuarterLine)
            {
                Text = "Draw Three-quarters Line",
                SortIndex = 7,
            });
            SettingItemRelationVisibility visibleRelationHalfLine = new SettingItemRelationVisibility("HalfLine", true);
            settings.Add(new SettingItemLineOptions("UpHalf", this.upHalfLineOptions)
            {
                Text = "Up Half Line Style",
                SortIndex = 7,
                Relation = visibleRelationHalfLine,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
            });
            settings.Add(new SettingItemLineOptions("DownHalf", this.downHalfLineOptions)
            {
                Text = "Down Half Line Style",
                SortIndex = 7,
                Relation = visibleRelationHalfLine,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
            });
            settings.Add(new SettingItemSelectorLocalized("Shrink", this.shrinkType, new List<SelectItem> { new SelectItem("Shrink Always", ShrinkType.Shrink), new SelectItem("Close on percent", ShrinkType.CloseOnValue), new SelectItem("Close Always", ShrinkType.Close), new SelectItem("No Shrink", ShrinkType.NoShrink) })
            {
                Text = "Shrink Type",
                SortIndex = 8
            });
            SettingItemRelationVisibility visibleRelationClosePercent = new SettingItemRelationVisibility("Shrink", new SelectItem("Close on percent", ShrinkType.CloseOnValue));
            settings.Add(new SettingItemInteger("Percent", this.percentToClose)
            {
                Text = "Percent to close",
                SortIndex = 8,
                Minimum = 1,
                Dimension = "%",
                Maximum = 100,
                Relation = visibleRelationClosePercent
            });
            settings.Add(new SettingItemBoolean("HideClosed", this.hideClosed)
            {
                Text = "Hide Closed",
                SortIndex = 8,
            });
            settings.Add(new SettingItemBoolean("ExpandGap", this.expandGap)
            {
                Text = "Expand Gap",
                SortIndex = 1,
            });
            settings.Add(new SettingItemBoolean("Direction", this.direction)
            {
                Text = "Same Direction Requested",
                SortIndex = 1,
            });
            settings.Add(new SettingItemSelectorLocalized("DirectionType", this.directionType, new List<SelectItem> { new SelectItem("Up and Down", DirectionType.UpAndDown), new SelectItem("Only Up", DirectionType.OnlyUp), new SelectItem("Only Down", DirectionType.OnlyDown) })
            {
                Text = "Direction Type",
                SortIndex = 1
            });
            SettingItemRelationVisibility visibleRelationDirection = new SettingItemRelationVisibility("Direction", true);
            SettingItemRelationVisibility showShrinkVisibleRelation = new SettingItemRelationVisibility("Shrink", [new SelectItem("Close on percent", ShrinkType.CloseOnValue), new SelectItem("Shrink Always", ShrinkType.Shrink)]);
            settings.Add(new SettingItemBoolean("ShowShrink", this.showShrink)
            {
                Text = "Show Shrink",
                SortIndex = 8,
                Relation = showShrinkVisibleRelation,
            });
            settings.Add(new SettingItemBoolean("continueToEnd", this.continueToEnd)
            {
                Text = "Continue unclosed gaps to end",
                SortIndex = 9,
            });
            SettingItemRelationVisibility visibleRelationContinueToEnd = new SettingItemRelationVisibility("continueToEnd", true);
            settings.Add(new SettingItemBoolean("lastContinue", this.lastContinue)
            {
                Text = "Continue only several last gaps",
                SortIndex = 9,
                Relation = visibleRelationContinueToEnd,
            });
            SettingItemRelationVisibility visibleRelationLastContinue = new SettingItemRelationVisibility("lastContinue", true);
            settings.Add(new SettingItemInteger("lastGapsCount", this.lastGapsCount)
            {
                Text = "Last gaps count",
                SortIndex = 9,
                Relation = visibleRelationLastContinue,
                Minimum = 1
            });
            return settings;

        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("Partially", out bool partially))
                this.closeGaps = partially;
            if (value.TryGetValue("UseBodiesOnly", out bool useBodiesOnly))
                this.useBodiesOnly = useBodiesOnly;
            if (value.TryGetValue("GapsLength", out int gapLength))
                this.gapLength = gapLength;
            if (value.TryGetValue("MaxNumber", out int MaxNumber))
                this.gapsNumber = MaxNumber;
            if (value.TryGetValue("lastGapsCount", out int lastGapsCount))
                this.lastGapsCount = lastGapsCount;
            if (value.TryGetValue("minimalDeviation", out double minimalDeviation))
                this.minimalDeviation = minimalDeviation;
            if (value.TryGetValue("MaxDeviation", out double maxDeviation))
                this.maxDeviation = maxDeviation;
            if (value.TryGetValue("upColor", out Color upColor))
                this.UpColor = upColor;
            if (value.TryGetValue("downColor", out Color downColor))
                this.DownColor = downColor;
            if (value.TryGetValue("Border", out bool drawBorder))
                this.drawBorder = drawBorder;
            if (value.TryGetValue("continueToEnd", out bool continueToEnd))
                this.continueToEnd = continueToEnd;
            if (value.TryGetValue("lastContinue", out bool lastContinue))
                this.lastContinue = lastContinue;
            if (value.TryGetValue("UpBorder", out LineOptions UpBorder))
            {
                this.upBorderOptions = UpBorder;
                this.upPen.Width = UpBorder.Width;
                this.upPen.Color = UpBorder.Color;
                this.upPen.DashStyle = (DashStyle)UpBorder.LineStyle;
            }
            if (value.TryGetValue("DownBorder", out LineOptions DownBorder))
            {
                this.downBorderOptions = DownBorder;
                this.downPen.Width = DownBorder.Width;
                this.downPen.Color = DownBorder.Color;
                this.downPen.DashStyle = (DashStyle)DownBorder.LineStyle;
            }
            if (value.TryGetValue("HalfLine", out bool halfLine))
                this.halfLine = halfLine;
            if (value.TryGetValue("quarterLine", out bool quarterLine))
                this.quarterLine = quarterLine;
            if (value.TryGetValue("threeQuarterLine", out bool threeQuarterLine))
                this.threeQuarterLine = threeQuarterLine;
            if (value.TryGetValue("UpHalf", out LineOptions UpHalf))
            {
                this.upHalfLineOptions = UpHalf;
                this.upHalfPen.Width = UpHalf.Width;
                this.upHalfPen.Color = UpHalf.Color;
                this.upHalfPen.DashStyle = (DashStyle)UpHalf.LineStyle;
            }
            if (value.TryGetValue("DownHalf", out LineOptions DownHalf))
            {
                this.downHalfLineOptions = DownHalf;
                this.downHalfPen.Width = DownHalf.Width;
                this.downHalfPen.Color = DownHalf.Color;
                this.downHalfPen.DashStyle = (DashStyle)DownHalf.LineStyle;
            }
            if (value.TryGetValue("Shrink", out ShrinkType Shrink))
                this.shrinkType = Shrink;
            if (value.TryGetValue("Percent", out int Percent))
                this.percentToClose = Percent;
            if (value.TryGetValue("ExpandGap", out bool expandGap))
                this.expandGap = expandGap;
            if (value.TryGetValue("Direction", out bool direction))
                this.direction = direction;
            if (value.TryGetValue("ShowShrink", out bool showShrink))
                this.showShrink = showShrink;
            if (value.TryGetValue("HideClosed", out bool hideClosed))
                this.hideClosed = hideClosed;
            if (value.TryGetValue("DirectionType", out DirectionType directionType))
                this.directionType = directionType;
            this.OnSettingsUpdated();
        }
    }
    private double BodyHigh(int offset) => Math.Max(this.Open(offset), this.Close(offset));
    private double BodyLow(int offset) => Math.Min(this.Open(offset), this.Close(offset));
}
internal sealed class IndicatorFairValueGapGap
{
    public IndicatorFairValueGapType GapType { get; private set; }
    public List<PricePoint> upPoints { get; private set; }
    public List<PricePoint> downPoints { get; private set; }

    public bool IsEnded { get; set; }

    public IndicatorFairValueGapGap(int startBar, double startPrice, double endPrice)
    {
        this.IsEnded = false;

        upPoints = new List<PricePoint>();
        downPoints = new List<PricePoint>();
        if (endPrice > startPrice)
        {
            this.GapType = IndicatorFairValueGapType.Up;
            upPoints.Add(new PricePoint(startBar, endPrice));
            downPoints.Add(new PricePoint(startBar, startPrice));
        }
        if (endPrice < startPrice)
        {
            this.GapType = IndicatorFairValueGapType.Down;
            upPoints.Add(new PricePoint(startBar, startPrice));
            downPoints.Add(new PricePoint(startBar, endPrice));
        }
    }
    public void AddUpPoint(int barNumber, double price)
    {
        upPoints.Add(new PricePoint(barNumber, price));
    }
    public void AddDownPoint(int barNumber, double price)
    {
        downPoints.Add(new PricePoint(barNumber, price));
    }
}
internal sealed class PricePoint
{
    public int BarNumber { get; private set; }
    public double Price { get; set; }
    public PricePoint(int barNumber, double price)
    {
        this.BarNumber = barNumber;
        this.Price = price;
    }
}
public enum IndicatorFairValueGapType
{
    Up,
    Down
}
public enum ShrinkType
{
    Shrink,
    CloseOnValue,
    Close,
    NoShrink
}
public enum DirectionType
{
    OnlyUp,
    OnlyDown,
    UpAndDown
}