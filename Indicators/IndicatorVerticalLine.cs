// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace ChanneIsIndicators;
public class IndicatorVerticalLine : Indicator
{
    private TimeLine[] lines = new TimeLine[5] { new TimeLine(Color.Green, "First Line", 1), new TimeLine(Color.Red, "Second Line", 2), new TimeLine(Color.GreenYellow, "Third Line", 3), new TimeLine(Color.Blue, "Fourth Line", 4), new TimeLine(Color.Cyan, "Fifth Line", 5) };

    private Period currentPeriod;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorVerticalLine.cs";

    public IndicatorVerticalLine()
        : base()
    {
        Name = "Vertical Lines";
        SeparateWindow = false;
    }

    protected override void OnInit()
    {
        this.currentPeriod = this.HistoricalData.Aggregation.GetPeriod;
    }

    protected override void OnUpdate(UpdateArgs args)
    {
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        base.OnPaintChart(args);

        if (this.CurrentChart == null)
            return;

        Graphics graphics = args.Graphics;
        RectangleF prevClipRectangle = graphics.ClipBounds;
        graphics.SetClip(args.Rectangle);

        try
        {
            var mainWindow = this.CurrentChart.MainWindow;
            DateTime leftBorderTime = mainWindow.CoordinatesConverter.GetTime(0);
            DateTime rightBorderTime = mainWindow.CoordinatesConverter.GetTime(mainWindow.ClientRectangle.Width);
            TimeSpan bordersSpan = rightBorderTime - leftBorderTime;
            int bottomY = this.CurrentChart.MainWindow.ClientRectangle.Height;
            int daysSpan = bordersSpan.Days;
            if (daysSpan <= 0)
                daysSpan = 1;
            if (currentPeriod.Duration.Days < 1)
                for (int i = 0; i < lines.Length; i++)
                {
                    DateTime lineTime = lines[i].Time;

                    DateTime currentLineTime = new DateTime(leftBorderTime.Year, leftBorderTime.Month, leftBorderTime.Day, lineTime.Hour, lineTime.Minute, lineTime.Second,  DateTimeKind.Utc);

                    if (currentLineTime < leftBorderTime)
                        currentLineTime = currentLineTime.AddDays(1);
                    if (currentLineTime > rightBorderTime)
                        currentLineTime = currentLineTime.AddDays(-1);

                    // 
                    int labelY = 0;
                    if (lines[i].LabelPosition == Position.MiddleLeft || lines[i].LabelPosition == Position.MiddleRight)
                        labelY=bottomY/2;
                    else if (lines[i].LabelPosition == Position.BottomLeft || lines[i].LabelPosition == Position.BottomRight)
                        labelY=bottomY;

                    if (lines[i].LineVisibility && currentLineTime > leftBorderTime && currentLineTime < rightBorderTime)
                    {                 
                        for (int j = 0; j <= daysSpan; j++)
                        {
                            int topX = (int)mainWindow.CoordinatesConverter.GetChartX(currentLineTime);          
                            graphics.DrawLine(lines[i].linePen, topX, 0, topX, bottomY);

                            if (lines[i].LabelVisibility)
                            {
                                //
                                string labelText = "";
                                if (lines[i].textFormat == Format.DateTime || lines[i].textFormat == Format.DateTimeText)
                                    labelText = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(currentLineTime).ToString();
                                if (lines[i].textFormat == Format.DateTimeText || lines[i].textFormat == Format.Text)
                                    labelText = labelText + " " + lines[i].labelText;

                                //
                                graphics.DrawString(labelText, lines[i].labelFont, lines[i].labelBrush, new PointF(topX, labelY), lines[i].lineSF);
                            }
                            currentLineTime = currentLineTime.AddDays(1);
                        }
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
            for (int i = 0; i < lines.Length; i++)
                settings.Add(new SettingItemGroup(lines[i].LineName, lines[i].Settings));

            return settings;
        }
        set
        {
            base.Settings = value;
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i].Settings = value;
            }
        }
    }
}

public class TimeLine : ICustomizable
{
    public StringFormat lineSF;

    public DateTime Time { get; set; }
    public bool LineVisibility { get; set; }
    public string LineName { get; set; }
    private int lineSortIndex { get; set; }
    public Pen linePen { get; set; }
    public LineOptions lineOptions { get; set; }
    public bool LabelVisibility { get; set; }
    public Format textFormat { get; set; }
    public string labelText { get; set; }
    public Font labelFont { get; set; }
    public Color labelColor { get; set; }
    public Position LabelPosition
    {
        get => this.labelPosition;
        set
        {
            this.labelPosition=value;

            this.UpdateLineSF();
        }
    }
    private Position labelPosition;
    public SolidBrush labelBrush { get; set; }
    public Orientation LabelOrientation
    {
        get => this.labelOrientation;
        set
        {
            this.labelOrientation=value;

            this.UpdateLineSF();
        }
    }
    private Orientation labelOrientation;
    public TimeLine(Color color, string name = "Line X", int sortingIndex = 20)
    {
        this.LineName = name;
        this.lineSortIndex = sortingIndex;
        this.Time = new DateTime();
        this.LineVisibility = false;
        this.linePen = new Pen(color);
        this.lineOptions = new LineOptions();
        this.lineOptions.Color = color;
        this.lineOptions.WithCheckBox = false;
        this.textFormat = Format.DateTime;
        this.labelFont = new Font("Arial", 8);
        this.labelColor = color;
        this.LabelPosition = Position.TopRight;
        this.LabelOrientation = Orientation.Horizontal;
        this.labelBrush = new SolidBrush(color);
        this.LabelVisibility = false;
    }
    public IList<SettingItem> Settings
    {
        get
        {
            var settings = new List<SettingItem>();
            SettingItemSeparatorGroup separatorGroup1 = new SettingItemSeparatorGroup(LineName, lineSortIndex);

            string relationName = this.LineName + "LineVisibility";
            string relationNameLabel = this.LineName + "ShowLabel";
            string relationNameCustomTextFormat = this.LineName + "CustomTextFormat";
            settings.Add(new SettingItemBoolean(relationName, this.LineVisibility)
            {
                Text = "Line Visibility",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
            });
            SettingItemRelationVisibility visibleRelation = new SettingItemRelationVisibility(relationName, true);
            SettingItemRelationVisibility visibleRelationLabel = new SettingItemRelationVisibility(relationNameLabel, true);
            settings.Add(new SettingItemDateTime("LineTime", this.Time)
            {
                Text = "Line Time",
                SortIndex = lineSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });
            settings.Add(new SettingItemLineOptions("LineStyle", this.lineOptions)
            {
                Text = "Line Style",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = true
            });
            settings.Add(new SettingItemBoolean(relationNameLabel, this.LabelVisibility)
            {
                Text = "Show Label",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation,
            });
            settings.Add(new SettingItemSelectorLocalized(relationNameCustomTextFormat, this.textFormat, new List<SelectItem> { new SelectItem("DateTime", Format.DateTime), new SelectItem("Text", Format.Text), new SelectItem("DateTime+Text", Format.DateTimeText) })
            {
                Text = "Label format",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            SettingItemRelationVisibility visibleRelationCustomText = new SettingItemRelationVisibility(relationNameCustomTextFormat, new SelectItem[2] { new SelectItem("Text", Format.Text), new SelectItem("DateTime+Text", Format.DateTimeText) });
            settings.Add(new SettingItemTextArea("LabelText", this.labelText)
            {
                Text = "Custom text",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationCustomText,
            });
            settings.Add(new SettingItemFont("Font", this.labelFont)
            {
                Text = "Font",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemColor("FontColor", this.labelColor)
            {
                Text = "Font Color",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemSelectorLocalized("LabelPosition", this.LabelPosition, new List<SelectItem> { new SelectItem("Top Right", Position.TopRight), new SelectItem("Top Left", Position.TopLeft), new SelectItem("Bottom Right", Position.BottomRight), new SelectItem("Bottom Left", Position.BottomLeft), new SelectItem("Middle Left", Position.MiddleLeft), new SelectItem("Middle Right", Position.MiddleRight) })
            {
                Text = "Label position",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemSelectorLocalized("LabelOrientation", this.LabelOrientation, new List<SelectItem> { new SelectItem("Horizontal", Orientation.Horizontal), new SelectItem("Vertical", Orientation.Vertical) })
            {
                Text = "Label orientation",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            return settings;
        }
        set
        {
            List<SettingItem> settings = new List<SettingItem>();
            if (value.TryGetValue(LineName, out List<SettingItem> inputSettings))
                settings = inputSettings;
            if (settings.TryGetValue(LineName + "LineVisibility", out bool SessionVisibility))
                this.LineVisibility = SessionVisibility;
            if (settings.TryGetValue("LineTime", out DateTime SessionFirstTime))
                this.Time = SessionFirstTime;
            if (settings.TryGetValue("LineStyle", out LineOptions lineStyle))
            {
                this.lineOptions = lineStyle;

                this.linePen.Width = lineStyle.Width;
                this.linePen.Color = lineStyle.Color;
                this.linePen.DashStyle = (DashStyle)lineStyle.LineStyle;
            }
            if (settings.TryGetValue(this.LineName + "ShowLabel", out bool LabelVisibility))
                this.LabelVisibility = LabelVisibility;
            if (settings.TryGetValue(this.LineName + "CustomTextFormat", out Format textFormat))
                this.textFormat = textFormat;
            if (settings.TryGetValue("LabelText", out string customText))
                this.labelText = customText;
            if (settings.TryGetValue("Font", out Font labelFont))
                this.labelFont = labelFont;
            if (settings.TryGetValue("FontColor", out Color labelColor))
            {
                this.labelColor = labelColor;
                this.labelBrush.Color = labelColor;
            }
            if (settings.TryGetValue("LabelPosition", out Position labelPosition))
                this.LabelPosition = labelPosition;
            if (settings.TryGetValue("LabelOrientation", out Orientation labelOrientation))
                this.LabelOrientation = labelOrientation;
        }
    }

    private void UpdateLineSF()
    {
        this.lineSF = new StringFormat();
        if (this.LabelOrientation == Orientation.Vertical)
            this.lineSF.FormatFlags |= StringFormatFlags.DirectionVertical;

        switch (this.LabelPosition)
        {
            case Position.TopRight:
                this.lineSF.Alignment = StringAlignment.Near;
                break;
            case Position.TopLeft:
                if (this.LabelOrientation == Orientation.Vertical)
                    this.lineSF.LineAlignment = StringAlignment.Far;
                else
                    this.lineSF.Alignment = StringAlignment.Far;
                break;
            case Position.BottomRight:
                if (this.LabelOrientation == Orientation.Vertical)
                    this.lineSF.Alignment = StringAlignment.Far;
                else
                    this.lineSF.LineAlignment = StringAlignment.Far;
                break;
            case Position.BottomLeft:
                if (this.LabelOrientation == Orientation.Vertical)
                {
                    this.lineSF.LineAlignment = StringAlignment.Far;
                    this.lineSF.Alignment = StringAlignment.Far;
                }
                else
                {
                    this.lineSF.Alignment = StringAlignment.Far;
                    this.lineSF.LineAlignment = StringAlignment.Far;
                }
                break;
            case Position.MiddleLeft:
                if (this.LabelOrientation == Orientation.Vertical)
                {
                    this.lineSF.LineAlignment = StringAlignment.Far;
                    this.lineSF.Alignment = StringAlignment.Center;
                }
                else
                {
                    this.lineSF.Alignment = StringAlignment.Far;
                    this.lineSF.LineAlignment = StringAlignment.Center;
                }
                break;
            case Position.MiddleRight:
                if (this.LabelOrientation == Orientation.Vertical)
                    this.lineSF.Alignment = StringAlignment.Center;
                else
                    this.lineSF.LineAlignment = StringAlignment.Center;
                break;
            default:
                this.lineSF.Alignment = StringAlignment.Near;
                break;
        }
    }
}

#region Utils

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

#endregion
