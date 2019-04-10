using System;

namespace Tivian.Display {
    public interface ISerialInterface : IDisposable {
        void Command(params byte[] data);
        void Data(params byte[] data);
    }
}
