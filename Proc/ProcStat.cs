using System;
using System.IO;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    /**
     * Processor statistics
     */
    public class ProcStat : StatGroup
    {
        //trace
        public string trace_fname;

        //instructions
        public AccumStat cycle;
        public AccumRateStat ipc;  //total instructions, mem and non-mem, retired (executed) by instruction window

        //misses
        public AccumRateStat rmpc;
        public AccumRateStat wmpc;

        //stall
        public AccumStat stall_inst_wnd;
        public AccumStat stall_read_mctrl;
        public AccumStat stall_write_mctrl;
        public AccumStat stall_mshr;

        //memory request issued (sent to memory scheduler)
        public AccumStat req;           //total memory requests issued
        public AccumStat read_req;      //read (load) requests issued
        public AccumStat write_req;     //write (store) requests issued
        //public AccumStat req_wb;      //writeback requests issued
        //public AccumStat dropped_wb;  //writeback requests not issued due to instruction window stall

        //memory request served (result received by processor)
        public AccumStat read_req_served;
        public AccumStat write_req_served;

        //per-quantum stats
        public PerQuantumStat read_quantum;
        public PerQuantumStat write_quantum;

        //writeback hit
        public AccumStat wb_hit;

        //row-buffer
        public AccumStat row_hit_read;
        public AccumStat row_miss_read;
        public SamplePercentAvgStat row_hit_rate_read;

        public AccumStat row_hit_write;
        public AccumStat row_miss_write;
        public SamplePercentAvgStat row_hit_rate_write;

        //latency (time between when a request is issued and served)
        public SampleAvgStat read_avg_latency;
        public SampleAvgStat write_avg_latency; 

        //bank-level parallelism
        public SampleAvgStat service_blp;

        //idealized row-buffer stats
        public SamplePercentAvgStat rw_buddy_prob;
        public SamplePercentAvgStat rw_buddy_wprob;

        public SamplePercentAvgStat rr_buddy_prob;
        public SamplePercentAvgStat rr_buddy_wprob;

        public AccumStat profile_inst_cnt; 
        public AccumStat profile_cycle_cnt; 

        //etc
        //public double mem_wait_avg; //memory waiting time average
        public AccumStat total_service;
        public AccumStat max_as;
        public AccumStat CopOut;
 
        public AccumStat memory_cycle;

        public AccumStat l1_cache_hit_count;
        public AccumStat l1_cache_miss_count;
        public AccumStat l2_cache_hit_count;
        public AccumStat l2_cache_miss_count;
        public AccumStat num_prefetches;
        public SampleAvgStat blp;
        public SampleAvgStat num_outstanding_req;

        public ProcStat()
        {
            Init();
        }
    }
}



