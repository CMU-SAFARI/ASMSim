using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MemMap
{
    class Cache
    {
        ulong cycle;        //artificial unit of time to timestamp blocks for LRU replacement
        uint set_max;       //total number of sets

        ulong hit = 0;      //number of cache hits
        ulong miss = 0;     //number of cache misses

        ulong[,] cache;     //tag for individual blocks [set_index, associativity]
        bool[,] dirty;      //dirty bit for individual blocks [set_index, associativity]
        bool[,] valid;
        ulong[,] core_id;
        int[] count_allocation;
        public double[] average_allocation;
        public int[] average_allocation_count;

        static Random random = new Random(100);  

        /**
         * Constructor
         */
        public Cache()
        {
            //initialize time
            cycle = 0;

            //size of a set in bytes
            int set_size = Config.proc.block_size * Config.proc.cache_assoc;

            //total number of sets
            Debug.Assert(Config.proc.cache_size % set_size == 0);
            set_max = (uint)(Config.proc.cache_size / set_size);

            //components
            cache = new ulong[set_max, Config.proc.cache_assoc];
            dirty = new bool[set_max, Config.proc.cache_assoc];
            valid = new bool[set_max, Config.proc.cache_assoc];
            core_id = new ulong[set_max, Config.proc.cache_assoc];
            count_allocation = new int[Config.N];
            average_allocation = new double[Config.N];
            average_allocation_count = new int[Config.N];

            //initialize tags
            for (int i = 0; i < set_max; i++)
                for (int j = 0; j < Config.proc.cache_assoc; j++)
                {
                    cache[i, j] = Proc.NULL_ADDRESS;
                    core_id[i, j] = Proc.NULL_ADDRESS;
                } 
        }

        private int RandomNumber(int min, int max)
        {
            return random.Next(min, max);
        }


        /**
         * Searches for a block within the cache.
         * If found, sets the dirty bit.
         * 
         * @param block_addr block address
         * @param inst_type instruction type
         * @return if found, true; otherwise, false
         */
        public bool has_addr(ulong block_addr, ReqType inst_type)
        {
            //progress time
            cycle++;

            //calculate set index
            uint set_index = (uint)(block_addr % set_max);

            //search for block
            for (int i = 0; i < Config.proc.cache_assoc; i++) {

                if (valid[set_index,i] == true && cache[set_index, i] == block_addr) {
                    //hit
                    hit++;
                    if (inst_type == ReqType.WR)
                        dirty[set_index, i] = true;


                    ulong temp_cache = cache[set_index, i];
                    bool temp_dirty = dirty[set_index,i];
                    ulong temp_core_id = core_id[set_index,i];
                    bool temp_valid = valid[set_index,i];

                    for (int j = i; j < Config.proc.cache_assoc - 1; j ++)
                    {
                        cache[set_index,j] = cache[set_index,j+1];
                        dirty[set_index,j] = dirty[set_index,j+1];
                        core_id[set_index,j] = core_id[set_index,j+1];
                        valid[set_index,j] = valid[set_index,j+1];
                    }
                    cache[set_index,Config.proc.cache_assoc-1] = temp_cache;
                    dirty[set_index,Config.proc.cache_assoc-1] = temp_dirty;
                    core_id[set_index,Config.proc.cache_assoc-1] = temp_core_id;
                    valid[set_index,Config.proc.cache_assoc-1] = temp_valid;
                        

                    return true;
                }
            }
            
            miss++;
            return false;
        }
        
        public bool is_sampled_set(ulong block_addr)
        {
            if ((block_addr%(ulong)set_max)%(ulong)Config.proc.sample == 0) return true;
            else return false;
            
        }

        public int has_addr_sampled_set(ulong block_addr, ReqType inst_type)
        {
            //progress time
            cycle++;

            //calculate set index
            uint set_index = (uint)(block_addr % set_max);
            
                
            //search for block
            for (int i = 0; i < Config.proc.cache_assoc; i++) {

                if (valid[set_index,i] == true && cache[set_index, i] == block_addr && set_index%Config.proc.sample == 0) {
                    return (i + 1000);
                }
            }
            if (set_index % Config.proc.sample == 0) return 1000;
            else return 100000;
        }

        /**
         * Add block to the cache.
         * Either an empty or the LRU block is populated.
         * @param block_addr block address
         * @param inst_type instruction type
         * @return if LRU block is populated, its tag; if empty block is populated, 0
         */
        public ulong cache_add(ulong block_addr, ReqType inst_type, ulong pid)
        {
            cycle++;

            //calculate set index
            int set_index = (int)(block_addr % set_max);

            //empty entry within a set
            int empty_entry_index = -1;

            //lru entry within a set
            ulong return_address = Proc.NULL_ADDRESS;

            for (int i = 0; i < Config.N; i ++)
            {
                count_allocation[i] = 0;
                average_allocation_count[i] ++;
            }
             
            for (int i = 0; i < Config.proc.cache_assoc; i++)
            {
                if (valid[set_index,i] == true) {
                    count_allocation[core_id[set_index, i]] ++;
                    average_allocation[core_id[set_index, i]] ++;
                }
            }
            //search for empty or lru entry
            for (int i = Config.proc.cache_assoc - 1; i >= 0; i--)
            {
                //make sure not already in cache
                if (valid[set_index,i] == true) Debug.Assert(cache[set_index, i] != block_addr);

                if (valid[set_index, i] == false) {
                    if (Config.compute_allocation && (count_allocation[pid] >= (int)Sim.cache_controller.get_allocation((int)pid))) break;
                    //found empty entry
                    empty_entry_index = i;
                    break;
                }
            }

            if (empty_entry_index == -1)
            {

                   if ((Config.ucp || Config.slowdown_allocation || Config.qos_allocation || Config.naive_qos_allocation || Config.compute_allocation) && !Double.IsNaN((double)Sim.cache_controller.get_allocation((int)pid)))
                   {
                           if (count_allocation[pid] < (int)Sim.cache_controller.get_allocation((int)pid))
                           {
                               for (int j = 0; j < Config.proc.cache_assoc; j ++)
                               {
                                   if (core_id[set_index,j] != pid)
                                   {
                                      if (Config.poll_filter && Sim.procs[pid].pollution_vector.check_filter(block_addr)) Sim.procs[pid].pollution_vector.clear_pollution_vector(block_addr);
                                      if (Config.poll_filter && pid != core_id[set_index,j]) Sim.procs[core_id[set_index, j]].pollution_vector.set_pollution_vector(cache[set_index, j], (int)pid);
                                      if (dirty[set_index,j]) return_address = cache[set_index,j];
                                      for (int k =  j; k < Config.proc.cache_assoc - 1; k ++)
                                      {
                                          cache[set_index,k] = cache[set_index,k+1];
                                          dirty[set_index,k] = dirty[set_index,k+1];
                                          core_id[set_index,k] = core_id[set_index,k+1];
                                      } 
                                      cache[set_index,Config.proc.cache_assoc-1] = block_addr;
                                      dirty[set_index,Config.proc.cache_assoc-1] = (inst_type == ReqType.WR);
                                      core_id[set_index,Config.proc.cache_assoc-1] = pid;
                                      break;
                                   }
                               }
                           }
                           else if (Sim.cache_controller.get_allocation((int)pid) != 0)
                           {
                               for (int j = 0; j < Config.proc.cache_assoc; j ++)
                               {
                                   if (core_id[set_index,j] == pid)
                                   {
                                      if (Config.poll_filter && Sim.procs[pid].pollution_vector.check_filter(block_addr)) Sim.procs[pid].pollution_vector.clear_pollution_vector(block_addr);
                                      if (dirty[set_index,j]) return_address = cache[set_index,j];
                                      for (int k =  j; k < Config.proc.cache_assoc - 1; k ++)
                                      {
                                          cache[set_index,k] = cache[set_index,k+1];
                                          dirty[set_index,k] = dirty[set_index,k+1];
                                          core_id[set_index,k] = core_id[set_index,k+1];
                                      } 
                                      cache[set_index,Config.proc.cache_assoc-1] = block_addr;
                                      dirty[set_index,Config.proc.cache_assoc-1] = (inst_type == ReqType.WR);
                                      core_id[set_index,Config.proc.cache_assoc-1] = pid;
                                      break;
                                   }
                               }
                           }
                   }
                   else
                   {
                        if (dirty[set_index,0]) return_address = cache[set_index,0];
//
                        if (Config.poll_filter && Sim.procs[pid].pollution_vector.check_filter(block_addr)) Sim.procs[pid].pollution_vector.clear_pollution_vector(block_addr);
                        if (Config.poll_filter && pid != core_id[set_index,0]) Sim.procs[core_id[set_index, 0]].pollution_vector.set_pollution_vector(cache[set_index, 0], (int)pid);
                        for (int k = 0; k < Config.proc.cache_assoc - 1; k ++)
                        {
                            cache[set_index,k] = cache[set_index,k+1];
                            dirty[set_index,k] = dirty[set_index,k+1];
                            core_id[set_index,k] = core_id[set_index,k+1];
                        }
                        
                        cache[set_index,Config.proc.cache_assoc-1] = block_addr;
                        dirty[set_index,Config.proc.cache_assoc-1] = (inst_type == ReqType.WR);
                        core_id[set_index,Config.proc.cache_assoc-1] = pid;
                   }
            }
            else
            {
                if (Config.poll_filter && Sim.procs[pid].pollution_vector.check_filter(block_addr)) Sim.procs[pid].pollution_vector.clear_pollution_vector(block_addr);
                for (int k =  empty_entry_index; k < Config.proc.cache_assoc - 1; k ++)
                {
                    cache[set_index,k] = cache[set_index,k+1];
                    dirty[set_index,k] = dirty[set_index,k+1];
                    core_id[set_index,k] = core_id[set_index,k+1];
                    valid[set_index,k] = valid[set_index,k+1];
                } 
                cache[set_index,Config.proc.cache_assoc-1] = block_addr;
                dirty[set_index,Config.proc.cache_assoc-1] = (inst_type == ReqType.WR);
                core_id[set_index,Config.proc.cache_assoc-1] = pid;
                valid[set_index,Config.proc.cache_assoc-1] = true;

            }

              

            return return_address;

        }
   }

}
