// Copyright QUANTOWER LLC. © 2017-2025. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace TrendIndicators;

public class IndicatorTDSequential : Indicator
{
    #region Consts

    private const int MIN_PERIOD = 6;

    private const int SETUP_MAX = 9;
    private const int COUNTDOWN_MAX = 13;

    private const string GROUP_CALC = "Calculation";
    private const string GROUP_COLORS = "Colors";
    private const string GROUP_NUMBERS = "Numbers display";

    private const string CALCULATION_SI = "Calculation";
    private const string SHOW_NUMBERS_SI = "Show numbers";
    private const string FROM_VALUE_SI = "Value";

    private const string UP_COLOR_SI = "Up color";
    private const string DOWN_COLOR_SI = "Down color";
    private const string UP_CD_COLOR_SI = "Up countdown color";
    private const string DOWN_CD_COLOR_SI = "Down countdown color";

    private const int ZERO = 0;
    private const int ONE = 1;
    private const int THREE = 3;
    private const int FIVE = 5;
    private const int TEN = 10;

    private const int SERIES_SETUP_UP = 0;
    private const int SERIES_SETUP_DOWN = 1;
    private const int SERIES_CD_BUY = 2;   
    private const int SERIES_CD_SELL = 3;  

    private const int LABEL_GAP = 2;

    #endregion Consts

    #region Parameters

    private byte upValue;
    private byte downValue;

    private byte buyCountdown;
    private byte sellCountdown;

    private bool buyCountdownActive;
    private bool sellCountdownActive;

    private readonly Font defaultFont;
    private readonly Font extraFont;

    public enum CalculationMode { OnlySetup9, SetupPlusCountdown13 }
    public enum TDSVisualMode { All, None, FromValue }

    public CalculationMode Calculation = CalculationMode.OnlySetup9;

    public Color DefaultUpColor
    {
        get => this.defaultUpColor;
        set
        {
            this.defaultUpColor = value;
            this.defaultUpPen = new Pen(value);
        }
    }
    private Color defaultUpColor;
    private Pen defaultUpPen;

    public Color DefaultDownColor
    {
        get => this.defaultDownColor;
        set
        {
            this.defaultDownColor = value;
            this.defaultDownPen = new Pen(value);
        }
    }
    private Color defaultDownColor;
    private Pen defaultDownPen;

    public Color CountdownUpColor
    {
        get => this.countdownUpColor;
        set
        {
            this.countdownUpColor = value;
            this.countdownUpPen = new Pen(value);
        }
    }
    private Color countdownUpColor;
    private Pen countdownUpPen;

    public Color CountdownDownColor
    {
        get => this.countdownDownColor;
        set
        {
            this.countdownDownColor = value;
            this.countdownDownPen = new Pen(value);
        }
    }
    private Color countdownDownColor;
    private Pen countdownDownPen;

    public TDSVisualMode VisualMode = TDSVisualMode.All;

    public int FromValue = 8;

    private static readonly StringFormat centerCenterSF = new()
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Center
    };
    private bool showLines = true;

    private LineOptions upLineOptions = new LineOptions();
    private LineOptions downLineOptions = new LineOptions();

    private Pen lineUpPen;
    private Pen lineDownPen;
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorTDSequential.cs";

    #endregion Parameters

    public IndicatorTDSequential()
    {
        this.Name = "TD Sequential";

        this.defaultFont = new Font("Verdana", 10, FontStyle.Regular);
        this.extraFont   = new Font("Verdana", 16, FontStyle.Bold);

        this.DefaultUpColor   = Color.FromArgb(55, 219, 186);
        this.DefaultDownColor = Color.FromArgb(235, 96, 47);
        this.CountdownUpColor   = this.DefaultUpColor;
        this.CountdownDownColor = this.DefaultDownColor;

        this.upLineOptions.Color   = this.DefaultUpColor;
        this.upLineOptions.Width   = 1;
        this.upLineOptions.LineStyle = LineStyle.Solid;
        this.upLineOptions.WithCheckBox = false;

        this.downLineOptions.Color   = this.DefaultDownColor;
        this.downLineOptions.Width   = 1;
        this.downLineOptions.LineStyle = LineStyle.Solid;
        this.downLineOptions.WithCheckBox = false;

        this.lineUpPen   = new Pen(this.upLineOptions.Color, this.upLineOptions.Width) { DashStyle = (System.Drawing.Drawing2D.DashStyle)this.upLineOptions.LineStyle };
        this.lineDownPen = new Pen(this.downLineOptions.Color, this.downLineOptions.Width) { DashStyle = (System.Drawing.Drawing2D.DashStyle)this.downLineOptions.LineStyle };

        this.AddLineSeries("Up", this.DefaultUpColor, 1, LineStyle.Histogramm).Visible = false;
        this.AddLineSeries("Down", this.DefaultDownColor, 1, LineStyle.Histogramm).Visible = false;
        this.AddLineSeries("Down Countdown", this.CountdownDownColor, 1, LineStyle.Histogramm).Visible = false;
        this.AddLineSeries("Up Countdown", this.CountdownUpColor, 1, LineStyle.Histogramm).Visible = false;
    }


    #region Overrides

    protected override void OnInit()
    {
        this.upValue = default;
        this.downValue = default;

        this.buyCountdown = default;
        this.sellCountdown = default;

        this.buyCountdownActive = false;
        this.sellCountdownActive = false;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.Count < MIN_PERIOD)
            return;

        if (args.Reason != UpdateReason.NewBar && args.Reason != UpdateReason.HistoricalBar)
            return;

        var prevClose = this.Close(ONE);
        var prevFiveClose = this.Close(FIVE);

        // Setup Up
        if (prevClose > prevFiveClose)
            this.upValue += ONE;
        else
            this.upValue = ZERO;

        if (this.IsCorrectSetupValue(this.upValue))
            this.SetValue(this.upValue, SERIES_SETUP_UP, ONE);

        // Setup Down
        if (prevClose < prevFiveClose)
            this.downValue += ONE;
        else
            this.downValue = ZERO;

        if (this.IsCorrectSetupValue(this.downValue))
            this.SetValue(this.downValue, SERIES_SETUP_DOWN, ONE);

        // Countdown 
        if (this.Calculation == CalculationMode.SetupPlusCountdown13)
        {
            if (this.upValue == SETUP_MAX)
            {
                this.sellCountdownActive = true;
                this.buyCountdownActive = false;
                this.sellCountdown = 0;
            }

            if (this.downValue == SETUP_MAX)
            {
                this.buyCountdownActive = true;
                this.sellCountdownActive = false;
                this.buyCountdown = 0;
            }

            if (this.buyCountdownActive && this.Count > THREE)
            {
                var prevTwoClose = this.Close(THREE);
                if (prevClose <= prevTwoClose)
                {
                    this.buyCountdown++;
                    if (this.IsCorrectCountdownValue(this.buyCountdown))
                        this.SetValue(this.buyCountdown, SERIES_CD_BUY, ONE);

                    if (this.buyCountdown >= COUNTDOWN_MAX)
                        this.buyCountdownActive = false;
                }
            }

            if (this.sellCountdownActive && this.Count > THREE)
            {
                var prevTwoClose = this.Close(THREE);
                if (prevClose >= prevTwoClose)
                {
                    this.sellCountdown++;
                    if (this.IsCorrectCountdownValue(this.sellCountdown))
                        this.SetValue(this.sellCountdown, SERIES_CD_SELL, ONE);

                    if (this.sellCountdown >= COUNTDOWN_MAX)
                        this.sellCountdownActive = false;
                }
            }
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var calcGroup = new SettingItemSeparatorGroup(GROUP_CALC, 0);
            var colorsGroup = new SettingItemSeparatorGroup(GROUP_COLORS, 1);
            var numbersGroup = new SettingItemSeparatorGroup(GROUP_NUMBERS, 2);


            settings.Add(new SettingItemSelectorLocalized(CALCULATION_SI, this.Calculation, new List<SelectItem>
        {
            new SelectItem("Only 9 (Setup)", CalculationMode.OnlySetup9),
            new SelectItem("9 + 13 (Setup + Countdown)", CalculationMode.SetupPlusCountdown13),
        })
            {
                Text = CALCULATION_SI,
                SeparatorGroup = calcGroup,
            });

            var countdownRelation = new SettingItemRelationVisibility(
                CALCULATION_SI,
                new SelectItem("9 + 13 (Setup + Countdown)", CalculationMode.SetupPlusCountdown13)
            );

            settings.Add(new SettingItemColor(UP_COLOR_SI, this.DefaultUpColor)
            {
                Text = UP_COLOR_SI,
                SeparatorGroup = colorsGroup,
            });
            settings.Add(new SettingItemColor(DOWN_COLOR_SI, this.DefaultDownColor)
            {
                Text = DOWN_COLOR_SI,
                SeparatorGroup = colorsGroup,
            });

            settings.Add(new SettingItemColor(UP_CD_COLOR_SI, this.CountdownUpColor)
            {
                Text = UP_CD_COLOR_SI,
                SeparatorGroup = colorsGroup,
                Relation = countdownRelation,
            });
            settings.Add(new SettingItemColor(DOWN_CD_COLOR_SI, this.CountdownDownColor)
            {
                Text = DOWN_CD_COLOR_SI,
                SeparatorGroup = colorsGroup,
                Relation = countdownRelation,
            });

            // Numbers display
            settings.Add(new SettingItemSelectorLocalized(SHOW_NUMBERS_SI, this.VisualMode, new List<SelectItem>
        {
            new SelectItem("None", TDSVisualMode.None),
            new SelectItem("All", TDSVisualMode.All),
            new SelectItem("From value", TDSVisualMode.FromValue),
        })
            {
                Text = SHOW_NUMBERS_SI,
                SeparatorGroup = numbersGroup,
            });

            var showFromRelation = new SettingItemRelationVisibility(
                SHOW_NUMBERS_SI,
                new SelectItem("", (int)TDSVisualMode.FromValue)
            );

            settings.Add(new SettingItemInteger(FROM_VALUE_SI, this.FromValue)
            {
                Text = FROM_VALUE_SI,
                Minimum = 1,
                Maximum = COUNTDOWN_MAX,
                SeparatorGroup = numbersGroup,
                Relation = showFromRelation,
            });
            // --- Lines group ---
            var linesGroup = new SettingItemSeparatorGroup("Lines", 3);

            settings.Add(new SettingItemBoolean("ShowLines", this.showLines)
            {
                Text = "Show lines",
                SeparatorGroup = linesGroup,
            });

            var showLinesRelation = new SettingItemRelationVisibility("ShowLines", true);

            settings.Add(new SettingItemLineOptions("UpLine", this.upLineOptions)
            {
                Text = "Up line style",
                SeparatorGroup = linesGroup,
                Relation = showLinesRelation,
                ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
            });

            settings.Add(new SettingItemLineOptions("DownLine", this.downLineOptions)
            {
                Text = "Down line style",
                SeparatorGroup = linesGroup,
                Relation = showLinesRelation,
                ExcludedStyles = new[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true,
            });

            return settings;
        }
        set
        {
            var prevCalc = this.Calculation;

            base.Settings = value;

            if (value.TryGetValue(CALCULATION_SI, out CalculationMode calc))
                this.Calculation = calc;

            if (value.TryGetValue(UP_COLOR_SI, out Color upColor))
                this.DefaultUpColor = upColor;

            if (value.TryGetValue(DOWN_COLOR_SI, out Color downColor))
                this.DefaultDownColor = downColor;

            if (value.TryGetValue(UP_CD_COLOR_SI, out Color upCdColor))
                this.CountdownUpColor = upCdColor;

            if (value.TryGetValue(DOWN_CD_COLOR_SI, out Color downCdColor))
                this.CountdownDownColor = downCdColor;

            if (value.TryGetValue(SHOW_NUMBERS_SI, out TDSVisualMode vm))
                this.VisualMode = vm;

            if (value.TryGetValue(FROM_VALUE_SI, out int fromVal))
                this.FromValue = Math.Max(1, Math.Min(COUNTDOWN_MAX, fromVal));
            if (value.TryGetValue("ShowLines", out bool showLines))
                this.showLines = showLines;

            if (value.TryGetValue("UpLine", out LineOptions upLine))
            {
                this.upLineOptions = upLine;
                this.lineUpPen.Color = upLine.Color;
                this.lineUpPen.Width = upLine.Width;
                this.lineUpPen.DashStyle = (System.Drawing.Drawing2D.DashStyle)upLine.LineStyle;
            }

            if (value.TryGetValue("DownLine", out LineOptions downLine))
            {
                this.downLineOptions = downLine;
                this.lineDownPen.Color = downLine.Color;
                this.lineDownPen.Width = downLine.Width;
                this.lineDownPen.DashStyle = (System.Drawing.Drawing2D.DashStyle)downLine.LineStyle;
            }
            if (this.Calculation != prevCalc)
                this.OnSettingsUpdated();
        }
    }


    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;
        var prevClip = gr.ClipBounds;
        var prevTextHint = gr.TextRenderingHint;

        try
        {
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            gr.SetClip(this.CurrentChart.MainWindow.ClientRectangle);

            var endUpLinePointX = double.NaN;
            var endDownLinePointX = double.NaN;
            var point = default(Point);

            var t1 = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(this.CurrentChart.MainWindow.ClientRectangle.Right);
            var rightIndex = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(t1);
            var startOffset = Math.Max(this.HistoricalData.Count - rightIndex - 1, 0);

            for (int i = startOffset; i < this.HistoricalData.Count - MIN_PERIOD - 1; i++)
            {
                if (this.HistoricalData[i, SeekOriginHistory.End] is not HistoryItemBar item)
                    break;

                var startBarPointX = this.CurrentChart.MainWindow.CoordinatesConverter.GetChartX(item.TimeLeft);
                var endBarPointX = startBarPointX + this.CurrentChart.BarsWidth;

                var upVal = this.GetValue(i, SERIES_SETUP_UP);
                var downVal = this.GetValue(i, SERIES_SETUP_DOWN);

                var buyCdVal = this.GetValue(i, SERIES_CD_BUY);
                var sellCdVal = this.GetValue(i, SERIES_CD_SELL);

                if (endBarPointX > ZERO && endBarPointX <= this.CurrentChart.MainWindow.ClientRectangle.Right)
                {
                    bool hasDownSetup = this.IsCorrectSetupValue(downVal);
                    bool hasUpSetup = this.IsCorrectSetupValue(upVal);
                    bool hasBuyCd = this.IsCorrectCountdownValue(buyCdVal);
                    bool hasSellCd = this.IsCorrectCountdownValue(sellCdVal);

                    int yLow = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low);
                    int yHigh = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High);

                    point.X = (int)(startBarPointX + this.CurrentChart.BarsWidth / 2);

                    int downSetupH = 0, buyCdH = 0;

                    if (hasDownSetup)
                    {
                        string s = ((int)downVal).ToString();
                        var font = Math.Abs(downVal - SETUP_MAX) > double.Epsilon ? this.defaultFont : this.extraFont;
                        downSetupH = (int)gr.MeasureString(s, font).Height;

                        point.Y = yLow + downSetupH / 2;
                        gr.DrawString(s, font, this.defaultDownPen.Brush, point, centerCenterSF);
                    }

                    if (hasBuyCd)
                    {
                        string s = ((int)buyCdVal).ToString();
                        var font = Math.Abs(buyCdVal - COUNTDOWN_MAX) > double.Epsilon ? this.defaultFont : this.extraFont;
                        buyCdH = (int)gr.MeasureString(s, font).Height;

                        int baseY = hasDownSetup ? (yLow + downSetupH + LABEL_GAP) : yLow;
                        point.Y = baseY + buyCdH / 2;

                        gr.DrawString(s, font, this.countdownDownPen.Brush, point, centerCenterSF);
                    }

                    int upSetupH = 0, sellCdH = 0;

                    if (hasUpSetup)
                    {
                        string s = ((int)upVal).ToString();
                        var font = Math.Abs(upVal - SETUP_MAX) > double.Epsilon ? this.defaultFont : this.extraFont;
                        upSetupH = (int)gr.MeasureString(s, font).Height;

                        point.Y = yHigh - upSetupH / 2;
                        gr.DrawString(s, font, this.defaultUpPen.Brush, point, centerCenterSF);
                    }

                    if (hasSellCd)
                    {
                        string s = ((int)sellCdVal).ToString();
                        var font = Math.Abs(sellCdVal - COUNTDOWN_MAX) > double.Epsilon ? this.defaultFont : this.extraFont;
                        sellCdH = (int)gr.MeasureString(s, font).Height;

                        int baseY = hasUpSetup ? (yHigh - upSetupH - LABEL_GAP) : yHigh;
                        point.Y = baseY - sellCdH / 2;

                        gr.DrawString(s, font, this.countdownUpPen.Brush, point, centerCenterSF);
                    }
                }

                // Draw lines
                if (double.IsNaN(endUpLinePointX))
                {
                    endUpLinePointX = endBarPointX;
                    endDownLinePointX = endBarPointX;
                }

                if (startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Left)
                    startBarPointX = this.CurrentChart.MainWindow.ClientRectangle.Left - TEN;

                if (endDownLinePointX > this.CurrentChart.MainWindow.ClientRectangle.Right + TEN)
                    endDownLinePointX = this.CurrentChart.MainWindow.ClientRectangle.Right + TEN;

                if (endUpLinePointX > this.CurrentChart.MainWindow.ClientRectangle.Right + TEN)
                    endUpLinePointX = this.CurrentChart.MainWindow.ClientRectangle.Right + TEN;

                if (this.showLines)
                {
                    if (Math.Abs(downVal - SETUP_MAX) < double.Epsilon
                        && endDownLinePointX > ZERO
                        && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                    {
                        var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.Low);
                        gr.DrawLine(this.lineDownPen, (int)startBarPointX, pointY, (int)endDownLinePointX, pointY);
                        endDownLinePointX = startBarPointX;
                    }
                    else if (Math.Abs(upVal - SETUP_MAX) < double.Epsilon
                        && endUpLinePointX > ZERO
                        && startBarPointX < this.CurrentChart.MainWindow.ClientRectangle.Right)
                    {
                        var pointY = (int)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(item.High);
                        gr.DrawLine(this.lineUpPen, (int)startBarPointX, pointY, (int)endUpLinePointX, pointY);
                        endUpLinePointX = startBarPointX;
                    }
                }

                if (endBarPointX < ZERO && endDownLinePointX < ZERO && endUpLinePointX < ZERO)
                    break;
            }
        }
        catch { }
        finally
        {
            gr.TextRenderingHint = prevTextHint;
            gr.SetClip(prevClip);
        }
    }

    #endregion Overrides

    private bool IsCorrectSetupValue(double value)
    {
        if (double.IsNaN(value) || value <= 0 || value > SETUP_MAX)
            return false;

        return this.VisualMode switch
        {
            TDSVisualMode.All => true,
            TDSVisualMode.None => false,
            TDSVisualMode.FromValue => value >= this.FromValue,
            _ => true,
        };
    }

    private bool IsCorrectCountdownValue(double value)
    {
        if (double.IsNaN(value) || value <= 0 || value > COUNTDOWN_MAX)
            return false;

        return this.Calculation == CalculationMode.SetupPlusCountdown13 && this.VisualMode switch
        {
            TDSVisualMode.All => true,
            TDSVisualMode.None => false,
            TDSVisualMode.FromValue => value >= this.FromValue,
            _ => true,
        };
    }
}
