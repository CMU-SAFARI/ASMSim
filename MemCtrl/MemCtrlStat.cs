using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using System.Reflection;

namespace MemMap
{
    public class MemCtrlStat : StatGroup
    {
        //id
        public uint cid;

        //load
        public AccumRateStat[] rbinaryloadtick_per_proc;
        public AccumRateStat[] rloadtick_per_proc;
        public AccumRateStat[] wbinaryloadtick_per_proc;
        public AccumRateStat[] wloadtick_per_proc;
        
        //busy/idle
        //public DictSampleStat read_loaded_time;
        //public DictSampleStat read_unloaded_time;

        //auxiliary
        //public AccumStat[] bank_divert_per_proc;

        //read-write RBL
        //public AccumStat rw_row_buffer_hit;

        //writeback
        public AccumRateStat wbmode_fraction;
        public SampleAvgStat rds_per_wb_mode;
        public SampleAvgStat wbs_per_wb_mode;
        public SampleAvgStat wbmode_blp;

        public SampleAvgStat wbmode_length;
        public SampleAvgStat wbmode_distance;
        public SampleAvgStat wbmode_longest_transaction;
        public SampleAvgStat[] queueing_latency_per_proc;

        public MemCtrlStat(uint cid)
        {
            this.cid = cid;
            Init();
        }
    }
}
