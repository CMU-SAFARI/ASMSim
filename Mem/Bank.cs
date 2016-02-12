using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Bank
    {
        public uint cid;
        public uint rid;
        public uint bid;

        //state
        public MemCtrl mc;
        public long curr_rowid;
        public int curr_proc;  

//        public long[] shadow_curr_rowid = new long[Config.N];

        //timestamps
        public long ts_act = -1;
        public long ts_pre = -1;
        public long ts_read = -1;
        public long ts_write = -1;
       

        //constructor
        public Bank(MemCtrl mc, Rank rank, uint bid) {
            this.cid = mc.cid;
            this.rid = rank.rid;
            this.bid = bid;

            this.mc = mc;
            curr_rowid = -1;   //closed-row

            curr_proc = int.MaxValue;
        }
   
        //action methods
        public void activate(ulong rowid) {
            ts_act = mc.cycles;
            curr_rowid = (long) rowid;
        }

        public void precharge() {
            ts_pre = mc.cycles;
            curr_rowid = -1;
        }

        public void read() {
            ts_read = mc.cycles;
        }

        public void write() {
            ts_write = mc.cycles;
        }

        //test methods
        public bool can_activate() {
            if (curr_rowid != -1) 
                return false;
            if (ts_act != -1 && mc.cycles - ts_act < mc.timing.tRC) 
                return false;
            if (ts_pre != -1 && mc.cycles - ts_pre < mc.timing.tRP) 
                return false;
            
            return true;
        }

        public bool can_precharge() {
            if (ts_act != -1 && mc.cycles - ts_act < mc.timing.tRAS) 
                return false;
            if (ts_read != -1 && mc.cycles - ts_read < mc.timing.tRTP) 
                return false;
            if (ts_write != -1 && mc.cycles - ts_write < mc.timing.tCWL + mc.timing.tBL + mc.timing.tWR) 
                return false;

            return true;
        }

        public bool can_read() {
            if (curr_rowid == -1) 
                return false;
            if (ts_act != -1 && mc.cycles - ts_act < mc.timing.tRCD) 
                return false;
            if (ts_read != -1 && mc.cycles - ts_read < mc.timing.tCCD) 
                return false;
            if (ts_write != -1 && mc.cycles - ts_write < mc.timing.tCWL + mc.timing.tBL + mc.timing.tWTR) 
                return false;

            return true;
        }

        public bool can_write() {
            if (curr_rowid == -1) 
                return false;
            if (ts_act != -1 && mc.cycles - ts_act < mc.timing.tRCD) 
                return false;
            if (ts_read != -1 && mc.cycles - ts_read < mc.timing.tRTW) 
                return false;
            if (ts_write != -1 && mc.cycles - ts_write < mc.timing.tCCD) 
                return false;

            return true;
        }

        public void reset() {
            ts_act = -1;
            ts_pre = -1;
            ts_read = -1;
            ts_write = -1;
            curr_rowid = -1;
        }
    }
}
