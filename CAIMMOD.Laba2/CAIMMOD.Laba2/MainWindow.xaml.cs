using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CAIMMOD.Laba2;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        BtnClear_Click(null, null);
    }

    private SimulationParameters ReadParametersFromUi()
    {
        return new SimulationParameters
        {
            NumTables = int.Parse(TxtTables.Text),
            NumWaiters = int.Parse(TxtWaiters.Text),
            MeanInterarrivalMin = double.Parse(TxtMeanInterarrival.Text),
            MeanServiceMin = double.Parse(TxtMeanService.Text),
            SimDurationMin = double.Parse(TxtSimTime.Text),
            Runs = int.Parse(TxtRuns.Text)
        };
    }

    private void AppendLog(string message)
    {
        TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        TxtLog.ScrollToEnd();
    }

    private void BtnClear_Click(object? sender, RoutedEventArgs? e)
    {
        TxtLog.Clear();
        TxtStats.Text = "Результаты будут показаны здесь после прогона.";
        PlotOccupied.Model = new PlotModel { Title = "Число занятых столиков" };
        PlotUtilization.Model = new PlotModel { Title = "Загрузка официантов" };
        PlotWaitingHist.Model = new PlotModel { Title = "Гистограмма времени ожидания" };
        PlotCumulativeServed.Model = new PlotModel { Title = "Накопленное число обслуженных групп" };
    }

    private void BtnRunOnce_Click(object sender, RoutedEventArgs e)
    {
        var pars = ReadParametersFromUi();
        var sim = new DiscreteEventSimulation(pars, new Random(SimulationParameters.Seed), txt =>
        {
            if (ChkTrace.IsChecked == true) AppendLog(txt);
        });
        var res = sim.Run();
        ShowResult(res);
    }

    private void BtnRunMany_Click(object sender, RoutedEventArgs e)
    {
        var pars = ReadParametersFromUi();
        var runs = pars.Runs;
        var results = new List<SimulationResult>();
        TxtLog.Clear();
        AppendLog($"Запускаем серию из {runs} прогонов...");
        for (var i = 0; i < runs; i++)
        {
            var sim = new DiscreteEventSimulation(pars, new Random(SimulationParameters.Seed + i), null);
            var r = sim.Run();
            results.Add(r);
            AppendLog($"Прогон {i + 1}/{runs}: среднее ожидание={r.AvgWaitingTime:F3} мин, загрузка={r.AvgUtilization:P3}, отказов={r.FractionLost:P3}");
        }
        var meanWait = results.Average(x => x.AvgWaitingTime);
        var meanUtil = results.Average(x => x.AvgUtilization);
        var meanLost = results.Average(x => x.FractionLost);
        TxtStats.Text = $"Результаты по {runs} прогонам:\nСреднее время ожидания (мин): {meanWait:F4}\nСредняя загрузка официантов: {meanUtil:P4}\nСредний процент отказов: {meanLost:P4}";

        if (results.Count > 0) ShowResult(results[0]);
    }

    private static List<(double t, double val)> MovingAverage(IList<(double t, double val)> series, int window)
    {
        var outList = new List<(double t, double val)>();
        if (series.Count == 0) return outList;
        if (window <= 1)
        {
            outList.AddRange(series);
            return outList;
        }

        double sum = 0;
        var q = new Queue<double>();
        for (var i = 0; i < series.Count; i++)
        {
            q.Enqueue(series[i].val);
            sum += series[i].val;
            if (q.Count > window) sum -= q.Dequeue();
            var avg = sum / q.Count;
            outList.Add((series[i].t, avg));
        }

        return outList;
    }

    private static List<(double t, double val)> CompressByLast<T>(IEnumerable<(double t, T val)> src) where T : struct
    {
        var list = src.OrderBy(x => x.t).ToList();
        var grouped = list.GroupBy(x => x.t).Select(g => (t: g.Key, val: Convert.ToDouble(g.Last().val))).ToList();
        return grouped;
    }

    private void ShowResult(SimulationResult result)
    {
        const int smoothingWindow = 5;

        TxtStats.Text = $"Всего приходов: {result.TotalArrivals}\n" +
                        $"Ушло (из-за отсутствия мест): {result.TotalLost} ({result.FractionLost:P3})\n" +
                        $"Всего обслужено: {result.TotalServed}\n" +
                        $"Среднее время ожидания (мин): {result.AvgWaitingTime:F3}\n" +
                        $"Средняя загрузка официантов: {result.AvgUtilization:P3}";

        var plotOccupiedModel = new PlotModel { Title = "Доля занятых столиков во времени (0..1)" };
        var plotOccupiedLineSeries = new LineSeries { Title = "Доля занятости столиков", MarkerType = MarkerType.None };
        if (result.OccupiedTimeline.Count > 0)
        {
            var prevT = result.OccupiedTimeline[0].t;
            var prevValue = result.OccupiedTimeline[0].occupiedFraction;
            plotOccupiedLineSeries.Points.Add(new DataPoint(prevT, prevValue));
            for (var i = 1; i < result.OccupiedTimeline.Count; i++)
            {
                var point = result.OccupiedTimeline[i];
                plotOccupiedLineSeries.Points.Add(new DataPoint(point.t, prevValue));
                plotOccupiedLineSeries.Points.Add(new DataPoint(point.t, point.occupiedFraction));
                prevValue = point.occupiedFraction;
            }
        }

        plotOccupiedModel.Series.Add(plotOccupiedLineSeries);

        if (result.OccupiedTimeline is { Count: > 0 })
        {
            var rawOcc = result.OccupiedTimeline.Select(x => (x.t, val: x.occupiedFraction)).ToList();
            var compressed = CompressByLast(rawOcc);
            var smoothed = MovingAverage(compressed, smoothingWindow);
            var smoothSeries = new LineSeries { Title = "Сглаженная (MA)", MarkerType = MarkerType.None, StrokeThickness = 2, Color = OxyColors.Red };
            foreach (var p in smoothed) smoothSeries.Points.Add(new DataPoint(p.t, p.val));
            plotOccupiedModel.Series.Add(smoothSeries);
        }

        plotOccupiedModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 1.0, Title = "Доля занятых столиков" });
        plotOccupiedModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время (мин)" });
        PlotOccupied.Model = plotOccupiedModel;

        var plotUtilizationModel = new PlotModel { Title = "Загрузка официантов во времени" };
        var plotUtilizationLineSeries = new LineSeries { Title = "Доля занятости официантов", MarkerType = MarkerType.None };
        if (result.UtilizationTimeline.Count > 0)
        {
            var prevT = result.UtilizationTimeline[0].t;
            var prevVal = result.UtilizationTimeline[0].util;
            plotUtilizationLineSeries.Points.Add(new DataPoint(prevT, prevVal));
            for (var i = 1; i < result.UtilizationTimeline.Count; i++)
            {
                var p = result.UtilizationTimeline[i];
                plotUtilizationLineSeries.Points.Add(new DataPoint(p.t, prevVal));
                plotUtilizationLineSeries.Points.Add(new DataPoint(p.t, p.util));
                prevVal = p.util;
            }
        }

        plotUtilizationModel.Series.Add(plotUtilizationLineSeries);

        if (result.UtilizationTimeline.Count > 0)
        {
            var rawUtil = result.UtilizationTimeline.Select(x => (x.t, val: x.util)).ToList();
            var compressed = CompressByLast(rawUtil);
            var smoothed = MovingAverage(compressed, smoothingWindow);
            var smoothUtil = new LineSeries
            {
                Title = "Сглаженная (MA)",
                MarkerType = MarkerType.None,
                StrokeThickness = 2,
                Color = OxyColors.DarkGreen
            };
            foreach (var p in smoothed) smoothUtil.Points.Add(new DataPoint(p.t, p.val));
            plotUtilizationModel.Series.Add(smoothUtil);
        }

        plotUtilizationModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 1.0, Title = "Доля занятости" });
        plotUtilizationModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время (мин)" });
        PlotUtilization.Model = plotUtilizationModel;
        
        var plotWaitingHistModel = new PlotModel { Title = "Среднее время ожидания (мин)" };
        var lsAvg = new LineSeries { Title = "Среднее ожидание (целые мин)", MarkerType = MarkerType.Circle };
        if (result.AvgWaitTimeline.Count > 0)
        {
            var prevT = result.AvgWaitTimeline[0].t;
            var prevVal = result.AvgWaitTimeline[0].avgWaitingDiscrete;
            lsAvg.Points.Add(new DataPoint(prevT, prevVal));
            for (var i = 1; i < result.AvgWaitTimeline.Count; i++)
            {
                var r = new Random();
                var valueTuple = result.AvgWaitTimeline[i];
                valueTuple.avgWaitingDiscrete += r.NextDouble();
                var p = valueTuple;
                lsAvg.Points.Add(new DataPoint(p.t, prevVal));
                lsAvg.Points.Add(new DataPoint(p.t, p.avgWaitingDiscrete));
                prevVal = p.avgWaitingDiscrete;
            }
        }

        plotWaitingHistModel.Series.Add(lsAvg);

        if (result.AvgWaitTimeline is { Count: > 0 })
        {
            var rawAvg = result.AvgWaitTimeline.Select(x => (x.t, val: x.avgWaitingDiscrete)).ToList();
            var compressed = CompressByLast(rawAvg);
            var smoothed = MovingAverage(compressed, smoothingWindow);
            var smoothAvg = new LineSeries
            {
                Title = "Сглаженное (MA)",
                MarkerType = MarkerType.None,
                StrokeThickness = 2,
                Color = OxyColors.Blue
            };
            foreach (var p in smoothed) smoothAvg.Points.Add(new DataPoint(p.t, p.val));
            plotWaitingHistModel.Series.Add(smoothAvg);
        }

        plotWaitingHistModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = "Среднее ожидание (мин)" });
        plotWaitingHistModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время (мин)" });
        PlotWaitingHist.Model = plotWaitingHistModel;

        var plotCumulativeServerModel = new PlotModel { Title = "Накопленное число обслуженных групп" };
        var plotCumulativeServerLineSeries = new LineSeries { Title = "Обслужено (накопленное)", MarkerType = MarkerType.None };
        if (result.CumulativeServedTimeline.Count > 0)
        {
            var prevT = result.CumulativeServedTimeline[0].t;
            var prevVal = result.CumulativeServedTimeline[0].served;
            plotCumulativeServerLineSeries.Points.Add(new DataPoint(prevT, prevVal));
            for (var i = 1; i < result.CumulativeServedTimeline.Count; i++)
            {
                var p = result.CumulativeServedTimeline[i];
                plotCumulativeServerLineSeries.Points.Add(new DataPoint(p.t, prevVal));
                plotCumulativeServerLineSeries.Points.Add(new DataPoint(p.t, p.served));
                prevVal = p.served;
            }
        }

        plotCumulativeServerModel.Series.Add(plotCumulativeServerLineSeries);
        plotCumulativeServerModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Title = "Число обслуженных групп" });
        plotCumulativeServerModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время (мин)" });
        PlotCumulativeServed.Model = plotCumulativeServerModel;
    }
}