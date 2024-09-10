// Copyright QUANTOWER LLC. © 2017-2024. All rights reserved.

using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace VolumeIndicators;

public class IndicatorContador : Indicator
{
    // consts
    public const string ERROR_MESSAGE = "Tick Counter only works on bars build with a set number of ticks";
    public const string TICKS_REMAINING_PREFIX = "Ticks Remaining = ";
    public const string TICK_COUNTER_PREFIX = "Ticks Count = ";
    public const string COUNTDOWN = "Countdown";
    public const string SHOW_PERCENTAGES = "Show percentages";

    // input parameters
    [InputParameter(COUNTDOWN, 10)]
    public bool IsCountdown = true;
    [InputParameter(SHOW_PERCENTAGES, 10)]
    public bool ShowPercentages = true;
    [InputParameter("Font color", 20)]
    public Color FontColor
    {
        get => this.fontColor;
        set
        {
            this.fontColor = value;
            this.fontBrush = new SolidBrush(value);
        }
    }
    private Color fontColor;
    private SolidBrush fontBrush;

    public override string ShortName
    {
        get
        {
            string parameters = this.IsCountdown ? COUNTDOWN : string.Empty;

            if (this.ShowPercentages)
            {
                if (this.IsCountdown)
                    parameters += ": ";

                parameters += SHOW_PERCENTAGES;
            }

            return $"{this.Name} ({parameters})";
        }
    }

    private readonly Font font;
    private bool isCorrectTimeFrame = false;
    private string calculatedMessage;
    private int messageMargin = 0;
    private string prefix;
    private readonly StringFormat farFarSF;

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorContador.cs";

    public IndicatorContador()
    {
        this.Name = "Contador";
        this.font = new Font("Verdana", 10, FontStyle.Regular, GraphicsUnit.Point);
        this.FontColor = Color.Orange;
        this.farFarSF = new StringFormat()
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Far
        };
    }

    protected override void OnInit()
    {
        this.calculatedMessage = string.Empty;
        this.isCorrectTimeFrame = this.HistoricalData.Aggregation.GetPeriod.BasePeriod == BasePeriod.Tick;
        this.messageMargin = this.isCorrectTimeFrame
            ? 30
            : 70;

        this.prefix = this.IsCountdown
            ? TICKS_REMAINING_PREFIX
            : TICK_COUNTER_PREFIX;
    }
    protected override void OnUpdate(UpdateArgs args)
    {
        if (args.Reason == UpdateReason.Unknown || args.Reason == UpdateReason.HistoricalBar)
            return;

        if (this.isCorrectTimeFrame)
        {
            int ticksCount = this.IsCountdown
                ? this.HistoricalData.Aggregation.GetPeriod.PeriodMultiplier - (int)this.Ticks()
                : (int)this.Ticks();

            this.calculatedMessage = this.ShowPercentages
                ? this.prefix + ticksCount * 100 / this.HistoricalData.Aggregation.GetPeriod.PeriodMultiplier + " %"
                : this.prefix + ticksCount.ToString();
        }
    }
    public override void OnPaintChart(PaintChartEventArgs args)
    {
        var gr = args.Graphics;

        string message = this.isCorrectTimeFrame ? this.calculatedMessage : ERROR_MESSAGE;

        gr.DrawString(message, this.font, this.fontBrush, args.Rectangle.Width - this.messageMargin, args.Rectangle.Height - 2, this.farFarSF);
    }
    public override void Dispose()
    {
        this.font?.Dispose();
        this.fontBrush?.Dispose();
        this.farFarSF?.Dispose();
    }
}