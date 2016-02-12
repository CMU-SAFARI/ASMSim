using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace MemMap
{
    public class StridePrefetcher
    {
        int degree;
        int table_size;
        int num_trains;
        int distance; 

        StrideEntry[] stride_entries;
        ulong[] pc;
        bool[] valid;

        public StridePrefetcher ()
        {
            degree = 4;
            table_size = 16;
            num_trains = 2;
            distance = 24;
    
            stride_entries = new StrideEntry[table_size];         
            pc = new ulong[table_size];
            valid = new bool[table_size];
        }
    
        public bool pc_present (ulong input_pc)
        {
            for (int i = 0; i < table_size; i ++)
            {
                if (valid[i] == true && pc[i] == input_pc) 
                {
                    return true;
                }
            }
            return false;
        }
    
        public int entry_position (ulong input_pc)
        {
            for (int i = 0; i < table_size; i ++)
            {
                if (valid[i] == true && pc[i] == input_pc) return i;
            }
            return 100;
        }
     
        public void insert_entry (ulong input_pc, StrideEntry input_stride_entry)
        {
            for (int i = table_size-1; i > 0; i --)
            {
                valid[i] = valid[i-1];
                pc[i] = pc[i-1];
                stride_entries[i] = stride_entries[i-1];
            }
    
            valid[0] = true;
            pc[0] = input_pc;
            stride_entries[0] = input_stride_entry; 
        }

        public void reorder_entries(int hit_position)
        {
            StrideEntry hit_position_entry = stride_entries[hit_position];
            bool hit_position_valid = valid[hit_position];
            ulong hit_position_pc = pc[hit_position];
            for (int i = hit_position; i > 0; i --)
            {
                valid[i] = valid[i-1];
                pc[i] = pc[i-1];
                stride_entries[i] = stride_entries[i-1];
            }

            valid[0] = hit_position_valid;
            pc[0] = hit_position_pc;
            stride_entries[0] = hit_position_entry; 
        }

    
        public Req create_new_prefetch_req (int pid, ulong prefetch_block_addr)
        {
    
            ulong addr = prefetch_block_addr << Config.proc.block_size_bits;
            Req new_prefetch_req = RequestPool.depool();
            new_prefetch_req.set_prefetch(pid, ReqType.RD, ReqType.RD, addr);         
    
            return new_prefetch_req;
        }

         private bool insert_mctrl(Req req)
        {
            MemAddr addr = req.addr;

            if (Sim.mctrls[addr.cid].is_q_full(req.pid, req.type, addr.rid, addr.bid)) {
                return false;
            }
            //success
            send_req(req);
            return true;
        }

        private void send_req(Req req)
        {
            Sim.procs[req.pid].inflight_mem_requests ++;
            req.callback = new Callback(Sim.procs[req.pid].recv_req);
            Sim.mctrls[req.addr.cid].enqueue_req(req);
        }
    
        public void process_request (int pid, ulong block_addr, ulong input_pc) {
            if (!pc_present(input_pc)) {
                StrideEntry new_stride_entry = new StrideEntry();
                new_stride_entry.block_addr = block_addr; 
                new_stride_entry.trained = false;
                new_stride_entry.stride = 0;
                new_stride_entry.train_hits = 0; 
    
                insert_entry(input_pc,new_stride_entry);
                return;
            }
            int hit_position = entry_position(input_pc); 
            int current_stride = (int)(block_addr - stride_entries[hit_position].block_addr); 
    
            if (current_stride != stride_entries[hit_position].stride) 
            {
                stride_entries[hit_position].stride = current_stride;
                stride_entries[hit_position].trained = false;
                stride_entries[hit_position].train_hits = 0;
            }
    
            stride_entries[hit_position].block_addr = block_addr;
    
            if (!stride_entries[hit_position].trained) {
                stride_entries[hit_position].train_hits ++;
                stride_entries[hit_position].prefetch_block_addr = block_addr;
            }
    
    
            if (stride_entries[hit_position].train_hits >= num_trains) stride_entries[hit_position].trained = true;
    
            if (stride_entries[hit_position].stride == 0) return;
    
            if (stride_entries[hit_position].trained == true)
            {
                ulong max_block_address = stride_entries[hit_position].block_addr + (ulong)((distance + 1) * stride_entries[hit_position].stride);
                int max_prefetches = (int)(((int)max_block_address - (int)stride_entries[hit_position].prefetch_block_addr)/stride_entries[hit_position].stride);
                int num_prefetches = (max_prefetches > degree) ? degree : max_prefetches;

                   
                for (int i = 0; i < num_prefetches; i ++)
                {
                    stride_entries[hit_position].prefetch_block_addr += (ulong)stride_entries[hit_position].stride; 
                    Req new_prefetch = create_new_prefetch_req(pid, stride_entries[hit_position].prefetch_block_addr);
         
                    bool mctrl_ok = insert_mctrl(new_prefetch);
                    if (!mctrl_ok) RequestPool.enpool(new_prefetch);
                    else Stat.procs[new_prefetch.pid].num_prefetches.Collect();;
                }
            }
    
            return;
             
        }
   }

}
