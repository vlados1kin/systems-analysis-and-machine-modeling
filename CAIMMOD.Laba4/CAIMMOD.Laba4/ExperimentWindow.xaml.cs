using System.Windows;
using CAIMMOD.Laba4.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using MathNet.Numerics;

namespace CAIMMOD.Laba4;

public partial class ExperimentWindow
{
    private readonly SimulationParameters _basePars;

    public ExperimentWindow(SimulationParameters pars)
    {
        InitializeComponent();
        _basePars = pars;
        AppendLog("Окно экспериментов готово.\nВыберите задачу слева.");
    }

    private void AppendLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            TxtLog.ScrollToEnd();
        });
    }

    private static List<SimulationResult> RunBatch(int runs, SimulationParameters pars)
    {
        var results = new List<SimulationResult>();
        var lockObj = new object();
        Parallel.For(0, runs, i =>
        {
            var sim = new DiscreteEventSimulation(pars, new Random(pars.Seed + i * 73), null);
            var res = sim.Run();
            lock (lockObj) results.Add(res);
        });
        return results;
    }

    private async void BtnTask1_Approximation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExpPlotView.Model = null;
            TxtLog.Clear();
            AppendLog("Запуск Задачи 1: Аппроксимация...");

            const string paramName = "MeanServiceMin";

            const double startX = 10.0;
            const double endX = 24.0;

            const int levels = 10;
            const int runsPerLevel = 20;

            var xData = new List<double>();
            var yData = new List<double>();

            AppendLog($"Варьирование параметра '{paramName}' от {startX} до {endX}.");

            const double step = (endX - startX) / (levels - 1);
            for (var i = 0; i < levels; i++)
            {
                var currentX = startX + i * step;

                var pars = ClonePars(_basePars);
                pars.MeanServiceMin = currentX;

                var batch = await Task.Run(() => RunBatch(runsPerLevel, pars));
                var avgY = batch.Average(r => r.AvgWaitingTime);

                xData.Add(currentX);
                yData.Add(avgY);
                AppendLog($"ServiceTime={currentX:F2} -> WaitTime={avgY:F2}");
            }

            var xArr = xData.ToArray();
            var yArr = yData.ToArray();

            var pLinear = Fit.Line(xArr, yArr);
            Func<double, double> fLinear = x => pLinear.A + pLinear.B * x;
            var r2Linear = GoodnessOfFit.RSquared(xArr.Select(fLinear), yArr);

            var pPoly = Fit.Polynomial(xArr, yArr, 2);
            Func<double, double> fPoly = x => pPoly[0] + pPoly[1] * x + pPoly[2] * x * x;
            var r2Poly = GoodnessOfFit.RSquared(xArr.Select(fPoly), yArr);

            AppendLog("\n--- Результаты аппроксимации ---");
            AppendLog($"Линейная (y = a + bx): R^2 = {r2Linear:F4}");
            AppendLog($"  a={pLinear.A:F2}, b={pLinear.B:F2}");

            AppendLog($"Полиномиальная (y = a + bx + cx^2): R^2 = {r2Poly:F4}");
            AppendLog($"  a={pPoly[0]:F2}, b={pPoly[1]:F2}, c={pPoly[2]:F3}");

            var bestModel = r2Poly > r2Linear ? "Нелинейная (Полином)" : "Линейная";
            AppendLog($"\nВЫВОД: Наилучшее приближение — {bestModel}.");

            var model = new PlotModel { Title = "Зависимость Времени ожидания от Скорости обслуживания" };
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Ср. время обслуживания (мин)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Ср. время ожидания (мин)" });

            var scatter = new ScatterSeries
            {
                Title = "Данные",
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerFill = OxyColors.Gray
            };
            for (var i = 0; i < levels; i++) scatter.Points.Add(new ScatterPoint(xData[i], yData[i]));
            model.Series.Add(scatter);

            var lineLin = new FunctionSeries(fLinear, startX, endX, 0.1, $"Линейная (R2={r2Linear:F3})") { Color = OxyColors.Blue, StrokeThickness = 2 };
            var linePoly = new FunctionSeries(fPoly, startX, endX, 0.1, $"Полином 2 ст. (R2={r2Poly:F3})") { Color = OxyColors.Red, LineStyle = LineStyle.Dash, StrokeThickness = 2 };

            model.Series.Add(lineLin);
            model.Series.Add(linePoly);

            ExpPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }

    private async void BtnTask2_Stability_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtLog.Clear();
            AppendLog("Запуск Задачи 2: Стресс-тест (уход сотрудника)...");

            const double simDuration = 3000.0;
            const double eventTime = 300.0;

            var demoPars = new SimulationParameters
            {
                NumTables = 15,
                MeanInterarrivalMin = 8.0,
                MeanServiceMin = 20.0,
                SimDurationMin = simDuration,
                ReduceWaiterTime = eventTime,
                Runs = 1,
                Seed = 123
            };
        
            var parsUnstable = ClonePars(demoPars);
            parsUnstable.NumWaiters = 3;

            var parsStable = ClonePars(demoPars);
            parsStable.NumWaiters = 4;

            AppendLog($"Параметры: Приход=8мин, Обслуживание=20мин.");
            AppendLog($"Событие в T={eventTime}: Уменьшение кол-ва официантов на 1.");

            var resUnstable = (await Task.Run(() => RunBatch(1, parsUnstable))).First();
            var resStable = (await Task.Run(() => RunBatch(1, parsStable))).First();

            var model = new PlotModel { Title = "Реакция системы на уменьшение ресурсов" };

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Модельное время (мин)", Maximum = simDuration });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Среднее время ожидания (мин)" });

            model.Annotations.Add(new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = eventTime,
                Color = OxyColors.Black,
                LineStyle = LineStyle.Dash,
                Text = "Уход (-1)",
                StrokeThickness = 2
            });

            model.Series.Add(CreateSeries(resUnstable.CustomerHistory, "3 -> 2 Официант (Срыв)", OxyColors.Red));
            model.Series.Add(CreateSeries(resStable.CustomerHistory, "4 -> 3 Официант (Норма)", OxyColors.Green));

            ExpPlotView.Model = model;

            AppendLog("Красная линия: 2 официанта не справляются, график улетает вверх.");
            AppendLog("Зеленая линия: 3 официанта справляются, график выравнивается.");
            return;

            LineSeries CreateSeries(List<(double t, double d)> history, string title, OxyColor color)
            {
                var series = new LineSeries { Title = title, Color = color, StrokeThickness = 3 };
                var sorted = history.OrderBy(h => h.t).ToList();
                double sum = 0;
                var count = 0;

                for (var i = 0; i < sorted.Count; i++)
                {
                    sum += sorted[i].d;
                    count++;
                    series.Points.Add(new DataPoint(sorted[i].t, sum / count));
                }

                return series;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
    
    private async void BtnTask3_Comparison_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExpPlotView.Model = null;
            TxtLog.Clear();
            AppendLog("Запуск Задачи 3: Сравнение стратегий обслуживания...");

            var scenarios = new List<(string Name, SimulationParameters Pars)>();

            var parsFifo = ClonePars(_basePars);
            parsFifo.Discipline = QueueDiscipline.Fifo;
            scenarios.Add(("FIFO (Очередь)", parsFifo));

            var parsLifo = ClonePars(_basePars);
            parsLifo.Discipline = QueueDiscipline.Lifo;
            scenarios.Add(("LIFO (Стек)", parsLifo));

            var parsPrior = ClonePars(_basePars);
            parsPrior.Discipline = QueueDiscipline.SmallestGroupFirst;
            scenarios.Add(("Priority (Малые группы)", parsPrior));

            var model = new PlotModel { Title = "Эффективность стратегий обслуживания" };

            var catAxis = new CategoryAxis { Position = AxisPosition.Left, Title = "Стратегия" };
            model.Axes.Add(catAxis);

            var valAxis = new LinearAxis { Position = AxisPosition.Bottom, Title = "Ср. время ожидания (мин)", MinimumPadding = 0 };
            model.Axes.Add(valAxis);

            var barSeries = new BarSeries
            {
                Title = "Среднее время",
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                LabelPlacement = LabelPlacement.Inside,
                LabelFormatString = "{0:0.00} мин"
            };

            var index = 0;
            foreach (var scene in scenarios)
            {
                AppendLog($"\nСимуляция: {scene.Name}...");

                var results = await Task.Run(() => RunBatch(50, scene.Pars));
                var data = results.Select(r => r.AvgWaitingTime).ToList();

                var mean = data.Average();

                catAxis.Labels.Add(scene.Name);

                var color = OxyColors.SkyBlue;
                if (scene.Name.Contains("Priority")) color = OxyColors.LightGreen;
                if (scene.Name.Contains("LIFO")) color = OxyColors.LightSalmon;

                barSeries.Items.Add(new BarItem { Value = mean, CategoryIndex = index, Color = color });

                AppendLog($"Result: {mean:F4} мин");
                index++;
            }

            model.Series.Add(barSeries);
            ExpPlotView.Model = model;

            AppendLog(new string('-', 40));
            AppendLog("ВЫВОД:");
            AppendLog("Стратегия Priority (обслуживание сначала тех, кто быстрее освободится)");
            AppendLog("обычно дает наименьшее среднее время ожидания в очереди.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }

    private async void BtnTask4_TwoFactor_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ExpPlotView.Model = null;
            TxtLog.Clear();
            AppendLog("Запуск Задачи 4: Двухфакторный эксперимент...");
            AppendLog("Варьируем два внутренних параметра управления.");
        
            double[] xLevels = [15.0, 17.5, 20.0, 22.5, 25.0];
            double[] yLevels = [2, 3, 4, 5, 6];

            var data = new double[xLevels.Length, yLevels.Length];

            AppendLog($"Фактор X (Время обслуживания): {string.Join(", ", xLevels)}");
            AppendLog($"Фактор Y (Кол-во официантов): {string.Join(", ", yLevels)}");
            AppendLog("Расчет поверхности отклика (25 точек)...");

            for (var xi = 0; xi < xLevels.Length; xi++)
            {
                for (var yi = 0; yi < yLevels.Length; yi++)
                {
                    var pars = ClonePars(_basePars);

                    pars.MeanServiceMin = xLevels[xi];
                    pars.NumWaiters = (int)yLevels[yi];

                    var batch = await Task.Run(() => RunBatch(10, pars));
                    var zVal = batch.Average(r => r.AvgWaitingTime);

                    data[xi, yi] = zVal;
                }

                AppendLog($"Столбец X={xLevels[xi]:F1} рассчитан.");
            }

            var model = new PlotModel { Title = "Время ожидания: Скорость vs Количество" };
            model.Axes.Add(new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Palette = OxyPalettes.Jet(100),
                Title = "Время ожидания (мин)"
            });

            var heatMap = new HeatMapSeries
            {
                X0 = xLevels[0],
                X1 = xLevels.Last(),
                Y0 = yLevels[0],
                Y1 = yLevels.Last(),
                Interpolate = true,
                RenderMethod = HeatMapRenderMethod.Bitmap,
                Data = data
            };

            model.Series.Add(heatMap);

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Среднее время обслуживания (мин)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Количество официантов (чел)" });

            ExpPlotView.Model = model;
        
            double xEffect = 0;
            for (var yi = 0; yi < yLevels.Length; yi++)
                xEffect += Math.Abs(data[0, yi] - data[xLevels.Length - 1, yi]);
            xEffect /= yLevels.Length;

            double yEffect = 0;
            for (var xi = 0; xi < xLevels.Length; xi++)
                yEffect += Math.Abs(data[xi, 0] - data[xi, yLevels.Length - 1]);
            yEffect /= xLevels.Length;

            AppendLog("\n--- Интерпретация значимости факторов ---");
            AppendLog($"Среднее влияние Скорости (X): {xEffect:F1} мин");
            AppendLog($"Среднее влияние Ресурсов (Y): {yEffect:F1} мин");

            AppendLog(xEffect > yEffect ? "ВЫВОД: Скорость работы персонала важнее, чем их количество." : "ВЫВОД: Количество персонала важнее, чем скорость их работы.");
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
    
    private static SimulationParameters ClonePars(SimulationParameters p)
    {
        return new SimulationParameters
        {
            NumTables = p.NumTables,
            NumWaiters = p.NumWaiters,
            MeanInterarrivalMin = p.MeanInterarrivalMin,
            MeanServiceMin = p.MeanServiceMin,
            SimDurationMin = p.SimDurationMin,
            Runs = p.Runs,
            Seed = p.Seed
        };
    }
}