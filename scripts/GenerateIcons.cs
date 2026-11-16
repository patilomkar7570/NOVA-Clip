using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public class Program
{
    public static void Main(string[] args)
    {
        string rawMarkPath = @"C:\Users\dudha\.gemini\antigravity\brain\67168ea5-3663-4547-bdc4-eadf8018640a\.user_uploaded\media_1794342971394_a40b8fb9.png";
        string rootDir = @"c:\Users\dudha\OneDrive\Documents\Day Projects\Nova Clip";

        Console.WriteLine("Generating White Box + Black Logo assets...");
        using (Bitmap hiRes = CreateWhiteBoxLogo(rawMarkPath, 1024))
        {
            // 1. Assets logo.png & App Resources logo.png
            string assetLogo = Path.Combine(rootDir, "assets", "logo.png");
            string appResLogo = Path.Combine(rootDir, "src", "NOVA.Clip.App", "Resources", "logo.png");
            hiRes.Save(assetLogo, ImageFormat.Png);
            hiRes.Save(appResLogo, ImageFormat.Png);
            Console.WriteLine("Saved logo.png");

            // 2. Multi-res icons
            string assetIco = Path.Combine(rootDir, "assets", "icon.ico");
            string appResIco = Path.Combine(rootDir, "src", "NOVA.Clip.App", "Resources", "app.ico");
            SaveMultiResIcon(hiRes, assetIco);
            SaveMultiResIcon(hiRes, appResIco);
            Console.WriteLine("Saved icon.ico and app.ico");

            // 3. wizard-small.bmp (55x55)
            string wizSmall = Path.Combine(rootDir, "assets", "wizard-small.bmp");
            using (Bitmap small = new Bitmap(55, 55, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(small))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(hiRes, new Rectangle(3, 3, 49, 49));
                small.Save(wizSmall, ImageFormat.Bmp);
            }
            Console.WriteLine("Saved wizard-small.bmp");

            // 4. wizard.bmp (164x314)
            string wizLarge = Path.Combine(rootDir, "assets", "wizard.bmp");
            using (Bitmap wiz = new Bitmap(164, 314, PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(wiz))
            {
                g.Clear(Color.FromArgb(248, 250, 252));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(hiRes, new Rectangle(27, 45, 110, 110));
                using (Pen linePen = new Pen(Color.FromArgb(226, 232, 240), 1))
                {
                    g.DrawLine(linePen, 20, 260, 144, 260);
                }
                wiz.Save(wizLarge, ImageFormat.Bmp);
            }
            Console.WriteLine("Saved wizard.bmp");
        }
        Console.WriteLine("All logo & icon assets successfully generated!");
    }

    private static GraphicsPath CreateRoundedPath(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2f;
        RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Bitmap CreateWhiteBoxLogo(string rawMarkPath, int size = 1024)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float margin = size * 0.05f;
            float boxSize = size - (margin * 2f);
            float radius = boxSize * 0.22f;
            RectangleF boxRect = new RectangleF(margin, margin, boxSize, boxSize);

            // Subtle drop shadow
            for (int i = 8; i >= 1; i--)
            {
                float expand = i * 2f;
                RectangleF shadowRect = new RectangleF(margin - i, margin - i + (i * 1.2f), boxSize + expand, boxSize + expand);
                using (GraphicsPath sPath = CreateRoundedPath(shadowRect, radius + i))
                using (SolidBrush sBrush = new SolidBrush(Color.FromArgb(5 * (9 - i), 0, 0, 0)))
                {
                    g.FillPath(sBrush, sPath);
                }
            }

            // White box fill
            using (GraphicsPath path = CreateRoundedPath(boxRect, radius))
            {
                using (SolidBrush whiteBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                {
                    g.FillPath(whiteBrush, path);
                }

                // Sleek border outline
                using (Pen borderPen = new Pen(Color.FromArgb(226, 232, 240), Math.Max(1.5f, size * 0.008f)))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // Center the black logo mark
            using (Bitmap raw = new Bitmap(rawMarkPath))
            {
                Rectangle srcRect = new Rectangle(118, 121, 781, 776);
                float markPad = size * 0.20f;
                float markSize = size - (markPad * 2f);
                RectangleF destRect = new RectangleF(markPad, markPad, markSize, markSize);
                g.DrawImage(raw, destRect, srcRect, GraphicsUnit.Pixel);
            }
        }
        return bmp;
    }

    public static void SaveMultiResIcon(Bitmap hiRes, string iconPath)
    {
        int[] sizes = new int[] { 256, 128, 64, 48, 32, 16 };
        using (FileStream fs = new FileStream(iconPath, FileMode.Create))
        using (BinaryWriter bw = new BinaryWriter(fs))
        {
            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)sizes.Length);

            int offset = 6 + (16 * sizes.Length);
            byte[][] pngBuffers = new byte[sizes.Length][];

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                using (Bitmap resized = new Bitmap(s, s, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(resized))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.Clear(Color.Transparent);
                        g.DrawImage(hiRes, new Rectangle(0, 0, s, s));
                    }
                    using (MemoryStream ms = new MemoryStream())
                    {
                        resized.Save(ms, ImageFormat.Png);
                        pngBuffers[i] = ms.ToArray();
                    }
                }
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                byte bW = (byte)(s >= 256 ? 0 : s);
                byte bH = (byte)(s >= 256 ? 0 : s);
                bw.Write(bW);
                bw.Write(bH);
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write((int)pngBuffers[i].Length);
                bw.Write(offset);
                offset += pngBuffers[i].Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngBuffers[i]);
            }
        }
    }
}
