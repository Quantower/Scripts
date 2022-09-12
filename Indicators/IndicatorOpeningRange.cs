// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators
{
    public class IndicatorOpeningRange : Indicator, IWatchlistIndicator
    {
        #region Parameters

        private const string START_TIME_SI = "Start time";
        private const string END_TIME_SI = "End time";

        [InputParameter(START_TIME_SI, 10)]
        public DateTime StartTime
        {
            get
            {
                if (this.startTime == default)
                    this.startTime = Core.Instance.TimeUtils.DateTimeUtcNow.Date;

                return this.startTime;
            }
            set => this.startTime = value;
        }
        private DateTime startTime;

        [InputParameter(END_TIME_SI, 20)]
        public DateTime EndTime
        {
            get
            {
                if (this.endTime == default)
                    this.endTime = Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddDays(1).AddTicks(-1);

                return this.endTime;
            }
            set => this.endTime = value;
        }
        private DateTime endTime;

        [InputParameter("Data source", 25, variants: new object[]
        {
            "LastAvailable", OpeningRangeDataSource.LastAvailable,
            "CurrentDayOnly", OpeningRangeDataSource.CurrentDayOnly,
        })]
        public OpeningRangeDataSource DataSource = OpeningRangeDataSource.LastAvailable;

        [InputParameter("Show labels", 30)]
        public bool ShowLabels = true;

        private HistoricalData loadedHistory;
        private OpeningRange openingRange;
        private Session timeRange;
        private SolidBrush highLabelBrush;
        private SolidBrush lowLabelBrush;
        private readonly Font font;
        private readonly StringFormat centerNearSF;

        private CancellationTokenSource cts;
        private bool isOutRange;
        private bool inSymbolSession;

        public bool IsLoadedSuccessfully { get; private set; }

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOpeningRange.cs";

        #endregion Parameters

        #region IWatchlistIndicator
        public int MinHistoryDepths => 1;
        #endregion IWatchlistIndicator

        public IndicatorOpeningRange()
        {
            this.Name = "Opening Range";

            this.AddLineSeries("High line", Color.FromArgb(239, 83, 80), 2, LineStyle.Solid);
            this.AddLineSeries("Low line", Color.FromArgb(33, 150, 243), 2, LineStyle.Solid);

            this.font = new Font("Verdana", 10, GraphicsUnit.Pixel);
            this.centerNearSF = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near
            };
            this.UpdateBrushes();

            this.SeparateWindow = false;
        }

        #region Overrides
        protected override void OnInit()
        {
            this.AbortPreviousTask();

            var startTimeUTC = new DateTime(this.StartTime.Ticks, DateTimeKind.Utc);
            var endTimeUTC = new DateTime(this.EndTime.Ticks, DateTimeKind.Utc);

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

            // update zero bar
            if (!this.openingRange.IsEmpty && args.Reason == UpdateReason.NewBar)
            {
                this.SetValue(this.openingRange.HighPrice.Value, 0, 0);
                this.SetValue(this.openingRange.LowPrice.Value, 1, 0);
            }

            // for watchlist
            if (this.openingRange.IsEmpty)
            {
                this.SetValue(0, 0, 0);
                this.SetValue(0, 1, 0);
            }
        }
        protected override void OnClear()
        {
            this.AbortPreviousTask();
        }
        public override void Dispose()
        {
            this.AbortPreviousTask();

            base.Dispose();
        }
        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                if (settings.GetItemByName(START_TIME_SI) is SettingItemDateTime startSI && settings.GetItemByName(END_TIME_SI) is SettingItemDateTime endSI)
                {
                    startSI.Format = endSI.Format = DatePickerFormat.LongTime;
                    startSI.ApplyingType = endSI.ApplyingType = SettingItemApplyingType.Manually;
                }

                return settings;
            }
            set
            {
                if (value.Count == 1 && value.GetItemByName("Show labels") is SettingItemBoolean showLabels)
                    this.ShowLabels = (bool)showLabels.Value;
                else
                    base.Settings = value;
            }
        }
        #endregion Overrides

        #region Drawing
        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (this.Symbol == null || this.openingRange.IsEmpty || !this.ShowLabels || !this.IsLoadedSuccessfully)
                return;

            try
            {
                var gr = args.Graphics;
                gr.SetClip(args.Rectangle);

                double rightBarHighLineX = 0d;
                double rightBarLowLineX = 0d;

                var rightBarTime = this.Time();

                if (this.LinesSeries.Any(l => l.TimeShift != 0))
                {
                    rightBarHighLineX = this.GetLabelRightX(args.Rectangle, rightBarTime, this.LinesSeries[0].TimeShift);
                    rightBarLowLineX = this.GetLabelRightX(args.Rectangle, rightBarTime, this.LinesSeries[1].TimeShift);
                }
                else
                {
                    rightBarHighLineX = rightBarLowLineX = this.GetLabelRightX(args.Rectangle, rightBarTime, 0);
                }

                float highPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.openingRange.HighPrice.Value);
                float lowPriceY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(this.openingRange.LowPrice.Value);

                this.UpdateBrushes();

                // draw 'High' billet
                if (this.LinesSeries[0].Visible)
                    this.DrawBillet(gr, rightBarHighLineX, highPriceY, this.LinesSeries[0].Width, $"\u0394: {this.openingRange.DeltaPrice.FormattedValue}", this.highLabelBrush, Brushes.White, this.font, LabelPosition.Upper, this.centerNearSF);
            }
            catch(Exception ex)
            {
                Core.Loggers.Log(ex);
            }
        }
        private double GetLabelRightX(Rectangle rectangle, DateTime barTime, int timeShift)
        {
            if (this.CurrentChart.RightOffset > 0)
                return this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(barTime) + this.CurrentChart.BarsWidth / 2 + timeShift * this.CurrentChart.BarsWidth;
            else
                return rectangle.Right - this.CurrentChart.BarsWidth / 2;
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
        #endregion Drawing

        #region Event handlers
        private void LoadedHistory_NewHistoryItem(object sender, HistoryEventArgs e) => this.CalculateIndicator(e.HistoryItem);
        private void LoadedHistory_HistoryItemUpdated(object sender, HistoryEventArgs e) => this.CalculateIndicator(e.HistoryItem);
        #endregion Event handlers

        #region Misc

        private void CalculateIndicator(IHistoryItem currentItem)
        {
            if (this.HistoricalData == null)
                return;

            bool hasTime = this.timeRange.ContainsTime(currentItem.TimeLeft.TimeOfDay);
            bool inSymbolSession = this.DataSource != OpeningRangeDataSource.CurrentDayOnly || (this.Symbol.CurrentSessionsInfo?.ContainsDate(currentItem.TimeLeft) ?? false);

            bool isOut = !hasTime;
            bool needFindStartIndex = false;

            if (hasTime)
            {
                if (!this.openingRange.IsEmpty)
                {
                    if (currentItem.TimeLeft > this.openingRange.RightTime)
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
                if (this.openingRange.TryUpdate(currentItem[PriceType.High], currentItem[PriceType.Low], currentItem.TimeLeft))
                {
                    // щоб постійно не вираховувати в "OnPaint".
                    if (needFindStartIndex)
                    {
                        // populate
                        if (this.openingRange.LeftTime == default)
                        {
                            var startArea = new DateTime(currentItem.TimeLeft.Date.Ticks + this.timeRange.OpenTime.Ticks, DateTimeKind.Utc);
                            var endArea = new DateTime(currentItem.TimeLeft.Date.Ticks + this.timeRange.CloseTime.Ticks, DateTimeKind.Utc);

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

                        this.openingRange.StartIndex = (int)this.HistoricalData.GetIndexByTime(currentItem.TicksLeft, SeekOriginHistory.Begin);
                    }

                    this.UpdateIndicatorLines(this.openingRange);
                }
            }

            this.isOutRange = isOut;
            this.inSymbolSession = inSymbolSession;
        }
        private void UpdateIndicatorLines(OpeningRange openingRange)
        {
            this.UpdateIndicatorLines(openingRange.HighPrice.Value, openingRange.LowPrice.Value, openingRange.StartIndex, this.Count - 1);
        }
        private void ClearIndicatorLines(OpeningRange openingRange) => this.UpdateIndicatorLines(double.NaN, double.NaN, openingRange.StartIndex, this.Count - 1);
        private void UpdateIndicatorLines(double high, double low, int startIndex, int endIndex)
        {
            if (startIndex < 0)
                startIndex = 0;

            int startOffset = this.GetOffset(startIndex);
            int endOffset = this.GetOffset(endIndex);

            for (int i = endOffset; i <= startOffset; i++)
            {
                this.SetValue(high, 0, i);
                this.SetValue(low, 1, i);
            }
        }
        private int GetOffset(int index)
        {
            return this.Count - index - 1;
        }
        private void UpdateBrushes()
        {
            if (this.highLabelBrush == null || !this.highLabelBrush.Color.Equals(this.LinesSeries[0].Color))
                this.highLabelBrush = new SolidBrush(this.LinesSeries[0].Color);

            if (this.lowLabelBrush == null || !this.lowLabelBrush.Color.Equals(this.LinesSeries[1].Color))
                this.lowLabelBrush = new SolidBrush(this.LinesSeries[1].Color);
        }

        #endregion Misc

        #region Relaod

        public void Reload(bool forceReload = false)
        {
            var token = this.cts.Token;

            var currentTime = this.HistoricalData.Count > 0
                ? this.HistoricalData[0].TimeLeft
                : Core.Instance.TimeUtils.DateTimeUtcNow;

            Task.Factory.StartNew(() => {
                this.IsLoadedSuccessfully = false;

                this.loadedHistory = this.Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Period = Period.MIN1,
                    CancellationToken = token,
                    Aggregation = new HistoryAggregationTime(Period.MIN1),
                    Symbol = this.Symbol,
                    FromTime = currentTime.AddDays(-2),
                    ForceReload = forceReload,
                    HistoryType = this.Symbol.HistoryType
                });

                for (int i = 0; i < this.loadedHistory.Count; i++)
                {
                    var currentItem = this.loadedHistory[i, SeekOriginHistory.Begin];
                    this.CalculateIndicator(currentItem);
                }

                this.loadedHistory.HistoryItemUpdated += this.LoadedHistory_HistoryItemUpdated;
                this.loadedHistory.NewHistoryItem += this.LoadedHistory_NewHistoryItem;

                this.IsLoadedSuccessfully = true;
            }, token);
        }
        private void AbortPreviousTask()
        {
            if (this.loadedHistory != null)
            {
                this.loadedHistory.HistoryItemUpdated -= this.LoadedHistory_HistoryItemUpdated;
                this.loadedHistory.NewHistoryItem -= this.LoadedHistory_NewHistoryItem;
                this.loadedHistory.Dispose();
            }

            if (this.cts != null)
                this.cts.Cancel();

            this.cts = new CancellationTokenSource();
        }

        #endregion Reload

        #region Nested

        private class OpeningRange
        {
            public Symbol Symbol { get; set; }
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
}
