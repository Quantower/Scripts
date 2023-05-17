// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend;

/// <summary>
/// Helps to define the direction of the prevailing trend and the moment to close positions opened during the reversal.
/// </summary>
public sealed class IndicatorParabolicSAR : Indicator, IWatchlistIndicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Step of parabolic SAR system", 0, 0.01, 9999, 0.01, 2)]
    public double Step = 0.02;

    [InputParameter("Maximum value for the acceleration factor", 1, 0.01, 9999, 0.01, 2)]
    public double Maximum = 0.2;

    [InputParameter("Calculation type", 10, variants: new object[]
    {
        "All available data", IndicatorCalculationType.AllAvailableData,
        "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    [InputParameter("Period", 20, 2, 9999, 1, 0)]
    public int Period = 100;

    public int MinHistoryDepths => this.CalculationType == IndicatorCalculationType.AllAvailableData ? 3 : this.Period + 1;
    public override string ShortName => $"SAR ({this.Step}: {this.Maximum})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorParabolicSAR.cs";

    private bool first;
    private bool dirlong;
    private double start, last_high, last_low;
    private double ep, sar, price_low, price_high;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorParabolicSAR()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Parabolic SAR";
        this.Description = "Helps to define the direction of the prevailing trend and the moment to close positions opened during the reversal";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("SAR", Color.Firebrick, 4, LineStyle.Points);

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit() => this.ClearState();

    /// <summary>
    /// Calculation entry point. This function is called when a price data updates. 
    /// Will be runing under the HistoricalBar mode during history loading. 
    /// Under NewTick during realtime. 
    /// Under NewBar if start of the new bar is required.
    /// </summary>
    /// <param name="args">Provides data of updating reason and incoming price.</param>
    protected override void OnUpdate(UpdateArgs args)
    {
        // Checking, if current amount of bars less, than 2 - calculation is impossible.
        if (this.Count < this.MinHistoryDepths)
            return;

        if (this.CalculationType == IndicatorCalculationType.AllAvailableData)
            this.CalculateForAllData();
        else if (this.CalculationType == IndicatorCalculationType.ByPeriod)
            this.CalcualteByPeriod();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            if (settings.GetItemByName("Period") is SettingItem periodSI && settings.GetItemByName("Calculation type") is SettingItem calcType)
                periodSI.Relation = new SettingItemRelation(new Dictionary<string, IEnumerable<object>>() { ["Calculation type"] = new object[0] }, this.RelationHandler);

            return settings;
        }
        set => base.Settings = value;
    }

    private void CalculateForAllData()
    {
        double sar = this.CalcualteSAR(this.GetValue(1), 0);
        this.SetValue(sar);
    }
    private void CalcualteByPeriod(int offset = 0)
    {
        this.ClearState();

        int startOffset = offset + this.Period;

        if (this.Count <= startOffset + 1)
            return;

        double sar = 0d;
        for (int i = startOffset - 1; i >= offset; i--)
            sar = this.CalcualteSAR(sar, i);

        this.SetValue(sar, 0, offset);
    }

    private double CalcualteSAR(double prevSAR, int offset = 0)
    {
        this.price_low = this.GetPrice(PriceType.Low, offset);
        this.price_high = this.GetPrice(PriceType.High, offset);

        if (this.first)
        {
            if (this.last_low > this.price_low)
                this.last_low = this.price_low;
            if (this.last_high < this.price_high)
                this.last_high = this.price_high;

            double prev_low = this.GetPrice(PriceType.Low, offset + 1);
            double prev_high = this.GetPrice(PriceType.High, offset + 1);
            if (this.price_high > prev_high && this.price_low > prev_low)
            {
                this.first = false;
                return prev_low;
            }
            if (this.price_high < prev_high && this.price_low < prev_low)
            {
                this.dirlong = false;
                this.first = false;
                return prev_high;
            }
        }

        double price = prevSAR;

        // Check for reverse.
        if (this.dirlong && this.price_low < price)
        {
            this.start = this.Step;
            this.dirlong = false;
            this.ep = this.price_low;
            this.last_low = this.price_low;
            return this.last_high;
        }

        if (!this.dirlong && this.price_high > price)
        {
            this.start = this.Step;
            this.dirlong = true;
            this.ep = this.price_high;
            this.last_high = this.price_high;
            return this.last_low;
        }

        // Calculate current value.
        this.sar = price + this.start * (this.ep - price);

        // Check long direction.
        if (this.dirlong)
        {
            if (this.ep < this.price_high && this.start + this.Step <= this.Maximum)
                this.start += this.Step;

            if (this.price_high < this.GetPrice(PriceType.High, offset + 1) && this.Count == 2)
                this.sar = price;

            if (this.sar > this.GetPrice(PriceType.Low, offset + 1))
                this.sar = this.GetPrice(PriceType.Low, offset + 1);

            if (this.sar > this.GetPrice(PriceType.Low, offset + 2))
                this.sar = this.GetPrice(PriceType.Low, offset + 2);

            if (this.sar > this.price_low)
            {
                this.start = this.Step;
                this.dirlong = false;
                this.ep = this.price_low;
                this.last_low = this.price_low;
                return this.last_high;
            }

            if (this.ep < this.price_high)
            {
                this.last_high = this.price_high;
                this.ep = this.price_high;
            }
        }
        else
        {
            if (this.ep > this.price_low && this.start + this.Step <= this.Maximum)
                this.start += this.Step;

            if (this.price_low < this.GetPrice(PriceType.Low, offset + 1) && this.Count == 2)
                this.sar = price;

            if (this.sar < this.GetPrice(PriceType.High, offset + 1))
                this.sar = this.GetPrice(PriceType.High, offset + 1);

            if (this.sar < this.GetPrice(PriceType.High, offset + 2))
                this.sar = this.GetPrice(PriceType.High, offset + 2);

            if (this.sar < this.price_high)
            {
                this.start = this.Step;
                this.dirlong = true;
                this.ep = this.price_high;
                this.last_high = this.price_high;
                return this.last_low;
            }

            if (this.ep > this.price_low)
            {
                this.last_low = this.price_low;
                this.ep = this.price_low;
            }
        }

        return this.sar;
    }

    /// <summary>
    /// calculation process values initialization
    /// </summary>
    private void ClearState()
    {
        this.first = true;
        this.dirlong = true;
        this.start = this.Step;
        this.last_high = -10_000_000.0;
        this.last_low = 10_000_000.0;
        this.sar = 0;
    }
    private bool RelationHandler(SettingItemRelationParameters relationParameters)
    {
        bool hasChanged = false;

        try
        {
            bool isVisible = this.CalculationType == IndicatorCalculationType.ByPeriod;
            hasChanged = relationParameters.DependentItem.Visible != isVisible;

            relationParameters.DependentItem.Visible = isVisible;
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex, "Swing Index: Relation");
        }

        return hasChanged;
    }
}