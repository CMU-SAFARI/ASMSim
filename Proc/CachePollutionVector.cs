using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MemMap
{
    public class CachePollutionVector
    {
        ulong num_entries;
        bool[] pollution_vector;
        int[] core_id;

        public CachePollutionVector()
        {
            num_entries = (ulong)Config.proc.num_pollution_vector_entries;
            pollution_vector = new bool[num_entries];          
            core_id = new int[num_entries];
            for (int i = 0; i < (int)num_entries; i ++) core_id[i] = Config.N;
        }

        public void set_pollution_vector (ulong block_addr, int pid)
        {
            ulong block_addr_index = block_addr % num_entries ;
            pollution_vector[block_addr_index] = true;
            core_id[block_addr_index] = pid;
        }

        public void clear_pollution_vector (ulong block_addr)
        {
            ulong block_addr_index = block_addr % num_entries ;
            pollution_vector[block_addr_index] = false;
            core_id[block_addr_index] = Config.N;
        }

        public bool check_filter (ulong block_addr)
        {
            if (pollution_vector[block_addr % num_entries] == true) return true;
            else return false;
        }

        public int get_interfering_core (ulong block_addr)
        {
            return core_id[block_addr % num_entries]; 
        }

        public bool check_set_bit (ulong block_addr)
        {
            if (pollution_vector[block_addr % num_entries] == true) return true;
            else return false;
        }

        public bool check_clear_bit (ulong block_addr)
        {
            if (pollution_vector[block_addr % num_entries] == true) return true;
            else return false;
        }

        public void clear_vector ()
        {
            for (ulong i = 0; i < num_entries; i ++)
            {
                pollution_vector[i] = false;
            }
        }

    }

    
}

