using System.Numerics;
namespace QuanLyGiangVien.Domain.Academics;
// Private working graph; no caller-owned objects or curriculum records are mutated.
internal sealed class DegreeCreditFlow(int count)
{
    internal sealed class Edge(int to,int capacity,BigInteger cost)
    {
        internal int To {get;}=to;internal int Remaining {get;set;}=capacity;
        internal int Original {get;}=capacity;internal BigInteger Cost {get;}=cost;
        internal Edge Reverse {get;set;}=null!;internal int Used=>Original-Remaining;
    }
    private readonly List<Edge>[] adjacency=Enumerable.Range(0,count).Select(_=>new List<Edge>()).ToArray();
    internal int Count=>count;
    internal Edge Add(int from,int to,int capacity,BigInteger cost)
    {
        var edge=new Edge(to,capacity,cost);var reverse=new Edge(from,0,-cost);edge.Reverse=reverse;reverse.Reverse=edge;
        adjacency[from].Add(edge);adjacency[to].Add(reverse);return edge;
    }
    internal void Run(int source,int sink)
    {
        while(true)
        {
            var distance=new BigInteger?[count];var previous=new Edge?[count];var parents=new int[count];
            var queued=new bool[count];var queue=new Queue<int>();distance[source]=0;queue.Enqueue(source);queued[source]=true;
            while(queue.Count>0)
            {
                int node=queue.Dequeue();queued[node]=false;
                foreach(var edge in adjacency[node])if(edge.Remaining>0&&(distance[edge.To] is null||distance[edge.To]>distance[node]+edge.Cost))
                {
                    distance[edge.To]=distance[node]+edge.Cost;previous[edge.To]=edge;parents[edge.To]=node;
                    if(!queued[edge.To]){queue.Enqueue(edge.To);queued[edge.To]=true;}
                }
            }
            // Positive-cost augmentation would trade an earlier priority for later progress.
            if(previous[sink] is null||distance[sink]>=0)return;
            int amount=int.MaxValue;for(int node=sink;node!=source;node=parents[node])amount=Math.Min(amount,previous[node]!.Remaining);
            for(int node=sink;node!=source;node=parents[node]){previous[node]!.Remaining-=amount;previous[node]!.Reverse.Remaining+=amount;}
        }
    }
}
