namespace CAIMMOD.Laba3.Analysis;

public class AutocorrelationResult
{
    public double C { get; private init; }
    public bool IsSignificant { get; private init; }
    
    public static AutocorrelationResult CalculateAutocorrelation(List<double> data)
    {
        if (data.Count < 2) return new AutocorrelationResult();

        var mean = data.Average();
        var variance = data.Sum(x => Math.Pow(x - mean, 2)) / (data.Count - 1);

        if (variance == 0) return new AutocorrelationResult();

        double covariance = 0;
        for (var i = 0; i < data.Count - 1; i++)
        {
            covariance += (data[i] - mean) * (data[i + 1] - mean);
        }
        covariance /= data.Count - 2;

        var r1 = covariance / variance;
        const double crit = 0.25;

        return new AutocorrelationResult
        {
            C = r1,
            IsSignificant = Math.Abs(r1) >= crit
        };
    }
}