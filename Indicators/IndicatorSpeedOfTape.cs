// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;
public class IndicatorSpeedOfTape : Indicator, IVolumeAnalysisIndicator
{
    private VolumeAnalysisField DataType = VolumeAnalysisField.Volume;
    private int SmoothingPeriod = 20;
    private ControlLineType controlLineType = ControlLineType.Smoothed;
    private MaMode MaType = MaMode.SMA;
    private double controlValue = 3000;
    private Color abnormalColor = Color.Green;
    private IndicatorLineMarkerIconType markerIcon = IndicatorLineMarkerIconType.None;
    private bool paintBar = false;

    private HistoricalDataCustom SmoothingSource;
    private Indicator smoothing;
    private bool volumeDataLoaded = false;

    private readonly Font font;
    private readonly StringFormat centerCenterSF;
    protected static string LoadingMessage => loc._("Loading volume analysis data...");

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTimeHistogram.cs";

    public override string ShortName => $"SoT";
    public IndicatorSpeedOfTape()
        : base()
    {
        Name = "Speed of Tape";
        AddLineSeries("Speed", Color.CadetBlue, 1, LineStyle.Histogramm);
        AddLineSeries("Control Line", Color.Green, 1, LineStyle.Solid);
        SeparateWindow = true;

        this.font = new Font("Verdana", 10, FontStyle.Regular, GraphicsUnit.Point);
        this.centerCenterSF = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
    }

    public bool IsRequirePriceLevelsCalculation => false;

    public void VolumeAnalysisData_Loaded()
    {
        this.volumeDataLoaded = true;
        this.OnSettingsUpdated();
    }

    protected override void OnInit()
    {
        this.smoothing = Core.Instance.Indicators.BuiltIn.MA(SmoothingPeriod, PriceType.Close, MaType);
        this.SmoothingSource = new HistoricalDataCustom(this);
        this.SmoothingSource.AddIndicator(this.smoothing);
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (!this.volumeDataLoaded || this.HistoricalData.Aggregation is HistoryAggregationTick || this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin].VolumeAnalysisData == null)
            return;
        double number = Math.Abs(this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin].VolumeAnalysisData.Total.GetValue(this.DataType));
        double seconds;
        if (this.HistoricalData.Aggregation is HistoryAggregationTime aggregationTime)
            seconds = aggregationTime.Period.Duration.TotalSeconds;
        else
        {
            var currHistoryItem = this.HistoricalData[this.Count - 1, SeekOriginHistory.Begin];
            seconds = new TimeSpan(currHistoryItem.TicksRight - currHistoryItem.TicksLeft).TotalSeconds;
        }
        double speed = number / seconds;
        this.SmoothingSource.SetValue(0, 0, 0, speed);
        this.SetValue(speed, 0);
        double smoothed = this.controlValue;
        if (this.controlLineType == ControlLineType.Smoothed)
            smoothed = this.smoothing.GetValue();
        this.SetValue(smoothed, 1);
        this.LinesSeries[0].RemoveMarker(0);
        if (this.paintBar)
            this.SetBarColor();
        if (speed > smoothed)
        {
            this.LinesSeries[0].SetMarker(0, new IndicatorLineMarker(abnormalColor, upperIcon: this.markerIcon));
            if (this.paintBar)
                this.SetBarColor(this.abnormalColor);
        }
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
            if (!this.volumeDataLoaded)
                graphics.DrawString(LoadingMessage, this.font, Brushes.DodgerBlue, args.Rectangle, this.centerCenterSF);
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
            settings.Add(new SettingItemSelectorLocalized("DataType", this.DataType, new List<SelectItem> { new SelectItem("Volume", VolumeAnalysisField.Volume), new SelectItem("Trades", VolumeAnalysisField.Trades), new SelectItem("Delta", VolumeAnalysisField.Delta), new SelectItem("Buy Trades", VolumeAnalysisField.BuyTrades), new SelectItem("Sell Trades", VolumeAnalysisField.SellTrades), new SelectItem("Sell Volume", VolumeAnalysisField.SellVolume), new SelectItem("Buy Volume", VolumeAnalysisField.BuyVolume) })
            {
                Text = "Data Type",
                SortIndex = 1
            });
            settings.Add(new SettingItemSelectorLocalized("ControlLineType", this.controlLineType, new List<SelectItem> { new SelectItem("Smoothed", ControlLineType.Smoothed), new SelectItem("Set Value", ControlLineType.SetValue) })
            {
                Text = "Control Line Value",
                SortIndex = 1
            });
            settings.Add(new SettingItemBoolean("paintBar", this.paintBar)
            {
                Text = "Mark Bar Color",
                SortIndex = 2,
            });
            SettingItemRelationVisibility smoothedVisibleRelation = new SettingItemRelationVisibility("ControlLineType", [new SelectItem("Smoothed", ControlLineType.Smoothed)]);
            settings.Add(new SettingItemSelectorLocalized("MaType", this.MaType, new List<SelectItem> { new SelectItem("Simple Moving Average", MaMode.SMA), new SelectItem("Exponential Moving Average", MaMode.EMA), new SelectItem("Smoothed Moving Average", MaMode.SMMA), new SelectItem("Linearly Weighted Moving Average", MaMode.LWMA) })
            {
                Text = "Smoothing Type",
                SortIndex = 2,
                Relation = smoothedVisibleRelation
            });
            settings.Add(new SettingItemInteger("SmoothingPeriod", this.SmoothingPeriod)
            {
                Text = "Smoothing Period",
                SortIndex = 2,
                Dimension = "bars",
                Minimum = 2,
                Relation = smoothedVisibleRelation
            });
            SettingItemRelationVisibility setValueVisibleRelation = new SettingItemRelationVisibility("ControlLineType", [new SelectItem("Set Value", ControlLineType.SetValue)]);
            settings.Add(new SettingItemDouble("controlValue", this.controlValue)
            {
                Text = "Control Value",
                SortIndex = 2,
                Dimension = "units per second",
                Minimum = 0,
                Maximum = 99999999999999999,
                DecimalPlaces = 2,
                Increment = 0.01,
                Relation = setValueVisibleRelation
            });
            settings.Add(new SettingItemColor("abnormalColor", this.abnormalColor)
            {
                Text = "Mark Color",
                SortIndex = 4,
            });
            settings.Add(new SettingItemSelectorLocalized("markerIcon", this.markerIcon, new List<SelectItem> { new SelectItem("Arrow", IndicatorLineMarkerIconType.DownArrow), new SelectItem("Circle", IndicatorLineMarkerIconType.FillCircle), new SelectItem("Flag", IndicatorLineMarkerIconType.Flag), new SelectItem("Only Color", IndicatorLineMarkerIconType.None) })
            {
                Text = "Marker type",
                SortIndex = 4
            });
            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("DataType", out VolumeAnalysisField DataType))
                this.DataType = DataType;
            if (value.TryGetValue("ControlLineType", out ControlLineType ControlLineType))
                this.controlLineType = ControlLineType;
            if (value.TryGetValue("MaType", out MaMode MaType))
                this.MaType = MaType;
            if (value.TryGetValue("SmoothingPeriod", out int SmoothingPeriod))
                this.SmoothingPeriod = SmoothingPeriod;
            if (value.TryGetValue("controlValue", out double controlValue))
                this.controlValue = controlValue;
            if (value.TryGetValue("abnormalColor", out Color abnormalColor))
                this.abnormalColor = abnormalColor;
            if (value.TryGetValue("markerIcon", out IndicatorLineMarkerIconType markerIcon))
                this.markerIcon = markerIcon;
            if (value.TryGetValue("paintBar", out bool paintBar))
                this.paintBar = paintBar;
            this.OnSettingsUpdated();
        }
    }
}
public enum ControlLineType
{
    SetValue,
    Smoothed
}
