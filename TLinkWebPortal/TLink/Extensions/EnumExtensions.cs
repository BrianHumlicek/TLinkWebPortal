// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.ComponentModel;

namespace DSC.TLink.Extensions
{
	internal static class EnumExtensions
	{
		public static string GetTypeDescription(Enum enumeration)
		{
			var enumerationtype = enumeration.GetType();
			var attribute = enumerationtype.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
			return (attribute as DescriptionAttribute)?.Description ?? enumerationtype.ToString();
		}
		public static string GetMemberDescription(Enum enumeration)
		{
			string enumerationString = enumeration.ToString();
			var member = enumeration.GetType().GetMember(enumerationString).First();
			var attribute = member.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
			return (attribute as DescriptionAttribute)?.Description ?? enumerationString;
		}
	}
}
