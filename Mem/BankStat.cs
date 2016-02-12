using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class BankStat : StatGroup
    {
        //id
        public uint cid;
        public uint rid;
        public uint bid;

        //service
        public AccumRateStat utilization;
        
        //commands
        public AccumStat cmd_activate;
        public AccumStat cmd_precharge;
        public AccumStat cmd_read;
        public AccumStat cmd_write;

        //accesses
        public AccumStat access;

        //hit or miss
        public AccumStat row_hit;
        public AccumStat row_miss;

        //hit or miss per originating processor
        public AccumStat[] row_hit_perproc;
        public AccumStat[] row_miss_perproc;
		
        public BankStat(uint cid, uint rid, uint bid)
        {
            this.cid = cid;
            this.rid = rid;
            this.bid = bid;
            Init();
        }
    }
}