// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators;

public sealed class IndicatorGaps : Indicator
{
    private bool closeGaps = false;

    private int gapLength = 200;
    private int gapsNumber = 15;
    private double minimalDeviation = 0;

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

    List<Gap> gaps;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorGaps.cs";

    public IndicatorGaps()
        : base()
    {
        Name = "Gaps";
        SeparateWindow = false;

        this.upBrush = new SolidBrush(Color.Green);
        this.downBrush = new SolidBrush(Color.Red);
    }
    protected override void OnInit()
    {
        this.gaps = new List<Gap>();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 2)
            return;
        double currentClose = this.Close();
        double currentOpen = this.Open();
        double currentHigh = this.High();
        double currentLow = this.Low();
        double previousHigh = this.High(1);
        double previousLow = this.Low(1);

        if (currentLow > previousHigh && 100 * (currentLow - previousHigh) / previousHigh >= minimalDeviation)
            this.gaps.Add(new Gap(this.Count - 2, previousHigh, this.Count - 1, currentLow));
        else if (currentHigh < previousLow && 100 * (previousLow - currentHigh) / currentHigh >= minimalDeviation)
            this.gaps.Add(new Gap(this.Count - 2, previousLow, this.Count - 1, currentHigh));

        for (int i = 0; i <= gaps.Count - 1; i++)
        {
            var currGap = gaps[i];
            if (!currGap.IsEnded)
            {
                currGap.EndBar++;
                if ((currentClose >= currGap.StartPrice && currentOpen <= currGap.StartPrice) || (currentClose > currGap.EndPrice && currentOpen < currGap.EndPrice))
                    currGap.IsEnded = true;
                else if ((currentOpen > currGap.StartPrice && currentClose < currGap.StartPrice) || (currentOpen > currGap.EndPrice && currentClose < currGap.EndPrice))
                    currGap.IsEnded = true;
            }
            if (closeGaps && this.gaps[i].EndBar - this.gaps[i].StartBar >= gapLength)
                this.gaps[i].EndBar = this.gaps[i].StartBar + gapLength;
            if (this.gaps[i].EndBar >= this.Count)
                this.gaps[i].EndBar = this.Count - 1;
        }
        while (this.gaps.Count > gapsNumber)
            this.gaps.RemoveAt(0);
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

        for (int i = 0; i <= gaps.Count - 1; i++)
        {
            DateTime startTime = this.HistoricalData[this.gaps[i].StartBar, SeekOriginHistory.Begin].TimeLeft;
            DateTime endTime = this.HistoricalData[this.gaps[i].EndBar, SeekOriginHistory.Begin].TimeLeft;
            int x = (int)currWindow.CoordinatesConverter.GetChartX(startTime);
            int width = (int)currWindow.CoordinatesConverter.GetChartX(endTime) - x;
            int y = this.gaps[i].GapType == GapType.Down ? (int)currWindow.CoordinatesConverter.GetChartY(this.gaps[i].StartPrice) : (int)currWindow.CoordinatesConverter.GetChartY(this.gaps[i].EndPrice);
            int height = this.gaps[i].GapType == GapType.Down ? Math.Abs(y - (int)currWindow.CoordinatesConverter.GetChartY(this.gaps[i].EndPrice)) : Math.Abs(y - (int)currWindow.CoordinatesConverter.GetChartY(this.gaps[i].StartPrice));
            var rect = new RectangleF(x + CurrentChart.BarsWidth / 2, y, width + CurrentChart.BarsWidth / 2, height);

            var brush = this.gaps[i].GapType == GapType.Up ? this.upBrush : this.downBrush;

            gr.FillRectangle(brush, rect);
        }
        gr.SetClip(prevClipRectangle);
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
            SettingItemRelationVisibility visibleRelation = new SettingItemRelationVisibility("Partially", true);
            settings.Add(new SettingItemInteger("GapsLength", this.gapLength)
            {
                Text = "Max gaps trail length",
                SortIndex = 2,
                Dimension = "Bars",
                Relation = visibleRelation,
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
            return settings;

        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("Partially", out bool partially))
                this.closeGaps = partially;
            if (value.TryGetValue("GapsLength", out int gapLength))
                this.gapLength = gapLength;
            if (value.TryGetValue("MaxNumber", out int MaxNumber))
                this.gapsNumber = MaxNumber;
            if (value.TryGetValue("minimalDeviation", out double minimalDeviation))
                this.minimalDeviation = minimalDeviation;
            if (value.TryGetValue("upColor", out Color upColor))
                this.UpColor = upColor;
            if (value.TryGetValue("downColor", out Color downColor))
                this.DownColor = downColor;
            this.OnSettingsUpdated();
        }
    }

}
internal sealed class Gap
{
    public int StartBar { get; private set; }
    public double StartPrice { get; private set; }
    public GapType GapType { get; private set; }

    public int EndBar { get; set; }
    public double EndPrice { get; set; }
    public bool IsEnded { get; set; }

    public Gap(int startBar, double startPrice, int endBar, double endPrice)
    {
        this.IsEnded = false;

        this.StartBar = startBar;
        this.StartPrice = startPrice;
        this.EndBar = endBar;
        this.EndPrice = endPrice;

        if (endPrice > startPrice)
            this.GapType = GapType.Up;
        if (endPrice < startPrice)
            this.GapType = GapType.Down;
    }
}

internal enum GapType
{
    Up,
    Down
}