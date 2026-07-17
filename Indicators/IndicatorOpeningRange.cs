// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators;

public class IndicatorOpeningRange : Indicator, IWatchlistIndicator
{
    #region Parameters

    private const string START_TIME_SI = "Start time";
    private const string END_TIME_SI = "End time";
    private const string DATA_SOURCE_SI = "Data source";
    private const string SHOW_LABELS_SI = "Show labels";
    private const string EXTEND_TO_DAY_END_SI = "Extend to day end";
    private const string SHOW_PRICE_FOR_LABEL_SI = "Show price for label";

    public DateTime StartTime
    {
        get
        {
            if (this.startTime == default)
                this.startTime = DateTime.Today;

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.startTime);
        }
        set => this.startTime = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(value);
    }
    private DateTime startTime;

    public DateTime EndTime
    {
        get
        {
            if (this.endTime == default)
                this.endTime = DateTime.Today.AddDays(1);

            return Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.endTime);
        }
        set => this.endTime = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(value);
    }
    private DateTime endTime;

    public OpeningRangeDataSource DataSource = OpeningRangeDataSource.LastAvailable;
    public bool ShowLabels = true;
    public bool ExtendToDayEnd = false;
    public bool ShowPriceForLabel = true;

    public bool ShowFill = true;

    public Color FillColor = Color.FromArgb(28, 33, 150, 243);

    public LineOptions FillBorderLineOptions
    {
        get => this.fillBorderLineOptions;
        private set
        {
            this.fillBorderLineOptions = value;
            this.fillBorderPen = ProcessPen(this.fillBorderPen, value);
        }
    }
    private LineOptions fillBorderLineOptions;
    private Pen fillBorderPen;

    private HistoricalData loadedHistory;
    private OpeningRange openingRange;
    private Session timeRange;
    private SolidBrush highLabelBrush;
    private SolidBrush lowLabelBrush;
    private SolidBrush middleLabelBrush;


    private Font labelFont;
    public Font LabelFont
    {
        get => this.labelFont;
        private set => this.labelFont = value;
    }

    private Color labelTextColor;
    public Color LabelTextColor
    {
        get => this.labelTextColor;
        private set
        {
            this.labelTextColor = value;
            this.labelTextBrush?.Dispose();
            this.labelTextBrush = new SolidBrush(value);
        }
    }
    private SolidBrush labelTextBrush;
    private readonly StringFormat centerNearSF;

    private CancellationTokenSource cts;
    private bool isOutRange;
    private bool inSymbolSession;

    public bool IsLoadedSuccessfully { get; private set; }
    public bool IsLoading { get; private set; }

    private const int SERIES_HIGH = 0;
    private const int SERIES_MIDDLE = 1;
    private const int SERIES_LOW = 2;
    private const int SERIES_EXT_U25 = 3;
    private const int SERIES_EXT_D25 = 4;
    private const int SERIES_EXT_U50 = 5;
    private const int SERIES_EXT_D50 = 6;
    private const int SERIES_EXT_U100 = 7;
    private const int SERIES_EXT_D100 = 8;
    private const int SERIES_EXT_U200 = 9;
    private const int SERIES_EXT_D200 = 10;
    private const int SERIES_EXT_U300 = 11;
    private const int SERIES_EXT_D300 = 12;


    public override string ShortName
    {
        get
        {
            if (this.IsLoading)
                return $"{base.ShortName} (Loading)";
            else
                return base.ShortName;
        }
    }

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOpeningRange.cs";

    #endregion Parameters

    #region IWatchlistIndicator
    public int MinHistoryDepths => 1;
    #endregion IWatchlistIndicator

    public IndicatorOpeningRange()
    {
        this.Name = "Opening Range";

        this.AddLineSeries("High line", Color.FromArgb(239, 83, 80), 2, LineStyle.Solid);
        this.AddLineSeries("Middle Line", Color.FromArgb(120, 120, 150), 1, LineStyle.Dash);
        this.AddLineSeries("Low line", Color.FromArgb(33, 150, 243), 2, LineStyle.Solid);

        this.AddLineSeries("Ext +25%", Color.FromArgb(255, 193, 7), 1, LineStyle.Dash);
        this.AddLineSeries("Ext -25%", Color.FromArgb(156, 204, 101), 1, LineStyle.Dash);

        this.AddLineSeries("Ext +50%", Color.FromArgb(255, 152, 0), 1, LineStyle.Dash);
        this.AddLineSeries("Ext -50%", Color.FromArgb(102, 187, 106), 1, LineStyle.Dash);

        this.AddLineSeries("Ext +100%", Color.FromArgb(239, 108, 0), 1, LineStyle.Dash);
        this.AddLineSeries("Ext -100%", Color.FromArgb(67, 160, 71), 1, LineStyle.Dash);


        this.AddLineSeries("Ext +200%", Color.FromArgb(230, 126, 34), 1, LineStyle.Dash);
        this.AddLineSeries("Ext -200%", Color.FromArgb(46, 204, 113), 1, LineStyle.Dash);

        this.AddLineSeries("Ext +300%", Color.FromArgb(211, 84, 0), 1, LineStyle.Dash);
        this.AddLineSeries("Ext -300%", Color.FromArgb(39, 174, 96), 1, LineStyle.Dash);

        this.LinesSeries[SERIES_MIDDLE].Visible = false;
        this.LinesSeries[SERIES_EXT_U25].Visible = false;
        this.LinesSeries[SERIES_EXT_U50].Visible = false;
        this.LinesSeries[SERIES_EXT_U100].Visible = false;
        this.LinesSeries[SERIES_EXT_U200].Visible = false;
        this.LinesSeries[SERIES_EXT_U300].Visible = false;
        this.LinesSeries[SERIES_EXT_D25].Visible = false;
        this.LinesSeries[SERIES_EXT_D50].Visible = false;
        this.LinesSeries[SERIES_EXT_D100].Visible = false;
        this.LinesSeries[SERIES_EXT_D200].Visible = false;
        this.LinesSeries[SERIES_EXT_D300].Visible = false;


        this.LabelFont = new Font("Tahoma", 10, GraphicsUnit.Pixel);
        this.LabelTextColor = Color.White;
        this.centerNearSF = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };
        this.UpdateBrushes();

        this.FillBorderLineOptions = new LineOptions()
        {
            Enabled = false,
            WithCheckBox = true,
            Color = Color.FromArgb(160, 33, 150, 243),
            LineStyle = LineStyle.Solid,
            Width = 1
        };

        this.SeparateWindow = false;
    }

    #region Overrides

    protected override void OnInit()
    {
        this.AbortLoading();

        var (startTimeUTC, endTimeUTC) = this.GetFullConvertedRangeTimes();

        this.openingRange = new OpeningRange()
        {
            Symbol = this.Symbol
        };
        this.timeRange = new Session("CurrentRange", startTimeUTC.TimeOfDay, endTimeUTC.TimeOfDay);

        this.isOutRange = false;
        this.inSymbolSession = false;
        this.Reload();
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (!this.IsLoadedSuccessfully)
            return;
        var (startTimeUTC, endTimeUTC) = this.GetFullConvertedRangeTimes();
        var currTime = this.Time();
        if (currTime < startTimeUTC || currTime > endTimeUTC)
        {
            if (!this.GetLineBreak(0, SERIES_HIGH))
                this.SetLineBreak(0, SERIES_HIGH);
            if (!this.GetLineBreak(0, SERIES_LOW))
                this.SetLineBreak(0, SERIES_LOW);
            if (!this.GetLineBreak(0, SERIES_EXT_U25))
                this.SetLineBreak(0, SERIES_EXT_U25);
            if (!this.GetLineBreak(0, SERIES_EXT_U50))
                this.SetLineBreak(0, SERIES_EXT_U50);
            if (!this.GetLineBreak(0, SERIES_EXT_U100))
                this.SetLineBreak(0, SERIES_EXT_U100);
            if (!this.GetLineBreak(0, SERIES_EXT_U200))
                this.SetLineBreak(0, SERIES_EXT_U200);
            if (!this.GetLineBreak(0, SERIES_EXT_U300))
                this.SetLineBreak(0, SERIES_EXT_U300);
            if (!this.GetLineBreak(0, SERIES_EXT_D25))
                this.SetLineBreak(0, SERIES_EXT_D25);
            if (!this.GetLineBreak(0, SERIES_EXT_D50))
                this.SetLineBreak(0, SERIES_EXT_D50);
            if (!this.GetLineBreak(0, SERIES_EXT_D100))
                this.SetLineBreak(0, SERIES_EXT_D100);
            if (!this.GetLineBreak(0, SERIES_EXT_D200))
                this.SetLineBreak(0, SERIES_EXT_D200);
            if (!this.GetLineBreak(0, SERIES_EXT_D300))
                this.SetLineBreak(0, SERIES_EXT_D300);
            if (!this.GetLineBreak(0, SERIES_MIDDLE))
                this.SetLineBreak(0, SERIES_MIDDLE);

        }
        if (!this.openingRange.IsEmpty)
        {
            double high = this.openingRange.HighPrice.Value;
            double low = this.openingRange.LowPrice.Value;
            double mid = (high + low) / 2.0;
            double range = high - low;

            this.SetValue(high, SERIES_HIGH, 0);
            this.SetValue(low, SERIES_LOW, 0);

            this.SetValue(high + 0.25 * range, SERIES_EXT_U25, 0);
            this.SetValue(high + 0.50 * range, SERIES_EXT_U50, 0);
            this.SetValue(high + 1.00 * range, SERIES_EXT_U100, 0);
            this.SetValue(high + 2.00 * range, SERIES_EXT_U200, 0);
            this.SetValue(high + 3.00 * range, SERIES_EXT_U300, 0);

            this.SetValue(low - 0.25 * range, SERIES_EXT_D25, 0);
            this.SetValue(low - 0.50 * range, SERIES_EXT_D50, 0);
            this.SetValue(low - 1.00 * range, SERIES_EXT_D100, 0);
            this.SetValue(low - 2.00 * range, SERIES_EXT_D200, 0);
            this.SetValue(low - 3.00 * range, SERIES_EXT_D300, 0);

            this.SetValue(mid, SERIES_MIDDLE, 0);
        }

        if (this.openingRange.IsEmpty && this.CurrentChart == null)
        {
            this.SetValue(0, SERIES_HIGH, 0);
            this.SetValue(0, SERIES_LOW, 0);
            this.SetValue(0, SERIES_EXT_U25, 0);
            this.SetValue(0, SERIES_EXT_U50, 0);
            this.SetValue(0, SERIES_EXT_U100, 0);
            this.SetValue(0, SERIES_EXT_U200, 0);
            this.SetValue(0, SERIES_EXT_U300, 0);
            this.SetValue(0, SERIES_EXT_D25, 0);
            this.SetValue(0, SERIES_EXT_D50, 0);
            this.SetValue(0, SERIES_EXT_D100, 0);
            this.SetValue(0, SERIES_EXT_D200, 0);
            this.SetValue(0, SERIES_EXT_D300, 0);
            this.SetValue(0, SERIES_MIDDLE, 0);
        }

    }
    protected override void OnClear()
    {
        this.AbortLoading();
        this.ProcessRealTimeSubscription(false);
    }
    public override void Dispose()
    {
        this.AbortLoading();
        base.Dispose();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var defaultSeparator = settings.GetItemByName(START_TIME_SI)?.SeparatorGroup
                                   ?? settings.FirstOrDefault()?.SeparatorGroup
                                   ?? new SettingItemSeparatorGroup("Opening Range", 10);

            var lastAvailable = new SelectItem("Last available", OpeningRangeDataSource.LastAvailable);
            var currentDayOnly = new SelectItem("Current day only", OpeningRangeDataSource.CurrentDayOnly);

            settings.Add(new SettingItemDateTime(START_TIME_SI, this.StartTime, 10)
            {
                Text = loc._(START_TIME_SI),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                SeparatorGroup = defaultSeparator,
                Value = Core.TimeUtils.ConvertFromSelectedTimeZoneToUTC(this.StartTime)
            });

            settings.Add(new SettingItemDateTime(END_TIME_SI, this.EndTime, 20)
            {
                Text = loc._(END_TIME_SI),
                Format = DatePickerFormat.LongTime,
                ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation,
                SeparatorGroup = defaultSeparator,
                Value = Core.TimeUtils.ConvertFromSelectedTimeZoneToUTC(this.EndTime)
            });

            settings.Add(new SettingItemSelectorLocalized(
                DATA_SOURCE_SI,
                new SelectItem(DATA_SOURCE_SI, this.DataSource),
                new List<SelectItem> { lastAvailable, currentDayOnly }, 25)
            {
                Text = loc._(DATA_SOURCE_SI),
                SeparatorGroup = defaultSeparator
            });

            settings.Add(new SettingItemBoolean(SHOW_LABELS_SI, this.ShowLabels, 30)
            {
                Text = loc._(SHOW_LABELS_SI),
                SeparatorGroup = defaultSeparator
            });

            settings.Add(new SettingItemBoolean(SHOW_PRICE_FOR_LABEL_SI, this.ShowPriceForLabel, 30)
            {
                Text = loc._(SHOW_PRICE_FOR_LABEL_SI),
                SeparatorGroup = defaultSeparator,
                Relation = new SettingItemRelationVisibility(SHOW_LABELS_SI, true)
            });

            settings.Add(new SettingItemFont("LabelFont", this.LabelFont, 31)
            {
                Text = loc._("Label font"),
                SeparatorGroup = defaultSeparator,
            });

            settings.Add(new SettingItemColor("LabelTextColor", this.LabelTextColor, 32)
            {
                Text = loc._("Label text color"),
                SeparatorGroup = defaultSeparator
            });
            settings.Add(new SettingItemBoolean(EXTEND_TO_DAY_END_SI, this.ExtendToDayEnd, 40)
            {
                Text = loc._(EXTEND_TO_DAY_END_SI),
                SeparatorGroup = defaultSeparator
            });

            settings.Add(new SettingItemColor("FillColor", this.FillColor, 38)
            {
                Text = loc._("Fill Color"),
                WithCheckBox = true,
                Checked = this.ShowFill,
                SeparatorGroup = defaultSeparator
            });

            settings.Add(new SettingItemLineOptions("FillBorderLineOptions", this.FillBorderLineOptions, 39)
            {
                Text = loc._("Fill border style"),
                ExcludedStyles = new LineStyle[] { LineStyle.Points },
                UseEnabilityToggler = true,
                SeparatorGroup = defaultSeparator
            });

            return settings;
        }
        set
        {
            var holder = new SettingsHolder(value);
            var needRefresh = false;

            if (holder.TryGetValue(START_TIME_SI, out SettingItem item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());
                if (this.StartTime != newValue)
                {
                    this.StartTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(END_TIME_SI, out item))
            {
                var newValue = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC(item.GetValue<DateTime>());
                if (this.EndTime != newValue)
                {
                    this.EndTime = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(DATA_SOURCE_SI, out item))
            {
                var newValue = item.GetValue<OpeningRangeDataSource>();
                if (this.DataSource != newValue)
                {
                    this.DataSource = newValue;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue(SHOW_LABELS_SI, out item) && item.Value is bool showLabels)
                this.ShowLabels = showLabels;

            if (holder.TryGetValue("LabelFont", out item) && item.Value is Font labelFont)
                this.LabelFont = labelFont;

            if (holder.TryGetValue("LabelTextColor", out item) && item.Value is Color labelTextColor)
                this.LabelTextColor = labelTextColor;

            if (holder.TryGetValue(SHOW_PRICE_FOR_LABEL_SI, out item) && item.Value is bool showPriceForLabel)
                this.ShowPriceForLabel = showPriceForLabel;

            if (holder.TryGetValue(EXTEND_TO_DAY_END_SI, out item) && item.Value is bool extendToDayEnd)
            {
                if (this.ExtendToDayEnd != extendToDayEnd)
                {
                    this.ExtendToDayEnd = extendToDayEnd;
                    needRefresh |= item.ValueChangingReason == SettingItemValueChangingReason.Manually;
                }
            }

            if (holder.TryGetValue("FillColor", out item) && item is SettingItemColor fillColor)
            {
                this.ShowFill = fillColor.Checked;
                this.FillColor = fillColor.GetValue<Color>();
            }

            if (holder.TryGetValue("FillBorderLineOptions", out item) && item is SettingItemLineOptions fillBorderOptions)
                this.FillBorderLineOptions = fillBorderOptions.Value as LineOptions;

            if (needRefresh)
                this.Refresh();

            base.Settings = value;
        }
    }

    #endregion Overrides

    #region Drawing

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        if (this.Symbol == null || this.openingRange == null || this.openingRange.IsEmpty || !this.IsLoadedSuccessfully)
            return;

        try
        {
            var gr = args.Graphics;
            gr.SetClip(args.Rectangle);

            double[] rightBarX = new double[this.LinesSeries.Length];
            int endIndexForLabel = (int)this.HistoricalData.GetIndexByTime(this.openingRange.RightTime.Ticks, SeekOriginHistory.Begin);
            if (endIndexForLabel < 0 || endIndexForLabel >= this.Count)
                endIndexForLabel = this.Count - 1;

            if (this.ExtendToDayEnd)
                endIndexForLabel = this.GetDayEndIndex(this.openingRange.EndTime, endIndexForLabel);

            DateTime labelAnchorTime = this.HistoricalData[endIndexForLabel, SeekOriginHistory.Begin].TimeLeft;

            for (int i = 0; i < this.LinesSeries.Length; i++)
            {
                rightBarX[i] = this.GetLabelRightX(
                    args.Rectangle,
                    labelAnchorTime,
                    this.LinesSeries[i].TimeShift,
                    this.ExtendToDayEnd);
            }

            var mid = (this.openingRange.LowPrice.Value + this.openingRange.HighPrice.Value) / 2;
            float highPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.openingRange.HighPrice.Value);
            float lowPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.openingRange.LowPrice.Value);
            float middlePriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(mid);

            double range = this.openingRange.HighPrice.Value - this.openingRange.LowPrice.Value;

            double extU25 = this.openingRange.HighPrice.Value + 0.25 * range;
            double extD25 = this.openingRange.LowPrice.Value - 0.25 * range;

            double extU50 = this.openingRange.HighPrice.Value + 0.50 * range;
            double extD50 = this.openingRange.LowPrice.Value - 0.50 * range;

            double extU100 = this.openingRange.HighPrice.Value + 1.00 * range;
            double extD100 = this.openingRange.LowPrice.Value - 1.00 * range;

            double extU200 = this.openingRange.HighPrice.Value + 2.00 * range;
            double extD200 = this.openingRange.LowPrice.Value - 2.00 * range;

            double extU300 = this.openingRange.HighPrice.Value + 3.00 * range;
            double extD300 = this.openingRange.LowPrice.Value - 3.00 * range;

            float extU25PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extU25);
            float extD25PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extD25);

            float extU50PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extU50);
            float extD50PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extD50);

            float extU100PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extU100);
            float extD100PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extD100);

            float extU200PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extU200);
            float extD200PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extD200);

            float extU300PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extU300);
            float extD300PriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(extD300);


            this.UpdateBrushes();
            var wnd = this.CurrentChart.MainWindow;
            var cc = wnd.CoordinatesConverter;
            int halfBar = this.CurrentChart.BarsWidth / 2;

            int startIndex = this.openingRange.StartIndex;
            if (startIndex >= 0 && startIndex < this.Count)
            {
                int endIndexToDraw = (int)this.HistoricalData.GetIndexByTime(this.openingRange.RightTime.Ticks, SeekOriginHistory.Begin);
                if (endIndexToDraw < 0 || endIndexToDraw >= this.Count)
                    endIndexToDraw = this.Count - 1;

                var startBarTime = this.HistoricalData[startIndex, SeekOriginHistory.Begin].TimeLeft;
                var endBarTime = this.HistoricalData[endIndexToDraw, SeekOriginHistory.Begin].TimeLeft;

                float xStart = (float)cc.GetChartX(startBarTime) + halfBar;
                float xEnd = (float)cc.GetChartX(endBarTime) + halfBar;

                float left = Math.Max(args.Rectangle.Left, Math.Min(xStart, xEnd));
                float right = Math.Min(args.Rectangle.Right, Math.Max(xStart, xEnd));

                if (right > left)
                {
                    float topY = Math.Min(highPriceY, lowPriceY);
                    float height = Math.Abs(highPriceY - lowPriceY);
                    var fillRect = new RectangleF(left, topY, right - left, height);

                    if (this.ShowFill)
                    {
                        var fillBrush = new SolidBrush(this.FillColor);
                        gr.FillRectangle(fillBrush, fillRect);
                    }

                    if (this.FillBorderLineOptions.Enabled && this.FillBorderLineOptions != null && this.FillBorderLineOptions.Enabled)
                    {
                        gr.DrawRectangle(
                            this.fillBorderPen,
                            fillRect.X,
                            fillRect.Y,
                            fillRect.Width,
                            fillRect.Height);
                    }
                }
            }
            if (this.ShowLabels && this.LinesSeries[SERIES_HIGH].Visible)
            {
                this.DrawBillet(
                    gr,
                    rightBarX[SERIES_HIGH],
                    highPriceY,
                    this.LinesSeries[SERIES_HIGH].Width,
                    this.ShowPriceForLabel
                    ? $"H: {this.openingRange.HighPrice.FormattedValue}"
                    : "H",
                    this.highLabelBrush,
                    this.labelTextBrush,
                    this.LabelFont,
                    LabelPosition.Upper,
                    this.centerNearSF);

                this.DrawBillet(
                    gr,
                    rightBarX[SERIES_HIGH],
                    highPriceY,
                    this.LinesSeries[SERIES_HIGH].Width,
                    this.ShowPriceForLabel
                    ? $"\u0394: {this.openingRange.DeltaPrice.FormattedValue}"
                    : "\u0394",
                    this.highLabelBrush,
                    this.labelTextBrush,
                    this.LabelFont,
                    LabelPosition.Bottom,
                    this.centerNearSF);
            }

            if (this.ShowLabels && this.LinesSeries[SERIES_LOW].Visible)
            {
                this.DrawBillet(
                    gr,
                    rightBarX[SERIES_LOW],
                    lowPriceY,
                    this.LinesSeries[SERIES_LOW].Width,
                    this.ShowPriceForLabel
                    ? $"L: {this.openingRange.LowPrice.FormattedValue}"
                    : "L",
                    this.lowLabelBrush,
                    this.labelTextBrush,
                    this.LabelFont,
                    LabelPosition.Bottom,
                    this.centerNearSF);
            }

            if (this.ShowLabels && this.LinesSeries[SERIES_MIDDLE].Visible)
            {
                this.DrawBillet(
                    gr,
                    rightBarX[SERIES_MIDDLE],
                    middlePriceY,
                    this.LinesSeries[SERIES_MIDDLE].Width,
                    this.ShowPriceForLabel
                    ? $"M: {this.Symbol.FormatPrice(mid)}"
                    : "M",
                    this.middleLabelBrush,
                    this.labelTextBrush,
                    this.LabelFont,
                    LabelPosition.Bottom,
                    this.centerNearSF);
            }

            var extLabels = new (int SeriesIndex, double Price, float PriceY, string Text, LabelPosition Position)[]
            {
            (SERIES_EXT_U25,  extU25,  extU25PriceY,  this.ShowPriceForLabel ? $"+25%: {this.Symbol.FormatPrice(extU25)}"   : "+25%",   LabelPosition.Upper),
            (SERIES_EXT_D25,  extD25,  extD25PriceY,  this.ShowPriceForLabel ? $"-25%: {this.Symbol.FormatPrice(extD25)}"   : "-25%",   LabelPosition.Bottom),

            (SERIES_EXT_U50,  extU50,  extU50PriceY,  this.ShowPriceForLabel ? $"+50%: {this.Symbol.FormatPrice(extU50)}"   : "+50%",   LabelPosition.Upper),
            (SERIES_EXT_D50,  extD50,  extD50PriceY,  this.ShowPriceForLabel ? $"-50%: {this.Symbol.FormatPrice(extD50)}"   : "-50%",   LabelPosition.Bottom),

            (SERIES_EXT_U100, extU100, extU100PriceY, this.ShowPriceForLabel ? $"+100%: {this.Symbol.FormatPrice(extU100)}" : "+100%",  LabelPosition.Upper),
            (SERIES_EXT_D100, extD100, extD100PriceY, this.ShowPriceForLabel ? $"-100%: {this.Symbol.FormatPrice(extD100)}" : "-100%",  LabelPosition.Bottom),

            (SERIES_EXT_U200, extU200, extU200PriceY, this.ShowPriceForLabel ? $"+200%: {this.Symbol.FormatPrice(extU200)}" : "+200%",  LabelPosition.Upper),
            (SERIES_EXT_D200, extD200, extD200PriceY, this.ShowPriceForLabel ? $"-200%: {this.Symbol.FormatPrice(extD200)}" : "-200%",  LabelPosition.Bottom),

            (SERIES_EXT_U300, extU300, extU300PriceY, this.ShowPriceForLabel ? $"+300%: {this.Symbol.FormatPrice(extU300)}" : "+300%",  LabelPosition.Upper),
            (SERIES_EXT_D300, extD300, extD300PriceY, this.ShowPriceForLabel ? $"-300%: {this.Symbol.FormatPrice(extD300)}" : "-300%",  LabelPosition.Bottom),
            };

            foreach (var ext in extLabels)
            {
                if (!this.ShowLabels || !this.LinesSeries[ext.SeriesIndex].Visible)
                    continue;

                using var extBrush = new SolidBrush(this.LinesSeries[ext.SeriesIndex].Color);

                this.DrawBillet(
                    gr,
                    rightBarX[ext.SeriesIndex],
                    ext.PriceY,
                    this.LinesSeries[ext.SeriesIndex].Width,
                    ext.Text,
                    extBrush,
                    this.labelTextBrush,
                    this.LabelFont,
                    ext.Position,
                    this.centerNearSF);
            }
        }

        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }
    }
    private double GetLabelRightX(Rectangle rectangle, DateTime barTime, int timeShift, bool snapToChartRight)
    {
        double x = this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(barTime)
                 + this.CurrentChart.BarsWidth / 2
                 + timeShift * this.CurrentChart.BarsWidth;

        if (snapToChartRight && this.CurrentChart.RightOffset <= 0)
            return rectangle.Right - this.CurrentChart.BarsWidth / 2;

        return x;
    }
    private void DrawBillet(Graphics gr, double rightBarX, float priceY, float offsetY, string label, Brush backgroundBrush, Brush labelBrush, Font font, LabelPosition labelPosition, StringFormat labelSF)
    {
        var labelSize = gr.MeasureString(label, font);

        float posY = labelPosition == LabelPosition.Bottom
            ? priceY + offsetY
            : priceY - labelSize.Height - offsetY;

        var rect = new RectangleF()
        {
            Height = labelSize.Height,
            Width = labelSize.Width + 5,
            X = (float)rightBarX - labelSize.Width - 5,
            Y = posY
        };

        gr.FillRectangle(backgroundBrush, rect);
        gr.DrawString(label, font, labelBrush, rect, labelSF);
    }

    private void ClearIndicatorLines(OpeningRange openingRange) => this.UpdateIndicatorLines(double.NaN, double.NaN, openingRange.StartIndex, this.Count - 1);
    private void UpdateIndicatorLines(double high, double low, int startIndex, int endIndex)
    {
        if (startIndex < 0)
            startIndex = 0;

        int startOffset = this.GetOffset(startIndex);
        int endOffset = this.GetOffset(endIndex);
        if (startOffset >= this.HistoricalData.Count)
            startOffset = this.HistoricalData.Count - 1;
        double range = high - low;

        double u25 = high + 0.25 * range;
        double u50 = high + 0.50 * range;
        double u100 = high + 1.00 * range;
        double u200 = high + 2.00 * range;
        double u300 = high + 3.00 * range;

        double d25 = low  - 0.25 * range;
        double d50 = low  - 0.50 * range;
        double d100 = low  - 1.00 * range;
        double d200 = low  - 2.00 * range;
        double d300 = low  - 3.00 * range;

        for (int i = endOffset; i <= startOffset; i++)
        {

            this.SetValue(high, SERIES_HIGH, i);
            this.SetValue(low, SERIES_LOW, i);
            this.SetValue((high + low) / 2, SERIES_MIDDLE, i);

            this.SetValue(u25, SERIES_EXT_U25, i);
            this.SetValue(u50, SERIES_EXT_U50, i);
            this.SetValue(u100, SERIES_EXT_U100, i);
            this.SetValue(u200, SERIES_EXT_U200, i);
            this.SetValue(u300, SERIES_EXT_U300, i);

            this.SetValue(d25, SERIES_EXT_D25, i);
            this.SetValue(d50, SERIES_EXT_D50, i);
            this.SetValue(d100, SERIES_EXT_D100, i);
            this.SetValue(d200, SERIES_EXT_D200, i);
            this.SetValue(d300, SERIES_EXT_D300, i);
        }
    }

    #endregion Drawing

    #region Event handlers

    private void Symbol_NewMark(Symbol symbol, Mark mark) => this.CalculateIndicator(mark.Time, mark.Price, mark.Price);
    private void Symbol_NewLast(Symbol symbol, Last last) => this.CalculateIndicator(last.Time, last.Price, last.Price);
    private void Symbol_NewQuote(Symbol symbol, Quote quote)
    {
        switch (symbol.HistoryType)
        {
            case HistoryType.Bid:
                this.CalculateIndicator(quote.Time, quote.Bid, quote.Bid);
                break;
            case HistoryType.Ask:
                this.CalculateIndicator(quote.Time, quote.Ask, quote.Ask);
                break;
        }
    }

    #endregion Event handlers

    #region Misc

    private void CalculateIndicator(IHistoryItem currentItem) => this.CalculateIndicator(currentItem.TimeLeft, currentItem[PriceType.High], currentItem[PriceType.Low]);
    private void CalculateIndicator(DateTime itemTime, double highPrice, double lowPrice)
    {
        if (this.HistoricalData == null)
            return;

        bool hasTime = this.timeRange.ContainsTime(itemTime.TimeOfDay);
        bool inSymbolSession = this.DataSource != OpeningRangeDataSource.CurrentDayOnly || (this.Symbol.CurrentSessionsInfo?.ContainsDate(itemTime) ?? false);

        bool isOut = !hasTime;
        bool needFindStartIndex = false;

        if (hasTime)
        {
            if (!this.openingRange.IsEmpty)
            {
                if (itemTime > this.openingRange.RightTime)
                    this.isOutRange = true;
            }
        }

        // in/out range
        if (isOut != this.isOutRange)
        {
            // починається нова зона
            if (!isOut)
            {
                this.ClearIndicatorLines(this.openingRange);
                this.openingRange.Clear();

                needFindStartIndex = true;
            }
        }
        // start symbol session
        else if (!hasTime && this.DataSource == OpeningRangeDataSource.CurrentDayOnly)
        {
            if (!this.openingRange.IsEmpty && inSymbolSession && !this.inSymbolSession)
            {
                this.ClearIndicatorLines(this.openingRange);
                this.openingRange.Clear();
            }
        }

        //
        if (hasTime)
        {
            if (this.openingRange.TryUpdate(highPrice, lowPrice, itemTime))
            {
                // щоб постійно не вираховувати в "OnPaint".
                if (needFindStartIndex || this.openingRange.StartIndex == -1)
                {
                    // populate
                    if (this.openingRange.LeftTime == default)
                    {
                        var startArea = new DateTime(itemTime.Date.Ticks + this.timeRange.OpenTime.Ticks, DateTimeKind.Utc);
                        var endArea = new DateTime(itemTime.Date.Ticks + this.timeRange.CloseTime.Ticks, DateTimeKind.Utc);

                        if (this.timeRange.OpenTime > this.timeRange.CloseTime)
                            startArea = startArea.AddDays(-1);

                        this.openingRange.LeftTime = startArea;
                        this.openingRange.RightTime = endArea;
                    }
                    else
                    {
                        this.openingRange.LeftTime = this.openingRange.LeftTime.AddDays(1);
                        this.openingRange.RightTime = this.openingRange.RightTime.AddDays(1);
                    }

                    this.openingRange.StartIndex = (int)this.HistoricalData.GetIndexByTime(itemTime.Ticks, SeekOriginHistory.Begin);
                }

                int endIndexToDraw = (int)this.HistoricalData.GetIndexByTime(this.openingRange.RightTime.Ticks, SeekOriginHistory.Begin);
                if (endIndexToDraw < 0 || endIndexToDraw >= this.Count)
                    endIndexToDraw = this.Count - 1;

                if (this.ExtendToDayEnd)
                    endIndexToDraw = this.GetDayEndIndex(itemTime, endIndexToDraw);

                this.UpdateIndicatorLines(openingRange.HighPrice.Value, openingRange.LowPrice.Value, openingRange.StartIndex, endIndexToDraw);
            }
        }

        this.isOutRange = isOut;
        this.inSymbolSession = inSymbolSession;
    }

    private int GetOffset(int index)
    {
        return this.Count - index - 1;
    }
    private void UpdateBrushes()
    {
        if (this.highLabelBrush == null || !this.highLabelBrush.Color.Equals(this.LinesSeries[0].Color))
            this.highLabelBrush = new SolidBrush(this.LinesSeries[SERIES_HIGH].Color);

        if (this.lowLabelBrush == null || !this.lowLabelBrush.Color.Equals(this.LinesSeries[1].Color))
            this.lowLabelBrush = new SolidBrush(this.LinesSeries[SERIES_LOW].Color);

        if (this.middleLabelBrush == null || !this.middleLabelBrush.Color.Equals(this.LinesSeries[SERIES_MIDDLE].Color))
            this.middleLabelBrush = new SolidBrush(this.LinesSeries[SERIES_MIDDLE].Color);
    }

    private void ProcessRealTimeSubscription(bool needSubscribe)
    {
        if (this.Symbol == null)
            return;

        switch (this.Symbol.HistoryType)
        {
            case HistoryType.Last:
                {
                    if (needSubscribe)
                        this.Symbol.NewLast += this.Symbol_NewLast;
                    else
                        this.Symbol.NewLast -= this.Symbol_NewLast;
                    break;
                }
            case HistoryType.Bid:
            case HistoryType.Ask:
                {
                    if (needSubscribe)
                        this.Symbol.NewQuote += this.Symbol_NewQuote;
                    else
                        this.Symbol.NewQuote -= this.Symbol_NewQuote;
                    break;
                }
            case HistoryType.Mark:
                {
                    if (needSubscribe)
                        this.Symbol.NewMark += this.Symbol_NewMark;
                    else
                        this.Symbol.NewMark -= this.Symbol_NewMark;
                    break;
                }
        }
    }
    private (DateTime startTimeUTC, DateTime endTimeUTC) GetFullConvertedRangeTimes()
    {
        var startTimeUTC = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC((DateTime)this.StartTime);
        var endTimeUTC = Core.Instance.TimeUtils.ConvertFromSelectedTimeZoneToUTC((DateTime)this.EndTime);

        var zeroBarLastUpdateTime = this.GetLastTradingUpdateTime();

        if (zeroBarLastUpdateTime != default)
        {
            if (startTimeUTC.TimeOfDay < endTimeUTC.TimeOfDay)
                startTimeUTC = zeroBarLastUpdateTime.Date.AddTicks(startTimeUTC.TimeOfDay.Ticks);
            else
                startTimeUTC = zeroBarLastUpdateTime.Date.AddDays(-1).AddTicks(startTimeUTC.TimeOfDay.Ticks);

            endTimeUTC = zeroBarLastUpdateTime.Date.AddTicks(endTimeUTC.TimeOfDay.Ticks);
        }

        //
        // Chart timezone is not equal to terminal timezone
        //
        if (this.CurrentChart != null && this.CurrentChart.CurrentTimeZone != Core.Instance.TimeUtils.SelectedTimeZone)
        {
            // from 'Utc' to termial timezone
            startTimeUTC = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(startTimeUTC);
            endTimeUTC = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(endTimeUTC);

            // from chart timezone to 'Utc'
            startTimeUTC = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startTimeUTC, this.CurrentChart.CurrentTimeZone);
            endTimeUTC = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endTimeUTC, this.CurrentChart.CurrentTimeZone);
        }
        startTimeUTC = startTimeUTC.SetKind(DateTimeKind.Utc);
        endTimeUTC = endTimeUTC.SetKind(DateTimeKind.Utc);

        return (startTimeUTC, endTimeUTC);
    }

    private DateTime GetLastTradingUpdateTime()
    {
        DateTime time;
        if (this.Symbol.HistoryType == HistoryType.BidAsk)
            time = this.Symbol.QuoteDateTime;
        else
            time = this.Symbol.LastDateTime;

        if (time == default && this.HistoricalData.Count > 0)
            time = this.HistoricalData[0, SeekOriginHistory.End].TimeLeft;

        return time;
    }
    private int GetDayEndIndex(DateTime anyUtcTimeInDay, int startFromIndex)
    {
        var tz = this.CurrentChart?.CurrentTimeZone ?? Core.Instance.TimeUtils.SelectedTimeZone;
        var day = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(anyUtcTimeInDay, tz).Date;

        int last = Math.Max(0, startFromIndex);

        for (int i = last; i < this.Count; i++)
        {
            var t = this.HistoricalData[i, SeekOriginHistory.Begin].TimeLeft;
            var d = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(t, tz).Date;

            if (d != day)
                break;

            last = i;
        }

        return last;
    }
    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        if (pen == null)
            pen = new Pen(Color.Empty);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    break;

                case LineStyle.Dot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    break;

                case LineStyle.Dash:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    break;

                case LineStyle.DashDot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = new float[] { 2, 4, 7, 4 };
                    break;

                case LineStyle.Histogramm:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = new float[] { 0.25F, 1 };
                    pen.Width = 4;
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Loggers.Log(ex);
        }

        return pen;
    }
    #endregion Misc

    #region Relaod

    public void Reload(bool forceReload = false)
    {
        var token = this.cts.Token;

        Task.Factory.StartNew(() =>
        {
            var historyCache = new List<HistoricalData>();

            try
            {
                if (this.HistoricalData.Count == 0)
                {
                    this.IsLoadedSuccessfully = true;
                    return;
                }

                this.IsLoadedSuccessfully = false;
                this.IsLoading = true;

                var (startTime, endTime) = this.GetFullConvertedRangeTimes();
                var zeroBarLastUpdateTime = this.GetLastTradingUpdateTime();

                // Hack. У випадку, якщо зона в поточному дні ще не почалася - грузимо і показуємо попередню.
                if (this.DataSource == OpeningRangeDataSource.LastAvailable && zeroBarLastUpdateTime < startTime)
                {
                    startTime = startTime.AddDays(-1);
                    endTime = endTime.AddDays(-1);
                }

                var ceilingStartDT = startTime.CeilingTo(Period.MIN1);
                var floorEndDT = endTime.FloorTo(Period.MIN1);

                // left part
                if (!token.IsCancellationRequested && startTime < ceilingStartDT)
                {
                    historyCache.Add(this.Symbol.GetHistory(new HistoryRequestParameters()
                    {
                        CancellationToken = token,
                        Aggregation = new HistoryAggregationTime(Period.SECOND1, this.Symbol.HistoryType),
                        Symbol = this.Symbol,
                        FromTime = startTime,
                        ToTime = ceilingStartDT,
                        ForceReload = forceReload,
                    }));
                }

                // middle part
                if (!token.IsCancellationRequested && ceilingStartDT < floorEndDT)
                {
                    historyCache.Add(this.Symbol.GetHistory(new HistoryRequestParameters()
                    {
                        CancellationToken = token,
                        Aggregation = new HistoryAggregationTime(Period.MIN1, this.Symbol.HistoryType),
                        Symbol = this.Symbol,
                        FromTime = ceilingStartDT,
                        ToTime = floorEndDT,
                        ForceReload = forceReload,
                    }));
                }

                // right part
                if (!token.IsCancellationRequested && floorEndDT < endTime)
                {
                    historyCache.Add(this.Symbol.GetHistory(new HistoryRequestParameters()
                    {
                        CancellationToken = token,
                        Aggregation = new HistoryAggregationTime(Period.SECOND1, this.Symbol.HistoryType),
                        Symbol = this.Symbol,
                        FromTime = floorEndDT,
                        ToTime = endTime,
                        ForceReload = forceReload,
                    }));
                }

                //
                if (!token.IsCancellationRequested)
                {
                    foreach (var item in historyCache)
                    {
                        if (token.IsCancellationRequested)
                            break;

                        for (int i = 0; i < item.Count; i++)
                        {
                            if (i % 1000 == 0 && token.IsCancellationRequested)
                                break;

                            var currentItem = item[i, SeekOriginHistory.Begin];
                            this.CalculateIndicator(currentItem);
                        }
                    }

                    if (!token.IsCancellationRequested)
                    {
                        this.ProcessRealTimeSubscription(true);
                    }
                }

                this.IsLoadedSuccessfully = true;
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex, "IndicatorOpeningRange:Reload ");
            }
            finally
            {
                foreach (var item in historyCache)
                    item.Dispose();

                historyCache.Clear();

                this.IsLoading = false;
            }

        }, token);
    }
    private void AbortLoading()
    {
        this.cts?.Cancel();
        this.cts = new CancellationTokenSource();
    }

    #endregion Reload

    #region Nested

    private class OpeningRange
    {
        public Symbol Symbol { get; init; }
        public Price HighPrice { get; private set; }
        public Price LowPrice { get; private set; }
        public Price DeltaPrice { get; private set; }

        public int StartIndex { get; set; }
        public bool IsEmpty { get; private set; }

        //
        public DateTime RightTime { get; internal set; }
        public DateTime LeftTime { get; internal set; }

        // фактичний початок/кінець зони
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }

        public OpeningRange()
        {
            this.Clear();
        }

        public void Clear()
        {
            this.HighPrice = Price.DefaultHigh;
            this.LowPrice = Price.DefaultLow;
            this.DeltaPrice = Price.Defalt;
            this.StartIndex = -1;
            this.StartTime = default;
            this.EndTime = default;

            this.IsEmpty = true;
        }

        internal bool TryUpdate(double high, double low, DateTime time)
        {
            bool isUpdated = false;

            // check prices
            if (this.HighPrice.Value < high)
            {
                this.HighPrice.Set(high, this.Symbol);
                isUpdated = true;
            }
            if (this.LowPrice.Value > low)
            {
                this.LowPrice.Set(low, this.Symbol);
                isUpdated = true;
            }

            // update 'delta' price
            if (isUpdated)
                this.DeltaPrice.Set(this.HighPrice.Value - this.LowPrice.Value, this.Symbol);

            // update indexes
            if (this.IsEmpty)
                this.StartTime = time;
            this.EndTime = time;

            this.IsEmpty = false;

            return isUpdated;
        }
        internal bool TryUpdate(double close, DateTime time)
        {
            return this.TryUpdate(close, close, time);
        }
    }
    private class Price
    {
        public static Price DefaultHigh => new() { Value = double.MinValue, FormattedValue = string.Empty };
        public static Price DefaultLow => new() { Value = double.MaxValue, FormattedValue = string.Empty };
        public static Price Defalt => new() { Value = default, FormattedValue = string.Empty };

        public double Value { get; private set; }
        public string FormattedValue { get; private set; }

        internal void Set(double value, Symbol symbol)
        {
            this.Value = value;

            if (symbol != null)
                this.FormattedValue = symbol.FormatPrice(value);
            else if (this.Value != DefaultHigh.Value || this.Value != DefaultLow.Value)
                this.FormattedValue = value.ToString();
        }
    }

    #endregion Nested
}

public enum OpeningRangeDataSource
{
    LastAvailable,
    CurrentDayOnly
}