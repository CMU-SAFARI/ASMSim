using System;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    public enum ReqType
    {
        RD,
        WR,
        WB,
        NULL
    };

    public class Req
    {
        //page mapping
        public static PageRandomizer prand;
        public static PageSequencer pseq;

        //state
        public int pid;
        public ReqType type;
        //hack
        public ReqType proc_req_type;


        //address
        public ulong paddr;
        public ulong actual_addr;
        public ulong block_addr;
        public MemAddr addr;

        //timestamp
        public long ts_arrival;
        public long ts_departure;
        public long ts_issue;
        public int latency;
        public int alone_latency;
        public int queueing_latency;

        //associated write-back request
        public Req wb_req;

        //callback
        public Callback callback;

        //scheduling-related
        public bool marked;

        //writeback-related
        public bool wb_marked;
        public int transaction_length;

        public bool row_hit;
        public int service_latency;
        public bool is_alone_hit;
        public int alone_counter_tracker;
        public ulong stall_time;
        public ulong pc;
        public ulong interference_cycles;
        public bool is_prefetch;

        //constructor
        public Req() { }

        public void set(int pid, ReqType type, ReqType proc_req_type, ulong paddr)
        {
            //state
            this.pid = pid;
            this.type = type;
            this.proc_req_type = proc_req_type;
            this.row_hit = true;

            this.actual_addr = paddr;
            //address
            if (Config.mctrl.page_randomize) {
                this.paddr = prand.get_paddr(paddr);
            }
            else if (Config.mctrl.page_sequence){
                this.paddr = pseq.get_paddr(paddr);
            }
            else {
                this.paddr = paddr;
            }
            this.block_addr = this.paddr >> Config.proc.block_size_bits;
            this.addr = MemMap.translate(this.paddr);
            this.is_prefetch = false;

            //misc
            this.reset();
        }


        public void set_prefetch(int pid, ReqType type, ReqType proc_req_type, ulong paddr)
        {
            //state
            this.pid = pid;
            this.type = type;
            this.proc_req_type = proc_req_type;
            this.row_hit = true;

            this.actual_addr = paddr;
            this.paddr = paddr;
            //address
            this.block_addr = this.paddr >> Config.proc.block_size_bits;
            this.addr = MemMap.translate(this.paddr);
            this.is_prefetch = true;

            //misc
            this.reset();
        }

        public void reset()
        {
            //timestamp
            ts_arrival = -1;
            ts_departure = -1;
            latency = -1;
            queueing_latency = -1;
 

            //other
            wb_req = null;
            callback = null;
            marked = false;
            wb_marked = false;
            transaction_length = 0;
            
            is_alone_hit = false;
            alone_counter_tracker = 0;
            interference_cycles = 0;
        }
    }

    public class RequestPool
    {
        private const int RECYCLE_MAX = 10000;
        private static LinkedList<Req> req_pool = new LinkedList<Req>();
        public static int inflight_req_count = 0;

        static RequestPool()
        {
            for (int i = 0; i < RECYCLE_MAX; i++)
                req_pool.AddFirst(new Req());
        }

        public static void enpool(Req req)
        {
            inflight_req_count --;
//            if (inflight_req_count > 150) Console.Write(" Decrement Inflight request count " + inflight_req_count + "\n");
            req.reset();
            req_pool.AddLast(req);
        }

        public static Req depool()
        {
            Dbg.Assert(req_pool.First != null);
            Req req = req_pool.First.Value;
            req_pool.RemoveFirst();
            inflight_req_count ++;
//            if (inflight_req_count > 150) Console.Write(" Increment Inflight request count " + inflight_req_count + "\n");
            return req;
        }

        public bool is_empty()
        {
            return req_pool.Count == 0;
        }
    }
}
