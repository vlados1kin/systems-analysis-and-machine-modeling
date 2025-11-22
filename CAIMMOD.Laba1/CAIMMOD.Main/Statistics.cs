using Accord.Statistics.Testing;
using Accord.Statistics.Distributions.Univariate;
using MathNet.Numerics.Distributions;

namespace CAIMMOD.Main;

public static class Statistics
{
    public static (int[] counts, double[] binEdges) Histogram(double[] samples, int bins)
    {
        var n = samples.Length;
        var counts = new int[bins];
        var edges = new double[bins + 1];
        for (var i = 0; i <= bins; i++) edges[i] = (double)i / bins;
        for (var i = 0; i < n; i++)
        {
            var v = samples[i];
            var idx = (int)Math.Floor(v * bins);
            if (idx < 0) idx = 0;
            if (idx >= bins) idx = bins - 1;
            counts[idx]++;
        }

        return (counts, edges);
    }

    public static (double chiStat, int df, double pValue) ChiSquareTest(int[] counts)
    {
        var k = counts.Length;
        var n = counts.Sum();
        if (k <= 0 || n <= 0) return (0.0, Math.Max(0, k - 1), 1.0);

        var expected = (double)n / k;
        var chi = 0.0;
        for (var i = 0; i < k; i++)
        {
            var d = counts[i] - expected;
            chi += d * d / expected;
        }

        var df = Math.Max(0, k - 1); 

        var cdf = ChiSquared.CDF(df, chi);
        var pUpper = 1.0 - cdf;
        if (double.IsNaN(pUpper)) pUpper = 0.0;
        if (pUpper < 0.0) pUpper = 0.0;
        if (pUpper > 1.0) pUpper = 1.0;
        return (chi, df, pUpper);
    }
    
    public static (double DplusScaled, double DminusScaled, double DScaled, double pValue, int idxPlus, int idxMinus, double xPlus, double xMinus, double DplusRaw, double DminusRaw) KsTestUniform(double[] samples)
    {
        var n = samples.Length;
        if (n == 0) return (0, 0, 0, 1.0, -1, -1, double.NaN, double.NaN, 0.0, 0.0);

        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);

        var plusRaw = 0.0;  
        var idxPlus = -1;
        var minusRaw = 0.0; 
        var idxMinus = -1;

        for (var i = 0; i < n; i++)
        {
            var xi = sorted[i];
            var ecdfI = (double)(i + 1) / n;  
            var ecdfPrev = (double)i / n;     

            var plus = ecdfI - xi;           
            var minus = xi - ecdfPrev;       

            if (plus > plusRaw)
            {
                plusRaw = plus;
                idxPlus = i;
            }

            if (minus > minusRaw)
            {
                minusRaw = minus;
                idxMinus = i;
            }
        }

        var sqrtN = Math.Sqrt(n);
        var plusScaled = plusRaw * sqrtN;
        var minusScaled = minusRaw * sqrtN;
        var dScaled = Math.Max(plusScaled, minusScaled);

        var uniform = new UniformContinuousDistribution(0.0, 1.0);
        var ks = new KolmogorovSmirnovTest(samples, uniform);
        var p = ks.PValue;
        if (double.IsNaN(p)) p = 0.0;
        if (p < 0.0) p = 0.0;
        if (p > 1.0) p = 1.0;

        var xPlus = idxPlus >= 0 ? sorted[idxPlus] : double.NaN;
        var xMinus = idxMinus >= 0 ? sorted[idxMinus] : double.NaN;

        return (plusScaled, minusScaled, dScaled, p, idxPlus, idxMinus, xPlus, xMinus, plusRaw, minusRaw);
    }
}