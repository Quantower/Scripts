// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace Relative_Volume
{
    public class IndicatorRelativeVolume : Indicator, IWatchlistIndicator
    {
        private HistoricalData dailyHistoricalData;
        [InputParameter("Period", 0, 1, 1000)]
        public int period = 10;
        
        private enum RvolMode { Cumulative, Regular }
        [InputParameter("RVOL Mode", 1, variants: new object[] {
            "Cumulative", RvolMode.Cumulative,
            "Regular", RvolMode.Regular
        })]
        private RvolMode rvolMode = RvolMode.Cumulative;
        private Task loadingDailyHistoryTask;
        private bool IsLoading = false;


        private CancellationTokenSource cts;
        public override string ShortName => $"RVOL ({this.period})";
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorRelativeVolume.cs";

        public IndicatorRelativeVolume()
        {
            this.Name = "Relative Volume";
            this.AddLineSeries("Relative Volume Line", Color.CadetBlue, 1, LineStyle.Histogramm);
            this.SeparateWindow = true;
        }

        protected override void OnInit()
        {
            this.RecalculateIndicator(true);
        }


        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.IsLoading || args.Reason == UpdateReason.HistoricalBar || this.HistoricalData == null)
                return;

            if (this.TryCalculateRelativeVolume(this.Time(), out double relVolume))
                this.SetValue(relVolume);
        }

        private bool TryCalculateRelativeVolume(DateTime referenceTime, out double result)
        {
            if (this.dailyHistoricalData == null)
            {
                result = 0;
                return false;
            }

            double averageVolume = 0;
            double currentVolume = 0;
            int validDays = 0;

            DateTime baseTime = referenceTime;
            DateTime startTime = baseTime.Date;

            for (int i = 0; i < this.period; i++)
            {
                double volume = 0;
                DateTime targetTime = baseTime;

                int index = (int)this.dailyHistoricalData.GetIndexByTime(targetTime.Ticks);
                if (index >= 0 && this.dailyHistoricalData[index] is HistoryItemBar bar)
                {
                    volume = bar.Volume;
                    averageVolume += volume;
                    validDays++;

                    if (i == 0 && this.rvolMode == RvolMode.Regular)
                        currentVolume = bar.Volume;
                }

                if (this.rvolMode == RvolMode.Cumulative)
                {
                    DateTime slider = targetTime;
                    double cumulative = 0;

                    Period step = this.GetStepPeriod();

                    while (startTime <= slider)
                    {
                        int idx = (int)this.dailyHistoricalData.GetIndexByTime(slider.Ticks);
                        if (idx >= 0 && this.dailyHistoricalData[idx] is HistoryItemBar b)
                            cumulative += b.Volume;

                        slider -= step.Duration;
                    }

                    if (i == 0)
                        currentVolume = cumulative;

                    averageVolume += cumulative;
                    validDays++;
                }

                startTime = startTime.AddDays(-1);
                baseTime = baseTime.AddDays(-1);
            }

            if (validDays == 0 || averageVolume == 0)
            {
                result = 0;
                return false;
            }

            result = currentVolume / (averageVolume / validDays);
            return true;
        }
        private Period GetStepPeriod()
        {
            if (this.HistoricalData == null)
                return Period.MIN1;
            if (this.HistoricalData.Aggregation is HistoryAggregationTime historyAggregationTime)
                return historyAggregationTime.Period;
            return Period.MIN1;
        }


        public int MinHistoryDepths => this.period;

        protected override void OnClear()
        {
            this.cts?.Cancel();
            base.OnClear();
        }
        private void RecalculateIndicator(bool needReload = false)
        {
            if (this.HistoricalData == null)
                return;
            if (this.HistoricalData.Aggregation is not HistoryAggregationTime)
            {
                Core.Loggers.Log("Not allowed tick aggregation", LoggingLevel.Error);
                return;
            }
            this.cts?.Cancel();
            this.cts = new CancellationTokenSource();
            var token = this.cts.Token;

            this.loadingDailyHistoryTask = Task.Factory.StartNew(() => {
                this.IsLoading = true;

                if (needReload)
                    this.dailyHistoricalData = this.Symbol.GetHistory(this.HistoricalData.Aggregation, DateTime.Today.AddDays(-this.period * 2));

                for (int j = 0; j < this.Count; j++)
                {
                    if (token.IsCancellationRequested)
                    {
                        this.IsLoading = false;
                        break;
                    }

                    DateTime time = this.Time(j);
                    if (this.TryCalculateRelativeVolume(time, out double relVolume) && j < this.Count)
                        this.SetValue(relVolume, 0, j);
                }
                this.IsLoading = false;
            }, token);
        }
    }
}