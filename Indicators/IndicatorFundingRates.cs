// Copyright QUANTOWER LLC. © 2017-2021. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace BarsDataIndicators;

public sealed class IndicatorFundingRates : Indicator
{
    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorFundingRates.cs";

    private bool showFundingLine = true;
    private LineOptions fundingLineOptions = new LineOptions()
    {
        Color = Color.OrangeRed,
        Width = 1,
        LineStyle = LineStyle.Solid,
        WithCheckBox = false
    };
    private Pen fundingPen;

    private bool showFundingLabel = true;
    private Format labelFormat = Format.DateTimeText;
    private string labelText = "Funding";
    private Font labelFont = new Font("Tahoma", 12);
    private Color labelColor = Color.White;
    private SolidBrush labelBrush;
    private Position labelPosition = Position.TopRight;
    private Orientation labelOrientation = Orientation.Horizontal;
    private StringFormat labelSF;

    public IndicatorFundingRates()
    {
        this.Name = "Funding Rates";
        this.Description = "";

        this.AddLineSeries("FundingRate", Color.Green, 2);
        this.SeparateWindow = true;

        this.fundingPen = new Pen(this.fundingLineOptions.Color)
        {
            Width = this.fundingLineOptions.Width,
            DashStyle = (DashStyle)this.fundingLineOptions.LineStyle
        };
        this.labelBrush = new SolidBrush(this.labelColor);
        this.UpdateLabelSF();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        double fundingRate = this.FundingRate();
        if (!double.IsNaN(fundingRate))
            fundingRate *= 100;

        this.SetValue(fundingRate);
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        var wnd = this.CurrentChart.Windows?[args.WindowIndex];
        if (wnd == null)
            return;

        DateTime fundingUtc = this.Symbol?.FundingTime ?? default;
        if (fundingUtc == default)
            return;

        DateTime leftBorderTime = wnd.CoordinatesConverter.GetTime(0);
        DateTime rightBorderTime = wnd.CoordinatesConverter.GetTime(wnd.ClientRectangle.Width);

        if (fundingUtc <= leftBorderTime || fundingUtc >= rightBorderTime)
            return;

        float x = (float)wnd.CoordinatesConverter.GetChartX(fundingUtc);

        var g = args.Graphics;
        var savedClip = g.ClipBounds;
        try
        {
            g.SetClip(args.Rectangle);

            if (this.showFundingLine && this.fundingPen != null)
                g.DrawLine(this.fundingPen, x, args.Rectangle.Top, x, args.Rectangle.Bottom);

            if (this.showFundingLabel)
            {
                var nowUtc = Core.Instance.TimeUtils.DateTimeUtcNow;
                var left = fundingUtc - nowUtc;
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;

                var tz = this.CurrentChart.CurrentTimeZone;
                var fundingLocal = Core.Instance.TimeUtils.ConvertFromUTCToTimeZone(fundingUtc, tz);

                string timerPart = $"{left:hh\\:mm\\:ss}";
                string text = this.labelFormat switch
                {
                    Format.DateTime => $"{timerPart} ({fundingLocal:HH:mm})",
                    Format.Text => $"{timerPart} {this.labelText}".Trim(),
                    Format.DateTimeText => $"{timerPart} ({fundingLocal:HH:mm}) {this.labelText}".Trim(),
                    _ => $"{timerPart} ({fundingLocal:HH:mm})"
                };

                int bottomY = wnd.ClientRectangle.Height;
                float labelY = this.labelPosition switch
                {
                    Position.TopLeft or Position.TopRight => args.Rectangle.Top+this.labelFont.Height/2,
                    Position.BottomLeft or Position.BottomRight => args.Rectangle.Bottom,
                    Position.MiddleLeft or Position.MiddleRight => args.Rectangle.Top + (args.Rectangle.Height / 2f),
                    _ => args.Rectangle.Top
                };

                g.DrawString(text, this.labelFont, this.labelBrush, new PointF(x, labelY), this.labelSF);
            }
        }
        finally
        {
            g.SetClip(savedClip);
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            var grp = new SettingItemSeparatorGroup("Funding Settings", 0);

            settings.Add(new SettingItemBooleanSwitcher("ShowFundingLine", this.showFundingLine)
            {
                Text = "Show Funding Line",
                SortIndex = 0,
                SeparatorGroup = grp,
            });
            var lineVis = new SettingItemRelationVisibility("ShowFundingLine", true);
            settings.Add(new SettingItemLineOptions("FundingLineStyle", this.fundingLineOptions)
            {
                Text = "Funding Line Style",
                SortIndex = 0,
                SeparatorGroup = grp,
                Relation = lineVis,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true
            });

            settings.Add(new SettingItemBoolean("ShowFundingLabel", this.showFundingLabel)
            {
                Text = "Show Label",
                SortIndex = 1,
                SeparatorGroup = grp,
                Relation = lineVis
            });
            var labelVis = new SettingItemRelationVisibility("ShowFundingLabel", true);

            settings.Add(new SettingItemSelectorLocalized(
                "FundingLabelFormat",
                this.labelFormat,
                new System.Collections.Generic.List<SelectItem>
                {
                        new SelectItem("DateTime",     Format.DateTime),
                        new SelectItem("Text",         Format.Text),
                        new SelectItem("DateTime+Text",Format.DateTimeText)
                })
            {
                Text = "Label Format",
                SortIndex = 2,
                SeparatorGroup = grp,
                Relation = labelVis
            });

            var labelTextNeeded = new SettingItemRelationVisibility("FundingLabelFormat",
                new SelectItem[]
                {
                        new SelectItem("Text",          Format.Text),
                        new SelectItem("DateTime+Text", Format.DateTimeText),
                });
            settings.Add(new SettingItemTextArea("FundingLabelText", this.labelText)
            {
                Text = "Custom Text",
                SortIndex = 3,
                SeparatorGroup = grp,
                Relation = labelTextNeeded
            });

            settings.Add(new SettingItemFont("FundingLabelFont", this.labelFont)
            {
                Text = "Font",
                SortIndex = 4,
                SeparatorGroup = grp,
                Relation = labelVis
            });
            settings.Add(new SettingItemColor("FundingLabelColor", this.labelColor)
            {
                Text = "Font Color",
                SortIndex = 5,
                SeparatorGroup = grp,
                Relation = labelVis
            });
            settings.Add(new SettingItemSelectorLocalized(
                "FundingLabelPosition",
                this.labelPosition,
                new System.Collections.Generic.List<SelectItem>
                {
                        new SelectItem("Top Right",     Position.TopRight),
                        new SelectItem("Top Left",      Position.TopLeft),
                        new SelectItem("Bottom Right",  Position.BottomRight),
                        new SelectItem("Bottom Left",   Position.BottomLeft),
                        new SelectItem("Middle Left",   Position.MiddleLeft),
                        new SelectItem("Middle Right",  Position.MiddleRight),
                })
            {
                Text = "Label Position",
                SortIndex = 6,
                SeparatorGroup = grp,
                Relation = labelVis
            });
            settings.Add(new SettingItemSelectorLocalized(
                "FundingLabelOrientation",
                this.labelOrientation,
                new System.Collections.Generic.List<SelectItem>
                {
                        new SelectItem("Horizontal", Orientation.Horizontal),
                        new SelectItem("Vertical",   Orientation.Vertical)
                })
            {
                Text = "Label Orientation",
                SortIndex = 7,
                SeparatorGroup = grp,
                Relation = labelVis
            });

            return settings;
        }
        set
        {
            base.Settings = value;

            if (value.TryGetValue("ShowFundingLine", out bool _showLine))
                this.showFundingLine = _showLine;

            if (value.TryGetValue("FundingLineStyle", out LineOptions _lo))
            {
                this.fundingLineOptions = _lo;

                if (this.fundingPen == null)
                    this.fundingPen = new Pen(_lo.Color);

                this.fundingPen.Width = _lo.Width;
                this.fundingPen.Color = _lo.Color;
                this.fundingPen.DashStyle = (DashStyle)_lo.LineStyle;
            }

            if (value.TryGetValue("ShowFundingLabel", out bool _showLbl))
                this.showFundingLabel = _showLbl;

            if (value.TryGetValue("FundingLabelFormat", out Format _fmt))
                this.labelFormat = _fmt;

            if (value.TryGetValue("FundingLabelText", out string _txt))
                this.labelText = _txt;

            if (value.TryGetValue("FundingLabelFont", out Font _font))
                this.labelFont = _font;

            if (value.TryGetValue("FundingLabelColor", out Color _col))
            {
                this.labelColor = _col;
                this.labelBrush?.Dispose();
                this.labelBrush = new SolidBrush(_col);
            }

            bool needUpdateSF = false;
            if (value.TryGetValue("FundingLabelPosition", out Position _pos))
            {
                this.labelPosition = _pos;
                needUpdateSF = true;
            }
            if (value.TryGetValue("FundingLabelOrientation", out Orientation _ori))
            {
                this.labelOrientation = _ori;
                needUpdateSF = true;
            }
            if (needUpdateSF)
                this.UpdateLabelSF();
        }
    }

    private void UpdateLabelSF()
    {
        var sf = new StringFormat();
        if (this.labelOrientation == Orientation.Vertical)
            sf.FormatFlags |= StringFormatFlags.DirectionVertical;

        switch (this.labelPosition)
        {
            case Position.TopRight:
                sf.Alignment = StringAlignment.Near;
                break;

            case Position.TopLeft:

                if (this.labelOrientation == Orientation.Vertical)
                    sf.LineAlignment = StringAlignment.Far;
                else
                    sf.Alignment = StringAlignment.Far;
                break;

            case Position.BottomRight:
                if (this.labelOrientation == Orientation.Vertical)
                    sf.Alignment = StringAlignment.Far;
                else
                    sf.LineAlignment = StringAlignment.Far;
                break;

            case Position.BottomLeft:
                if (this.labelOrientation == Orientation.Vertical)
                {
                    sf.LineAlignment = StringAlignment.Far;
                    sf.Alignment =      StringAlignment.Far;
                }
                else
                {
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment =  StringAlignment.Far;
                }
                break;

            case Position.MiddleLeft:
                if (this.labelOrientation == Orientation.Vertical)
                {
                    sf.LineAlignment = StringAlignment.Far;
                    sf.Alignment = StringAlignment.Center;
                }
                else
                {
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment =  StringAlignment.Center;
                }
                break;

            case Position.MiddleRight:
                if (this.labelOrientation == Orientation.Vertical)
                    sf.Alignment = StringAlignment.Center;
                else
                    sf.LineAlignment = StringAlignment.Center;
                break;

            default:
                sf.Alignment = StringAlignment.Near;
                break;
        }

        this.labelSF = sf;
    }
}

    public enum Format
    {
        DateTime,
        DateTimeText,
        Text
    }
    public enum Position
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft,
        MiddleLeft,
        MiddleRight,
    }
    public enum Orientation
    {
        Horizontal,
        Vertical
    }

