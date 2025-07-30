using TWClients.JsonRpcMessages;

namespace TWClients;

public class PerformanceTesting
{
    /*private static long[] logTimeTest(int n, String msg) {long time=0,space=0;
        for (int i = n; i>0; i--) {
            long[] thisTime = timeTest( ()->{TriggerwareClient.log(msg);});
            time += thisTime[0];  space += thisTime[1];
        }
        return new long[] {time/n, space/n};
    }*/

    private static IPreparedQuery TestPreparedQuery;
    public static long[]? MeasurementTime { get; private set; }

    private static long[] TimeTest(TriggerwareClient twClient, Action toTime)
    {
        TwRuntimeMeasure origTime = null;
        TwRuntimeMeasure finalTime = null;
        try
        {
            origTime = twClient.Runtime();
        }
        catch (JsonRpcException e1)
        {
            return null;
        }

        try
        {
            toTime();
        }
        catch (Exception e)
        {
            return null;
        }

        try
        {
            finalTime = twClient.Runtime();
        }
        catch (Exception e1)
        {
            return null;
        }

        var runTime = finalTime.RunTime - origTime.RunTime;
        var gcTime = finalTime.GcTime - origTime.GcTime;
        var bytes = finalTime.Bytes - origTime.Bytes;
        long[] measured = { runTime - gcTime, bytes };
        if (MeasurementTime == null) return measured;

        measured[0] -= MeasurementTime[0];
        measured[1] -= MeasurementTime[1];

        return measured;
    }

    private static long[] NoopTimeTest(TriggerwareClient twClient, int n)
    {
        long time = 0, space = 0;
        for (var i = n; i > 0; i--)
        {
            var thisTime = TimeTest(twClient, twClient.Noop);
            time += thisTime[0];
            space += thisTime[1];
        }

        return new[] { time / n, space / n };
    }

    private static long[] MeasurementTimeTest(TriggerwareClient twClient, int n)
    {
        long time = 0, space = 0;
        for (var i = n; i > 0; i--)
        {
            var thisTime = TimeTest(twClient, () => { });
            time += thisTime[0];
            space += thisTime[1];
        }

        MeasurementTime = new[] { time / n, space / n };
        return MeasurementTime;
    }

    private static long[] PqTimeTest(TriggerwareClient twClient)
    {
        return TimeTest(twClient, () =>
        {
            TestPreparedQuery =
                new PreparedQuery<int[]>("SELECT * FROM r2test WHERE col1 >=:col1Min AND col2<=:col2Max", "AP5",
                    twClient);
        });
    }

    private static void DebugTest(TriggerwareClient twClient)
    {
        var qs = twClient.CreateQuery<int[]>();
        qs.SetFetchSize(null);
        try
        {
            var rs = qs.ExecuteQueryAsync("SELECT * FROM r2test WHERE col1 >=11 AND col2<=15", "AP5").Result;
            var seen = 0;
            while (rs.Next())
            {
                var ip = rs.Get();
                seen++;
            }

            seen = seen;
        }
        catch (TimeoutException e)
        {
        }

        var pq = new PreparedQuery<int[]>("SELECT * FROM r2test WHERE col1 >=:col1Min AND col2<=:col2Max", "AP5",
            twClient);
        pq.FetchSize = null;
        pq.ClearParameters();
        pq.SetParameter("col1Min", 11);
        pq.SetParameter("col2Max", 15);
        try
        {
            var rs = pq.ExecuteQuery();
            var seen = 0;
            while (rs.Next())
            {
                var tuple = rs.Get();
                seen++;
            }

            seen = seen;
        }
        catch (TimeoutException tex)
        {
        }
    }

    private static long[] PqcTimeTest(TriggerwareClient twClient)
    {
        return TimeTest(twClient, () => { TestPreparedQuery.Close(); });
    }

    private static long[] PqUsageTest<T>(TriggerwareClient twClient, int howmany, PreparedQuery<T> pq, int c1min,
        int c2max)
    {
        return TimeTest(twClient, () =>
        {
            pq.FetchSize = null;
            for (var count = howmany; count > 0; count--)
            {
                pq.ClearParameters();
                pq.SetParameter("col1Min", c1min);
                pq.SetParameter("col2Max", c2max);
                try
                {
                    var rs = pq.ExecuteQuery();
                    var seen = 0;
                    while (rs.Next())
                    {
                        var tuple = rs.Get();
                        seen++;
                    }

                    seen = seen;
                }
                catch (Exception e)
                {
                    Logging.Log("PqUsageTest problem: " + e.Message);
                }
            }
        });
    }

    public static void testPreparedQuery(TriggerwareClient twClient)
    {
        //throws JRPCException, MiddlewareException, InterruptedException, ExecutionException {
        var origLogger = Logging.Logger = Logging.EmptyLogger;
        Logging.Logger = origLogger;
        MeasurementTimeTest(twClient, 20);
        var noopTime = NoopTimeTest(twClient, 20);
        Logging.Logger = origLogger;
        if (noopTime != null)
            Logging.Log(
                null,
                "pairs are [usec, bytes]. measurement =" + MeasurementTime[0] + ", " + MeasurementTime[1] + "noop = " +
                noopTime[0] + ", " + noopTime[1]
            );
        foreach (var repeatCount in
                 new[] { 5, 30, 100 })
        {
            origLogger = Logging.Logger = Logging.EmptyLogger;
            var pqtime = PqTimeTest(twClient);
            var pqutime = PqUsageTest(twClient, repeatCount, (PreparedQuery<int[]>)TestPreparedQuery, 11, 15);
            var pqctime = PqcTimeTest(twClient);
            Logging.Logger = origLogger;
            Logging.Log("pairs are [usec, bytes]. " +
                        "preparing = " + pqtime[0] + ", " + pqtime[1] + " generating = " + pqutime[0] + ", " +
                        pqutime[1] + " closing = " + pqctime[0] + ", " + pqctime[1]);
        }

        Logging.Logger = origLogger;
    }
}