// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace BarsDataIndicators;

/// <summary>
/// Volume allows to confirm the strength of a trend or to suggest about it's weakness.
/// </summary>
public sealed class IndicatorVolume : Indicator, IWatchlistIndicator
{
    [InputParameter("Volume asset", 10, variants: new object[]
    {
        "Base asset", PriceType.Volume,
        "Quote asset", PriceType.QuoteAssetVolume
    })]
    public PriceType VolumeAsset = PriceType.Volume;

    [InputParameter("MA period", 20, 1, int.MaxValue, 1, 0)]
    public int SmoothMaPeriod = 21;

    [InputParameter("Volume coloring scheme", 40, variants: new object[]{
        "By bar", VolumeColoringScheme.ByBar,
        "By difference", VolumeColoringScheme.ByDifference,
        "Fixed", VolumeColoringScheme.Fixed,
        "Above/below the moving average", VolumeColoringScheme.AboveBelowMA,
    })]
    public VolumeColoringScheme ColoringScheme = VolumeColoringScheme.ByDifference;

    public int MinHistoryDepths => Math.Max(this.SmoothMaPeriod, 2);
    public override string ShortName
    {
        get
        {
            string name = this.VolumeAsset == PriceType.QuoteAssetVolume ? "Quote asset volume" : this.Name;
            if (this.LinesSeries[1].Visible)
                name += $" ({this.SmoothMaPeriod})";

            return name;
        }
    }
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorVolume.cs";

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var separGroup = settings.GetItemByName("MA period") is SettingItem si
                ? si.SeparatorGroup
                : new SettingItemSeparatorGroup("");

            settings.Add(new SettingItemPairColor("PairColor", this.pairColor, 30)
            {
                Text = loc._("Histogram style"),
                SeparatorGroup = separGroup
            });

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.GetItemByName("PairColor") is SettingItemPairColor si)
            {
                this.pairColor = (PairColor)si.Value;
                this.Refresh();
            }
        }
    }

    private PairColor pairColor;
    private Indicator sma;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc.
    /// </summary>
    public IndicatorVolume()
        : base()
    {
        // Defines indicator's group, name and description.
        this.Name = "Volume";
        this.Description = "Volume allows to confirm the strength of a trend or to suggest about it's weakness.";
        this.IsUpdateTypesSupported = false;

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Volume", Color.Gray, 2, LineStyle.Histogramm);
        this.AddLineSeries("Smooth volume", Color.Orange, 2, LineStyle.Solid);
        this.SeparateWindow = true;

        this.pairColor = new PairColor()
        {
            Color1 = Color.FromArgb(255, 251, 87, 87),
            Color2 = Color.FromArgb(255, 0, 178, 89),
            Text1 = "Down",
            Text2 = "Up"
        };
    }

    protected override void OnInit()
    {
        this.sma = Core.Indicators.BuiltIn.SMA(this.SmoothMaPeriod, this.VolumeAsset);
        this.AddIndicator(this.sma);
    }

    /// <summary>
    /// Calculation entry point. This function is called when a price data updates.
    /// Will be runing under the HistoricalBar mode during history loading.
    /// Under NewTick during realtime.
    /// Under NewBar if start of the new bar is required.
    /// </summary>
    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < 2)
            return;

        double volume = this.VolumeAsset == PriceType.Volume ? this.Volume() : this.QuoteAssetVolume();

        // If symbol type or chart aggregation doesn't provide volumes - use ticks value.
        double curVolume = (double.IsNaN(volume) || volume == 0) ? this.Ticks() : volume;
        double prevVolume = (this.Count > 1) ? this.GetValue(1) : curVolume;

        this.SetValue(curVolume);

        switch (this.ColoringScheme)
        {
            case VolumeColoringScheme.ByBar:
                {
                    if (this.Open() < this.Close())
                        this.LinesSeries[0].SetMarker(0, this.pairColor.Color2);
                    else if (this.Open() > this.Close())
                        this.LinesSeries[0].SetMarker(0, this.pairColor.Color1);
                    else if(this.Open() == this.Close())
                        this.LinesSeries[0].SetMarker(0, this.LinesSeries[0].Color);
                    break;
                }
            case VolumeColoringScheme.ByDifference:
                {
                    if (prevVolume < curVolume)
                        this.LinesSeries[0].SetMarker(0, this.pairColor.Color2);
                    else if (prevVolume > curVolume)
                        this.LinesSeries[0].SetMarker(0, this.pairColor.Color1);
                    break;
                }
        }

        //
        if (this.Count < this.SmoothMaPeriod)
            return;

        double smaValue = this.sma.GetValue();
        this.SetValue(smaValue, 1, 0);

        if (this.ColoringScheme == VolumeColoringScheme.AboveBelowMA)
        {
            if (smaValue < curVolume)
                this.LinesSeries[0].SetMarker(0, this.pairColor.Color2);
            else if (smaValue > curVolume)
                this.LinesSeries[0].SetMarker(0, this.pairColor.Color1);
        }
    }

    protected override void OnClear()
    {
        if (this.sma != null)
        {
            this.RemoveIndicator(this.sma);
            this.sma.Dispose();
        }
    }

    public enum VolumeColoringScheme
    {
        ByBar,
        ByDifference,
        Fixed,
        AboveBelowMA
    }
}