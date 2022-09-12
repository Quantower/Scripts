// Copyright QUANTOWER LLC. © 2017-2022. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;

namespace VolumeIndicators
{
    public class IndicatorDeltaRotation : Indicator, IVolumeAnalysisIndicator
    {
        #region Parameters
        public const string VISUAL_MODE_SI = "Visual mode";

        public override string HelpLink => "https://help.quantower.com/analytics-panels/chart/technical-indicators/volume/delta-rotation";

        [InputParameter(VISUAL_MODE_SI, 10, variants: new object[]{
            "Boxes", DeltaRotationVisualMode.Boxes,
            "Bars", DeltaRotationVisualMode.Bars,
            "Bars in boxes", DeltaRotationVisualMode.BarsInBoxes,
        })]
        internal DeltaRotationVisualMode VisualMode = DeltaRotationVisualMode.BarsInBoxes;

        [InputParameter("Absorption bar color", 15)]
        internal Color AbsorptionBarColor;

        [InputParameter("Exclude absorption bars", 30)]
        internal bool ExcludeAbsorptionBars = false;

        [InputParameter("Highlight absorption bars", 40)]
        internal bool HighlightAbsorptionBars = false;

        private PairColor pairColor;
        internal PairColor AreaPairColor
        {
            get => this.pairColor;
            set
            {
                this.pairColor = value;

                this.upAreaPen = new Pen(value.Color1, 1);
                this.downAreaPen = new Pen(value.Color2, 1);
            }
        }

        private readonly Font font;
        private readonly StringFormat centerCenterSF;
        private Pen upAreaPen;
        private Pen downAreaPen;

        private readonly List<DeltaRotationArea> deltaRotationAreas;

        private DeltaRotationArea previousArea;
        private DeltaRotationArea currentArea;
        private bool allowCalculateRealTime;
        public bool isLoadedSuccessfully;

        public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorDeltaRotation.cs";

        #endregion Parameters

        public IndicatorDeltaRotation()
        {
            this.Name = "Delta Rotation";
            this.deltaRotationAreas = new List<DeltaRotationArea>();
            this.IsUpdateTypesSupported = false;

            this.AddLineSeries("DR", Color.Gray, 5, LineStyle.Histogramm);

            this.AbsorptionBarColor = Color.Orange;
            this.AreaPairColor = new PairColor()
            {
                Color1 = Color.FromArgb(33, 150, 243),
                Color2 = Color.FromArgb(239, 83, 80),
                Text1 = loc._("Up"),
                Text2 = loc._("Down")
            };
            this.font = new Font("Verdana", 10, FontStyle.Regular, GraphicsUnit.Point);
            this.centerCenterSF = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            this.SeparateWindow = true;
        }

        #region Overrides
        protected override void OnInit()
        {
            this.previousArea = null;
            this.currentArea = null;
            this.isLoadedSuccessfully = false;
            this.deltaRotationAreas.Clear();
        }
        protected override void OnUpdate(UpdateArgs args)
        {
            if (!this.allowCalculateRealTime)
                return;

            var isNewBar = this.HistoricalData.Period == Period.TICK1 && this.HistoricalData.Aggregation is HistoryAggregationTick
                ? args.Reason == UpdateReason.NewTick
                : args.Reason == UpdateReason.HistoricalBar || args.Reason == UpdateReason.NewBar;

            this.CalculateIndicatorByOffset(0, isNewBar);
        }
        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                var inputSepar = settings.GetItemByName(VISUAL_MODE_SI)?.SeparatorGroup ?? new SettingItemSeparatorGroup();

                settings.Add(new SettingItemPairColor("AreaColors", this.AreaPairColor, 20)
                {
                    SeparatorGroup = inputSepar,
                    Text = loc._("Area colors")
                });

                return settings;
            }
            set
            {
                base.Settings = value;

                if (value.GetItemByName("AreaColors") is SettingItemPairColor areaPairColorSI)
                {
                    this.AreaPairColor = (PairColor)areaPairColorSI.Value;
                    this.Refresh();
                }
            }
        }
        #endregion Overrides

        #region Drawing
        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (this.VisualMode == DeltaRotationVisualMode.Bars && this.isLoadedSuccessfully)
                return;

            var gr = args.Graphics;
            gr.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            var converter = this.CurrentChart.Windows[args.WindowIndex].CoordinatesConverter;

            var clip = gr.Save();
            gr.SetClip(args.Rectangle);

            if (!this.isLoadedSuccessfully)
            {
                gr.DrawString(loc._("Loading volume analysis data..."), this.font, Brushes.DodgerBlue, args.Rectangle, this.centerCenterSF);
            }
            else
            {
                var leftDT = converter.GetTime(args.Rectangle.X);
                var rightDT = converter.GetTime(args.Rectangle.Right);
                var zeroY = (float)converter.GetChartY(0d);

                // draw stable areas
                foreach (var area in this.deltaRotationAreas)
                    this.DrawArea(gr, converter, area, ref leftDT, ref rightDT, ref zeroY);

                // draw current parts
                if (this.currentArea != null)
                {
                    // draw 'current' area.
                    this.DrawArea(gr, converter, this.currentArea, ref leftDT, ref rightDT, ref zeroY);

                    //
                    // mikhail: дві останні зони - не стабільні, оскільки можуть злитись в одну, а потім знову розділитись.
                    // якщо в них різний 'Direction' - малюємо обидві. Інакше, малюємо лише "currentArea" бо в даний момент вона вже об'єднана з "previousArea".
                    if (this.previousArea != null && this.currentArea.IsUpBarsDirection != this.previousArea.IsUpBarsDirection)
                        this.DrawArea(gr, converter, this.previousArea, ref leftDT, ref rightDT, ref zeroY);
                }
            }
            //
            gr.Restore(clip);
        }
        private void DrawArea(Graphics gr, IChartWindowCoordinatesConverter converter, DeltaRotationArea area, ref DateTime leftScreenDT, ref DateTime rightScreenDT, ref float zeroLineY)
        {
            if (area.StartTime >= rightScreenDT)
                return;

            if (area.EndTime < leftScreenDT)
                return;

            var startX = (float)converter.GetChartX(area.StartTime);
            var endX = (float)converter.GetChartX(area.EndTime) + this.CurrentChart.BarsWidth - 1f;
            var priceY = (float)converter.GetChartY(area.CumulativeDelta);

            //
            var height = zeroLineY > priceY
                ? zeroLineY - priceY
                : priceY - zeroLineY;

            if (height < 1)
                height = 1;

            //
            var topY = area.IsAboveZero
                ? priceY
                : zeroLineY;

            topY -= 1f;

            if (this.VisualMode == DeltaRotationVisualMode.Boxes)
            {
                if (area.IsUpBarsDirection)
                    gr.FillRectangle(this.upAreaPen.Brush, startX, topY, endX - startX, height);
                else
                    gr.FillRectangle(this.downAreaPen.Brush, startX, topY, endX - startX, height);
            }
            else
            {
                if (area.IsUpBarsDirection)
                    gr.DrawRectangle(this.upAreaPen, startX, topY, endX - startX, height);
                else
                    gr.DrawRectangle(this.downAreaPen, startX, topY, endX - startX, height);
            }

        }
        #endregion Drawing

        private void RecalculateAllIndicator()
        {
            this.allowCalculateRealTime = false;

            for (int offset = this.Count - 1; offset >= 0; offset--)
                CalculateIndicatorByOffset(offset, isNewBar: true);

            this.allowCalculateRealTime = true;
        }
        private void CalculateIndicatorByOffset(int offset, bool isNewBar)
        {
            //
            if (isNewBar)
            {
                if (this.currentArea != null)
                {
                    if (this.previousArea != null && this.previousArea.IsUpBarsDirection != this.currentArea.IsUpBarsDirection)
                        this.deltaRotationAreas.Add(this.previousArea);

                    this.previousArea = this.currentArea;
                }
            }

            var index = this.Count - 1 - offset;
            if (index < 0)
                return;

            var volumeAnalysis = this.HistoricalData[index, SeekOriginHistory.Begin].VolumeAnalysisData;

            if (volumeAnalysis != null)
            {
                if (!this.isLoadedSuccessfully)
                    this.isLoadedSuccessfully = true;

                var close = this.Close(offset);
                var open = this.Open(offset);

                var isGrownBar = close > open;
                var isDoji = close == open;
                var isAbsorptionBar = !isDoji && ((volumeAnalysis.Total.Delta < 0 && isGrownBar) || (volumeAnalysis.Total.Delta > 0 && !isGrownBar));
                var isUpDirection = isDoji
                    ? this.previousArea?.IsUpBarsDirection ?? false
                    : isGrownBar;

                //
                //
                //
                if (this.ExcludeAbsorptionBars && isAbsorptionBar)
                {
                    if (this.previousArea != null)
                        this.deltaRotationAreas.Add(this.previousArea);

                    this.SetValue(volumeAnalysis.Total.Delta, 0, offset);

                    this.previousArea = null;
                    this.currentArea = null;
                }
                else
                {


                    this.currentArea = new DeltaRotationArea();
                    this.currentArea.Update(volumeAnalysis.Total.Delta, this.Time(offset), index, isUpDirection);

                    if (this.previousArea != null && this.previousArea.IsUpBarsDirection == this.currentArea.IsUpBarsDirection)
                        this.currentArea.Merge(this.previousArea);

                    if (this.VisualMode == DeltaRotationVisualMode.Boxes)
                    {
                        int startOffset = this.Count - this.currentArea.StartIndex - 1;
                        int endOffset = this.Count - this.currentArea.EndIndex - 1;

                        if (startOffset >= 0 && endOffset >= 0)
                        {
                            for (int i = (int)endOffset; i <= startOffset; i++)
                                this.SetValue(this.currentArea.CumulativeDelta, 0, i);
                        }

                        if (this.previousArea != null && this.previousArea.IsUpBarsDirection != this.currentArea.IsUpBarsDirection)
                        {
                            startOffset = this.Count - this.previousArea.StartIndex - 1;
                            endOffset = this.Count - this.previousArea.EndIndex - 1;

                            if (startOffset >= 0 && endOffset >= 0)
                            {
                                for (int i = (int)endOffset; i <= startOffset; i++)
                                    this.SetValue(this.previousArea.CumulativeDelta, 0, i);
                            }
                        }
                    }
                    else
                        this.SetValue(currentArea.CumulativeDelta, 0, offset);
                }

                //
                // Set markers
                //
                if (this.HighlightAbsorptionBars && isAbsorptionBar)
                    this.LinesSeries[0].SetMarker(offset, this.AbsorptionBarColor);
                else
                {
                    if (isUpDirection)
                        this.LinesSeries[0].SetMarker(offset, this.AreaPairColor.Color1);
                    else
                        this.LinesSeries[0].SetMarker(offset, this.AreaPairColor.Color2);
                }
            }
        }

        #region IVolumeAnalysisIndicator
        public bool IsRequirePriceLevelsCalculation => false;
        public void VolumeAnalysisData_Loaded()
        {
            this.RecalculateAllIndicator();
        }
        #endregion IVolumeAnalysisIndicator

        #region Nested
        internal enum DeltaRotationVisualMode
        {
            Boxes,
            Bars,
            BarsInBoxes
        }
        class DeltaRotationArea
        {
            public DateTime StartTime { get; private set; }
            public int StartIndex { get; private set; }
            public DateTime EndTime { get; private set; }
            public int EndIndex { get; private set; }

            public double CumulativeDelta { get; private set; }
            public bool IsUpBarsDirection { get; private set; }

            public bool IsAboveZero => this.CumulativeDelta > 0;

            internal void Merge(DeltaRotationArea area)
            {
                if (area.StartTime < this.StartTime)
                {
                    this.StartTime = area.StartTime;
                    this.StartIndex = area.StartIndex;
                }

                if (area.EndTime > this.EndTime)
                {
                    this.EndTime = area.EndTime;
                    this.EndIndex = area.EndIndex;
                }

                this.CumulativeDelta += area.CumulativeDelta;
            }
            internal void Update(double delta, DateTime dateTime, int index, bool isUpDirection)
            {
                if (this.StartTime == default)
                {
                    this.StartTime = dateTime;
                    this.StartIndex = index;
                }
                this.CumulativeDelta += delta;
                this.IsUpBarsDirection = isUpDirection;

                this.EndTime = dateTime;
                this.EndIndex = index;
            }
        }
        #endregion Nested
    }
}

