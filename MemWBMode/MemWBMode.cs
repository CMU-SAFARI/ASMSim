using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public abstract class MemWBMode
    {
        protected int cmax;
        public MemCtrl[] mctrls;
        public bool[] wb_mode;
        public ulong cycles;

        public MemWBMode(MemCtrl[] mctrls)
        {
            this.cmax = mctrls.Length;
            this.mctrls = mctrls;
            this.wb_mode = new bool[cmax];
        }

        public abstract void tick(uint cid);
        public bool is_wb_mode(uint cid)
        {
            return wb_mode[cid];
        }

        public virtual void issued_write_cmd(Cmd cmd){}

        protected bool is_writeq_empty(uint cid)
        {
            MemCtrl mctrl = mctrls[cid];
            return mctrl.wload == 0;
        }

        protected bool is_writeq_full(uint cid)
        {
            MemCtrl mctrl = mctrls[cid];
            return mctrl.mctrl_writeq.Capacity == mctrl.wload;
        }              

        protected bool is_readq_empty(uint cid)
        {
            MemCtrl mctrl = mctrls[cid];
            return mctrl.rload == 0;
        }
    }
}
