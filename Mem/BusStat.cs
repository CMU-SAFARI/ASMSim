using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class BusStat : StatGroup
    {
        //id
        public uint cid;

        //service
        public AccumRateStat utilization;

        //accesses
        public AccumStat access;

        public BusStat(uint cid)
        {
            this.cid = cid;
            Init();
        }
    }
}