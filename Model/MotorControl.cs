﻿// Importing stuff
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

// Nanmespace is a way of organizing code in C#, just like directories in a computer. This namespace
// contains everything related to running the motor, reading inputs and writing outputs.
namespace Model
{
    /// <summary>
    /// MotorControl - Controls the motor with a list of position and times or velocity and times.
    ///                Also reads the inputs.
    /// </summary>

    // A class for everything related to the MotorControl 
    public class MotorControl
    {
        // Instance of the class ModbusCommuniation 
        ModbusCommunication ModCom { get; set; }


        // Define lists for logged values from the motor
        public List<Double> LoggedVelocities { get; set; }       // List to store the positions from motor
        public List<Double> LoggedTargets { get; set; }         // List to store the current from the motor
        public List<Double> LoggedPressures { get; set; }       // List to store the measured pressure (Analog input)
        public List<Double> LoggedLinearPositions { get; set; } // List to store the measured linear position in mm
        public List<int>    LoggedSensorValues    { get; set; } // List to store the measured linear position sensor value

        // Define list for the time vector corresponding to the logged values
        public List<Double> LoggedTime { get; set; }

        // Creates a struct for the hardware
        struct Hardware
        {
            //DEFINE HARDWARE PARAMETERS
            public const Double Pitch                   = 32.01;            // Circumference of gearwheel [mm]
            public const Double TicksPerRev             = 4096;             // ticks per revolution (4096 positions on one revolution)
            public const Double VelocityResolution      = 16;               // velocity resolution (position resolution / constant)
            public const Double TimePerSecond           = 2000;             // time register (+2000 each second)
            public const Double MotorTorquePerTorque    = 1000;             // motor Torque [mNm] per Torque [Nm]

            public const Double PressureGain            = 1;                // Analog in [VDC] to Pressure [?] gain
            public const Double PressureBias            = 0;                // Analog in [VDC] to Pressure [?] bias
            
            public const Int16  MaxTorque               = 300;              // Maximum allowed torque [mNm]
            
            public const Double HomePosition            = 10.0;             // Homeposition of the system [mm]   
            public const Double HomePosTolerance        = 0.01;             // Tolerance for homepositon [mm]
            public const Double MinPosition             = 5;                // Minimum possible position from linear sensor [mm]
            public const Double MaxPosition             = 98.0;             // Maximum possible position from linear sensor [mm]
            
            // Constants for polynomial to convert from linear sensor reading to mm
            public const Double P1  = 0.037019062835858;
            public const Double P2  = 0.109436390195930;
            public const Double P3  = -0.239489917466821;
            public const Double P4  = -1.777499203864061;
            public const Double P5  = 1.194829066662426;
            public const Double P6  = 35.69061189682709;
            public const Double P7  = 52.382724912178460;
            public const Double Mu1 = 32611.28571428571;
            public const Double Mu2 = 20410.47887932406;

        }

        // Defines a struct for all registers in the motor
        public struct Register
        {
            // DEFINE REGISTERS related to motor drive, inputs and outputs
            public const ushort TargetInput     = 450;      // Regulator target
            public const ushort TargetPresent   = 463;      // Current target value sen to regulator
            public const ushort Position        = 200;      // Motor position           [ticks]
            public const ushort Speed           = 202;      // Motor speed              [positions/sec/16]
            public const ushort Torque          = 203;      // Motor torque             [nNm]
            public const ushort Current         = 223;      // Motor current            [No unit]  
            public const ushort Time            = 420;      // Time                     [2000 counts/second]
            public const ushort Pressure        = 170;      // AnalogIn 1 (Pressure)    [Vdc]
            public const ushort LinearPosition  = 172;      // AnalogIn 2 (Linear pos.) [Vdc]
            public const ushort OutputControl3  = 152;      // Mode of digital output 3
            public const ushort Output3         = 162;      // Value of digital output 3
            public const ushort Acceleration    = 353;      // Acceleration value       [positions/second^2 / 256]
            public const ushort Deacceleration  = 354;      // Deacceleration value     [positions/second^2 / 256]
            public const ushort Mode            = 400;      // Mode of motor drive
            public const ushort MotorTorqueMax  = 204;      // Torque limit value       [nNm]
            public const ushort Status          = 410;      // Current motor drive status (info in different bits)
            public const ushort LogRegister1    = 905;      // Register numbers to log channel 1
            public const ushort LogRegister2    = 906;      // Register numbers to log channel 2
            public const ushort LogRegister3    = 907;      // Register numbers to log channel 3
            public const ushort LogRegister4    = 908;      // Register numbers to log channel 4
            public const ushort LogRegisterValue1 = 1000;     // Logvalue channel 1 start register
            public const ushort LogRegisterValue2 = 2000;     // Logvalue channel 2 start register
            public const ushort LogRegisterValue3 = 3000;     // Logvalue channel 3 start register
            public const ushort LogRegisterValue4 = 4000;     // Logvalue channel 4 start register
            public const ushort LogState          = 900;      // Log Mode register
            public const ushort LogPeriod         = 902;      // Number of skipped regulator cycles between samples
            public const ushort SeqTarget         = 510;      // Register for Sequence target
            public const ushort SeqTime           = 570;      // Register for time for sequence target  
            public const ushort SeqIndex          = 501;      // Register with current sequence index 

            // DEFINE REGISTERS related to events. (see p.19 in manual for information)
            public const ushort EventControl    = 680;      // Control Register for events  (see p.11 in manual)
            public const ushort EventTrgReg     = 700;      // Event Target Register number (see p.12 in manual)
            public const ushort EventTrgData    = 720;      // Event Target Data            (see p.12 in manual)
            public const ushort EventSrcReg     = 740;      // Event Source Register number (see p.12 in manual)
            public const ushort EventSrcData    = 760;      // Event Source Data            (see p.12 in manual)
            public const ushort EventDstReg     = 780;      // Event Destination Register   (see p.12 in manual)

        }

        // Defines a struct for the motor modes
        struct Mode
        {
            public const Int16 PositionRamp = 21;   // PositionRamp(Mode 21) : Closed control of position with ramp control.
            public const Int16 SpeedRamp    = 33;   // SpeedRamp    (Mode 33): Speed control mode with ramp control.
            public const Int16 Shutdown     = 4;    // Shutdown     (Mode 4)
            public const Int16 MotorOff     = 0;    // Stop mode, motor is off 
            public const Int16 Beep         = 60;   // Motor produces sound att 500 Hz
        }

        //EventLogic,
        // The operands are used according to: "Trigger value = <Register> OPERATOR DataValue"
        // see p.20 in manual for more information about thie
        /* Note that only bits 0-3 of Register.EventControl is considered here.
        0           Always true
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
        //

        // Function to create events. See p.19-21 in manual for more information about events
        public void CreateEvent(ushort EventNr,
                                Int16 TrgData,
                                Int16 TrgReg,
                                ushort EventLogic,
                                Int16 DstRegister,
                                ushort SrcData,
                                Int16 SrcRegister)
        {
            /* (ushort) EventNr,        Number between 0-19 
             * (Int16)  TrgData,        Value that is used for event triggers
             * (Int16)  TrgReg,         Register that is read and used to trigger event
             * (ushort) EventLogic,     0XA--B where A is how the event should behave
             *                          and B is how the event should be triggered
             * (Int16)  DstRegister,    Destination register
             * (ushort) SrcData,        What should be written to the destination register
             * (Int16)  SrcRegister,    Combined with SrcData
             */

            // Write to the registers to "implement/create" the events
            ModCom.RunModbus((ushort)(Register.EventTrgData + EventNr), TrgData);
            ModCom.RunModbus((ushort)(Register.EventTrgReg + EventNr), TrgReg);
            ModCom.RunModbus((ushort)(Register.EventControl + EventNr), EventLogic);
            ModCom.RunModbus((ushort)(Register.EventDstReg + EventNr), DstRegister);
            ModCom.RunModbus((ushort)(Register.EventSrcData + EventNr), SrcData);
            ModCom.RunModbus((ushort)(Register.EventSrcReg + EventNr), SrcRegister);
        }

        // Function to set a maximum torque to the motor and to create lists for
        // values to be read from motor controller.
        public MotorControl(ModbusCommunication modCom)
        {
            ModCom = modCom;

            // Create empty lists to store values that should be logged from the motor controller
            LoggedVelocities        = new List<Double>();
            LoggedTargets           = new List<Double>();
            LoggedPressures         = new List<Double>();
            LoggedLinearPositions   = new List<Double>();
            LoggedSensorValues      = new List<int>();
            // Create empty list to store the time vector corresponding to logged values
            LoggedTime              = new List<Double>();

            // Create event reading the maximum torque status register
            CreateEvent((ushort)0,
                                   (Int16)(0B000000000100000),              // Bitmask to get torque from status register
                                   (Int16)(MotorControl.Register.Status),
                                   (ushort)0XF007,                          // Logical 'and' between bitmask and status register
                                   (Int16)(MotorControl.Register.Mode),
                                   (ushort)0,
                                   (Int16)0);                               // No source register

            // Set a maximum allowed torque
            ModCom.RunModbus(MotorControl.Register.MotorTorqueMax, Hardware.MaxTorque);
        }

        // Function to run the motor based on positions
        public void RunWithPosition(List<Double> positions, List<Double> times)
        {
            List<Int32> ticks = PositionToTick(positions);
            RunTickSequence(ticks, times, Mode.PositionRamp);
        }

        // Function to run the motor based on velocities
        public void RunWithVelocity(List<Double> velocities, List<Double> times)
        {
            List<Int32> ticks = VelocityToTicksPerSecond(velocities);
            RunTickSequence(ticks, times, Mode.SpeedRamp);
        }

        // Function to actually run the motor based on a list of ticks, i.e a list with angular positions
        // for the motor, a list of times and a given mode.
        public void RunTickSequence(List<Int32> ticks, List<Double> times, Int16 mode)
        {
            // If the number of ticks and number of times are not equal, throw an error
            if (ticks.Count() != times.Count())
            {
                throw new Exception("Input lists not of equal length");
            }
            // Create event reading the maximum torque status register
            CreateEvent((ushort)0,
                                   (Int16)(0B000000000100000),              // Bitmask to get torque from status register
                                   (Int16)(MotorControl.Register.Status),
                                   (ushort)0XF007,                          // Logical 'and' between bitmask and status register
                                   (Int16)(MotorControl.Register.Mode),
                                   (ushort)0,
                                   (Int16)0);                               // No source register

            // Set a maximum allowed torque
            ModCom.RunModbus(MotorControl.Register.MotorTorqueMax, Hardware.MaxTorque);
            // Define the sequence length
            int sequenceLength = ticks.Count();
            // Create an instance of Stopwatch used to measure the time
            Stopwatch stopWatch = new Stopwatch();

            // Define lists to save logged motor values
            Int32[] LogRecordedVelocities = new Int32[500];
            Int32[] LogRecordedTargets = new Int32[500];
            Int32[] LogRecordedPressures = new Int32[500];
            Int32[] LogRecordedLinearPositions = new Int32[500];

            // Define list for the actual time
            Double[] StopwatchRecordedTimes = new Double[sequenceLength];

            // Add time values to LoggedTime and compute the LogFactor for logging
            int RegLogFactor = createTimeVector(times, sequenceLength);

            // Run the system to its home position
            goToHome(); 

            // SET the PID-parameters
            ModCom.RunModbus(300, (Int16)10000); //P
            ModCom.RunModbus(301, (Int16)1000);  //I
            ModCom.RunModbus(302, (Int16)0);     //D


            // Initializing the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff);     // Turn off the motor
            ModCom.RunModbus(Register.Speed, (Int32)0);         // Set the speed to 0
            ModCom.RunModbus(Register.Position, (Int32)0);      // Set the position to 0
            ModCom.RunModbus(Register.TargetInput, (Int32)0);   // Set the motor to be continously controlled via communication bus
            // Set the mode of the motor to the given mode
            ModCom.RunModbus(Register.Mode, mode);
            // Set time = 0 in the motor time register
            ModCom.RunModbus(Register.Time, (Int32)0);
            
            // Setup and start the logging
            setupLogging(RegLogFactor, 0);

            // Start to run the actual sequence and send regulator targets to the motor
            int i = 0;

            // Start to measure time at computer
            stopWatch.Start();
            while (i < sequenceLength)
            {
                // If more time have elapsed than times[i], send a new regulator target to the motor
                if (times[i] <= stopWatch.Elapsed.TotalSeconds)
                {
                    // Write a new target value to the motor
                    ModCom.RunModbus(Register.TargetInput, (Int32)ticks[i]);
                    // Read time
                    StopwatchRecordedTimes[i] = stopWatch.Elapsed.TotalSeconds;
                    i++;
                }

            }
            
            // Set target to zero
            ModCom.RunModbus(Register.TargetInput, (Int32)0);
            // Turn off the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff);
            // Stop counting the time
            stopWatch.Stop();

            // Read and save logged values from motor
            saveLoggedValues(LogRecordedVelocities, LogRecordedTargets,
                             LogRecordedPressures, LogRecordedLinearPositions, 1);

            // Convert Ticks to positions
            LoggedVelocities = MotorSpeedToVelocity(LogRecordedVelocities);
            // Convert Ticks to velocity
            LoggedTargets = TicksPerSecondToVelocity(LogRecordedTargets);
            // Convert sensorreading to linear position
            LoggedLinearPositions = sensorToLinPosList(LogRecordedLinearPositions);
            //LoggedPressures = uns16ToVDc(LogRecordedPressures);

        }


        // Function to convert from MotorSpeed to velocities
        public List<Double> MotorSpeedToVelocity(IList<int> motorSpeeds)
        {
            List<Double> velocities = new List<Double>();
            for (int i = 0; i < motorSpeeds.Count; i++)
            {
                velocities.Add(-(Double) motorSpeeds[i] * Hardware.Pitch /256.0 );
            }
            return velocities;
        }
        // Function to convert from positions to ticks
        public List<int> PositionToTick(List<Double> positions)
        {
            List<int> ticks = new List<int>();
            for (int i = 0; i < positions.Count; i++)
            {
                ticks.Add(-(int)Math.Round(positions[i] * Hardware.TicksPerRev / Hardware.Pitch));
            }
            return ticks;
        }

        // Function to convert from Velocities to ticks per second
        public List<Int32> VelocityToTicksPerSecond(IList<Double> velocities)
        {
            List<Int32> ticks = new List<Int32>();
            for (int i = 0; i < velocities.Count; i++)
            {
                ticks.Add(-(int)Math.Round(velocities[i] * Hardware.TicksPerRev / Hardware.Pitch / Hardware.VelocityResolution));
            }
            return ticks;
        }

        // Function to convert from ticks to positions
        public List<Double> TickToPosition(IList<int> ticks)
        {
            List<Double> position = new List<Double>();
            for (int i = 0; i < ticks.Count; i++)
            {
                position.Add(-(Double)ticks[i] * Hardware.Pitch / Hardware.TicksPerRev);
            }
            return position;
        }

        // Function to convert from ticks per second to velocity
        public List<Double> TicksPerSecondToVelocity(IList<int> ticksPerSecond)
        {
            List<Double> velocity = new List<Double>();
            for (int i = 0; i < ticksPerSecond.Count; i++)
            {
                velocity.Add(-(Double)ticksPerSecond[i] * Hardware.Pitch * Hardware.VelocityResolution / Hardware.TicksPerRev);
            }
            return velocity;
        }

        // Function to convert from time to seconds
        public List<Double> TimeToSeconds(IList<int> time)
        {
            List<Double> seconds = new List<Double>();
            for (int i = 0; i < time.Count; i++)
            {
                seconds.Add((Double)time[i] / Hardware.TimePerSecond);
            }
            return seconds;
        }

        // Function to convert from motortorques [nNm] to torques (Nm)
        public List<Double> MotorTorquesToTorques(IList<int> motorTorques)
        {
            List<Double> torques = new List<Double>();
            for (int i = 0; i < motorTorques.Count; i++)
            {
                torques.Add((Double)motorTorques[i] / Hardware.MotorTorquePerTorque);
            }
            return torques;
        }

        // Function to convert from Analogin 1 [VDc] to Pressure [?]
        public List<Double> MotorPressureToPressure(IList<int> motorPressure)
        {
            List<Double> pressure = new List<Double>();
            for (int i = 0; i < motorPressure.Count; i++)
            {
                pressure.Add((Double)motorPressure[i] * Hardware.PressureGain + Hardware.PressureBias);
            }
            return pressure;
        }

        // Function to convert¨List from linear sensorreading [uns16] to Linear position [mm]
        public List<Double> sensorToLinPosList(IList<int> sensorReading)
        {
            List<Double> linearPos = new List<Double>();
            for (int i = 0; i < sensorReading.Count; i++)
            {
                Double temp = (Double)((Double)sensorReading[i] - Hardware.Mu1) / Hardware.Mu2;
                Double linearPosition = Hardware.P1 * Math.Pow(temp,6) +
                                        Hardware.P2 * Math.Pow(temp, 5) +
                                        Hardware.P3 * Math.Pow(temp, 4) +
                                        Hardware.P4 * Math.Pow(temp, 3) +
                                        Hardware.P5 * Math.Pow(temp, 2) +
                                        Hardware.P6 * Math.Pow(temp, 1) +
                                        Hardware.P7;
                linearPos.Add(linearPosition);
            }
            return linearPos;
        }

        // Function to convert number from linear sensorreading [uns16] to Linear position [mm]
        public Double sensorToLinPos(int sensorReading)
        {
            Double temp = (Double)((Double)sensorReading - Hardware.Mu1) / Hardware.Mu2;
            Double linearPos = Hardware.P1 * Math.Pow(temp, 6) +
                               Hardware.P2 * Math.Pow(temp, 5) +
                               Hardware.P3 * Math.Pow(temp, 4) +
                               Hardware.P4 * Math.Pow(temp, 3) +
                               Hardware.P5 * Math.Pow(temp, 2) +
                               Hardware.P6 * Math.Pow(temp, 1) +
                               Hardware.P7;
            return linearPos;
        }

        // Function to convert from Analogin [uns16] to [VDc]
        public List<Double> uns16ToVDc(IList<int> analogIn)
        {
            List<Double> VDc = new List<Double>();
            for (int i = 0; i < analogIn.Count; i++)
            {
                VDc.Add((Double)analogIn[i] * (5 / 65535));
            }
            return VDc;
        }

        // Function to initialize the motor and set the system in its home position
        public void goToHome()
        {
            // Tune the regulator
            ModCom.RunModbus(300, (Int16)1000); //P
            ModCom.RunModbus(301, (Int16)1000); //I
            ModCom.RunModbus(302, (Int16)0); //D

            // Get the current linear position
            double currentPosition = sensorToLinPos(ModCom.ReadModbus(Register.LinearPosition, 1, false));

            // Chech if the current linear position is valid, if not throw an error
            if (currentPosition > Hardware.MaxPosition | currentPosition < Hardware.MinPosition)
            {
                throw new Exception("Measurement from linear sensor is out of physical range!");
            }

            // Boolean operator to keep track if home position found
            bool done = false;

            // Define value for the updated position for the motor
            int newPosition = 0;

            // Initialize the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff);     // Turn off the motor
            ModCom.RunModbus(Register.Speed, (Int32)0);         // Set the speed to 0
            ModCom.RunModbus(Register.Position, (Int32)0);      // Set the position to 0
            ModCom.RunModbus(Register.TargetInput, (Int32)0);   // Set the target to 0
            ModCom.RunModbus(Register.Mode, Mode.PositionRamp); // Set the mode to positonramp

            // Run the motor until the system is in its homeposition
            while (!done)
            {
                // Read the current position
                currentPosition = (Double) sensorToLinPos(ModCom.ReadModbus(Register.LinearPosition, 1, false));

                // Define variable for how much to inrease
                int increase = 0;
                // Decide how much to increase with depending on how far away from home
                if (Math.Abs(currentPosition - Hardware.HomePosition) > 0.5)
                {
                    increase = 20;
                }
                else
                {
                    increase = 1;
                }

                // If the piston is to far into the syringe, turn the motor counterclockwise (piston is moved 0.mm)
                if (currentPosition > Hardware.HomePosition + Hardware.HomePosTolerance)
                {
                    // Increase the position
                    newPosition = newPosition + increase;
                }
                // If the piston is to far out of the syringe, turn the motor clockwise
                else if (currentPosition < Hardware.HomePosition - Hardware.HomePosTolerance)
                {
                    // Decrease the position
                    newPosition = newPosition - increase;
                }

                // Check if we are done
                if (currentPosition < Hardware.HomePosition + Hardware.HomePosTolerance &
                    currentPosition > Hardware.HomePosition - Hardware.HomePosTolerance)
                {
                    done = true;
                }
                Console.Write("Current Position: ");
                Console.WriteLine(currentPosition);

                // Write the new position to the motor
                ModCom.RunModbus(Register.TargetInput, newPosition);
            }
            Console.Write("Current Position: ");
            Console.WriteLine(currentPosition);
            // Turn off the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff); // Set the mode to positonramp
            // Set the position to 0
            ModCom.RunModbus(Register.Position, (Int32)0);
            // Set the target to 0
            ModCom.RunModbus(Register.TargetInput, (Int32)0);
        }
        
        public void volumeTest()
        {

            // Define variable to save keyboard input from the user
            ConsoleKeyInfo input = new ConsoleKeyInfo();

            Console.WriteLine("Press Enter to start the volume test");

            // Start the volume test when enter is pressed
            while (Console.ReadKey().Key != ConsoleKey.Enter)
            {

            }



           // Initialize the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff);     // Turn off the motor
            ModCom.RunModbus(Register.Speed, (Int32)0);         // Set the speed to 0
            ModCom.RunModbus(Register.Position, (Int32)0);      // Set the position to 0
            ModCom.RunModbus(Register.TargetInput, (Int32)0);   // Set the target to 0
            ModCom.RunModbus(Register.Mode, Mode.PositionRamp); // Set the mode to positonramp

            // Get the current linear position
            double currentPosition = sensorToLinPos(ModCom.ReadModbus(Register.LinearPosition, 1, false));
            double homePos = currentPosition;
            double dist = 3.304 + homePos;
            int newPosition = 0;
            bool done = false;

            Console.Write("Start Position in MM: ");
            Console.WriteLine(currentPosition);
            Console.Write("Start Position: ");
            Console.WriteLine(ModCom.ReadModbus(Register.LinearPosition, 1, false));

            while (!done)
            {

                // Read the current position
                currentPosition = sensorToLinPos(ModCom.ReadModbus(Register.LinearPosition, 1, false));

                // Define variable for how much to inrease
                int increase = 0;
                // Decide how much to increase with depending on how far away from home
                if (Math.Abs(currentPosition - dist) > 0.5)
                {
                    increase = 20;
                }
                else
                {
                    increase = 1;
                }

                // If the piston is to far into the syringe, turn the motor counterclockwise (piston is moved 0.mm)
                if (currentPosition < dist  - Hardware.HomePosTolerance)
                {
                    // Increase the position
                    newPosition = newPosition - increase;
                }
                // Check if we are done
                else if (currentPosition > dist -  Hardware.HomePosTolerance)
                {
                    // Decrease the position
                    done = true;
                }
              
                // Write the new position to the motor
                ModCom.RunModbus(Register.TargetInput, newPosition);
            }


            // Turn off the motor
            ModCom.RunModbus(Register.Mode, Mode.MotorOff);
            // Set the position to 0
            ModCom.RunModbus(Register.Position, (Int32)0);
            // Set the target to 0
            ModCom.RunModbus(Register.TargetInput, (Int32)0);


            Console.Write("End Position in MM: ");
            Console.WriteLine(currentPosition);
            Console.Write("End Position: ");
            Console.WriteLine(ModCom.ReadModbus(Register.LinearPosition, 1, false));
        }

        // Function to create the time vector. Adds the times to LoggedTime and returns the RegLogFactor
        public int createTimeVector(List<Double> times, int sequenceLength)
        {
            // Get the maximum time from the input tick sequence 
            Double MaxTime = times[sequenceLength - 1];
            //Console.Write("Max Time: ");
            //Console.WriteLine(MaxTime);
            // Calculate the logfactor that will be used as input to the ReqPeriod register.
            int LogFactor = (int)Math.Ceiling(MaxTime * (Hardware.TimePerSecond / 500.0));
            // Subtract 1 to enable the factor to directly fed to the logging register.
            int RegLogFactor = LogFactor - 1;

            // Empty the time list
            LoggedTime.Clear();

            // Create Time vector used for plotting the logged values
            for (int t = 0; t < 500; t++)
            {
                // Compute the time value
                double time = t * LogFactor / Hardware.TimePerSecond;
                LoggedTime.Add(time);
                //Console.WriteLine(time);
            }
            return RegLogFactor;
        }

        // Function for setting upp the logging of values from motor
        // Position = 1     Logging for position control
        // Position = 0     Logging for velocity control
        public void setupLogging(int RegLogFactor, int Position)
        {
            // If position control
            if (Position == 1)
            {
                // Define registers to log 
                ModCom.RunModbus(Register.LogRegister1, (ushort)(Register.Position + 1));     // Position
                ModCom.RunModbus(Register.LogRegister2, (ushort)(Register.TargetInput + 1));  // Regulator target
                ModCom.RunModbus(Register.LogRegister3, Register.Pressure);                   // Pressure
                ModCom.RunModbus(Register.LogRegister4, Register.LinearPosition);             // Linear position
            }
            else
            {
                // Define registers to log 
                ModCom.RunModbus(Register.LogRegister1, Register.Speed);                      // Speed
                ModCom.RunModbus(Register.LogRegister2, (ushort)(Register.TargetInput + 1));  // Regulator target
                ModCom.RunModbus(Register.LogRegister3, Register.Pressure);                   // Pressure
                ModCom.RunModbus(Register.LogRegister4, Register.LinearPosition);             // Linear position
            }
            // Settings for the logging
            ModCom.RunModbus(Register.LogPeriod, (short)RegLogFactor);
            // Start logging
            ModCom.RunModbus(Register.LogState, (short)2);

        }

        // Function for reading and saving the logged values
        // Position = 1     Logging for position control
        // Position = 0     Logging for velocity control
        public void saveLoggedValues(int[] LogRecordedVelocities, int[] LogRecordedTargets,
                                     int[] LogRecordedPressures, int[] LogRecordedLinearPositions,
                                     int Position)
        {
            // Tmpty the lists for logged values, to avoid problem with lists growing
            LoggedPressures = new List<Double>();
            LoggedSensorValues = new List<int>();
            // Save values from the log registers
            for (ushort j = 0; j< 500; j++)
            {
                // Variables for increasing the register numbers
                ushort ch1 = (ushort)(Register.LogRegisterValue1 + j);
                ushort ch2 = (ushort)(Register.LogRegisterValue2 + j);
                ushort ch3 = (ushort)(Register.LogRegisterValue3 + j);
                ushort ch4 = (ushort)(Register.LogRegisterValue4 + j);
                // Read values
                LogRecordedVelocities[j]        = ModCom.ReadModbus(ch1, 1, false);
                LogRecordedTargets[j]           = ModCom.ReadModbus(ch2, 1, false);
                LogRecordedPressures[j]         = ModCom.ReadModbus(ch3, 1, false);
                LogRecordedLinearPositions[j]   = ModCom.ReadModbus(ch4, 1, false);
                LoggedSensorValues.Add(LogRecordedLinearPositions[j]);
                LoggedPressures.Add((Double)LogRecordedPressures[j] - LogRecordedPressures[0]);
            }
            //Loop for converting LSB part of int32 to int16
            for (int o = 0; o < 500; o++)
            {
                if (Position == 1)
                {
                    byte[] bytes = BitConverter.GetBytes((ushort)LogRecordedVelocities[o]);
                    short y = BitConverter.ToInt16(bytes, 0);
                    LogRecordedVelocities[o] = (int)y;
                }
                byte[] bytes2 = BitConverter.GetBytes((ushort)LogRecordedTargets[o]);
                short x = BitConverter.ToInt16(bytes2, 0);
                LogRecordedTargets[o] = (int)x;
                Console.Write("Target");
                Console.WriteLine(LogRecordedTargets[o]);
                Console.Write("Velocity");
                Console.WriteLine(LogRecordedVelocities[o]);
            }
        }

        // Function for Sequence Control
        public void sequenceControl(List<Int32> ticks, List<Double> times)
        {


            // Wrtie the first 16 targets
            // Save values from the log registers
            for (ushort i = 0; i < 16; i++)
            {
                // Variables for increasing the register numbers
                ushort ch1 = (ushort)(Register.SeqTarget + i);
                ushort ch2 = (ushort)(Register.SeqTime + i);

                // Write values
                ModCom.RunModbus(ch1, ticks[i]);
                ModCom.RunModbus(ch2, (ushort)(times[i + 1] - times[i] * 1000));
            }

            // Set mode to sequence control
            ModCom.RunModbus(Register.Mode, 51); // Set the mode to positonramp

            bool done = false;
            // Start to continously update the sequence registers
            while(!done)
            {

            }
        }

        // Function for initial test of linear sensor.
        public void testLinearSensor()
        {
            // Write to the console
            Console.WriteLine("Test of Linear sensor active!");
            Console.WriteLine("Press enter to exit");
            // Read current position from sensor
            //int Position = ModCom.ReadModbus(Register.LinearPosition, 1, false);
            //Console.WriteLine(Position);
            // As long as "Enter" is not pressed, continue 

            //while (Console.ReadKey().Key != ConsoleKey.Enter)
            while (true)
            {
                int Position = ModCom.ReadModbus(Register.LinearPosition, 1, false);
                double PositionMM = sensorToLinPos(Position);
                Console.WriteLine("PositionMM: ");
                Console.WriteLine(PositionMM.ToString());
                Console.WriteLine("Position: ");
                Console.WriteLine(Position.ToString());
            }
            Console.WriteLine("Exiting manual control");
        }


        // Function to allow the user to manually control the motor, i.e the syring, from the console.
        public void ManualControl()
        {
            // Write to the console
            Console.WriteLine("Manual control active");
			Console.WriteLine("Press enter to exit, + to increase and - to decrease position");
            // Read current position from motor
            int CurrentPosition = ModCom.ReadModbus(Register.Position, 2, true); 
            // Update last position to current position
			int LastPosition = CurrentPosition;
            // Define variable to save keyboard input from the user
			ConsoleKeyInfo input = new ConsoleKeyInfo();

            // As long as "Enter" is not pressed, continue 
			while (Console.ReadKey().Key != ConsoleKey.Enter)
			{
                // Read the keyboard input
				input = Console.ReadKey(true);

                // In input is "+", increase currenposition with 50
				if (input.KeyChar == '+')
				{
					CurrentPosition = CurrentPosition + 50;
					Console.WriteLine("position increased");
				}

                // In input is "-", decrease currenposition with 50
                if (input.KeyChar == '-')
				{
					CurrentPosition = CurrentPosition - 50;
					Console.WriteLine("position decreased");
				}

                // If currenposition was updated, update position of motor
				if (CurrentPosition != LastPosition)
				{
					ModCom.RunModbus(Register.TargetInput, CurrentPosition);
					LastPosition = CurrentPosition;
				}

			}
			Console.WriteLine("Exiting manual control");
        }
    }
}
