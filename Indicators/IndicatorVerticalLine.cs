// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Utils;

namespace OtherIndicators;

public class IndicatorVerticalLine : Indicator
{
    private TimeLine[] lines = new TimeLine[5] { new TimeLine(Color.Green, "First Line", 1), new TimeLine(Color.Red, "Second Line", 2), new TimeLine(Color.GreenYellow, "Third Line", 3), new TimeLine(Color.Blue, "Fourth Line", 4), new TimeLine(Color.Cyan, "Fifth Line", 5) };
    private bool allWeekAvailable = true;
    private Dictionary<DayOfWeek, bool> awailableDays = new Dictionary<DayOfWeek, bool>()
    {
        {DayOfWeek.Monday, true},
        {DayOfWeek.Tuesday, true},
        {DayOfWeek.Wednesday, true},
        {DayOfWeek.Thursday, true},
        {DayOfWeek.Friday, true},
        {DayOfWeek.Saturday, true},
        {DayOfWeek.Sunday, true},
    };

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
            int bottomY = mainWindow.ClientRectangle.Height;

            if (this.currentPeriod.Duration.Days >= 1)
                return;

            // We check one extra calendar day on each side because a periodic line
            // can be visible even when its main (anchor) line is outside the screen.
            DateTime firstDate = leftBorderTime.Date.AddDays(-1);
            DateTime lastDate = rightBorderTime.Date.AddDays(1);

            for (int i = 0; i < this.lines.Length; i++)
            {
                TimeLine line = this.lines[i];
                if (!line.LineVisibility)
                    continue;

                int labelY = 0;
                if (line.LabelPosition == Position.MiddleLeft || line.LabelPosition == Position.MiddleRight)
                    labelY = bottomY / 2;
                else if (line.LabelPosition == Position.BottomLeft || line.LabelPosition == Position.BottomRight)
                    labelY = bottomY;

                for (DateTime date = firstDate; date <= lastDate; date = date.AddDays(1))
                {
                    DateTime anchorTime = new DateTime(
                        date.Year,
                        date.Month,
                        date.Day,
                        line.Time.Hour,
                        line.Time.Minute,
                        line.Time.Second,
                        DateTimeKind.Utc);

                    bool drawCurrentDay = this.allWeekAvailable || this.awailableDays[anchorTime.DayOfWeek];
                    if (!drawCurrentDay)
                        continue;

                    // Main daily line.
                    if (anchorTime > leftBorderTime && anchorTime < rightBorderTime)
                        this.DrawMainLine(graphics, mainWindow, line, anchorTime, labelY, bottomY);

                    // Additional lines are built in both directions from the main line.
                    // The periodic grid is restarted for every enabled calendar day so
                    // that day-of-week filtering remains predictable.
                    if (line.DrawPeriodicLine)
                    {
                        this.DrawPeriodicLines(
                            graphics,
                            mainWindow,
                            line,
                            anchorTime,
                            leftBorderTime,
                            rightBorderTime,
                            bottomY);
                    }
                }
            }
        }
        finally
        {
            graphics.SetClip(prevClipRectangle);
        }
    }

    private void DrawMainLine(
        Graphics graphics,
        IChartWindow mainWindow,
        TimeLine line,
        DateTime lineTime,
        int labelY,
        int bottomY)
    {
        int x = (int)mainWindow.CoordinatesConverter.GetChartX(lineTime);
        graphics.DrawLine(line.linePen, x, 0, x, bottomY);

        if (!line.LabelVisibility)
            return;

        string labelText = string.Empty;

        if (line.textFormat == Format.DateTime || line.textFormat == Format.DateTimeText)
            labelText = Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(lineTime).ToString();

        if (line.textFormat == Format.DateTimeText || line.textFormat == Format.Text)
            labelText = labelText + " " + line.labelText;

        graphics.DrawString(
            labelText,
            line.labelFont,
            line.labelBrush,
            new PointF(x, labelY),
            line.lineSF);
    }

    private void DrawPeriodicLines(
        Graphics graphics,
        IChartWindow mainWindow,
        TimeLine line,
        DateTime anchorTime,
        DateTime leftBorderTime,
        DateTime rightBorderTime,
        int bottomY)
    {
        TimeSpan step = line.PeriodicLinePeriod.Duration;
        if (step <= TimeSpan.Zero)
            return;

        // Periodic lines belong to the same calendar day as their anchor.
        // The next day receives its own periodic grid from its own main line.
        DateTime dayStart = new DateTime(
            anchorTime.Year,
            anchorTime.Month,
            anchorTime.Day,
            0,
            0,
            0,
            DateTimeKind.Utc);
        DateTime dayEnd = dayStart.AddDays(1);

        DateTime visibleStart = leftBorderTime > dayStart ? leftBorderTime : dayStart;
        DateTime visibleEnd = rightBorderTime < dayEnd ? rightBorderTime : dayEnd;

        if (visibleEnd <= visibleStart)
            return;

        long stepTicks = step.Ticks;
        long firstOffset = (long)Math.Ceiling((visibleStart.Ticks - anchorTime.Ticks) / (double)stepTicks);
        long lastOffset = (long)Math.Floor((visibleEnd.Ticks - anchorTime.Ticks) / (double)stepTicks);

        for (long offset = firstOffset; offset <= lastOffset; offset++)
        {
            // Offset zero is the main line and is drawn separately (with its label).
            if (offset == 0)
                continue;

            DateTime periodicTime = anchorTime.AddTicks(offset * stepTicks);

            if (periodicTime <= leftBorderTime || periodicTime >= rightBorderTime ||
                periodicTime < dayStart || periodicTime >= dayEnd)
            {
                continue;
            }

            int x = (int)mainWindow.CoordinatesConverter.GetChartX(periodicTime);
            graphics.DrawLine(line.linePen, x, 0, x, bottomY);
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            SettingItemSeparatorGroup availableDaysGroup = new SettingItemSeparatorGroup("Days Settings", 0);
            settings.Add(new SettingItemBooleanSwitcher("allWeekAvailable", this.allWeekAvailable)
            {
                Text = "Draw Every Day Line",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
            });
            SettingItemRelationVisibility visibleRelationNotAllDays = new SettingItemRelationVisibility("allWeekAvailable", false);
            settings.Add(new SettingItemBoolean("Monday", this.awailableDays[DayOfWeek.Monday])
            {
                Text = "Monday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Tuesday", this.awailableDays[DayOfWeek.Tuesday])
            {
                Text = "Tuesday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Wednesday", this.awailableDays[DayOfWeek.Wednesday])
            {
                Text = "Wednesday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Thursday", this.awailableDays[DayOfWeek.Thursday])
            {
                Text = "Thursday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Friday", this.awailableDays[DayOfWeek.Friday])
            {
                Text = "Friday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Saturday", this.awailableDays[DayOfWeek.Saturday])
            {
                Text = "Saturday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            settings.Add(new SettingItemBoolean("Sunday", this.awailableDays[DayOfWeek.Sunday])
            {
                Text = "Sunday",
                SortIndex = 0,
                SeparatorGroup = availableDaysGroup,
                Relation = visibleRelationNotAllDays
            });
            for (int i = 0; i < lines.Length; i++)
                settings.Add(new SettingItemGroup(lines[i].LineName, lines[i].Settings));

            return settings;
        }
        set
        {
            base.Settings = value;
            if (value.TryGetValue("allWeekAvailable", out bool allWeekAvailable))
                this.allWeekAvailable = allWeekAvailable;
            if (value.TryGetValue("Monday", out bool Monday))
                this.awailableDays[DayOfWeek.Monday] = Monday;
            if (value.TryGetValue("Tuesday", out bool Tuesday))
                this.awailableDays[DayOfWeek.Tuesday] = Tuesday;
            if (value.TryGetValue("Wednesday", out bool Wednesday))
                this.awailableDays[DayOfWeek.Wednesday] = Wednesday;
            if (value.TryGetValue("Thursday", out bool Thursday))
                this.awailableDays[DayOfWeek.Thursday] = Thursday;
            if (value.TryGetValue("Friday", out bool Friday))
                this.awailableDays[DayOfWeek.Friday] = Friday;
            if (value.TryGetValue("Saturday", out bool Saturday))
                this.awailableDays[DayOfWeek.Saturday] = Saturday;
            if (value.TryGetValue("Sunday", out bool Sunday))
                this.awailableDays[DayOfWeek.Sunday] = Sunday;
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
    public bool DrawPeriodicLine { get; set; }
    public Period PeriodicLinePeriod { get; set; }
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
            this.labelPosition = value;

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
            this.labelOrientation = value;

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
        this.DrawPeriodicLine = false;
        this.PeriodicLinePeriod = new Period(BasePeriod.Hour, 1);
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

            string relationName = this.LineName + "LineVisibility";
            string relationNameLabel = this.LineName + "ShowLabel";
            string relationNameCustomTextFormat = this.LineName + "CustomTextFormat";
            string periodicRelationName = this.LineName + "DrawPeriodicLine";
            string periodicPeriodName = this.LineName + "PeriodicLinePeriod";

            var separatorGroup1 = new SettingItemSeparatorGroup(this.LineName, this.lineSortIndex)
            {
                ItemsEnabilitySettingName = this.LineName + "LineVisibility",
            };

            settings.Add(new SettingItemBooleanSwitcher(relationName, this.LineVisibility)
            {
                Text = "Line Visibility",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
            });
            SettingItemRelationEnability visibleRelation = new SettingItemRelationEnability(relationName, true);
            SettingItemRelationVisibility visibleRelationLabel = new SettingItemRelationVisibility(relationNameLabel, true);
            settings.Add(new SettingItemDateTime(this.LineName + "LineTime", this.Time)
            {
                Text = "Line Time",
                SortIndex = lineSortIndex,
                Format = DatePickerFormat.Time,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation,
            });
            settings.Add(new SettingItemLineOptions(this.LineName + "LineStyle", this.lineOptions)
            {
                Text = "Line Style",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation,
                ExcludedStyles = new LineStyle[] { LineStyle.Histogramm, LineStyle.Points },
                UseEnabilityToggler = false,
            });
            settings.Add(new SettingItemBoolean(periodicRelationName, this.DrawPeriodicLine)
            {
                Text = "Draw periodic line",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelation
            });

            var periodicVisibleRelation = new SettingItemMultipleRelation(
                visibleRelation,
                new SettingItemRelationVisibility(periodicRelationName, true));

            settings.Add(new SettingItemPeriod(periodicPeriodName, this.PeriodicLinePeriod)
            {
                Text = "Draw line every",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = periodicVisibleRelation,
                MultiplierMinimum = 1,
                MultiplierMaximum = 9999,
                ExcludedPeriods = new[]
                {
                    BasePeriod.Tick,
                    BasePeriod.Day,
                    BasePeriod.Week,
                    BasePeriod.Month,
                    BasePeriod.Year
                }
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
                Relation = visibleRelationLabel,

            });
            SettingItemRelationVisibility visibleRelationCustomText = new SettingItemRelationVisibility(relationNameCustomTextFormat, new SelectItem[2] { new SelectItem("Text", Format.Text), new SelectItem("DateTime+Text", Format.DateTimeText) });
            settings.Add(new SettingItemTextArea(this.LineName + "LabelText", this.labelText)
            {
                Text = "Custom text",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationCustomText,
            });
            settings.Add(new SettingItemFont(this.LineName + "Font", this.labelFont)
            {
                Text = "Font",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemColor(this.LineName + "FontColor", this.labelColor)
            {
                Text = "Font Color",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemSelectorLocalized(this.LineName + "LabelPosition", this.LabelPosition, new List<SelectItem> { new SelectItem("Top Right", Position.TopRight), new SelectItem("Top Left", Position.TopLeft), new SelectItem("Bottom Right", Position.BottomRight), new SelectItem("Bottom Left", Position.BottomLeft), new SelectItem("Middle Left", Position.MiddleLeft), new SelectItem("Middle Right", Position.MiddleRight) })
            {
                Text = "Label position",
                SortIndex = lineSortIndex,
                SeparatorGroup = separatorGroup1,
                Relation = visibleRelationLabel
            });
            settings.Add(new SettingItemSelectorLocalized(this.LineName + "LabelOrientation", this.LabelOrientation, new List<SelectItem> { new SelectItem("Horizontal", Orientation.Horizontal), new SelectItem("Vertical", Orientation.Vertical) })
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
            if (settings.TryGetValue(this.LineName + "LineTime", out DateTime SessionFirstTime))
                this.Time = SessionFirstTime;
            if (settings.TryGetValue(this.LineName + "LineStyle", out LineOptions lineStyle))
            {
                this.lineOptions = lineStyle;

                this.linePen.Width = lineStyle.Width;
                this.linePen.Color = lineStyle.Color;
                this.linePen.DashStyle = (DashStyle)lineStyle.LineStyle;
            }
            if (settings.TryGetValue(this.LineName + "DrawPeriodicLine", out bool drawPeriodicLine))
                this.DrawPeriodicLine = drawPeriodicLine;
            if (settings.TryGetValue(this.LineName + "PeriodicLinePeriod", out Period periodicLinePeriod))
                this.PeriodicLinePeriod = periodicLinePeriod;
            if (settings.TryGetValue(this.LineName + "ShowLabel", out bool LabelVisibility))
                this.LabelVisibility = LabelVisibility;
            if (settings.TryGetValue(this.LineName + "CustomTextFormat", out Format textFormat))
                this.textFormat = textFormat;
            if (settings.TryGetValue(this.LineName + "LabelText", out string customText))
                this.labelText = customText;
            if (settings.TryGetValue(this.LineName + "Font", out Font labelFont))
                this.labelFont = labelFont;
            if (settings.TryGetValue(this.LineName + "FontColor", out Color labelColor))
            {
                this.labelColor = labelColor;
                this.labelBrush.Color = labelColor;
            }
            if (settings.TryGetValue(this.LineName + "LabelPosition", out Position labelPosition))
                this.LabelPosition = labelPosition;
            if (settings.TryGetValue(this.LineName + "LabelOrientation", out Orientation labelOrientation))
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