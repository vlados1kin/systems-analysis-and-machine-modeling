namespace CAIMMOD.Laba4.Analysis;

public static class DistTables
{
    private static readonly Dictionary<int, double> Values = new()
    {
        { 10, 2.228 }, { 15, 2.131 }, { 20, 2.086 }, { 25, 2.060 }, { 29, 2.045 },
        { 30, 2.042 }, { 40, 2.021 }, { 50, 2.009 }, { 60, 2.000 }, { 80, 1.990 },
        { 99, 1.984 }, { 100, 1.984 }, { 120, 1.980 }, { 198, 1.972 }, { 1998, 1.961 }
    };

    public static double GetTCrit(int df)
    {
        var key = Values.Keys.OrderBy(k => Math.Abs(k - df)).First();
        return Values[key];
    }

    private static readonly Dictionary<(int, int), double> FValues = new()
    {
        { (29, 29), 1.85 }, { (30, 30), 1.84 }, { (40, 40), 1.69 },
        { (50, 50), 1.60 }, { (60, 60), 1.53 }, { (100, 100), 1.39 },
        { (30, 40), 1.74 }, { (40, 30), 1.87 }, { (50, 40), 1.66 }, { (40, 50), 1.56 }
    };

    public static double GetFCrit(int df1, int df2)
    {
        var key = FValues.Keys.OrderBy(k => Math.Abs(k.Item1 - df1) + Math.Abs(k.Item2 - df2)).First();
        return FValues[key];
    }
}

public static class StatMath
{
    public static double Variance(List<double> values)
    {
        var mean = values.Average();
        return values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
    }

    public static double GetTCritical(int df)
    {
        return df switch
        {
            < 1 => 12.7,
            <= 10 => 2.23,
            <= 20 => 2.09,
            <= 30 => 2.04,
            <= 60 => 2.00,
            _ => 1.96
        };
    }
}