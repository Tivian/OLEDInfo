using Microsoft.Win32;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OLEDInfo {
    class Program {
        private static bool isRunning = true;
        private static readonly PrivateFontCollection fontCollection = new PrivateFontCollection();

        private static readonly byte displayAddress = 0x3C;
        private static readonly float fontSize = 20f;
        private static readonly Font font = new Font("Consolas", fontSize);
        private static readonly Font smallFont = new Font(font.FontFamily, font.Size / 2.5f);
        private static readonly Pen pen = new Pen(Color.White);
        private static readonly int clockDefaultDelay = 1000;
        private static readonly int statDefaultDelay = clockDefaultDelay / 4;

        private static OLED disp;
        private static Computer computer;
        private static CancellationTokenSource tokenSrc;

        private struct Screen {
            public int Delay;
            public Action<Graphics> Draw;
        }

        static async Task Main() {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            AppDomain.CurrentDomain.UnhandledException += CrashLogger;
            fontCollection.AddResourceFont(Properties.Resources._7_segment_mono);

            using (Process p = Process.GetCurrentProcess())
                p.PriorityClass = ProcessPriorityClass.Normal;

            computer = new Computer() {
                CPUEnabled = true,
                RAMEnabled = true,
                MainboardEnabled = true,
                GPUEnabled = true
            };
            computer.Open();

            Action<Graphics, string[], Func<string, string>> printSensors = (g, labels, value) => {
                var baseLine = 0.0f;
                string text = string.Empty;
                SizeF measure = SizeF.Empty;

                foreach (var label in labels) {
                    g.DrawString(label, smallFont, pen.Brush, 0, baseLine);
                    text = value(label);
                    measure = g.MeasureString(text, font);
                    g.DrawString(text, font, pen.Brush, disp.Width - measure.Width - 5, baseLine + smallFont.Size * 0.5f);
                    baseLine += font.Size * 1.6f;
                }
            };

            int current = 0;
            bool blink = false;
            var screens = new List<Screen>() {
                new Screen() { // current time
                    Delay = clockDefaultDelay,
                    Draw = (g) => {
                        var timeFont = new Font(fontCollection.Families[0], font.Size * 1.5f);
                        var time = DateTime.Now;
                        string timeStr = $"{time.ToString("H:mm"),5}";
                        var measure = g.MeasureString(timeStr, timeFont);
                        if (blink = !blink)
                            timeStr = timeStr.Replace(':', ' ');
                        g.DrawString(timeStr, timeFont, pen.Brush, (disp.Width - measure.Width) / 2.0f, (disp.Height - measure.Height) / 2.0f);
                    }
                },

                new Screen() { // CPU / GPU power consumption
                    Delay = statDefaultDelay,
                    Draw = (g) => printSensors(g, new[] { "CPU", "GPU" }, (label) => {
                        var hardware = computer.Hardware.Where(h => h.HardwareType == ((label == "CPU") ? HardwareType.CPU : HardwareType.GpuNvidia)).FirstOrDefault();
                        hardware.Update();
                        return $"{hardware.Sensors.Where(s => s.SensorType == SensorType.Power).FirstOrDefault().Value,6:f2}W";
                    })
                },

                new Screen() { // CPU / GPU temperature
                    Delay = statDefaultDelay,
                    Draw = (g) => printSensors(g, new[] { "CPU", "GPU" }, (label) => {
                        var hardware = computer.Hardware.Where(h => h.HardwareType == ((label == "CPU") ? HardwareType.CPU : HardwareType.GpuNvidia)).FirstOrDefault();
                        hardware.Update();
                        return $"{hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature).LastOrDefault().Value,2:f0}℃";
                    })
                },

                new Screen() { // CPU / GPU load level
                    Delay = statDefaultDelay,
                    Draw = (g) => printSensors(g, new[] { "CPU", "GPU" }, (label) => {
                        string name = (label == "CPU") ? "Total" : "Core";
                        var hardware = computer.Hardware.Where(h => h.HardwareType == ((label == "CPU") ? HardwareType.CPU : HardwareType.GpuNvidia)).FirstOrDefault();
                        hardware.Update();
                        return $"{hardware.Sensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains(name)).FirstOrDefault().Value,4:f2}%";
                    })
                },

                new Screen() { // RAM / VRAM usage
                    Delay = statDefaultDelay,
                    Draw = (g) => printSensors(g, new[] { "RAM             [MB]", "Video RAM       [MB]" }, (label) => {
                        float factor = label.StartsWith("RAM") ? 1000f : 1f;
                        var type = label.StartsWith("RAM") ? HardwareType.RAM : HardwareType.GpuNvidia;
                        var hardware = computer.Hardware.Where(h => h.HardwareType == type).FirstOrDefault();
                        hardware.Update();
                        return $"{hardware.Sensors.Where(s => s.Name.Contains("Used")).FirstOrDefault().Value * factor,8:f0}";
                    })
                },

                new Screen() { // CPU / GPU cooler speed
                    Delay = statDefaultDelay,
                    Draw = (g) => printSensors(g, new[] { "CPU cooler     [RPM]", "GPU fan        [RPM]" }, (label) => {
                        var type = label.Contains("CPU") ? HardwareType.Mainboard : HardwareType.GpuNvidia;
                        var hardware = computer.Hardware.Where(h => h.HardwareType == type).FirstOrDefault();
                        if (label.Contains("CPU"))
                            hardware = hardware.SubHardware[0];
                        hardware.Update();
                        return $"{hardware.Sensors.Where(s => s.SensorType == SensorType.Fan).FirstOrDefault().Value,6:f1}";
                    })
                },

                new Screen() {
                    Delay = -1,
                    Draw = (g) => { }
                }
            };

            KeyLogg.Instance.OnKeyDown += (sender, e) => {
                if (e.Key == Keys.F11 && e.ModifierKeys.HasFlag(Keys.Control)) {
                    if (e.ModifierKeys.HasFlag(Keys.Shift)) {
                        disp?.Restart();
                        return;
                    }

                    if (++current == screens.Count)
                        current = 0;

                    try {
                        tokenSrc?.Cancel();
                    } catch (ObjectDisposedException) { }
                }
            };

            using (var i2c = new I2C(displayAddress))
            using (disp = new OLED(i2c))
            using (var img = new Bitmap(disp.Width, disp.Height))
            using (var g = Graphics.FromImage(img)) {
                g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                void onExit<T>(object sender, T e) {
                    disp.Dispose();
                    SystemEvents.SessionEnded -= onExit;
                    SystemEvents.SessionEnding -= onExit;
                    SystemEvents.EventsThreadShutdown -= onExit;
                }
                SystemEvents.EventsThreadShutdown += onExit;
                SystemEvents.SessionEnding += onExit;
                SystemEvents.SessionEnded += onExit;

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

        private static void CrashLogger(object sender, UnhandledExceptionEventArgs args) {
            var ex = (Exception)args.ExceptionObject;
            var sb = new StringBuilder();
            sb.AppendLine(DateTime.Now.ToString("G"));
            sb.AppendLine($"{ex}");
            File.WriteAllText("crash.log", sb.ToString());
        }
    }
}
