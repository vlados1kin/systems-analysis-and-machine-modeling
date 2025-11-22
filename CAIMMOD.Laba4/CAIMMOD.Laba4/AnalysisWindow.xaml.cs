using System.Text;
using System.Windows;
using CAIMMOD.Laba4.Analysis;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CAIMMOD.Laba4;

public partial class AnalysisWindow
{
    private readonly SimulationParameters _basePars;

    public AnalysisWindow(SimulationParameters parameters)
    {
        InitializeComponent();
        _basePars = parameters;
        TxtStatsLog.Text = $"Окно аналитики открыто.\nБазовые параметры:\n" +
                           $"Столов: {_basePars.NumTables}, Официантов: {_basePars.NumWaiters}\n" +
                           $"Приход: {_basePars.MeanInterarrivalMin}, Обс: {_basePars.MeanServiceMin}\n\n" +
                           $"Выберите задачу слева.";
    }

    private void AppendLog(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            TxtStatsLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            TxtStatsLog.ScrollToEnd();
        });
    }

    private List<SimulationResult> RunBatchSimulation(int runs, SimulationParameters pars)
    {
        var results = new List<SimulationResult>();
        var lockObj = new object();

        Parallel.For(0, runs, i =>
        {
            var sim = new DiscreteEventSimulation(pars, new Random(pars.Seed + i), null);
            var res = sim.Run();
            lock (lockObj) results.Add(res);
        });

        return results;
    }
    
    private async void BtnTask1_ChiSquare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatsPlotView.Model = null;
            TxtStatsLog.Clear();
            AppendLog("Запуск задачи 1 (Хи-квадрат)...");

            const int n = 1000;
            var pars = _basePars;
            var results = await Task.Run(() => RunBatchSimulation(n, pars));

            var data = results.Select(r => r.AvgWaitingTime).OrderBy(x => x).ToList();

            if (data.Count < 20)
            {
                AppendLog("ОШИБКА: Слишком мало данных (<20) для теста.");
                return;
            }


            var mean = data.Average();
            var stdDev = data.StandardDeviation();
            AppendLog($"Выборка N={data.Count}. Mean={mean:F4}, StdDev={stdDev:F4}");

            var normalDist = new Normal(mean, stdDev);
            var rawIntervals = PrepareIntervals(data, normalDist);

            var mergedIntervals = MergeIntervals(rawIntervals);

            double chiSquareCalc = 0;
            var sb = new StringBuilder();
            sb.AppendLine("Интервал Observed Expected (O-E)^2/E");

            foreach (var item in mergedIntervals)
            {
                var contrib = Math.Pow(item.ObservedFrequency - item.ExpectedFrequency, 2) / item.ExpectedFrequency;
                chiSquareCalc += contrib;

                var range = $"[{item.LowerBound:F1}; {item.UpperBound:F1})";
                sb.AppendLine($"{range,-15} {item.ObservedFrequency} {item.ExpectedFrequency:F2} {contrib:F4}");
            }

            AppendLog(sb.ToString());

            var df = mergedIntervals.Count - 1 - 2;
            if (df < 1)
            {
                AppendLog($"ВНИМАНИЕ: Мало степеней свободы ({df}). Принудительно ставим 1.");
                df = 1;
            }

            var chiCrit = ChiSquared.InvCDF(df, 0.95);

            AppendLog($"Хи-квадрат расчетное: {chiSquareCalc:F4}");
            AppendLog($"Хи-квадрат критическое (df={df}): {chiCrit:F4}");

            AppendLog(chiSquareCalc < chiCrit ? "РЕЗУЛЬТАТ: Гипотеза о нормальности ПРИНИМАЕТСЯ." : "РЕЗУЛЬТАТ: Гипотеза о нормальности ОТВЕРГАЕТСЯ.");

            var model = new PlotModel { Title = $"Гистограмма и Нормальное распределение (N={n})" };

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Плотность вероятности" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время ожидания" });

            var histSeries = new HistogramSeries
            {
                FillColor = OxyColors.SkyBlue,
                StrokeColor = OxyColors.Black,
                StrokeThickness = 1,
                Title = "Наблюдения"
            };

            foreach (var item in mergedIntervals)
            {
                var normalizedArea = (double)item.ObservedFrequency / n;
                histSeries.Items.Add(new HistogramItem(item.LowerBound, item.UpperBound, normalizedArea, item.ObservedFrequency));
            }

            model.Series.Add(histSeries);

            var lineSeries = new FunctionSeries(x => normalDist.Density(x), data.Min(), data.Max(), 0.1, "Нормальное распр.")
            {
                Color = OxyColors.Red,
                StrokeThickness = 2
            };
            model.Series.Add(lineSeries);

            StatsPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
    
    private List<IntervalInfo> PrepareIntervals(List<double> data, Normal dist)
    {
        var n = data.Count;
        var k = (int)Math.Ceiling(1 + 3.322 * Math.Log10(n));

        var min = data.Min();
        var max = data.Max();
        var width = (max - min) / k;

        var intervals = new List<IntervalInfo>();

        for (var i = 0; i < k; i++)
        {
            var lower = min + i * width;
            var upper = min + (i + 1) * width;

            var info = new IntervalInfo
            {
                LowerBound = lower, UpperBound = upper,
                ObservedFrequency = i == k - 1 ? data.Count(x => x >= lower && x <= max + 0.000001) : data.Count(x => x >= lower && x < upper)
            };

            var pLower = (i == 0) ? 0 : dist.CumulativeDistribution(lower);
            var pUpper = (i == k - 1) ? 1.0 : dist.CumulativeDistribution(upper);

            info.ExpectedFrequency = (pUpper - pLower) * n;
            intervals.Add(info);
        }

        return intervals;
    }

    private static List<IntervalInfo> MergeIntervals(List<IntervalInfo> raw)
    {
        var merged = new List<IntervalInfo>(raw);
        const double minExpected = 5.0;

        while (merged.Count > 1 && merged.Last().ExpectedFrequency < minExpected)
        {
            var last = merged.Last();
            merged.RemoveAt(merged.Count - 1);

            var prev = merged.Last();
            prev.UpperBound = last.UpperBound;
            prev.ObservedFrequency += last.ObservedFrequency;
            prev.ExpectedFrequency += last.ExpectedFrequency;
        }

        while (merged.Count > 1 && merged.First().ExpectedFrequency < minExpected)
        {
            var first = merged.First();
            merged.RemoveAt(0);

            var next = merged.First();
            next.LowerBound = first.LowerBound;
            next.ObservedFrequency += first.ObservedFrequency;
            next.ExpectedFrequency += first.ExpectedFrequency;
        }

        for (var i = 0; i < merged.Count - 1; i++)
        {
            if (!(merged[i].ExpectedFrequency < minExpected)) continue;
            var current = merged[i];
            var next = merged[i + 1];

            next.LowerBound = current.LowerBound;
            next.ObservedFrequency += current.ObservedFrequency;
            next.ExpectedFrequency += current.ExpectedFrequency;

            merged.RemoveAt(i);
            i--;
        }

        return merged;
    }

    private async void BtnTask2_Estimates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatsPlotView.Model = null;
            TxtStatsLog.Clear();
            AppendLog("Запуск задачи 2 (Оценки N=20)...");

            const int n = 20;

            var results = await Task.Run(() => RunBatchSimulation(n, _basePars));
            var data = results.Select(r => r.AvgWaitingTime).ToList();

            var stats = ConfidenceStats.CalculateStats(data);

            AppendLog($"Среднее: {stats.Mean:F4}");
            AppendLog($"Асимметрия: {stats.Skewness:F4}");
            AppendLog($"Стандартный ДИ: [{stats.LowerStandard:F4}; {stats.UpperStandard:F4}]");
            AppendLog($"ДИ Уилл инка: [{stats.LowerWillink:F4}; {stats.UpperWillink:F4}]");
        
            var model = new PlotModel { Title = "Распределение значений и Доверительные Интервалы" };

            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.BottomCenter,
                LegendOrientation = OxyPlot.Legends.LegendOrientation.Horizontal,
                LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Номер репликации",
                Minimum = 0,
                Maximum = n + 1,
                MajorStep = 1
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Время ожидания (мин)"
            });

            var scatterSeries = new ScatterSeries
            {
                Title = "Значения",
                MarkerType = MarkerType.Circle,
                MarkerSize = 4,
                MarkerFill = OxyColors.Gray,
                MarkerStroke = OxyColors.Black
            };
        
            for (var i = 0; i < stats.Values.Count; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint(i + 1, stats.Values[i]));
            }

            model.Series.Add(scatterSeries);

            var meanLine = new LineSeries
            {
                Title = "Среднее",
                Color = OxyColors.Black,
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid
            };
            meanLine.Points.Add(new DataPoint(0, stats.Mean));
            meanLine.Points.Add(new DataPoint(n + 1, stats.Mean));
            model.Series.Add(meanLine);

            var stdUpperLine = new LineSeries
            {
                Title = "Стандартный ДИ (95%)",
                Color = OxyColors.DodgerBlue,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2
            };
            stdUpperLine.Points.Add(new DataPoint(0, stats.UpperStandard));
            stdUpperLine.Points.Add(new DataPoint(n + 1, stats.UpperStandard));
            model.Series.Add(stdUpperLine);
        
            var stdLowerLine = new LineSeries
            {
                Color = OxyColors.DodgerBlue,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2,
                RenderInLegend = false
            };
            stdLowerLine.Points.Add(new DataPoint(0, stats.LowerStandard));
            stdLowerLine.Points.Add(new DataPoint(n + 1, stats.LowerStandard));
            model.Series.Add(stdLowerLine);


            var wilUpperLine = new LineSeries
            {
                Title = "ДИ Уилл инка (95%)",
                Color = OxyColors.OrangeRed,
                LineStyle = LineStyle.DashDot,
                StrokeThickness = 2
            };
            wilUpperLine.Points.Add(new DataPoint(0, stats.UpperWillink));
            wilUpperLine.Points.Add(new DataPoint(n + 1, stats.UpperWillink));
            model.Series.Add(wilUpperLine);

            var wilLowerLine = new LineSeries
            {
                Color = OxyColors.OrangeRed,
                LineStyle = LineStyle.DashDot,
                StrokeThickness = 2,
                RenderInLegend = false
            };
            wilLowerLine.Points.Add(new DataPoint(0, stats.LowerWillink));
            wilLowerLine.Points.Add(new DataPoint(n + 1, stats.LowerWillink));
            model.Series.Add(wilLowerLine);

            StatsPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
    
    private async void BtnTask3_Accuracy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatsPlotView.Model = null;
            TxtStatsLog.Clear();
            AppendLog("Запуск задачи 3 (Анализ сходимости)...");

            const double targetPrecision = 0.05;
            const int maxReplications = 200;

            var stablePars = new SimulationParameters
            {
                NumTables = _basePars.NumTables, NumWaiters = _basePars.NumWaiters,
                MeanInterarrivalMin = _basePars.MeanInterarrivalMin, MeanServiceMin = _basePars.MeanServiceMin,
                SimDurationMin = Math.Max(_basePars.SimDurationMin, 1000),
                Runs = 1, Seed = _basePars.Seed
            };

            AppendLog($"Генерация базы данных из {maxReplications} репликаций...");
            var allResults = await Task.Run(() => RunBatchSimulation(maxReplications, stablePars));
            var values = allResults.Select(r => r.AvgWaitingTime).ToList();

            var points = new List<DataPoint>();
            int? foundN = null;
            double foundPrecision = 0;
        
            for (var n = 10; n <= maxReplications; n += 10)
            {
                var subset = values.GetRange(0, n);

                var sum = subset.Sum();
                var sumSq = subset.Sum(v => v * v);

                var mean = sum / n;
                var variance = (sumSq - sum * sum / n) / (n - 1);
                if (variance < 0) variance = 0;
                var stdDev = Math.Sqrt(variance);

                var t = StudentT.InvCDF(0, 1, n - 1, 0.975);

                var absError = t * stdDev / Math.Sqrt(n);
                var relError = mean > 1e-9 ? absError / mean : 0;

                points.Add(new DataPoint(n, relError));

                if (foundN == null && relError <= targetPrecision)
                {
                    foundN = n;
                    foundPrecision = relError;
                }
            }

            if (foundN.HasValue)
            {
                AppendLog($"Целевая точность {(targetPrecision * 100):F0}% достигнута (или пройдена) на шаге N = {foundN}");
                AppendLog($"Текущая точность: {foundPrecision:P2}");
            }
            else
            {
                AppendLog($"Точность не достигнута за {maxReplications} прогонов.");
                AppendLog($"Лучшая точность: {points.Last().Y:P2}");
            }

            var model = new PlotModel { Title = "Сходимость относительной погрешности (Шаг 10)" };

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Количество прогонов (N)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Погрешность", StringFormat = "P0" });

            var lineSeries = new LineSeries
            {
                Title = "Погрешность",
                Color = OxyColors.Blue,
                StrokeThickness = 2,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4
            };
            lineSeries.Points.AddRange(points);
            model.Series.Add(lineSeries);

            var limitSeries = new LineSeries
            {
                Title = $"Цель ({targetPrecision:P0})",
                Color = OxyColors.Red,
                LineStyle = LineStyle.Dash,
                StrokeThickness = 2
            };
            limitSeries.Points.Add(new DataPoint(0, targetPrecision));
            limitSeries.Points.Add(new DataPoint(maxReplications, targetPrecision));
            model.Series.Add(limitSeries);

            if (foundN.HasValue)
            {
                var scatter = new ScatterSeries
                {
                    MarkerType = MarkerType.Circle,
                    MarkerSize = 6,
                    MarkerFill = OxyColors.Green,
                    Title = "Точка достижения"
                };
                scatter.Points.Add(new ScatterPoint(foundN.Value, foundPrecision));
                model.Series.Add(scatter);
            }

            StatsPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }

    private async void BtnTask4_Transient_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatsPlotView.Model = null;
            TxtStatsLog.Clear();
            AppendLog("ЧАСТЬ 1: Визуализация динамики (1 длинный прогон)...");

            var longPars = _basePars;
            longPars.SimDurationMin = Math.Max(_basePars.SimDurationMin, 1000);
            longPars.Runs = 1;

            var resOne = (await Task.Run(() => RunBatchSimulation(1, longPars))).First();

            if (resOne.CustomerHistory.Count == 0)
            {
                AppendLog("ОШИБКА: Нет истории клиентов. Добавьте CustomerHistory в SimulationResult.");
                return;
            }

            var history = resOne.CustomerHistory.OrderBy(h => h.Time).ToList();

            var scatterPoints = new List<ScatterPoint>();
            var linePoints = new List<DataPoint>();
            double runningSum = 0;

            for (var i = 0; i < history.Count; i++)
            {
                var t = history[i].Time;
                var val = history[i].Duration;
                scatterPoints.Add(new ScatterPoint(t, val));
                runningSum += val;
                linePoints.Add(new DataPoint(t, runningSum / (i + 1)));
            }

            var model = new PlotModel { Title = "Динамика отклика и Проверка гипотезы", PlotAreaBorderColor = OxyColors.Transparent };
            model.Legends.Add(new OxyPlot.Legends.Legend { LegendPosition = OxyPlot.Legends.LegendPosition.TopCenter });

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Время (мин)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Время в системе" });

            var scatterSeries = new ScatterSeries
            {
                Title = "Клиенты", MarkerType = MarkerType.Circle, MarkerSize = 2, MarkerFill = OxyColor.Parse("#90A4AE")
            };
            scatterSeries.Points.AddRange(scatterPoints);
            model.Series.Add(scatterSeries);

            var lineSeries = new LineSeries
            {
                Title = "Среднее скользящее",
                Color = OxyColor.Parse("#F58231"),
                StrokeThickness = 2,
                LineStyle = LineStyle.Dot
            };
            lineSeries.Points.AddRange(linePoints);
            model.Series.Add(lineSeries);

            StatsPlotView.Model = model;
            AppendLog("График построен.");
            AppendLog(new string('-', 40));
        
            AppendLog("ЧАСТЬ 2: Проверка гипотезы об уменьшении времени...");

            const double t0 = 600.0;
            const double t1 = 400.0;
            const int n = 30;

            AppendLog($"Сравниваем T0={t0} и T1={t1} (N={n})...");

            var pars0 = _basePars;
            pars0.SimDurationMin = t0;
            var batch0 = await Task.Run(() => RunBatchSimulation(n, pars0));
            var data0 = batch0.Select(x => x.AvgWaitingTime).ToList();

            var pars1 = _basePars;
            pars1.SimDurationMin = t1;
            var batch1 = await Task.Run(() => RunBatchSimulation(n, pars1));
            var data1 = batch1.Select(x => x.AvgWaitingTime).ToList();

            var mean0 = data0.Average();
            var var0 = StatMath.Variance(data0);

            var mean1 = data1.Average();
            var var1 = StatMath.Variance(data1);

            AppendLog($"T0: Mean={mean0:F3}, Var={var0:F3}");
            AppendLog($"T1: Mean={mean1:F3}, Var={var1:F3}");

            var tStatNumerator = Math.Abs(mean0 - mean1);
            var tStatDenominator = Math.Sqrt((var0 / n) + (var1 / n));
            var tStat = tStatNumerator / tStatDenominator;

            const int df = 2 * n - 2;
            var tCrit = StatMath.GetTCritical(df);

            AppendLog($"t-статистика: {tStat:F4}");
            AppendLog($"t-критическое: {tCrit:F4}");

            if (tStat < tCrit)
            {
                AppendLog("РЕЗУЛЬТАТ: t_stat < t_crit");
                AppendLog("Гипотеза ПРИНЯТА: Различия статистически незначимы.");
                AppendLog($"Вывод: Время прогона можно сократить до {t1} мин без потери точности.");

                var cutLine = new LineSeries { Title = $"Recommended Cut ({t1})", Color = OxyColors.Green, LineStyle = LineStyle.Dash };
                cutLine.Points.Add(new DataPoint(t1, 0));
                cutLine.Points.Add(new DataPoint(t1, linePoints.Max(p => p.Y) * 1.2));
                model.Series.Add(cutLine);
            }
            else
            {
                AppendLog("РЕЗУЛЬТАТ: t_stat >= t_crit");
                AppendLog("Гипотеза ПРИНЯТА: Различия статистически незначимы.");
                AppendLog($"Вывод: Нельзя сокращать время до {t1} мин, система еще не стабилизировалась.");

                var cutLine = new LineSeries { Title = $"Rejected Cut ({t1})", Color = OxyColors.Red, LineStyle = LineStyle.Dash };
                cutLine.Points.Add(new DataPoint(t1, 0));
                cutLine.Points.Add(new DataPoint(t1, linePoints.Max(p => p.Y) * 1.2));
                model.Series.Add(cutLine);
            }

            model.InvalidatePlot(true);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
    
    private async void BtnTask5_Ergodicity_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatsLog.Clear();
            AppendLog("Запуск задачи 5 (Сравнение на одном графике)...");

            const int n = 30;
            var subRunDuration = _basePars.SimDurationMin;
        
            var iBatch = await Task.Run(() => RunBatchSimulation(n, _basePars));
            var iData = iBatch.Select(x => x.AvgWaitingTime).ToList();

            var longPars = _basePars;
            longPars.SimDurationMin = subRunDuration * n;
            longPars.Runs = 1;
            var longRes = (await Task.Run(() => RunBatchSimulation(1, longPars))).First();

            var contData = new List<double>();
            {
                var history = longRes.CustomerHistory;
                for (var i = 0; i < n; i++)
                {
                    var tStart = i * subRunDuration;
                    var tEnd = (i + 1) * subRunDuration;
                    var subData = history.Where(h => h.Time >= tStart && h.Time < tEnd).Select(h => h.Duration).ToList();
                    contData.Add(subData.Count > 0 ? subData.Average() : 0);
                }
            }

            var mean1 = iData.Average();
            var var1 = StatMath.Variance(iData);
            var mean2 = contData.Average();
            var var2 = StatMath.Variance(contData);

            var fStat = (var1 >= var2) ? var1 / var2 : var2 / var1;
            var fCrit = DistTables.GetFCrit(n - 1, n - 1);
            var eqVars = fStat < fCrit;

            double tStat, tCrit;
            if (eqVars)
            {
                var sp2 = ((n - 1) * var1 + (n - 1) * var2) / (2 * n - 2);
                tStat = Math.Abs(mean1 - mean2) / Math.Sqrt(sp2 * (2.0 / n));
                tCrit = DistTables.GetTCrit(2 * n - 2);
            }
            else
            {
                tStat = Math.Abs(mean1 - mean2) / Math.Sqrt(var1 / n + var2 / n);
                tCrit = DistTables.GetTCrit(n - 1);
            }

            var eqMeans = tStat < tCrit;
            var auto = AutocorrelationResult.CalculateAutocorrelation(contData);

            AppendLog("=== РЕЗУЛЬТАТЫ ТЕСТОВ ===");
            AppendLog($"F-тест: F={fStat:F3} < {fCrit:F3} ? {(eqVars ? "ДА (Дисперсии равны)" : "НЕТ")}");
            AppendLog($"t-тест: t={tStat:F3} < {tCrit:F3} ? {(eqMeans ? "ДА (Средние равны)" : "НЕТ")}");
            AppendLog($"Автокорреляция: r={auto.C:F3} (Значима: {auto.IsSignificant})");

            if (eqMeans && !auto.IsSignificant)
                AppendLog("ИТОГ: Непрерывный прогон ДОПУСТИМ.");
            else
                AppendLog("ИТОГ: Непрерывный прогон НЕ РЕКОМЕНДУЕТСЯ.");
        
            var model = new PlotModel { Title = "Сравнение распределений (Наложение)" };

            var minX = Math.Min(iData.Min(), contData.Min());
            var maxX = Math.Max(iData.Max(), contData.Max());

            minX -= 1;
            maxX += 1;

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Значение отклика", Minimum = minX, Maximum = maxX });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Частота" });

            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.TopRight,
                LegendPlacement = OxyPlot.Legends.LegendPlacement.Inside,
                LegendBackground = OxyColor.FromAColor(200, OxyColors.White),
                LegendBorder = OxyColors.Black,
                LegendBorderThickness = 1
            });

            const int bins = 120;
            var width = (maxX - minX) / bins;

            var s1 = new HistogramSeries
            {
                Title = "Независимые",
                FillColor = OxyColor.FromAColor(150, OxyColors.DodgerBlue),
                StrokeColor = OxyColors.Blue,
                StrokeThickness = 1
            };
            FillHistogramSeries(s1, iData, minX, bins, width);
            model.Series.Add(s1);

            var s2 = new HistogramSeries
            {
                Title = "Непрерывный",
                FillColor = OxyColor.FromAColor(150, OxyColors.OrangeRed),
                StrokeColor = OxyColors.Red,
                StrokeThickness = 1
            };
            FillHistogramSeries(s2, contData, minX, bins, width);
            model.Series.Add(s2);

            StatsPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }

    private static void FillHistogramSeries(HistogramSeries series, List<double> data, double start, int bins, double width)
    {
        for (var i = 0; i < bins; i++)
        {
            var low = start + i * width;
            var high = low + width;

            var count = data.Count(v => v >= low && v < high);

            series.Items.Add(new HistogramItem(low, high, count * width, count));
        }
    }

    private async void BtnTask6_Sensitivity_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatsLog.Clear();
            AppendLog("Запуск задачи 6 (Анализ чувствительности)...");

            var baseVal = _basePars.MeanInterarrivalMin;
            const int steps = 11;
            var minVal = baseVal * 0.8;
            var maxVal = baseVal * 1.2;
            var stepSize = (maxVal - minVal) / (steps - 1);

            var xValues = new List<double>();
            var yValues = new List<double>();

            AppendLog($"Параметр: MeanInterarrivalMin (База = {baseVal:F2})");
            AppendLog($"Диапазон: [1.9; 2.1]");

            for (var i = 0; i < steps; i++)
            {
                var currentVal = minVal + i * stepSize;

                var pars = _basePars;
                pars.MeanInterarrivalMin = currentVal;
                pars.Runs = 1;
                const int subRuns = 10;

                var batch = await Task.Run(() => RunBatchSimulation(subRuns, pars));
                var avgResponse = batch.Average(r => r.AvgWaitingTime);

                xValues.Add(currentVal);
                yValues.Add(avgResponse);

                if (currentVal is >= 1.9 and <= 2.1)
                {
                    AppendLog($"X={currentVal:F2} -> Y={avgResponse:F2}");                
                }
            }

            var baseY = yValues[steps / 2];

            var sens = SensitivityResult.CalculateSensitivity(xValues, yValues, baseVal, baseY);

            AppendLog(new string('-', 40));
            AppendLog($"Коэффициент корреляции R: {sens.Correlation:F4}");
            AppendLog($"Наклон регрессии (b1): {sens.Slope:F4}");
            AppendLog($"Чувствительность: {sens.Conclusion}");

            var model = new PlotModel { Title = $"Чувствительность ({sens.Conclusion})" };

            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Значение параметра (Interarrival)" });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Отклик (WaitTime)" });

            var scatter = new ScatterSeries
            {
                Title = "Эксперимент",
                MarkerType = MarkerType.Circle,
                MarkerSize = 5,
                MarkerFill = OxyColor.Parse("#3498db"),
                MarkerStroke = OxyColors.White
            };
        
            for (var i = 0; i < xValues.Count; i++)
                scatter.Points.Add(new ScatterPoint(xValues[i], yValues[i]));

            model.Series.Add(scatter);

            var regressionLine = new LineSeries
            {
                Title = $"Регрессия (R={sens.Correlation:F2})",
                Color = OxyColor.Parse("#e74c3c"),
                StrokeThickness = 2,
                LineStyle = LineStyle.Solid
            };

            var yMin = sens.Intercept + sens.Slope * minVal;
            var yMax = sens.Intercept + sens.Slope * maxVal;

            regressionLine.Points.Add(new DataPoint(minVal, yMin));
            regressionLine.Points.Add(new DataPoint(maxVal, yMax));

            model.Series.Add(regressionLine);

            StatsPlotView.Model = model;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
        }
    }
}