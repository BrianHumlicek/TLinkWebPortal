// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.ComponentModel;

namespace DSC.TLink.ITv2.Enumerations
{
	internal enum CommandResponseCode : byte
	{
		[Description("Success")]
		Success = 0,

		[Description("Cannot Exit Configuration/Ignore Firmware (No Firmware Available)/ Block out of range/ Command Output is not defined/System cannot be put in the specified test mode/ Unsupported Buffer ID/ Enabled Late To Open/ Invalid Type")]
		CannotExitConfiguration = 1,

		[Description("Invalid programming Type/ Packet Length cannot be supported/ Ignore Firmware (No Firmware Available)/ Invalid Output Number/ Invalid Unsupported Test Type")]
		InvalidProgrammingType = 2,

		[Description("Unsupported module/No new information in the specified buffer")]
		UnsupportedModule = 3,

		[Description("Invalid Signal Type/ Some Or All Partition Failed To Arm")]
		InvalidSignalType = 4,

		[Description("Not in correct Programming Mode/ WalkTestActive")]
		NotInCorrectProgrammingMode = 16,

		[Description("Invalid Access Code")]
		InvalidAccessCode = 17,

		[Description("Access Code is Required")]
		AccessCodeRequired = 18,

		[Description("System/Partition is Busy")]
		SystemPartitionBusy = 19,

		[Description("Invalid Partition")]
		InvalidPartition = 20,

		[Description("Function Not Available")]
		FunctionNotAvailable = 23,

		[Description("Internal Error (For example, memory access failure)")]
		InternalError = 24,

		[Description("Command TimeOut")]
		CommandTimeOut = 25,

		[Description("No Troubles Present For Requested Type")]
		NoTroublesPresentForRequestedType = 26,

		[Description("No Requested Alarms Found")]
		NoRequestedAlarmsFound = 27,

		[Description("Invalid Device Module")]
		InvalidDeviceModule = 28,

		[Description("Invalid Trouble Type")]
		InvalidTroubleType = 29
	}
}
