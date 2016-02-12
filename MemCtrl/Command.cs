using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class Cmd
    {
        //constructor
        public Cmd(TypeEnum type, MemAddr addr, int pid, Req req, List<Cmd> cmd_q)
        {
            valid = true;

            this.pid = pid;
            this.addr = addr;
            this.type = type;
            this.req = req;
            this.cmd_q = cmd_q;
        }
        public bool valid;
        public int pid;
        public MemAddr addr;
        public TypeEnum type;
        public Req req;
        public List<Cmd> cmd_q;
        public bool is_drain;

        public enum TypeEnum
        {
            PRECHARGE,
            ACTIVATE,
            READ,
            WRITE,
        }
    }
}
