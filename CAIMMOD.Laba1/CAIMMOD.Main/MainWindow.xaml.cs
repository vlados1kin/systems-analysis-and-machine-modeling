using global::System.Windows;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CAIMMOD.Main;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void GenerateBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var seed = int.Parse(SeedBox.Text);
            var a = long.Parse(Abox.Text);
            var c = long.Parse(CBox.Text);
            var m = long.Parse(MBox.Text);
            var n = int.Parse(NBox.Text);
            var bins = int.Parse(BinsBox.Text);

            var gen = new LemerGenerator(seed, a, c, m);
            var samples = new double[n];
            for (var i = 0; i < n; i++) samples[i] = gen.NextDouble();

            var (counts, edges) = Statistics.Histogram(samples, bins);

            var (chiStat, df, chiP) = Statistics.ChiSquareTest(counts);

            var (ksDplusScaled, ksDominusScaled, _, ksP, _, _, xPlus, xMinus, ksDplusRaw, ksDminusRaw) = Statistics.KsTestUniform(samples);

            DrawHistogram(counts, edges);
            DrawCdf(samples, xPlus, ksDplusRaw, xMinus, ksDminusRaw, ksDplusScaled, ksDominusScaled);

            ChiResult.Text = $"χ² = {chiStat:F4} (df={df}), p-value={chiP:F6}";
            KsResult.Text = $"КС: Kn+={ksDplusScaled:F6}, Kn-={ksDominusScaled:F6}, p-value={ksP:F6}";

            var chiConclusion = chiP > 0.05 ? "Не отвергаем гипотезу по χ²." : "Отвергаем гипотезу по χ².";
            var ksConclusion = ksP > 0.05 ? "Не отвергаем гипотезу по КС." : "Отвергаем гипотезу по КС.";
            Conclusion.Text = $"{chiConclusion} {ksConclusion}";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message);
        }
    }

    private void DrawHistogram(int[] counts, double[] edges)
    {
        var model = new PlotModel { Title = "Гистограмма" };

        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Minimum = 0, Maximum = 1, Title = "Value" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Count" });

        var rbs = new RectangleBarSeries { StrokeThickness = 0.5 };
        for (var i = 0; i < counts.Length; i++)
        {
            var x0 = edges[i];
            var x1 = edges[i + 1];
            double y1 = counts[i];
            rbs.Items.Add(new RectangleBarItem(x0, 0, x1, y1));
        }

        model.Series.Add(rbs);

        HistogramPlot.Model = model;
    }

    private void DrawCdf(double[] samples, double? xPlus = null, double? dPlusRaw = null, double? xMinus = null, double? dMinusRaw = null, double? ksDplusScaled = null, double? ksDminusScaled = null)
    {
        var model = new PlotModel { Title = "Эмпирическая CDF и теоретическая (y=x)" };
        var n = samples.Length;
        if (n == 0)
        {
            CdfPlot.Model = model;
            return;
        }

        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);

        var ecdf = new LineSeries
        {
            Title = "ECDF (ступенчатая)",
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid
        };

        var prevY = 0.0;
        ecdf.Points.Add(new DataPoint(0.0, 0.0));
        for (var i = 0; i < n; i++)
        {
            var x = sorted[i];
            var y = (double)(i + 1) / n;
            ecdf.Points.Add(new DataPoint(x, prevY));
            ecdf.Points.Add(new DataPoint(x, y));
            prevY = y;
        }

        if (prevY < 1.0)
            ecdf.Points.Add(new DataPoint(1.0, prevY));

        var theory = new LineSeries
        {
            Title = "Theoretical (y=x)",
            StrokeThickness = 2,
            LineStyle = LineStyle.Dash
        };
        theory.Points.Add(new DataPoint(0, 0));
        theory.Points.Add(new DataPoint(1, 1));

        model.Series.Add(ecdf);
        model.Series.Add(theory);
        
        if (xPlus.HasValue && dPlusRaw.HasValue && !double.IsNaN(xPlus.Value))
        {
            var x = xPlus.Value;
            var yEcdf = x + dPlusRaw.Value;

            var segPlus = new LineSeries { Color = OxyColors.Red, StrokeThickness = 2 };
            segPlus.Points.Add(new DataPoint(x, x));
            segPlus.Points.Add(new DataPoint(x, yEcdf));
            model.Series.Add(segPlus);

            var markerPlus = new ScatterSeries
                { MarkerType = MarkerType.Circle, MarkerSize = 3, MarkerFill = OxyColors.Red };
            markerPlus.Points.Add(new ScatterPoint(x, yEcdf));
            model.Series.Add(markerPlus);

            var labelPlus = ksDplusScaled.HasValue ? $"Kn+ = {ksDplusScaled.Value:F6}" : $"Kn+ (raw scaled) = {(dPlusRaw.Value * Math.Sqrt(n)):F6}";

            var textPlus = new TextAnnotation
            {
                Text = labelPlus,
                TextPosition = new DataPoint(x, (x + yEcdf) / 2.0),
                Stroke = OxyColors.Undefined,
                TextColor = OxyColors.Red,
                FontWeight = OxyPlot.FontWeights.Bold
            };
            model.Annotations.Add(textPlus);
        }

        if (xMinus.HasValue && dMinusRaw.HasValue && !double.IsNaN(xMinus.Value))
        {
            var x = xMinus.Value;
            var yEcdfLeft = x - dMinusRaw.Value;

            var segMinus = new LineSeries { Color = OxyColors.Blue, StrokeThickness = 2 };
            segMinus.Points.Add(new DataPoint(x, yEcdfLeft));
            segMinus.Points.Add(new DataPoint(x, x));
            model.Series.Add(segMinus);

            var markerMinus = new ScatterSeries { MarkerType = MarkerType.Circle, MarkerSize = 3, MarkerFill = OxyColors.Blue };
            markerMinus.Points.Add(new ScatterPoint(x, yEcdfLeft));
            model.Series.Add(markerMinus);

            var labelMinus = ksDminusScaled.HasValue ? $"Kn- = {ksDminusScaled.Value:F6}" : $"Kn- (raw scaled) = {(dMinusRaw.Value * Math.Sqrt(n)):F6}";

            var textMinus = new TextAnnotation
            {
                Text = labelMinus,
                TextPosition = new DataPoint(x, (yEcdfLeft + x) / 2.0),
                Stroke = OxyColors.Undefined,
                TextColor = OxyColors.Blue,
                FontWeight = OxyPlot.FontWeights.Bold
            };
            model.Annotations.Add(textMinus);
        }

        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Minimum = 0, Maximum = 1, Title = "Value" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = 0, Maximum = 1, Title = "CDF" });

        CdfPlot.Model = model;
    }
}
