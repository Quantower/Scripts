using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace IndicatorTTMSqueeze
{
    public class IndicatorTTMSqueeze : Indicator
    {
        private int Period = 20;
        private double BBCoeff = 2.0;
        private double KCCoeffHigh = 1.0;
        private double KCCoeffMid = 1.5;
        private double KCCoeffLow = 2.0;
        private PriceType calculationPriceType = PriceType.Typical;

        private Color noSqzColor = Color.Gray;
        private Color lowSqzColor = Color.Lime;
        private Color midSqzColor = Color.Yellow;
        private Color highSqzColor = Color.Tomato;

        private Color posUp = Color.Blue;
        private Color posDown = Color.LightBlue;
        private Color negUp = Color.IndianRed;
        private Color negDown = Color.DarkRed;

        private int markerSize = 10;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTTMSqueeze.cs";
        public IndicatorTTMSqueeze()
        {
            this.Name = "TTM Squeeze";
            this.SeparateWindow = true;
            this.AddLineSeries("Impulse", Color.CadetBlue, 1, LineStyle.Histogramm);
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < Period)
                return;
            double mean = GetMean(0, Period, calculationPriceType);
            double std = GetStdDev(0, Period, calculationPriceType);
            double bbUpper = mean + BBCoeff * std;
            double bbLower = mean - BBCoeff * std;

            double atr = GetMeanTR(0, Period);
            double kcHigh1 = mean + atr * KCCoeffHigh;
            double kcLow1 = mean - atr * KCCoeffHigh;
            double kcHigh2 = mean + atr * KCCoeffMid;
            double kcLow2 = mean - atr * KCCoeffMid;
            double kcHigh3 = mean + atr * KCCoeffLow;
            double kcLow3 = mean - atr * KCCoeffLow;

            double regressionValue = GetLinearRegression(0, Period);
            this.SetValue(regressionValue);
            double curr = this.GetValue(0);
            double prev = this.GetValue(1);

            Color color = noSqzColor;
            if (curr > 0)
                color = curr > prev ? posUp : posDown;
            else
                color = curr > prev ? negUp : negDown;

            this.LinesSeries[0].SetMarker(0, new IndicatorLineMarker(color));
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);
            if (this.CurrentChart == null)
                return;

            var gr = args.Graphics;
            var wnd = this.CurrentChart.Windows[args.WindowIndex];
            var converter = wnd.CoordinatesConverter;
            RectangleF prevClip = gr.ClipBounds;
            gr.SetClip(args.Rectangle);

            try
            {
                int left = (int)converter.GetBarIndex(converter.GetTime(0));
                int right = (int)converter.GetBarIndex(converter.GetTime(args.Rectangle.Width));
                left = Math.Max(0, left);
                right = Math.Min(this.HistoricalData.Count - 1, right);

                float y = (float)converter.GetChartY(0) - markerSize / 2;
                int barWidth = this.CurrentChart.BarsWidth;
                RectangleF marker = new RectangleF(0, 0, markerSize, markerSize);

                for (int i = left; i <= right; i++)
                {

                    double mean = GetMean(this.HistoricalData.Count - i - 1, Period, calculationPriceType);
                    double std = GetStdDev(this.HistoricalData.Count - i - 1, Period, calculationPriceType);
                    double bbUpper = mean + BBCoeff * std;
                    double bbLower = mean - BBCoeff * std;

                    double atr = GetMeanTR(this.HistoricalData.Count - i - 1, Period);
                    double kcHigh1 = mean + atr * KCCoeffHigh;
                    double kcLow1 = mean - atr * KCCoeffHigh;
                    double kcHigh2 = mean + atr * KCCoeffMid;
                    double kcLow2 = mean - atr * KCCoeffMid;
                    double kcHigh3 = mean + atr * KCCoeffLow;
                    double kcLow3 = mean - atr * KCCoeffLow;

                    Color sqzColor = noSqzColor;
                    if (bbLower >= kcLow1 && bbUpper <= kcHigh1)
                        sqzColor = highSqzColor;
                    else if (bbLower >= kcLow2 && bbUpper <= kcHigh2)
                        sqzColor = midSqzColor;
                    else if (bbLower >= kcLow3 && bbUpper <= kcHigh3)
                        sqzColor = lowSqzColor;

                    marker.X = (float)converter.GetChartX(this.HistoricalData[i, SeekOriginHistory.Begin].TimeLeft) + barWidth / 2 - markerSize / 2;
                    marker.Y = y;
                    gr.FillEllipse(new SolidBrush(sqzColor), marker);
                }
            }
            finally
            {
                gr.SetClip(prevClip);
            }
        }
        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;
                settings.Add(new SettingItemInteger("Period", this.Period)
                {
                    Text = "Squeeze Period",
                    SortIndex = 0,
                });
                settings.Add(new SettingItemDouble("BBCoeff", this.BBCoeff)
                {
                    Text = "BB Coefficient",
                    DecimalPlaces = 2,
                    Increment = 0.01,
                    SortIndex = 0,
                });
                settings.Add(new SettingItemDouble("KCCoeffHigh", this.KCCoeffHigh)
                {
                    Text = "KC Coefficient High",
                    SortIndex = 0,
                    DecimalPlaces = 2,
                    Increment = 0.01,
                });
                settings.Add(new SettingItemDouble("KCCoeffMid", this.KCCoeffMid)
                {
                    Text = "KC Coefficient Mid",
                    SortIndex = 0,
                    DecimalPlaces = 2,
                    Increment = 0.01,
                });
                settings.Add(new SettingItemDouble("KCCoeffLow", this.KCCoeffLow)
                {
                    Text = "KC Coefficient Low",
                    SortIndex = 0,
                    DecimalPlaces = 2,
                    Increment = 0.01,
                });
                settings.Add(new SettingItemSelectorLocalized("PriceType", this.calculationPriceType, new List<SelectItem>
                {
                    new SelectItem("Close", PriceType.Close),
                    new SelectItem("Open", PriceType.Open),
                    new SelectItem("High", PriceType.High),
                    new SelectItem("Low", PriceType.Low),
                    new SelectItem("Typical", PriceType.Typical),
                    new SelectItem("Weighted", PriceType.Weighted),
                    new SelectItem("Median", PriceType.Median),
                })
                {
                    Text = "Price Type",
                    SortIndex = 0,
                });
                settings.Add(new SettingItemColor("posUp", this.posUp)
                {
                    Text = "Negative Ascending",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("posDown", this.posDown)
                {
                    Text = "Positive Falling",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("negUp", this.negUp)
                {
                    Text = "Negative Ascending",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("negDown", this.negDown)
                {
                    Text = "Negative Falling",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("noSqzColor", this.noSqzColor)
                {
                    Text = "No Squeeze Color",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("lowSqzColor", this.lowSqzColor)
                {
                    Text = "Low Squeeze Color",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("midSqzColor", this.midSqzColor)
                {
                    Text = "Mid Squeeze Color",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemColor("highSqzColor", this.highSqzColor)
                {
                    Text = "High Squeeze Color",
                    SortIndex = 1,
                });
                settings.Add(new SettingItemInteger("markerSize", this.markerSize)
                {
                    Text = "Marker Size",
                    SortIndex = 1,
                });
                return settings;
            }
            set
            {
                base.Settings = value;
                bool needRecalculation = false;
                if (value.TryGetValue("Period", out int Period))
                {
                    this.Period = Period;
                    needRecalculation = true;
                }
                if (value.TryGetValue("BBCoeff", out double BBCoeff))
                {
                    this.BBCoeff = BBCoeff;
                    needRecalculation = true;
                }

                if (value.TryGetValue("KCCoeffHigh", out double KCCoeffHigh))
                {
                    this.KCCoeffHigh = KCCoeffHigh;
                    needRecalculation = true;
                }

                if (value.TryGetValue("KCCoeffMid", out double KCCoeffMid))
                {
                    this.KCCoeffMid = KCCoeffMid;
                    needRecalculation = true;
                }
                if (value.TryGetValue("KCCoeffLow", out double KCCoeffLow))
                {
                    this.KCCoeffLow = KCCoeffLow;
                    needRecalculation = true;
                }
                if (value.TryGetValue("PriceType", out PriceType calculationPriceType))
                {
                    this.calculationPriceType = calculationPriceType;
                    needRecalculation = true;
                }
                if (value.TryGetValue("noSqzColor", out Color noSqzColor))
                    this.noSqzColor = noSqzColor;
                if (value.TryGetValue("lowSqzColor", out Color lowSqzColor))
                    this.lowSqzColor = lowSqzColor;
                if (value.TryGetValue("midSqzColor", out Color midSqzColor))
                    this.midSqzColor = midSqzColor;
                if (value.TryGetValue("highSqzColor", out Color highSqzColor))
                    this.highSqzColor = highSqzColor;
                if (value.TryGetValue("posDown", out Color posDown))
                {
                    this.posDown = posDown;
                    needRecalculation = true;
                }
                if (value.TryGetValue("posUp", out Color posUp))
                {
                    this.posUp = posUp;
                    needRecalculation = true;
                }
                if (value.TryGetValue("negDown", out Color negDown))
                {
                    this.negDown = negDown;
                    needRecalculation = true;
                }
                if (value.TryGetValue("negUp", out Color negUp))
                {
                    this.negUp = negUp;
                    needRecalculation = true;
                }
                if (value.TryGetValue("markerSize", out int markerSize))
                    this.markerSize = markerSize;
                if (needRecalculation)
                    this.OnSettingsUpdated();
            }
        }
        private double GetMean(int bar, int length, PriceType type)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if (bar+i >= this.Count)
                    break;
                double price = this.GetPrice(type, bar + i);
                if (!double.IsNaN(price))
                {
                    sum += price;
                    count++;
                }
            }
            return count > 0 ? sum / count : double.NaN;
        }

        private double GetStdDev(int bar, int length, PriceType type)
        {
            double mean = GetMean(bar, length, type);
            double sumSq = 0;
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if (bar + i >= this.Count)
                    break;
                double price = this.GetPrice(type, bar + i);
                if (!double.IsNaN(price))
                {
                    sumSq += Math.Pow(price - mean, 2);
                    count++;
                }
            }
            return count > 0 ? Math.Sqrt(sumSq / count) : double.NaN;
        }

        private double GetMeanTR(int bar, int length)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                double tr = GetTR(bar + i);
                if (!double.IsNaN(tr))
                {
                    sum += tr;
                    count++;
                }
            }
            return count > 0 ? sum / count : double.NaN;
        }

        private double GetTR(int bar)
        {
            if (bar <= 0 || bar >= this.Count-1)
                return double.NaN;

            double high = this.GetPrice(PriceType.High, bar);
            double low = this.GetPrice(PriceType.Low, bar);
            double closePrev = this.GetPrice(PriceType.Close, bar + 1);

            if (double.IsNaN(closePrev))
                return high - low;

            return Math.Max(high - low, Math.Max(Math.Abs(high - closePrev), Math.Abs(low - closePrev)));
        }

        private double GetLinearRegression(int bar, int length)
        {
            double[] diff = new double[length];
            for (int i = 0; i < length; i++)
            {
                int index = bar + i;
                double high = this.GetPrice(PriceType.High, index);
                double low = this.GetPrice(PriceType.Low, index);
                double close = this.GetPrice(PriceType.Close, index);
                double hl2 = (high + low) / 2.0;
                double avg = GetMean(index, length, PriceType.Close);
                double baseLine = (hl2 + avg) / 2.0;
                diff[i] = close - baseLine;
            }

            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            for (int i = 0; i < length; i++)
            {
                sumX += i;
                sumY += diff[i];
                sumXY += i * diff[i];
                sumXX += i * i;
            }

            double n = length;
            double slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            return intercept + slope * (n - 1);
        }
    }
}