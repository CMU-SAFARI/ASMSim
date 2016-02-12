using System;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    public class StrideEntry
    {
        public ulong block_addr; 
        public ulong prefetch_block_addr;
        public int stride;
        public int train_hits;
        public bool trained;

        public StrideEntry() {}
    }
}
