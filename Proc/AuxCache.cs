using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MemMap
{
    class AuxCache
    {
        ulong cycle;        //artificial unit of time to timestamp blocks for LRU replacement
        uint set_max;       //total number of sets

        ulong hit = 0;      //number of cache hits
        ulong miss = 0;     //number of cache misses

        ulong[,] cache;     //tag for individual blocks [set_index, associativity]
        ulong[,] timestamp; //timestamp for individual blocks [set_index, associativity]
        int[] recency_position;
        bool[,] dirty;      //dirty bit for individual blocks [set_index, associativity]
        ulong[,] core_id;

        static Random random = new Random(100);  

        /**
         * Constructor
         */
        public AuxCache()
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
            timestamp = new ulong[set_max, Config.proc.cache_assoc];
            dirty = new bool[set_max, Config.proc.cache_assoc];
            core_id = new ulong[set_max, Config.proc.cache_assoc];
            recency_position = new int[Config.proc.cache_assoc];

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

                if (cache[set_index, i] == block_addr) {
                    //hit
                    hit++;
                    timestamp[set_index, i] = cycle;
                    if (inst_type == ReqType.WR)
                        dirty[set_index, i] = true;

                    return true;
                }
            }
            
            //couldn't find block_addr; miss
            miss++;
            return false;
        }


        public int has_addr_sampled_set(ulong block_addr, ReqType inst_type)
        {
            //progress time
            cycle++;

            //calculate set index
            uint set_index = (uint)(block_addr % set_max);
            
            if (set_index%Config.proc.sample == 0)
            {
                for (int i = 0; i < Config.proc.cache_assoc; i ++)
                {
                    recency_position[i] = 0;
                    for (int j = 0; j < Config.proc.cache_assoc; j ++)
                    {
                        if (timestamp[set_index, i] >= timestamp[set_index, j] && i != j)
                            recency_position[i] ++;
                    }
                }
            }
                
            //search for block
            for (int i = 0; i < Config.proc.cache_assoc; i++) {

                if (cache[set_index, i] == block_addr && set_index%Config.proc.sample == 0) {
                    return (recency_position[i] + 1000);
                }
            }
            if (set_index % Config.proc.sample == 0) return 1000;
            else return 100000;
            //couldn't find block_addr; miss
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
            int lru_entry_index = -1;
            ulong lru_timestamp = ulong.MaxValue;

            for (int i = 0; i < Config.proc.cache_assoc; i++)
            {
                //make sure not already in cache
                Debug.Assert(cache[set_index, i] != block_addr);

                if (cache[set_index, i] == Proc.NULL_ADDRESS) {
                    //found empty entry
                    empty_entry_index = i;
                    break;
                }

                if (timestamp[set_index, i] < lru_timestamp) {
                    lru_timestamp = timestamp[set_index, i];
                    lru_entry_index = i;
                }
            }

            ulong return_addr = Proc.NULL_ADDRESS;

            //populate empty entry
            if (empty_entry_index != -1) {
                cache[set_index, empty_entry_index] = block_addr;
                timestamp[set_index, empty_entry_index] = cycle;
                dirty[set_index, empty_entry_index] = (inst_type == ReqType.WR);
                core_id[set_index, empty_entry_index] = pid;


                return return_addr;
            }

            

            if (dirty[set_index, lru_entry_index])
                return_addr = cache[set_index, lru_entry_index];


            cache[set_index, lru_entry_index] = block_addr;
            timestamp[set_index, lru_entry_index] = cycle;
            dirty[set_index, lru_entry_index] = (inst_type == ReqType.WR);
            core_id[set_index, lru_entry_index] = pid;


            return return_addr;

        }


        public ulong cache_add(ulong block_addr, ReqType inst_type)
        {
            //progress time
            cycle++;

            //calculate set index
            int set_index = (int)(block_addr % set_max);

            //empty entry within a set
            int empty_entry_index = -1;

            //lru entry within a set
            int lru_entry_index = -1;
            ulong lru_timestamp = ulong.MaxValue;

            //search for empty or lru entry
            for (int i = 0; i < Config.proc.cache_assoc; i++)
            {
                //make sure not already in cache
                Debug.Assert(cache[set_index, i] != block_addr);

                if (cache[set_index, i] == Proc.NULL_ADDRESS) {
                    //found empty entry
                    empty_entry_index = i;
                    break;
                }

                if (timestamp[set_index, i] < lru_timestamp) {
                    lru_timestamp = timestamp[set_index, i];
                    lru_entry_index = i;
                }
            }

            ulong return_addr = Proc.NULL_ADDRESS;

            //populate empty entry
            if (empty_entry_index != -1) {
                cache[set_index, empty_entry_index] = block_addr;
                timestamp[set_index, empty_entry_index] = cycle;
                dirty[set_index, empty_entry_index] = (inst_type == ReqType.WR);

                return return_addr;
            }


            //replace lru entry
            if (dirty[set_index, lru_entry_index])
                return_addr = cache[set_index, lru_entry_index];

            cache[set_index, lru_entry_index] = block_addr;
            timestamp[set_index, lru_entry_index] = cycle;
            dirty[set_index, lru_entry_index] = (inst_type == ReqType.WR);

            return return_addr;

        }
    }//class
}//namespace
