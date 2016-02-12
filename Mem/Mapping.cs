using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

namespace MemMap
{
    public class MemAddr
    {
        public uint cid;
        public uint rid;
        public uint bid;
        public ulong rowid;
        public uint colid;
    }

    public static class MemMap
    {
        public enum MapEnum
        {
            //inter-channel
            ROW_RANK_BANK_CHAN_COL,         //row-interleaving
            ROW_RANK_BANK_COL_CHAN,         //block-interleaving
            ROW_RANK_BANK_COL1_CHAN_COL2,   //intermediate-interleaving

            //inter-bank
            ROW_CHAN_RANK_BANK_COL,         //row-interleaving
            ROW_CHAN_COL_RANK_BANK,         //block-interleaving
            ROW_CHAN_COL1_RANK_BANK_COL2,   //intermediate-interleaving

            //inter-channel/bank
            ROW_COL_RANK_BANK_CHAN,         //block-interleaving
            ROW_COL1_RANK_BANK_CHAN_COL2,   //intermediate-interleaving

            //inter-bank/channel
            ROW_COL_CHAN_RANK_BANK,         //block-interleaving
            ROW_COL1_CHAN_RANK_BANK_COL2,   //intermediate-interleaving
        }
        public static MapEnum map_type;
        public static uint channel_max;

        //bits
        private static uint chan_bits;
        private static uint rank_bits;
        private static uint bank_bits;
        private static uint col_bits;
        private static uint col1_bits;
        private static uint col2_bits;
        private static uint transfer_bits;

        //offset
        private static uint chan_offset;
        private static uint rank_offset;
        private static uint bank_offset;
        private static uint row_offset;
        private static uint col_offset;
        private static uint col1_offset;
        private static uint col2_offset;

        //masks
        private static ulong chan_mask;
        private static ulong rank_mask;
        private static ulong bank_mask;
        private static ulong col_mask;
        private static ulong col1_mask;
        private static ulong col2_mask;

        //constructor
        public static void init(MapEnum map_type, uint channel_max, uint rank_max, uint col_per_subrow, DDR3DRAM ddr3)
        {
            MemMap.map_type = map_type;
            MemMap.channel_max = channel_max;

            //bits
            chan_bits = (uint)Math.Log(channel_max, 2);
            rank_bits = (uint)Math.Log(rank_max, 2);
            bank_bits = (uint)Math.Log(ddr3.BANK_MAX, 2);
            col_bits = (uint)Math.Log(ddr3.COL_MAX, 2);
            if (col_per_subrow > 0) {
                col2_bits = (uint)Math.Log(col_per_subrow, 2);
                col1_bits = col_bits - col2_bits;
            }
            else {
                col2_bits = 0;
            }
            transfer_bits = (uint)Math.Log(64, 2); //64B transfer

            //mask, offset
            set_maskoffset();
        }

        //MemoryAddress
        public static MemAddr translate(ulong paddr) {
            MemAddr addr = new MemAddr();
            addr.cid = (uint)((paddr & chan_mask) >> (int)chan_offset);
            addr.rid = (uint)((paddr & rank_mask) >> (int)rank_offset);
            addr.bid = (uint)((paddr & bank_mask) >> (int)bank_offset);
            addr.rowid = (ulong)(paddr >> (int)row_offset);
            if (col2_bits == 0) {
                addr.colid = (uint)((paddr & col_mask) >> (int)col_offset);
            }
            else {
                uint col2id = (uint)((paddr & col2_mask) >> (int)col2_offset);
                uint col1id = (uint)((paddr & col1_mask) >> (int)col1_offset);
                uint colid = col2id + (col1id << (int)col2_bits);
                addr.colid = colid;
            }

            return addr;
        }

        private static void set_maskoffset(ref ulong mask, uint bits, ref uint offset, ref uint curr_offset)
        {
            offset = curr_offset;
            mask = 1;
            mask = (mask << (int) bits) - 1;
            mask <<= (int) offset;

            curr_offset += bits;
        }

        private static void chan(ref uint curr_offset)
        {
            set_maskoffset(ref chan_mask, chan_bits, ref chan_offset, ref curr_offset);
        }
        private static void rank(ref uint curr_offset)
        {
            set_maskoffset(ref rank_mask, rank_bits, ref rank_offset, ref curr_offset);
        }
        private static void bank(ref uint curr_offset)
        {
            set_maskoffset(ref bank_mask, bank_bits, ref bank_offset, ref curr_offset);
        }
        private static void row(ref uint curr_offset)
        {
            row_offset = curr_offset;
        }
        private static void col(ref uint curr_offset)
        {
            set_maskoffset(ref col_mask, col_bits, ref col_offset, ref curr_offset);
        }
        private static void col1(ref uint curr_offset)
        {
            set_maskoffset(ref col1_mask, col1_bits, ref col1_offset, ref curr_offset);
        }
        private static void col2(ref uint curr_offset)
        {
            set_maskoffset(ref col2_mask, col2_bits, ref col2_offset, ref curr_offset);
        }

        private static void set_maskoffset() {
            uint curr_offset = transfer_bits;
            switch (map_type) {
                //intra-channel
                case MapEnum.ROW_RANK_BANK_CHAN_COL:
                    col(ref curr_offset);
                    chan(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_RANK_BANK_COL_CHAN:
                    chan(ref curr_offset);
                    col(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_RANK_BANK_COL1_CHAN_COL2:
                    col2(ref curr_offset);
                    chan(ref curr_offset);
                    col1(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    row(ref curr_offset);
                    break;

                //inter-bank
                case MapEnum.ROW_CHAN_RANK_BANK_COL:
                    col(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    chan(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_CHAN_COL_RANK_BANK:
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    col(ref curr_offset);
                    chan(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_CHAN_COL1_RANK_BANK_COL2:
                    col2(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    col1(ref curr_offset);
                    chan(ref curr_offset);
                    row(ref curr_offset);
                    break;

                //inter-channel/bank
                case MapEnum.ROW_COL_RANK_BANK_CHAN:
                    chan(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    col(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_COL1_RANK_BANK_CHAN_COL2:
                    col2(ref curr_offset);
                    chan(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    col1(ref curr_offset);
                    row(ref curr_offset);
                    break;

                //inter-bank/channel
                case MapEnum.ROW_COL_CHAN_RANK_BANK:
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    chan(ref curr_offset);
                    col(ref curr_offset);
                    row(ref curr_offset);
                    break;
                case MapEnum.ROW_COL1_CHAN_RANK_BANK_COL2:
                    col2(ref curr_offset);
                    bank(ref curr_offset);
                    rank(ref curr_offset);
                    chan(ref curr_offset);
                    col1(ref curr_offset);
                    row(ref curr_offset);
                    break;
            }
        }
    }
}
