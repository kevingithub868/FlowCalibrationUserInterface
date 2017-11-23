﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Model
{  
    public class MotorControl
	// MotorControl - Controls the motor with a list of position and times or velocity and times. 
	//     		      Can convert from position to ticks or velocity to ticks per second and the other way.
	//				  Implements homing sequence.
    //                Needs to have the pulley wheel size as parameter.
    //                Assumes to get an initialized modbus object when created
    {
        ModbusCommunication ModCom { get; set; }

        List<Double> RecordedTimes { get; set; }
        List<Double> RecordedPositions { get; set; }
        List<Double> RecordedVelocities { get; set; }
        List<Double> RecordedTorques { get; set; }
        List<Double> RecordedPressures { get; set; }

        struct Hardware
        {
            //DEFINE HARDWARE PARAMETERS
            public const int Pitch = 32; // [mm] of gearwheel
            public const int TicksPerRev = 4096; // [ticks per revolution position data]
            public const int VelocityResolution = 16; // [velocity resolution is position resolution / constant]
            public const int TimePerSecond = 2000; // [time register +2000 each second]
            public const int MotorTorquePerTorque = 1000; // [motor Torue [mNm] per Torque [Nm]]
            public const double PressureGain = 1; // [motor Pressure [VDC] to Pressure [?] gain]
            public const double PressureBias = 0; // [motor Pressure [VDC] to Pressure [?] bias]
        }
        struct Register
        {
            // DEFINE REGISTERS 
            // REGISTER: 450/451 (int32)
            public const ushort TargetInput = 450;
            // REGISTER: 200/201
            public const ushort Position = 200;
            // REGISTER: 202 
            public const ushort Speed = 202;
            // REGISTER: 203 
            public const ushort Torque = 203;
            // REGISTER: 420/421
            public const ushort Time = 420;
            // REGISTER: 170-173
            public const ushort Pressure = 170;
            // REGISTER: 353 
            public const ushort Acceleration = 353;
            // REGISTER: 354 
            public const ushort Deacceleration = 354;
            // PositionRamp (Mode 21): Closed control of position with ramp control.
            // SpeedRamp (Mode 33): Speed control mode with ramp control.
            // Shutdown (Mode 4)
            public const ushort Mode = 400;

            // 20 possible events. Mapped by increase of 20.... I guess
            // goes from 680 to 699
            public const ushort EventControl = 680;
            // goes from 700 to 719
            public const ushort EventTrgReg = 700;
            // goes from 720 to 739
            public const ushort EventTrgData = 720;
            // goes from 740 to 759
            public const ushort EventSrcReg = 740;
			// goes from 760 to 779
			public const ushort EventSrcData = 760;
			// goes from 780 to 799
			public const ushort EventDstReg = 780;

        }
        struct Mode
        {
            public const Int16 PositionRamp = 21;
            public const Int16 SpeedRamp = 33;
            public const Int16 Shutdown = 4;
            public const Int16 MotorOff = 0;
        }

        struct EventLogic
        {
            /* Note that only bits 0-3 of Register.EventControl is considered here.
            0    Always      true
            1   =       Equal
            2   !=      Not equal
            3   <       Less than
            4   >       Greater than
            5   or      Bitwise or
            6   nor     Bitwise not or
            7   and     Bitwise and
            8   nand    Bitwise not and
            9   xor     Bitwise exclusive or
            10  nxor    Bitwise not exclusive or
            11  +       Add
            12  -       Subtract
            13  *       Multiply
            14  /       Divide
            15  Value   Takes data value directly
            */

            // defined in hex form to make it clearer that it's about manipulation of bit 0-3
            public const Int16 GreaterThan = 0X4000;
            public const Int16 UseValue = 0X000F;
            // the final value is composed of different values for different bits. Hardcoded for now
            public const Int16 HardCodedTemp = 0X400F;
        }
        public void MotorSafetyInit(ushort MaxTorque)
        {
            // Set the max allowed torqe
            ModCom.RunModbus(Register.EventTrgData, MaxTorque);
            // Select the Torque as value of interest
            ModCom.RunModbus(Register.EventTrgReg, Register.Torque);
            // Select Greater than as event and what to do...
            // TODO: rewrite this bit manipulation to something less horrible
            ModCom.RunModbus(Register.EventControl,EventLogic.HardCodedTemp);
            // Select the mode register as target register for event
            ModCom.RunModbus(Register.EventDstReg,Register.Mode);
            // Select MotorOff as response to event.
            ModCom.RunModbus(Register.EventSrcData,Mode.MotorOff)
        }

        
        public MotorControl(ModbusCommunication modCom)
        {
            ModCom = modCom;

            RecordedTimes = new List<Double>();
            RecordedPositions = new List<Double>();
            RecordedVelocities = new List<Double>();
        }

        public void RunTickSequence(List<int> ticks, List<double> times)
        {
            if (ticks.Count() != times.Count())
            {
                throw new Exception("Input lists not of equal length");
            }

            // Set mode
            ModCom.RunModbus(Register.Mode, Mode.PositionRamp);

            Stopwatch stopWatch = new Stopwatch();

            List<int> MotorRecordedTimes = new List<int>();
            List<int> MotorRecordedPositions = new List<int>();
            List<int> MotorRecordedVelocities = new List<int>();
            List<int> MotorRecordedTorques = new List<int>();
            List<int> MotorRecordedPressures = new List<int>();

            // Set time = 0
            ModCom.RunModbus(Register.Time, (Int32)0);

            stopWatch.Start();

            int i = 0;

            while (i < ticks.Count())
            {
                if (times[i] <= stopWatch.Elapsed.TotalSeconds)
                { // If more time have elapsed than time[i]
                  // Write and read to/from modbus
                    ModCom.RunModbus(Register.TargetInput,(Int32)ticks[i]);
                    MotorRecordedTimes.Add(ModCom.ReadModbus(Register.Time, 2, false));
                    MotorRecordedPositions.Add(ModCom.ReadModbus(Register.Position, 2, true));
                    MotorRecordedVelocities.Add(ModCom.ReadModbus(Register.Speed, 1, false));
                    MotorRecordedTorques.Add(ModCom.ReadModbus(Register.Torque, 1, false));
                    MotorRecordedPressures.Add(ModCom.ReadModbus(Register.Pressure, 1, false));
                    i += 1;
                }
                
                double overtime = 30;
                if (stopWatch.Elapsed.TotalSeconds > overtime)
                {
                    Console.WriteLine("RunTickSequence running more than [overtime] seconds. Function stopped.");
                    stopWatch.Stop();
                    break;
                }
            }
            stopWatch.Stop();

            // Go to position zero
            ModCom.RunModbus(Register.TargetInput, (Int32)0);

            // Convert units
            RecordedTimes = TimeToSeconds(MotorRecordedTimes);
            RecordedPositions = TickToPosition(MotorRecordedPositions);
            RecordedVelocities = TicksPerSecondToVelocity(MotorRecordedVelocities);
            RecordedTorques = MotorTorquesToTorques(MotorRecordedTorques);
            RecordedPressures = MotorPressureToPressure(MotorRecordedPressures);
        }

        public void RunTicksToVelocitySequence(List<int> ticksPerSecond, List<double> times)
        {
            if (ticksPerSecond.Count() != times.Count())
            {
                throw new Exception("Input lists not of equal length");
            }

            // Set mode
            ModCom.RunModbus(Register.Mode, Mode.SpeedRamp);

            Stopwatch stopWatch = new Stopwatch();

            List<int> MotorRecordedTimes = new List<int>();
            List<int> MotorRecordedPositions = new List<int>();
            List<int> MotorRecordedVelocities = new List<int>();

            // Set time = 0
            ModCom.RunModbus(Register.Time, (Int32)0);

            stopWatch.Start();

            int i = 0;

            while (i < ticksPerSecond.Count())
            {
                if (times[i] <= stopWatch.Elapsed.TotalSeconds)
                { // If more time have elapsed than time[i]
                  // Write and read to/from modbus
                    ModCom.RunModbus(Register.TargetInput, (Int32)ticksPerSecond[i]);
                    MotorRecordedTimes.Add(ModCom.ReadModbus(Register.Time, 2, false));
                    MotorRecordedPositions.Add(ModCom.ReadModbus(Register.Position, 2, true));
                    MotorRecordedVelocities.Add(ModCom.ReadModbus(Register.Speed, 1, false));
                    i += 1;
                }

                double overtime = 30;
                if (stopWatch.Elapsed.TotalSeconds > overtime)
                {
                    Console.WriteLine("RunTickSequence running more than [overtime] seconds. Function stopped.");
                    stopWatch.Stop();
                    break;
                }
            }
            stopWatch.Stop();

            // Go to speed zero
            ModCom.RunModbus(Register.TargetInput, (Int32)0);

            // Convert units
            RecordedTimes = TimeToSeconds(MotorRecordedTimes);
            RecordedPositions = TickToPosition(MotorRecordedPositions);
            RecordedVelocities = TicksPerSecondToVelocity(MotorRecordedVelocities);
        }

        public List<int> PositionToTick(List<Double> positions)
        {
            List<int> ticks = new List<int>();
            for (int i = 0; i < positions.Count; i++)
            {
                ticks.Add( (int)Math.Round(positions[i] * 10 * Hardware.TicksPerRev / Hardware.Pitch));
            }
            return ticks;
        }

        public List<Double> TickToPosition(List<int> ticks)
        {
            List<Double> position = new List<Double>();
            for (int i = 0; i < ticks.Count; i++)
            {
                position.Add(ticks[i] * Hardware.Pitch / Hardware.TicksPerRev /10);
            }
            return position;
            
        }

        public List<Double> TicksPerSecondToVelocity(List<int> ticksPerSecond)
        {
            List<Double> velocity = new List<Double>();
            for (int i = 0; i < ticksPerSecond.Count; i++)
            {
                velocity.Add(ticksPerSecond[i] * Hardware.Pitch * Hardware.VelocityResolution/Hardware.TicksPerRev/10);
            }
            return velocity;
        }

        public List<Double> TimeToSeconds(List<int> time)
        {
            List<Double> seconds = new List<Double>();
            for (int i = 0; i < time.Count; i++)
            {
                seconds.Add( time[i] / Hardware.TimePerSecond);
            }
            return seconds;
        }

        public List<Double> MotorTorquesToTorques(List<int> motorTorques)
        {
            List<Double> torques = new List<Double>();
            for (int i = 0; i < motorTorques.Count; i++)
            {
                torques.Add(motorTorques[i] / Hardware.MotorTorquePerTorque);
            }
            return torques;
        }
        public List<Double> MotorPressureToPressure(List<int> motorPressure)
        {
            List<Double> pressure = new List<Double>();
            for (int i = 0; i < motorPressure.Count; i++)
            {
                pressure.Add(motorPressure[i] * Hardware.PressureGain + Hardware.PressureBias);
            }
            return pressure;
        }
    }
}
