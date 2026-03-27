using System.Collections.Concurrent;

namespace CapstoneUdpServer.NetworkStream;

public class JobQueue
{
    private readonly BlockingCollection<IJob> _jobs = new BlockingCollection<IJob>();

    public void Enqueue(IJob job)
    {
        _jobs.Add(job);
    }

    public IJob Dequeue()
    {
        
        return _jobs.Take();
    }
}