// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace Trend;

/// <summary>
/// A trend following indicator that is used to predict when a given symbol's momentum is reversing. 
/// </summary>
public sealed class IndicatorZigZag : Indicator
{
    // Displays Input Parameter as input field (or checkbox if value type is bolean).
    [InputParameter("Percent Deviation", 0, 0.01, 100.0, 0.01, 2)]
    public double deviation = 5;

    // Defines ZigZag calculation variables.
    private int trendLineLenght;
    private int retracementLenght;
    private int direction;
    private double lastTurnPoint;

    public override string ShortName => $"ZZ ({this.deviation})";
    public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/trend/zigzag";
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorZigZag.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorZigZag()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "ZigZag";
        this.Description = "A trend following indicator that is used to predict when a given security's momentum is reversing. ";

        // Defines line on demand with particular parameters.
        this.AddLineSeries("ZZ'Line", Color.Yellow, 2, LineStyle.Solid);

        this.SeparateWindow = false;
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        // Initializes calculation parameters.
        this.trendLineLenght = 0;
        this.retracementLenght = 0;
        this.direction = 1;
        this.lastTurnPoint = 0;
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
        // Changes calculation parameters on each bar.
        if (args.Reason != UpdateReason.NewTick)
        {
            this.trendLineLenght++;
            this.retracementLenght++;
        }

        if (this.Count == 0)
            return;

        double high = this.High(0);
        double low = this.Low(0);

        // Detects uptrend moving.
        if (this.direction == 1)
        {
            // Trend continues.
            if (high >= this.lastTurnPoint)
            {
                this.lastTurnPoint = high;
                this.retracementLenght = 0;
                this.DrawTrendLine(this.trendLineLenght + 1);
                return;
            }
            // Sloping trend detection block.
            if (low <= this.lastTurnPoint - this.lastTurnPoint * this.deviation / 100)
            {
                this.lastTurnPoint = low;
                this.direction = -1;
                this.trendLineLenght = this.retracementLenght;
                this.retracementLenght = 0;
                this.DrawTrendLine(this.trendLineLenght + 1);
                return;
            }
        }
        // Detects downtrend moving.
        if (this.direction == -1)
        {
            // Trend continues.
            if (low <= this.lastTurnPoint)
            {
                this.lastTurnPoint = low;
                this.retracementLenght = 0;
                this.DrawTrendLine(this.trendLineLenght + 1);
                return;
            }
            // Sloping trend detection block.
            if (high >= this.lastTurnPoint + this.lastTurnPoint * this.deviation / 100)
            {
                this.lastTurnPoint = high;
                this.direction = 1;
                this.trendLineLenght = this.retracementLenght;
                this.retracementLenght = 0;
                this.DrawTrendLine(this.trendLineLenght + 1);
                return;
            }
        }
    }

    /// <summary>
    /// Trend line drawing
    /// </summary>
    private void DrawTrendLine(int x)
    {
        if (x > this.Count - 1)
            return;

        double high = this.High(0);
        double low = this.Low(0);

        for (int i = 0; i < x; i++)
        {
            double y = (this.direction > 0) ? high - i * (high - this.Low(x - 1)) / (x - 1) : low - i * (low - this.High(x - 1)) / (x - 1);
            this.SetValue(y, 0, i);
        }
    }
}