using System;
using System.Collections.Generic;
using System.Text;

using System.IO;

namespace MemMap
{
    public class PageRandomizer
    {
        public ulong page_size;
        public Dictionary<ulong, ulong> ptable; //pages
        public List<ulong> ftable;              //frames

        public Random rand = new Random(0);

        public PageRandomizer(ulong page_size)
        {
            this.page_size = page_size;
            this.ptable = new Dictionary<ulong, ulong>();
            this.ftable = new List<ulong>();
        }

        public ulong get_paddr(ulong paddr)
        {
            ulong page_id = paddr / page_size;
            ulong page_mod = paddr % page_size;

            if (ptable.ContainsKey(page_id)) {
                return ptable[page_id] * page_size + page_mod;
            }

            ulong frame_id;
            while (true){
                frame_id = (ulong) rand.Next();
                if (ftable.Contains(frame_id)) {
                    continue;
                }

                ftable.Add(frame_id);
                break;
            }

            ptable.Add(page_id, frame_id);
            return frame_id * page_size + page_mod;
        }
    }
}
