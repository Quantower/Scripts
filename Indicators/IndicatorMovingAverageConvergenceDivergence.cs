// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Oscillators;

public sealed class IndicatorMovingAverageConvergenceDivergence : Indicator, IWatchlistIndicator
{
    // Display input parameters as input fields.
    [InputParameter("Period of fast EMA", 0, 1, 999, 1, 0)]
    public int FastPeriod = 12;

    [InputParameter("Period of slow EMA", 1, 1, 999, 1, 0)]
    public int SlowPeriod = 26;

    [InputParameter("Period of signal SMA", 2, 1, 999, 1, 0)]
    public int SignalPeriod = 9;

    //
    [InputParameter("Price type", 4, variants: new object[]
    {
        "Open", PriceType.Open,
        "High", PriceType.High,
        "Low", PriceType.Low,
        "Close", PriceType.Close,
    })]
    public PriceType PriceType = PriceType.Close;
    [InputParameter("Calculation type", 5, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;
  
    public int MinHistoryDepths => this.MaxEMAPeriod + this.SignalPeriod;
    public override string ShortName => $"MACD ({this.FastPeriod}: {this.SlowPeriod}: {this.SignalPeriod})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/oscillators/moving-average-convergence-divergence";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorMovingAverageConvergenceDivergence.cs";

    private int MaxEMAPeriod => Math.Max(this.FastPeriod, this.SlowPeriod);

    private Indicator fastEMA;
    private Indicator slowEMA;
    private Indicator sma;
    private HistoricalDataCustom customHD;

    private Color level1_Color;
    private Color level2_Color;
    private Color level3_Color;
    private Color level4_Color;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorMovingAverageConvergenceDivergence()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Moving Average Convergence/Divergence";
        this.Description = "A trend-following momentum indicator that shows the relationship between two moving averages of prices";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("OsMA", Color.Green, 4, LineStyle.Histogramm);
        this.AddLineLevel(0, "0 level", Color.DarkGreen, 1, LineStyle.Solid);
        this.AddLineSeries("MACD", Color.DodgerBlue, 1, LineStyle.Solid);
        this.AddLineSeries("Signal", Color.Red, 1, LineStyle.Solid);
        this.SeparateWindow = true;

        this.level1_Color = Color.FromArgb(0, 178, 89);
        this.level2_Color = Color.FromArgb(50, this.level1_Color);
        this.level3_Color = Color.FromArgb(251, 87, 87);
        this.level4_Color = Color.FromArgb(50, this.level3_Color);
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Get two EMA and one SMA indicators from built-in indicator collection. 
        this.fastEMA = Core.Indicators.BuiltIn.EMA(this.FastPeriod, this.PriceType, this.CalculationType);
        this.slowEMA = Core.Indicators.BuiltIn.EMA(this.SlowPeriod, this.PriceType, this.CalculationType);
        this.sma = Core.Indicators.BuiltIn.SMA(this.SignalPeriod, PriceType.Close);

        // Create a custom HistoricalData and synchronize it with this(MACD) indicator.
        this.customHD = new HistoricalDataCustom(this);

        // Attach SMA indicator to custom HistoricalData. The SMA will calculate on the data, which will store in custom HD. 
        this.customHD.AddIndicator(this.sma);

        // Add auxiliary EMA indicators to the current one. 
        this.AddIndicator(this.fastEMA);
        this.AddIndicator(this.slowEMA);

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
        // Skip max period for correct calculation.  
        if (this.Count < this.MaxEMAPeriod)
            return;

        // Calculate a difference bettwen two EMA indicators and set value to 'MACD' line buffer.
        double differ = this.fastEMA.GetValue() - this.slowEMA.GetValue();
        this.SetValue(differ, 1);

        // The calculated value must be set as close price against the custom HistoricalData,
        // because the SMA indicator was initialized with the source price - PriceType.Close. 
        this.customHD[PriceType.Close, 0] = differ;

        if (this.Count < this.MinHistoryDepths)
            return;

        // Get value from SMA indicator, which is calculated based on custom HistoricalData.
        double signal = this.sma.GetValue();
        if (double.IsNaN(signal))
            return;

        // Set value to the 'Signal' line buffer.
        this.SetValue(signal, 2);

        // Set value to the 'OsMA' line buffer.
        this.SetValue(differ - signal, 0);

        var osMAValue = differ - signal;
        if (osMAValue > 0)
            this.LinesSeries[0].SetMarker(0, osMAValue > this.LinesSeries[0].GetValue(1) ? this.level1_Color : this.level2_Color);
        else
            this.LinesSeries[0].SetMarker(0, osMAValue < this.LinesSeries[0].GetValue(1) ? this.level3_Color : this.level4_Color);
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            if (settings.GetItemByName("Line_0") is SettingItemGroup lineGroup)
            {
                var items = lineGroup.Value as IList<SettingItem>;
                var separatorGeoup = items?.FirstOrDefault()?.SeparatorGroup;

                lineGroup.AddItem(new SettingItemColor("Color 1", this.level1_Color, 1) { ColorText = loc._("Color"), SeparatorGroup = separatorGeoup });
                lineGroup.AddItem(new SettingItemColor("Color 2", this.level2_Color, 1) { ColorText = loc._("Color"), SeparatorGroup = separatorGeoup });
                lineGroup.AddItem(new SettingItemColor("Color 3", this.level3_Color, 1) { ColorText = loc._("Color"), SeparatorGroup = separatorGeoup });
                lineGroup.AddItem(new SettingItemColor("Color 4", this.level4_Color, 1) { ColorText = loc._("Color"), SeparatorGroup = separatorGeoup });
            }

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.GetItemByName("Line_2") is SettingItemGroup lineGroup)
            {
                bool needUpdate = false;
                var colorsHolder = new SettingsHolder(lineGroup.Value as IList<SettingItem>);

                if (colorsHolder.TryGetValue("Color 1", out var item))
                {
                    this.level1_Color = item.GetValue<Color>();
                    needUpdate |= true;
                }

                if (colorsHolder.TryGetValue("Color 2", out item))
                {
                    this.level2_Color = item.GetValue<Color>();
                    needUpdate |= true;
                }

                if (colorsHolder.TryGetValue("Color 3", out item))
                {
                    this.level3_Color = item.GetValue<Color>();
                    needUpdate |= true;
                }

                if (colorsHolder.TryGetValue("Color 4", out item))
                {
                    this.level4_Color = item.GetValue<Color>();
                    needUpdate |= true;
                }

            if (needUpdate)
                this.OnSettingsUpdated();
        }
    }
    }
}