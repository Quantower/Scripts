// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace GannsIndicators;

public class IndicatorSquareLevels : Indicator
{
    private const string ONE_DIV_THREE_PROPORTION_GROUP = "1/3 proportion";
    private const string ONE_DIV_FOUR_PROPORTION_GROUP = "1/4 proportion";
    private const string ONE_DIV_EIGHT_PROPORTION_GROUP = "1/8 proportion";

    [InputParameter("Level count", 50, 1, 9999, 1, 0)]
    public int LevelCount;

    private bool showLabels;

    private readonly Dictionary<string, SquareLevelContainer> containersCache;
    private Font defaultFont;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSquareLevels.cs";

    public IndicatorSquareLevels()
    {
        this.Name = "Square levels";
        this.ShortName = this.Name;

        this.showLabels = true;
        this.LevelCount = 10;

        this.defaultFont = new Font("Verdana", 8, FontStyle.Regular);
        this.containersCache = new Dictionary<string, SquareLevelContainer>()
        {
            //
            ["Step033"] = new SquareLevelContainer(0.33d, ONE_DIV_THREE_PROPORTION_GROUP, "Use '0.33' step", new LineOptions()
            {
                Color = Color.Orange,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step066"] = new SquareLevelContainer(0.66d, ONE_DIV_THREE_PROPORTION_GROUP, "Use '0.66' step", new LineOptions()
            {
                Color = Color.DodgerBlue,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),

            //
            ["Step1"] = new SquareLevelContainer(1d, ONE_DIV_FOUR_PROPORTION_GROUP, "Use '1' step", new LineOptions()
            {
                Enabled = true,
                Color = Color.Orange,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step075"] = new SquareLevelContainer(0.75d, ONE_DIV_FOUR_PROPORTION_GROUP, "Use '0.75' step", new LineOptions()
            {
                Color = Color.DodgerBlue,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step05"] = new SquareLevelContainer(0.5d, ONE_DIV_FOUR_PROPORTION_GROUP, "Use '0.5' step", new LineOptions()
            {
                Color = Color.HotPink,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step025"] = new SquareLevelContainer(0.25d, ONE_DIV_FOUR_PROPORTION_GROUP, "Use '0.25' step", new LineOptions()
            {
                Color = Color.ForestGreen,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),

            //
            ["Step0125"] = new SquareLevelContainer(0.125d, ONE_DIV_EIGHT_PROPORTION_GROUP, "Use '0.125' step", new LineOptions()
            {
                Color = Color.Orange,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step0375"] = new SquareLevelContainer(0.375d, ONE_DIV_EIGHT_PROPORTION_GROUP, "Use '0.375' step", new LineOptions()
            {
                Color = Color.DodgerBlue,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
            ["Step0625"] = new SquareLevelContainer(0.625d, ONE_DIV_EIGHT_PROPORTION_GROUP, "Use '0.625' step", new LineOptions()
            {
                Color = Color.HotPink,
                Width = 1,
                LineStyle = LineStyle.Solid
            }),
        };
    }

    protected override void OnInit()
    {
        base.OnInit();

        var squareBasis = SquareBasis.Calculate(this.HistoricalData[0][PriceType.Close]);

        foreach (var c in this.containersCache.Values)
        {
            bool includeBasisLevel = c.Step == 1d;
            c.GenerateLevels(squareBasis, this.LevelCount, includeBasisLevel);
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        var gr = args.Graphics;
        gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        gr.SetClip(args.Rectangle);

        for (int i = 0; i <= this.LevelCount * 2; i++)
        {
            foreach (var c in this.containersCache.Values)
            {
                if (c.LineOptions.Enabled)
                    this.DrawLine(gr, i, c, args);
            }
        }

        gr.ResetClip();
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;

            var inputParametersSepar = settings.GetItemByName("Level count") is SettingItem si
                ? si.SeparatorGroup
                : new SettingItemSeparatorGroup("", 10);

            var separGroups = new Dictionary<string, SettingItemSeparatorGroup>();
            foreach (var pair in this.containersCache)
            {
                string settingName = pair.Key;
                var container = pair.Value;

                //
                if (!separGroups.ContainsKey(container.VisualGroup))
                    separGroups[container.VisualGroup] = new SettingItemSeparatorGroup(container.VisualGroup, -1100 + separGroups.Count);

                //
                settings.Add(new SettingItemLineOptions(settingName, container.LineOptions)
                {
                    Text = loc._(container.VisualName),
                    SeparatorGroup = separGroups[container.VisualGroup]
                });
            }

            settings.Add(new SettingItemBoolean("ShowLabels", this.showLabels, 50)
            {
                Text = loc._("Show labels"),
                SeparatorGroup = inputParametersSepar
            });
            settings.Add(new SettingItemFont("Font", this.defaultFont, 60)
            {
                Text = loc._("Label font"),
                SeparatorGroup = inputParametersSepar,
                Relation = new SettingItemRelationVisibility("ShowLabels", true)
            });

            return settings;
        }
        set
        {
            base.Settings = value;

            foreach (var item in value)
            {
                if (this.containersCache.ContainsKey(item.Name) && item is SettingItemLineOptions lineOptionsSI)
                    this.containersCache[item.Name].LineOptions = (LineOptions)lineOptionsSI.Value;
            }

            if (value.GetItemByName("Font") is SettingItemFont font)
                this.defaultFont = (Font)font.Value;

            if (value.GetItemByName("ShowLabels") is SettingItemBoolean showLabelsSI)
                this.showLabels = (bool)showLabelsSI.Value;
        }
    }

    protected override void OnClear()
    {
        base.OnClear();

        foreach (var c in this.containersCache.Values)
            c?.Dispose();
    }


    private void DrawLine(Graphics gr, int lineIndex, SquareLevelContainer container, PaintChartEventArgs args)
    {
        if (container.Levels.Count <= lineIndex)
            return;

        float pointY = (float)this.CurrentChart.MainWindow.CoordinatesConverter.GetChartY(container.Levels[lineIndex].PriceResult);

        if (pointY < args.Rectangle.Top || pointY > args.Rectangle.Bottom)
            return;

        gr.DrawLine(container.LinePen, 0, pointY, args.Rectangle.Right, pointY);

        if (this.showLabels)
        {
            var textSize = gr.MeasureString(container.Levels[lineIndex].VisualBasisValue, this.defaultFont);
            gr.DrawString(container.Levels[lineIndex].VisualBasisValue, this.defaultFont, container.LinePen.Brush, args.Rectangle.Right - textSize.Width - 2f, pointY - textSize.Height - container.LinePen.Width + 2);
        }
    }
}

internal class SquareBasis
{
    public double BasisValue { get; private set; }

    public double PriceResult
    {
        get => this.priceResult;
        set
        {
            this.priceResult = value;
            this.VisualBasisValue = GetFormattedBasisValue(value, this.BasisValue);
        }
    }
    private double priceResult;

    public string VisualBasisValue { get; private set; }

    private double price;
    private int precition;

    public static SquareBasis Calculate(double price)
    {
        var res = new SquareBasis()
        {
            price = price
        };

        int precition = CoreMath.GetValuePrecision((decimal)price);

        if (price >= 1)
        {
            double r = price;
            while (r > 1)
            {
                r /= 10;
                res.precition += 1;
            }
        }
        else
            res.precition = precition;

        double number = price * Math.Pow(10, precition);
        double number1 = Math.Sqrt(number);

        res.BasisValue = Math.Floor(number1);
        res.PriceResult = CalculatePrice(res.price, Math.Pow(res.BasisValue, 2), res.precition);

        return res;
    }


    public SquareBasis CalculateWithStep(double step)
    {
        double newBasis = this.BasisValue + step;
        double squareBasis = Math.Floor(Math.Pow(newBasis, 2));
        var res = new SquareBasis()
        {
            BasisValue = newBasis,
            price = this.price,
            precition = this.precition
        };

        res.PriceResult = CalculatePrice(res.price, squareBasis, res.precition);

        return res;
    }

    public override string ToString() => $"{this.BasisValue} | {this.PriceResult}";


    private static double CalculatePrice(double price, double basis, int precition)
    {
        int p = precition;
        if (price >= 1)
            p = basis.ToString().Length - precition;

        return basis / Math.Pow(10, p);
    }

    private static string GetFormattedBasisValue(double price, double basisValue) => $"{(decimal)price}({basisValue}\u00B2)";
}

internal class SquareLevelContainer : IDisposable
{
    public LineOptions LineOptions
    {
        get => this.lineOptions;
        set
        {
            this.lineOptions = value;

            this.LinePen = ProcessPen(this.LinePen, value);
        }
    }
    private LineOptions lineOptions;

    public Pen LinePen { get; private set; }
    public SquareBasis Basis { get; private set; }
    public double Step { get; private set; }
    public string VisualName { get; private set; }
    public string VisualGroup { get; private set; }
    public List<SquareBasis> Levels { get; private set; }

    public SquareLevelContainer(LineOptions lineOptions, double step)
    {
        this.LineOptions = lineOptions;
        this.Step = step;
        this.Levels = new List<SquareBasis>();
    }

    public SquareLevelContainer(double step, string visualGroup, string visualName, LineOptions lineOptions)
    {
        this.LineOptions = lineOptions;
        this.Step = step;
        this.VisualName = visualName;
        this.VisualGroup = visualGroup;
        this.Levels = new List<SquareBasis>();
    }

    internal void GenerateLevels(SquareBasis squareBasis, int levelCount, bool includeBasisLevel = true)
    {
        this.Levels.Clear();
        this.Basis = squareBasis;

        if (includeBasisLevel)
            this.Levels.Add(this.Basis);

        for (int i = 1; i <= levelCount; i++)
        {
            this.Levels.Insert(0, this.Basis.CalculateWithStep(i + this.Step - 1));
            this.Levels.Add(this.Basis.CalculateWithStep(-(i - this.Step)));
        }
    }

    public void Dispose() => this.Levels?.Clear();

    private static Pen ProcessPen(Pen pen, LineOptions lineOptions)
    {
        pen ??= new Pen(Color.Empty);

        pen.Color = lineOptions.Color;
        pen.Width = lineOptions.Width;

        try
        {
            switch (lineOptions.LineStyle)
            {
                case LineStyle.Solid:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    break;

                case LineStyle.Dot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    break;

                case LineStyle.Dash:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    break;

                case LineStyle.DashDot:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = (new float[] { 2, 4, 7, 4 });
                    break;

                case LineStyle.Histogramm:
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = (new float[] { 0.25F, 1 });
                    pen.Width = 4;
                    break;

                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Instance.Loggers.Log(ex);
        }

        return pen;
    }
}
