// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Volatility;

public sealed class IndicatorHistoricalVolatility : Indicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Period of STD", 0, 1, 999, 1, 0)]
    public int STDPeriod = 30;

    [InputParameter("Volatility period", 1, 1, 999, 1, 0)]
    public int VolatilityPeriod = 365;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Sources prices for MA", 2, variants: new object[] {
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

    [InputParameter("All history", 3)]
    public bool isAllHistoryMode = false;

    [InputParameter("History period", 4, 1, 999, 1, 0)]
    public int PercentilePeriod = 100;

    // Displays Input Parameter as dropdown list.
    [InputParameter("Display mode", 5, variants: new object[] {
        "HV and percentile", HVSheduleMode.HV,
        "Percentile only", HVSheduleMode.Percentile}
    )]
    public HVSheduleMode DisplayMode = HVSheduleMode.HV;

    private List<double> hvCollection;
    private Font percentileTextFont;

    public override string ShortName => $"HV ({this.STDPeriod}: {this.DisplayMode}: {(this.isAllHistoryMode ? "All history" : this.PercentilePeriod.ToString())})";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorHistoricalVolatility.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorHistoricalVolatility()
        : base()
    {
        // Defines indicator's name and description.
        this.Name = "Historical Volatility";
        this.Description = "Is the realized volatility of a financial symbol over a given time period.";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("HV", Color.Red, 2, LineStyle.Solid);

        this.SeparateWindow = true;
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var historyPeriodSetting = settings.GetItemByName("History period");
            // Create "Visibility" relation between "All history" and "History period".
            if (historyPeriodSetting != null)
                historyPeriodSetting.Relation = new SettingItemRelationVisibility("All history", false);

            return settings;
        }

        set => base.Settings = value;
    }
    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        this.hvCollection = new List<double>();
        this.percentileTextFont = new Font("Times New Roman", 12, FontStyle.Regular);
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
        if (this.Count <= this.STDPeriod)
            return;

        double hv = this.GetStandardDeviation(this.STDPeriod) * Math.Sqrt(this.VolatilityPeriod) * 100;

        if (this.DisplayMode == HVSheduleMode.HV)
            this.SetValue(hv);

        if (args.Reason != UpdateReason.NewTick)
        {
            this.hvCollection.Insert(0, hv);

            if (this.Count == this.HistoricalData.Count && this.DisplayMode == HVSheduleMode.Percentile)
            {
                for (int i = 0; i < this.hvCollection.Count; i++)
                {
                    double percentile = this.GetPercentile(i, this.hvCollection[i]);
                    this.SetValue(percentile, 0, i);
                }
            }
        }
        else
        {
            // Watchlist
            if (this.hvCollection.Count == 0)
                this.hvCollection.Insert(0, hv);
            else
                this.hvCollection[0] = hv;

            if (this.DisplayMode == HVSheduleMode.Percentile)
            {
                double percentile = this.GetPercentile(0, hv);
                this.SetValue(percentile);// Calculate only last bar
            }
        }
    }

    /// <summary>
    /// Allows to release resources. It will be called before removing an indicator.
    /// </summary>
    protected override void OnClear()
    {

    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        if (this.DisplayMode != HVSheduleMode.HV)
            return;

        var time = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(args.MousePosition.X);
        int index = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(time);

        if (index >= this.HistoricalData.Count)
            return;

        var graphics = args.Graphics;
        int offset = this.HistoricalData.Count - index - 1;

        double percentile = this.GetPercentile(offset, this.hvCollection[offset]);
        string percentileText = $"Percentile: {Math.Round(percentile, 2)} %";

        var cursorLinePen = new Pen(this.LinesSeries[0].Color);

        graphics.DrawString(percentileText, this.percentileTextFont, cursorLinePen.Brush, args.Rectangle.X, args.Rectangle.Y + 20);

        var startCursorLine = new Point(args.MousePosition.X, args.Rectangle.Y);
        var endCursorLine = new Point(args.MousePosition.X, args.Rectangle.Height + args.Rectangle.Y);

        graphics.DrawLine(cursorLinePen, startCursorLine, endCursorLine);
    }

    #region Misc

    private double GetPercentile(int targetIndex, double value)
    {
        double percentile;

        if (this.isAllHistoryMode)
        {
            // calculate HV value by using all HV values from collection;
            percentile = this.CalcPecentileInRangeOf(this.hvCollection.Count - 1, 0, value);
        }
        else
        {
            int startIndex = targetIndex + this.PercentilePeriod > this.hvCollection.Count - 1 ? this.hvCollection.Count - 1 : targetIndex + this.PercentilePeriod;
            // calculate HV value in the range of "start" to "targetIndex"
            percentile = this.CalcPecentileInRangeOf(startIndex, targetIndex, value);
        }

        return percentile;
    }

    private double CalcPecentileInRangeOf(int startIndex, int endIndex, double value)
    {
        double count = 0.0;
        for (int i = startIndex; i > endIndex; i--)
        {
            if (this.hvCollection[i] <= value)
                count += 1.0;
        }
        return count / (startIndex - endIndex) * 100.0;
    }

    private double GetStandardDeviation(int period)
    {
        //list of Ln(curPrice/prevPrice)
        var data = Enumerable.Range(0, period - 1).Select(i => Math.Log(this.GetPrice(this.SourcePrice, i) / this.GetPrice(this.SourcePrice, i + 1)));

        //calc the standard deviation by the data
        double sumOfDerivationAverage = data.Sum(value => value * value) / (period - 1);
        double average = Enumerable.Average(data);
        return Math.Sqrt(sumOfDerivationAverage - average * average);
    }

    #endregion Misc
}