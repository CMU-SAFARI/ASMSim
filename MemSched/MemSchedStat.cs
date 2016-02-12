using System;
using System.IO;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    /* 07172011: Have yet to make use of this stats */
    public class MemSchedStat : StatGroup
    {
        public PerQuantumStat[] rank_per_proc;
        public PerQuantumStat[] mpki_per_proc;
        public PerQuantumStat[] rbl_per_proc;
        public PerQuantumStat[] blp_per_proc;
        public PerQuantumStat[] nonintensive_per_proc;

        public MemSchedStat()
        {
            Init();
        }
    }
}



