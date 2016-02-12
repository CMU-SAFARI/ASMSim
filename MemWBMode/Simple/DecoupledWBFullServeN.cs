using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class DecoupledWBFullServeN : MemWBMode
    {
        public uint serve_max;
        public uint[] serve_cnt;

        public DecoupledWBFullServeN(MemCtrl[] mctrls)
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

                if (serve_cnt[i] < serve_max)
                    continue;

                wb_mode[i] = false;
            }

            //check for start of wb_mode
            for (uint i = 0; i < cmax; i++) {
                if (wb_mode[i])
                    continue;

                if (!is_writeq_full(i))
                    continue;

                serve_cnt[i] = 0;
                wb_mode[i] = true;
            }
        }
    }
}

/*
if (i == 2 && cycles >= 658200) {
//if (i == 2 && cycles >= 736000) {
    Console.WriteLine("===============================================");
    Console.WriteLine("{0}\t{1}\t{2}", cycles, serve_cnt[i], mctrls[i].mctrl_writeq.Count);
    Console.WriteLine("ReadsToDrain:\t{0}", mctrls[i].reads_to_drain);
    Console.WriteLine("WritesToDrain:\t{0}", mctrls[i].writes_to_drain);
    uint b = 0;
    foreach (List<Cmd> cmdq in mctrls[i].cmdqs) {
        Console.Write("{0}:\t", b);
        b++;
        foreach (Cmd cmd in cmdq) {
            Console.Write("{0}({1})\t", cmd.type, mctrls[i].can_schedule_cmd(cmd));
        }
        Console.WriteLine("");
    }
    Console.WriteLine("------------------------------");
    b = 0;
    foreach (List<Req> q in mctrls[i].inflightqs) {
        Console.Write("{0}:\t", b);
        b++;
        foreach (Req req in q) {
            Console.Write("{0}\t", req.type);
        }
        Console.WriteLine("");
    }
    Console.Out.Flush();
}
*/