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

using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace DSC.TLink.ITv2.Encryption
{
	internal class Type2EncryptionHandler : EncryptionHandler
	{
		readonly byte[] integrationAccessCode;
		public Type2EncryptionHandler(IConfiguration configuration) : this(configuration[ConfigurationSettings.IntegrationAccessCodeType2])
		{

		}
		/// <summary>
		/// Configure type 2 encryption with parameter 'Integration Access Code'
		/// ID's [851][700,701,702,703] for panel integration session 1-4
		/// </summary>
		/// <param name="integrationAccessCode">Type 2 Integration Access Code [851][700,701,702,703]</param>
		public Type2EncryptionHandler(string integrationAccessCode)
		{
			if (integrationAccessCode == null) throw new ArgumentNullException(nameof(integrationAccessCode));
			if (integrationAccessCode.Length != 32) throw new ArgumentException(nameof(integrationAccessCode));
			this.integrationAccessCode = Convert.FromHexString(integrationAccessCode);
		}
		public override void ConfigureOutboundEncryption(byte[] remoteInitializer)  //Notes in the code indicate this might be the MAC address of the remote device.  If so, wow...
		{
			if (remoteInitializer.Length != 16) throw new ArgumentException(nameof(remoteInitializer));
			byte[] outboundKey = encryptKeyData(integrationAccessCode, remoteInitializer);
			activateOutbound(outboundKey);
		}
		public override byte[] ConfigureInboundEncryption()
		{
			byte[] localInitializer = RandomNumberGenerator.GetBytes(16);
			byte[] outboundKey = encryptKeyData(integrationAccessCode, localInitializer);
			activateOutbound(outboundKey);
			return localInitializer;
		}
	}
}
