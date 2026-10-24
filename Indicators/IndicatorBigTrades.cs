using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace BigTradesIndicator
{
    /// <summary>
    /// Big Trades for Quantower: bubbles on the main chart for large prints.
    ///
    /// Source: real trades (Last), live via Symbol.NewLast plus an optional history load of ticks.
    /// Trades of the same side that arrive within AggregateMs of each other are merged into ONE print
    /// (sweeps / iceberg fills), at the volume-weighted price. A print is shown when its total size is
    ///   >= MinSize                                   (fixed threshold), or, if AutoMult > 0,
    ///   >= max(MinSize, AutoMult * average trade size)   (adaptive threshold).
    /// Colour = aggressor: green = buyer (market buy), red = seller (market sell).
    /// Bubble radius grows with sqrt(size / threshold). Needs a feed that sends Last (time and sales);
    /// the aggressor is taken from the feed, or inferred (bid/ask, then tick rule) when it is not set.
    /// </summary>
    public class BigTrades : Indicator
    {
        [InputParameter("Min trade size", 0, 1, 1000000, 1, 0)]
        public int MinSize = 200;

        [InputParameter("Auto threshold (x avg trade size, 0 = off)", 1, 0, 1000, 0.5, 1)]
        public double AutoMult = 0;

        [InputParameter("Aggregate window (ms, 0 = off)", 2, 0, 5000, 10, 0)]
        public int AggregateMs = 100;

        [InputParameter("History to load (hours, 0 = off)", 3, 0, 240, 1, 0)]
        public int HistoryHours = 240;

        [InputParameter("Min bubble radius (px)", 4, 3, 50, 1, 0)]
        public int MinRadius = 6;

        [InputParameter("Max bubble radius (px)", 5, 5, 100, 1, 0)]
        public int MaxRadius = 26;

        [InputParameter("Bubble fill opacity (0-255)", 6, 0, 255, 5, 0)]
        public int Opacity = 60;

        [InputParameter("Show size labels", 7)]
        public bool ShowLabels = true;

        [InputParameter("Size for max bubble (x threshold)", 12, 1.5, 100, 0.5, 1)]
        public double MaxAtMult = 10;

        [InputParameter("Border width (px)", 11, 1, 6, 1, 0)]
        public int BorderWidth = 2;

        [InputParameter("Max bubbles kept", 8, 100, 20000, 100, 0)]
        public int MaxTrades = 3000;

        [InputParameter("Show debug info", 10)]
        public bool ShowDebug = false;

        [InputParameter("Time offset (hours, manual fix)", 9, -24, 24, 1, 0)]
        public int ManualOffsetHours = 0;

        public Color BuyColor = Color.FromArgb(0, 200, 120);
        public Color SellColor = Color.FromArgb(235, 60, 60);

        private class Print
        {
            public DateTime Time;   // chart time of the first fill
            public double Price;    // volume-weighted
            public double Size;
            public bool Buy;
        }

        private readonly List<Print> prints = new List<Print>();
        private readonly object sync = new object();

        private Print pending;
        private DateTime pendingLast;
        private double avgSize;
        private bool avgInit;
        private double lastPrice = double.NaN;
        private bool lastSideBuy = true;

        private bool started;
        private TimeSpan tzOffset = TimeSpan.Zero;
        private bool tzReady;
        private string status = "";
        private string histInfo = "";
        private long histCount, liveCount;
        private double maxPrint;

        public override string ShortName => "Big Trades";

        public BigTrades()
        {
            Name = "Big Trades";
            Description = "Bubbles for large trades (aggregated prints, coloured by aggressor)";
            SeparateWindow = false;
        }

        protected override void OnInit()
        {
            lock (sync)
            {
                prints.Clear();
                pending = null;
                avgInit = false;
                avgSize = 0;
            }
            started = false;
            status = "";
            histCount = 0; liveCount = 0; maxPrint = 0;

            if (Symbol != null)
                Symbol.NewLast += Symbol_NewLast;
        }

        protected override void OnClear()
        {
            if (Symbol != null)
                Symbol.NewLast -= Symbol_NewLast;
        }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (started || Count <= 0)
                return;

            started = true;

            tzReady = false;

            if (HistoryHours > 0)
                Task.Run(() => LoadHistory());
        }

        // ---------------------------------------------------------------- data

        private void Symbol_NewLast(Symbol symbol, Last last)
        {
            if (last == null)
                return;
            Accept(DateTime.UtcNow, last.Price, last.Size, last.AggressorFlag, true);
        }

        private void LoadHistory()
        {
            try
            {
                DateTime anchor = DateTime.UtcNow;
                try
                {
                    DateTime lastBar = HistoricalData[0, SeekOriginHistory.End].TimeLeft;
                    if (lastBar > DateTime.UtcNow.AddDays(-10) && lastBar < anchor)
                        anchor = lastBar.AddMinutes(5);
                }
                catch { }
                DateTime from = anchor.AddHours(-HistoryHours);
                var hist = Symbol.GetHistory(Period.TICK1, HistoryType.Last, from);
                if (hist == null)
                {
                    status = "no tick history";
                    return;
                }

                int n = hist.Count;
                int used = 0;
                long skippedOld = 0;
                DateTime tickMin = DateTime.MaxValue, tickMax = DateTime.MinValue;
                string firstType = n > 0 && hist[0, SeekOriginHistory.Begin] != null
                    ? hist[0, SeekOriginHistory.Begin].GetType().Name : "none";
                for (int i = 0; i < n; i++)
                {
                    var item = hist[i, SeekOriginHistory.Begin] as HistoryItemLast;
                    if (item == null)
                        continue;
                    if (item.TimeLeft < from)
                    {
                        skippedOld++;
                        continue;
                    }
                    if (item.TimeLeft < tickMin) tickMin = item.TimeLeft;
                    if (item.TimeLeft > tickMax) tickMax = item.TimeLeft;
                    Accept(item.TimeLeft, item.Price, item.Volume, item.AggressorFlag, false);
                    used++;
                }

                lock (sync)
                    FlushPending();

                (hist as IDisposable)?.Dispose();
                histInfo = "from=" + from.ToString("dd.MM HH:mm") + " n=" + n + " old=" + skippedOld +
                           " ticks " + (used > 0 ? tickMin.ToString("dd.MM HH:mm") + "-" + tickMax.ToString("dd.MM HH:mm") : "-") +
                           " tz=" + tzOffset.TotalHours.ToString("0.#") + "h";
                status = used == 0 ? "no ticks used: count=" + n + ", first item=" + firstType + ", from=" + from.ToString("dd.MM HH:mm") : "";
            }
            catch (Exception ex)
            {
                status = "history load failed: " + ex.Message;
            }
        }

        private bool Infer(double price, bool live)
        {
            if (live)
            {
                double ask = Symbol.Ask, bid = Symbol.Bid;
                if (ask > 0 && price >= ask)
                    return true;
                if (bid > 0 && price <= bid)
                    return false;
            }

            // tick rule: up-tick = buy, down-tick = sell, same price = previous side
            if (!double.IsNaN(lastPrice))
            {
                if (price > lastPrice)
                    lastSideBuy = true;
                else if (price < lastPrice)
                    lastSideBuy = false;
            }
            lastPrice = price;
            return lastSideBuy;
        }

        private double Threshold()
        {
            double thr = Math.Max(1, MinSize);
            if (AutoMult > 0 && avgInit)
                thr = Math.Max(thr, AutoMult * avgSize);
            return thr;
        }

        // must be called under lock(sync)
        private void FlushPending()
        {
            if (pending == null)
                return;

            if (pending.Size > maxPrint)
                maxPrint = pending.Size;
            if (pending.Size >= Threshold())
            {
                prints.Add(pending);
                if (prints.Count > MaxTrades)
                    prints.RemoveRange(0, prints.Count - MaxTrades);
            }
            pending = null;
        }

        private void Accept(DateTime feedTime, double price, double size, AggressorFlag flag, bool live)
        {
            if (size <= 0 || double.IsNaN(price))
                return;

            bool buy;
            if (flag == AggressorFlag.Buy)
                buy = true;
            else if (flag == AggressorFlag.Sell)
                buy = false;
            else
                buy = Infer(price, live);

            if (flag == AggressorFlag.Buy || flag == AggressorFlag.Sell)
            {
                lastPrice = price;
                lastSideBuy = buy;
            }

            // store feed time (UTC); the chart offset is applied when drawing
            DateTime t = DateTime.SpecifyKind(feedTime, DateTimeKind.Utc).AddHours(ManualOffsetHours);

            lock (sync)
            {
                // running average trade size (for the adaptive threshold)
                avgSize = avgInit ? avgSize * 0.999 + size * 0.001 : size;
                avgInit = true;

                if (pending != null && pending.Buy == buy &&
                    (t - pendingLast).TotalMilliseconds <= AggregateMs)
                {
                    double tot = pending.Size + size;
                    pending.Price = (pending.Price * pending.Size + price * size) / tot;
                    pending.Size = tot;
                    pendingLast = t;
                }
                else
                {
                    FlushPending();
                    pending = new Print { Time = t, Price = price, Size = size, Buy = buy };
                    pendingLast = t;
                }
            }
        }

        // ---------------------------------------------------------------- drawing

        private static string Format(double size)
        {
            if (size >= 1000)
                return (size / 1000.0).ToString("0.#") + "K";
            return Math.Round(size, 0, MidpointRounding.AwayFromZero).ToString("0");
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);

            if (CurrentChart == null)
                return;

            if (!tzReady && Count > 1)
            {
                try
                {
                    TimeSpan d = Time(0) - HistoricalData[0, SeekOriginHistory.End].TimeLeft;
                    tzOffset = TimeSpan.FromMinutes(Math.Round(d.TotalMinutes / 15.0) * 15.0);
                    tzReady = true;
                }
                catch { }
            }

            Graphics g = args.Graphics;
            var conv = CurrentChart.MainWindow.CoordinatesConverter;

            // draw strictly inside the main price window
            Rectangle clip = args.Rectangle;
            try
            {
                Rectangle win = CurrentChart.MainWindow.ClientRectangle;
                if (win.Width > 0 && win.Height > 0)
                    clip = win;
            }
            catch { }
            if (clip.Width <= 0 || clip.Height <= 0)
                return;

            Region oldClip = g.Clip;
            SmoothingMode oldSmooth = g.SmoothingMode;
            g.SetClip(clip);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Print[] snap;
            double thr;
            lock (sync)
            {
                thr = Threshold();
                var list = new List<Print>(prints);
                if (pending != null && pending.Size >= thr)
                    list.Add(pending);
                snap = list.ToArray();
            }

            DateTime tMin = DateTime.MinValue, tMax = DateTime.MaxValue;
            try
            {
                tMin = conv.GetTime(clip.Left - 80);
                tMax = conv.GetTime(clip.Right + 80);
            }
            catch { }

            int drawn = 0;
            using (var font = new Font("Arial", 9f, FontStyle.Bold))
            using (var smallFont = new Font("Arial", 7.5f, FontStyle.Bold))
            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var textBrush = new SolidBrush(Color.White))
            {
                foreach (var p in snap)
                {
                    DateTime pt = p.Time + tzOffset;
                    if (pt < tMin || pt > tMax)
                        continue;

                    float x = (float)conv.GetChartX(pt);
                    float y = (float)conv.GetChartY(p.Price);
                    if (float.IsNaN(x) || float.IsNaN(y))
                        continue;

                    double ratio = Math.Max(1.0, p.Size / Math.Max(1.0, thr));
                    double tt = Math.Log(ratio) / Math.Log(Math.Max(1.5, MaxAtMult));
                    tt = Math.Max(0.0, Math.Min(1.0, tt));
                    float r = (float)(MinRadius + (MaxRadius - MinRadius) * tt);

                    if (x < clip.Left - r || x > clip.Right + r || y < clip.Top - r || y > clip.Bottom + r)
                        continue;

                    drawn++;
                    Color baseColor = p.Buy ? BuyColor : SellColor;
                    using (var fill = new SolidBrush(Color.FromArgb(Opacity, baseColor)))
                        g.FillEllipse(fill, x - r, y - r, r * 2, r * 2);
                    using (var pen = new Pen(Color.FromArgb(255, baseColor), BorderWidth))
                        g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);

                    if (ShowLabels && r >= 4)
                    {
                        Font f = r >= 14 ? font : smallFont;
                        g.DrawString(Format(p.Size), f, textBrush, new RectangleF(x - r - 4, y - r, r * 2 + 8, r * 2), fmt);
                    }
                }

                if (ShowDebug)
                {
                    string diag1 = "Big Trades v7 | hist ticks: " + histCount + " | live ticks: " + liveCount +
                                  " | max print: " + Format(maxPrint) + " | threshold: " + Format(thr) +
                                  " | shown: " + snap.Length + " | drawn: " + drawn;
                    string diag2 = histInfo;
                    string diag3 = (snap.Length > 0 ? "prints " + snap[0].Time.ToString("dd.MM HH:mm") + " - " + snap[snap.Length - 1].Time.ToString("dd.MM HH:mm") +
                                      " @" + snap[0].Price.ToString("0.##") + ".." + snap[snap.Length - 1].Price.ToString("0.##") : "no prints") +
                                  " | view " + tMin.ToString("dd.MM HH:mm") + " - " + tMax.ToString("dd.MM HH:mm");
                    string diag4 = status;
                    g.DrawString(diag1, font, Brushes.Orange, clip.Left + 8, clip.Top + 40);
                    g.DrawString(diag2, font, Brushes.Orange, clip.Left + 8, clip.Top + 52);
                    g.DrawString(diag3, font, Brushes.Orange, clip.Left + 8, clip.Top + 64);
                    g.DrawString(diag4, font, Brushes.Orange, clip.Left + 8, clip.Top + 76);
                }
                else if (!string.IsNullOrEmpty(status))
                    g.DrawString("Big Trades: " + status, font, Brushes.Orange, clip.Left + 8, clip.Top + 40);
            }

            g.SmoothingMode = oldSmooth;
            g.Clip = oldClip;
        }
    }
}
