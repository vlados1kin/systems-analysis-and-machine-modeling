namespace CAIMMOD.Main;

public class LemerGenerator
{
    private readonly long _a;
    private readonly long _c;
    private readonly long _m;
    private long _state;
    
    public LemerGenerator(long seed, long a, long c, long m)
    {
        _a = a;
        _c = c;
        _m = m;
        _state = seed % m;
        if (_state < 0) _state += m;
    }

    private int NextInt()
    {
        _state = (_a * _state + _c) % _m;
        return (int)_state;
    }

    public double NextDouble()
    {
        return NextInt() / (double)_m;
    }
}