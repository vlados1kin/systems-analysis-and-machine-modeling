namespace CAIMMOD.Laba4.Analysis;

public class SensitivityResult
{
    public double Slope { get; private init; }
    public double Intercept { get; private init; }
    public double Correlation { get; private init; }
    public string? Conclusion { get; private init; }
    
    public static SensitivityResult CalculateSensitivity(List<double> x, List<double> y, double baseX, double baseY)
    {
        var n = x.Count;
        var sumX = x.Sum();
        var sumY = y.Sum();
        var sumXy = x.Zip(y, (a, b) => a * b).Sum();
        var sumX2 = x.Sum(a => a * a);
        var sumY2 = y.Sum(a => a * a);

        var slope = (n * sumXy - sumX * sumY) / (n * sumX2 - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        var rNumerator = n * sumXy - sumX * sumY;
        var rDenominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        var r = rDenominator == 0 ? 0 : rNumerator / rDenominator;

        var threshold = 0.05 * baseY / baseX;
    
        var isSensitive = Math.Abs(r) > 0.5 && Math.Abs(slope) > threshold;

        return new SensitivityResult
        {
            Slope = slope,
            Intercept = intercept,
            Correlation = r,
            Conclusion = isSensitive ? "Высокая" : "Низкая"
        };
    }
}