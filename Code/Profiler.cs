using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

public class Profiler
{
    public class Timer : IDisposable
    {
        [Required] public Stopwatch Watch;
        [Required] public Profiler Parent;
        public void Dispose()
        {
            Watch.Stop();
            Parent.Samples.Enqueue( Watch.Elapsed.Nanoseconds );
            while ( Parent.Samples.Count > Parent.SampleCount )
            {
                Parent.Samples.Dequeue();
            }
        }
    }

    // Total number of samples to keep track of.
    public int SampleCount = 10000;

    public Queue<long> Samples;

    public Profiler( int sampleCount = 10000 )
    {
        this.SampleCount = sampleCount;
        Samples = new( sampleCount );
    }

    public Timer Push()
    {
        var watch = new Stopwatch();
        watch.Start();
        return new()
        {
            Watch = watch,
            Parent = this
        };
    }

}