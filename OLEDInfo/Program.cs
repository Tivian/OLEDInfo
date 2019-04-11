using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenHardwareMonitor.Hardware;
using Tivian.Utilities;

namespace Tivian.Display {
    class OLEDInfo {
        private static bool isRunning = true;
        private static readonly string diplayPort = "COM1";
        private static readonly int baudrate = 170000;
        private static readonly byte displayAddr = 0x3c;
        private static readonly PrivateFontCollection fontCollection = new PrivateFontCollection();

        private static OLED disp;
        private static Computer computer;
        private static CancellationTokenSource tokenSrc;

        private struct Screen {
            public int Delay;
            public Action<Graphics> Draw;
        }

        static async Task Main() {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            fontCollection.AddResourceFont(Properties.Resources._7_segment_mono);

            computer = new Computer() {
                GPUEnabled = true,
                CPUEnabled = true
            };
            computer.Open();

            float fontSize = 20.0f;
            var font = new Font("Consolas", fontSize);
            var pen = new Pen(Color.White);

            var families = FontFamily.Families.Where(family => {
                var g = Graphics.FromImage(new Bitmap(100, 100));
                return g.MeasureString("000.00", new Font(family, 100)).Width - g.MeasureString("  1.11", new Font(family, 100)).Width == 0;
            }).ToArray();

            int current = 0;
            var screens = new List<Screen>() {
                new Screen() {
                    Delay = 30000,
                    Draw = (g) => {
                        var timeFont = new Font(fontCollection.Families[0], font.Size * 1.5f);
                        string time = DateTime.Now.ToString("HH:mm");
                        var measure = g.MeasureString(time, timeFont);
                        g.DrawString(time, timeFont, pen.Brush, (disp.Width - measure.Width) / 2.0f, (disp.Height - measure.Height) / 2.0f);
                    }
                },

                new Screen() {
                    Delay = 1000,
                    Draw = (g) => {
                        var baseLine = 0.0f;
                        var smallFont = new Font(font.FontFamily, font.Size / 2.5f);
                        var cpu = computer.Hardware.Where(h => h.HardwareType == HardwareType.CPU).FirstOrDefault();
                        cpu.Update();
                        g.DrawString("CPU", smallFont, pen.Brush, 0, baseLine);
                        g.DrawString($"{cpu.Sensors.Where(s => s.SensorType == SensorType.Power).FirstOrDefault().Value,6:f2}W", font, pen.Brush, 10, baseLine + smallFont.Size * 0.5f);
                        baseLine += font.Size * 1.6f;

                        var gpu = computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia).FirstOrDefault();
                        gpu.Update();
                        g.DrawString("GPU", smallFont, pen.Brush, 0, baseLine);
                        g.DrawString($"{gpu.Sensors.Where(s => s.SensorType == SensorType.Power).FirstOrDefault().Value,6:f2}W", font, pen.Brush, 10, baseLine + smallFont.Size * 0.5f);
                    }
                },

                new Screen() {
                    Delay = 1000,
                    Draw = (g) => {
                        var baseLine = 0.0f;
                        var smallFont = new Font(font.FontFamily, font.Size / 2.5f);
                        var cpu = computer.Hardware.Where(h => h.HardwareType == HardwareType.CPU).FirstOrDefault();
                        cpu.Update();
                        g.DrawString("CPU", smallFont, pen.Brush, 0, baseLine);
                        g.DrawString($"{cpu.Sensors.Where(s => s.SensorType == SensorType.Temperature).FirstOrDefault().Value,2:f0}℃", font, pen.Brush, 10, baseLine + smallFont.Size * 0.5f);
                        baseLine += font.Size * 1.6f;

                        var gpu = computer.Hardware.Where(h => h.HardwareType == HardwareType.GpuNvidia).FirstOrDefault();
                        gpu.Update();
                        g.DrawString("GPU", smallFont, pen.Brush, 0, baseLine);
                        g.DrawString($"{gpu.Sensors.Where(s => s.SensorType == SensorType.Temperature).FirstOrDefault().Value,2:f0}℃", font, pen.Brush, 10, baseLine + smallFont.Size * 0.5f);
                    }
                },

                new Screen() {
                    Delay = -1,
                    Draw = (g) => { }
                }
            };

            KeyLogg.Instance.OnKeyDown += (sender, e) => {
                bool cancel = true;
                if (e.Key == Keys.RWin && e.ModifierKeys == Keys.Control) {
                    if (++current == screens.Count)
                        current = 0;
                } else if (e.Key == Keys.F11 && e.ModifierKeys.HasFlag(Keys.Control) && e.ModifierKeys.HasFlag(Keys.Alt)) {
                    isRunning = false;
                } else {
                    cancel = false;
                }

                if (cancel) {
                    try {
                        tokenSrc?.Cancel();
                    } catch (ObjectDisposedException) { }
                }
            };

            using (disp = new OLED(new UART(diplayPort, baudrate, displayAddr)))
            using (var img = new Bitmap(disp.Width, disp.Height))
            using (var g = Graphics.FromImage(img)) {
                while (isRunning) {
                    g.Clear(Color.Black);

                    screens[current].Draw(g);
                    disp.Display(img);

                    using (tokenSrc = new CancellationTokenSource()) {
                        try {
                            await Task.Delay(screens[current].Delay, tokenSrc.Token);
                        } catch (OperationCanceledException) { }
                    }
                }
            }

            computer.Close();
        }
    }
}
