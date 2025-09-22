// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace VolumeIndicators
{
    public sealed class IndicatorTimeSessions : Indicator
    {
        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTimeSessions.cs";

        private Period currentPeriod;

        private readonly Session[] sessions;

        public IndicatorTimeSessions()
            : base()
        {
            this.Name = "TimeSessions";
            this.SeparateWindow = false;
            this.OnBackGround = true;

            this.sessions = new Session[]
            {
                new Session(Color.Green,"First Session",1),
                new Session(Color.Red,"Second Session",2),
                new Session(Color.GreenYellow, "Third Session",3),
                new Session(Color.Blue,"Fourth Session", 4),
                new Session(Color.Cyan,"Fifth Session",5)
            };
        }

        protected override void OnInit()
        {
            this.currentPeriod = this.HistoricalData.Aggregation.GetPeriod;
            base.OnInit();
            if(this.HistoricalData.Aggregation is HistoryAggregationTime haTime)
                this.currentPeriod = haTime.Period;
            else
                this.currentPeriod = Period.TICK1;

        }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            base.OnPaintChart(args);

            if (this.CurrentChart == null)
                return;

            if (this.currentPeriod.Duration.Days >= 1)
                return;

            var graphics = args.Graphics;
            var mainWindow = this.CurrentChart.MainWindow;
            RectangleF prevClipRectangle = graphics.ClipBounds;
            graphics.SetClip(args.Rectangle);
            try
            {
                var leftBorderTime = this.Time(this.Count - 1);
                var rightBorderTime = this.Time(0);

                var bordersSpan = rightBorderTime - leftBorderTime;
                int daysSpan = (int)bordersSpan.TotalDays;

                int leftCoordinate;
                int rightCoordinate;

                DateTime startTime;
                DateTime endTime;


                int panelTop = mainWindow.ClientRectangle.Top;
                int panelBottom = mainWindow.ClientRectangle.Bottom;
                int panelHeight = mainWindow.ClientRectangle.Height;


                DateTime screenLeftTime = mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Left);
                DateTime screenRightTime = mainWindow.CoordinatesConverter.GetTime((int)mainWindow.ClientRectangle.Right);
                if (screenLeftTime < leftBorderTime)
                    screenLeftTime = leftBorderTime;
                if (screenRightTime > rightBorderTime)
                    screenRightTime = rightBorderTime;

                for (int i = 0; i < this.sessions.Length; i++)
                {
                    var s = this.sessions[i];
                    if (!s.SessionVisibility)
                        continue;

                    var leftTime = s.SessionFirstTime;
                    var rightTime = s.SessionSecondTime;


                    startTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, leftTime.Hour, leftTime.Minute, leftTime.Second);
                    endTime   = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, rightTime.Hour, rightTime.Minute, rightTime.Second);

                    if (leftTime.Hour > rightTime.Hour || (leftTime.Hour == rightTime.Hour && leftTime.Minute > rightTime.Minute))
                        endTime = endTime.AddDays(1);

                    for (int j = 0; j <= daysSpan + 1; j++)
                    {
                        if (startTime < screenRightTime && endTime > screenLeftTime)
                        {
                            if (startTime < screenLeftTime)
                                leftCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(screenLeftTime);
                            else
                                leftCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(startTime);

                            if (endTime > screenRightTime)
                                rightCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(screenRightTime);
                            else
                                rightCoordinate = (int)mainWindow.CoordinatesConverter.GetChartX(endTime);
                            int topY = 0;
                            int height = panelHeight;

                            if (s.DrawMode == SessionDrawMode.Simple)
                            {
                                if (rightCoordinate > leftCoordinate)
                                    graphics.FillRectangle(s.sessionBrush, leftCoordinate, topY, rightCoordinate - leftCoordinate, panelHeight);
                                if (s.DrawInnerArea)
                                {
                                    var startBar = this.HistoricalData[(int)mainWindow.CoordinatesConverter.GetBarIndex(startTime), SeekOriginHistory.Begin];
                                    var endBar = this.HistoricalData[(int)mainWindow.CoordinatesConverter.GetBarIndex(endTime), SeekOriginHistory.Begin];
                                    int zoneOpenY = (int)mainWindow.CoordinatesConverter.GetChartY(startBar[PriceType.Open]);
                                    int zoneCloseY = (int)mainWindow.CoordinatesConverter.GetChartY(endBar[PriceType.Close]);
                                    int areaY = zoneOpenY < zoneCloseY ? zoneOpenY : zoneCloseY;
                                    graphics.FillRectangle(s.sessionBrush, leftCoordinate, areaY, rightCoordinate - leftCoordinate, Math.Abs(zoneOpenY-zoneCloseY));

                                }
                            }
                            else
                            {
                                double hi = double.MinValue;
                                double lo = double.MaxValue;
                                bool hasBars = false;

                                for (int k = 0; k < this.Count; k++)
                                {
                                    var t = this.Time(k);

                                    if (t >= endTime)
                                        continue;

                                    if (t < startTime)
                                        break;

                                    var bar = this.HistoricalData[k, SeekOriginHistory.End];
                                    double bh = bar[PriceType.High];
                                    double bl = bar[PriceType.Low];

                                    if (bh > hi) hi = bh;
                                    if (bl < lo) lo = bl;
                                    hasBars = true;
                                }

                                if (hasBars && hi > lo && rightCoordinate > leftCoordinate)
                                {
                                    int yHigh = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(hi);
                                    int yLow = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(lo);

                                    topY = Math.Min(yHigh, yLow);
                                    height = Math.Abs(yHigh - yLow);

                                    if (height > 0)
                                        graphics.FillRectangle(s.sessionBrush, leftCoordinate, topY, rightCoordinate - leftCoordinate, height);

                                }
                            }
                            if (s.DrawBorder)
                            {
                                var tmpPen = new Pen(s.BorderLineOptions.Color, s.BorderLineOptions.Width);
                                tmpPen.DashStyle = (DashStyle)s.BorderLineOptions.LineStyle;
                                graphics.DrawRectangle(tmpPen, leftCoordinate, topY, rightCoordinate - leftCoordinate, height);
                            }
                            if (s.ShowLabel)
                            {
                                var labelRect = new Rectangle(
                                    leftCoordinate,
                                    topY,
                                    Math.Max(0, rightCoordinate - leftCoordinate),
                                    Math.Max(0, height)
                                );

                                this.DrawSessionLabel(graphics, labelRect, s, startTime, endTime);
                            }
                        }

                        startTime = startTime.AddDays(1);
                        endTime = endTime.AddDays(1);
                    }
                }
            }
            finally
            {
                graphics.SetClip(prevClipRectangle);
            }
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;
                for (int i = 0; i < this.sessions.Length; i++)
                    settings.Add(new SettingItemGroup(this.sessions[i].SessionName, this.sessions[i].Settings));
                return settings;
            }
            set
            {
                base.Settings = value;
                for (int i = 0; i < this.sessions.Length; i++)
                    this.sessions[i].Settings = value;
            }
        }
        private void DrawSessionLabel(Graphics g, Rectangle rect, Session s, DateTime startTime, DateTime endTime)
        {
            if (!s.ShowLabel || rect.Width <= 0 || rect.Height <= 0)
                return;

            string text = s.LabelText ?? string.Empty;

            if (s.ShowDelta)
            {
                var mainWindow = this.CurrentChart.MainWindow;
                int iStart = (int)mainWindow.CoordinatesConverter.GetBarIndex(startTime);
                int iEnd = (int)mainWindow.CoordinatesConverter.GetBarIndex(endTime);

                iStart = Math.Max(0, Math.Min(iStart, this.HistoricalData.Count - 1));
                iEnd   = Math.Max(0, Math.Min(iEnd, this.HistoricalData.Count - 1));

                var startBar = this.HistoricalData[iStart, SeekOriginHistory.Begin];
                var endBar = this.HistoricalData[iEnd, SeekOriginHistory.Begin];

                double open = startBar?[PriceType.Open]  ?? double.NaN;
                double close = endBar?[PriceType.Close]   ?? double.NaN;

                if (!double.IsNaN(open) && !double.IsNaN(close))
                {
                    double delta = close - open;

                    int prec = this.Symbol != null
                        ? Math.Max(0, (int)Math.Round(-Math.Log10(this.Symbol.TickSize)))
                        : 2;
                    string deltaStr = delta.ToString("F" + prec);

                    text = string.IsNullOrEmpty(text) ? $"Δ {deltaStr}" : $"{text}  Δ {deltaStr}";
                }
            }

            if (string.IsNullOrEmpty(text))
                return;

            var font = s.LabelFont; 
            var brush = s.LabelBrush;  
            var size = g.MeasureString(text, font);


            bool inside = s.DrawMode == SessionDrawMode.Simple || s.LabelPlacement == LabelPlacement.Inside;

            float x = rect.Left, y = rect.Top;
            int pad = 4;
            if (inside)
            {
                x = s.LabelHAlign switch
                {
                    NativeAlignment.Left => rect.Left  + pad,
                    NativeAlignment.Center => rect.Left  + (rect.Width - size.Width) / 2f,
                    NativeAlignment.Right => rect.Right - size.Width - pad,
                    _ => rect.Left + (rect.Width - size.Width) / 2f
                };
            }
            else 
            {
                x = s.LabelHAlign switch
                {
                    NativeAlignment.Left => rect.Left  - size.Width - pad,
                    NativeAlignment.Center => rect.Left  + (rect.Width - size.Width) / 2f, 
                    NativeAlignment.Right => rect.Right + pad,
                    _ => rect.Left + (rect.Width - size.Width) / 2f
                };
            }


            if (inside)
            {
                y = s.LabelVAlign switch
                {
                    LabelVAlign.Top => rect.Top    + pad,
                    LabelVAlign.Middle => rect.Top    + (rect.Height - size.Height) / 2f,
                    LabelVAlign.Bottom => rect.Bottom - size.Height - pad,
                    _ => rect.Top + (rect.Height - size.Height) / 2f
                };
            }
            else
            {
                y = s.LabelVAlign switch
                {
                    LabelVAlign.Top => rect.Top    - size.Height - pad,
                    LabelVAlign.Middle => rect.Top    + (rect.Height - size.Height) / 2f,
                    LabelVAlign.Bottom => rect.Bottom + pad,
                    _ => rect.Top + (rect.Height - size.Height) / 2f
                };
            }

            g.DrawString(text, font, brush, new PointF(x, y));
        }
    }

    internal sealed class Session : ICustomizable
    {
        public DateTime SessionFirstTime { get; set; }
        public DateTime SessionSecondTime { get; set; }
        public Color SessionColor { get; set; }
        public bool SessionVisibility { get; set; }
        public string SessionName { get; set; }
        private int sessionSortIndex { get; set; }

        public SolidBrush sessionBrush { get; set; }

        public SessionDrawMode DrawMode { get; set; } = SessionDrawMode.Simple;
        public bool DrawBorder { get; set; } = false;

        private LineOptions _borderLineOptions;
        public LineOptions BorderLineOptions
        {
            get => this._borderLineOptions;
            set
            {
                this._borderLineOptions = value;
                this.borderPen.Color = value.Color;
                this.borderPen.Width = value.Width;
                this.borderPen.DashStyle = (DashStyle)value.LineStyle;
            }
        }
        private readonly Pen borderPen;

        public bool DrawInnerArea { get; set; }

        public bool ShowLabel { get; set; }        
        public string LabelText { get; set; }          
        public bool ShowDelta { get; set; }         

        public Font LabelFont { get; set; }
        public Color LabelColor { get; set; }
        public SolidBrush LabelBrush { get; set; }

        public LabelPlacement LabelPlacement { get; set; } = LabelPlacement.Inside; 
        public NativeAlignment LabelHAlign { get; set; } = NativeAlignment.Center;
        public LabelVAlign LabelVAlign { get; set; } = LabelVAlign.Middle;

        public Session(Color color, string name = "Session X", int sortingIndex = 20)
        {
            this.SessionName = name;
            this.sessionSortIndex = sortingIndex;
            this.SessionColor = Color.FromArgb(51, color);
            this.SessionFirstTime = new DateTime();
            this.SessionSecondTime = new DateTime();
            this.SessionVisibility = false;

            this.sessionBrush = new SolidBrush(this.SessionColor);

            this._borderLineOptions = new LineOptions();
            this._borderLineOptions.Color = Color.White;
            this._borderLineOptions.Width = 1;
            this._borderLineOptions.LineStyle = LineStyle.Solid;
            this._borderLineOptions.WithCheckBox = false;
            this._borderLineOptions.Enabled = false;

            this.borderPen = new Pen(this._borderLineOptions.Color, this._borderLineOptions.Width)
            {
                DashStyle = (DashStyle)this._borderLineOptions.LineStyle
            };

            this.DrawInnerArea = false;

            this.ShowLabel     = false;
            this.LabelText     = string.Empty;
            this.ShowDelta     = false;
            this.LabelFont     = new Font("Arial", 9f, FontStyle.Regular);
            this.LabelColor    = Color.Yellow;
            this.LabelBrush    = new SolidBrush(this.LabelColor);
            this.LabelPlacement= LabelPlacement.Inside;
            this.LabelHAlign   = NativeAlignment.Center;
            this.LabelVAlign   = LabelVAlign.Top;
        }

        public IList<SettingItem> Settings
        {
            get
            {
                var settings = new List<SettingItem>();
                var separatorGroup1 = new SettingItemSeparatorGroup(this.SessionName, this.sessionSortIndex);

                string visibleRelationName = $"{this.SessionName}SessionVisibility";
                settings.Add(new SettingItemBoolean(visibleRelationName, this.SessionVisibility)
                {
                    Text = "Visible",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                });
                var visibleRelation = new SettingItemRelationVisibility(visibleRelationName, true);

                var simple = new SelectItem("Simple", SessionDrawMode.Simple);
                var box = new SelectItem("Box", SessionDrawMode.Box);
                settings.Add(new SettingItemSelectorLocalized(
                    "SessionDrawMode",
                    new SelectItem("SessionDrawMode", this.DrawMode),
                    new List<SelectItem> { simple, box })
                {
                    Text = "Draw mode",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation,
                    ValueChangingBehavior = SettingItemValueChangingBehavior.WithConfirmation
                });


                settings.Add(new SettingItemDateTime("SessionFirstTime", this.SessionFirstTime)
                {
                    Text = "Start Time",
                    SortIndex = sessionSortIndex,
                    Format = DatePickerFormat.Time,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });

                settings.Add(new SettingItemDateTime("SessionSecondTime", this.SessionSecondTime)
                {
                    Text = "End Time",
                    SortIndex = sessionSortIndex,
                    Format = DatePickerFormat.Time,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });

                
                settings.Add(new SettingItemColor("SessionColor", this.SessionColor)
                {
                    Text = "Color",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });

                string borderVisibleName = $"{this.SessionName}DrawBorder";

                settings.Add(new SettingItemBoolean(borderVisibleName, this.DrawBorder)
                {
                    Text = "Draw border",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });
                var borderRelation = new SettingItemMultipleRelation(visibleRelation, new SettingItemRelationVisibility(borderVisibleName, true));
                

                settings.Add(new SettingItemLineOptions("BorderLineOptions", this._borderLineOptions)
                {
                    Text = "Border line",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = borderRelation,        
                    UseEnabilityToggler = true,       
                    ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points }
                });
                settings.Add(new SettingItemBoolean("DrawInnerArea", this.DrawInnerArea)
                {
                    Text = "Draw the open-close area",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });
                settings.Add(new SettingItemBoolean("ShowLabel", this.ShowLabel)
                {
                    Text = "Show label",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = visibleRelation
                });
                var labelVisibleRelation = new SettingItemRelationVisibility("ShowLabel", true);

                settings.Add(new SettingItemTextArea("LabelText", this.LabelText)
                {
                    Text = "Custom text",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });

                settings.Add(new SettingItemBoolean("ShowDelta", this.ShowDelta)
                {
                    Text = "Append Δ price",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });

                settings.Add(new SettingItemFont("LabelFont", this.LabelFont)
                {
                    Text = "Label font",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });
                settings.Add(new SettingItemColor("LabelColor", this.LabelColor)
                {
                    Text = "Label color",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });

                var allowOutsideRelation = new SettingItemMultipleRelation(
                    labelVisibleRelation,
                    new SettingItemRelationVisibility("SessionDrawMode", SessionDrawMode.Box) 
                );

                settings.Add(new SettingItemSelectorLocalized("LabelPlacement", this.LabelPlacement,
                    new List<SelectItem> {
        new SelectItem("Inside", LabelPlacement.Inside),
        new SelectItem("Outside", LabelPlacement.Outside)
                    })
                {
                    Text = "Placement (Box only)",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = allowOutsideRelation
                });
                settings.Add(new SettingItemAlignment("LabelHAlign", this.LabelHAlign)
                {
                    Text = "Horizontal align",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });

                settings.Add(new SettingItemSelectorLocalized("LabelVAlign", this.LabelVAlign,
                    new List<SelectItem> {
        new SelectItem("Top",    LabelVAlign.Top),
        new SelectItem("Middle", LabelVAlign.Middle),
        new SelectItem("Bottom", LabelVAlign.Bottom),
                    })
                {
                    Text = "Vertical align",
                    SortIndex = sessionSortIndex,
                    SeparatorGroup = separatorGroup1,
                    Relation = labelVisibleRelation
                });
                return settings;
            }
            set
            {
                var settings = new List<SettingItem>();
                string borderVisibleName = $"{this.SessionName}DrawBorder";

                if (value.TryGetValue(this.SessionName, out List<SettingItem> inputSettings))
                    settings = inputSettings;

                if (settings.TryGetValue($"{this.SessionName}SessionVisibility", out bool SessionVisibility))
                    this.SessionVisibility = SessionVisibility;

                if (settings.TryGetValue("SessionDrawMode", out SessionDrawMode drawMode))
                    this.DrawMode = drawMode;

                if (settings.TryGetValue("SessionFirstTime", out DateTime SessionFirstTime))
                    this.SessionFirstTime = SessionFirstTime;

                if (settings.TryGetValue("SessionSecondTime", out DateTime SessionSecondTime))
                    this.SessionSecondTime = SessionSecondTime;

                if (settings.TryGetValue("SessionColor", out Color SessionColor))
                {
                    this.SessionColor = SessionColor;
                    this.sessionBrush.Color = this.SessionColor;
                }

                if (settings.TryGetValue(borderVisibleName, out bool drawBorder))
                    this.DrawBorder = drawBorder;

                if (settings.TryGetValue("BorderLineOptions", out LineOptions borderLineOptions))
                    this.BorderLineOptions = borderLineOptions;

                if (settings.TryGetValue("DrawInnerArea", out bool DrawInnerArea))
                    this.DrawInnerArea = DrawInnerArea;

                if (settings.TryGetValue("ShowLabel", out bool showLabel))
                    this.ShowLabel = showLabel;

                if (settings.TryGetValue("LabelText", out string labelText))
                    this.LabelText = labelText;

                if (settings.TryGetValue("ShowDelta", out bool showDelta))
                    this.ShowDelta = showDelta;

                if (settings.TryGetValue("LabelFont", out Font labelFont))
                    this.LabelFont = labelFont;

                if (settings.TryGetValue("LabelColor", out Color labelColor))
                {
                    this.LabelColor = labelColor;
                    this.LabelBrush.Color = labelColor;
                }

                if (settings.TryGetValue("LabelPlacement", out LabelPlacement labelPlacement))
                    this.LabelPlacement = labelPlacement;

                if (settings.TryGetValue("LabelHAlign", out NativeAlignment hAlign))
                    this.LabelHAlign = hAlign;

                if (settings.TryGetValue("LabelVAlign", out LabelVAlign vAlign))
                    this.LabelVAlign = vAlign;
            }
        }
    }
    internal enum SessionDrawMode
    {
        Simple = 0,
        Box = 1
    }
    internal enum LabelPlacement { Inside = 0, Outside = 1 }  
    internal enum LabelVAlign { Top = 0, Middle = 1, Bottom = 2 }
}
