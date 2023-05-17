// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Volume;

public sealed class IndicatorOnBalanceVolume : Indicator, IWatchlistIndicator
{
    private const int MIN_PERIOD = 2;
    private const double MAGIC_VALUE = 0.00001;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 1, variants: new object[] {
         "Close", PriceType.Close,
         "Open", PriceType.Open,
         "High", PriceType.High,
         "Low", PriceType.Low,
         "Typical", PriceType.Typical,
         "Medium", PriceType.Median,
         "Weighted", PriceType.Weighted,
         "Volume", PriceType.Volume,
         "Open interest", PriceType.OpenInterest
    })]
    public PriceType SourcePrice = PriceType.Close;
    [InputParameter("Calculation type", 10, variants: new object[]
    {
         "All available data", IndicatorCalculationType.AllAvailableData,
         "By period", IndicatorCalculationType.ByPeriod,
    })]
    public IndicatorCalculationType CalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

    [InputParameter("Period", 20, 2, 9999, 1, 0)]
    public int Period = 10;

    public int MinHistoryDepths => this.CalculationType == IndicatorCalculationType.AllAvailableData ? MIN_PERIOD : this.Period + 1;
    public override string ShortName => $"OBV ({this.SourcePrice})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOnBalanceVolume.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorOnBalanceVolume()
        : base()
    {
        // Serves for an identification of related indicators with different parameters.
        this.Name = "On Balance Volume";
        this.Description = "On Balance Volume (OBV) measures buying and selling pressure as a cumulative indicator that adds volume on up days and subtracts volume on down days";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("OBV Line", Color.Red, 1, LineStyle.Solid);

        this.SeparateWindow = true;
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
        if (this.Count < this.MinHistoryDepths)
            return;

        if (this.CalculationType == IndicatorCalculationType.AllAvailableData)
            this.CalculateForAllData();
        else if (this.CalculationType == IndicatorCalculationType.ByPeriod)
            this.CalculateByPeriod();
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
        double prevValue = this.GetValue(1);

        if (double.IsNaN(prevValue))
            prevValue = this.Close(1);

        double obv = this.CalculateOBV(prevValue, 0);
        this.SetValue(obv);
    }
    private void CalculateByPeriod(int offset = 0)
    {
        int startOffset = offset + this.Period;

        if (this.Count <= startOffset)
            return;

        double temp = this.GetCurrentVolume(offset);

        double obv = temp;
        for (int i = startOffset - 1; i >= offset; i--)
            obv = this.CalculateOBV(obv, i);

        this.SetValue(obv, 0, offset);
    }
    private double CalculateOBV(double prevValue, int offset = 0)
    {
        double temp = this.GetCurrentVolume(offset);

        double curPrice = this.GetPrice(this.SourcePrice, offset);
        double prevPrice = this.GetPrice(this.SourcePrice, offset + 1);

        if (this.SourcePrice == PriceType.Typical && Math.Abs(prevPrice - curPrice) < MAGIC_VALUE || prevPrice == curPrice)
            return prevValue;
        else if (curPrice > prevPrice)
            return prevValue + temp;
        else if (curPrice < prevPrice)
            return prevValue - temp;

        return temp;
    }
    private double GetCurrentVolume(int offset = 0)
    {
        double volume = this.Volume(offset);

        return volume == 0 || double.IsNaN(volume) ? this.Ticks(offset) : volume;
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