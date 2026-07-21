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
            var history = this.dailyHistoricalData;
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

            int currIndex = (int)history.GetIndexByTime(referenceTime.Ticks);
            if (currIndex < 0 || history[currIndex] is not HistoryItemBar currentBar)
            {
                result = 0;
                return false;
            }
            if (this.rvolMode == RvolMode.Regular)
            {
                currentVolume = currentBar.Volume;
            }
            else
            {
                for (int i = currIndex; i >= 0; i--)
                {
                    var prevBar = (HistoryItemBar)history[i];

                    if (prevBar.TimeLeft < startTime)
                        break;

                    currentVolume += prevBar.Volume;
                }
            }
            DateTime prevDayTime = referenceTime;
            DateTime prevStartTime = startTime;
            for (int i = 1; i <= this.period; i++)
            {
                prevDayTime = prevDayTime.AddDays(-1);
                prevStartTime = prevStartTime.AddDays(-1);
                int index = (int)history.GetIndexByTime(prevDayTime.Ticks);
                if (index < 0)
                    continue;
                var prevBar = (HistoryItemBar)history[index];
                if (this.rvolMode == RvolMode.Regular)
                {
                    averageVolume += prevBar.Volume;
                    validDays++;
                }
                else
                {
                    double cumVolume = 0;
                    for (int j = index; j >= 0; j--)
                    {
                        prevBar = (HistoryItemBar)history[j];

                        if (prevBar.TimeLeft < prevStartTime)
                            break;

                        cumVolume += prevBar.Volume;
                    }

                    averageVolume += cumVolume;
                    validDays++;
                }

            }
            if (validDays == 0 || averageVolume <= 0)
            {
                result = 0;
                return false;
            }

            result = currentVolume / (averageVolume / validDays);

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                result = 0;
                return false;
            }

            return true;

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

                for (int j = 0; j < this.HistoricalData.Count; j++)
                {
                    if (token.IsCancellationRequested)
                    {
                        this.IsLoading = false;
                        break;
                    }

                    DateTime time = this.Time(j);
                    if (this.TryCalculateRelativeVolume(time, out double relVolume))
                        this.SetValue(relVolume, 0, j);
                }
                this.IsLoading = false;
            }, token);
        }
    }
}