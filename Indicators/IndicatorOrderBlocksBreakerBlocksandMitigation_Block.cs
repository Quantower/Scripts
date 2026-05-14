// Copyright QUANTOWER LLC. © 2017-2025. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators
{
    public class IndicatorOrderBlocksBreakerBlocksandMitigation_Block : Indicator
    {
        private int Period = 10;
        private bool drawBraker = true;
        private bool drawBorder = true;
        private int showBull = 3;
        private int showBear = 3;
        private bool useCandleBody = false;

        public LineOptions bullBorderOptions { get; set; }
        public LineOptions bearBorderOptions { get; set; }
        public LineOptions bullBreakerBorderOptions { get; set; }
        public LineOptions bearBreakerBorderOptions { get; set; }

        private Brush bullBrush = new SolidBrush(Color.FromArgb(60, Color.Blue));
        private Brush bearBrush = new SolidBrush(Color.FromArgb(60, Color.Orange));
        private Brush bullBreakerBrush = new SolidBrush(Color.FromArgb(40, Color.Red));
        private Brush bearBreakerBrush = new SolidBrush(Color.FromArgb(40, Color.Green));
        private Pen bullPen;
        private Pen bearPen;
        private Pen bullBreakerPen;
        private Pen bearBreakerPen;

        public Color BullColor
        {
            get => ((SolidBrush)this.bullBrush).Color;
            set => ((SolidBrush)this.bullBrush).Color = value;
        }
        public Color BearColor
        {
            get => ((SolidBrush)this.bearBrush).Color;
            set => ((SolidBrush)this.bearBrush).Color = value;
        }
        public Color BullBreakerColor
        {
            get => ((SolidBrush)this.bullBreakerBrush).Color;
            set => ((SolidBrush)this.bullBreakerBrush).Color = value;
        }
        public Color BearBreakerColor
        {
            get => ((SolidBrush)this.bearBreakerBrush).Color;
            set => ((SolidBrush)this.bearBreakerBrush).Color = value;
        }

        private SwingState swingTop = new SwingState();   
        private SwingState swingBtm = new SwingState();   
        private int os = -1; 

        private readonly List<OrderBlock> bullishOb = new List<OrderBlock>(); 
        private readonly List<OrderBlock> bearishOb = new List<OrderBlock>();

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorOrderBlocksBreakerBlocksandMitigation_Block.cs";
        public IndicatorOrderBlocksBreakerBlocksandMitigation_Block()
        {
            this.Name = "Order Blocks / Breaker Blocks and Mitigation Block";
            this.SeparateWindow = false;

            this.bullBorderOptions = new LineOptions { Color = Color.Blue, Width = 1, WithCheckBox = false };
            this.bearBorderOptions = new LineOptions { Color = Color.Orange, Width = 1, WithCheckBox = false };
            this.bullBreakerBorderOptions = new LineOptions { Color = Color.Red, Width = 1, WithCheckBox = false, LineStyle = LineStyle.Dash };
            this.bearBreakerBorderOptions = new LineOptions { Color = Color.Green, Width = 1, WithCheckBox = false, LineStyle = LineStyle.Dash };

            this.bullPen = new Pen(this.bullBorderOptions.Color, this.bullBorderOptions.Width) { DashStyle = (DashStyle)this.bullBorderOptions.LineStyle };
            this.bearPen = new Pen(this.bearBorderOptions.Color, this.bearBorderOptions.Width) { DashStyle = (DashStyle)this.bearBorderOptions.LineStyle };
            this.bullBreakerPen = new Pen(this.bullBreakerBorderOptions.Color, this.bullBreakerBorderOptions.Width) { DashStyle = (DashStyle)this.bullBreakerBorderOptions.LineStyle };
            this.bearBreakerPen = new Pen(this.bearBreakerBorderOptions.Color, this.bearBreakerBorderOptions.Width) { DashStyle = (DashStyle)this.bearBreakerBorderOptions.LineStyle };
        }

        protected override void OnInit() { }

        protected override void OnUpdate(UpdateArgs args)
        {
            if (this.Count < this.Period + 2)
                return;

            int last = this.Count - 1;
            var bar = (HistoryItemBar)this.HistoricalData[last, SeekOriginHistory.Begin];      
            DetectSwings(last);
            if (this.swingTop.IsValid && !this.swingTop.Crossed && bar.Close > this.swingTop.Y)
            {
                this.swingTop.Crossed = true;

                if (this.swingTop.X < last) 
                {

                    int start = Math.Max(this.swingTop.X + 1, 0);
                    int end = last - 1;
                    int loc = end >= start ? end : this.swingTop.X;
                    double minima = double.PositiveInfinity;
                    double maxima = double.NegativeInfinity;
                    for (int i = end; i >= start; i--)
                    {
                        double mi = GetMin(i);
                        if (mi <= minima) 
                        {
                            minima = mi;
                            maxima = GetMax(i);
                            loc    = i;
                        }
                    }
                    var ob = new OrderBlock
                    {
                        IsBullish = true,
                        Top       = maxima,
                        Bottom    = minima,
                        LocIndex  = loc,
                        Breaker   = false,
                        BreakIndex= -1
                    };
                    this.bullishOb.Insert(0, ob);
                }
            }

            
            if (this.swingBtm.IsValid && !this.swingBtm.Crossed && bar.Close < this.swingBtm.Y)
            {
                this.swingBtm.Crossed = true;

                if (this.swingBtm.X < last)
                {
                    int start = Math.Max(this.swingBtm.X + 1, 0);
                    int end = last - 1;
                    int loc = end >= start ? end : this.swingBtm.X;
                    double maxima = double.NegativeInfinity;
                    double minima = double.PositiveInfinity;
                    for (int i = end; i >= start; i--)
                    {
                        double mx = GetMax(i);
                        if (mx >= maxima)
                        {
                            maxima = mx;
                            minima = GetMin(i);
                            loc    = i;
                        }
                    }
                    var ob = new OrderBlock
                    {
                        IsBullish = false,
                        Top       = maxima,
                        Bottom    = minima,
                        LocIndex  = loc,
                        Breaker   = false,
                        BreakIndex= -1
                    };
                    this.bearishOb.Insert(0, ob);
                }
            }

            
            
            double bodyMin = Math.Min(bar.Open, bar.Close);
            double bodyMax = Math.Max(bar.Open, bar.Close);

            
            for (int i = this.bullishOb.Count - 1; i >= 0; i--)
            {
                var ob = this.bullishOb[i];
                if (!ob.Breaker)
                {
                    if (bodyMin < ob.Bottom)
                    {
                        ob.Breaker = true;
                        ob.BreakIndex = last;
                    }
                }
                else
                {
                    
                    if (bar.Close > ob.Top)
                        this.bullishOb.RemoveAt(i);
                }
            }

            
            for (int i = this.bearishOb.Count - 1; i >= 0; i--)
            {
                var ob = this.bearishOb[i];
                if (!ob.Breaker)
                {
                    if (bodyMax > ob.Top)
                    {
                        ob.Breaker = true;
                        ob.BreakIndex = last;
                    }
                }
                else
                {
                    
                    if (bar.Close < ob.Bottom)
                        this.bearishOb.RemoveAt(i);
                }
            }
        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);
            if (this.CurrentChart == null)
                return;

            var gr = args.Graphics;
            RectangleF prev = gr.ClipBounds;
            gr.SetClip(args.Rectangle);

            try
            {
                int bullCount = Math.Min(this.showBull, this.bullishOb.Count);
                for (int i = 0; i < bullCount; i++)
                    DrawOB(gr, args.WindowIndex, this.bullishOb[i], this.bullBrush, this.bullPen, this.bullBreakerBrush, this.bullBreakerPen);

                int bearCount = Math.Min(this.showBear, this.bearishOb.Count);
                for (int i = 0; i < bearCount; i++)
                    DrawOB(gr, args.WindowIndex, this.bearishOb[i], this.bearBrush, this.bearPen, this.bearBreakerBrush, this.bearBreakerPen);
            }
            finally
            {
                gr.SetClip(prev);
            }
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                settings.Add(new SettingItemInteger("Period", this.Period)
                {
                    Text = "Swing Lookback (Period)",
                    SortIndex = 1,
                    Dimension = "Bars",
                    Minimum = 3
                });

                settings.Add(new SettingItemBoolean("UseCandleBody", this.useCandleBody)
                {
                    Text = "Use Candle Body",
                    SortIndex = 2
                });

                settings.Add(new SettingItemBoolean("DrawBreaker", this.drawBraker)
                {
                    Text = "Draw Breaker Zones",
                    SortIndex = 3
                });
                var drawBreakerRelation = new SettingItemRelationVisibility("DrawBreaker", true);

                settings.Add(new SettingItemBoolean("DrawBorders", this.drawBorder)
                {
                    Text = "Draw Borders",
                    SortIndex = 4
                });
                var drawBorderRelation = new SettingItemRelationVisibility("DrawBorders", true);

                settings.Add(new SettingItemInteger("ShowLastBullishOB", this.showBull)
                {
                    Text = "Show Last Bullish OB",
                    SortIndex = 5,
                    Minimum = 0
                });
                settings.Add(new SettingItemInteger("ShowLastBearishOB", this.showBear)
                {
                    Text = "Show Last Bearish OB",
                    SortIndex = 6,
                    Minimum = 0
                });

                settings.Add(new SettingItemColor("BullColor", this.BullColor) { Text = "Bull Color", SortIndex = 10 });
                settings.Add(new SettingItemColor("BearColor", this.BearColor) { Text = "Bear Color", SortIndex = 11 });
                settings.Add(new SettingItemColor("BullBreakerColor", this.BullBreakerColor) { Text = "Bull Breaker Color", SortIndex = 12, Relation = drawBreakerRelation });
                settings.Add(new SettingItemColor("BearBreakerColor", this.BearBreakerColor) { Text = "Bear Breaker Color", SortIndex = 13, Relation = drawBreakerRelation });

                settings.Add(new SettingItemLineOptions("BullBorder", this.bullBorderOptions)
                {
                    Text = "Bull Border Style",
                    SortIndex = 20,
                    ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawBorderRelation
                });
                settings.Add(new SettingItemLineOptions("BearBorder", this.bearBorderOptions)
                {
                    Text = "Bear Border Style",
                    SortIndex = 21,
                    ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = drawBorderRelation
                });
                var breakBorderRelations = new SettingItemMultipleRelation([drawBorderRelation, drawBreakerRelation]);
                settings.Add(new SettingItemLineOptions("BullBreakerBorder", this.bullBreakerBorderOptions)
                {
                    Text = "Bull Breaker Border Style",
                    SortIndex = 22,
                    ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = breakBorderRelations
                });
                settings.Add(new SettingItemLineOptions("BearBreakerBorder", this.bearBreakerBorderOptions)
                {
                    Text = "Bear Breaker Border Style",
                    SortIndex = 23,
                    ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                    UseEnabilityToggler = true,
                    Relation = breakBorderRelations
                });

                return settings;
            }
            set
            {
                bool updateRequired = false;
                base.Settings = value;

                if (value.TryGetValue("Period", out int period)) { this.Period = Math.Max(3, period); updateRequired = true; }
                if (value.TryGetValue("UseCandleBody", out bool body)) { this.useCandleBody = body; updateRequired = true; }
                if (value.TryGetValue("DrawBreaker", out bool drawBraker)) this.drawBraker = drawBraker;
                if (value.TryGetValue("DrawBorders", out bool drawBorder)) this.drawBorder = drawBorder;
                if (value.TryGetValue("ShowLastBullishOB", out int showBull)) this.showBull = Math.Max(0, showBull);
                if (value.TryGetValue("ShowLastBearishOB", out int showBear)) this.showBear = Math.Max(0, showBear);

                if (value.TryGetValue("BullBorder", out LineOptions bullBorder))
                {
                    this.bullBorderOptions = bullBorder;
                    if (this.bullPen != null)
                    {
                        this.bullPen.Width = bullBorder.Width;
                        this.bullPen.Color = bullBorder.Color;
                        this.bullPen.DashStyle = (DashStyle)bullBorder.LineStyle;
                    }
                }
                if (value.TryGetValue("BearBorder", out LineOptions bearBorder))
                {
                    this.bearBorderOptions = bearBorder;
                    if (this.bearPen != null)
                    {
                        this.bearPen.Width = bearBorder.Width;
                        this.bearPen.Color = bearBorder.Color;
                        this.bearPen.DashStyle = (DashStyle)bearBorder.LineStyle;
                    }
                }
                if (value.TryGetValue("BullBreakerBorder", out LineOptions bullBrBorder))
                {
                    this.bullBreakerBorderOptions = bullBrBorder;
                    if (this.bullBreakerPen != null)
                    {
                        this.bullBreakerPen.Width = bullBrBorder.Width;
                        this.bullBreakerPen.Color = bullBrBorder.Color;
                        this.bullBreakerPen.DashStyle = (DashStyle)bullBrBorder.LineStyle;
                    }
                }
                if (value.TryGetValue("BearBreakerBorder", out LineOptions bearBrBorder))
                {
                    this.bearBreakerBorderOptions = bearBrBorder;
                    if (this.bearBreakerPen != null)
                    {
                        this.bearBreakerPen.Width = bearBrBorder.Width;
                        this.bearBreakerPen.Color = bearBrBorder.Color;
                        this.bearBreakerPen.DashStyle = (DashStyle)bearBrBorder.LineStyle;
                    }
                }
                if (value.TryGetValue("BullColor", out Color bullColor)) this.BullColor = bullColor;
                if (value.TryGetValue("BearColor", out Color bearColor)) this.BearColor = bearColor;
                if (value.TryGetValue("BullBreakerColor", out Color bullBrColor)) this.BullBreakerColor = bullBrColor;
                if (value.TryGetValue("BearBreakerColor", out Color bearBrColor)) this.BearBreakerColor = bearBrColor;

                if (updateRequired)
                {
                    this.swingTop = new SwingState();
                    this.swingBtm = new SwingState();
                    this.os = -1;
                    this.bullishOb.Clear();
                    this.bearishOb.Clear();
                    this.OnSettingsUpdated();
                }
            }
        }

        protected override void OnClear()
        {
            base.OnClear();
            this.bullishOb.Clear();
            this.bearishOb.Clear();
            this.swingTop = new SwingState();
            this.swingBtm = new SwingState();
            this.os = -1;
        }

        private void DrawOB(Graphics gr, int windowIndex, OrderBlock ob, Brush baseBrush, Pen basePen, Brush brkBrush, Pen brkPen)
        {
            var wnd = this.CurrentChart.Windows[windowIndex];
            if (ob.LocIndex < 0 || ob.LocIndex >= this.Count)
                return;

            float xStart = (float)wnd.CoordinatesConverter.GetChartX(this.HistoricalData[ob.LocIndex, SeekOriginHistory.Begin].TimeLeft)
                           + this.CurrentChart.BarsWidth / 2f;
            float xEnd = (float)wnd.CoordinatesConverter.GetChartX(this.HistoricalData[0].TimeLeft)
                         + this.CurrentChart.BarsWidth;

            float yTop = (float)wnd.CoordinatesConverter.GetChartY(ob.Top);
            float yBot = (float)wnd.CoordinatesConverter.GetChartY(ob.Bottom);
            float rectY = Math.Min(yTop, yBot);
            float rectH = Math.Abs(yTop - yBot);

            if (!ob.Breaker || ob.BreakIndex < 0)
            {

                gr.FillRectangle(baseBrush, xStart, rectY, xEnd - xStart, rectH);
                if (this.drawBorder)
                    gr.DrawRectangle(basePen, xStart, rectY, xEnd - xStart, rectH);
            }
            else
            {
                if (!this.drawBraker)
                    return;

                float xBreak = (float)wnd.CoordinatesConverter.GetChartX(this.HistoricalData[ob.BreakIndex, SeekOriginHistory.Begin].TimeLeft)
                               + this.CurrentChart.BarsWidth / 2f;

                if (xBreak > xStart)
                {
                    float wLeft = xBreak - xStart;
                    gr.FillRectangle(baseBrush, xStart, rectY, wLeft, rectH);
                    if (this.drawBorder)
                        gr.DrawRectangle(basePen, xStart, rectY, wLeft, rectH);
                }

                if (xEnd > xBreak)
                {
                    float wRight = xEnd - xBreak;
                    gr.FillRectangle(brkBrush, xBreak, rectY, wRight, rectH);
                    if (this.drawBorder)
                        gr.DrawRectangle(brkPen, xBreak, rectY, wRight, rectH);
                }
            }
        }

        private void DetectSwings(int last)
        {

            int cand = last - this.Period;
            if (cand < 1) return;

            double upper = double.MinValue;
            double lower = double.MaxValue;
            int from = Math.Max(last - this.Period + 1, 0);
            for (int i = from; i <= last; i++)
            {
                var b = (HistoryItemBar)this.HistoricalData[i, SeekOriginHistory.Begin];
                if (b.High > upper) upper = b.High;
                if (b.Low < lower) lower = b.Low;
            }

            var cb = (HistoryItemBar)this.HistoricalData[cand, SeekOriginHistory.Begin];
            int newOs = this.os;
            if (cb.High > upper) newOs = 0;           
            else if (cb.Low < lower) newOs = 1;       

            
            if (newOs == 0 && this.os != 0)
            {
                this.swingTop = new SwingState { X = cand, Y = cb.High, Crossed = false };
            }
            else if (newOs == 1 && this.os != 1)
            {
                this.swingBtm = new SwingState { X = cand, Y = cb.Low, Crossed = false };
            }

            this.os = newOs;
        }

        private double GetMin(int index)
        {
            var b = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];
            return this.useCandleBody ? Math.Min(b.Open, b.Close) : b.Low;
        }
        private double GetMax(int index)
        {
            var b = (HistoryItemBar)this.HistoricalData[index, SeekOriginHistory.Begin];
            return this.useCandleBody ? Math.Max(b.Open, b.Close) : b.High;
        }

        
        private struct SwingState
        {
            public int X;       
            public double Y;    
            public bool Crossed;
            public bool IsValid => X >= 0 && !double.IsNaN(Y) && !double.IsInfinity(Y);
        }

        private class OrderBlock
        {
            public bool IsBullish;
            public double Top;
            public double Bottom;
            public int LocIndex;
            public bool Breaker;
            public int BreakIndex;
        }
    }
}
