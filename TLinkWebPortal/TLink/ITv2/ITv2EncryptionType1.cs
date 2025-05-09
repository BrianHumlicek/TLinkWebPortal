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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2
{
	internal class ITv2EncryptionType1 : ITv2Encryption
	{
		readonly byte[] integrationAccessCode;
		readonly byte[] integrationIdentificationNumber;

		public ITv2EncryptionType1(IConfiguration configuration) : this(configuration[ConfigurationSettings.IntegrationAccessCodeType1], configuration[ConfigurationSettings.IntegrationIdentificationNumber])
		{

		}
		/// <summary>
		/// This constructor sets up Type 1 encryption
		/// </summary>
		/// <param name="integrationAccessCode">Type 1 Integration Access Code [851][423,450,477,504]</param>
		/// <param name="integrationIdentificationNumber">Integration Identification Number [851][422]</param>
		public ITv2EncryptionType1(string integrationAccessCode, string integrationIdentificationNumber)
		{
			if (integrationAccessCode == null) throw new ArgumentNullException(nameof(integrationAccessCode));
			if (integrationIdentificationNumber == null) throw new ArgumentNullException(nameof(integrationIdentificationNumber));
			if (integrationAccessCode.Length < 8) throw new ArgumentException(nameof(integrationAccessCode));
			//This parameter is 12 digits long, but we only need the first 8 digits so that is all that is being enforced here.
			if (integrationIdentificationNumber.Length < 8) throw new ArgumentException(nameof(integrationIdentificationNumber));

			this.integrationAccessCode = transformKeyString(integrationAccessCode);
			this.integrationIdentificationNumber = transformKeyString(integrationIdentificationNumber);
		}

		byte[] transformKeyString(string keyString)
		{
			string first8 = keyString.Substring(0, 8);
			//This makes a 32 digit base 10 string, and the reads it as base 16 string which it makes a 16 byte array.
			return Convert.FromHexString($"{first8}{first8}{first8}{first8}");
		}

		IEnumerable<byte> evenIndexes(IEnumerable<byte> bytes) => bytes.Where((element, index) => index % 2 == 0);
		IEnumerable<byte> oddIndexes(IEnumerable<byte> bytes) => bytes.Where((element, index) => index % 2 == 1);
		public override void ConfigureOutboundEncryption(byte[] remoteInitializer)
		{
			if (remoteInitializer.Length != 48) throw new ArgumentException(nameof(remoteInitializer));

			var checkBytes = remoteInitializer.Take(16);
			var cipherText = remoteInitializer.Skip(16).Take(32).ToArray();

			byte[] plainText = decryptKeyData(integrationIdentificationNumber, cipherText);

			if (!checkBytes.SequenceEqual(evenIndexes(plainText))) throw new TLinkPacketException(TLinkPacketException.Code.Unknown, "Encryption initializer check byte failure.");

			byte[] outboundKey = oddIndexes(plainText).ToArray();

			activateOutbound(outboundKey);
		}
		public override byte[] ConfigureInboundEncryption()
		{
			byte[] randomBytes = RandomNumberGenerator.GetBytes(32);

			var checkBytes = evenIndexes(randomBytes);

			byte[] inboundKey = oddIndexes(randomBytes).ToArray();

			activateInbound(inboundKey);

			byte[] cipherText = encryptKeyData(integrationAccessCode, randomBytes);

			return checkBytes.Concat(cipherText).ToArray();
		}
	}
}
