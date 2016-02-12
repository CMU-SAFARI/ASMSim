using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class DecoupledWBFullDrain : MemWBMode
    {
        public DecoupledWBFullDrain(MemCtrl[] mctrls)
            : base(mctrls)
        {

        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (!wb_mode[i])
                    continue;

                if (!is_writeq_empty(i))
                    continue;

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_writeq_full(i))
                    continue;

                wb_mode[i] = true;
            }
        }
    }
}