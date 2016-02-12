using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MemMap
{
    public class DDR3DRAM
    {
        //constructor
        public DDR3DRAM(DDR3Enum type, uint clock_factor, uint tWR, uint tWTR) {
            timing = new Timing();
            switch (type) {
                case DDR3Enum.DDR3_2Gb_x8_1066_8_8_8:
                    DDR3_2Gb_x8_1066_8_8_8();
                    break;
                case DDR3Enum.DDR3_2Gb_x8_1333_10_10_10:
                    DDR3_2Gb_x8_1333_10_10_10();
                    break;
                case DDR3Enum.DDR3_2Gb_x8_1600_11_11_11:
                    DDR3_2Gb_x8_1600_11_11_11();
                    break;
                case DDR3Enum.PCM_2Gb_x8_1066_36_8_8:
                    PCM_2Gb_x8_1066_36_8_8();
                    break;
            }

            if (tWR != 0)
                timing.tWR = tWR;

            if (tWTR != 0)
                timing.tWTR = tWTR;

            timing.scale(clock_factor);

            /*
            if (tCCD != 0)
                timing.tCCD = tCCD;

            if (tBL != 0)
                timing.tBL = tBL;
            */
        }

        public enum DDR3Enum
        {
            //tRCD-tRP-tCL
            DDR3_2Gb_x8_1066_8_8_8,
            DDR3_2Gb_x8_1333_10_10_10,
            DDR3_2Gb_x8_1600_11_11_11,
            PCM_2Gb_x8_1066_36_8_8
        }

        public uint CHANNEL_WIDTH = 64;
        public uint BANK_MAX = 8;
        public uint ROW_MAX;
        public uint COL_MAX;    //Number of DEVICE_WIDTHs in a row
        public uint DEVICE_WIDTH;
        public uint DEVICES_PER_RANK;
        public Timing timing;

        private void DDR3_2Gb_x8_1066_8_8_8() {
            BANK_MAX = 8;   //fixed for DDR3
//            BANK_MAX = 4;   //Lavanya's hack 
            Console.Write(" bank max " + BANK_MAX + "\n");
            ROW_MAX = 32 * 1024;
            COL_MAX = 8 * 1024 / 64;
            DEVICE_WIDTH = 8;
            DEVICES_PER_RANK = CHANNEL_WIDTH / DEVICE_WIDTH;

            timing.tCK = 1.875;    //ns

//            timing.tRC = 0;
//            timing.tRAS = 0;
//            timing.tRP = 0;    //8_8_8


            timing.tRC = 28;
            timing.tRAS = 20;
            timing.tRP = 8;    //8_8_8


            timing.tCCD = 4;
            timing.tWTR = 4;

            timing.tCL = 8;    //8_8_8
            timing.tCWL = 6;
//            timing.tBL = 0;
            timing.tBL = 4;

            timing.tRCD = 8;   //8_8_8
            timing.tRTP = 4;
            timing.tWR = 8;

            timing.tRRD = 4;
            timing.tFAW = 20;

//            timing.tRTRS = 0;
            timing.tRTRS = 1;

            timing.tRTW = (timing.tCL - timing.tCWL + timing.tCCD + 2);
        }

        private void PCM_2Gb_x8_1066_36_8_8()
        {
            BANK_MAX = 8;   //fixed for DDR3
            ROW_MAX = 32 * 1024;
            COL_MAX = 8 * 1024 / 64;
            DEVICE_WIDTH = 8;
            DEVICES_PER_RANK = CHANNEL_WIDTH / DEVICE_WIDTH;

            timing.tCK = 1.875;    //ns

            timing.tRC = 28;
            timing.tRAS = 20;
            timing.tRP = 8;    //36_8_8

            timing.tCCD = 4;
            timing.tWTR = 4;

            timing.tCL = 8;    //36_8_8
            timing.tCWL = 6;
            timing.tBL = 4;

            timing.tRCD = 36;   //36_8_8
            timing.tRTP = 4;
            timing.tWR = 96;

            timing.tRRD = 4;
            timing.tFAW = 20;

            timing.tRTRS = 1;

            timing.tRTW = (timing.tCL - timing.tCWL + timing.tCCD + 2);
        }
        
        private void DDR3_2Gb_x8_1333_10_10_10() {
            BANK_MAX = 8;   //fixed for DDR3
            ROW_MAX = 32 * 1024;
            COL_MAX = 8 * 1024 / 64;
            DEVICE_WIDTH = 8;
            DEVICES_PER_RANK = CHANNEL_WIDTH / DEVICE_WIDTH;

            timing.tCK = 1.5;  //ns

            timing.tRC = 34;
            timing.tRAS = 24;
            timing.tRP = 10;   //10_10_10

            timing.tCCD = 4;
            timing.tWTR = 5;

            timing.tCL = 10;   //10_10_10
            timing.tCWL = 7;
            timing.tBL = 4;

            timing.tRCD = 10;  //10_10_10
            timing.tRTP = 5;
            timing.tWR = 10;

            timing.tRRD = 4;
            timing.tFAW = 20;

            timing.tRTRS = 1;

            timing.tRTW = (timing.tCL - timing.tCWL + timing.tCCD + 2);
        }

        private void DDR3_2Gb_x8_1600_11_11_11()
        {
            BANK_MAX = 8;   //fixed for DDR3
            ROW_MAX = 32 * 1024;
            COL_MAX = 8 * 1024 / 64;
            DEVICE_WIDTH = 8;
            DEVICES_PER_RANK = CHANNEL_WIDTH / DEVICE_WIDTH;

            timing.tCK = 1.25;  //ns

            timing.tRC = 39;
            timing.tRAS = 28;
            timing.tRP = 11;   //11_11_11

            timing.tCCD = 4;
            timing.tWTR = 6;

            timing.tCL = 11;   //11_11_11
            timing.tCWL = 8;
            timing.tBL = 4;

            timing.tRCD = 11;  //11_11_11
            timing.tRTP = 6;
            timing.tWR = 12;

            timing.tRRD = 5;
            timing.tFAW = 24;

            timing.tRTRS = 1;

            timing.tRTW = (timing.tCL - timing.tCWL + timing.tCCD + 2);

        }

        public class Timing
        {
            /***** Timing Constraints *****/
            public double tCK; //clock cycle (ns)

            //----------------------------------------------------------//
            //-----Timing constraints between commands to SAME BANK-----//
            //----------------------------------------------------------//
            //Between row commands
            public uint tRC;   //ACTIVATE-to-ACTIVATE
            public uint tRAS;  //ACTIVATE-to-PRECHARGE 
            public uint tRP;   //PRECHARGE-to-ACTIVATE
            //PRECHARGE-to-PRECHARGE (no constraint; can be issued consecutively)

            //Between column commands
            public uint tCCD;  //READ-to-READ and WRITE-to-WRITE (tCCD >= tBL to avoid data bus conflict)
            public uint tRTW;  //READ-to-WRITE (function of other timing constraints: tCL-tCWL+tCCD+2)
            public uint tWTR;  //WRITE*-to-READ (*starts counting from first rising clock after last write data)

            //Between column command and first data
            public uint tCL;   //READ-to-DATA
            public uint tCWL;  //WRITE-to-DATA
            public uint tBL;   //DATA

            //Between row and column commands
            public uint tRCD;  //ACTIVATE-to-READ/WRITE
            public uint tRTP;  //READ-to-PRECHARGE
            public uint tWR;   //WRITE*-to-PRECHARGE (*starts counting from first rising clock after last write data)

            //----------------------------------------------------------------//
            //-----Timing constraints between commands to DIFFERENT BANKS-----//
            //----------------------------------------------------------------//
            //Between row commands
            public uint tRRD;  //ACTIVATE-to-ACTIVATE
            //ACTIVATE-to-PRECHARGE (no constraint; can be issued consecutively)
            //PRECHARGE-to-ACTIVATE (no constraint; can be issued consecutively)
            //PRECHARGE-to-PRECHARGE (no constraint; can be issued consecutively)
            public uint tFAW;  //Minimum time between five ACTIVATEs (subsumed by tRRD when 4 x tRRD >= tFAW)

            //Between column commands
            //READ-to-READ and WRITE-to-WRITE (same constraint as issuing commands to same bank)
            //READ-to-WRITE (same constraint as issuing commands to same bank)
            //WRITE*-to-READ (same constraint as issuing commands to same bank)

            //Between column command and first data (not applicable)

            //Between row and column commands
            //ACTIVATE-to-READ/WRITE (no constraint; can be issued consecutively)
            //READ-to-PRECHARGE (no constraint; can be issued consecutively)
            //WRITE-to-PRECHARGE (no constraint; can be issued consecutively)
            //READ/WRITE-to-ACTIVATE (no constraint; can be issued consecutively)

            //----------------------------------------------------------------//
            //-----Timing constraints between commands to DIFFERENT RANKS-----//
            //----------------------------------------------------------------//
            //Between row commands (no constraint; can be issued consecutively)

            //Between column commands (according to "DRAMSim2" from University of Maryland)
            public uint tRTRS;  //READ-to-READ and WRITE-to-WRITE (bubbles need be inserted in data bus, the number of which is tRTRS)
            //READ-to-WRITE (same constraint as issuing commands to same bank)
            //WRITE*-to-READ (same constraint as issuing commands to same bank)

            //Between column command and first data (not applicable)

            //Between row and column commands (no constraint; can be issued consecutively)

            //----------------------------------------------------------------//
            //-----Scale Timing-----------------------------------------------//
            //----------------------------------------------------------------//
            public void scale(uint clock_factor)
            {
                tRC *= clock_factor;
                tRAS *= clock_factor;
                tRP *= clock_factor;

                tCCD *= clock_factor;
                tRTW *= clock_factor;
                tWTR *= clock_factor;

                tCL *= clock_factor;
                tCWL *= clock_factor;
                tBL *= clock_factor;

                tRCD *= clock_factor;
                tRTP *= clock_factor;
                tWR *= clock_factor;

                tRRD *= clock_factor;
                tFAW *= clock_factor;

                tRTRS *= clock_factor;
            }
        }
    }
}






