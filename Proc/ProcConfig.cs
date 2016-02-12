using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class ProcConfig : ConfigGroup
    {
        public int ipc = 3;

        //writebacks
        public bool wb = false;

        //L1 cache
        public int l1_cache_size;
        public int l1_cache_size_bits = 16;    //power of two

        public int l1_cache_assoc;
        public int l1_cache_assoc_bits = 2;    //power of two

        //cache
        public int cache_size;
        public int cache_size_bits = 21;    //power of two

        public int cache_assoc;
        public int cache_assoc_bits = 4;    //power of two
        
        public int block_size;
        public int block_size_bits = 6;     //power of two

        //instruction window
        public int inst_wnd_max = 128;

        //mshr
        public int mshr_max = 32;

        //writeback queue
        public int wb_q_max = 128;
        public int quantum_cycles = 5000000;
        public int num_pollution_vector_entries = 65536;
        public int clear_poll_filter_cycles = 1000000;
        public int num_evicted_address_filter_entries = 32;
        public int set_allocation = 1;
        public int sample = 32;
        public double slowdown_bound = 2;

        protected override bool set_special_param(string param, string val)
        {
            return false;
        }

        public override void finalize()
        {
            l1_cache_size = 1 << l1_cache_size_bits;
            l1_cache_assoc = 1 << l1_cache_assoc_bits;


            cache_size = 1 << cache_size_bits;
            cache_assoc = 1 << cache_assoc_bits;
            block_size = 1 << block_size_bits;
        }
    }
}
