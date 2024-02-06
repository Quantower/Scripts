// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend;

public sealed class IndicatorIchimoku : Indicator, IWatchlistIndicator
{
    private const int TENKAN = 0;
    private const int KIJUN = 1;
    private const int SENKOU_SPANA = 2;
    private const int SENKOU_SPANB = 3;
    private const int CHINKOU_SPAN = 4;

    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Tenkan Sen", 0, 1, 9999, 1, 0)]
    public int TenkanPeriod = 9;

    [InputParameter("Kijun Sen", 1, 1, 9999, 1, 0)]
    public int KijunPeriod = 26;

    [InputParameter("Senkou Span B", 2, 1, 9999, 1, 0)]
    public int SenkouSpanB = 52;

    [InputParameter("Cloud up color", 3)]
    public Color UpColor = Color.FromArgb(50, Color.Green);

    [InputParameter("Cloud down color", 3)]
    public Color DownColor = Color.FromArgb(50, Color.Red);

    public override string ShortName => $"ICH ({this.TenkanPeriod}: {this.KijunPeriod}: {this.SenkouSpanB})";
    public int MinHistoryDepths => Math.Max(this.TenkanPeriod, Math.Max(this.KijunPeriod, this.SenkouSpanB));
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/ichimoku-indicator";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorIchimoku.cs";

    private Trend currentTrend;

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorIchimoku()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Ichimoku";
        this.Description = "Enables to quickly discern and filter 'at a glance' the low-probability trading setups from those of higher probability";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("Tenkan", Color.Blue, 1, LineStyle.Solid);
        this.AddLineSeries("Kijun", Color.Lime, 1, LineStyle.Solid);
        this.AddLineSeries("Senkou Span A", Color.SpringGreen, 1, LineStyle.Solid);
        this.AddLineSeries("Senkou Span B", Color.Red, 1, LineStyle.Solid);
        this.AddLineSeries("Chinkou Span", Color.FromArgb(255, 153, 0), 1, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        this.LinesSeries[SENKOU_SPANA].TimeShift = this.KijunPeriod;
        this.LinesSeries[SENKOU_SPANB].TimeShift = this.KijunPeriod;
        this.LinesSeries[CHINKOU_SPAN].TimeShift = -this.KijunPeriod;
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

        //1 line
        this.SetValue(this.GetAverage(this.TenkanPeriod), TENKAN);

        //2 line
        this.SetValue(this.GetAverage(this.KijunPeriod), KIJUN);

        //5 line
        this.SetValue(this.GetPrice(PriceType.Close), CHINKOU_SPAN);

        //3 line
        double senkouSpanA = (this.GetValue(0, TENKAN) + this.GetValue(0, KIJUN)) / 2;
        this.SetValue(senkouSpanA, SENKOU_SPANA);

        //4 line
        double senkouSpanB = this.GetAverage(this.SenkouSpanB);
        this.SetValue(senkouSpanB, SENKOU_SPANB);

        var newTrend = senkouSpanA == senkouSpanB ? Trend.Unknown :
            senkouSpanA > senkouSpanB ? Trend.Up : Trend.Down;

        if (this.currentTrend != newTrend)
        {
            this.EndCloud(SENKOU_SPANA, SENKOU_SPANB, this.GetColorByTrend(this.currentTrend));
            this.BeginCloud(SENKOU_SPANA, SENKOU_SPANB, this.GetColorByTrend(newTrend));
        }

        this.currentTrend = newTrend;
    }

    public double GetAverage(int period)
    {
        double high = this.GetPrice(PriceType.High);
        double low = this.GetPrice(PriceType.Low);
        for (int i = 1; i < period; i++)
        {
            double price = this.GetPrice(PriceType.High, i);
            if (high < price)
                high = price;
            price = this.GetPrice(PriceType.Low, i);
            if (low > price)
                low = price;
        }

        return (high + low) / 2.0;
    }

    private Color GetColorByTrend(Trend trend) => trend switch
    {
        Trend.Up => this.UpColor,
        Trend.Down => this.DownColor,
        _ => Color.Empty
    };

    private enum Trend
    {
        Unknown,
        Up,
        Down
    }
}