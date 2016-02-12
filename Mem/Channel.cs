using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class Channel
    {
        public uint cid;

        //state
        public MemCtrl mc;

        private int last_read_rid = -1;
        private int last_write_rid = -1;

        //components
        public uint rmax;
        public Rank[] ranks;

        //timestamps
        long ts_act = -1;
        long ts_pre = -1;
        long ts_read = -1;
        long ts_write = -1;
        
        //constructor
        public Channel(MemCtrl mc, uint rmax, uint bmax)
        {
            this.cid = mc.cid;
            this.mc = mc;
            this.rmax = rmax;
            ranks = new Rank[rmax];
            for (uint i = 0; i < ranks.Length; i++) {
                ranks[i] = new Rank(mc, this, i, bmax);
            }
        }

        //action methods
        public void activate(uint rank_id, uint bank_id, ulong row_idx) {
            ts_act = mc.cycles;
            ranks[rank_id].activate(bank_id, row_idx);
        }

        public void precharge(uint rank_id, uint bank_id) {
            ts_pre = mc.cycles;
            ranks[rank_id].precharge(bank_id);
        }

        public void read(uint rank_id, uint bank_id) {
            ts_read = mc.cycles;
            ranks[rank_id].read(bank_id);
            last_read_rid = (int)rank_id;
        }

        public void write(uint rank_id, uint bank_id) {
            ts_write = mc.cycles;
            ranks[rank_id].write(bank_id);
            last_write_rid = (int)rank_id;
        }

        //test methods
        public bool can_activate(uint rank_id, uint bank_id) {
            return ranks[rank_id].can_activate(bank_id);
        }

        public bool can_precharge(uint rank_id, uint bank_id) {
            return ranks[rank_id].can_precharge(bank_id);
        }

        public bool can_read(uint rank_id, uint bank_id) {
            bool ready = ranks[rank_id].can_read(bank_id);

            if (last_read_rid == -1 || rank_id == last_read_rid) {
                return ready;
            }

            /*** according to "DRAMSim2" ***/
            //check my rank
            if (!ready) 
                return false;

            //check other rank
            if (mc.cycles - ranks[last_read_rid].ts_read < mc.timing.tBL + mc.timing.tRTRS) 
                return false;

            return true;
        }

        public bool can_write(uint rank_id, uint bank_id) {
            bool ready = ranks[rank_id].can_write(bank_id);

            if (last_write_rid == -1 || rank_id == last_write_rid) {
                return ready;
            }

            /*** according to "DRAMSim2" ***/
            //check my rank
            if (!ready) 
                return false;

            //check other rank
            if (mc.cycles - ranks[last_write_rid].ts_write < mc.timing.tBL + mc.timing.tRTRS) 
                return false;

            return true;
        }

        public void reset() {
            last_read_rid = -1;
            last_write_rid = -1;

            ts_act = -1;
            ts_pre = -1;
            ts_read = -1;
            ts_write = -1;

            foreach (Rank r in ranks) {
                r.reset();
            }
        }
    }
}
