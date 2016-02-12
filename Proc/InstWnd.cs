using System;
using System.Collections.Generic;
using System.Text;


namespace MemMap
{
    public class InstWnd
    {
        public int size;
        public int load;           //number of instructions currently in the window

        public int oldest;         //index of the oldest instruction in the window
        public int next;           //index of the next empty slot in the window

        public ulong[] addr;       //array of instructions (memory request block addresses)
        public ulong[] pc;       //array of instructions (memory request block addresses)
        public bool[] ready;       //array of instructions (whether they are ready)
        public bool[] is_mem;
        public bool[] is_alone_hit;

        public InstWnd(int size)
        {
            this.size = size + 1;
            next = oldest = 0;

            addr = new ulong[size + 1];
            pc = new ulong[size + 1];
            ready = new bool[size + 1];
            is_mem = new bool[size + 1];
            is_alone_hit = new bool[size + 1];
            for (int i = 0; i < size + 1; i++) {
                ready[i] = true;
            }
        }

        public bool is_full()
        {
           
            return load == size - 1;
        }

        public bool is_empty()
        {
            return load == 0;
        }

        public void add(ulong block_addr, bool is_mem_inst, bool is_ready, ulong input_pc)
        {
            Dbg.Assert(load < size - 1);
            load++;

            addr[next] = block_addr;
            ready[next] = is_ready;
            is_mem[next] = is_mem_inst;
            pc[next] = input_pc;

            next = (next + 1) % size;
        }


        public void add(ulong block_addr, bool is_mem_inst, bool is_ready, bool is_alone_hit_req, ulong input_pc)
        {
            Dbg.Assert(load < size - 1);
            load++;

            addr[next] = block_addr;
            ready[next] = is_ready;
            is_mem[next] = is_mem_inst;
            is_alone_hit[next] = is_alone_hit_req;
            pc[next] = input_pc;

            next = (next + 1) % size;
        }

        public void print_all()
        {
            int count = 0;
            int start = oldest;
            while (count < size)
            {
                count ++;
                start = (start + 1)%size;
            }
            
        }

        public int retire(int n)
        {
            int retired = 0;

            while (oldest != next && retired < n) {
                if (!ready[oldest])
                    break;

                oldest = (oldest + 1) % size;
                load--;
                retired++;
            }

            return retired;
        }

        public bool is_duplicate(ulong block_addr)
        {
            int count = 0;
            int i = oldest;
            while (i != next) {
                if (is_mem[i] && addr[i] == block_addr) {
                    if (++count > 0) {
                        return true;
                    }
                }
                i = (i + 1) % size;
            }
            return false;
        }


        public bool get_ready_status(ulong block_addr)
        {
            int i = oldest;
            while (i != next) {
                if (is_mem[i] && addr[i] == block_addr) {
                        return ready[i];
                }
                i = (i + 1) % size;
            }
            return false;
        }


        public bool get_alone_hit_status(ulong block_addr)
        {
            int i = oldest;
            while (i != next) {
                if (is_mem[i] && addr[i] == block_addr) {
                        return is_alone_hit[i];
                }
                i = (i + 1) % size;
            }
            return false;
        }

        public void set_ready(ulong block_addr)
        {

            int i = oldest;
            while (i != next) {
                if (is_mem[i] && addr[i] == block_addr) {
                    ready[i] = true;
                    addr[i] = 0;
                }
                i = (i + 1) % size;
            }
        }
        public bool is_oldest_mem()
        {
            return is_mem[oldest];
        }

        public bool is_oldest_ready()
        {
            return ready[oldest];
        }


        public bool is_oldest_alone_hit()
        {
            return is_alone_hit[oldest];
        }

        public ulong addr_oldest()
        {
            return addr[oldest];
        }

        public ulong pc_oldest()
        {
            return pc[oldest];
        }
        public void print_oldest()
        {
              Console.Write(" OLDEST Addr - " + addr[oldest] + " ready " + ready[oldest] + " is mem " + is_mem[oldest] + "\n");
        }

    }
}
