// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Utils;

namespace OscillatorsIndicators;

public class IndicatorSlippage : Indicator
{
    #region Parameters
    // Displays Input Parameter as dropdown list.
    [InputParameter("View mode", 0, variants: new object[] {
         "Absolute", SlippageViewMode.Absolute,
         "Percentage", SlippageViewMode.Percentage}
    )]
    public SlippageViewMode ViewMode = SlippageViewMode.Absolute;

    public string FileFullPath;
    public bool WriteToFile = true;

    private CancellationTokenSource cancellationSource;
    private Task loadingTickHistoryTask;
    private Queue<SlippageSourceItem> realTimeLastBuffer;

    private SlippageFinder slippageFinder;
    private SlippageDataExporter dataExporter;

    internal bool IsLoadedSuccessfully { get; private set; }
    public bool IsLoading => this.loadingTickHistoryTask != null && this.loadingTickHistoryTask.Status == TaskStatus.Running;

    public Font Font { get; private set; }
    public Brush Brush { get; private set; }

    public override string SourceCodeLink => "https://github.com/Quantower/Scripts/blob/main/Indicators/IndicatorSlippage.cs";

    #endregion Parameters

    public IndicatorSlippage()
    {
        this.Name = "Slippage indicator";

        this.AddLineSeries("Max slippage", Color.Orange, 2, LineStyle.Histogramm);
        this.AddLineLevel(0d, "Zero line", Color.Gray, 2, LineStyle.Solid);

        this.SeparateWindow = true;
        this.FileFullPath = Path.Combine(Directory.GetCurrentDirectory(), "slippage.csv");

        this.Brush = new SolidBrush(Color.Orange);
        this.Font = new Font("Verdana", 11, FontStyle.Regular, GraphicsUnit.Pixel);

        this.realTimeLastBuffer = new Queue<SlippageSourceItem>();
        this.slippageFinder = new SlippageFinder();
        this.dataExporter = new SlippageDataExporter();
    }

    protected override void OnInit()
    {
        this.AbortLoading();
        var token = this.cancellationSource.Token;

        this.Digits = 5;
        this.slippageFinder.OnNewArea += this.SlippageFinder_OnNewArea;
        this.dataExporter.Initialize();

        // from (time of first item) to (now)
        var intervalToDownload = new Interval<DateTime>(this.HistoricalData[0, SeekOriginHistory.Begin].TimeLeft, Core.TimeUtils.DateTimeUtcNow)
           .Split(this.Symbol.GetHistoryDownloadingStep(Period.TICK1))
           .ToArray();

        this.Symbol.NewLast += this.Symbol_NewLast;
        this.loadingTickHistoryTask = Task.Factory.StartNew(() =>
        {
            this.IsLoadedSuccessfully = false;

            for (int j = 0; j < intervalToDownload.Length; j++)
            {
                if (token.IsCancellationRequested)
                    return;

                try
                {
                    var tickHistory = this.Symbol.GetHistory(new HistoryRequestParameters()
                    {
                        Symbol = this.Symbol,

                        FromTime = intervalToDownload[j].From,
                        ToTime = intervalToDownload[j].To,

                        Aggregation = new HistoryAggregationTick(1),
                        Period = Period.TICK1,
                        HistoryType = HistoryType.Last,

                        CancellationToken = token,
                        //ForceReload = isForceReload
                    });

                    if (token.IsCancellationRequested)
                        return;

                    // 
                    for (int i = 0; i < tickHistory.Count; i++)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        var historyItem = (HistoryItemLast)tickHistory[i, SeekOriginHistory.Begin];

                        var index = this.HistoricalData.GetIndexByTime(historyItem.TicksLeft, SeekOriginHistory.Begin);

                        if (index == -1)
                            continue;

                        this.slippageFinder.Push(new SlippageSourceItem()
                        {
                            Time = new DateTime(historyItem.TimeLeft.Year,
                                historyItem.TimeLeft.Month,
                                historyItem.TimeLeft.Day, historyItem.
                                TimeLeft.Hour,
                                historyItem.TimeLeft.Minute,
                                historyItem.TimeLeft.Second,
                                historyItem.TimeLeft.Millisecond, DateTimeKind.Utc),
                            Price = historyItem.Price,
                            Volume = historyItem.Volume,
                            ChartBarIndex = (int)index // для простішого пошуку
                        });
                    }


                    tickHistory.Dispose();
                }
                catch (Exception ex)
                {
                    Core.Loggers.Log(ex, "Slippage indicator: Init", LoggingLevel.Error);
                }

                // draw
                foreach (var item in this.slippageFinder.AggregatedAreas)
                {
                    if (item.Key >= this.Count)
                        break;

                    this.SetSlippageValue(item.Value.BestArea, this.Count - item.Key - 1);
                }
            }

            // save data
            this.dataExporter.SaveToFile(this.FileFullPath);
            this.dataExporter.Initialize();

            this.IsLoadedSuccessfully = true;
        });
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        base.OnUpdate(args);

        if (args.Reason == UpdateReason.HistoricalBar)
            return;

        if (this.IsLoadedSuccessfully)
        {
            if (this.slippageFinder.AggregatedAreas.ContainsKey(this.Count - 1))
                this.SetSlippageValue(this.slippageFinder.AggregatedAreas[this.Count - 1].BestArea, 0);
        }
    }

    public override void OnPaintChart(PaintChartEventArgs args)
    {
        if (this.IsLoading)
        {
            var graphics = args.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawString("Loading...", this.Font, this.Brush, args.Rectangle.X, args.Rectangle.Y + 30);
        }
        else if (this.IsLoadedSuccessfully)
        {
            var time = this.CurrentChart.MainWindow.CoordinatesConverter.GetTime(args.MousePosition.X);
            var index = (int)CurrentChart.MainWindow.CoordinatesConverter.GetBarIndex(time);

            if (index >= this.HistoricalData.Count)
                return;

            if (this.slippageFinder != null && this.slippageFinder.AggregatedAreas.ContainsKey(index))
            {
                var graphics = args.Graphics;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var elem = this.slippageFinder.AggregatedAreas[index];

                var message = $"Time:{elem.BestArea.Time.ToString("HH:mm:ss.fff")} | Ticks:{elem.BestArea.ElementsCount} | Volume:{elem.BestArea.CulmulativeVolume} | Slippage:({elem.BestArea.SlippageAbsoluteValue}, {elem.BestArea.SlippagePercentValue.ToString("F5")}%)";
                graphics.DrawString(message, this.Font, this.Brush, args.Rectangle.X, args.Rectangle.Y + 30);
            }
        }
    }

    public override IList<SettingItem> Settings
    {
        get
        {
            var settings = base.Settings;
            var separGroup = base.Settings.GetItemByName("View mode").SeparatorGroup;
            settings.Add(new SettingItemBoolean("WriteToFile", this.WriteToFile, 10)
            {
                Text = "Allow write to file",
                SeparatorGroup = separGroup
            });
            settings.Add(new SettingItemString("Filepath", this.FileFullPath, 20)
            {
                Text = "File path",
                SeparatorGroup = separGroup,
                Relation = new SettingItemRelationVisibility("WriteToFile", true)
            });

            return settings;
        }
        set => base.Settings = value;
    }

    internal void AbortLoading()
    {
        if (this.cancellationSource != null)
            this.cancellationSource.Cancel();

        this.cancellationSource = new CancellationTokenSource();
    }

    public override void Dispose()
    {
        this.AbortLoading();
        this.slippageFinder.Clear();
        this.dataExporter.Clear();

        this.slippageFinder.OnNewArea -= this.SlippageFinder_OnNewArea;
        if (this.Symbol != null)
            this.Symbol.NewLast -= this.Symbol_NewLast;
        base.Dispose();
    }

    private void Symbol_NewLast(Symbol symbol, Last last)
    {
        var sourceItem = new SlippageSourceItem()
        {
            Time = new DateTime(last.Time.Year, last.Time.Month, last.Time.Day, last.Time.Hour, last.Time.Minute, last.Time.Second, last.Time.Millisecond),
            Price = last.Price,
            Volume = last.Size,
            ChartBarIndex = this.Count - 1
        };

        // collect
        if (this.IsLoading)
        {
            this.realTimeLastBuffer.Enqueue(sourceItem);
        }
        else if (this.IsLoadedSuccessfully)
        {
            while (this.realTimeLastBuffer.Count > 0)
                this.slippageFinder.Push(this.realTimeLastBuffer.Dequeue());

            this.slippageFinder.Push(sourceItem);
        }
    }

    private void SlippageFinder_OnNewArea(SlippageArea obj) => this.dataExporter.AddNewItem(obj);
    private void SetSlippageValue(SlippageArea bestArea, int offset)
    {
        var value = ViewMode == SlippageViewMode.Absolute
            ? bestArea.SlippageAbsoluteValue
            : bestArea.SlippagePercentValue;

        this.SetValue(value, 0, offset);
    }
}

internal class SlippageFinder
{
    private const int MIN_ELEMENT_COUNT_FOR_CALCULATION = 2;

    private SlippageArea currectArea;
    public Dictionary<int, SlippageAggregator> AggregatedAreas { get; private set; }
    public event Action<SlippageArea> OnNewArea;

    public SlippageFinder() => this.AggregatedAreas = new Dictionary<int, SlippageAggregator>();

    internal void Clear()
    {
        foreach (var item in AggregatedAreas.Values)
            item.Dispose();

        this.AggregatedAreas.Clear();
    }

    internal void Push(SlippageSourceItem sourceItem)
    {
        if (this.currectArea == null)
            this.currectArea = new SlippageArea();

        if (!this.currectArea.IsInitialized || this.currectArea.Time.Equals(sourceItem.Time))
        {
            this.currectArea.Push(sourceItem);

            if (this.currectArea.ElementsCount == MIN_ELEMENT_COUNT_FOR_CALCULATION)
            {
                if (!this.AggregatedAreas.ContainsKey(sourceItem.ChartBarIndex))
                    this.AggregatedAreas[sourceItem.ChartBarIndex] = new SlippageAggregator(this.currectArea);
            }

            if (this.AggregatedAreas.ContainsKey(sourceItem.ChartBarIndex))
                this.AggregatedAreas[sourceItem.ChartBarIndex].Recalculate(this.currectArea);
        }
        else
        {
            this.OnNewArea?.Invoke(this.currectArea);
            this.currectArea = null;
            this.Push(sourceItem);
        }
    }
}
internal class SlippageArea
{
    public SlippageSourceItem FirstSourceItem { get; private set; }

    public int ElementsCount { get; private set; }
    public DateTime Time { get; private set; }
    public double CulmulativeVolume { get; private set; }
    public double SlippageAbsoluteValue { get; private set; }
    public double SlippagePercentValue { get; private set; }

    public bool IsInitialized { get; private set; }

    public SlippageArea(SlippageArea currectArea)
        : this()
    {
        this.CulmulativeVolume = currectArea.CulmulativeVolume;
        this.ElementsCount = currectArea.ElementsCount;
        this.FirstSourceItem = currectArea.FirstSourceItem;
        this.IsInitialized = currectArea.IsInitialized;
        this.SlippageAbsoluteValue = currectArea.SlippageAbsoluteValue;
        this.SlippagePercentValue = currectArea.SlippagePercentValue;
        this.Time = currectArea.Time;
    }
    public SlippageArea() => this.IsInitialized = false;

    internal void Push(SlippageSourceItem sourceItem)
    {
        if (!this.IsInitialized)
        {
            this.FirstSourceItem = sourceItem;
            this.IsInitialized = true;
        }

        this.ElementsCount++;

        this.Time = sourceItem.Time;
        this.CulmulativeVolume += sourceItem.Volume;

        if (this.ElementsCount > 1)
        {
            this.SlippageAbsoluteValue = sourceItem.Price - this.FirstSourceItem.Price;
            this.SlippagePercentValue = (sourceItem.Price / this.FirstSourceItem.Price - 1) * 100;
        }
    }
}
internal class SlippageSourceItem
{
    public DateTime Time { get; internal set; }
    public double Price { get; internal set; }
    public int ChartBarIndex { get; internal set; }
    public double Volume { get; internal set; }
}
internal class SlippageAggregator
{
    public SlippageArea BestArea { get; private set; }

    public SlippageAggregator(SlippageArea currectArea) => this.BestArea = new SlippageArea(currectArea);

    internal void Dispose() => this.BestArea = null;
    internal void Recalculate(SlippageArea currectArea)
    {
        if ((this.BestArea != null && Math.Abs(this.BestArea.SlippageAbsoluteValue) < Math.Abs(currectArea.SlippageAbsoluteValue)) || currectArea.Time.Equals(this.BestArea.Time))
            this.BestArea = new SlippageArea(currectArea);
    }
}

internal class SlippageDataExporter
{
    private StringBuilder data = new StringBuilder();
    private const string SEPARATOR = ";";

    internal void AddNewItem(SlippageArea area)
    {
        // data line
        this.data.Append(
            area.Time.ToString("yyyy.MM.dd HH:mm:ss.fff") + SEPARATOR +
            area.ElementsCount + SEPARATOR +
            area.CulmulativeVolume + SEPARATOR +
            area.FirstSourceItem.Price + SEPARATOR +
            area.SlippageAbsoluteValue + SEPARATOR +
            area.SlippagePercentValue + SEPARATOR);

        // new line
        this.data.Append(Environment.NewLine);
    }
    internal void Initialize()
    {
        this.Clear();

        // headers
        this.data.Append(
            "DateTime" + SEPARATOR +
            "Ticks" + SEPARATOR +
            "Cumulative volume" + SEPARATOR +
            "Price" + SEPARATOR +
            "Absolute slippage" + SEPARATOR +
            "Percentage slippage" + SEPARATOR);

        // new line
        this.data.Append(Environment.NewLine);
    }
    internal void Clear() => this.data.Clear();

    public void SaveToFile(string filePath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            return;

        Task.Factory.StartNew(() =>
        {
            try
            {
                File.WriteAllText(filePath, this.data.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
                Core.Instance.Loggers.Log("SI indicator: Cannot to save file", LoggingLevel.Error);
            }

        });
    }
}

public enum SlippageViewMode
{
    Absolute,
    Percentage
}