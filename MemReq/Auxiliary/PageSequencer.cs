using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class PageSequencer
    {
        public ulong page_size;
        public uint cmax;
        public uint rmax;
        public uint bmax;
        public uint stride;

        public Dictionary<ulong, ulong> ptable; //pages
        public List<ulong> ftable;              //frames

        public ulong curr_fid;

        public Random rand = new Random(0);

        public PageSequencer(ulong page_size, uint cmax, uint rmax, uint bmax)
        {
            this.page_size = page_size;
            this.cmax = cmax;
            this.rmax = rmax;
            this.bmax = bmax;
            this.stride = cmax * rmax * bmax;

            this.ptable = new Dictionary<ulong, ulong>();
            this.ftable = new List<ulong>();
        }

        public ulong get_paddr(ulong paddr)
        {
            ulong page_id = paddr / page_size;
            ulong page_mod = paddr % page_size;

            //page table hit
            if (ptable.ContainsKey(page_id)) {
                return ptable[page_id] * page_size + page_mod;
            }

            //page table miss
            ulong frame_id = page_id / stride;
            frame_id *= stride;
            frame_id += curr_fid;

            //update tables
            Dbg.Assert(!ftable.Contains(frame_id));
            ftable.Add(frame_id);
            ptable.Add(page_id, frame_id);
            
            //update frame id
            curr_fid += 1;
            curr_fid = curr_fid % stride;
            
            //return physical address
            return frame_id * page_size + page_mod;
        }
    }
}