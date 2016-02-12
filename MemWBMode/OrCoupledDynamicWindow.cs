using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class OrCoupledDynamicWindow : MemWBMode
    {
        public uint window;
        public uint wb_mode_cycles;

        public OrCoupledDynamicWindow(MemCtrl[] mctrls)
            : base(mctrls)
        {
            window = Config.mctrl.wb_window;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //check for end of wb_mode
            if (wb_mode[0]) {
                wb_mode_cycles++;
                if (wb_mode_cycles == window) {
                    for (uint i = 0; i < cmax; i++) {
                        wb_mode[i] = false;
                    }
                }
            }

            //check for start of wb_mode
            if (wb_mode[0])
                return;

            bool any_writeq_full = false;
            bool any_readq_empty = false;
            
            for (uint i = 0; i < cmax; i++) {
                any_writeq_full = any_writeq_full || is_writeq_full(i);
                any_readq_empty = any_readq_empty || is_readq_empty(i);
            }

            if (any_writeq_full || any_readq_empty) {
                for (uint i = 0; i < cmax; i++) {
                    wb_mode[i] = true;
                }
                wb_mode_cycles = 0;
            }
        }
    }
}
