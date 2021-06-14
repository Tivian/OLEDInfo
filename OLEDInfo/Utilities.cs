using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OLEDInfo {
    public static class BitmapExtensions {
        private static byte[,] lum = null;
        public static Bitmap Dither(this Bitmap bitmap) {
            if (lum == null) {
                double[] greyCoef = new[] { 0.2126, 0.7152, 0.0722 };
                lum = new byte[3, 256];

                for (int i = 0; i < 256; i++) {
                    for (int j = 0; j < 3; j++)
                        lum[j, i] = (byte)(greyCoef[j] * i);
                }
            }

            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
            byte[] rawData = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rawData, 0, bytes);

            for (int i = 0; i < bytes; i += 4)
                rawData[i] = (byte)(lum[0, rawData[i]] + lum[1, rawData[i + 1]] + lum[2, rawData[i + 2]]);

            var w = bitmap.Width;
            for (int currentPixel = 0; currentPixel < bytes; currentPixel += 4) {
                byte newPixel = (byte)(rawData[currentPixel] < 135 ? 0 : 255);
                double err = (rawData[currentPixel] - newPixel) / 23.0;

                try {
                    rawData[currentPixel + 0 * 1 - 0] = newPixel;
                    rawData[currentPixel + 4 * 1 - 0] += (byte)(err * 7.0);
                    rawData[currentPixel + 4 * w - 4] += (byte)(err * 3.0);
                    rawData[currentPixel + 4 * w - 0] += (byte)(err * 5.0);
                    rawData[currentPixel + 4 * w + 4] += (byte)(err * 1.0);
                } catch {
                    break;
                }

                rawData[currentPixel + 1] = rawData[currentPixel + 2] = rawData[currentPixel];
            }

            Marshal.Copy(rawData, 0, bmpData.Scan0, bytes);
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }
    }

    public static class ImageExtensions {
        public static Image Scale(this Image src, Size target) {
            return src.Scale(target.Width, target.Height);
        }

        public static Image Scale(this Image src, int width, int height) {
            var img = new Bitmap(width, height);

            float scale = Math.Min((float)img.Width / src.Width, (float)img.Height / src.Height);
            int scaleWidth = (int)(src.Width * scale);
            int scaleHeight = (int)(src.Height * scale);

            using (var g = Graphics.FromImage(img))
                g.DrawImage(src, (int)((img.Width - scaleWidth) / 2.0), (int)((img.Height - scaleHeight) / 2.0), scaleWidth, scaleHeight);

            return img;
        }
    }

    public static class PrivateFontCollectionExtensions {
        public static void AddResourceFont(this PrivateFontCollection collection, byte[] resource) {
            var pinned = GCHandle.Alloc(resource, GCHandleType.Pinned);
            collection.AddMemoryFont(pinned.AddrOfPinnedObject(), resource.Length);
            pinned.Free();
        }
    }

    public class Utilities {
        public static Bitmap ScreenShot() {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            var bitmap = new Bitmap(bounds.Width, bounds.Height);

            using (var g = Graphics.FromImage(bitmap))
                g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);

            return bitmap;
        }
    }
}
