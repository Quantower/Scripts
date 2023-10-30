// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace Trend;

/// <summary>
/// PriceActionZones 
/// </summary>
public class IndicatorPriceActionZones : Indicator
{
    #region Properties

    private const string DEVIATION = "Deviation";
    private const string MAX_ZONE_HEIGHT = "Max Zone Height";
    private const string CONTROL_POINTS = "Control points";
    private const string MAX_BREAKOUTS = "Max Breakouts";
    private const string HISTORY_DEPTH = "History Depth";
    private const string SHOW_ZZ = "Show ZigZag";

    private readonly string[] manualyAppliedSettings;

    [InputParameter(DEVIATION, 0, 0.00001, int.MaxValue, 0.00001, 5)]
    public double deviation = 0.0001;

    [InputParameter(MAX_ZONE_HEIGHT, 1, 0.00001, int.MaxValue, 0.00001, 5)]
    public double zoneHeight = 0.0001;

    [InputParameter(CONTROL_POINTS, 2, 2, int.MaxValue, 1, 0)]
    public int ControlPoints = 2;

    [InputParameter(MAX_BREAKOUTS, 3, 0, int.MaxValue, 1, 0)]
    public int maxBreakouts = 2;

    [InputParameter(HISTORY_DEPTH, 4, 2, int.MaxValue, 1, 0)]
    public int zoneLength = 300;

    [InputParameter("Zone Color", 5)]
    public Color zoneColor = Color.FromArgb(120, 33, 150, 243);

    [InputParameter("Text Color", 6)]
    public Color fontColor = Color.FromArgb(200, 200, 200);

    [InputParameter("Line Color", 7)]
    public Color lineColor = Color.FromArgb(120, 220, 220, 0);

    [InputParameter(SHOW_ZZ, 8)]
    public bool showZZ = false;

    private Dictionary<DateTime, double> Points;
    private List<Zone> zones;
    private double prevPrice;
    private DateTime prevTime;

    private readonly SolidBrush brush;
    private readonly SolidBrush fontBrush;
    private readonly Pen linePen;
    private readonly Font font;
    private readonly StringFormat format;

    #endregion

    #region ZZ realization

    // Defines ZigZag calculation variables.
    private int trendLineLenght;
    private int retracementLenght;
    private int direction;
    private double lastTurnPoint;

    private bool AllDataLoaded => this.HistoricalData.Count == this.Count;
    private bool FirstPopulate;

    public override string ShortName => $"PAZ ({Symbol?.FormatPrice(this.deviation)}: {this.zoneHeight}: {this.zoneLength})";

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorPriceActionZones.cs";

    /// <summary>
    /// Indicator's constructor. Contains general information: name, description, LineSeries etc. 
    /// </summary>
    public IndicatorPriceActionZones()
        : base()
    {
        // Defines indicator's group, name and description.            
        this.Name = "Price Action Zones";
        this.Description = "";

        this.SeparateWindow = false;

        this.manualyAppliedSettings = new string[] { DEVIATION, MAX_ZONE_HEIGHT, CONTROL_POINTS, MAX_BREAKOUTS, HISTORY_DEPTH, SHOW_ZZ };

        this.format = new StringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        this.font = new Font("Arial", 10);
        this.linePen = new Pen(Color.DarkOrange, 2);
        this.brush = new SolidBrush(Color.FromArgb(100, Color.DarkOrange));
        this.fontBrush = new SolidBrush(Color.FromArgb(100, Color.DarkOrange));
    }

    /// <summary>
    /// This function will be called after creating an indicator as well as after its input params reset or chart (symbol or timeframe) updates.
    /// </summary>
    protected override void OnInit()
    {
        this.counter = 0;

        this.FirstPopulate = true;

        // Initializes calculation parameters.
        this.trendLineLenght = 0;
        this.retracementLenght = 0;
        this.direction = 1;
        this.lastTurnPoint = 0;

        this.Points = new Dictionary<DateTime, double>();
        this.zones = new List<Zone>();
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

        double high = High(0);
        double low = Low(0);

        // Detects uptrend moving.
        if (this.direction == 1)
        {
            // Trend continues.
            if (high >= this.lastTurnPoint)
            {
                this.lastTurnPoint = high;
                this.retracementLenght = 0;
                ProcessTrend(this.trendLineLenght + 1);
                return;
            }
            // Sloping trend detection block.
            if (low <= this.lastTurnPoint - this.deviation)
            {
                this.lastTurnPoint = low;
                this.direction = -1;
                this.trendLineLenght = this.retracementLenght;
                this.retracementLenght = 0;
                ProcessTrend(this.trendLineLenght + 1);
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
                ProcessTrend(this.trendLineLenght + 1);
                return;
            }
            // Sloping trend detection block.
            if (high >= this.lastTurnPoint + this.deviation)
            {
                this.lastTurnPoint = high;
                this.direction = 1;
                this.trendLineLenght = this.retracementLenght;
                this.retracementLenght = 0;
                ProcessTrend(this.trendLineLenght + 1);
                return;
            }
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var set = base.Settings;

            var holder = new SettingsHolder(set);

            foreach (var setItem in this.manualyAppliedSettings)
            {
                if (holder.TryGetValue(setItem, out SettingItem item))
                {
                    item.ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation;
                }
            }

            List<SettingItem> zoneItems = new List<SettingItem>();
            for (int i = 0; i < zones?.Count; ++i)
            {
                zoneItems.Add(new SettingItemGroup("Zone" + i, new List<SettingItem>
                {
                    new SettingItemDouble("Top", zones[i].Top),
                    new SettingItemDouble("Bottom", zones[i].Bottom),
                    new SettingItemDouble("Price", zones[i].Price),
                    new SettingItemDateTime("From", zones[i].From),
                    new SettingItemDateTime("To", zones[i].To),
                }));
            }
            set.Add(new SettingItemGroup("Zones", zoneItems)
            {
                VisibilityMode = VisibilityMode.Hidden
            });
            return set;
        }
        set
        {
            base.Settings = value;
        }
    }

    #endregion

    private void ProcessTrend(int x)
    {
        if (x > this.Count - 1)
            return;

        ProcessPoint(x - 1);
    }

    #region ZZ Zone 

    private void ProcessPoint(int x)
    {
        AddPoint(this.Time(x), (this.direction > 0) ? Low(x) : High(x));
    }

    private int counter = 0;

    private void AddPoint(DateTime time, double price)
    {
        var count = this.Points.Count;

        if (price == this.prevPrice)
            this.Points.Remove(this.prevTime);

        this.prevTime = time;
        this.Points[time] = this.prevPrice = price;

        if (this.AllDataLoaded && (this.FirstPopulate || this.Points.Count > count) && this.Points.Count > this.ControlPoints)
        {
            this.FirstPopulate = false;

            var fromTime = Time(Math.Min(this.Count - 1, this.zoneLength));

            var newPoints = this.Points.Where(p => p.Key > fromTime).ToDictionary(p => p.Key, p => p.Value);
            this.Points = newPoints;

            var pointsKeys = newPoints.Keys.OrderBy(p => p).ToList();
            this.zones = CreateZones(pointsKeys, pointsKeys);
        }
    }

    private bool CalculateBrakes(Zone z, List<DateTime> pointsKeys)
    {
        var gapKeys = pointsKeys.Where(p => p > z.From).Except(z.Items.Keys).ToList();
        if (gapKeys.Count < 1)
            return false;

        var count = gapKeys.Count - 1;
        for (int i = 0; i < count; i++)
        {
            var prevKey = gapKeys[i];
            var nextKey = gapKeys[i + 1];

            var prevValue = this.Points[prevKey];
            var nextValue = this.Points[nextKey];

            if ((prevValue > z.Price && nextValue < z.Price) || (prevValue < z.Price && nextValue > z.Price))
                z.Breaks++;

            if (z.Breaks > this.maxBreakouts)
                return false;
        }

        return true;
    }

    private List<Zone> CreateZones(List<DateTime> keys, List<DateTime> keysAll)
    {
        var zones = new List<Zone>();

        if (keys == null || keys.Count < this.ControlPoints)
            return zones;

        var p = this.Points[keys[0]];

        double upper = p + this.zoneHeight / 2.0d;
        double lower = p - this.zoneHeight / 2.0d;

        var zone = new Zone();
        var searcingKeys = new List<DateTime>();

        for (int i = 0; i < keys.Count; i++)
        {
            var val = this.Points[keys[i]];

            if (val > lower && val < upper)
                zone.AddItem(keys[i], val);
            else
                searcingKeys.Add(keys[i]);
        }

        if (zone.Count >= this.ControlPoints)
        {
            if (CalculateBrakes(zone, keysAll))
                zones.Add(zone);
        }

        zones.AddRange(CreateZones(searcingKeys, keysAll));

        return zones;
    }

    public override void OnPaintChart(TradingPlatform.BusinessLayer.PaintChartEventArgs args)
    {
        var gfx = args.Graphics;

        gfx.SetClip(args.Rectangle);

        gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;

        var right = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(Time());
        var height = (float)(this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(0) - this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.zoneHeight));

        this.brush.Color = this.zoneColor;
        this.fontBrush.Color = this.fontColor;
        this.linePen.Color = this.lineColor;

        var obj = new object();

        lock (obj)
        {
            foreach (var zone in this.zones)
            {
                var x = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(zone.From);
                var top = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(zone.Top);
                var bottom = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(zone.Bottom);

                var zoneHeight = (bottom - top < 1) ? 1 : bottom - top;

                var rect = new RectangleF(x, top, right - x, zoneHeight);

                gfx.FillRectangle(this.brush, rect);

                var size = gfx.MeasureString(zone.ToString(), this.font);

                var median = (bottom + top) / 2;

                gfx.FillRectangle(this.brush, right + 10, median - size.Height / 2, size.Width, size.Height);
                gfx.DrawString(zone.ToString(), this.font, this.fontBrush, right + 10, median, this.format);
            }
        }

        if (this.showZZ && this.Points.Count > 1)
        {
            Point[] drawPoints = null;

            try
            {
                drawPoints = this.Points.Select(p =>
                    new Point(
                        (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(p.Key),
                        (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(p.Value)
                        )
                    ).ToArray();
            }
            catch
            {
                return;
            }

            gfx.DrawLines(this.linePen, drawPoints);
        }
    }

    #endregion  
}

internal class Zone
{
    public double Top { get; private set; }
    public double Bottom { get; private set; }
    public double Price { get; private set; }
    public DateTime From { get; private set; }
    public DateTime To { get; private set; }
    public Dictionary<DateTime, double> Items { get; private set; }
    public int Breaks { get; set; }
    public int Count => this.Items.Count;

    public Zone()
    {
        this.Items = new Dictionary<DateTime, double>();
    }

    public void AddItem(DateTime time, double price)
    {
        this.Items[time] = price;

        this.From = this.Items.Keys.Min();

        this.Top = this.Items.Values.Max();
        this.Bottom = this.Items.Values.Min();
        this.Price = (this.Top + this.Bottom) / 2;
    }

    public void Draw(TradingPlatform.BusinessLayer.PaintChartEventArgs args)
    {

    }

    public override string ToString() => $"{this.Count}/{this.Breaks}";
}