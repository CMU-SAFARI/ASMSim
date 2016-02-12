using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class DecoupledRDEmptyServeN : MemWBMode
    {
        public uint serve_max;
        public uint[] serve_cnt;

        public DecoupledRDEmptyServeN(MemCtrl[] mctrls)
            : base(mctrls)
        {
            serve_max = Config.mctrl.serve_max;
            serve_cnt = new uint[cmax];
        }

        public override void issued_write_cmd(Cmd cmd)
        {
            Dbg.Assert(cmd.type == Cmd.TypeEnum.WRITE);
            uint cid = cmd.addr.cid;
            serve_cnt[cid]++;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (!wb_mode[i])
                    continue;

                if (!is_writeq_empty(i) && serve_cnt[i] < serve_max)
                    continue;

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_readq_empty(i))
                    continue;

                serve_cnt[i] = 0;
                wb_mode[i] = true;
            }
        }
    }
}