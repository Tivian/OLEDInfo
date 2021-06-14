using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace OLEDInfo {
    class OLED : IDisposable {
        public static class Const {
            public static byte DISPLAYOFF => 0xAE;
            public static byte DISPLAYON => 0xAF;
            public static byte DISPLAYALLON => 0xA5;
            public static byte DISPLAYALLON_RESUME => 0xA4;
            public static byte NORMALDISPLAY => 0xA6;
            public static byte INVERTDISPLAY => 0xA7;
            public static byte SETREMAP => 0xA0;
            public static byte SETMULTIPLEX => 0xA8;
            public static byte SETCONTRAST => 0x81;
            public static byte CHARGEPUMP => 0x8D;
            public static byte COLUMNADDR => 0x21;
            public static byte COMSCANDEC => 0xC8;
            public static byte COMSCANINC => 0xC0;
            public static byte EXTERNALVCC => 0x01;
            public static byte MEMORYMODE => 0x20;
            public static byte PAGEADDR => 0x22;
            public static byte SETCOMPINS => 0xDA;
            public static byte SETDISPLAYCLOCKDIV => 0xD5;
            public static byte SETDISPLAYOFFSET => 0xD3;
            public static byte SETHIGHCOLUMN => 0x10;
            public static byte SETLOWCOLUMN => 0x00;
            public static byte SETPRECHARGE => 0xD9;
            public static byte SETSEGMENTREMAP => 0xA1;
            public static byte SETSTARTLINE => 0x40;
            public static byte SETVCOMDETECT => 0xDB;
            public static byte SWITCHCAPVCC => 0x02;

            public static byte COMMAND => 0x00;
            public static byte DATA => 0x40;
        }

        public enum Orientation {
            DEG_0 = 0,
            DEG_90 = 1,
            DEG_180 = 2,
            DEG_270 = 3
        }

        private struct Settings {
            public byte Multiplex;
            public byte DisplayClockDiv;
            public byte ComPins;
        }

        private readonly IDataInterface dataInterface;
        private readonly Settings settings;
        private readonly byte pages;
        private readonly byte colStart;
        private readonly byte colEnd;

        private static readonly Dictionary<Size, Settings> possibleSettings = new Dictionary<Size, Settings>() {
            { new Size(128, 64), new Settings() { Multiplex = 0x3F, DisplayClockDiv = 0x80, ComPins = 0x12 } },
            { new Size(128, 32), new Settings() { Multiplex = 0x1F, DisplayClockDiv = 0x80, ComPins = 0x02 } },
            { new Size(96,  16), new Settings() { Multiplex = 0x0F, DisplayClockDiv = 0x60, ComPins = 0x02 } },
            { new Size(64,  48), new Settings() { Multiplex = 0x2F, DisplayClockDiv = 0x80, ComPins = 0x12 } },
            { new Size(64,  32), new Settings() { Multiplex = 0x1F, DisplayClockDiv = 0x80, ComPins = 0x12 } }
        };

        public byte Width { get; }
        public byte Height { get; }
        public Orientation Rotate { get; }

        private byte contrast = 0x00;
        public byte Contrast {
            get => contrast;
            set {
                contrast = value;
                Command(Const.SETCONTRAST, value);
            }
        }

        public OLED(IDataInterface dataInterface, byte width = 128, byte height = 64, Orientation rotate = Orientation.DEG_0) {
            this.dataInterface = dataInterface;
            Width = width;
            Height = height;
            Rotate = rotate;

            bool success = possibleSettings.TryGetValue(new Size(width, height), out settings);
            if (!success)
                throw new ArgumentException($"Unsupported display mode: {width} x {height}");

            pages = (byte)(height / 8);
            colStart = (byte)((0x80 - width) / 2);
            colEnd = (byte)(colStart + width);

            try {
                Init();
            } catch (IOException) {
                throw new IOException("Can't initialize OLED screen");
            }
        }

        public void Close() => Dispose();

        private bool disposed = false;

        public void Dispose() {
            if (!disposed) {
                Hide();
                Clear();
                disposed = true;
            }

            dataInterface.Dispose();
        }

        public void Init() {
            Command(
                Const.DISPLAYOFF,
                Const.SETDISPLAYCLOCKDIV, settings.DisplayClockDiv,
                Const.SETMULTIPLEX, settings.Multiplex,
                Const.SETDISPLAYOFFSET, 0x00,
                Const.SETSTARTLINE,
                Const.CHARGEPUMP, 0x14,
                Const.MEMORYMODE, 0x00,
                Const.SETSEGMENTREMAP,
                Const.COMSCANDEC,
                Const.SETCOMPINS, settings.ComPins,
                Const.SETPRECHARGE, 0xF1,
                Const.SETVCOMDETECT, 0x40,
                Const.DISPLAYALLON_RESUME,
                Const.NORMALDISPLAY
            );

            Contrast = 0xCF;
            Clear();
            Show();
        }

        public void Restart() {
            Clear();
            Init();
        }

        public void Display(Image img, bool invert = false) {
            if (img.Width > Width || img.Height > Height)
                img = new Bitmap(img.Scale(Width, Height)).Dither();

            byte[] pixels = new byte[Width / 8 * Height];
            byte[] rawData = new byte[Width / 8 * Height];

            using (var stream = new MemoryStream()) {
                using (var bmp = new Bitmap(img)) {
                    bmp.Clone(new Rectangle(0, 0, img.Width, img.Height), PixelFormat.Format1bppIndexed)
                       .Save(stream, ImageFormat.Bmp);
                    pixels = stream.ToArray().Skip((int)stream.Length - (img.Width * img.Height / 8)).ToArray();
                }
            }

            for (int page = 0, n = 0; page < pages; page++) {
                for (int x = 0; x < Width / 8; x++) {
                    for (int k = 7; k >= 0; k--, n++) {
                        for (int y = (Height - 1) - 8 * page, j = 0; y >= Height - 8 * (page + 1); y--, j++) {
                            if ((pixels[(y * (Width / 8)) + x] & (1 << k)) != 0)
                                rawData[n] |= (byte)(1 << j);
                        }
                    }
                }
            }

            img = null;
            pixels = null;
            Display(!invert ? rawData : rawData.Select(x => (byte)(255 - x)).ToArray());
        }

        public void Display(byte[] pixels) {
            Command(
                Const.COLUMNADDR, colStart, (byte)(colEnd - 1),
                Const.PAGEADDR, 0x00, (byte)(pages - 1)
            );
            Data(pixels);
        }

        public void Fill(byte val) {
            Display(Enumerable.Repeat(val, Width * Height / 8).ToArray());
        }

        public void Clear() {
            Fill(0x00);
        }

        public void Show() {
            Command(Const.DISPLAYON);
        }

        public void Hide() {
            Command(Const.DISPLAYOFF);
        }

        private void Command(params byte[] data) {
            Send(Const.COMMAND, data);
        }

        private void Data(params byte[] data) {
            Send(Const.DATA, data);
        }

        private void Send(byte mode, params byte[] data) {
            byte[] buffer = new byte[data.Length + 1];
            buffer[0] = mode;
            Array.Copy(data, 0, buffer, 1, data.Length);
            dataInterface.Write(buffer);
        }
    }
}
