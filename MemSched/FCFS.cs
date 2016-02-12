using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class FCFS : MemSched
    {
        public override void enqueue_req(Req req) { }
        public override void dequeue_req(Req req) { }

        public override Req better_req(Req req1, Req req2)
        {
            if (req1.ts_arrival <= req2.ts_arrival) return req1;
            else return req2;
        }
    }

}