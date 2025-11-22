namespace CAIMMOD.Laba4.Analysis;

public class ConfidenceStats
{
    public double Mean { get; private init; }
    public double LowerStandard { get; private init; }
    public double UpperStandard { get; private init; }
    public double LowerWillink { get; private init; }
    public double UpperWillink { get; private init; }
    public double Skewness { get; private init; }
    public List<double> Values { get; private init; } = [];
    
    public static ConfidenceStats CalculateStats(List<double> data)
    {
        var n = data.Count;
        var mean = data.Average();
        var sumSqDiff = data.Sum(x => Math.Pow(x - mean, 2));
        var variance = sumSqDiff / (n - 1);
        var stdDev = Math.Sqrt(variance);
        var sem = stdDev / Math.Sqrt(n);
        
        var tValue = GetTCritical(n - 1);
    
        var margin = tValue * sem;
        var lowerStd = mean - margin;
        var upperStd = mean + margin;

        var mu3 = data.Sum(x => Math.Pow(x - mean, 3)) * n / ((n - 1) * (n - 2));
        var skewness = mu3 / Math.Pow(stdDev, 3);
        var a = mu3 / (6 * Math.Sqrt(n) * Math.Pow(stdDev, 3));

        double gLeft = tValue, gRight = -tValue;
        if (Math.Abs(a) > 1e-10)
        {
            var term1 = 1 + 6 * a * (tValue - a);
            var term2 = 1 + 6 * a * (-tValue - a);
            if (term1 > 0) gLeft = (Math.Pow(term1, 1.0 / 3) - 1) * 0.5 / a;
            if (term2 > 0) gRight = (Math.Pow(term2, 1.0 / 3) - 1) * 0.5 / a;
        }

        var lowerWillink = mean - gLeft * sem;
        var upperWillink = mean - gRight * sem;

        return new ConfidenceStats
        {
            Mean = mean,
            LowerStandard = lowerStd,
            UpperStandard = upperStd,
            LowerWillink = lowerWillink,
            UpperWillink = upperWillink,
            Skewness = skewness,
            Values = data
        };
    }
    
    private static double GetTCritical(int df)
    {
        var tTable = new Dictionary<int, double>
        {
            { 1, 12.706 }, { 2, 4.303 }, { 3, 3.182 }, { 4, 2.776 }, { 5, 2.571 },
            { 6, 2.447 }, { 7, 2.365 }, { 8, 2.306 }, { 9, 2.262 }, { 10, 2.228 },
            { 11, 2.201 }, { 12, 2.179 }, { 13, 2.160 }, { 14, 2.145 }, { 15, 2.131 },
            { 16, 2.120 }, { 17, 2.110 }, { 18, 2.101 }, { 19, 2.093 }, { 20, 2.086 },
            { 21, 2.080 }, { 22, 2.074 }, { 23, 2.069 }, { 24, 2.064 }, { 25, 2.060 },
            { 26, 2.056 }, { 27, 2.052 }, { 28, 2.048 }, { 29, 2.045 }, { 30, 2.042 }
        };
        return tTable.GetValueOrDefault(df, 1.96);
    }
}