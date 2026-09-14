using CrowdPunch.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
namespace CrowdPunch.Utilities
{
    public enum NavigationSearchResult : byte { Running, Found, Unreachable, ExpansionLimit, PathLimit }
    public struct NavigationSearchRequest
    { public Entity Enemy;public uint Version;public int Start,Goal,Clearance;public float2 Destination; }
    public struct NavigationSearchSlot
    { public NavigationSearchRequest Request;public uint Stamp;public int HeapCount,Expanded,End;public byte Active;public NavigationSearchResult Result; }
    public struct NavigationSearchNode { public uint Stamp;public float Cost,Score;public int Parent,HeapIndex;public byte Closed; }
    // One fixed allocation per arena, reused for every search. Each slot has its own heap and stamped nodes.
    public struct NavigationSearchStorage : System.IDisposable
    {
        public NativeArray<NavigationSearchSlot> Slots;
        public NativeArray<NavigationSearchNode> Nodes;
        public NativeArray<int> Heap;
        public int CellCount,Cursor;
        public NavigationSearchStorage(int cells,int concurrent)
        { CellCount=cells;Cursor=0;Slots=new NativeArray<NavigationSearchSlot>(concurrent,Allocator.Persistent);
            Nodes=new NativeArray<NavigationSearchNode>(cells*concurrent,Allocator.Persistent);Heap=new NativeArray<int>(cells*concurrent,Allocator.Persistent); }
        public void Dispose(){ if(Slots.IsCreated)Slots.Dispose();if(Nodes.IsCreated)Nodes.Dispose();if(Heap.IsCreated)Heap.Dispose(); }
        public void Begin(int slot,NavigationSearchRequest request,ref NavigationGridBlob g)
        {
            var s=Slots[slot];s.Stamp++;if(s.Stamp==0){for(int i=0;i<CellCount;i++)Nodes[slot*CellCount+i]=default;s.Stamp=1;}
            s.Request=request;s.HeapCount=0;s.Expanded=0;s.Active=1;s.Result=NavigationSearchResult.Running;s.End=-1;
            Slots[slot]=s;
            SetNode(slot,request.Start,new NavigationSearchNode { Stamp=s.Stamp,Cost=0,Score=Heuristic(ref g,request.Start,request.Goal),Parent=-1,HeapIndex=-1 });
            Push(slot,request.Start);
        }
        public int ExpandBudget(ref NavigationGridBlob g,int budget,int searchLimit)
        {
            int used=0,idle=0;
            while(used<budget && idle<Slots.Length)
            {
                int slot=Cursor;Cursor=(Cursor+1)%Slots.Length;var s=Slots[slot];
                if(s.Active==0 || s.Result!=NavigationSearchResult.Running){idle++;continue;}idle=0;
                Step(slot,ref g,searchLimit);used++;
            }
            return used;
        }
        private void Step(int slot,ref NavigationGridBlob g,int limit)
        {
            var s=Slots[slot];
            if(s.HeapCount==0){s.Result=NavigationSearchResult.Unreachable;Slots[slot]=s;return;}
            int cell=Pop(slot);s=Slots[slot];s.Expanded++;
            if(cell==s.Request.Goal){s.Result=NavigationSearchResult.Found;s.End=cell;Slots[slot]=s;return;}
            if(s.Expanded>=limit){s.Result=NavigationSearchResult.ExpansionLimit;Slots[slot]=s;return;}
            Slots[slot]=s;var current=GetNode(slot,cell);current.Closed=1;SetNode(slot,cell,current);
            byte edges=g.Edges[s.Request.Clearance*CellCount+cell];
            for(int d=0;d<8;d++)if((edges&(1<<d))!=0)
            {
                int next=NavigationGeometry.Neighbor(ref g,cell,d);var node=GetNode(slot,next);
                float cost=current.Cost+(d<4?1f:1.41421356f);
                if(node.Stamp==s.Stamp && (node.Closed!=0 || cost>=node.Cost))continue;
                bool fresh=node.Stamp!=s.Stamp;
                node.Stamp=s.Stamp;node.Parent=cell;node.Cost=cost;node.Score=cost+Heuristic(ref g,next,s.Request.Goal);node.Closed=0;
                if(fresh)node.HeapIndex=-1;SetNode(slot,next,node);
                if(fresh)Push(slot,next);else Rise(slot,node.HeapIndex);
            }
        }
        public NavigationSearchNode GetNode(int slot,int cell)=>Nodes[slot*CellCount+cell];
        private void SetNode(int slot,int cell,NavigationSearchNode node)=>Nodes[slot*CellCount+cell]=node;
        private static float Heuristic(ref NavigationGridBlob g,int a,int b)
        { int2 d=math.abs(new int2(a%g.Size.x,a/g.Size.x)-new int2(b%g.Size.x,b/g.Size.x));return math.cmax(d)+.41421356f*math.cmin(d); }
        private bool Less(int slot,int a,int b){var x=GetNode(slot,a);var y=GetNode(slot,b);return x.Score<y.Score || x.Score==y.Score && a<b;}
        private void Put(int slot,int index,int cell){Heap[slot*CellCount+index]=cell;var n=GetNode(slot,cell);n.HeapIndex=index;SetNode(slot,cell,n);}
        private void Push(int slot,int cell){var s=Slots[slot];int i=s.HeapCount++;Slots[slot]=s;Put(slot,i,cell);Rise(slot,i);}
        private void Rise(int slot,int index)
        { int cell=Heap[slot*CellCount+index];while(index>0){int parent=(index-1)/2;int p=Heap[slot*CellCount+parent];if(!Less(slot,cell,p))break;Put(slot,index,p);index=parent;}Put(slot,index,cell); }
        private int Pop(int slot)
        {
            var s=Slots[slot];int result=Heap[slot*CellCount];int cell=Heap[slot*CellCount+--s.HeapCount];Slots[slot]=s;
            int index=0;while(index*2+1<s.HeapCount){int child=index*2+1;
                if(child+1<s.HeapCount && Less(slot,Heap[slot*CellCount+child+1],Heap[slot*CellCount+child]))child++;
                int next=Heap[slot*CellCount+child];if(!Less(slot,next,cell))break;Put(slot,index,next);index=child;}
            if(s.HeapCount>0)Put(slot,index,cell);return result;
        }
    }
}
