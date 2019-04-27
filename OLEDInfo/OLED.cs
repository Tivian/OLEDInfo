using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Tivian.Display {
    public class OLED : IDisposable {
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
        }

        public enum Orientation {
            DEG_0   = 0,
            DEG_90  = 1,
            DEG_180 = 2,
            DEG_270 = 3
        }

        private struct Settings {
            public byte Multiplex;
            public byte DisplayClockDiv;
            public byte ComPins;
        }

        private readonly ISerialInterface serialInterface;
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

        public OLED(ISerialInterface serialInterface, byte width = 128, byte height = 64, Orientation rotate = Orientation.DEG_0) {
            this.serialInterface = serialInterface;
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
                File.AppendAllText("error.log", $"[{DateTime.Now}] IOException: Can't initialize OLED screen\n");
                System.Windows.Forms.MessageBox.Show("Can't initialize OLED screen!");
                Environment.Exit(0);
            }
        }

        ~OLED() {
            Dispose();
        }

        private bool disposed = false;

        public void Dispose() {
            if (!disposed) {
                Hide();
                Clear();
                disposed = true;
            }

            serialInterface.Dispose();
        }

        public void Init() {
            serialInterface.Command(
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
                    pixels = stream.ToArray().Skip((int) stream.Length - (img.Width * img.Height / 8)).ToArray();
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
            serialInterface.Command(
                Const.COLUMNADDR, colStart, (byte)(colEnd - 1),
                Const.PAGEADDR, 0x00, (byte)(pages - 1)
            );
            serialInterface.Data(pixels);
        }

        public void Fill(byte val) {
            Display(Enumerable.Repeat(val, Width * Height / 8).ToArray());
        }

        public void Clear() {
            Fill(0x00);
        }
    
        public void Show() {
            serialInterface.Command(Const.DISPLAYON);
        }

        public void Hide() {
            serialInterface.Command(Const.DISPLAYOFF);
        }

        private byte contrast = 0x00;
        public byte Contrast {
            get => contrast;
            set {
                contrast = value;
                serialInterface.Command(Const.SETCONTRAST, value);
            }
        }
    }
}
/*
   0  1  2  3  4  5  6  7
  63 55 47 39 31 23 15  7 (H - 1) - 8p
  56 48 40 32 24 16  8  0 H - 8(p + 1)


  (63, 0, 0, 1) (62, 0, 0, 2) (61, 0, 0, 4) (60, 0, 0, 8) (59, 0, 0, 16) (58, 0, 0, 32) (57, 0, 0, 64) (56, 0, 0, 128)
   ...
  (63, 0, 7) (62, 0, 7) (61, 0, 7) (60, 0, 7) (59, 0, 7) (58, 0, 7) (57, 0, 7) (56, 0, 7)
  (63, 1, 0) (62, 1, 0) (61, 1, 0) (60, 1, 0) (59, 1, 0) (58, 1, 0) (57, 1, 0) (56, 1, 0)
   ...
  (63, 15, 7) (62, 15, 7) (61, 15, 7) (60, 15, 7) (59, 15, 7) (58, 15, 7) (57, 15, 7) (56, 15, 7)
  (55, 0, 0) (54, 0, 0) (53, 0, 0) (52, 0, 0) (51, 0, 0) (50, 0, 0) (49, 0, 0) (48, 0, 0)
   ...
  (55, 15, 7) (54, 15, 7) (53, 15, 7) (52, 15, 7) (51, 15, 7) (50, 15, 7) (49, 15, 7) (48, 15, 7)
   47 - 40
   39 - 32
   31 - 24
   23 - 16
   15 -  8
    7 -  0
  (7, 15, 7) (6, 15, 7) (5, 15, 7) (4, 15, 7) (3, 15, 7) (2, 15, 7) (1, 15, 7) (0, 15, 7)
 

     0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15

 0   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0   3
 1   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0  12
 2   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0  48
 3   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0 192
 4   0   0   0   0   0   0   0   0   0   0   0   0   0   0   3   0
 5   0   0   0   0   0   0   0   0   0   0   0   0   0   0  12   0
 6   0   0   0   0   0   0   0   0   0   0   0   0   0   0  48   0
 7   0   0   0   0   0   0   0   0   0   0   0   0   0   0 192   0
 8   0   0   0   0   0   0   0   0   0   0   0   0   0   3   0   0 
 9   0   0   0   0   0   0   0   0   0   0   0   0   0  12   0   0
10   0   0   0   0   0   0   0   0   0   0   0   0   0  48   0   0
11   0   0   0   0   0   0   0   0   0   0   0   0   0 192   0   0
12   0   0   0   0   0   0   0   0   0   0   0   0   3   0   0   0
13   0   0   0   0   0   0   0   0   0   0   0   0  12   0   0   0
14   0   0   0   0   0   0   0   0   0   0   0   0  48   0   0   0
15   0   0   0   0   0   0   0   0   0   0   0   0 192   0   0   0
16   0   0   0   0   0   0   0   0   0   0   0   3   0   0   0   0 
17   0   0   0   0   0   0   0   0   0   0   0  12   0   0   0   0
18   0   0   0   0   0   0   0   0   0   0   0  48   0   0   0   0 
19   0   0   0   0   0   0   0   0   0   0   0 192   0   0   0   0
20   0   0   0   0   0   0   0   0   0   0   3   0   0   0   0   0 
21   0   0   0   0   0   0   0   0   0   0  12   0   0   0   0   0  
22   0   0   0   0   0   0   0   0   0   0  48   0   0   0   0   0 
23   0   0   0   0   0   0   0   0   0   0 192   0   0   0   0   0
24   0   0   0   0   0   0   0   0   0   3   0   0   0   0   0   0 
25   0   0   0   0   0   0   0   0   0  12   0   0   0   0   0   0 
26   0   0   0   0   0   0   0   0   0  48   0   0   0   0   0   0 
27   0   0   0   0   0   0   0   0   0 192   0   0   0   0   0   0 
28   0   0   0   0   0   0   0   0   3   0   0   0   0   0   0   0  
29   0   0   0   0   0   0   0   0  12   0   0   0   0   0   0   0  
30   0   0   0   0   0   0   0   0  48   0   0   0   0   0   0   0 
31   0   0   0   0   0   0   0   0 192   0   0   0   0   0   0   0
32   0   0   0   0   0   0   0   3   0   0   0   0   0   0   0   0  
33   0   0   0   0   0   0   0  12   0   0   0   0   0   0   0   0 
34   0   0   0   0   0   0   0  48   0   0   0   0   0   0   0   0  
35   0   0   0   0   0   0   0 192   0   0   0   0   0   0   0   0 
36   0   0   0   0   0   0   3   0   0   0   0   0   0   0   0   0 
37   0   0   0   0   0   0  12   0   0   0   0   0   0   0   0   0 
38   0   0   0   0   0   0  48   0   0   0   0   0   0   0   0   0 
39   0   0   0   0   0   0 192   0   0   0   0   0   0   0   0   0
40   0   0   0   0   0   3   0   0   0   0   0   0   0   0   0   0 
41   0   0   0   0   0  12   0   0   0   0   0   0   0   0   0   0  
42   0   0   0   0   0  48   0   0   0   0   0   0   0   0   0   0 
43   0   0   0   0   0 192   0   0   0   0   0   0   0   0   0   0 
44   0   0   0   0   3   0   0   0   0   0   0   0   0   0   0   0
45   0   0   0   0  12   0   0   0   0   0   0   0   0   0   0   0 
46   0   0   0   0  48   0   0   0   0   0   0   0   0   0   0   0 
47   0   0   0   0 192   0   0   0   0   0   0   0   0   0   0   0
48   0   0   0   3   0   0   0   0   0   0   0   0   0   0   0   0  
49   0   0   0  12   0   0   0   0   0   0   0   0   0   0   0   0 
50   0   0   0  48   0   0   0   0   0   0   0   0   0   0   0   0 
51   0   0   0 192   0   0   0   0   0   0   0   0   0   0   0   0 
52   0   0   3   0   0   0   0   0   0   0   0   0   0   0   0   0 
53   0   0  12   0   0   0   0   0   0   0   0   0   0   0   0   0 
54   0   0  48   0   0   0   0   0   0   0   0   0   0   0   0   0 
55   0   0 192   0   0   0   0   0   0   0   0   0   0   0   0   0
56   0   3   0   0   0   0   0   0   0   0   0   0   0   0   0   0 
57   0  12   0   0   0   0   0   0   0   0   0   0   0   0   0   0 
58   0  48   0   0   0   0   0   0   0   0   0   0   0   0   0   0 
59   0 192   0   0   0   0   0   0   0   0   0   0   0   0   0   0  
60   3   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0 
61  12   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0 
62  48   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0
63 192   0   0   0   0   0   0   0   0   0   0   0   0   0   0   0
*/
