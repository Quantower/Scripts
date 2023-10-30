// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace OscillatorsIndicators;

public class IndicatorDivergenceDetector : Indicator
{
    #region Parameters

    [InputParameter("Pivot lookback left offset", 20, 1, 9999, 1, 0)]
    public int Left = 5;

    [InputParameter("Pivot lookback right offset", 30, 1, 9999, 1, 0)]
    public int Right = 5;

    public LineOptions RegularBullishLineStyle
    {
        get => this.regularBullishLineStyle;
        private set
        {
            this.regularBullishLineStyle = value;
            this.regularBullishLinePen = ProcessPen(this.regularBullishLinePen, value);
        }
    }
    private LineOptions regularBullishLineStyle;
    private Pen regularBullishLinePen;

    public LineOptions HiddenBullishLineStyle
    {
        get => this.hiddenBullishLineStyle;
        private set
        {
            this.hiddenBullishLineStyle = value;
            this.hiddenBullishLinePen = ProcessPen(this.hiddenBullishLinePen, value);
        }
    }
    private LineOptions hiddenBullishLineStyle;
    private Pen hiddenBullishLinePen;

    public LineOptions RegularBearishLineStyle
    {
        get => this.regularBearishLineStyle;
        private set
        {
            this.regularBearishLineStyle = value;
            this.regularBearishLinePen = ProcessPen(this.regularBearishLinePen, value);
        }
    }
    private LineOptions regularBearishLineStyle;
    private Pen regularBearishLinePen;

    public LineOptions HiddenBearishLineStyle
    {
        get => this.hiddenBearishLineStyle;
        private set
        {
            this.hiddenBearishLineStyle = value;
            this.hiddenBearishLinePen = ProcessPen(this.hiddenBearishLinePen, value);
        }
    }
    private LineOptions hiddenBearishLineStyle;
    private Pen hiddenBearishLinePen;

    private Indicator subIndicator;
    private readonly List<int> bullishPatternIndexes;
    private readonly List<int> bearishPatternIndexes;

    private readonly IList<DivergenceRange> divergenceRanges;
    private readonly StringFormat centerNearSF;
    private Font currentFont;

    private string selectedIndicatorName;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDivergenceDetector.cs";

    #endregion Parameters

    #region RSI parameters

    private int rsiPeriod;
    private PriceType rsiSourcePrice;
    private RSIMode rsiMode;
    public IndicatorCalculationType rsiCalculationType;

    #endregion RSI parameters

    #region MACD parameters

    public int macdFastPeriod;
    public int macdSlowPeriod;
    public IndicatorCalculationType macdCalculationType;

    #endregion MACD parameters

    public IndicatorDivergenceDetector()
    {
        this.Name = "Divergence Detector";

        this.bullishPatternIndexes = new List<int>();
        this.bearishPatternIndexes = new List<int>();

        this.divergenceRanges = new List<DivergenceRange>();

        this.RegularBullishLineStyle = new LineOptions()
        {
            Color = Color.Green,
            Enabled = true,
            LineStyle = LineStyle.Solid,
            Width = 2,
            WithCheckBox = true,
            WithColor = true,
            WithNumeric = true
        };
        this.HiddenBullishLineStyle = new LineOptions()
        {
            Color = Color.Green,
            Enabled = false,
            LineStyle = LineStyle.DashDot,
            Width = 1,
            WithCheckBox = true,
            WithColor = true,
            WithNumeric = true
        };
        this.RegularBearishLineStyle = new LineOptions()
        {
            Color = Color.Red,
            Enabled = true,
            LineStyle = LineStyle.Solid,
            Width = 2,
            WithCheckBox = true,
            WithColor = true,
            WithNumeric = true
        };
        this.HiddenBearishLineStyle = new LineOptions()
        {
            Color = Color.Red,
            Enabled = false,
            LineStyle = LineStyle.DashDot,
            Width = 1,
            WithCheckBox = true,
            WithColor = true,
            WithNumeric = true
        };

        this.currentFont = new Font("Verdana", 10, GraphicsUnit.Pixel);
        this.centerNearSF = new StringFormat()
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };

        this.selectedIndicatorName = RSI;

        this.rsiPeriod = 14;
        this.rsiSourcePrice = PriceType.Close;
        this.rsiMode = RSIMode.Exponential;
        this.rsiCalculationType = Indicator.DEFAULT_CALCULATION_TYPE;

        this.macdFastPeriod = 12;
        this.macdSlowPeriod = 26;
        this.macdCalculationType = Indicator.DEFAULT_CALCULATION_TYPE;
    }

    protected override void OnInit()
    {
        switch (this.selectedIndicatorName)
        {
            case RSI:
                {
                    this.subIndicator = Core.Instance.Indicators.BuiltIn.RSI(this.rsiPeriod, this.rsiSourcePrice, this.rsiMode, MaMode.SMA, 1, this.rsiCalculationType);
                    this.AddIndicator(this.subIndicator);
                    break;
                }
            case MACD:
                {
                    this.subIndicator = Core.Instance.Indicators.BuiltIn.MACD(this.macdFastPeriod, this.macdSlowPeriod, 1, this.rsiCalculationType);
                    this.AddIndicator(this.subIndicator);
                    break;
                }
        }
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (this.subIndicator == null)
            return;

        if (args.Reason == UpdateReason.NewTick)
            return;

        if (this.Count <= this.Left + this.Right && this.subIndicator?.Count <= this.Left + this.Right)
            return;

        bool hasBullishPattern = !double.IsNaN(PivotLow(this.subIndicator, this.Left, this.Right));
        bool hasBearishPattern = !double.IsNaN(PivotHigh(this.subIndicator, this.Left, this.Right));

        //
        // Regular and Hidden Bullish
        //
        if (hasBullishPattern)
        {
            if (this.bullishPatternIndexes.Count == 0 || this.bullishPatternIndexes[this.bullishPatternIndexes.Count - 1] != this.Count - 1)
                this.bullishPatternIndexes.Add(this.Count - 1);

            if (this.bullishPatternIndexes.Count > 2)
            {
                var prevPatternIndex = this.bullishPatternIndexes[this.bullishPatternIndexes.Count - 2];
                var prevIndicatorValue = this.subIndicator.GetValue(prevPatternIndex - this.Right, 0, SeekOriginHistory.Begin);
                var indicatorValue = this.subIndicator.GetValue(this.Right);
                var lowPrice = this.GetPrice(PriceType.Low, this.Right);
                var prevLowPrice = this.HistoricalData[prevPatternIndex - this.Right, SeekOriginHistory.Begin][PriceType.Low];

                var leftOffset = this.Count - prevPatternIndex + this.Right - 1;
                var rightOffset = this.Right;

                if (indicatorValue > prevIndicatorValue && lowPrice < prevLowPrice)
                    this.divergenceRanges.Add(new DivergenceRange(this.Time(leftOffset), prevLowPrice, this.Time(rightOffset), lowPrice, DivergenceType.RegularBullish));

                if (indicatorValue < prevIndicatorValue && lowPrice > prevLowPrice)
                    this.divergenceRanges.Add(new DivergenceRange(this.Time(leftOffset), prevLowPrice, this.Time(rightOffset), lowPrice, DivergenceType.HiddenBullish));
            }
        }

        //
        // Regular and Hidden Bearish
        //
        if (hasBearishPattern)
        {
            if (this.bearishPatternIndexes.Count == 0 || this.bearishPatternIndexes[this.bearishPatternIndexes.Count - 1] != this.Count - 1)
                this.bearishPatternIndexes.Add(this.Count - 1);

            if (this.bearishPatternIndexes.Count > 2)
            {
                var prevPatternIndex = this.bearishPatternIndexes[this.bearishPatternIndexes.Count - 2];
                var prevIndicatorValue = this.subIndicator.GetValue(prevPatternIndex - this.Right, 0, SeekOriginHistory.Begin);
                var indicatorValue = this.subIndicator.GetValue(this.Right);
                var highPrice = this.GetPrice(PriceType.High, this.Right);
                var prevHighPrice = this.HistoricalData[prevPatternIndex - this.Right, SeekOriginHistory.Begin][PriceType.High];

                var leftOffset = this.Count - prevPatternIndex + this.Right - 1;
                var rightOffset = this.Right;

                if (indicatorValue < prevIndicatorValue && highPrice > prevHighPrice)
                    this.divergenceRanges.Add(new DivergenceRange(this.Time(leftOffset), prevHighPrice, this.Time(rightOffset), highPrice, DivergenceType.RegularBearish));

                if (indicatorValue > prevIndicatorValue && highPrice < prevHighPrice)
                    this.divergenceRanges.Add(new DivergenceRange(this.Time(leftOffset), prevHighPrice, this.Time(rightOffset), highPrice, DivergenceType.HiddenBearish));
            }
        }
    }
    protected override void OnClear()
    {
        if (this.subIndicator != null)
            this.RemoveIndicator(this.subIndicator);

        this.bearishPatternIndexes.Clear();
        this.bullishPatternIndexes.Clear();

        this.divergenceRanges.Clear();
    }
    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var inputSeparatorGroup = settings.Count > 0
                ? settings[0].SeparatorGroup
                : new SettingItemSeparatorGroup("");

            var separatorGroup = new SettingItemSeparatorGroup(string.Empty, -999);
            settings.Add(new SettingItemLineOptions("RegularBullishStyle", this.RegularBullishLineStyle, 50)
            {
                Text = loc._("Regular bullish line style"),
                SeparatorGroup = separatorGroup
            });
            settings.Add(new SettingItemLineOptions("HiddenBullishStyle", this.HiddenBullishLineStyle, 60)
            {
                Text = loc._("Hidden bullish line style"),
                SeparatorGroup = separatorGroup
            });
            settings.Add(new SettingItemLineOptions("RegularBearishStyle", this.RegularBearishLineStyle, 70)
            {
                Text = loc._("Regular bearish line style"),
                SeparatorGroup = separatorGroup
            });
            settings.Add(new SettingItemLineOptions("HiddenBearishStyle", this.HiddenBearishLineStyle, 80)
            {
                Text = loc._("Hidden bearish line style"),
                SeparatorGroup = separatorGroup
            });
            settings.Add(new SettingItemFont("CurrentFont", this.currentFont, 90)
            {
                Text = loc._("Font"),
                SeparatorGroup = separatorGroup
            });

            if (!string.IsNullOrEmpty(this.selectedIndicatorName))
            {
                settings.Add(new SettingItemSelector("SelectedIndicator", this.selectedIndicatorName, new List<string>() { RSI, MACD })
                {
                    Text = loc._("Oscillator"),
                    SeparatorGroup = inputSeparatorGroup
                });

                //
                // RSI settings
                //
                var rsiRelation = new SettingItemRelationVisibility("SelectedIndicator", RSI);
                settings.Add(this.CreateDefaultIntegerSettingItem("RSI period", "RSI period", this.rsiPeriod, rsiRelation, inputSeparatorGroup));
                settings.Add(this.CreateSourcePriceSettingItem("RSI source price", "Sources prices for the RSI line", this.rsiSourcePrice, rsiRelation, inputSeparatorGroup));
                settings.Add(new SettingItemSelectorLocalized("RSI mode", new SelectItem("", (int)this.rsiMode), new List<SelectItem>()
                {
                    new SelectItem("Simple", (int)RSIMode.Simple),
                    new SelectItem("Exponential", (int)RSIMode.Exponential),
                })
                { Relation = rsiRelation, Text = loc._("Mode for the RSI line"), SeparatorGroup = inputSeparatorGroup });
                settings.Add(this.CreateCalculationTypeSettingItem("RSI Calculation type", "Calculation type", this.rsiCalculationType, rsiRelation, inputSeparatorGroup));

                //
                // MACD settings
                //
                var macdRelation = new SettingItemRelationVisibility("SelectedIndicator", MACD);
                settings.Add(this.CreateDefaultIntegerSettingItem("MACD Period of fast EMA", "Period of fast EMA", this.macdFastPeriod, macdRelation, inputSeparatorGroup));
                settings.Add(this.CreateDefaultIntegerSettingItem("MACD Period of slow EMA", "Period of slow EMA", this.macdSlowPeriod, macdRelation, inputSeparatorGroup));
                settings.Add(this.CreateCalculationTypeSettingItem("MACD Calculation type", "Calculation type", this.macdCalculationType, macdRelation, inputSeparatorGroup));
            }

            return settings;
        }
        set
        {
            if (value.GetItemByName("RegularBullishStyle")?.Value is LineOptions regularBullish)
                this.RegularBullishLineStyle = regularBullish;

            if (value.GetItemByName("HiddenBullishStyle")?.Value is LineOptions hiddenBullish)
                this.HiddenBullishLineStyle = hiddenBullish;

            if (value.GetItemByName("RegularBearishStyle")?.Value is LineOptions regularBearish)
                this.RegularBearishLineStyle = regularBearish;

            if (value.GetItemByName("HiddenBearishStyle")?.Value is LineOptions hiddenBearish)
                this.HiddenBearishLineStyle = hiddenBearish;

            if (value.GetItemByName("CurrentFont")?.Value is Font font)
                this.currentFont = font;

            //
            // Oscillator settings
            //
            var needRefresh = false;
            if (value.GetItemByName("SelectedIndicator") is SettingItemSelector indicatorNameSI)
            {
                this.selectedIndicatorName = indicatorNameSI.Value.ToString();
                if (!needRefresh)
                    needRefresh = indicatorNameSI.ValueChangingReason == SettingItemValueChangingReason.Manually;
            }

            //
            // RSI settings
            //
            if (value.GetItemByName("RSI period") is SettingItemInteger rsiPeriodSI)
            {
                var newValue = (int)rsiPeriodSI.Value;

                if (!needRefresh)
                    needRefresh = rsiPeriodSI.ValueChangingReason == SettingItemValueChangingReason.Manually && this.rsiPeriod != newValue;

                this.rsiPeriod = newValue;
            }
            if (value.GetItemByName("RSI source price") is SettingItemSelectorLocalized rsiSourcePriceSI)
            {
                var newValue = (PriceType)((SelectItem)rsiSourcePriceSI.Value).Value;

                if (!needRefresh)
                    needRefresh = rsiSourcePriceSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.rsiSourcePrice;

                this.rsiSourcePrice = newValue;
            }
            if (value.GetItemByName("RSI mode") is SettingItemSelectorLocalized rsiModeSI)
            {
                var newValue = (RSIMode)((SelectItem)rsiModeSI.Value).Value;

                if (!needRefresh)
                    needRefresh = rsiModeSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.rsiMode;

                this.rsiMode = newValue;
            }
            if (value.GetItemByName("RSI Calculation type") is SettingItemSelectorLocalized rsiCalculationTypeSI)
            {
                var newValue = (IndicatorCalculationType)((SelectItem)rsiCalculationTypeSI.Value).Value;

                if (!needRefresh)
                    needRefresh = rsiCalculationTypeSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.rsiCalculationType;

                this.rsiCalculationType = newValue;
            }

            //
            // MACD settings
            //
            if (value.GetItemByName("MACD Period of fast EMA") is SettingItemInteger macdFastPeriodSI)
            {
                var newValue = (int)macdFastPeriodSI.Value;

                if (!needRefresh)
                    needRefresh = macdFastPeriodSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.macdFastPeriod;

                this.macdFastPeriod = newValue;
            }
            if (value.GetItemByName("MACD Period of slow EMA") is SettingItemInteger macdSlowPeriodSI)
            {
                var newValue = (int)macdSlowPeriodSI.Value;

                if (!needRefresh)
                    needRefresh = macdSlowPeriodSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.macdSlowPeriod;

                this.macdSlowPeriod = newValue;
            }
            if (value.GetItemByName("RSI Calculation type") is SettingItemSelectorLocalized macdCalculationTypeSI)
            {
                var newValue = (IndicatorCalculationType)((SelectItem)macdCalculationTypeSI.Value).Value;

                if (!needRefresh)
                    needRefresh = macdCalculationTypeSI.ValueChangingReason == SettingItemValueChangingReason.Manually && newValue != this.macdCalculationType;

                this.macdCalculationType = newValue;
            }

            base.Settings = value;

            if (needRefresh)
                this.Refresh();
        }
    }

    #region Drawing

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;
        gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var restoredClip = gr.ClipBounds;

        gr.SetClip(args.Rectangle);

        try
        {
            var currentWindow = this.CurrentChart.Windows[args.WindowIndex];

            var leftTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Left);
            var rightTime = currentWindow.CoordinatesConverter.GetTime(args.Rectangle.Right);
            var halfBarWidth = this.CurrentChart.BarsWidth / 2;

            var tempPen = new Pen(Color.Empty);
            var lineOption = new LineOptions();
            var isTopBilletPosition = false;
            var billetText = string.Empty;

            for (int i = this.divergenceRanges.Count - 1; i >= 0; i--)
            {
                var divergenceRange = this.divergenceRanges[i];

                if (divergenceRange.LeftPoint.Time > rightTime)
                    continue;
                if (divergenceRange.RightPoint.Time < leftTime)
                    break;

                //
                //
                //
                switch (divergenceRange.Type)
                {
                    case DivergenceType.RegularBullish:
                        {
                            if (!this.RegularBullishLineStyle.Enabled)
                                continue;

                            tempPen = this.regularBullishLinePen;
                            lineOption = this.RegularBullishLineStyle;
                            isTopBilletPosition = false;
                            billetText = "Regular bull";
                            break;
                        }
                    case DivergenceType.HiddenBullish:
                        {
                            if (!this.HiddenBullishLineStyle.Enabled)
                                continue;

                            tempPen = this.hiddenBullishLinePen;
                            lineOption = this.HiddenBullishLineStyle;
                            isTopBilletPosition = false;
                            billetText = "Hidden bull";
                            break;
                        }
                    case DivergenceType.RegularBearish:
                        {
                            if (!this.RegularBearishLineStyle.Enabled)
                                continue;

                            tempPen = this.regularBearishLinePen;
                            lineOption = this.RegularBearishLineStyle;
                            isTopBilletPosition = true;
                            billetText = "Regular bear";
                            break;
                        }
                    case DivergenceType.HiddenBearish:
                        {
                            if (!this.HiddenBearishLineStyle.Enabled)
                                continue;

                            tempPen = this.hiddenBearishLinePen;
                            lineOption = this.HiddenBearishLineStyle;
                            isTopBilletPosition = true;
                            billetText = "Hidden bear";
                            break;
                        }
                }

                //
                //
                //
                var leftRangeX = (float)currentWindow.CoordinatesConverter.GetChartX(divergenceRange.LeftPoint.Time) + halfBarWidth;
                var leftRangeY = (float)currentWindow.CoordinatesConverter.GetChartY(divergenceRange.LeftPoint.Price);

                var rightRangeX = (float)currentWindow.CoordinatesConverter.GetChartX(divergenceRange.RightPoint.Time) + halfBarWidth;
                var rightRangeY = (float)currentWindow.CoordinatesConverter.GetChartY(divergenceRange.RightPoint.Price);

                gr.DrawLine(tempPen, leftRangeX, leftRangeY, rightRangeX, rightRangeY);
                DrawBillet(gr, ref rightRangeX, ref rightRangeY, isTopBilletPosition, this.currentFont, lineOption, tempPen, this.centerNearSF, billetText);
            }

        }
        catch { }
        finally
        {
            gr.SetClip(restoredClip);
        }
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
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                        break;
                    }
                case LineStyle.Dot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                        break;
                    }
                case LineStyle.Dash:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        break;
                    }
                case LineStyle.DashDot:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 2, 4, 7, 4 };
                        pen.DashPattern = dp;
                        break;
                    }
                case LineStyle.Histogramm:
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                        float[] dp = new float[] { 0.25F, 1 };
                        pen.DashPattern = dp;
                        pen.Width = 4;
                        break;
                    }
            }
        }
        catch { }
        return pen;
    }
    private static void DrawBillet(Graphics gr, ref float rightX, ref float priceY, bool isTopBilletPosition, Font font, LineOptions lineOptions, Pen pen, StringFormat stringFormat, string text)
    {
        var labelSize = gr.MeasureString(text, font);
        var height = labelSize.Height;
        var posY = priceY - height - lineOptions.Width;

        var rect = new RectangleF()
        {
            Height = height,
            Width = labelSize.Width + 5,
            X = rightX,
            Y = isTopBilletPosition ? posY : posY + height
        };

        gr.FillRectangle(pen.Brush, rect);
        gr.DrawString(text, font, Brushes.White, rect, stringFormat);
    }

    #endregion Drawing

    #region Misc

    private SettingItem CreateCalculationTypeSettingItem(string name, string text, IndicatorCalculationType calculationType, SettingItemRelationVisibility relation, SettingItemSeparatorGroup separatorGroup)
    {
        return new SettingItemSelectorLocalized(name, new SelectItem("", (int)calculationType), new List<SelectItem>()
        {
            new SelectItem("All available data", (int)IndicatorCalculationType.AllAvailableData),
            new SelectItem("By period", (int)IndicatorCalculationType.ByPeriod),
        })
        { Relation = relation, Text = text, SeparatorGroup = separatorGroup };
    }
    private SettingItem CreateSourcePriceSettingItem(string name, string text, PriceType sourcePrice, SettingItemRelationVisibility relation, SettingItemSeparatorGroup separatorGroup)
    {
        return new SettingItemSelectorLocalized(name, new SelectItem("", (int)sourcePrice), new List<SelectItem>()
        {
            new SelectItem("Close", (int)PriceType.Close),
            new SelectItem("Open", (int)PriceType.Open),
            new SelectItem("High", (int)PriceType.High),
            new SelectItem("Low", (int)PriceType.Low),
            new SelectItem("Typical", (int)PriceType.Typical),
            new SelectItem("Medium", (int)PriceType.Median),
            new SelectItem("Weighted", (int)PriceType.Weighted),
            new SelectItem("Volume", (int)PriceType.Volume),
            new SelectItem("Open interest", (int)PriceType.OpenInterest),
        })
        { Relation = relation, Text = text, SeparatorGroup = separatorGroup };
    }
    private SettingItem CreateDefaultIntegerSettingItem(string name, string text, int value, SettingItemRelationVisibility relation, SettingItemSeparatorGroup separatorGroup)
    {
        return new SettingItemInteger(name, value)
        {
            Minimum = 1,
            Maximum = 9999,
            Increment = 1,
            Relation = relation,
            Text = text,
            SeparatorGroup = separatorGroup
        };
    }

    private static double PivotLow(Indicator indicator, int left, int right, int offset = 0)
    {
        var target = indicator.GetValue(offset + right);

        for (int i = offset + left + right; i >= offset; i--)
        {
            var sourcePrice = indicator.GetValue(i);

            if (sourcePrice < target)
                return double.NaN;
        }

        return target;
    }
    private static double PivotHigh(Indicator indicator, int left, int right, int offset = 0)
    {
        var target = indicator.GetValue(offset + right);

        for (int i = offset + left + right; i >= offset; i--)
        {
            var sourcePrice = indicator.GetValue(i);

            if (sourcePrice > target)
                return double.NaN;
        }

        return target;
    }

    #endregion Misc
}

#region Inner types

internal struct DivergenceRange
{
    public DivergencePoint LeftPoint { get; private set; }
    public DivergencePoint RightPoint { get; private set; }

    public DivergenceType Type { get; private set; }

    public DivergenceRange(DateTime leftTime, double leftPrice, DateTime rightTime, double rightPrice, DivergenceType type)
        : this(new DivergencePoint(leftTime, leftPrice), new DivergencePoint(rightTime, rightPrice), type)
    { }
    public DivergenceRange(DivergencePoint left, DivergencePoint right, DivergenceType type)
    {
        this.LeftPoint = left;
        this.RightPoint = right;
        this.Type = type;
    }
}
internal struct DivergencePoint
{
    public DateTime Time { get; private set; }
    public double Price { get; private set; }

    public DivergencePoint(DateTime time, double price)
    {
        this.Time = time;
        this.Price = price;
    }
}
internal enum DivergenceType
{
    RegularBullish,
    HiddenBullish,

    RegularBearish,
    HiddenBearish
}

#endregion Inner types