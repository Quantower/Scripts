// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace ChanneIsIndicators
{
    public class IndicatorHistoricalMinMaxChange : Indicator, IWatchlistIndicator
    {
        #region Consts

        private const string HISTORICAL_DEPTH_SI_NAME = "Historical depth";
        private const string FROM_DATE_SI_NAME = "From date";

        #endregion Consts

        #region Parameters

        [InputParameter(HISTORICAL_DEPTH_SI_NAME, 10, variants: new object[]
        {
            "All available", HistoricalMinMaxDepthType.AllAvailableHistory,
            "From date", HistoricalMinMaxDepthType.FromDate,
        })]
        public HistoricalMinMaxDepthType DepthType { get; set; }

        [InputParameter(FROM_DATE_SI_NAME, 20)]
        public DateTime FromDateTime { get; set; }

        public double HighPrice { get; private set; }
        public double LowPrice { get; private set; }

        public bool IsLoadedSuccesfully { get; private set; }
        public int MinHistoryDepths => 1;

        private CancellationTokenSource globalCts;
        private readonly Font font;
        private readonly StringFormat centerCenterSF;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorHistoricalMinMaxChange.cs";

        #endregion Parameters

        public IndicatorHistoricalMinMaxChange()
        {
            this.Name = "Historical Min/Max change";

            this.AddLineSeries("High change, %", Color.Orange, 2, LineStyle.Solid);
            this.AddLineSeries("Low change, %", Color.DodgerBlue, 2, LineStyle.Solid);

            this.font = new Font("Verdana", 10, FontStyle.Regular, GraphicsUnit.Point);
            this.centerCenterSF = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            this.DepthType = HistoricalMinMaxDepthType.AllAvailableHistory;
            this.FromDateTime = Core.Instance.TimeUtils.DateTimeUtcNow.Date.AddYears(-1);

            this.SeparateWindow = true;
        }

        #region Overrides

        protected override void OnInit()
        {
            this.HighPrice = double.MinValue;
            this.LowPrice = double.MaxValue;
            this.IsLoadedSuccesfully = false;

            this.Reload();
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            if (!this.IsLoadedSuccesfully)
                return;

            var close = this.Close();

            this.CheckHighPrice(close);
            this.CheckLowPrice(close);
            this.CalculateMinMaxIndicator(close, 0);
        }
        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                if (settings.GetItemByName(FROM_DATE_SI_NAME) is SettingItemDateTime fromDateSi)
                {
                    fromDateSi.Relation = new SettingItemRelationVisibility(HISTORICAL_DEPTH_SI_NAME, new SelectItem("", (int)HistoricalMinMaxDepthType.FromDate));
                    fromDateSi.ApplyingType = SettingItemApplyingType.Manually;
                    fromDateSi.Format = DatePickerFormat.Date;
                }

                return settings;
            }
            set => base.Settings = value;
        }
        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (this.IsLoadedSuccesfully)
                return;

            var gr = args.Graphics;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            var clip = gr.Save();
            gr.SetClip(args.Rectangle);

            gr.DrawString(loc._("Loading data..."), this.font, Brushes.DodgerBlue, args.Rectangle, this.centerCenterSF);

            gr.Restore(clip);
        }
        protected override void OnClear()
        {
            this.AbortLoading();
        }

        #endregion Overrides

        #region Relaod

        internal void Reload()
        {
            this.AbortLoading();

            var token = this.globalCts.Token;
            Task.Factory.StartNew(() =>
            {
                try
                {
                    this.IsLoadedSuccesfully = false;

                    if (this.Symbol == null)
                        return;

                    //
                    var to = Core.Instance.TimeUtils.DateTimeUtcNow;
                    switch (this.DepthType)
                    {
                        case HistoricalMinMaxDepthType.AllAvailableHistory:
                            {
                                bool keepGoing = true;
                                while (keepGoing)
                                {
                                    //
                                    if (token.IsCancellationRequested)
                                        break;

                                    var from = to.AddYears(-5);
                                    var hd = this.Symbol.GetHistory(Period.YEAR1, this.Symbol.HistoryType, from, to);

                                    //
                                    if (token.IsCancellationRequested)
                                        break;

                                    if (hd == null || hd.Count == 0)
                                        break;

                                    for (int i = 0; i < hd.Count; i++)
                                    {
                                        var item = hd[i];

                                        this.CheckHighPrice(item[PriceType.High]);
                                        this.CheckLowPrice(item[PriceType.Low]);
                                    }

                                    to = from;
                                }

                                break;
                            }
                        case HistoricalMinMaxDepthType.FromDate when this.FromDateTime != default:
                            {
                                //
                                if (token.IsCancellationRequested)
                                    break;

                                var hd = this.Symbol.GetHistory(Period.DAY1, this.Symbol.HistoryType, this.FromDateTime.Date, to);

                                //
                                if (token.IsCancellationRequested)
                                    break;

                                if (hd == null || hd.Count == 0)
                                    break;

                                for (int i = 0; i < hd.Count; i++)
                                {
                                    var item = hd[i];

                                    this.CheckHighPrice(item[PriceType.High]);
                                    this.CheckLowPrice(item[PriceType.Low]);
                                }

                                break;
                            }
                    }

                    //
                    if (this.HighPrice == double.MinValue || this.LowPrice == double.MaxValue)
                        return;

                    //
                    if (!token.IsCancellationRequested)
                    {
                        for (int offset = 0; offset < this.HistoricalData.Count; offset++)
                        {
                            var close = this.HistoricalData[offset][PriceType.Close];
                            this.CalculateMinMaxIndicator(close, offset);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Core.Instance.Loggers.Log(ex, this.Name);
                }
                finally
                {
                    this.IsLoadedSuccesfully = true;
                }
            }, token);
        }
        internal void AbortLoading()
        {
            this.globalCts?.Cancel();
            this.globalCts = new CancellationTokenSource();
        }

        #endregion Reload

        #region Misc

        private void CheckHighPrice(double newPrice)
        {
            if (this.HighPrice < newPrice)
                this.HighPrice = newPrice;
        }
        private void CheckLowPrice(double newPrice)
        {
            if (this.LowPrice > newPrice)
                this.LowPrice = newPrice;
        }
        private void CalculateMinMaxIndicator(double close, int offset)
        {
            var maxChance = CalculateChangePercentage(this.HighPrice, close);
            this.SetValue(maxChance, 0, offset);

            var minChange = CalculateChangePercentage(this.LowPrice, close);
            this.SetValue(minChange, 1, offset);
        }
        private static double CalculateChangePercentage(double basePrice, double targetPrice) => Math.Round(targetPrice * 100 / basePrice - 100d, 2);

        #endregion Misc

        public enum HistoricalMinMaxDepthType
        {
            AllAvailableHistory,
            FromDate,
        }
    }
  
}