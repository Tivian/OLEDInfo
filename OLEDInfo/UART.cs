using System.IO;
using System.IO.Ports;
using System.Linq;

namespace Tivian.Display {
    class UART : ISerialInterface {
        private readonly SerialPort Port;
        public readonly byte Address;

        public static UART Connect(string device, int baudRate, byte address) {
            for (int i = 0; i < 5; i++) { // dirty fix
                using (var uart = new UART(device, baudRate, address))
                    uart.Command(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
            }

            return new UART(device, baudRate, address);
        }

        private UART(string device, int baudRate, byte address) {
            Address = address;

            if (!SerialPort.GetPortNames().Contains(device)) {
                if (SerialPort.GetPortNames().Length > 0)
                    device = SerialPort.GetPortNames()[0];
                else
                    throw new IOException("There's no such serial port!");
            }

            Port = new SerialPort(device, baudRate) {
                WriteTimeout = -1,
                ReadTimeout = -1
            };

            if (!Port.IsOpen)
                Port.Open();
        }

        ~UART() {
            Dispose();
        }

        public void Dispose() {
            Port.Close();
        }

        public void Command(params byte[] data) {
            Send(data, 0x00);
        }

        public void Data(params byte[] data) {
            Send(data, 0x40);
        }

        private void Send(byte[] data, byte ctrl) {
            var size = data.Length + 1;

            Port.Write(new byte[] { Address, (byte) (size >> 8), (byte) (size & 0xff), ctrl }, 0, 4);
            Port.Write(data, 0, data.Length);
        }
    }
}
