using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;
using System;
using System.Linq;

namespace I2CTinyUSB {
    public class I2CTiny : IDisposable {
        private readonly int[] VID = { 0x0403, 0x1c40 };
        private readonly int[] PID = { 0xc631, 0x0534 };

        private readonly byte USB_CTRL_IN = (byte)UsbRequestType.TypeClass | (byte)EndpointDirection.In;
        private readonly byte USB_CTRL_OUT = (byte)UsbRequestType.TypeClass | (byte)EndpointDirection.Out;
        private readonly byte I2C_M_RD = 0x01;

        private IUsbDevice Device;
		private IUsbContext Context;

		[Flags]
		enum I2CCommand : byte {
			Begin = 1,
			End = 2,
			IO = 4
		}

		public enum UsbCommand : byte {
			Echo = 0,
			GetFunc = 1,
			SetDelay = 2,
			GetStatus = 3,
			Reset = 0xFE,
			Debug = 0xFF
		}

		public enum Status : byte {
			Idle = 0,
			ACK = 1,
			NACK = 2
		}

		public void Connect() {
			Context = new UsbContext();
            Device = Context.Find(d => VID.Any(vid => d.VendorId == vid)
                && PID.Any(pid => d.ProductId == pid)).Clone();

            if (Device is null)
                throw new Exception("Device isn't connected!");

            Device.Open();
        }

		public int ReadCommand(UsbCommand cmd, byte[] data, short value = 0, short index = 0) {
			return Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_IN,
				Request = (byte)cmd,
				Value = value,
				Index = index
			}, data, 0, data.Length);
		}

		public int WriteCommand(UsbCommand cmd, short value, short index = 0) {
			int ret = Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_OUT,
				Request = (byte)cmd,
				Value = value,
				Index = index
			});

			if (GetStatus() != Status.ACK)
				throw new Exception("Writing to the device failed");

			return ret;
		}

		public uint GetFunctions() {
			byte[] buffer = new byte[4];
			ReadCommand(UsbCommand.GetFunc, buffer);
			return BitConverter.ToUInt32(buffer, 0);
		}

		public Status GetStatus() {
			byte[] buffer = new byte[1];
			ReadCommand(UsbCommand.GetStatus, buffer);
			return (Status)buffer[0];
		}

		public short Echo(short value) {
			byte[] buffer = new byte[2];
			ReadCommand(UsbCommand.Echo, buffer, value);
			return BitConverter.ToInt16(buffer, 0);
		}

		public void Reset() {
			Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_OUT,
				Request = (byte)UsbCommand.Reset,
				Value = 0,
				Index = 0
			});
		}

		public int Read(byte address, byte cmd, int length) {
			byte[] buffer = new byte[2];

			if (length < 0 || length > buffer.Length)
				throw new ArgumentException("Request exceeds 2 bytes");

			buffer[0] = cmd;
			Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_OUT,
				Request = (byte)I2CCommand.IO | (byte)I2CCommand.Begin,
				Value = 0,
				Index = address
			}, buffer, 0, 1);

			if (GetStatus() != Status.ACK)
				throw new Exception("Writing to the device failed");

			if (length == 0)
				return 0;

			Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_IN,
				Request = (byte)I2CCommand.IO | (byte)I2CCommand.End,
				Value = I2C_M_RD,
				Index = address
			}, buffer, 0, length);

			if (GetStatus() != Status.ACK)
				throw new Exception("Writing to the device failed");

			return length == 2 ? BitConverter.ToInt16(buffer, 0) : buffer[0];
		}

		public void Write(byte address, byte[] data) {
			Device.ControlTransfer(new UsbSetupPacket {
				RequestType = USB_CTRL_OUT,
				Request = (byte)I2CCommand.IO | (byte)I2CCommand.Begin | (byte)I2CCommand.End,
				Value = 0,
				Index = address
			}, data, 0, data.Length);
		}

		~I2CTiny() {
            Dispose();
        }

        public void Dispose() {
			Context.Dispose();
			Device.Dispose();
        }
    }
}
