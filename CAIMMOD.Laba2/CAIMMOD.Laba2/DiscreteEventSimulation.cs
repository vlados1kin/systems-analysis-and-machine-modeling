using CAIMMOD.Laba2.Models;

namespace CAIMMOD.Laba2;

public class DiscreteEventSimulation
{
    private readonly SimulationParameters _pars;
    private readonly Random _rng;
    private readonly Action<string>? _traceLog;

    private readonly PriorityQueue<Event, double> _eventQueue;
    private readonly List<Table> _tables;
    private readonly List<Waiter> _waiters;
    
    private readonly List<(double t, double occupiedFraction)> _occupiedTimeline = [];
    private readonly List<(double t, double util)> _utilTimeline = [];
    private readonly List<(double t, int)> _cumulativeServedTimeline = [];

    private readonly List<(double t, double)> _avgWaitTimeline = [];

    private int _totalArrivals;
    private int _totalLost;
    private int _totalServed;
    private readonly List<double> _waitingTimes = [];

    private double Normal(double mean, double stdDev)
    {
        var u1 = 1.0 - _rng.NextDouble();
        var u2 = 1.0 - _rng.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    public DiscreteEventSimulation(SimulationParameters parameters, Random random, Action<string>? trace)
    {
        _pars = parameters;
        _rng = random;
        _traceLog = trace;
        _eventQueue = new PriorityQueue<Event, double>();
        _tables = [];
        _waiters = [];
        for (var i = 0; i < _pars.NumTables; i++) _tables.Add(new Table { Id = i + 1 });
        for (var i = 0; i < _pars.NumWaiters; i++) _waiters.Add(new Waiter { Id = i + 1 });
    }

    private double Exp(double mean)
    {
        var u = _rng.NextDouble();
        if (u < 1e-12) u = 1e-12;
        return -mean * Math.Log(u);
    }

    private int GenGroupSize()
    {
        return _rng.Next(SimulationParameters.GroupMinSize, SimulationParameters.GroupMaxSize + 1);
    }

    public SimulationResult Run()
    {
        var first = Exp(_pars.MeanInterarrivalMin);
        _eventQueue.Enqueue(new Event(first, EventType.Arrival), first);

        var previousTime = 0.0;
        var prevUtil = 0.0;
        var utilArea = 0.0;

        while (_eventQueue.Count > 0)
        {
            var e = _eventQueue.Dequeue();
            var currentTime = e.Time;

            if (currentTime > _pars.SimDurationMin) break;

            var delta = currentTime - previousTime;
            if (delta > 0) utilArea += prevUtil * delta;

            var occupied = _tables.Count(x => x.IsOccupied);
            var occupiedFraction = _pars.NumTables > 0 ? (double)occupied / _pars.NumTables : 0.0;
            var busyCount = _waiters.Count(w => w.IsBusy);
            var utilNow = _pars.NumWaiters > 0 ? (double)busyCount / _pars.NumWaiters : 0.0;
            var servedSoFar = _totalServed;
            var avgWaitDiscreteNow = _waitingTimes.Count > 0 ? _waitingTimes.Average() : 0;

            _occupiedTimeline.Add((currentTime, occupiedFraction));
            _utilTimeline.Add((currentTime, utilNow));
            _cumulativeServedTimeline.Add((currentTime, servedSoFar));
            _avgWaitTimeline.Add((currentTime, avgWaitDiscreteNow));

            if (e.Type == EventType.Arrival)
            {
                HandleArrival(currentTime);
            }
            else if (e.Type == EventType.ServiceEnd)
            {
                HandleServiceEnd(e, currentTime);
            }
            else if (e.Type == EventType.ServiceStartWaiting)
            {
                HandleServiceStartWaiting(e, currentTime);
            }

            var occupiedAfter = _tables.Count(x => x.IsOccupied);
            var occupiedFractionAfter = _pars.NumTables > 0 ? (double)occupiedAfter / _pars.NumTables : 0.0;
            var busyAfter = _waiters.Count(w => w.IsBusy);
            var utilAfter = _pars.NumWaiters > 0 ? (double)busyAfter / _pars.NumWaiters : 0.0;
            var avgWaitDiscreteAfter = _waitingTimes.Count > 0 ? _waitingTimes.Average() : 0;

            if (Math.Abs(utilAfter - utilNow) > 1e-12 || Math.Abs(occupiedFractionAfter - occupiedFraction) > 1e-12)
            {
                _occupiedTimeline.Add((currentTime, occupiedFractionAfter));
                _utilTimeline.Add((currentTime, utilAfter));
                _cumulativeServedTimeline.Add((currentTime, _totalServed));
                _avgWaitTimeline.Add((currentTime, avgWaitDiscreteAfter));
            }

            previousTime = currentTime;
            prevUtil = utilAfter;
        }

        var simEnd = _pars.SimDurationMin;
        var dtLast = simEnd - previousTime;
        if (dtLast > 0) utilArea += prevUtil * dtLast;

        _occupiedTimeline.Add((simEnd, _tables.Count(x => x.IsOccupied) / (double)_pars.NumTables));
        _utilTimeline.Add((simEnd, _pars.NumWaiters > 0 ? _waiters.Count(w => w.IsBusy) / (double)_pars.NumWaiters : 0.0));
        _cumulativeServedTimeline.Add((simEnd, _totalServed));
        var finalAvgWaitDiscrete = _waitingTimes.Count > 0 ? _waitingTimes.Average() : 0;
        _avgWaitTimeline.Add((simEnd, finalAvgWaitDiscrete));
        
        return new SimulationResult
        {
            OccupiedTimeline = _occupiedTimeline,
            UtilizationTimeline = _utilTimeline,
            CumulativeServedTimeline = _cumulativeServedTimeline,
            AvgWaitTimeline = _avgWaitTimeline,
            TotalArrivals = _totalArrivals,
            TotalLost = _totalLost,
            TotalServed = _totalServed,
            AvgWaitingTime = _waitingTimes.Count > 0 ? _waitingTimes.Average() : 0.0,
            FractionLost = _totalArrivals > 0 ? (double)_totalLost / _totalArrivals : 0.0,
            AvgUtilization = simEnd > 0 ? utilArea / simEnd : 0.0
        };
    }
    
    private void HandleArrival(double now)
    {
        _totalArrivals++;
        var group = new Group(_totalArrivals, GenGroupSize(), now);
        _traceLog($"Прибытие группы {group.Id} (size={group.Size}) в t={now:F3}");

        var freeTable = _tables.FirstOrDefault(t => !t.IsOccupied);
        if (freeTable == null)
        {
            _totalLost++;
            _traceLog($"Группа {group.Id} ушла — нет свободных столов");
        }
        else
        {
            freeTable.IsOccupied = true;
            freeTable.OccupyingGroup = group;
            _traceLog($"Группа {group.Id} заняла стол {freeTable.Id} в t={now:F3}");

            var freeWaiter = _waiters.FirstOrDefault(w => !w.IsBusy);
            if (freeWaiter != null)
            {
                freeWaiter.IsBusy = true;
                group.ServiceStartTime = now;
                var wait = group.ServiceStartTime - group.ArrivalTime;
                _waitingTimes.Add(wait);
                var serviceTime = Math.Max(0.1, Normal(_pars.MeanServiceMin, _pars.MeanServiceMin * 0.2));
                var end = now + serviceTime;
                _eventQueue.Enqueue(new Event(end, EventType.ServiceEnd, group, freeWaiter), end);
                _traceLog($"Официант {freeWaiter.Id} начал обслуживание группы {group.Id} в t={now:F3}, длительность {serviceTime:F3}");
            }
            else
            {
                if (SimulationParameters.WaitIfNoWaiter)
                {
                    _traceLog($"Группа {group.Id} ожидает официанта у стола {freeTable.Id}");
                }
                else
                {
                    freeTable.IsOccupied = false;
                    freeTable.OccupyingGroup = null;
                    _totalLost++;
                    _traceLog($"Группа {group.Id} ушла — политика не позволяет ждать официанта");
                }
            }
        }

        var next = now + Exp(_pars.MeanInterarrivalMin);
        if (next <= _pars.SimDurationMin)
        {
            _eventQueue.Enqueue(new Event(next, EventType.Arrival), next);
        }
    }

    private void HandleServiceEnd(Event e, double now)
    {
        var waiter = e.AssignedWaiter;
        var group = e.Group;
        var table = _tables.FirstOrDefault(t => t.IsOccupied && t.OccupyingGroup == group);
        if (table != null)
        {
            table.IsOccupied = false;
            table.OccupyingGroup = null;
        }

        _totalServed++;
        if (waiter == null || group == null) return;
        _traceLog($"Официант {waiter.Id} закончил обслуживание группы {group.Id} в t={now:F3}");

        waiter.IsBusy = false;

        var waitingTable = _tables.FirstOrDefault(t => t is { IsOccupied: true, OccupyingGroup.ServiceStartTime: double.NaN });
        if (waitingTable != null)
        {
            var occupyingGroup = waitingTable.OccupyingGroup;
            waiter.IsBusy = true;
            if (occupyingGroup == null) return;
            occupyingGroup.ServiceStartTime = now;
            var wait = occupyingGroup.ServiceStartTime - occupyingGroup.ArrivalTime;
            _waitingTimes.Add(wait);
            var serviceTime = Math.Max(0.1, Normal(_pars.MeanServiceMin, _pars.MeanServiceMin * 0.2));
            var end = now + serviceTime;
            _eventQueue.Enqueue(new Event(end, EventType.ServiceEnd, occupyingGroup, waiter), end);
            _traceLog($"Официант {waiter.Id} начал обслуживание ожидающей группы {occupyingGroup.Id} в t={now:F3}, длительность {serviceTime:F3}");
        }
        else
        {
            _traceLog($"Официант {waiter.Id} свободен в t={now:F3}");
        }
    }

    private void HandleServiceStartWaiting(Event e, double now)
    {
        var group = e.Group;
        if (group == null) return;
        if (!double.IsNaN(group.ServiceStartTime))
        {
            _traceLog($"ServiceStartWaiting для группы {group.Id} в t={now:F3} проигнорирован — группа уже обслуживается");
            return;
        }

        var freeWaiter = _waiters.FirstOrDefault(w => !w.IsBusy);
        if (freeWaiter != null)
        {
            var table = _tables.FirstOrDefault(t => t.IsOccupied && t.OccupyingGroup == group);
            if (table == null)
            {
                _traceLog($"ServiceStartWaiting: группа {group.Id} больше не за столом в t={now:F3}");
                return;
            }

            freeWaiter.IsBusy = true;
            group.ServiceStartTime = now;
            var wait = group.ServiceStartTime - group.ArrivalTime;
            _waitingTimes.Add(wait);
            var serviceTime = Math.Max(0.1, Normal(_pars.MeanServiceMin, _pars.MeanServiceMin * 0.2));
            var end = now + serviceTime;
            _eventQueue.Enqueue(new Event(end, EventType.ServiceEnd, group, freeWaiter), end);
            _traceLog($"(ServiceStartWaiting) Официант {freeWaiter.Id} начал обслуживание группы {group.Id} в t={now:F3}, длительность {serviceTime:F3}");
        }
        else
        {
            _traceLog($"ServiceStartWaiting: в t={now:F3} свободного официанта не найдено для группы {group.Id}");
        }
    }
}